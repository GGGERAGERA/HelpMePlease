using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(Rigidbody2D))]
public class EnemyChaseMovement : EnemyMovement
{
    public enum ChasePattern { Direct, Flanker, Breacher }
    [Header("Production behaviour")]
    [SerializeField] private ChasePattern chasePattern;
    [SerializeField] private bool useLabTacticalMovement;
    public bool UsesProductionTacticalTuning => useLabTacticalMovement;
    [SerializeField] private LineRenderer ramTelegraph;
    public bool IsProductionTacticalEnemy => chasePattern != ChasePattern.Direct;
    [Header("Target")]
    [SerializeField] private string playerTag = "Player";

    [Header("Speed")]
    [SerializeField] private float normalSpeed = 2f;
    [SerializeField] private float aggroSpeed = 4f;
    [SerializeField] private float aggroDistance = 5f;

    [Header("Hit Stop")]
    [SerializeField] private float stopAfterHitDuration = 2f;

    [Header("Knockback")]
    [SerializeField] private float knockbackDecay = 16f;

    [Header("Visual")]
    [SerializeField] private Transform visualRoot;
    [SerializeField] private bool flipVisual = true;

    [Header("Animation")]
    [SerializeField] private Animator animator;
    [SerializeField] private string runParameterName = "IsRunning";
    [Tooltip("Optional MovePosition locomotion speed. Empty keeps existing enemy animators unchanged.")]
    [SerializeField] private string movementSpeedParameterName;
    private int movementSpeedHash;
    private bool hasMovementSpeed;
    private float attackPauseRemaining;
    private Vector2? assaultDestination;
    private bool flankingExperiment;
    private bool flankingApproach;
    private float flankingRemaining;
    private float flankingWeight;
    private float flankingSide = 1f;
    private Transform flankingVisual;
    private Quaternion flankingAuthoredRotation;
    private bool flankingPoseApplied;
    public bool IsFlankingExperiment => flankingExperiment;
    private enum RamPhase { Chasing, Preparing, Charging, Recovering }
    private bool committedRamExperiment;
    private RamPhase ramPhase;
    private float ramRemaining;
    private Vector2 ramDirection;
    private bool ramNeedsSeparation;
    private float ramReadyAt;
    // Legacy opt-in experiments retain their defaults; authored tactical variants use the accepted lab tuning.
    private float ramSpeedMultiplier = 2.5f;
    private float ramApproachRange = 5f, ramPrepareSeconds = .65f, ramChargeSeconds = .45f;
    private float ramRecoverySeconds = 1.2f, ramSeparationRange = 6.5f, ramCooldownSeconds = 2f;
    private bool exactRamDuration;
    private Collider2D[] ramWarningColliders;
    private bool spiralApproach;
    private readonly Dictionary<Object, float> attackRecoveryModifiers = new();
    public bool CanDealContactDamage => !committedRamExperiment ||
        ramPhase == RamPhase.Chasing || ramPhase == RamPhase.Charging;
    public float AttackRecoveryMultiplier
    {
        get
        {
            float result = 1f;
            foreach (var pair in attackRecoveryModifiers)
                if (pair.Key != null) result *= pair.Value;
            return Mathf.Clamp(result, .5f, 4f);
        }
    }

    // Duration multiplier: >1 lengthens recovery. Replacing/removing a zone
    // never changes the authored speed, damage or preparation timer.
    public void SetAttackRecoveryMultiplier(Object source, float multiplier)
    {
        if (source == null || float.IsNaN(multiplier) || float.IsInfinity(multiplier)) return;
        attackRecoveryModifiers[source] = Mathf.Clamp(multiplier, .5f, 4f);
    }

    public void RemoveAttackRecoveryMultiplier(Object source)
    {
        if (!ReferenceEquals(source, null)) attackRecoveryModifiers.Remove(source);
    }

    public bool IsCommittedRamExperiment => committedRamExperiment;
    public bool CanUseCommittedRam => ResolveSafeVisual() != null;

    // Shared by accepted production variants and the lab's scene-local spawn path.
    public Transform ConfigureLabTacticalMovement()
    {
        if (chasePattern == ChasePattern.Flanker)
        {
            spiralApproach = true;
            normalSpeed = aggroSpeed = 7f;
        }
        else if (chasePattern == ChasePattern.Breacher)
        {
            normalSpeed = 4f; aggroSpeed = 4.8f;
            ramSpeedMultiplier = 5f; // 24 m/s, retaining ordinary external speed modifiers.
            ramApproachRange = 18f; ramPrepareSeconds = 1f; ramChargeSeconds = 1.2f;
            ramRecoverySeconds = 1.5f; ramSeparationRange = 18f; ramCooldownSeconds = 3.5f;
            exactRamDuration = true;
            ramWarningColliders = GetComponents<Collider2D>();
            rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        }
        return flankingVisual;
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    private bool woundedRetreatExperiment;
    private bool woundedRetreatUsed;
    private float woundedRetreatRemaining;
    private Vector2 woundedRetreatDirection;
    private EnemyHealth woundedRetreatHealth;
    public bool IsWoundedRetreatExperiment => woundedRetreatExperiment;
    public bool CanUseWoundedRetreat => ResolveSafeVisual() != null;

    public void ConfigureWoundedRetreatExperiment(bool enabled)
    {
        CancelWoundedRetreat();
        woundedRetreatExperiment = enabled && !flankingExperiment &&
            !committedRamExperiment && !HasAssaultDestination && CanUseWoundedRetreat;
        if (woundedRetreatExperiment && woundedRetreatHealth == null)
            woundedRetreatHealth = GetComponent<EnemyHealth>();
        // Never rearm a living actor by toggling the experiment or healing it.
    }

    private void CancelWoundedRetreat()
    {
        if (woundedRetreatRemaining > 0f && flankingVisual != null)
            flankingVisual.localRotation = flankingAuthoredRotation;
        woundedRetreatRemaining = 0f;
    }

    private void TryBeginWoundedRetreat(Vector2 playerOffset)
    {
        if (!woundedRetreatExperiment || assaultDestination.HasValue) return;
        if (!woundedRetreatUsed && woundedRetreatHealth != null &&
            !woundedRetreatHealth.IsDead && woundedRetreatHealth.CurrentHealth > 0f &&
            woundedRetreatHealth.MaxHealth > 0f &&
            woundedRetreatHealth.CurrentHealth <= woundedRetreatHealth.MaxHealth * .5f)
        {
            woundedRetreatUsed = true;
            woundedRetreatDirection = playerOffset.sqrMagnitude > .0001f
                ? -playerOffset.normalized : Vector2.down;
            woundedRetreatRemaining = .9f;
        }
    }

    private bool TickWoundedRetreat(ref Vector2 direction)
    {
        if (!woundedRetreatExperiment || assaultDestination.HasValue) return false;
        if (woundedRetreatRemaining <= 0f) return false;
        direction = woundedRetreatDirection;
        float localX = transform.InverseTransformDirection(woundedRetreatDirection).x;
        flankingVisual.localRotation = flankingAuthoredRotation *
            Quaternion.Euler(0f, 0f, -12f * (localX < 0f ? -1f : 1f));
        woundedRetreatRemaining = Mathf.Max(0f, woundedRetreatRemaining - Time.fixedDeltaTime);
        if (woundedRetreatRemaining <= 0f)
            flankingVisual.localRotation = flankingAuthoredRotation;
        return true;
    }
#endif

    private Transform ResolveSafeVisual()
    {
        var candidate = visualRoot;
        while (candidate != null && candidate != transform && candidate.parent != transform)
            candidate = candidate.parent;
        return candidate == null || candidate == transform ||
            candidate.GetComponentInChildren<Collider2D>(true) != null ||
            candidate.GetComponentInChildren<Rigidbody2D>(true) != null ? null : candidate;
    }

    public void ConfigureCommittedRamExperiment(bool enabled)
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (enabled) ConfigureWoundedRetreatExperiment(false);
#endif
        ResetRamCycle();
        ramNeedsSeparation = false;
        ramReadyAt = 0f;
        committedRamExperiment = enabled && !flankingExperiment && CanUseCommittedRam;
    }

    private void ResetRamCycle()
    {
        if (ramTelegraph != null) ramTelegraph.enabled = false;
        if (committedRamExperiment && flankingVisual != null)
            flankingVisual.localRotation = flankingAuthoredRotation;
        if (committedRamExperiment) chaseVelocity = Vector2.zero;
        ramPhase = RamPhase.Chasing;
        ramRemaining = 0f;
        ramDirection = Vector2.zero;
    }

    private void BeginRamRecovery()
    {
        if (ramTelegraph != null) ramTelegraph.enabled = false;
        ramPhase = RamPhase.Recovering;
        ramRemaining = ramRecoverySeconds;
        chaseVelocity = Vector2.zero;
        if (flankingVisual != null) flankingVisual.localRotation = flankingAuthoredRotation;
    }

    public void ConfigureFlankingExperiment(bool enabled, bool clockwise)
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (enabled) ConfigureWoundedRetreatExperiment(false);
#endif
        if (enabled) ConfigureCommittedRamExperiment(false);
        flankingExperiment = enabled;
        flankingApproach = enabled;
        flankingRemaining = enabled ? 3f : 0f;
        flankingWeight = 0f;
        flankingSide = clockwise ? -1f : 1f;
        if (!enabled) RestoreFlankingPose();
    }
    public float BaseChaseSpeed => normalSpeed;
    public bool HasAssaultDestination => assaultDestination.HasValue;
    public void SetAssaultDestination(Vector2? destination)
    {
        assaultDestination = destination;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (destination.HasValue) ConfigureWoundedRetreatExperiment(false);
#endif
        if (destination.HasValue) ResetRamCycle();
        if (destination.HasValue) RestoreFlankingPose();
    }
    public Transform Target => player;
    public bool IsAttackPaused => attackPauseRemaining > 0f;
    public void PauseForAttack(float timeout) => attackPauseRemaining = Mathf.Max(0f, timeout);
    public void ResumeAfterAttack() => attackPauseRemaining = 0f;
    private bool hasRunParameter;

    private Rigidbody2D rb;
    private Transform player;

    private float speedMultiplier = 1f;
    private float anomalySpeedMultiplier = 1f;
    private float worldRuleSpeedMultiplier = 1f;
    private Vector2 worldRuleExternalVelocity;
    private float stopTimer;
    private Vector2 knockbackVelocity;
    private float threatSpeedMultiplier = 1f;
    private float threatResponseSeconds;
    private Vector2 chaseVelocity;

    public void SetThreatMovement(float speed, float responseSeconds)
    {
        threatSpeedMultiplier = speed;
        threatResponseSeconds = responseSeconds;
    }
    private bool animatorStateInitialized;
    private bool animatorRunning;

    private static readonly Dictionary<(int, string), bool>
        runParameterCache = new();

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        hasRunParameter = HasBoolParameter(animator, runParameterName);
        if (animator != null && !string.IsNullOrEmpty(movementSpeedParameterName))
        {
            movementSpeedHash = Animator.StringToHash(movementSpeedParameterName);
            foreach (var parameter in animator.parameters)
                if (parameter.nameHash == movementSpeedHash && parameter.type == AnimatorControllerParameterType.Float)
                    hasMovementSpeed = true;
        }
        InitializeCrowdSteering();
        // Graphic is the existing parent of the animated sprite parts. Keep
        // their authored animation and visualRoot's facing scale independent.
        flankingVisual = visualRoot;
        while (flankingVisual != null && flankingVisual.parent != transform &&
               flankingVisual != transform)
            flankingVisual = flankingVisual.parent;
        if (flankingVisual == transform || (flankingVisual != null &&
            (flankingVisual.GetComponentInChildren<Collider2D>() != null ||
             flankingVisual.GetComponentInChildren<Rigidbody2D>() != null)))
            flankingVisual = null;
        if (flankingVisual != null)
            flankingAuthoredRotation = flankingVisual.localRotation;
    }

    private void Start()
    {
        FindPlayer();
    }

    private void OnEnable()
    {
        if (useLabTacticalMovement) ConfigureLabTacticalMovement();
        if (chasePattern == ChasePattern.Flanker)
            ConfigureFlankingExperiment(true, (GetInstanceID() & 2) == 0);
        else if (chasePattern == ChasePattern.Breacher)
            ConfigureCommittedRamExperiment(true);
    }

    private void FixedUpdate()
    {
        if (Time.timeScale == 0f)
            return;

        if (IsAttackPaused)
        {
            if (!EnemyDebugAiFreeze.IsFrozen)
                attackPauseRemaining = Mathf.Max(0f, attackPauseRemaining - Time.fixedDeltaTime);
            rb.linearVelocity = Vector2.zero;
            SetMovementSpeed(0f);
            return;
        }

        if (EnemyDebugAiFreeze.IsFrozen)
        {
            MoveWithoutAi();
            return;
        }

        if (player == null)
            FindPlayer();

        if (player == null)
        {
            ResetRamCycle();
            SetMovementSpeed(0f);
            return;
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        // Observe the wound before hit stop; lock the world line now, but do
        // not spend retreat time until ordinary locomotion can resume.
        TryBeginWoundedRetreat((Vector2)player.position - rb.position);
#endif
        if (stopTimer > 0f)
        {
            SetMovementSpeed(0f);
            stopTimer -= Time.fixedDeltaTime;
            rb.linearVelocity = Vector2.zero;
            rb.MovePosition(
                rb.position +
                (worldRuleExternalVelocity + AnomalyExternalVelocity) *
                Time.fixedDeltaTime
            );
            return;
        }

        MoveToPlayer();
    }

    private void MoveWithoutAi()
    {
        SetMovementSpeed(0f);
        if (animator != null && hasRunParameter &&
            (!animatorStateInitialized || animatorRunning))
        {
            animator.SetBool(runParameterName, false);
            animatorRunning = false;
            animatorStateInitialized = true;
        }

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
    }

    private void FindPlayer()
    {
        player = PlayerRuntimeReference.ResolvePlayerTransform(playerTag, true);
    }

    private void MoveToPlayer()
    {
        Vector2 offset = (Vector2)player.position - rb.position;
        float sqrDistance = offset.sqrMagnitude;
        if (assaultDestination.HasValue &&
            Vector2.Distance(rb.position, assaultDestination.Value) < .75f)
            assaultDestination = null;
        if (assaultDestination.HasValue)
            offset = assaultDestination.Value - rb.position;
        Vector2 productionDirection = offset.normalized;
        if (flankingExperiment && !assaultDestination.HasValue)
        {
            if (!spiralApproach)
            {
                flankingRemaining = Mathf.Max(0f, flankingRemaining - Time.fixedDeltaTime);
                if (sqrDistance <= 4f * 4f || flankingRemaining <= 0f) flankingApproach = false;
            }
            // Blend out before the ordinary chase takes over; never reverse side
            // or inspect the player's weapon layout to choose a supposedly weak spot.
            // A persistent inward+tangent vector forms a shrinking spiral, with a
            // stable side and a soft contact transition instead of endless circling.
            float targetWeight = spiralApproach ? Mathf.InverseLerp(.85f, 2.5f, Mathf.Sqrt(sqrDistance)) : flankingApproach
                ? Mathf.InverseLerp(4f, 5f, Mathf.Sqrt(sqrDistance)) * Mathf.Clamp01(flankingRemaining / .25f) : 0f;
            flankingWeight = Mathf.MoveTowards(flankingWeight, targetWeight, Time.fixedDeltaTime * 4f);
            if (flankingWeight > 0f)
            {
                Vector2 tangent = new Vector2(-productionDirection.y, productionDirection.x) * flankingSide;
                productionDirection = (productionDirection + tangent * ((spiralApproach ? 2.4f : .9f) * flankingWeight)).normalized;
            }
        }
        Vector2 direction = assaultDestination.HasValue ? productionDirection :
            ApplyCrowdSteering(productionDirection, player.position, Time.fixedDeltaTime);

        bool isRunning = sqrDistance <= aggroDistance * aggroDistance;

        float selectedSpeed = isRunning ? aggroSpeed : normalSpeed;
        bool ramControlsMovement = committedRamExperiment && !assaultDestination.HasValue;
        if (ramControlsMovement)
        {
            if (ramNeedsSeparation && sqrDistance >= ramSeparationRange * ramSeparationRange) ramNeedsSeparation = false;
            if (ramPhase == RamPhase.Chasing && sqrDistance <= ramApproachRange * ramApproachRange && sqrDistance > .0001f &&
                !ramNeedsSeparation && Time.time >= ramReadyAt)
            {
                ramDirection = offset.normalized;
                ramPhase = RamPhase.Preparing;
                ramRemaining = ramPrepareSeconds;
                // Tilt the safe graphic parent toward the committed world line.
                float localX = transform.InverseTransformDirection(ramDirection).x;
                flankingVisual.localRotation = flankingAuthoredRotation *
                    Quaternion.Euler(0f, 0f, -20f * (localX < 0f ? -1f : 1f));
            }
            else if (ramPhase != RamPhase.Chasing)
            {
                // The long lab charge spends its time with the movement command,
                // including its first/last fixed step, so it cannot overrun the cue.
                if (!exactRamDuration || ramPhase != RamPhase.Charging)
                    ramRemaining -= Time.fixedDeltaTime /
                        (ramPhase == RamPhase.Recovering ? AttackRecoveryMultiplier : 1f);
                if (ramRemaining <= 0f)
                {
                    if (ramPhase == RamPhase.Preparing)
                    {
                        ramPhase = RamPhase.Charging;
                        ramRemaining = ramChargeSeconds;
                    }
                    else if (ramPhase == RamPhase.Charging)
                    {
                        BeginRamRecovery();
                    }
                    else
                    {
                        ResetRamCycle();
                        ramNeedsSeparation = true;
                        ramReadyAt = Time.time + ramCooldownSeconds;
                    }
                }
            }
            if (ramPhase != RamPhase.Chasing)
            {
                direction = ramDirection;
                selectedSpeed = ramPhase == RamPhase.Charging ? aggroSpeed * ramSpeedMultiplier : 0f;
                isRunning = ramPhase == RamPhase.Charging;
            }
        }
        selectedSpeed *= speedMultiplier *
            anomalySpeedMultiplier *
            worldRuleSpeedMultiplier * threatSpeedMultiplier;
        if (exactRamDuration && ramControlsMovement && ramPhase == RamPhase.Charging)
        {
            selectedSpeed *= Mathf.Clamp01(ramRemaining / Time.fixedDeltaTime);
            ramRemaining = Mathf.Max(0f, ramRemaining - Time.fixedDeltaTime);
        }

        if (ramTelegraph != null)
        {
            ramTelegraph.enabled = ramControlsMovement && ramPhase == RamPhase.Preparing;
            if (ramTelegraph.enabled)
            {
                ramTelegraph.SetPosition(0, transform.position);
                float chargeDistance = aggroSpeed * ramSpeedMultiplier * ramChargeSeconds * speedMultiplier *
                    anomalySpeedMultiplier * worldRuleSpeedMultiplier * threatSpeedMultiplier;
                ramTelegraph.SetPosition(1, transform.position + (Vector3)(ramDirection * chargeDistance));
                float charge = Mathf.Clamp01(1f - ramRemaining / ramPrepareSeconds);
                ramTelegraph.startWidth = Mathf.Lerp(.075f, .15f, charge);
                ramTelegraph.endWidth = Mathf.Lerp(.04f, .08f, charge);
                if (exactRamDuration && ramWarningColliders != null && ramWarningColliders.Length > 0)
                {
                    // Both the trigger and the solid body can deal contact damage.
                    // Project their union onto the locked path: a conservative full
                    // swept corridor, including the body's offset and both end caps.
                    Bounds bounds = new Bounds(transform.position, Vector3.zero);
                    bool found = false;
                    foreach (var collider in ramWarningColliders)
                        if (collider != null && collider.enabled)
                        { if (!found) { bounds = collider.bounds; found = true; } else bounds.Encapsulate(collider.bounds); }
                    if (found)
                    {
                        Vector2 extents = bounds.extents;
                        Vector2 perpendicular = new(-ramDirection.y, ramDirection.x);
                        float halfWidth = Mathf.Abs(perpendicular.x) * extents.x + Mathf.Abs(perpendicular.y) * extents.y;
                        float cap = Mathf.Abs(ramDirection.x) * extents.x + Mathf.Abs(ramDirection.y) * extents.y;
                        ramTelegraph.SetPosition(0, bounds.center - (Vector3)(ramDirection * cap));
                        ramTelegraph.SetPosition(1, bounds.center + (Vector3)(ramDirection * (chargeDistance + cap)));
                        ramTelegraph.startWidth = ramTelegraph.endWidth = halfWidth * 2f;
                        // Charge remains readable without shrinking the shown damage area.
                        ramTelegraph.startColor = new Color(1f, .22f, .16f, Mathf.Lerp(.35f, .9f, charge));
                        ramTelegraph.endColor = new Color(1f, .65f, .2f, Mathf.Lerp(.3f, .65f, charge));
                    }
                }
            }
        }

        bool retreatControlsMovement = false;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        retreatControlsMovement = TickWoundedRetreat(ref direction);
#endif

        if (animator != null &&
            hasRunParameter &&
            (!animatorStateInitialized || animatorRunning != isRunning))
        {
            animator.SetBool(runParameterName, isRunning);
            animatorRunning = isRunning;
            animatorStateInitialized = true;
        }

        knockbackVelocity = Vector2.MoveTowards(
            knockbackVelocity,
            Vector2.zero,
            knockbackDecay * Time.fixedDeltaTime
        );

        Vector2 targetVelocity = direction * selectedSpeed;
        chaseVelocity = threatResponseSeconds > .001f && !retreatControlsMovement && !(ramControlsMovement && ramPhase != RamPhase.Chasing)
            ? Vector2.Lerp(chaseVelocity, targetVelocity, 1f - Mathf.Exp(-Time.fixedDeltaTime / threatResponseSeconds))
            : targetVelocity;
        Vector2 locomotion = chaseVelocity;
        if (knockbackVelocity.sqrMagnitude > .0001f)
        {
            // Keep lateral steering, but let a hit displace the enemy instead of
            // spending the impulse fighting its own pursuit for the entire decay.
            Vector2 impulseDirection = knockbackVelocity.normalized;
            float opposingSpeed = Vector2.Dot(locomotion, impulseDirection);
            if (opposingSpeed < 0f) locomotion -= impulseDirection * opposingSpeed;
        }
        Vector2 movement = locomotion +
            knockbackVelocity +
            worldRuleExternalVelocity +
            AnomalyExternalVelocity;
        Vector2 nextPosition = rb.position + movement * Time.fixedDeltaTime;

        // MovePosition's command is the locomotion source, not transient linearVelocity.
        SetMovementSpeed(movement.magnitude);
        rb.MovePosition(nextPosition);

        if (flipVisual)
            UpdateVisualFacing(visualRoot, retreatControlsMovement ? (Vector2)player.position - rb.position : direction);
        if (!committedRamExperiment && !retreatControlsMovement) UpdateFlankingPose(direction);
    }

    private void UpdateFlankingPose(Vector2 direction)
    {
        if (!flankingExperiment || assaultDestination.HasValue)
        {
            RestoreFlankingPose();
            return;
        }
        if (flankingVisual == null) return;
        // Absolute offset, independent of facing flips and previous frames.
        var target = flankingAuthoredRotation *
            Quaternion.Euler(0f, 0f, -12f * Mathf.Clamp(direction.x, -1f, 1f) * flankingWeight);
        flankingVisual.localRotation = Quaternion.Slerp(flankingVisual.localRotation, target, 1f - Mathf.Exp(-12f * Time.fixedDeltaTime));
        flankingPoseApplied = true;
    }

    private void RestoreFlankingPose()
    {
        if (flankingPoseApplied && flankingVisual != null)
            flankingVisual.localRotation = flankingAuthoredRotation;
        flankingPoseApplied = false;
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
        knockbackVelocity = direction.normalized * force;
    }

    public override void StopAfterHit()
    {
        if (committedRamExperiment && ramPhase != RamPhase.Chasing)
        {
            if (ramPhase != RamPhase.Recovering) BeginRamRecovery();
            return;
        }
        stopTimer = stopAfterHitDuration;
    }
    private void OnDisable()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        ConfigureWoundedRetreatExperiment(false);
        woundedRetreatUsed = false;
#endif
        knockbackVelocity = Vector2.zero;
        chaseVelocity = Vector2.zero;
        stopTimer = 0f;
        attackRecoveryModifiers.Clear();
        anomalySpeedMultiplier = 1f;
        SetThreatMovement(1f, 0f);
        assaultDestination = null;
        ConfigureCommittedRamExperiment(false);
        ConfigureFlankingExperiment(false, false);
        ResumeAfterAttack();
        SetMovementSpeed(0f);
        ReleaseCrowdSteering();
        ClearAnomalyExternalVelocities();
    }
    private void SetMovementSpeed(float speed)
    {
        if (animator != null && hasMovementSpeed) animator.SetFloat(movementSpeedHash, speed);
    }
    private static bool HasBoolParameter(
    Animator targetAnimator,
    string parameterName)
    {
        if (targetAnimator == null ||
            string.IsNullOrWhiteSpace(parameterName))
        {
            return false;
        }

        RuntimeAnimatorController controller =
            targetAnimator.runtimeAnimatorController;
        int controllerId = controller != null
            ? controller.GetInstanceID()
            : targetAnimator.GetInstanceID();
        (int, string) cacheKey = (controllerId, parameterName);

        if (runParameterCache.TryGetValue(cacheKey, out bool cachedResult))
            return cachedResult;

        foreach (AnimatorControllerParameter parameter
                 in targetAnimator.parameters)
        {
            if (parameter.type == AnimatorControllerParameterType.Bool &&
                parameter.name == parameterName)
            {
                runParameterCache[cacheKey] = true;
                return true;
            }
        }

        runParameterCache[cacheKey] = false;
        Debug.LogWarning(
            $"[EnemyChaseMovement] '{targetAnimator.gameObject.name}' " +
            $"has no Bool parameter '{parameterName}'.",
            targetAnimator
        );

        return false;
    }
}
