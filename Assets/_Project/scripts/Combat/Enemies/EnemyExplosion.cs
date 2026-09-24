using System.Collections.Generic;
using UnityEngine;

internal static class EnemyExplosion
{
    // Enemy explosions damage players, matching Bomber's existing target policy.
    // Resolve parent health and deduplicate so compound colliders receive one hit.
    internal static ParticleSystem Detonate(Vector2 position, float radius, int damage,
        ParticleSystem explosionPrefab, SimplePrefabPool pool = null, bool nonLethal = false)
    {
        var recipients = new HashSet<PlayerHealth>();
        var hits = new List<Collider2D>();
        var filter = ContactFilter2D.noFilter;
        filter.useTriggers = true;
        Physics2D.OverlapCircle(position, Mathf.Max(0.1f, radius), filter, hits);
        foreach (var hit in hits)
        {
            var health = hit.GetComponentInParent<PlayerHealth>();
            if (health == null || !health.CompareTag("Player") || !recipients.Add(health)) continue;
            float appliedDamage = Mathf.Max(0, damage);
            if (nonLethal)
            {
                // Apply the cap before PlayerHealth scales incoming damage. Boss/Bomber are unchanged.
                float multiplier = health.IncomingDamageMultiplier;
                if (multiplier <= 0f || health.CurrentHealth <= 1f) continue;
                appliedDamage = Mathf.Min(appliedDamage, (health.CurrentHealth - 1f) / multiplier);
                if (appliedDamage <= 0f) continue;
            }
            health.TakeDamage(appliedDamage, (Vector2)health.transform.position - position);
        }
        AudioService.Instance?.PlayAt(AudioCueId.Explosion, position);
        if (explosionPrefab == null) return null;
        var fx = pool != null ? pool.Get(position, Quaternion.identity)?.PrimaryParticleSystem :
            Object.Instantiate(explosionPrefab, position, Quaternion.identity);
        if (fx == null) return null;
        fx.Play();
        if (pool == null) Object.Destroy(fx.gameObject, fx.main.duration);
        return fx;
    }
}
