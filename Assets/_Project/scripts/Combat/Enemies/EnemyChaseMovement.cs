using UnityEngine;

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
    private bool hasRunParameter;

    private Rigidbody2D rb;
    private Transform player;

    private float speedMultiplier = 1f;
    private float stopTimer;
    private Vector2 knockbackVelocity;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        hasRunParameter = HasBoolParameter(animator, runParameterName);
    }

    private void Start()
    {
        FindPlayer();
    }

    private void FixedUpdate()
    {
        if (Time.timeScale == 0f)
            return;

        if (player == null)
            FindPlayer();

        if (player == null)
            return;

        if (stopTimer > 0f)
        {
            stopTimer -= Time.fixedDeltaTime;
            rb.linearVelocity = Vector2.zero;
            return;
        }

        MoveToPlayer();
    }

    private void FindPlayer()
    {
        GameObject playerObject = GameObject.FindGameObjectWithTag(playerTag);

        if (playerObject != null)
            player = playerObject.transform;
    }

    private void MoveToPlayer()
    {
        Vector2 direction = ((Vector2)player.position - rb.position).normalized;
        float distance = Vector2.Distance(rb.position, player.position);

        bool isRunning = distance <= aggroDistance;

        float selectedSpeed = isRunning ? aggroSpeed : normalSpeed;
        selectedSpeed *= speedMultiplier;

        if (animator != null && hasRunParameter)
            animator.SetBool(runParameterName, isRunning);

        knockbackVelocity = Vector2.MoveTowards(
            knockbackVelocity,
            Vector2.zero,
            knockbackDecay * Time.fixedDeltaTime
        );

        Vector2 movement = direction * selectedSpeed + knockbackVelocity;
        Vector2 nextPosition = rb.position + movement * Time.fixedDeltaTime;

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
        scale.x = direction.x > 0f ? -Mathf.Abs(scale.x) : Mathf.Abs(scale.x);
        visualRoot.localScale = scale;
    }

    public override void SetSpeedMultiplier(float multiplier)
    {
        speedMultiplier = Mathf.Max(0.1f, multiplier);
    }

    public override void ApplyKnockback(Vector2 direction, float force)
    {
        knockbackVelocity = direction.normalized * force;
    }

    public override void StopAfterHit()
    {
        stopTimer = stopAfterHitDuration;
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

        foreach (AnimatorControllerParameter parameter
                 in targetAnimator.parameters)
        {
            if (parameter.type == AnimatorControllerParameterType.Bool &&
                parameter.name == parameterName)
            {
                return true;
            }
        }

        Debug.LogWarning(
            $"[EnemyChaseMovement] '{targetAnimator.gameObject.name}' " +
            $"has no Bool parameter '{parameterName}'.",
            targetAnimator
        );

        return false;
    }
}