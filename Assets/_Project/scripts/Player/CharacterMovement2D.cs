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

    [Header("Knockback")]
    [SerializeField] private float knockbackDecay = 18f;

    private Vector2 knockbackVelocity;

    private Vector2 moveInput;
    private Vector2 currentVelocity;

    [SerializeField] private Transform visualRoot;

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        animator = GetComponentInChildren<Animator>();

        if (rb != null)
        {
            rb.gravityScale = 0f;
            rb.freezeRotation = true;
        }
    }

    void Update()
    {
        moveInput = new Vector2(
            Input.GetAxisRaw("Horizontal"),
            Input.GetAxisRaw("Vertical")
        ).normalized;

        if (moveInput.x != 0 && visualRoot != null)
        {
            Vector3 scale = visualRoot.localScale;
            scale.x = Mathf.Abs(scale.x) * -Mathf.Sign(moveInput.x);
            visualRoot.localScale = scale;
        }

        if (animator != null)
            animator.SetFloat("Speed", moveInput.magnitude);
    }

    private void FixedUpdate()
    {
        if (rb == null)
            return;

        Vector2 targetVelocity = moveInput * speed;

        float rate = moveInput.sqrMagnitude > 0.01f
            ? acceleration
            : deceleration;

        currentVelocity = Vector2.MoveTowards(
            currentVelocity,
            targetVelocity,
            rate * Time.fixedDeltaTime
        );

        Vector2 finalVelocity = currentVelocity + knockbackVelocity;

        rb.MovePosition(rb.position + finalVelocity * Time.fixedDeltaTime);

        knockbackVelocity = Vector2.MoveTowards(
            knockbackVelocity,
            Vector2.zero,
            knockbackDecay * Time.fixedDeltaTime
        );
    }
    public void ApplyKnockback(Vector2 direction, float force)
    {
        if (direction.sqrMagnitude < 0.001f)
            return;

        knockbackVelocity = direction.normalized * force;
    }
}