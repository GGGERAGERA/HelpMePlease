using UnityEngine;

public class CharacterMovement2D : MonoBehaviour
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
    private float worldRuleSpeedMultiplier = 1f;
    private Vector2 worldRuleExternalVelocity;

    private const float DashCollisionSkin = 0.02f;
    private readonly RaycastHit2D[] dashHits = new RaycastHit2D[8];
    private ContactFilter2D dashContactFilter;

    [SerializeField] private Transform visualRoot;

    public float DashCooldown => Mathf.Max(0f, dashCooldown);
    public float DashCooldownRemaining =>
        Mathf.Max(0f, dashCooldownRemaining);
    public float DashCooldownProgress => dashCooldown <= 0f
        ? 1f
        : 1f - Mathf.Clamp01(dashCooldownRemaining / dashCooldown);
    public bool IsDashReady => !isDashing && dashCooldownRemaining <= 0f;
    public KeyCode DashKey => dashKey;

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        animator = GetComponentInChildren<Animator>();

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

        if (moveInput.x != 0 && visualRoot != null)
        {
            Vector3 scale = visualRoot.localScale;
            scale.x = Mathf.Abs(scale.x) * -Mathf.Sign(moveInput.x);
            visualRoot.localScale = scale;
            
            /*
            if (scale.x < 0)
                visualRoot.GetComponent<SpriteRenderer>().flipX = true;
            else
                visualRoot.GetComponent<SpriteRenderer>().flipX = false;
            */
        }

        if (animator != null)
            animator.SetFloat("Speed", moveInput.magnitude);
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
        anomalySpeedMultiplier = Mathf.Max(0.1f, multiplier);
    }
    public void SetWorldRuleSpeedMultiplier(float multiplier)
    {
        worldRuleSpeedMultiplier = Mathf.Max(0.1f, multiplier);
    }
    public void SetWorldRuleExternalVelocity(Vector2 velocity)
    {
        worldRuleExternalVelocity = velocity;
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
             worldRuleExternalVelocity) * Time.fixedDeltaTime
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
                worldRuleExternalVelocity * stepTime
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
    }
}
