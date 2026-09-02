using UnityEngine;

public class CharacterMovement2D : MonoBehaviour, IAnomalyExternalVelocity
{
    [Header("Движение")]
    public float speed = 5f;

    private Rigidbody2D rb;
    private Animator animator;

    [Header("Movement Feel")]
    [SerializeField] private float acceleration = 18f;
    [SerializeField] private float deceleration = 22f;

    [Header("Hit Knockback")]
    [SerializeField, Min(0f)] private float hitKnockbackSpeed = 4f;
    [SerializeField, Min(0.01f)] private float hitKnockbackDuration = 0.12f;

    [Header("Dash")]
    [SerializeField, Min(0.01f)] private float dashDistance = 3f;
    [SerializeField, Min(0.01f)] private float dashDuration = 0.15f;
    [SerializeField, Min(0f)] private float dashCooldown = 3f;
    [SerializeField] private KeyCode dashKey = KeyCode.Space;

    private Vector2 moveInput;
    private Vector2 currentVelocity;
    private Vector2 lastMoveDirection = Vector2.right;
    private Vector2 dashDirection;
    private float dashTimeRemaining;
    private float dashCooldownRemaining;
    private bool isDashing;
    private Vector2 hitKnockbackVelocity;
    private float hitKnockbackTimeRemaining;
    private float anomalySpeedMultiplier = 1f;
    private float legacyAnomalySpeedMultiplier = 1f;
    private readonly AnomalySpeedMultiplierStack anomalySpeedSources = new();
    private float worldRuleSpeedMultiplier = 1f;
    private float runUpgradeSpeedMultiplier = 1f;
    private Vector2 worldRuleExternalVelocity;
    private readonly AnomalyExternalVelocityStack
        anomalyExternalVelocity = new();

    private const float DashCollisionSkin = 0.02f;
    private readonly RaycastHit2D[] dashHits = new RaycastHit2D[8];
    private ContactFilter2D dashContactFilter;

    [SerializeField] private Transform visualRoot;
    private float visualRootScaleMagnitudeX = 1f;
    private float facingScaleSign = 1f;
    private bool hasFacingDirection;

    public Transform VisualRoot => visualRoot;
    public Vector2 LastMoveDirection => lastMoveDirection;

    public void SetVisualRoot(Transform value)
    {
        visualRoot = value;
        animator = visualRoot != null
            ? visualRoot.GetComponentInParent<Animator>()
            : GetComponentInChildren<Animator>();
        CacheVisualRootScale();
        ApplyFacing();
    }

    public float DashCooldown => Mathf.Max(0f, dashCooldown);
    public float DashCooldownRemaining =>
        Mathf.Max(0f, dashCooldownRemaining);
    public float DashCooldownProgress => dashCooldown <= 0f
        ? 1f
        : 1f - Mathf.Clamp01(dashCooldownRemaining / dashCooldown);
    public bool IsDashReady => !isDashing && dashCooldownRemaining <= 0f;
    public KeyCode DashKey => dashKey;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    public float DebugAcceleration => acceleration;
    public float DebugDeceleration => deceleration;

    public void SetDebugMoveSpeed(float value) => speed = Mathf.Max(0f, value);
    public void SetDebugAcceleration(float value) =>
        acceleration = Mathf.Max(0f, value);
    public void SetDebugDeceleration(float value) =>
        deceleration = Mathf.Max(0f, value);
#endif

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        animator = GetComponentInChildren<Animator>();
        CacheVisualRootScale();

        if (rb != null)
        {
            rb.gravityScale = 0f;
            rb.freezeRotation = true;
        }

        dashContactFilter = new ContactFilter2D
        {
            useTriggers = false
        };
    }

    void Update()
    {
        moveInput = new Vector2(
            Input.GetAxisRaw("Horizontal"),
            Input.GetAxisRaw("Vertical")
        ).normalized;

        if (moveInput.sqrMagnitude > 0.01f)
            lastMoveDirection = moveInput;

        if (Time.timeScale > 0f)
        {
            dashCooldownRemaining = Mathf.Max(
                0f,
                dashCooldownRemaining - Time.deltaTime
            );

            if (Input.GetKeyDown(dashKey))
                TryStartDash();
        }

        UpdateFacing(moveInput.x);

        if (animator != null)
            animator.SetFloat("Speed", moveInput.magnitude);
    }

    private void CacheVisualRootScale()
    {
        if (visualRoot == null)
            return;

        float scaleX = visualRoot.localScale.x;
        visualRootScaleMagnitudeX = Mathf.Max(0.0001f, Mathf.Abs(scaleX));
        if (!hasFacingDirection && !Mathf.Approximately(scaleX, 0f))
            facingScaleSign = Mathf.Sign(scaleX);
    }

    private void UpdateFacing(float horizontalInput)
    {
        if (horizontalInput == 0f || visualRoot == null)
            return;

        facingScaleSign = -Mathf.Sign(horizontalInput);
        hasFacingDirection = true;
        ApplyFacing();
    }

    private void ApplyFacing()
    {
        if (visualRoot == null)
            return;

        Vector3 scale = visualRoot.localScale;
        scale.x = visualRootScaleMagnitudeX * facingScaleSign;
        visualRoot.localScale = scale;
    }
    public void AddMoveSpeed(float amount)
    {
        speed += amount;
    }
    public void AddMoveSpeedPercent(float percent)
    {
        speed *= 1f + percent;
    }
    public void SetAnomalySpeedMultiplier(float multiplier)
    {
        legacyAnomalySpeedMultiplier = Mathf.Max(0.1f, multiplier);
        RefreshAnomalySpeedMultiplier();
    }
    public void SetAnomalySpeedMultiplier(
        Object source,
        float multiplier)
    {
        anomalySpeedSources.Set(source, multiplier);
        RefreshAnomalySpeedMultiplier();
    }
    public void RemoveAnomalySpeedMultiplier(Object source)
    {
        anomalySpeedSources.Remove(source);
        RefreshAnomalySpeedMultiplier();
    }
    private void RefreshAnomalySpeedMultiplier()
    {
        anomalySpeedMultiplier = legacyAnomalySpeedMultiplier *
            anomalySpeedSources.Value;
    }
    public void SetWorldRuleSpeedMultiplier(float multiplier)
    {
        worldRuleSpeedMultiplier = Mathf.Max(0.1f, multiplier);
    }
    public void SetWorldRuleExternalVelocity(Vector2 velocity)
    {
        worldRuleExternalVelocity = velocity;
    }
    public void SetRunUpgradeMoveSpeedMultiplier(float multiplier)
    {
        multiplier = Mathf.Max(0.1f, multiplier);
        speed = speed / Mathf.Max(0.1f, runUpgradeSpeedMultiplier) * multiplier;
        runUpgradeSpeedMultiplier = multiplier;
    }
    public float RunUpgradeMoveSpeedMultiplier => runUpgradeSpeedMultiplier;
    public Component ExternalVelocityComponent => this;
    public void SetAnomalyExternalVelocity(
        Object source,
        Vector2 velocity)
    {
        anomalyExternalVelocity.Set(source, velocity);
    }
    public void RemoveAnomalyExternalVelocity(Object source)
    {
        anomalyExternalVelocity.Remove(source);
    }

    private void FixedUpdate()
    {
        if (rb == null)
            return;

        if (isDashing)
        {
            UpdateDash();
            return;
        }

        Vector2 targetVelocity =
            moveInput *
            speed *
            anomalySpeedMultiplier *
            worldRuleSpeedMultiplier;

        float rate = moveInput.sqrMagnitude > 0.01f
            ? acceleration
            : deceleration;

        currentVelocity = Vector2.MoveTowards(
            currentVelocity,
            targetVelocity,
            rate * Time.fixedDeltaTime
        );


        UpdateHitKnockback();

        rb.MovePosition(
            rb.position +
            (currentVelocity +
             hitKnockbackVelocity +
             worldRuleExternalVelocity +
             anomalyExternalVelocity.Value) * Time.fixedDeltaTime
        );

    }

    public void ApplyKnockback(Vector2 direction)
    {
        if (direction.sqrMagnitude <= 0.0001f)
            return;

        if (isDashing)
            EndDash();

        hitKnockbackVelocity = direction.normalized * hitKnockbackSpeed;
        hitKnockbackTimeRemaining = hitKnockbackDuration;
    }

    private void UpdateHitKnockback()
    {
        if (hitKnockbackTimeRemaining <= 0f)
        {
            hitKnockbackVelocity = Vector2.zero;
            return;
        }

        hitKnockbackTimeRemaining = Mathf.Max(
            0f,
            hitKnockbackTimeRemaining - Time.fixedDeltaTime
        );

        float remainingRatio = hitKnockbackTimeRemaining /
            Mathf.Max(0.01f, hitKnockbackDuration);
        hitKnockbackVelocity = hitKnockbackVelocity.normalized *
            hitKnockbackSpeed * remainingRatio;
    }

    private void TryStartDash()
    {
        if (isDashing || dashCooldownRemaining > 0f || rb == null)
            return;

        Vector2 direction = moveInput.sqrMagnitude > 0.01f
            ? moveInput
            : lastMoveDirection;

        if (direction.sqrMagnitude <= 0.01f)
            return;

        dashDirection = direction.normalized;
        dashTimeRemaining = Mathf.Max(0.01f, dashDuration);
        dashCooldownRemaining = Mathf.Max(0f, dashCooldown);
        currentVelocity = Vector2.zero;
        isDashing = true;
    }

    private void UpdateDash()
    {
        float stepTime = Mathf.Min(Time.fixedDeltaTime, dashTimeRemaining);
        float dashSpeed = Mathf.Max(0.01f, dashDistance) /
            Mathf.Max(0.01f, dashDuration);
        float desiredDistance = dashSpeed * stepTime;
        float allowedDistance = GetAllowedDashDistance(desiredDistance);

        if (allowedDistance > 0f)
        {
            rb.MovePosition(
                rb.position +
                dashDirection * allowedDistance +
                (worldRuleExternalVelocity +
                 anomalyExternalVelocity.Value) * stepTime
            );
        }

        dashTimeRemaining -= stepTime;

        bool hitObstacle = allowedDistance + DashCollisionSkin < desiredDistance;

        if (dashTimeRemaining <= 0f || hitObstacle)
            EndDash();
    }

    private float GetAllowedDashDistance(float desiredDistance)
    {
        int hitCount = rb.Cast(
            dashDirection,
            dashContactFilter,
            dashHits,
            desiredDistance + DashCollisionSkin
        );

        float allowedDistance = desiredDistance;

        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit2D hit = dashHits[i];

            if (hit.collider == null)
                continue;

            allowedDistance = Mathf.Min(
                allowedDistance,
                Mathf.Max(0f, hit.distance - DashCollisionSkin)
            );
        }

        return allowedDistance;
    }

    private void EndDash()
    {
        isDashing = false;
        dashTimeRemaining = 0f;
        currentVelocity = Vector2.zero;
    }

    private void OnDisable()
    {
        moveInput = Vector2.zero;
        currentVelocity = Vector2.zero;
        dashDirection = Vector2.zero;
        dashTimeRemaining = 0f;
        dashCooldownRemaining = 0f;
        isDashing = false;
        hitKnockbackVelocity = Vector2.zero;
        hitKnockbackTimeRemaining = 0f;
        worldRuleExternalVelocity = Vector2.zero;
        anomalyExternalVelocity.Clear();
        anomalySpeedSources.Clear();
        legacyAnomalySpeedMultiplier = 1f;
        anomalySpeedMultiplier = 1f;
    }
}
