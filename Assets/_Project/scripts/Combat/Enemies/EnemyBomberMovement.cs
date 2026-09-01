using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class EnemyBomberMovement : EnemyMovement
{
    [Header("Target")]
    [SerializeField] private string playerTag = "Player";

    [Header("Movement")]
    [SerializeField] private float moveSpeed = 4.5f;

    [Header("Knockback")]
    [SerializeField] private float knockbackDecay = 18f;

    [Header("Explosion")]
    [SerializeField] private float triggerRadius = 1.4f;
    [SerializeField] private float explosionRadius = 2f;
    [SerializeField] private float explosionDelay = 1f;
    [SerializeField] private int explosionDamage = 25;

    [Header("FX")]
    [SerializeField] private GameObject explosionRadiusPrefab;
    [SerializeField] private ParticleSystem explosionFxPrefab;
    [SerializeField] private GameObject shockwaveFxPrefab;

    private Rigidbody2D rb;
    private Transform player;
    private bool isExploding;
    private float speedMultiplier = 1f;
    private float anomalySpeedMultiplier = 1f;
    private float worldRuleSpeedMultiplier = 1f;
    private Vector2 worldRuleExternalVelocity;
    private Vector2 knockbackVelocity;
    private readonly Dictionary<ExplosiveZone, float>
        explosiveZoneRadiusMultipliers = new();
    private BomberExplosionSequence activeExplosionSequence;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        InitializeCrowdSteering();
    }

    private void Start()
    {
        FindPlayer();
    }

    private void FixedUpdate()
    {
        if (Time.timeScale == 0f || isExploding)
            return;

        if (EnemyDebugAiFreeze.IsFrozen)
        {
            knockbackVelocity = Vector2.MoveTowards(
                knockbackVelocity,
                Vector2.zero,
                knockbackDecay * Time.fixedDeltaTime
            );
            rb.MovePosition(
                rb.position +
                (knockbackVelocity + worldRuleExternalVelocity +
                 AnomalyExternalVelocity) * Time.fixedDeltaTime
            );
            return;
        }

        if (player == null)
            FindPlayer();

        if (player == null)
            return;

        Vector2 offset = (Vector2)player.position - rb.position;
        Vector2 direction = ApplyCrowdSteering(offset.normalized,
            player.position, Time.fixedDeltaTime);

        knockbackVelocity = Vector2.MoveTowards(
            knockbackVelocity,
            Vector2.zero,
            knockbackDecay * Time.fixedDeltaTime
        );

        Vector2 movement =
            direction *
            moveSpeed *
            speedMultiplier *
            anomalySpeedMultiplier *
            worldRuleSpeedMultiplier +
            knockbackVelocity +
            worldRuleExternalVelocity +
            AnomalyExternalVelocity;

        rb.MovePosition(rb.position + movement * Time.fixedDeltaTime);

        float sqrDistance =
            ((Vector2)player.position - rb.position).sqrMagnitude;

        if (sqrDistance <= triggerRadius * triggerRadius)
            StartExplosionSequence();
    }

    private void FindPlayer()
    {
        GameObject playerObject = GameObject.FindGameObjectWithTag(playerTag);

        if (playerObject != null)
            player = playerObject.transform;
    }

    private void StartExplosionSequence()
    {
        if (isExploding)
            return;

        isExploding = true;
        rb.linearVelocity = Vector2.zero;
        float lockedRadius = explosionRadius *
            GetExplosiveZoneRadiusMultiplier(out ExplosiveZone sourceZone);
        activeExplosionSequence = BomberExplosionSequence.Create(
            transform.position,
            lockedRadius,
            explosionDelay,
            explosionDamage,
            explosionRadiusPrefab,
            explosionFxPrefab,
            shockwaveFxPrefab,
            gameObject
        );
        sourceZone?.TrackBomberSequence(activeExplosionSequence);
    }

    internal void EnterExplosiveZone(ExplosiveZone source, float multiplier)
    {
        if (source == null)
            return;

        explosiveZoneRadiusMultipliers[source] = Mathf.Max(1f, multiplier);
    }

    internal void ExitExplosiveZone(ExplosiveZone source)
    {
        if (!ReferenceEquals(source, null))
            explosiveZoneRadiusMultipliers.Remove(source);
    }

    internal bool TryStartExplosionAfterDeath(
        out BomberExplosionSequence sequence)
    {
        if (activeExplosionSequence != null)
        {
            sequence = activeExplosionSequence;
            return false;
        }

        StartExplosionSequence();
        sequence = activeExplosionSequence;
        return sequence != null;
    }

    internal void NotifyExplosionSequenceCanceled(
        BomberExplosionSequence sequence)
    {
        if (activeExplosionSequence != sequence)
            return;

        activeExplosionSequence = null;
        isExploding = false;
    }

    private float GetExplosiveZoneRadiusMultiplier(
        out ExplosiveZone sourceZone)
    {
        float multiplier = 1f;
        sourceZone = null;

        foreach (KeyValuePair<ExplosiveZone, float> pair in
                 explosiveZoneRadiusMultipliers)
        {
            if (pair.Key == null || pair.Value <= multiplier)
                continue;

            multiplier = pair.Value;
            sourceZone = pair.Key;
        }

        return multiplier;
    }

    public override void SetSpeedMultiplier(float multiplier)
    {
        speedMultiplier = Mathf.Max(0.1f, multiplier);
    }

    public override void SetAnomalySpeedMultiplier(float multiplier)
    {
        anomalySpeedMultiplier = Mathf.Max(0.1f, multiplier);
    }

    public override void SetWorldRuleSpeedMultiplier(float multiplier)
    {
        worldRuleSpeedMultiplier = Mathf.Max(0.1f, multiplier);
    }

    public override void SetWorldRuleExternalVelocity(Vector2 velocity)
    {
        worldRuleExternalVelocity = velocity;
    }

    public override void ApplyKnockback(Vector2 direction, float force)
    {
        if (isExploding)
            return;

        knockbackVelocity = direction.normalized * force;
    }

    public override void StopAfterHit()
    {
        // Подрывник не останавливается от контактного удара.
    }

    private void OnDisable()
    {
        ReleaseCrowdSteering();
        explosiveZoneRadiusMultipliers.Clear();
        ClearAnomalyExternalVelocities();
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, triggerRadius);

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, explosionRadius);
    }
}

internal sealed class BomberExplosionSequence : MonoBehaviour
{
    private static Collider2D[] explosionHits = new Collider2D[16];
    private float radius;
    private float delay;
    private int damage;
    private ParticleSystem explosionFxPrefab;
    private GameObject shockwaveFxPrefab;
    private GameObject owner;
    private Vector2 explosionPosition;
    private bool canceled;
    private bool exploded;

    internal static BomberExplosionSequence Create(
        Vector2 position,
        float radius,
        float delay,
        int damage,
        GameObject warningPrefab,
        ParticleSystem explosionFxPrefab,
        GameObject shockwaveFxPrefab,
        GameObject owner)
    {
        GameObject sequenceObject = ExplosionWarningVisual.Spawn(
            warningPrefab,
            position,
            radius,
            delay
        );

        if (sequenceObject == null)
        {
            sequenceObject = new GameObject("Bomber Explosion Sequence");
            sequenceObject.transform.position = position;
        }

        BomberExplosionSequence sequence =
            sequenceObject.AddComponent<BomberExplosionSequence>();
        sequence.radius = Mathf.Max(0.1f, radius);
        sequence.delay = Mathf.Max(0f, delay);
        sequence.damage = Mathf.Max(0, damage);
        sequence.explosionFxPrefab = explosionFxPrefab;
        sequence.shockwaveFxPrefab = shockwaveFxPrefab;
        sequence.owner = owner;
        sequence.explosionPosition = position;
        sequence.StartCoroutine(sequence.Run());
        return sequence;
    }

    internal void Cancel()
    {
        if (canceled || exploded)
            return;

        canceled = true;
        StopAllCoroutines();

        if (owner != null)
        {
            EnemyBomberMovement bomber =
                owner.GetComponent<EnemyBomberMovement>();
            bomber?.NotifyExplosionSequenceCanceled(this);
        }

        Destroy(gameObject);
    }

    private IEnumerator Run()
    {
        if (delay > 0f)
            yield return new WaitForSeconds(delay);

        if (!canceled)
            Explode();
    }

    private void Explode()
    {
        if (canceled || exploded)
            return;

        exploded = true;
        ContactFilter2D filter = ContactFilter2D.noFilter;
        filter.useTriggers = true;
        int hitCount;

        do
        {
            hitCount = Physics2D.OverlapCircle(
                explosionPosition,
                radius,
                filter,
                explosionHits);

            if (hitCount < explosionHits.Length)
                break;

            Array.Resize(ref explosionHits, explosionHits.Length * 2);
        }
        while (true);

        for (int i = 0; i < hitCount; i++)
        {
            Collider2D hit = explosionHits[i];
            explosionHits[i] = null;

            if (hit == null || !hit.CompareTag("Player"))
                continue;

            PlayerHealth health = hit.GetComponent<PlayerHealth>();

            if (health == null)
                continue;

            Vector2 hitDirection =
                (Vector2)hit.transform.position - explosionPosition;
            health.TakeDamage(damage, hitDirection);
        }

        AudioService.Instance?.PlayAt(
            AudioCueId.Explosion,
            explosionPosition
        );

        if (explosionFxPrefab != null)
        {
            ParticleSystem fx = Instantiate(
                explosionFxPrefab,
                explosionPosition,
                Quaternion.identity
            );
            fx.Play();
            Destroy(fx.gameObject, fx.main.duration);
        }

        if (shockwaveFxPrefab != null)
        {
            Instantiate(
                shockwaveFxPrefab,
                explosionPosition,
                Quaternion.identity
            );
        }

        if (owner != null)
            Destroy(owner);

        Destroy(gameObject);
    }
}
