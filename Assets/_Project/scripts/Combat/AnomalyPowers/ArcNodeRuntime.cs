using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
internal sealed class ArcNodeRuntime : MonoBehaviour, IAnomalyPowerRuntime, IAnomalyEvolutionPower
{
    private const float BaseDamage = 40f;
    private const float VisualRadius = 0.53f;
    private const float HitFlashDuration = 0.1f;
    private const float IdleTargetSearchInterval = 0.15f;
    private const int MaximumSweepHits = 64;
    private static readonly Vector2 IdleOffset = new(-1.45f, 1.05f);

    private enum MovementState { Idle, Outbound, Returning }

    [Header("Arc Construct Movement")]
    [SerializeField, Min(0.1f)] private float flightSpeed = 13.5f;
    [SerializeField, Min(0.1f)] private float targetSearchRadius = 9f;
    [SerializeField, Min(0.01f)] private float hitRadius = 0.42f;
    [SerializeField, Min(0.1f)] private float overshootDistance = 3f;
    [SerializeField, Min(0.01f)] private float returnRadius = 0.3f;

    [Header("Arc II Moving Weapon Platform")]
    [SerializeField, Min(0.05f)] private float hybridPayloadInterval = 0.75f;

    [Header("Arc III Flight Overdrive")]
    [SerializeField, Min(1f)] private float overdriveSpeedMultiplier = 1.35f;
    [SerializeField, Min(0.05f)] private float overdrivePayloadInterval = 0.2f;
    [SerializeField, Range(1f, 180f)] private float overdrivePayloadAngularStep = 47f;
    [SerializeField, Range(0f, 45f)] private float overdrivePayloadJitter = 15f;

    [Header("Payload Safety")]
    [SerializeField, Min(0f)] private float minPayloadInterval = 0.06f;

    private Material material;
    private Transform nodeVisual;
    private TrailRenderer trail;
    private readonly HashSet<EnemyHealth> outboundHits = new();
    private readonly HashSet<EnemyHealth> returningHits = new();
    private readonly RaycastHit2D[] sweepHits = new RaycastHit2D[MaximumSweepHits];
    private EnemyHealth lastCollisionEnemy;
    private BaseWeapon payloadWeapon;
    private MovementState state;
    private float lastPayloadTime = float.NegativeInfinity;
    private float hitFlashUntil;
    private float idlePhase;
    private float nextIdleTargetSearch;
    private float nextPayloadAt;
    private float payloadAngle;
    private Vector2 outboundDirection;
    private Vector2 outboundEndpoint;
    private int level = 1;

    public AnomalyPowerType Type => AnomalyPowerType.ArcNode;
    public int Level => level;

    public void SetLevel(int value)
    {
        level = AnomalyPowerLevelProfiles.ClampLevel(value);
    }

    public void ConfigureEvolutionPayload(BaseWeapon weapon, EvolutionDefinition definition, int anomalyLevel)
    {
        payloadWeapon = weapon;
        SetLevel(anomalyLevel);
    }

    public void DisableEvolutionPayload() => payloadWeapon = null;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void RegisterRuntime()
    {
        AnomalyPowerRuntimeRegistry.Register(AnomalyPowerType.ArcNode, owner => owner.AddComponent<ArcNodeRuntime>());
    }

    public void Activate()
    {
        enabled = true;
        if (nodeVisual != null)
            nodeVisual.gameObject.SetActive(true);
        if (trail != null)
            trail.emitting = true;
    }

    public void Deactivate()
    {
        enabled = false;
        DisableEvolutionPayload();
        outboundHits.Clear();
        returningHits.Clear();
        lastCollisionEnemy = null;
        if (trail != null)
        {
            trail.emitting = false;
            trail.Clear();
        }
        if (nodeVisual != null)
            nodeVisual.gameObject.SetActive(false);
    }

    private void Awake()
    {
        material = AnomalyPowerVisuals.CreateMaterial("Arc Construct Runtime Material");
        BuildNodeVisual();
        SnapToIdleAnchor();
        state = MovementState.Idle;
    }

    private void Update()
    {
        if (nodeVisual == null)
            return;

        float attackSize = Mathf.Max(0.01f, OffensiveAttackContext.GetAttackSize(gameObject));
        UpdateVisual(attackSize);
        switch (state)
        {
            case MovementState.Idle: UpdateIdle(); break;
            case MovementState.Outbound: UpdateOutbound(attackSize); break;
            case MovementState.Returning: UpdateReturning(attackSize); break;
        }
    }

    private void UpdateIdle()
    {
        nodeVisual.position = Vector2.MoveTowards(
            nodeVisual.position,
            GetIdleAnchor(),
            flightSpeed * Time.deltaTime);
        if (Time.time < nextIdleTargetSearch)
            return;

        nextIdleTargetSearch = Time.time + IdleTargetSearchInterval;
        EnemyHealth next = FindNearestTarget(
            nodeVisual.position,
            targetSearchRadius,
            null);
        if (next != null)
            BeginOutbound(next);
    }

    private void BeginOutbound(EnemyHealth target)
    {
        if (!IsValidTarget(target))
            return;

        Vector2 launchPosition = nodeVisual.position;
        Vector2 targetOffset =
            (Vector2)target.transform.position - launchPosition;
        if (targetOffset.sqrMagnitude <= 0.001f)
            targetOffset = Vector2.right;

        float distanceToTarget = targetOffset.magnitude;
        outboundDirection = targetOffset.normalized;
        outboundEndpoint = launchPosition + outboundDirection *
            (distanceToTarget + overshootDistance);
        outboundHits.Clear();
        returningHits.Clear();
        lastCollisionEnemy = null;
        state = MovementState.Outbound;
        nextPayloadAt = Time.time + GetPayloadInterval();
    }

    private void UpdateOutbound(float attackSize)
    {
        Vector2 previousPosition = nodeVisual.position;
        float speed = GetFlightSpeed();
        Vector2 currentPosition = Vector2.MoveTowards(
            previousPosition,
            outboundEndpoint,
            speed * Time.deltaTime);
        nodeVisual.position = currentPosition;
        ApplySweptDamage(
            previousPosition,
            currentPosition,
            attackSize,
            outboundHits);

        if ((currentPosition - outboundEndpoint).sqrMagnitude <= 0.0001f)
        {
            state = MovementState.Returning;
            returningHits.Clear();
            return;
        }

        UpdateMovementPayload();
    }

    private void UpdateReturning(float attackSize)
    {
        Vector2 returnTarget = GetIdleAnchor();
        Vector2 previousPosition = nodeVisual.position;
        Vector2 currentPosition = Vector2.MoveTowards(
            previousPosition,
            returnTarget,
            GetFlightSpeed() * Time.deltaTime);
        nodeVisual.position = currentPosition;
        ApplySweptDamage(
            previousPosition,
            currentPosition,
            attackSize,
            returningHits);

        if ((currentPosition - returnTarget).sqrMagnitude <=
            returnRadius * returnRadius)
        {
            nodeVisual.position = returnTarget;
            state = MovementState.Idle;
            lastCollisionEnemy = null;
            return;
        }

        UpdateMovementPayload();
    }

    private void ApplySweptDamage(
        Vector2 previousPosition,
        Vector2 currentPosition,
        float attackSize,
        HashSet<EnemyHealth> hitSet)
    {
        Vector2 movement = currentPosition - previousPosition;
        float distance = movement.magnitude;
        if (distance <= Mathf.Epsilon)
            return;

        float radius = hitRadius * Mathf.Max(0.01f, attackSize);
        int hitCount = Physics2D.CircleCast(
            previousPosition,
            radius,
            movement / distance,
            ContactFilter2D.noFilter,
            sweepHits,
            distance);
        for (int i = 0; i < hitCount; i++)
        {
            Collider2D collider = sweepHits[i].collider;
            EnemyHealth enemy = collider != null
                ? collider.GetComponentInParent<EnemyHealth>()
                : null;
            if (!IsValidTarget(enemy) || !hitSet.Add(enemy))
                continue;

            Vector2 hitPoint = collider.ClosestPoint(currentPosition);
            OffensiveAttackContext attack = OffensiveAttackContext.Resolve(
                gameObject,
                BaseDamage);
            enemy.TakeDamage(attack.Damage, hitPoint, attack.IsCritical);
            lastCollisionEnemy = enemy;
            hitFlashUntil = Time.time + HitFlashDuration;
        }
    }

    private void UpdateMovementPayload()
    {
        if (level < 2 || payloadWeapon == null ||
            Time.time < nextPayloadAt)
        {
            return;
        }

        nextPayloadAt = Time.time + GetPayloadInterval();
        if (level >= 3)
        {
            float jitter = Random.Range(
                -overdrivePayloadJitter,
                overdrivePayloadJitter);
            Vector2 radialDirection =
                DirectionFromAngle(payloadAngle + jitter);
            TryEmitPayload(radialDirection);
            payloadAngle = Mathf.Repeat(
                payloadAngle + overdrivePayloadAngularStep,
                360f);
            return;
        }

        EnemyHealth target = FindNearestTarget(
            nodeVisual.position,
            targetSearchRadius,
            lastCollisionEnemy);
        if (target == null)
        {
            target = FindNearestTarget(
                nodeVisual.position,
                targetSearchRadius,
                null);
        }

        if (target == null)
            return;

        Vector2 targetDirection =
            (Vector2)target.transform.position -
            (Vector2)nodeVisual.position;
        TryEmitPayload(targetDirection);
    }

    private float GetFlightSpeed()
    {
        return flightSpeed * (level >= 3 ? overdriveSpeedMultiplier : 1f);
    }

    private float GetPayloadInterval()
    {
        return level >= 3
            ? overdrivePayloadInterval
            : hybridPayloadInterval;
    }

    private bool TryEmitPayload(Vector2 direction)
    {
        if (payloadWeapon == null || direction.sqrMagnitude <= 0.001f ||
            Time.time < lastPayloadTime + minPayloadInterval)
        {
            return false;
        }

        lastPayloadTime = Time.time;
        return payloadWeapon.EmitAttack(
            nodeVisual.position,
            direction.normalized);
    }

    private EnemyHealth FindNearestTarget(
        Vector2 origin,
        float radius,
        EnemyHealth excludedTarget)
    {
        EnemyHealth nearest = null;
        float nearestDistanceSquared = radius * radius;

        foreach (EnemyHealth enemy in EnemyHealth.ActiveInstances)
        {
            if (!IsValidTarget(enemy) || enemy == excludedTarget)
                continue;
            float distanceSquared = ((Vector2)enemy.transform.position - origin).sqrMagnitude;
            if (distanceSquared <= nearestDistanceSquared)
            {
                nearest = enemy;
                nearestDistanceSquared = distanceSquared;
            }
        }
        return nearest;
    }

    private static bool IsValidTarget(EnemyHealth enemy)
    {
        return enemy != null && !enemy.IsDead && enemy.gameObject.activeInHierarchy;
    }

    private static Vector2 DirectionFromAngle(float angle)
    {
        float radians = angle * Mathf.Deg2Rad;
        return new Vector2(Mathf.Cos(radians), Mathf.Sin(radians));
    }

    private void BuildNodeVisual()
    {
        GameObject nodeObject = new("Arc Construct");
        nodeObject.transform.SetParent(transform, false);
        nodeVisual = nodeObject.transform;

        LineRenderer glow = AnomalyPowerVisuals.CreateLine(nodeVisual, "Arc Construct Glow", new Color(0.12f, 0.65f, 1f, 0.28f), 0.18f, 25, material);
        ConfigureNodeRing(glow, VisualRadius);
        glow.sortingOrder = 35;

        LineRenderer star = AnomalyPowerVisuals.CreateLine(nodeVisual, "Arc Construct Star", new Color(0.2f, 0.92f, 1f, 1f), 0.11f, 12, material);
        star.useWorldSpace = false;
        star.loop = true;
        star.sortingOrder = 38;
        star.endColor = new Color(0.18f, 0.48f, 1f, 1f);
        for (int i = 0; i < star.positionCount; i++)
        {
            float radians = Mathf.PI * 2f * i / star.positionCount;
            float radius = i % 2 == 0 ? 0.42f : 0.22f;
            star.SetPosition(i, new Vector3(Mathf.Cos(radians) * radius, Mathf.Sin(radians) * radius, 0f));
        }

        LineRenderer core = AnomalyPowerVisuals.CreateLine(nodeVisual, "Arc Construct Core", new Color(0.72f, 0.96f, 1f, 1f), 0.3f, 2, material);
        core.useWorldSpace = false;
        core.numCapVertices = 8;
        core.SetPosition(0, new Vector3(-0.02f, 0f, 0f));
        core.SetPosition(1, new Vector3(0.02f, 0f, 0f));
        core.sortingOrder = 39;

        trail = nodeObject.AddComponent<TrailRenderer>();
        trail.time = 0.16f;
        trail.minVertexDistance = 0.08f;
        trail.startWidth = 0.2f;
        trail.endWidth = 0.015f;
        trail.numCapVertices = 4;
        trail.numCornerVertices = 3;
        trail.startColor = new Color(0.35f, 0.9f, 1f, 0.7f);
        trail.endColor = new Color(0.12f, 0.45f, 1f, 0f);
        trail.sortingLayerName = "Effects";
        trail.sortingOrder = 34;
        if (material != null)
            trail.sharedMaterial = material;
        trail.Clear();
    }

    private static void ConfigureNodeRing(LineRenderer ring, float radius)
    {
        ring.useWorldSpace = false;
        ring.loop = true;
        for (int i = 0; i < ring.positionCount; i++)
        {
            float radians = Mathf.PI * 2f * i / ring.positionCount;
            ring.SetPosition(i, new Vector3(Mathf.Cos(radians) * radius, Mathf.Sin(radians) * radius, 0f));
        }
    }

    private void UpdateVisual(float attackSize)
    {
        idlePhase += Time.deltaTime;
        float flashScale = Time.time < hitFlashUntil ? 1.35f : 1f;
        nodeVisual.localScale = Vector3.one * attackSize * flashScale;
        nodeVisual.Rotate(0f, 0f, 120f * Time.deltaTime);
        if (trail != null)
        {
            trail.startWidth = 0.2f * attackSize;
            trail.endWidth = 0.015f * attackSize;
        }
    }

    private Vector2 GetIdleAnchor()
    {
        Vector2 drift = new(Mathf.Cos(idlePhase * 2.2f) * 0.1f, Mathf.Sin(idlePhase * 2.5f) * 0.14f);
        return (Vector2)transform.position + IdleOffset + drift;
    }

    private void SnapToIdleAnchor()
    {
        if (nodeVisual != null)
            nodeVisual.position = (Vector2)transform.position + IdleOffset;
    }

    private void OnValidate()
    {
        flightSpeed = Mathf.Max(0.1f, flightSpeed);
        targetSearchRadius = Mathf.Max(0.1f, targetSearchRadius);
        hitRadius = Mathf.Max(0.01f, hitRadius);
        overshootDistance = Mathf.Max(0.1f, overshootDistance);
        returnRadius = Mathf.Max(0.01f, returnRadius);
        hybridPayloadInterval = Mathf.Max(0.05f, hybridPayloadInterval);
        overdriveSpeedMultiplier = Mathf.Max(1f, overdriveSpeedMultiplier);
        overdrivePayloadInterval = Mathf.Max(
            0.05f,
            overdrivePayloadInterval);
        overdrivePayloadAngularStep = Mathf.Clamp(
            overdrivePayloadAngularStep,
            1f,
            180f);
        overdrivePayloadJitter = Mathf.Clamp(
            overdrivePayloadJitter,
            0f,
            45f);
        minPayloadInterval = Mathf.Max(0f, minPayloadInterval);
    }

    private void OnDestroy()
    {
        if (material != null)
            Destroy(material);
    }
}
