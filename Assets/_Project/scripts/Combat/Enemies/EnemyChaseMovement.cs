using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(Rigidbody2D))]
public class EnemyChaseMovement : EnemyMovement
{
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
    }

    private void Start()
    {
        FindPlayer();
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
            SetMovementSpeed(0f);
            return;
        }

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
        GameObject playerObject = GameObject.FindGameObjectWithTag(playerTag);

        if (playerObject != null)
            player = playerObject.transform;
    }

    private void MoveToPlayer()
    {
        Vector2 offset = (Vector2)player.position - rb.position;
        float sqrDistance = offset.sqrMagnitude;
        Vector2 productionDirection = offset.normalized;
        Vector2 direction = ApplyCrowdSteering(productionDirection,
            player.position, Time.fixedDeltaTime);

        bool isRunning = sqrDistance <= aggroDistance * aggroDistance;

        float selectedSpeed = isRunning ? aggroSpeed : normalSpeed;
        selectedSpeed *= speedMultiplier *
            anomalySpeedMultiplier *
            worldRuleSpeedMultiplier;

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

        Vector2 movement = direction * selectedSpeed +
            knockbackVelocity +
            worldRuleExternalVelocity +
            AnomalyExternalVelocity;
        Vector2 nextPosition = rb.position + movement * Time.fixedDeltaTime;

        // MovePosition's command is the locomotion source, not transient linearVelocity.
        SetMovementSpeed(movement.magnitude);
        rb.MovePosition(nextPosition);

        UpdateVisual(direction);
    }

    private void UpdateVisual(Vector2 direction)
    {
        if (!flipVisual || visualRoot == null)
            return;

        if (Mathf.Abs(direction.x) < 0.05f)
            return;

        Vector3 scale = visualRoot.localScale;
        float targetScaleX =
            direction.x > 0f ? -Mathf.Abs(scale.x) : Mathf.Abs(scale.x);

        if (Mathf.Approximately(scale.x, targetScaleX))
            return;

        scale.x = targetScaleX;
        visualRoot.localScale = scale;
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
        stopTimer = stopAfterHitDuration;
    }
    private void OnDisable()
    {
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
