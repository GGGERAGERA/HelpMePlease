using UnityEngine;

public sealed class ProjectileFireBehaviour : MonoBehaviour, IWeaponFireBehaviour
{
    [SerializeField] private GameObject projectilePrefab;

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
        ScaleProjectileVisuals(projectileObject.transform, context.ShotVisualScale);
        ScaleRootCollisionGeometry(
            projectileObject.transform,
            context.ShotVisualScale);

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

    private static void ScaleProjectileVisuals(Transform root, float scale)
    {
        if (root == null || Mathf.Approximately(scale, 1f))
            return;

        // Production projectile visuals live under child transforms. Root
        // collision geometry is scaled separately without changing speed.
        for (int i = 0; i < root.childCount; i++)
            root.GetChild(i).localScale *= scale;
    }

    private static void ScaleRootCollisionGeometry(Transform root, float scale)
    {
        if (root == null || Mathf.Approximately(scale, 1f))
            return;

        Collider2D[] colliders = root.GetComponents<Collider2D>();
        for (int i = 0; i < colliders.Length; i++)
        {
            switch (colliders[i])
            {
                case CircleCollider2D circle:
                    circle.radius *= scale;
                    circle.offset *= scale;
                    break;
                case BoxCollider2D box:
                    box.size *= scale;
                    box.offset *= scale;
                    break;
                case CapsuleCollider2D capsule:
                    capsule.size *= scale;
                    capsule.offset *= scale;
                    break;
                case PolygonCollider2D polygon:
                    for (int path = 0; path < polygon.pathCount; path++)
                    {
                        Vector2[] points = polygon.GetPath(path);
                        for (int point = 0; point < points.Length; point++)
                            points[point] *= scale;
                        polygon.SetPath(path, points);
                    }
                    polygon.offset *= scale;
                    break;
            }
        }
    }
}
