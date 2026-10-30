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

    [Header("Visual")]
    [SerializeField] private Transform visualRoot;

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

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    private bool interceptingExperiment;
    private Quaternion interceptingAuthoredRotation;
    private bool interceptingPoseCaptured;
    private Rigidbody2D interceptingPlayerBody;
    private Vector2 interceptingPreviousPosition;
    private Vector2 interceptingPlayerVelocity;
    private bool interceptingHasBaseline;
    public bool InterceptingExperiment => interceptingExperiment;
    // Serialized configuration only: safe to query on a prefab before Awake.
    public bool HasInterceptingVisual => visualRoot != null &&
        visualRoot != transform && visualRoot.IsChildOf(transform);
    public bool CanUseIntercepting => rb != null && isActiveAndEnabled &&
        !isExploding && HasInterceptingVisual &&
        GetComponent<EnemyHealth>() is { IsDead: false };

    public void ConfigureInterceptingExperiment(bool enabled)
    {
        if (enabled)
        {
            if (!CanUseIntercepting || RunStateManager.Instance == null) return;
            RunStateManager.Instance.DebugMarkDevelopmentRun();
            if (!interceptingPoseCaptured)
            {
                interceptingAuthoredRotation = visualRoot.localRotation;
                interceptingPoseCaptured = true;
            }
        }
        if (!enabled || !interceptingExperiment) ResetInterceptingMeasurement(enabled);
        interceptingExperiment = enabled;
        if (!enabled && interceptingPoseCaptured)
        {
            if (visualRoot != null) visualRoot.localRotation = interceptingAuthoredRotation;
            interceptingPoseCaptured = false;
        }
    }

    private void ResetInterceptingMeasurement(bool initializeBaseline = false)
    {
        interceptingPlayerBody = initializeBaseline && player != null
            ? player.GetComponent<Rigidbody2D>() : null;
        interceptingPreviousPosition = interceptingPlayerBody != null
            ? interceptingPlayerBody.position : Vector2.zero;
        interceptingPlayerVelocity = Vector2.zero;
        interceptingHasBaseline = interceptingPlayerBody != null;
    }

    private void MeasureInterceptingPlayerMovement()
    {
        // Called once from each natural FixedUpdate, before scheduling this step's movement.
        // Rigidbody2D.position is the completed simulation, irrespective of controller order.
        var playerBody = player != null ? player.GetComponent<Rigidbody2D>() : null;
        if (playerBody == null || !interceptingHasBaseline || interceptingPlayerBody != playerBody)
        {
            ResetInterceptingMeasurement(true);
            return;
        }
        Vector2 displacement = playerBody.position - interceptingPreviousPosition;
        interceptingPreviousPosition = playerBody.position;
        // Match CharacterMovement2D: diagnostic jumps of >= 2 m are not physical velocity.
        interceptingPlayerVelocity = displacement.sqrMagnitude < 4f && Time.fixedDeltaTime > 0f
            ? displacement / Time.fixedDeltaTime : Vector2.zero;
    }

    private Vector2 GetInterceptingTarget(Vector2 target, Vector2 offset, Vector2 velocity)
    {
        if (!interceptingExperiment) return target;
        float weight = Mathf.SmoothStep(0f, 1f,
            Mathf.InverseLerp(triggerRadius + .5f, triggerRadius + 2f, offset.magnitude));
        return target + Vector2.ClampMagnitude(velocity * .55f, 1.75f) * weight;
    }

    private void ApplyInterceptingPose(Vector2 offset, Vector2 lead)
    {
        Vector2 desired = offset + lead;
        float tilt = offset.sqrMagnitude > .0001f && desired.sqrMagnitude > .0001f
            ? Mathf.Clamp(Vector2.SignedAngle(offset, desired), -20f, 20f) : 0f;
        // Always compose from the authored rotation; never accumulate rotations.
        visualRoot.localRotation = interceptingAuthoredRotation * Quaternion.Euler(0f, 0f, tilt);
    }

    private void Update()
    {
        // FixedUpdate does not run during a full pause. Discard its pre-pause baseline here.
        if (interceptingExperiment && (Time.timeScale == 0f || EnemyDebugAiFreeze.IsFrozen))
            ResetInterceptingMeasurement();
    }
#endif

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
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            ResetInterceptingMeasurement();
#endif
            return;
        }

        if (EnemyDebugAiFreeze.IsFrozen)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            ResetInterceptingMeasurement();
#endif
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
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            ResetInterceptingMeasurement();
#endif
            return;
        }

        Vector2 offset = (Vector2)player.position - rb.position;
        UpdateVisualFacing(visualRoot, offset.normalized);
        Vector2 target = player.position;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (interceptingExperiment)
        {
            MeasureInterceptingPlayerMovement();
            target = GetInterceptingTarget(target, offset, interceptingPlayerVelocity);
            ApplyInterceptingPose(offset, target - (Vector2)player.position);
        }
#endif
        Vector2 direction = ApplyCrowdSteering((target - rb.position).normalized,
            target, Time.fixedDeltaTime);

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
        player = PlayerRuntimeReference.ResolvePlayerTransform(playerTag, true);
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
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        ConfigureInterceptingExperiment(false);
#endif
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
        EnemyExplosion.Detonate(explosionPosition, radius, damage, explosionFxPrefab,
            audioCue: AudioCueId.BomberExplosion);

        if (shockwaveFxPrefab != null)
        {
            var shockwave = Instantiate(
                shockwaveFxPrefab,
                explosionPosition,
                Quaternion.identity
            );
            Subject42.Visuals.LiveVisualRuntime.RegisterHierarchy(shockwave, shockwaveFxPrefab);
        }

        if (owner != null)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            // Honor the existing combat-dummy invulnerability for self-destruction too.
            var dummy = owner.GetComponent<EnemyDebugDamagePolicy>();
            if (dummy != null && dummy.Invulnerable)
                owner.GetComponent<EnemyBomberMovement>()?.NotifyExplosionSequenceCanceled(this);
            else
#endif
                Destroy(owner);
        }

        Destroy(gameObject);
    }
}
