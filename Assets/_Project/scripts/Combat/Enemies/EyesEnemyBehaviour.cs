using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public sealed class EyesEnemyBehaviour : EnemyMovement
{
    [Header("Existing prefab references")]
    [SerializeField] private Transform eyeVisual;

    [SerializeField, Min(0f)] private float moveSpeed = 1.2f;

    private Rigidbody2D body;
    private Transform player;
    private WorldRuleVisual worldRuleVisual;
    private float speedMultiplier = 1f;
    private float anomalySpeedMultiplier = 1f;
    private float worldRuleSpeedMultiplier = 1f;
    private Vector2 worldRuleExternalVelocity;

    private const float EffectStartDistance = 8f;
    private const float EffectMaxDistance = 3f;
    private const float MaxRadiusReduction = 0.4f;

    private void Awake()
    {
        body = GetComponent<Rigidbody2D>();
    }

    private void Start()
    {
        player = GameObject.FindGameObjectWithTag("Player")?.transform;
        worldRuleVisual = FindFirstObjectByType<WorldRuleVisual>();
        UpdateVisibilityEffect();
    }

    private void Update()
    {
        if (Time.timeScale == 0f || player == null)
            return;

        UpdateVisibilityEffect();
    }

    private void FixedUpdate()
    {
        if (Time.timeScale == 0f || player == null)
            return;

        Vector2 offset = (Vector2)player.position - body.position;
        Vector2 direction = offset.sqrMagnitude > Mathf.Epsilon
            ? offset.normalized
            : Vector2.zero;
        Vector2 movement =
            direction * moveSpeed * speedMultiplier *
            anomalySpeedMultiplier * worldRuleSpeedMultiplier +
            worldRuleExternalVelocity + AnomalyExternalVelocity;

        body.MovePosition(body.position + movement * Time.fixedDeltaTime);
        LookAtPlayer(direction);
    }

    private void LookAtPlayer(Vector2 direction)
    {
        if (eyeVisual == null || Mathf.Abs(direction.x) < 0.05f)
            return;

        Vector3 scale = eyeVisual.localScale;
        scale.x = direction.x > 0f
            ? -Mathf.Abs(scale.x)
            : Mathf.Abs(scale.x);
        eyeVisual.localScale = scale;
    }

    private void UpdateVisibilityEffect()
    {
        if (worldRuleVisual == null || player == null)
            return;

        float distance = Vector2.Distance(
            body.position,
            (Vector2)player.position
        );
        float effectStrength = Mathf.InverseLerp(
            EffectStartDistance,
            EffectMaxDistance,
            distance
        );
        float radiusMultiplier = 1f - effectStrength * MaxRadiusReduction;
        worldRuleVisual.SetPlayerLightRadiusMultiplier(
            this,
            radiusMultiplier
        );
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
    }

    public override void StopAfterHit()
    {
    }

    private void OnDisable()
    {
        worldRuleVisual?.RemovePlayerLightRadiusMultiplier(this);
        ClearAnomalyExternalVelocities();

        if (body != null)
            body.linearVelocity = Vector2.zero;
    }
}
