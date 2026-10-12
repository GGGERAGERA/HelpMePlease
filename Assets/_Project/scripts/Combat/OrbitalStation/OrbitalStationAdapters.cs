using System;
using System.Collections.Generic;
using UnityEngine;

namespace Subject42.Combat.OrbitalStation
{
#if UNITY_EDITOR || DEVELOPMENT_BUILD
    // Optional scene-local lab interception. Release gameplay has no hook or dependency on lab types.
    public interface IOrbitalCombatLabInterceptor
    {
        bool HandlesContact(OrbitalModuleRuntime module);
        void TickContacts(float deltaTime);
        bool TryResolveDamage(EnemyHealth target, float damage, Vector2 point, out bool applied);
        bool InterceptProjectile(EnemyHealth target, Vector2 from, Vector2 to, float damage);
    }
#endif
    public interface IOrbitalOwnerAdapter
    {
        Transform Transform { get; }
        GameObject DamageOwner { get; }
        Vector2 MoveDirection { get; }
        bool CanControl { get; }
        bool IsDead { get; }
    }

    public interface IOrbitalCombatAdapter
    {
        event Action<EnemyHealth, float> Hit;
        event Action<EnemyHealth> Death;
        EnemyHealth FindNearest(Vector2 origin, float range);
        List<EnemyHealth> FindNearestMany(Vector2 origin, float range, int count);
        bool ApplyDamage(EnemyHealth enemy, float damage, Vector2 hitPoint);
        void SpawnProjectile(Vector2 origin, EnemyHealth target, float speed,
            float damage, Color color);
        void Tick(float deltaTime);
        void Teardown();
    }

    public sealed class ProductionOrbitalOwnerAdapter : IOrbitalOwnerAdapter
    {
        private readonly GameObject player;
        private readonly CharacterMovement2D movement;
        private readonly PlayerHealth health;
        private readonly Rigidbody2D body;

        public ProductionOrbitalOwnerAdapter(GameObject owner)
        {
            player = owner;
            movement = owner != null ? owner.GetComponent<CharacterMovement2D>() : null;
            health = owner != null ? owner.GetComponent<PlayerHealth>() : null;
            body = owner != null ? owner.GetComponent<Rigidbody2D>() : null;
        }

        public Transform Transform => player != null ? player.transform : null;
        public GameObject DamageOwner => player;
        public bool IsDead => player == null || (health != null && health.IsDead);
        public bool CanControl => !IsDead && (movement == null || movement.enabled);
        public Vector2 MoveDirection
        {
            get
            {
                if (body != null && body.linearVelocity.sqrMagnitude > 0.01f)
                    return body.linearVelocity.normalized;
                return movement != null ? movement.LastMoveDirection : Vector2.right;
            }
        }
    }

    public sealed class ProductionOrbitalCombatAdapter : IOrbitalCombatAdapter
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        public static IOrbitalCombatLabInterceptor LabInterceptor { get; set; }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetLabInterceptor() => LabInterceptor = null;
        public void DebugClearProjectiles() { foreach (var projectile in projectiles) if (projectile.Active) Release(projectile); }
#endif
        private sealed class Projectile
        {
            public GameObject GameObject;
            public EnemyHealth Target;
            public float Speed;
            public float Damage;
            public bool Active;
        }

        private readonly Transform root;
        private readonly GameObject projectilePrefab;
        private readonly List<Projectile> projectiles = new();
        // Borrowed by the synchronous Arc attack until the next query on this adapter.
        private readonly List<EnemyHealth> nearestTargets = new();
        private readonly System.Comparison<EnemyHealth> nearestComparison;
        private Vector2 nearestOrigin;

        public event Action<EnemyHealth, float> Hit;
        public event Action<EnemyHealth> Death;

        public ProductionOrbitalCombatAdapter(Transform runtimeRoot, Sprite sharedSprite)
        {
            root = runtimeRoot;
            projectilePrefab = OrbitalPresentationConfig.Active.PistolProjectilePrefab;
            nearestComparison = CompareNearestTargets;
            for (int i = 0; i < OrbitalPresentationConfig.Active.ProjectilePrewarmCount; i++)
                CreateProjectile();
        }

        private Projectile CreateProjectile()
        {
            GameObject gameObject = UnityEngine.Object.Instantiate(projectilePrefab, root, false);
            // Hits remain owned by this adapter, not the authored prefab's trigger.
            foreach (var collider in gameObject.GetComponentsInChildren<Collider2D>(true))
                collider.enabled = false;
            var projectile = new Projectile { GameObject = gameObject };
            gameObject.SetActive(false);
            projectiles.Add(projectile);
            return projectile;
        }

        public EnemyHealth FindNearest(Vector2 origin, float range)
        {
            EnemyHealth best = null;
            float bestDistance = range * range;
            var enemies = EnemyHealth.GetActiveEnumerator();
            while (enemies.MoveNext())
            {
                EnemyHealth enemy = enemies.Current;
                if (!IsValid(enemy))
                    continue;
                float distance = ((Vector2)enemy.transform.position - origin).sqrMagnitude;
                if (distance >= bestDistance)
                    continue;
                bestDistance = distance;
                best = enemy;
            }
            return best;
        }

        public List<EnemyHealth> FindNearestMany(Vector2 origin, float range, int count)
        {
            var result = nearestTargets;
            result.Clear();
            nearestOrigin = origin;
            var enemies = EnemyHealth.GetActiveEnumerator();
            while (enemies.MoveNext())
            {
                EnemyHealth enemy = enemies.Current;
                if (IsValid(enemy) && Vector2.Distance(origin, enemy.transform.position) <= range)
                    result.Add(enemy);
            }
            result.Sort(nearestComparison);
            if (result.Count > count)
                result.RemoveRange(count, result.Count - count);
            return result;
        }

        private int CompareNearestTargets(EnemyHealth a, EnemyHealth b) =>
            ((Vector2)a.transform.position - nearestOrigin).sqrMagnitude.CompareTo(
                ((Vector2)b.transform.position - nearestOrigin).sqrMagnitude);

        public bool ApplyDamage(EnemyHealth enemy, float damage, Vector2 hitPoint)
        {
            if (!IsValid(enemy))
                return false;
            bool wasAlive = !enemy.IsDead;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (LabInterceptor != null && LabInterceptor.TryResolveDamage(enemy, damage, hitPoint, out bool applied))
            {
                if (applied) { Hit?.Invoke(enemy, damage); if (enemy != null && enemy.IsDead) Death?.Invoke(enemy); }
                return applied;
            }
#endif
            enemy.TakeDamage(Mathf.Max(0f, damage), hitPoint);
            Hit?.Invoke(enemy, damage);
            if (wasAlive && enemy != null && enemy.IsDead)
                Death?.Invoke(enemy);
            return true;
        }

        public void SpawnProjectile(Vector2 origin, EnemyHealth target, float speed,
            float damage, Color color)
        {
            if (!IsValid(target))
                return;
            Projectile projectile = null;
            for (int i = 0; i < projectiles.Count; i++)
            {
                if (!projectiles[i].Active)
                {
                    projectile = projectiles[i];
                    break;
                }
            }
            if (projectile == null)
            {
                projectile = CreateProjectile();
            }
            projectile.Active = true;
            projectile.Target = target;
            projectile.Speed = Mathf.Max(1f, speed);
            projectile.Damage = damage;
            projectile.GameObject.transform.position = origin;
            projectile.GameObject.SetActive(true);
        }

        public void Tick(float deltaTime)
        {
            if (deltaTime <= 0f) return;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            LabInterceptor?.TickContacts(deltaTime);
#endif
            for (int i = 0; i < projectiles.Count; i++)
            {
                Projectile projectile = projectiles[i];
                if (!projectile.Active)
                    continue;
                if (!IsValid(projectile.Target))
                {
                    Release(projectile);
                    continue;
                }
                Vector2 position = projectile.GameObject.transform.position;
                Vector2 target = projectile.Target.transform.position;
                Vector2 next = Vector2.MoveTowards(position, target,
                    projectile.Speed * deltaTime);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                if (LabInterceptor != null && LabInterceptor.InterceptProjectile(projectile.Target, position, next, projectile.Damage))
                { Release(projectile); continue; }
#endif
                projectile.GameObject.transform.position = next;
                Vector2 direction = target - position;
                if (direction.sqrMagnitude > 0.001f)
                    projectile.GameObject.transform.right = direction;
                if ((next - target).sqrMagnitude > 0.025f)
                    continue;
                ApplyDamage(projectile.Target, projectile.Damage, next);
                Release(projectile);
            }
        }

        public void Teardown()
        {
            Hit = null;
            Death = null;
            for (int i = 0; i < projectiles.Count; i++)
                if (projectiles[i].GameObject != null)
                    UnityEngine.Object.Destroy(projectiles[i].GameObject);
            projectiles.Clear();
        }

        private static bool IsValid(EnemyHealth enemy) =>
            enemy != null && enemy.isActiveAndEnabled && !enemy.IsDead;

        private static void Release(Projectile projectile)
        {
            projectile.Active = false;
            projectile.Target = null;
            if (projectile.GameObject != null)
                projectile.GameObject.SetActive(false);
        }
    }
}
