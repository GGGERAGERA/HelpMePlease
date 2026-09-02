using System;
using System.Collections.Generic;
using UnityEngine;

namespace Subject42.Combat.OrbitalStation
{
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

    public interface IOrbitalProgressionAdapter
    {
        OrbitalRingRuntime AddRing();
        bool RemoveRing(int stableRingId, out string error);
        void BeginModulePlacement(OrbitalModuleKind kind);
        bool InstallModule(OrbitalModuleKind kind, int stableRingId,
            int mountIndex, out string error);
        bool MoveModule(int stableModuleId, int targetRingId,
            int targetMountIndex, out string error);
        bool RemoveModule(int stableModuleId);
        bool UpgradeRingSpeed(int stableRingId);
        bool UpgradeRingPower(int stableRingId);
        bool AddMount(int stableRingId, out string error);
        void UpgradeSelectedRingSpeed();
        void UpgradeSelectedRingPower();
        void AddMount();
        void UpgradeCore();
        bool UpgradeLinkMatrix();
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
        private sealed class Projectile
        {
            public GameObject GameObject;
            public SpriteRenderer Renderer;
            public EnemyHealth Target;
            public float Speed;
            public float Damage;
            public bool Active;
        }

        private readonly Transform root;
        private readonly Sprite sprite;
        private readonly List<Projectile> projectiles = new();

        public event Action<EnemyHealth, float> Hit;
        public event Action<EnemyHealth> Death;

        public ProductionOrbitalCombatAdapter(Transform runtimeRoot, Sprite sharedSprite)
        {
            root = runtimeRoot;
            sprite = sharedSprite;
        }

        public EnemyHealth FindNearest(Vector2 origin, float range)
        {
            EnemyHealth best = null;
            float bestDistance = range * range;
            foreach (EnemyHealth enemy in EnemyHealth.ActiveInstances)
            {
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
            List<EnemyHealth> result = new();
            foreach (EnemyHealth enemy in EnemyHealth.ActiveInstances)
            {
                if (IsValid(enemy) && Vector2.Distance(origin, enemy.transform.position) <= range)
                    result.Add(enemy);
            }
            result.Sort((a, b) =>
                ((Vector2)a.transform.position - origin).sqrMagnitude.CompareTo(
                ((Vector2)b.transform.position - origin).sqrMagnitude));
            if (result.Count > count)
                result.RemoveRange(count, result.Count - count);
            return result;
        }

        public bool ApplyDamage(EnemyHealth enemy, float damage, Vector2 hitPoint)
        {
            if (!IsValid(enemy))
                return false;
            bool wasAlive = !enemy.IsDead;
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
                GameObject gameObject = new("Orbital Projectile");
                SpriteRenderer renderer = gameObject.AddComponent<SpriteRenderer>();
                renderer.sprite = sprite;
                renderer.sortingOrder = 15;
                projectile = new Projectile { GameObject = gameObject, Renderer = renderer };
                projectiles.Add(projectile);
            }
            projectile.Active = true;
            projectile.Target = target;
            projectile.Speed = Mathf.Max(1f, speed);
            projectile.Damage = damage;
            projectile.GameObject.transform.position = origin;
            projectile.GameObject.transform.localScale = new Vector3(0.14f, 0.07f, 1f);
            projectile.Renderer.color = color;
            projectile.GameObject.SetActive(true);
        }

        public void Tick(float deltaTime)
        {
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
