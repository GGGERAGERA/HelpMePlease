using System.Collections.Generic;
using UnityEngine;

internal static class ProductionSiteHazardUtility
{
    public static void ApplyLineDamage(
        Vector2 start,
        Vector2 end,
        float halfWidth,
        float enemyDamage,
        float playerDamage)
    {
        List<EnemyHealth> enemies = new(EnemyHealth.ActiveInstances);

        for (int i = 0; i < enemies.Count; i++)
        {
            EnemyHealth enemy = enemies[i];

            if (enemy == null || enemy.IsDead ||
                DistanceToSegment(enemy.transform.position, start, end) >
                halfWidth)
            {
                continue;
            }

            enemy.TakeDamage(enemyDamage, enemy.transform.position, false);
        }

        GameObject player = GameObject.FindGameObjectWithTag("Player");
        PlayerHealth playerHealth = player != null
            ? player.GetComponent<PlayerHealth>()
            : null;

        if (playerHealth == null || playerHealth.IsDead ||
            DistanceToSegment(player.transform.position, start, end) >
            halfWidth)
        {
            return;
        }

        Vector2 nearest = ClosestPoint(player.transform.position, start, end);
        Vector2 knockback = (Vector2)player.transform.position - nearest;

        if (knockback.sqrMagnitude < 0.001f)
            knockback = Vector2.up;

        playerHealth.TakeDamage(playerDamage, knockback.normalized);
    }

    private static Vector2 ClosestPoint(Vector2 point, Vector2 start, Vector2 end)
    {
        Vector2 segment = end - start;
        float denominator = segment.sqrMagnitude;

        if (denominator <= 0.0001f)
            return start;

        float amount = Mathf.Clamp01(
            Vector2.Dot(point - start, segment) / denominator
        );
        return start + segment * amount;
    }

    private static float DistanceToSegment(
        Vector2 point,
        Vector2 start,
        Vector2 end)
    {
        return Vector2.Distance(point, ClosestPoint(point, start, end));
    }
}
