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
        bool limitedCoreApplied = false;

        foreach (RaycastHit2D hit in hits)
        {
            EnemyHealth enemyHealth = hit.collider.GetComponentInParent<EnemyHealth>();

            if (enemyHealth == null)
                continue;

            if (damagedEnemies.Contains(enemyHealth))
                continue;

            damagedEnemies.Add(enemyHealth);
            HitEnemyLastFire = true;

            bool usesSingleSourceCore = context.Core == WeaponCoreType.Chain ||
                context.Core == WeaponCoreType.Void;
            WeaponHitContext hitContext = new WeaponHitContext(
                context.Weapon,
                context.Owner,
                context.Weapon,
                context.ShotKind,
                context.Core,
                enemyHealth,
                hit.point,
                context.Direction,
                context.Damage,
                context.IsCritical
            );
            WeaponHitResolver.Resolve(
                hitContext,
                !usesSingleSourceCore || !limitedCoreApplied
            );

            if (usesSingleSourceCore)
                limitedCoreApplied = true;

            CombatExplosionService.TryExplodeOnHit(
                hit.point,
                context.Damage,
                context.Modifiers,
                enemyMask
            );
        }

        if (beamRenderer != null)
            beamRenderer.Render(context.Origin, endPoint);

        return true;
    }
}
