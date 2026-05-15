using UnityEngine;

public class EnemyMovement : MonoBehaviour
{
    private Transform target;
    private SpriteRenderer spriteRenderer;
    private Rigidbody2D rb;

    [SerializeField] private Transform visualRoot;

    [Header("Chase States")]
    [SerializeField] private float runDistance = 6f;
    [SerializeField] private float walkSpeed = 1.5f;
    [SerializeField] private float runSpeed = 3f;
    [SerializeField] private Animator animator;
    private bool isRunning;

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        if (animator == null)
            animator = GetComponentInChildren<Animator>();
        if (rb == null)
            Debug.LogError("EnemyMovement: Rigidbody2D is required for smooth collisions!");

        spriteRenderer = GetComponent<SpriteRenderer>();
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

        Vector2 direction = (target.position - transform.position).normalized;

        float distanceToPlayer = Vector2.Distance(transform.position, target.position);
        isRunning = distanceToPlayer <= runDistance;

        float currentSpeed = isRunning ? runSpeed : walkSpeed;

        if (animator != null)
            animator.SetBool("IsRunning", isRunning);

        Vector2 newPosition = rb.position + direction * currentSpeed * Time.fixedDeltaTime;

        FlipVisual(direction.x);
        rb.MovePosition(newPosition);
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
}
