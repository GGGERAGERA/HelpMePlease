using System.Collections.Generic;
using UnityEngine;

public static class CombatExplosionService
{
    private const float ExplosionRadius = 2f;
    private const float ExplosionDamageMultiplier = 0.5f;

    public static void TryExplodeOnHit(
        Vector2 position,
        float sourceDamage,
        PlayerCombatModifiers modifiers,
        LayerMask enemyMask
    )
    {
        if (modifiers == null)
            return;

        if (modifiers.hitExplosionChance <= 0f)
            return;

        if (Random.value > modifiers.hitExplosionChance)
            return;

        Explode(position, sourceDamage, modifiers, enemyMask);
    }

    public static void Explode(
        Vector2 position,
        float sourceDamage,
        PlayerCombatModifiers modifiers,
        LayerMask enemyMask,
        float extraDamageMultiplier = 1f
    )
    {
        float damage = sourceDamage * ExplosionDamageMultiplier * extraDamageMultiplier;

        Collider2D[] hits = Physics2D.OverlapCircleAll(
            position,
            ExplosionRadius,
            enemyMask
        );

        HashSet<EnemyHealth> damagedEnemies = new HashSet<EnemyHealth>();

        foreach (Collider2D hit in hits)
        {
            EnemyHealth enemy = hit.GetComponentInParent<EnemyHealth>();

            if (enemy == null)
                continue;

            if (damagedEnemies.Contains(enemy))
                continue;

            damagedEnemies.Add(enemy);
            enemy.TakeDamage(damage, position, false);
        }
    }

}
