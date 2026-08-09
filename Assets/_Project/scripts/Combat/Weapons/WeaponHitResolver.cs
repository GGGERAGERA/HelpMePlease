using System.Collections.Generic;
using UnityEngine;

public static class WeaponHitResolver
{
    // Temporary gameplay knobs for Prototype Plan Step 1.
    private const float RuptureDamageMultiplier = 0.65f;
    private const float RuptureRadius = 2.2f;
    private const float RocketRuptureRadius = 2.7f;
    private const int MaxRuptureDepth = 3;

    private const float ChainDamageMultiplier = 0.45f;
    private const float ChainRadius = 4f;
    private const int ChainTargetsPerHit = 1;
    private const int MaxRocketChainHits = 4;
    private const float LaserChainCooldown = 0.2f;

    private const float VoidDamageMultiplier = 0.55f;
    private const float RocketVoidDamageMultiplier = 0.45f;
    private const float VoidRange = 5f;
    private const float RocketVoidRange = 4.5f;
    private const int RocketVoidRayCount = 3;
    private const float LaserVoidCooldown = 0.2f;

    private struct ChainBudget
    {
        public int Frame;
        public int Used;
    }

    private static readonly Dictionary<int, ChainBudget> rocketChainBudgets =
        new();
    private static readonly List<int> staleBudgetKeys = new();

    public static void Resolve(
        in WeaponHitContext context,
        bool applyCore = true)
    {
        if (context.Target == null || context.Target.IsDead)
            return;

        context.Target.TakeDamage(
            context.Damage,
            context.HitPoint,
            context.IsCritical
        );

        if (applyCore)
            ResolveCoreImpact(context);
    }

    public static void ResolveCoreImpact(in WeaponHitContext context)
    {
        switch (context.Core)
        {
            case WeaponCoreType.Rupture:
                ApplyRupture(context);
                break;
            case WeaponCoreType.Chain:
                ApplyChain(context);
                break;
            case WeaponCoreType.Void:
                ApplyVoid(context);
                break;
        }
    }

    private static void ApplyRupture(in WeaponHitContext context)
    {
        if (context.Target == null || !context.Target.IsDead ||
            context.PropagationDepth >= MaxRuptureDepth)
        {
            return;
        }

        Vector2 center = context.Target.transform.position;
        float radius = context.ShotKind == WeaponShotKind.Rocket
            ? RocketRuptureRadius
            : RuptureRadius;
        float damage = context.Damage * RuptureDamageMultiplier;

        WeaponCoreDebugVisual.DrawRing(
            center,
            radius,
            new Color(1f, 0.22f, 0.12f, 0.95f),
            0.16f
        );

        Collider2D[] hits = Physics2D.OverlapCircleAll(center, radius);
        HashSet<EnemyHealth> damaged = new();

        for (int i = 0; i < hits.Length; i++)
        {
            EnemyHealth enemy = hits[i].GetComponentInParent<EnemyHealth>();
            if (enemy == null || enemy.IsDead || !damaged.Add(enemy))
                continue;

            Vector2 direction = (Vector2)enemy.transform.position - center;
            Resolve(context.CreateSecondary(
                enemy,
                center,
                direction,
                damage
            ));
        }
    }

    private static void ApplyChain(in WeaponHitContext context)
    {
        if (context.Target == null || context.PropagationDepth > 0)
            return;

        if (context.ShotKind == WeaponShotKind.Laser &&
            !WeaponCoreEnemyEffects.GetOrCreate(context.Target)
                .TryBeginChainCooldown(LaserChainCooldown))
        {
            return;
        }

        int targetCount = ChainTargetsPerHit;
        if (context.ShotKind == WeaponShotKind.Rocket)
            targetCount = ReserveRocketChainHits(context.Source, targetCount);

        if (targetCount <= 0)
            return;

        List<EnemyHealth> targets = FindNearestEnemies(
            context.Target.transform.position,
            context.Target,
            ChainRadius,
            targetCount
        );
        float damage = context.Damage * ChainDamageMultiplier;
        Vector2 start = context.Target.transform.position;

        for (int i = 0; i < targets.Count; i++)
        {
            EnemyHealth target = targets[i];
            Vector2 end = target.transform.position;
            WeaponCoreDebugVisual.DrawLine(
                start,
                end,
                new Color(0.2f, 0.9f, 1f, 1f),
                0.075f,
                0.13f
            );
            Resolve(context.CreateSecondary(
                target,
                end,
                end - start,
                damage
            ));
        }
    }

    private static void ApplyVoid(in WeaponHitContext context)
    {
        if (context.PropagationDepth > 0)
            return;

        if (context.ShotKind == WeaponShotKind.Rocket)
        {
            ApplyRocketVoid(context);
            return;
        }

        if (context.Target == null)
            return;

        if (context.ShotKind == WeaponShotKind.Laser &&
            !WeaponCoreEnemyEffects.GetOrCreate(context.Target)
                .TryBeginVoidCooldown(LaserVoidCooldown))
        {
            return;
        }

        Vector2 start = context.Target.transform.position;
        CastVoidRay(
            context,
            start,
            context.Direction,
            VoidRange,
            context.Damage * VoidDamageMultiplier,
            context.Target
        );
    }

    private static void ApplyRocketVoid(in WeaponHitContext context)
    {
        Vector2 baseDirection = context.Direction.sqrMagnitude > 0.001f
            ? context.Direction
            : Vector2.right;

        for (int i = 0; i < RocketVoidRayCount; i++)
        {
            float angle = 360f * i / RocketVoidRayCount;
            Vector2 direction = Rotate(baseDirection, angle);
            CastVoidRay(
                context,
                context.HitPoint,
                direction,
                RocketVoidRange,
                context.Damage * RocketVoidDamageMultiplier,
                null
            );
        }
    }

    private static void CastVoidRay(
        in WeaponHitContext context,
        Vector2 start,
        Vector2 direction,
        float range,
        float damage,
        EnemyHealth ignored)
    {
        direction.Normalize();
        Vector2 rayStart = start + direction * 0.12f;
        Vector2 end = rayStart + direction * range;
        EnemyHealth target = null;

        RaycastHit2D[] hits = Physics2D.RaycastAll(rayStart, direction, range);
        for (int i = 0; i < hits.Length; i++)
        {
            EnemyHealth candidate =
                hits[i].collider.GetComponentInParent<EnemyHealth>();
            if (candidate == null || candidate == ignored || candidate.IsDead)
                continue;

            target = candidate;
            end = hits[i].point;
            break;
        }

        WeaponCoreDebugVisual.DrawLine(
            start,
            end,
            new Color(1f, 0.08f, 0.4f, 1f),
            0.09f,
            0.14f
        );

        if (target != null)
        {
            Resolve(context.CreateSecondary(
                target,
                end,
                direction,
                damage
            ));
        }
    }

    private static List<EnemyHealth> FindNearestEnemies(
        Vector2 center,
        EnemyHealth ignored,
        float radius,
        int count)
    {
        List<EnemyHealth> result = new(count);
        float radiusSquared = radius * radius;

        for (int slot = 0; slot < count; slot++)
        {
            EnemyHealth nearest = null;
            float nearestDistance = float.MaxValue;

            foreach (EnemyHealth enemy in EnemyHealth.ActiveInstances)
            {
                if (enemy == null || enemy == ignored || enemy.IsDead ||
                    result.Contains(enemy))
                {
                    continue;
                }

                float distance = ((Vector2)enemy.transform.position - center)
                    .sqrMagnitude;
                if (distance > radiusSquared || distance >= nearestDistance)
                    continue;

                nearest = enemy;
                nearestDistance = distance;
            }

            if (nearest == null)
                break;

            result.Add(nearest);
        }

        return result;
    }

    private static int ReserveRocketChainHits(Object source, int requested)
    {
        if (source == null)
            return Mathf.Min(requested, MaxRocketChainHits);

        int id = source.GetInstanceID();
        if (!rocketChainBudgets.TryGetValue(id, out ChainBudget budget) ||
            budget.Frame != Time.frameCount)
        {
            budget = new ChainBudget { Frame = Time.frameCount, Used = 0 };
        }

        int granted = Mathf.Min(
            requested,
            MaxRocketChainHits - budget.Used
        );
        budget.Used += Mathf.Max(0, granted);
        rocketChainBudgets[id] = budget;

        if (rocketChainBudgets.Count > 64)
            PruneChainBudgets();

        return Mathf.Max(0, granted);
    }

    private static void PruneChainBudgets()
    {
        staleBudgetKeys.Clear();
        foreach (KeyValuePair<int, ChainBudget> pair in rocketChainBudgets)
        {
            if (pair.Value.Frame < Time.frameCount - 1)
                staleBudgetKeys.Add(pair.Key);
        }

        for (int i = 0; i < staleBudgetKeys.Count; i++)
            rocketChainBudgets.Remove(staleBudgetKeys[i]);
    }

    private static Vector2 Rotate(Vector2 direction, float degrees)
    {
        float radians = degrees * Mathf.Deg2Rad;
        float cos = Mathf.Cos(radians);
        float sin = Mathf.Sin(radians);
        return new Vector2(
            direction.x * cos - direction.y * sin,
            direction.x * sin + direction.y * cos
        ).normalized;
    }
}
