using System.Collections.Generic;
using UnityEngine;

public sealed class BeamFireBehaviour : MonoBehaviour, IWeaponFireBehaviour
{
    [SerializeField] private LayerMask enemyMask;
    [SerializeField] private LaserBeamRenderer beamRenderer;
    [SerializeField, Min(0.01f)] private float baseHitHalfWidth = 0.08f;

    public bool HitEnemyLastFire { get; private set; }

    public bool Fire(WeaponFireContext context)
    {
        HitEnemyLastFire = false;

        Vector2 endPoint = context.Origin + context.Direction * context.Range;

        float hitHalfWidth = Mathf.Max(
            0.01f,
            baseHitHalfWidth * context.ShotVisualScale);
        RaycastHit2D[] hits = Physics2D.CircleCastAll(
            context.Origin,
            hitHalfWidth,
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
                context.CreateHitContext(
                    enemyHealth,
                    hit.point != Vector2.zero
                        ? hit.point
                        : hit.collider.ClosestPoint(context.Origin))
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

    private void OnValidate()
    {
        baseHitHalfWidth = Mathf.Max(0.01f, baseHitHalfWidth);
    }
}
