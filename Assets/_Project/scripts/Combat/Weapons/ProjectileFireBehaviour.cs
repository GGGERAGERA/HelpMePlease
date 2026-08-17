using UnityEngine;

public sealed class ProjectileFireBehaviour : MonoBehaviour, IWeaponFireBehaviour
{
    [SerializeField] private GameObject projectilePrefab;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    public float DebugLastContextScale { get; private set; } = 1f;
    public Vector3 DebugLastPrefabScale { get; private set; } = Vector3.one;
    public Vector3 DebugLastFinalScale { get; private set; } = Vector3.one;
#endif

    public bool UsesRocketProjectile =>
        projectilePrefab != null &&
        projectilePrefab.GetComponent<RocketProjectile>() != null;

    public WeaponUpgradeCapability UpgradeCapabilities =>
        projectilePrefab != null && projectilePrefab.GetComponent<Bullet>() != null
            ? WeaponUpgradeCapability.Pierce |
              WeaponUpgradeCapability.Ricochet |
              WeaponUpgradeCapability.MultiProjectile |
              WeaponUpgradeCapability.Knockback
            : WeaponUpgradeCapability.None;

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

        GameObject projectileObject = Instantiate(
            projectilePrefab,
            context.Origin,
            Quaternion.Euler(0f, 0f, angle)
        );
        ScaleProjectileGeometry(
            projectileObject.transform,
            context.ShotVisualScale);
        ProductionVisualTuningController.RegisterProjectile(projectileObject);

        IWeaponProjectile projectile = projectileObject.GetComponent<IWeaponProjectile>();

        if (projectile == null)
        {
            Debug.LogWarning("[ProjectileFireBehaviour] Spawned projectile has no IWeaponProjectile.");
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

        ProjectileCombatContext projectileContext =
            projectileObject.GetComponent<ProjectileCombatContext>();

        if (projectileContext == null)
            projectileContext = projectileObject.AddComponent<ProjectileCombatContext>();

        projectileContext.Initialize(context);
        return true;
    }

    private void ScaleProjectileGeometry(Transform root, float scale)
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
