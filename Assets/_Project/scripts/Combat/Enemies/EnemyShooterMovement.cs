using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class EnemyShooterMovement : EnemyMovement
{
    [Header("Target")]
    [SerializeField] private string playerTag = "Player";

    [Header("Movement")]
    [SerializeField] private float moveSpeed = 2.2f;
    [SerializeField] private float preferredDistance = 6f;
    [SerializeField] private float distanceTolerance = 1f;

    [Header("Shooting")]
    [SerializeField] private GameObject projectilePrefab;
    [SerializeField] private Transform firePoint;
    [SerializeField] private float fireInterval = 1.8f;

    [Header("Visual")]
    [SerializeField] private Transform visualRoot;
    [SerializeField] private bool flipVisual = true;

    private Rigidbody2D rb;
    private Transform player;
    private float fireTimer;
    private float speedMultiplier = 1f;
    private float anomalySpeedMultiplier = 1f;
    private float worldRuleSpeedMultiplier = 1f;
    private Vector2 worldRuleExternalVelocity;
    private SimplePrefabPool projectilePool;

    public GameObject ProjectilePrefab => projectilePrefab;

    public void SetProjectilePool(SimplePrefabPool pool)
    {
        projectilePool = pool;
    }

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        InitializeCrowdSteering();
    }

    private void Start()
    {
        FindPlayer();

        if (firePoint == null)
            firePoint = transform;
    }

    private void FixedUpdate()
    {
        if (Time.timeScale == 0f)
            return;

        if (EnemyDebugAiFreeze.IsFrozen)
        {
            rb.MovePosition(
                rb.position +
                (worldRuleExternalVelocity + AnomalyExternalVelocity) *
                Time.fixedDeltaTime
            );
            return;
        }

        if (player == null)
            FindPlayer();

        if (player == null)
            return;

        Move();
    }

    private void Update()
    {
        if (Time.timeScale == 0f || EnemyDebugAiFreeze.IsFrozen ||
            player == null)
            return;

        fireTimer -= Time.deltaTime;

        if (fireTimer <= 0f)
        {
            Shoot();
            fireTimer = fireInterval;
        }
    }

    private void FindPlayer()
    {
        GameObject playerObject = GameObject.FindGameObjectWithTag(playerTag);
        if (playerObject != null)
            player = playerObject.transform;
    }

    private void Move()
    {
        Vector2 offset = (Vector2)player.position - rb.position;
        float sqrDistance = offset.sqrMagnitude;
        Vector2 directionToPlayer = offset.normalized;

        Vector2 moveDirection = Vector2.zero;
        float maximumDistance = preferredDistance + distanceTolerance;
        float minimumDistance = preferredDistance - distanceTolerance;

        if (maximumDistance < 0f ||
            sqrDistance > maximumDistance * maximumDistance)
            moveDirection = directionToPlayer;
        else if (minimumDistance > 0f &&
                 sqrDistance < minimumDistance * minimumDistance)
            moveDirection = -directionToPlayer;

        moveDirection = ApplyCrowdSteering(moveDirection,
            player.position, Time.fixedDeltaTime);

        rb.MovePosition(
            rb.position +
            (moveDirection *
             moveSpeed *
             speedMultiplier *
             anomalySpeedMultiplier *
             worldRuleSpeedMultiplier +
             worldRuleExternalVelocity +
             AnomalyExternalVelocity) *
            Time.fixedDeltaTime
        );
        UpdateVisual(directionToPlayer);
    }

    private void Shoot()
    {
        if (projectilePrefab == null)
            return;

        Vector2 direction = ((Vector2)player.position - (Vector2)firePoint.position).normalized;

        PooledGameObject pooled = projectilePool?.Get(
            firePoint.position,
            Quaternion.identity);
        GameObject projectile = pooled != null
            ? pooled.gameObject
            : Instantiate(
                projectilePrefab,
                firePoint.position,
                Quaternion.identity);
        EnemyProjectile enemyProjectile = pooled != null
            ? pooled.EnemyProjectile
            : projectile.GetComponent<EnemyProjectile>();

        if (enemyProjectile != null)
            enemyProjectile.Initialize(direction);
        else if (pooled == null || !pooled.Release())
            Destroy(projectile);
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
        // Пока без knockback.
    }

    public override void StopAfterHit()
    {
        // Стрелок не контактный враг.
    }

    private void OnDisable()
    {
        ReleaseCrowdSteering();
        ClearAnomalyExternalVelocities();
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
}
