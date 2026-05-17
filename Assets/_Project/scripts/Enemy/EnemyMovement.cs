using UnityEngine;

public class EnemyMovement : MonoBehaviour
{
    private Transform target;
    private Rigidbody2D rb;

    [SerializeField] private Transform visualRoot;

    [Header("Chase States")]
    [SerializeField] private float runDistance = 6f;
    [SerializeField] private float walkSpeed = 1.5f;
    [SerializeField] private float runSpeed = 3f;
    [SerializeField] private Animator animator;

    [Header("Movement Feel")]
    [SerializeField] private float acceleration = 12f;

    [Header("Knockback")]
    [SerializeField] private float knockbackDecay = 18f;

    [Header("Hit Stun")]
    [SerializeField] private float hitStunDuration = 0.06f;

    private float hitStunTimer;

    private Vector2 knockbackVelocity;
    private Vector2 currentVelocity;
    private bool isRunning;
    private bool hasIsRunningParameter;

    void Start()
    {
        if (animator != null)
        {
            foreach (AnimatorControllerParameter parameter in animator.parameters)
            {
                if (parameter.name == "IsRunning")
                {
                    hasIsRunningParameter = true;
                    break;
                }
            }
        }
        rb = GetComponent<Rigidbody2D>();
        if (animator == null)
            animator = GetComponentInChildren<Animator>();
        if (rb == null)
            Debug.LogError("EnemyMovement: Rigidbody2D is required for smooth collisions!");

        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null)
            SetTarget(playerObj.transform);
    }

    public void SetTarget(Transform newTarget)
    {
        target = newTarget;
    }

    void FixedUpdate()
    {
        if (target == null || rb == null) return;

        if (hitStunTimer > 0f)
        {
            hitStunTimer -= Time.fixedDeltaTime;

            Vector2 stunVelocity = knockbackVelocity;

            rb.MovePosition(rb.position + stunVelocity * Time.fixedDeltaTime);

            knockbackVelocity = Vector2.MoveTowards(
                knockbackVelocity,
                Vector2.zero,
                knockbackDecay * Time.fixedDeltaTime
            );

            return;
        }

        Vector2 direction = ((Vector2)target.position - rb.position).normalized;

        float distanceToPlayer = Vector2.Distance(rb.position, target.position);
        isRunning = distanceToPlayer <= runDistance;

        float targetSpeed = isRunning ? runSpeed : walkSpeed;

        if (animator != null && hasIsRunningParameter)
            animator.SetBool("IsRunning", isRunning);

        Vector2 targetVelocity = direction * targetSpeed;

        currentVelocity = Vector2.MoveTowards(
            currentVelocity,
            targetVelocity,
            acceleration * Time.fixedDeltaTime
        );

        Vector2 finalVelocity = currentVelocity + knockbackVelocity;

        rb.MovePosition(rb.position + finalVelocity * Time.fixedDeltaTime);

        knockbackVelocity = Vector2.MoveTowards(
            knockbackVelocity,
            Vector2.zero,
            knockbackDecay * Time.fixedDeltaTime
        );

        FlipVisual(currentVelocity.x);
    }
    public void SetSpeedMultiplier(float multiplier)
    {
        walkSpeed *= multiplier;
        runSpeed *= multiplier;
    }
    private void FlipVisual(float directionX)
    {
        if (visualRoot == null)
            return;

        if (Mathf.Abs(directionX) < 0.01f)
            return;

        Vector3 scale = visualRoot.localScale;

        // если зомби ИЗНАЧАЛЬНО смотрит влево:
        scale.x = directionX > 0 ? -Mathf.Abs(scale.x) : Mathf.Abs(scale.x);

        visualRoot.localScale = scale;
    }
    public void ApplyKnockback(Vector2 direction, float force)
    {
        knockbackVelocity += direction.normalized * force;
        hitStunTimer = hitStunDuration;
    }
}
