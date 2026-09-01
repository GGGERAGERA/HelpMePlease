using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class BeamFireBehaviour : MonoBehaviour, IWeaponFireBehaviour
{
    [SerializeField] private LayerMask enemyMask;
    [SerializeField] private LaserBeamRenderer beamRenderer;
    [SerializeField, Min(0.01f)] private float baseHitHalfWidth = 0.08f;

    private RaycastHit2D[] hitBuffer = new RaycastHit2D[64];
    private readonly HashSet<EnemyHealth> damagedEnemies = new();
    private readonly HashSet<WorldBreakable> damagedBreakables = new();
    private ContactFilter2D enemyFilter;

    public bool HitEnemyLastFire { get; private set; }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    public float DebugLastContextScale { get; private set; } = 1f;
    public float DebugLastHitHalfWidth { get; private set; }
    public LaserBeamRenderer DebugBeamRenderer => beamRenderer;
#endif

    private void Awake()
    {
        ConfigureFilter();
    }

    public bool Fire(WeaponFireContext context)
    {
        HitEnemyLastFire = false;

        Vector2 endPoint = context.Origin + context.Direction * context.Range;

        float hitHalfWidth = Mathf.Max(
            0.01f,
            baseHitHalfWidth * context.ShotVisualScale);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        DebugLastContextScale = context.ShotVisualScale;
        DebugLastHitHalfWidth = hitHalfWidth;
#endif
        int hitCount;
        do
        {
            hitCount = Physics2D.CircleCast(
                context.Origin,
                hitHalfWidth,
                context.Direction,
                enemyFilter,
                hitBuffer,
                context.Range);

            if (hitCount < hitBuffer.Length)
                break;

            Array.Resize(ref hitBuffer, hitBuffer.Length * 2);
        }
        while (true);

        damagedEnemies.Clear();
        damagedBreakables.Clear();

        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit2D hit = hitBuffer[i];
            hitBuffer[i] = default;
            EnemyHealth enemyHealth = hit.collider.GetComponentInParent<EnemyHealth>();

            if (enemyHealth == null)
            {
                WorldBreakable breakable =
                    hit.collider.GetComponentInParent<WorldBreakable>();

                if (breakable == null ||
                    !damagedBreakables.Add(breakable))
                {
                    continue;
                }

                breakable.TakeDamage(context.Damage, hit.point);
                continue;
            }

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
        ConfigureFilter();
    }

    private void ConfigureFilter()
    {
        enemyFilter = ContactFilter2D.noFilter;
        enemyFilter.SetLayerMask(enemyMask | (1 << 0));
        enemyFilter.useTriggers = true;
    }
}
