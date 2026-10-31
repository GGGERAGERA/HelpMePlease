using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(Rigidbody2D))]
public class EnemyChaseMovement : EnemyMovement
{
    public enum ChasePattern { Direct, Flanker, Breacher }
    [Header("Production behaviour")]
    [SerializeField] private ChasePattern chasePattern;
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
    // One committed attack in the arena, including its recovery window.
    private static EnemyChaseMovement ramOwner;
    private static float ramNextAllowedAt;
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

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetRamAdmission()
    {
        ramOwner = null;
        ramNextAllowedAt = 0f;
    }
    public bool IsCommittedRamExperiment => committedRamExperiment;
    public bool CanUseCommittedRam => ResolveSafeVisual() != null;

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
        committedRamExperiment = enabled && !flankingExperiment && CanUseCommittedRam;
    }

    private void ResetRamCycle()
    {
        ReleaseRamAdmission();
        if (ramTelegraph != null) ramTelegraph.enabled = false;
        if (committedRamExperiment && flankingVisual != null)
            flankingVisual.localRotation = flankingAuthoredRotation;
        if (committedRamExperiment) chaseVelocity = Vector2.zero;
        ramPhase = RamPhase.Chasing;
        ramRemaining = 0f;
        ramDirection = Vector2.zero;
    }

    private void ReleaseRamAdmission()
    {
        if (ramOwner != this) return;
        ramOwner = null;
        ramNextAllowedAt = Time.time + .35f;
    }

    private void BeginRamRecovery()
    {
        if (ramTelegraph != null) ramTelegraph.enabled = false;
        ramPhase = RamPhase.Recovering;
        ramRemaining = 1.2f;
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
            flankingRemaining = Mathf.Max(0f, flankingRemaining - Time.fixedDeltaTime);
            if (sqrDistance <= 4f * 4f || flankingRemaining <= 0f) flankingApproach = false;
            if (flankingApproach)
            {
                Vector2 tangent = new Vector2(-productionDirection.y, productionDirection.x) * flankingSide;
                productionDirection = (productionDirection + tangent * .9f).normalized;
            }
        }
        Vector2 direction = assaultDestination.HasValue ? productionDirection :
            ApplyCrowdSteering(productionDirection, player.position, Time.fixedDeltaTime);

        bool isRunning = sqrDistance <= aggroDistance * aggroDistance;

        float selectedSpeed = isRunning ? aggroSpeed : normalSpeed;
        bool ramControlsMovement = committedRamExperiment && !assaultDestination.HasValue;
        if (ramControlsMovement)
        {
            if (ramPhase == RamPhase.Chasing && sqrDistance <= 25f && sqrDistance > .0001f &&
                ramOwner == null && Time.time >= ramNextAllowedAt)
            {
                ramOwner = this;
                ramDirection = offset.normalized;
                ramPhase = RamPhase.Preparing;
                ramRemaining = .65f;
                // Tilt the safe graphic parent toward the committed world line.
                float localX = transform.InverseTransformDirection(ramDirection).x;
                flankingVisual.localRotation = flankingAuthoredRotation *
                    Quaternion.Euler(0f, 0f, -20f * (localX < 0f ? -1f : 1f));
            }
            else if (ramPhase != RamPhase.Chasing)
            {
                ramRemaining -= Time.fixedDeltaTime /
                    (ramPhase == RamPhase.Recovering ? AttackRecoveryMultiplier : 1f);
                if (ramRemaining <= 0f)
                {
                    if (ramPhase == RamPhase.Preparing)
                    {
                        ramPhase = RamPhase.Charging;
                        ramRemaining = .45f;
                    }
                    else if (ramPhase == RamPhase.Charging)
                    {
                        BeginRamRecovery();
                    }
                    else ResetRamCycle();
                }
            }
            if (ramPhase != RamPhase.Chasing)
            {
                direction = ramDirection;
                selectedSpeed = ramPhase == RamPhase.Charging ? aggroSpeed * 1.75f : 0f;
                isRunning = ramPhase == RamPhase.Charging;
            }
        }
        selectedSpeed *= speedMultiplier *
            anomalySpeedMultiplier *
            worldRuleSpeedMultiplier * threatSpeedMultiplier;

        if (ramTelegraph != null)
        {
            ramTelegraph.enabled = ramControlsMovement && ramPhase == RamPhase.Preparing;
            if (ramTelegraph.enabled)
            {
                ramTelegraph.SetPosition(0, transform.position);
                float chargeDistance = aggroSpeed * 1.75f * .45f * speedMultiplier *
                    anomalySpeedMultiplier * worldRuleSpeedMultiplier * threatSpeedMultiplier;
                ramTelegraph.SetPosition(1, transform.position + (Vector3)(ramDirection * chargeDistance));
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
        if (!flankingExperiment || !flankingApproach || assaultDestination.HasValue)
        {
            RestoreFlankingPose();
            return;
        }
        if (flankingVisual == null) return;
        // Absolute offset, independent of facing flips and previous frames.
        flankingVisual.localRotation = flankingAuthoredRotation *
            Quaternion.Euler(0f, 0f, -12f * Mathf.Clamp(direction.x, -1f, 1f));
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
