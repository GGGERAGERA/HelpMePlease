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
    private enum VolleyPhase { Ready, Preparing, Firing, Recovering }
    private bool committedVolleyExperiment;
    private VolleyPhase volleyPhase;
    private float volleyTimer;
    private int volleyShots;
    private Vector2 volleyDirection;
    private Quaternion volleyVisualRotation;
    private Vector3 volleyVisualScale;
    private bool volleyVisualLocked;
    private bool strafingExperiment;
    private float strafingDirection;
    private enum RecoilPhase { Ready, Retreating, Recovering }
    private bool recoilingExperiment;
    private RecoilPhase recoilPhase;
    private float recoilTimer;
    private Vector2 recoilDirection;
    private Quaternion recoilVisualRotation;
    private bool recoilVisualLocked;

    public bool RecoilingExperiment => recoilingExperiment;
    // Prefab eligibility deliberately does not depend on Awake/runtime readiness.
    public bool HasRecoilingVisual => visualRoot != null && visualRoot != transform &&
        visualRoot.IsChildOf(transform) &&
        visualRoot.GetComponentInChildren<SpriteRenderer>(true) != null &&
        visualRoot.GetComponentInChildren<Collider2D>(true) == null &&
        visualRoot.GetComponentInChildren<Rigidbody2D>(true) == null;
    public bool CanUseRecoiling => isActiveAndEnabled && rb != null &&
        projectilePrefab != null && HasRecoilingVisual &&
        !committedVolleyExperiment && !strafingExperiment &&
        GetComponent<CarrierEscapeBehaviour>() == null &&
        GetComponent<TurretEnemyBehaviour>() == null &&
        !(GetComponent<EnemyChaseMovement>() is { HasAssaultDestination: true }) &&
        GetComponent<EnemyHealth>() is { IsBoss: false, IsDead: false };

    public void ConfigureRecoilingExperiment(bool enabled)
    {
        if (enabled && (!CanUseRecoiling || recoilingExperiment)) return;
        RestoreRecoilVisual();
        recoilingExperiment = false;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        recoilingExperiment = enabled;
#endif
        recoilPhase = RecoilPhase.Ready;
        recoilTimer = 0f;
        recoilDirection = Vector2.zero;
    }

    private void RestoreRecoilVisual()
    {
        if (recoilVisualLocked && visualRoot != null)
            visualRoot.localRotation = recoilVisualRotation;
        recoilVisualLocked = false;
    }

    private void BeginRecoil()
    {
        if (!recoilingExperiment || recoilPhase != RecoilPhase.Ready) return;
        Vector2 away = rb.position - (Vector2)player.position;
        float maximumDistance = preferredDistance + distanceTolerance;
        if (maximumDistance < 0f || away.sqrMagnitude > maximumDistance * maximumDistance ||
            away.sqrMagnitude < 0.0001f) return;
        recoilDirection = away.normalized;
        recoilPhase = RecoilPhase.Retreating;
        recoilTimer = 0.65f;
        recoilVisualRotation = visualRoot.localRotation;
        recoilVisualLocked = true;
        float tilt = recoilDirection.x < 0f ? 15f : -15f;
        visualRoot.localRotation = recoilVisualRotation * Quaternion.Euler(0f, 0f, tilt);
    }

    private void UpdateRecoil()
    {
        if (recoilPhase == RecoilPhase.Ready) return;
        recoilTimer -= Time.deltaTime;
        if (recoilTimer > 0f) return;
        if (recoilPhase == RecoilPhase.Retreating)
        {
            RestoreRecoilVisual();
            recoilPhase = RecoilPhase.Recovering;
            recoilTimer = 0.65f;
        }
        else
        {
            recoilPhase = RecoilPhase.Ready;
            recoilTimer = 0f;
        }
    }

    public bool StrafingExperiment => strafingExperiment;
    public bool CanUseStrafing => projectilePrefab != null && !committedVolleyExperiment && !recoilingExperiment &&
        GetComponent<CarrierEscapeBehaviour>() == null;

    public void ConfigureStrafingExperiment(bool enabled, bool clockwise)
    {
        if (enabled && !CanUseStrafing) return;
        strafingExperiment = false;
        strafingDirection = 0f;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        strafingExperiment = enabled && CanUseStrafing;
        if (strafingExperiment) strafingDirection = clockwise ? 1f : -1f;
#endif
    }

    public bool CommittedVolleyExperiment => committedVolleyExperiment;
    public bool CanUseCommittedVolley => visualRoot != null &&
        visualRoot != transform && visualRoot.IsChildOf(transform) &&
        visualRoot.GetComponentInChildren<SpriteRenderer>(true) != null &&
        visualRoot.GetComponentInChildren<Collider2D>(true) == null &&
        visualRoot.GetComponentInChildren<Rigidbody2D>(true) == null &&
        projectilePrefab != null && !strafingExperiment && !recoilingExperiment;

    public void ConfigureCommittedVolleyExperiment(bool enabled)
    {
        if (enabled && !CanUseCommittedVolley) return;
        bool resetFireTimer = committedVolleyExperiment || enabled;
        RestoreVolleyVisual();
        committedVolleyExperiment = false;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        committedVolleyExperiment = enabled && CanUseCommittedVolley;
#endif
        volleyPhase = VolleyPhase.Ready;
        volleyTimer = 0f;
        volleyShots = 0;
        volleyDirection = Vector2.zero;
        if (resetFireTimer) fireTimer = fireInterval;
    }

    private void RestoreVolleyVisual()
    {
        if (!volleyVisualLocked) return;
        if (visualRoot != null)
        {
            visualRoot.localRotation = volleyVisualRotation;
            visualRoot.localScale = volleyVisualScale;
        }
        volleyVisualLocked = false;
    }

    private void BeginVolley()
    {
        Vector2 origin = firePoint != null ? (Vector2)firePoint.position : rb.position;
        volleyDirection = ((Vector2)player.position - origin).normalized;
        if (volleyDirection.sqrMagnitude < 0.01f) volleyDirection = Vector2.left;
        volleyVisualRotation = visualRoot.localRotation;
        volleyVisualScale = visualRoot.localScale;
        volleyVisualLocked = true;
        // The existing shooter artwork faces left at positive X scale.
        visualRoot.localScale = new Vector3(Mathf.Abs(volleyVisualScale.x),
            volleyVisualScale.y, volleyVisualScale.z);
        visualRoot.rotation = Quaternion.Euler(0f, 0f,
            Mathf.Atan2(volleyDirection.y, volleyDirection.x) * Mathf.Rad2Deg - 180f);
        volleyPhase = VolleyPhase.Preparing;
        volleyTimer = 0.6f;
        volleyShots = 0;
    }

    private void UpdateVolley()
    {
        if (volleyPhase == VolleyPhase.Ready)
        {
            fireTimer -= Time.deltaTime;
            if (fireTimer <= 0f) BeginVolley();
            return;
        }
        volleyTimer -= Time.deltaTime;
        if (volleyTimer > 0f) return;
        if (volleyPhase == VolleyPhase.Recovering)
        {
            BeginVolley();
            return;
        }
        ShootInDirection(volleyDirection);
        if (++volleyShots < 3)
        {
            volleyPhase = VolleyPhase.Firing;
            // At most one shot per frame: stalls must not collapse the volley.
            volleyTimer = 0.15f;
        }
        else
        {
            RestoreVolleyVisual();
            volleyPhase = VolleyPhase.Recovering;
            volleyTimer = 1.8f;
        }
    }

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
        if (recoilingExperiment && !CanUseRecoiling)
            ConfigureRecoilingExperiment(false);
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
        if (recoilingExperiment && !CanUseRecoiling)
            ConfigureRecoilingExperiment(false);
        if (Time.timeScale == 0f || EnemyDebugAiFreeze.IsFrozen ||
            player == null)
            return;

        // Carrier conversion may happen after an ordinary spawn was assigned.
        if (committedVolleyExperiment &&
            (!CanUseCommittedVolley || GetComponent<CarrierEscapeBehaviour>() != null))
            ConfigureCommittedVolleyExperiment(false);
        if (strafingExperiment && !CanUseStrafing)
            ConfigureStrafingExperiment(false, false);
        if (recoilingExperiment) UpdateRecoil();
        if (committedVolleyExperiment)
        {
            UpdateVolley();
            return;
        }

        fireTimer -= Time.deltaTime;

        if (fireTimer <= 0f)
        {
            if (Shoot()) BeginRecoil();
            fireTimer = fireInterval;
        }
    }

    private void FindPlayer()
    {
        player = PlayerRuntimeReference.ResolvePlayerTransform(
            playerTag,
            true);
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
        else if (strafingExperiment && sqrDistance > 0.0001f)
            moveDirection = new Vector2(-directionToPlayer.y, directionToPlayer.x) * strafingDirection;

        if (recoilingExperiment && recoilPhase == RecoilPhase.Retreating)
            moveDirection = recoilDirection;

        moveDirection = ApplyCrowdSteering(moveDirection,
            player.position, Time.fixedDeltaTime);

        if (recoilingExperiment && recoilPhase == RecoilPhase.Recovering)
            moveDirection = Vector2.zero;

        if (committedVolleyExperiment &&
            (volleyPhase == VolleyPhase.Preparing || volleyPhase == VolleyPhase.Firing))
            moveDirection = Vector2.zero;

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
        if (flipVisual && !volleyVisualLocked)
            UpdateVisualFacing(visualRoot, directionToPlayer);
    }

    private bool Shoot()
    {
        if (projectilePrefab == null) return false;
        return ShootInDirection(((Vector2)player.position - (Vector2)firePoint.position).normalized);
    }

    private bool ShootInDirection(Vector2 direction)
    {
        if (projectilePrefab == null)
            return false;

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
        {
            enemyProjectile.Initialize(direction);
            return true;
        }
        else if (pooled == null || !pooled.Release())
            Destroy(projectile);
        return false;
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
        ConfigureRecoilingExperiment(false);
        ConfigureCommittedVolleyExperiment(false);
        ConfigureStrafingExperiment(false, false);
        ReleaseCrowdSteering();
        ClearAnomalyExternalVelocities();
    }

}
