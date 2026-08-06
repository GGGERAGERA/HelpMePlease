using UnityEngine;

public sealed class ProjectileFireBehaviour : MonoBehaviour, IWeaponFireBehaviour
{
    [SerializeField] private GameObject projectilePrefab;

    public bool UsesRocketProjectile =>
        projectilePrefab != null &&
        projectilePrefab.GetComponent<RocketProjectile>() != null;

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

        projectileContext.Initialize(context.Modifiers);
        return true;
    }
}
