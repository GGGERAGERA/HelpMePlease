using System.Collections.Generic;
using UnityEngine;

internal static class EnemyExplosion
{
    // Enemy explosions damage players, matching Bomber's existing target policy.
    // Resolve parent health and deduplicate so compound colliders receive one hit.
    internal static ParticleSystem Detonate(Vector2 position, float radius, int damage,
        ParticleSystem explosionPrefab)
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
            health.TakeDamage(Mathf.Max(0, damage), (Vector2)health.transform.position - position);
        }
        AudioService.Instance?.PlayAt(AudioCueId.Explosion, position);
        if (explosionPrefab == null) return null;
        var fx = Object.Instantiate(explosionPrefab, position, Quaternion.identity);
        fx.Play();
        Object.Destroy(fx.gameObject, fx.main.duration);
        return fx;
    }
}
