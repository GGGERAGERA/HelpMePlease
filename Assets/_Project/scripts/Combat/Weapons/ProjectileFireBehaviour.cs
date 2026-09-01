using UnityEngine;

public sealed class ProjectileFireBehaviour : MonoBehaviour, IWeaponFireBehaviour
{
    [SerializeField] private GameObject projectilePrefab;
    [Header("Runtime Pool")]
    [SerializeField, Min(0)] private int prewarmCount = 24;
    [SerializeField, Min(1)] private int maximumPoolSize = 192;

    private SimplePrefabPool projectilePool;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    public float DebugLastContextScale { get; private set; } = 1f;
    public Vector3 DebugLastPrefabScale { get; private set; } = Vector3.one;
    public Vector3 DebugLastFinalScale { get; private set; } = Vector3.one;
#endif

    public bool UsesExplosiveProjectile =>
        projectilePrefab != null &&
        projectilePrefab.GetComponent<ExplosiveProjectile>() != null;

    public WeaponUpgradeCapability UpgradeCapabilities =>
        projectilePrefab != null && projectilePrefab.GetComponent<Bullet>() != null
            ? WeaponUpgradeCapability.Pierce |
              WeaponUpgradeCapability.Ricochet |
              WeaponUpgradeCapability.MultiProjectile |
              WeaponUpgradeCapability.Knockback
            : WeaponUpgradeCapability.None;

    private void Awake()
    {
        EnsurePool();
    }

    public bool Fire(WeaponFireContext context)
    {
        if (projectilePrefab == null)
        {
            Debug.LogWarning("[ProjectileFireBehaviour] Projectile prefab is missing.");
            return false;
        }

        if (!float.IsFinite(context.Direction.x) ||
            !float.IsFinite(context.Direction.y) ||
            context.Direction.sqrMagnitude < 0.001f)
        {
            Debug.LogWarning("[ProjectileFireBehaviour] Invalid fire direction.");
            return false;
        }

        float angle = Mathf.Atan2(context.Direction.y, context.Direction.x) * Mathf.Rad2Deg;

        EnsurePool();
        Quaternion rotation = Quaternion.Euler(0f, 0f, angle);
        PooledGameObject pooled = projectilePool?.Get(
            context.Origin,
            rotation,
            context.ShotVisualScale,
            true);
        GameObject projectileObject;
        IWeaponProjectile projectile;

        if (pooled != null)
        {
            projectileObject = pooled.gameObject;
            projectile = pooled.WeaponProjectile;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            DebugLastContextScale = Mathf.Max(
                0.1f,
                context.ShotVisualScale);
            DebugLastPrefabScale = pooled.AuthoredScale;
            DebugLastFinalScale = projectileObject.transform.localScale;
#endif
        }
        else
        {
            // Safe fallback for teardown/editor edge cases.
            projectileObject = Instantiate(
                projectilePrefab,
                context.Origin,
                rotation);
            ScaleFallbackProjectileGeometry(
                projectileObject.transform,
                context.ShotVisualScale);
            projectile = projectileObject.GetComponent<IWeaponProjectile>();
        }

        ProductionVisualTuningController.RegisterProjectile(projectileObject);

        if (projectile == null)
        {
            Debug.LogWarning("[ProjectileFireBehaviour] Spawned projectile has no IWeaponProjectile.");
            if (pooled == null || !pooled.Release())
                Destroy(projectileObject);
            return false;
        }

        projectile.Initialize(
            context.Damage,
            context.ProjectileSpeed,
            context.Range,
            context.Direction,
            context.Pierce,
            context.IsCritical,
            context.Ricochet,
            context.KnockbackForce
        );

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        PhysicalCombatFeedbackRuntime.RegisterProjectile(
            projectileObject, context.Direction);
#endif

        ProjectileCombatContext projectileContext = pooled != null
            ? pooled.CombatContext
            : projectileObject.GetComponent<ProjectileCombatContext>();

        if (projectileContext == null)
            projectileContext = projectileObject.AddComponent<ProjectileCombatContext>();

        projectileContext.Initialize(context);
        return true;
    }

    private void EnsurePool()
    {
        if (projectilePool != null || projectilePrefab == null)
            return;

        projectilePool = new SimplePrefabPool(
            this,
            projectilePrefab,
            prewarmCount,
            maximumPoolSize);
    }

    private void OnDestroy()
    {
        projectilePool?.Dispose();
        projectilePool = null;
    }

    private void ScaleFallbackProjectileGeometry(Transform root, float scale)
    {
        if (root == null)
            return;

        float finalMultiplier = Mathf.Max(0.1f, scale);
        Vector3 prefabScale = root.localScale;

        // The pistol prefab uses Local particle-system scaling, which ignores
        // the projectile root scale. Switch spawned particles to Hierarchy so
        // the single root scale drives both authored visuals and colliders.
        // This instance is freshly instantiated (not pooled), so prefab data
        // remains untouched and scale cannot accumulate between attacks.
        ParticleSystem[] particles =
            root.GetComponentsInChildren<ParticleSystem>(true);
        for (int i = 0; i < particles.Length; i++)
        {
            ParticleSystem.MainModule main = particles[i].main;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
        }

        root.localScale = prefabScale * finalMultiplier;

        // Trail width is expressed in world units and is not governed by the
        // projectile transform scale. Keep it aligned with the same modifier.
        TrailRenderer[] trails =
            root.GetComponentsInChildren<TrailRenderer>(true);
        for (int i = 0; i < trails.Length; i++)
            trails[i].widthMultiplier *= finalMultiplier;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        DebugLastContextScale = finalMultiplier;
        DebugLastPrefabScale = prefabScale;
        DebugLastFinalScale = root.localScale;
#endif
    }
}
