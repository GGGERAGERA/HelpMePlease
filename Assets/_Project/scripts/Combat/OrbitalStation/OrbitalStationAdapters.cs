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
        bool InterceptProjectile(EnemyHealth target, Vector2 from, Vector2 to, float damage, Vector2 incomingDirection);
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
        public bool LabStraightProjectiles { get; set; }
        public Func<OrbitalStationRuntime, OrbitalModuleState, OrbitalModuleRuntime> LabModuleFactory;
        public sealed class LabVolley
        {
            public readonly long Id;
            internal readonly HashSet<EnemyHealth> Shields = new();
            internal readonly HashSet<EnemyHealth> Pushed = new();
            public LabVolley(long id) => Id = id;
        }
        public void SpawnLabProjectile(Vector2 origin, EnemyHealth target, Vector2 direction,
            EnemyOrbitalLabWeaponSettings settings, LabVolley volley, float damage)
        {
            Projectile shot = SpawnProjectileCore(origin, target, settings.ProjectileSpeed, damage,
                settings.ProjectileVisual);
            if (shot == null) return;
            shot.Straight = true; shot.Target = null; shot.Position = origin; shot.Direction = direction.normalized;
            shot.RemainingRange = settings.Range; shot.Radius = settings.ProjectileRadius;
            shot.BodiesRemaining = Mathf.Max(1, settings.BodyPierces); shot.Volley = volley;
            shot.Knockback = settings.Knockback;
            shot.GameObject.transform.right = shot.Direction;
        }
        // Pistol targeting reaches 8 m; the legacy 0.2 m body visual is scaled by 1.4 in this lab.
        private const float LabProjectileRange = 8f;
        private const float LabProjectileRadius = .14f;
        private readonly List<RaycastHit2D> projectileHits = new();
        private ContactFilter2D projectileFilter = new() { useTriggers = true };
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetLabInterceptor() => LabInterceptor = null;
        public void DebugClearProjectiles() { foreach (var projectile in projectiles) if (projectile.Active) Release(projectile); }
        public Action<Vector2, Vector2, bool> LabBodyImpact;
        public void PrewarmLabProjectiles(GameObject prefab, int count)
        { for (int i = 0; i < count; i++) CreateProjectile(prefab); }
        public void ConfigureLabProjectileVisual(GameObject visualPrefab)
        {
            if (visualPrefab == null) throw new ArgumentNullException(nameof(visualPrefab));
            foreach (var projectile in projectiles)
                UnityEngine.Object.Destroy(projectile.GameObject);
            projectiles.Clear();
            projectilePrefab = visualPrefab;
            for (int i = 0; i < OrbitalPresentationConfig.Active.ProjectilePrewarmCount; i++) CreateProjectile();
        }
#endif
        private sealed class Projectile
        {
            public GameObject GameObject;
            public EnemyHealth Target;
            public float Speed;
            public float Damage;
            public bool Active;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            public ParticleSystem[] Particles;
            public TrailRenderer[] Trails;
            public bool Straight;
            public Vector2 Position, Direction;
            public float RemainingRange;
            public float Radius, Knockback;
            public int BodiesRemaining;
            public LabVolley Volley;
            public readonly HashSet<EnemyHealth> HitBodies = new();
            public GameObject SourcePrefab;
#endif
        }

        private readonly Transform root;
        private GameObject projectilePrefab;
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

        private Projectile CreateProjectile(GameObject source = null)
        {
            GameObject gameObject = UnityEngine.Object.Instantiate(source != null ? source : projectilePrefab, root, false);
            // Hits remain owned by this adapter, not the authored prefab's trigger.
            foreach (var collider in gameObject.GetComponentsInChildren<Collider2D>(true))
                collider.enabled = false;
            var projectile = new Projectile { GameObject = gameObject };
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            projectile.Particles = gameObject.GetComponentsInChildren<ParticleSystem>(true);
            projectile.Trails = gameObject.GetComponentsInChildren<TrailRenderer>(true);
            projectile.SourcePrefab = source != null ? source : projectilePrefab;
#endif
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
            => SpawnProjectileCore(origin, target, speed, damage, null);

        private Projectile SpawnProjectileCore(Vector2 origin, EnemyHealth target, float speed,
            float damage, GameObject source)
        {
            if (!IsValid(target))
                return null;
            Projectile projectile = null;
            for (int i = 0; i < projectiles.Count; i++)
            {
                if (!projectiles[i].Active
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    && projectiles[i].SourcePrefab == (source != null ? source : projectilePrefab)
#endif
                    )
                {
                    projectile = projectiles[i];
                    break;
                }
            }
            if (projectile == null)
            {
                projectile = CreateProjectile(source);
            }
            projectile.Active = true;
            projectile.Target = target;
            projectile.Speed = Mathf.Max(1f, speed);
            projectile.Damage = damage;
            projectile.GameObject.transform.position = origin;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            projectile.Straight = LabStraightProjectiles;
            projectile.Radius = LabProjectileRadius; projectile.BodiesRemaining = 1;
            projectile.Knockback = 0f; projectile.Volley = null; projectile.HitBodies.Clear();
            if (projectile.Straight)
            {
                projectile.Position = origin;
                projectile.Direction = ((Vector2)target.transform.position - origin).normalized;
                projectile.RemainingRange = LabProjectileRange;
                projectile.Target = null; // A fired shot no longer owns or follows its aim target.
            }
            if (projectile.Particles.Length > 0 || projectile.Trails.Length > 0)
            {
                projectile.GameObject.transform.right = (Vector2)target.transform.position - origin;
                foreach (var trail in projectile.Trails) trail.Clear();
                foreach (var particles in projectile.Particles) particles.Clear(true);
            }
#endif
            projectile.GameObject.SetActive(true);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            foreach (var particles in projectile.Particles) particles.Play(true);
#endif
            return projectile;
        }

        public void Tick(float deltaTime)
        {
            if (deltaTime <= 0f) return;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            LabInterceptor?.TickContacts(deltaTime);
            if (LabStraightProjectiles) Physics2D.SyncTransforms();
#endif
            for (int i = 0; i < projectiles.Count; i++)
            {
                Projectile projectile = projectiles[i];
                if (!projectile.Active)
                    continue;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                if (projectile.Straight)
                {
                    TickStraightProjectile(projectile, deltaTime);
                    continue;
                }
#endif
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
                if (LabInterceptor != null && LabInterceptor.InterceptProjectile(projectile.Target, position, next, projectile.Damage, next - position))
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

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private void TickStraightProjectile(Projectile projectile, float deltaTime)
        {
            // Stored world position prevents station/player translation from dragging an airborne shot.
            Vector2 from = projectile.Position;
            float distance = Mathf.Min(projectile.Speed * deltaTime, projectile.RemainingRange);
            projectileHits.Clear();
            Physics2D.CircleCast(from, projectile.Radius, projectile.Direction,
                projectileFilter, projectileHits, distance);
            // Process the complete swept segment in travel order; piercing cannot skip a plate.
            projectileHits.Sort((a, b) => a.distance.CompareTo(b.distance));
            foreach (var hit in projectileHits)
            {
                var enemy = hit.collider.GetComponentInParent<EnemyHealth>();
                if (!IsValid(enemy) || enemy.gameObject.scene != root.gameObject.scene) continue;
                Vector2 point = hit.point;
                if (enemy.TryGetComponent<EnemyOrbitalLabShield>(out var shield) && hit.collider == shield.ShieldCollider)
                {
                    point = hit.collider.ClosestPoint(point);
                    bool consume = projectile.Volley == null || !projectile.Volley.Shields.Contains(enemy);
                    if (!shield.BlockProjectile(projectile.Direction, point, consume)) continue;
                    projectile.Volley?.Shields.Add(enemy);
                    Release(projectile); return; // Includes the hit that breaks the shield.
                }
                if (!projectile.HitBodies.Add(enemy)) continue;
                ApplyDamage(enemy, projectile.Damage, point);
                LabBodyImpact?.Invoke(point, projectile.Direction, projectile.BodiesRemaining > 1);
                if (projectile.Knockback > 0f && !enemy.IsDead && enemy.TryGetComponent<EnemyChaseMovement>(out var movement) &&
                    (projectile.Volley == null || projectile.Volley.Pushed.Add(enemy)))
                    movement.ApplyKnockback(projectile.Direction, Mathf.Clamp(projectile.Knockback, 0f, 5f));
                if (--projectile.BodiesRemaining <= 0) { Release(projectile); return; }
            }
            projectile.Position = from + projectile.Direction * distance;
            projectile.GameObject.transform.position = projectile.Position;
            projectile.GameObject.transform.right = projectile.Direction;
            projectile.RemainingRange -= distance;
            if (projectile.RemainingRange <= 0f) Release(projectile);
        }
#endif

        private static bool IsValid(EnemyHealth enemy) =>
            enemy != null && enemy.isActiveAndEnabled && !enemy.IsDead;

        private static void Release(Projectile projectile)
        {
            projectile.Active = false;
            projectile.Target = null;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            projectile.Volley = null; projectile.HitBodies.Clear();
            foreach (var trail in projectile.Trails) trail.Clear();
            foreach (var particles in projectile.Particles)
                particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
#endif
            if (projectile.GameObject != null)
                projectile.GameObject.SetActive(false);
        }
    }
}
