using System.Collections.Generic;
using UnityEngine;

public sealed class BeamFireBehaviour : MonoBehaviour, IWeaponFireBehaviour
{
    [SerializeField] private LayerMask enemyMask;
    [SerializeField] private LaserBeamRenderer beamRenderer;

    public bool HitEnemyLastFire { get; private set; }

    public bool Fire(WeaponFireContext context)
    {
        HitEnemyLastFire = false;

        Vector2 endPoint = context.Origin + context.Direction * context.Range;

        RaycastHit2D[] hits = Physics2D.RaycastAll(
            context.Origin,
            context.Direction,
            context.Range,
            enemyMask
        );

        HashSet<EnemyHealth> damagedEnemies = new HashSet<EnemyHealth>();

        foreach (RaycastHit2D hit in hits)
        {
            EnemyHealth enemyHealth = hit.collider.GetComponentInParent<EnemyHealth>();

            if (enemyHealth == null)
                continue;

            if (damagedEnemies.Contains(enemyHealth))
                continue;

            damagedEnemies.Add(enemyHealth);
            HitEnemyLastFire = true;

            WeaponHitResolver.Resolve(
                context.CreateHitContext(enemyHealth, hit.point)
            );

            CombatExplosionService.TryExplodeOnHit(
                hit.point,
                context.Damage,
                context.Modifiers,
                enemyMask
            );
        }

        if (beamRenderer != null)
            beamRenderer.Render(
                context.Origin,
                endPoint,
                context.ShotVisualScale);

        return true;
    }
}
