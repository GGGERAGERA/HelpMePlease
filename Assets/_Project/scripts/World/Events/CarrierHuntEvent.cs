using UnityEngine;

[RequireComponent(typeof(CircleCollider2D))]
public sealed class CarrierHuntEvent : WorldEvent
{
    private const float StandardCarrierSpeedMultiplier = 0.9f;
    private const float RiskCarrierSpeedMultiplier = 1.3f;
    private const float BaseEscapeSpeed = 2f;
    private const int StandardEscortCount = 3;
    private const int RiskEscortCount = 5;

    [Header("Start")]
    [SerializeField, Min(0.1f)] private float startRadius = 2.5f;
    [SerializeField] private Material lineMaterial;

    [Header("Scene")]
    [SerializeField] private GameplayAreaService gameplayArea;
    [SerializeField] private EnemySpawner enemySpawner;

    private CircleCollider2D startCollider;
    private LineRenderer startVisual;
    private EnemyHealth carrier;
    private CarrierEscapeBehaviour escapeBehaviour;
    private CarrierTargetMarker carrierMarker;
    private WorldEventMarker carrierOffscreenIndicator;
    private Vector3 rewardPosition;
    private bool hasRewardPosition;
    private bool riskMode;
    private bool waitingForCarrier;
    private bool despawnSubscribed;
    private bool resolved;

    public bool IsRiskMode => riskMode;
    public bool HasCarrier => carrier != null;
    public override Vector3 RewardPosition => hasRewardPosition
        ? rewardPosition
        : base.RewardPosition;

    private void Awake()
    {
        startCollider = GetComponent<CircleCollider2D>();
        BuildStartVisual();
    }

    public override void Initialize(WorldEventSpawner spawner)
    {
        base.Initialize(spawner);
        riskMode = false;
        waitingForCarrier = false;
        despawnSubscribed = false;
        resolved = false;
        hasRewardPosition = false;
        ShowEventMarker(transform, "CARRIER HUNT");
    }

    public override void ApplyDifficultyMultiplier(float multiplier)
    {
        riskMode = multiplier > 1f;
    }

    protected override bool CanStartFrom(Vector2 playerPosition)
    {
        return Vector2.Distance(transform.position, playerPosition) <=
            startRadius;
    }

    protected override void OnEventStarted()
    {
        if (startCollider != null)
            startCollider.enabled = false;

        if (startVisual != null)
            startVisual.enabled = false;

        HideEventMarker();
        ResolveSceneReferences();

        if (TryAssignCarrier())
            return;

        waitingForCarrier = true;
        EnemyHealth.SpawnConfigured += HandleEnemySpawnConfigured;
        enemySpawner?.SpawnAdditionalWave(
            transform.position,
            1,
            1.5f,
            3.5f,
            3f
        );
    }

    private bool TryAssignCarrier()
    {
        EnemyHealth preferred = null;
        EnemyHealth fallback = null;
        int preferredCount = 0;
        int fallbackCount = 0;

        foreach (EnemyHealth health in EnemyHealth.ActiveInstances)
        {
            if (!IsEligibleCarrier(health, out EnemyMovement movement))
                continue;

            fallbackCount++;

            if (Random.Range(0, fallbackCount) == 0)
                fallback = health;

            if (!(movement is EnemyChaseMovement))
                continue;

            preferredCount++;

            if (Random.Range(0, preferredCount) == 0)
                preferred = health;
        }

        EnemyHealth selected = preferred != null ? preferred : fallback;

        if (selected == null)
            return false;

        return AssignCarrier(selected);
    }

    private static bool IsEligibleCarrier(
        EnemyHealth health,
        out EnemyMovement movement)
    {
        movement = null;

        if (health == null || health.IsDead || health.IsBoss ||
            !health.isActiveAndEnabled ||
            !health.gameObject.activeInHierarchy ||
            health.GetComponent<CarrierEscapeBehaviour>() != null)
        {
            return false;
        }

        movement = health.GetComponent<EnemyMovement>();
        return movement != null && movement.isActiveAndEnabled;
    }

    private bool AssignCarrier(EnemyHealth selected)
    {
        EnemyMovement[] movements =
            selected.GetComponentsInChildren<EnemyMovement>(true);
        Rigidbody2D body = selected.GetComponent<Rigidbody2D>();

        if (movements.Length == 0 || body == null || gameplayArea == null)
            return false;

        bool[] movementEnabledStates = new bool[movements.Length];
        EnemyMovement primaryMovement = null;

        for (int i = 0; i < movements.Length; i++)
        {
            EnemyMovement movement = movements[i];
            movementEnabledStates[i] = movement != null && movement.enabled;

            if (primaryMovement == null && movementEnabledStates[i])
                primaryMovement = movement;
        }

        if (primaryMovement == null)
            return false;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        string movementLocation = primaryMovement.transform ==
            selected.transform
                ? "root"
                : $"child '{primaryMovement.transform.name}'";
        Debug.Log(
            $"[CarrierHunt] Selected carrier '{selected.name}': " +
            $"movement={primaryMovement.GetType().Name}, " +
            $"location={movementLocation}, enabledBefore=" +
            $"{primaryMovement.enabled}, movementCount={movements.Length}.",
            selected
        );
#endif

        // Disable every EnemyMovement writer before the escape behaviour is
        // created, so no FixedUpdate can continue the previous chase.
        for (int i = 0; i < movements.Length; i++)
        {
            if (movements[i] != null)
                movements[i].enabled = false;
        }

        body.linearVelocity = Vector2.zero;

        CarrierEscapeBehaviour newEscapeBehaviour =
            selected.gameObject.AddComponent<CarrierEscapeBehaviour>();
        GameObject playerObject =
            GameObject.FindGameObjectWithTag("Player");
        Vector2 playerPosition = playerObject != null
            ? playerObject.transform.position
            : selected.transform.position;
        float speedMultiplier = riskMode
            ? RiskCarrierSpeedMultiplier
            : StandardCarrierSpeedMultiplier;
        bool initialized = newEscapeBehaviour.Initialize(
            movements,
            movementEnabledStates,
            body,
            gameplayArea,
            playerPosition,
            BaseEscapeSpeed * speedMultiplier,
            riskMode,
            HandleCarrierEscaped
        );

        if (!initialized)
        {
            body.linearVelocity = Vector2.zero;

            for (int i = 0; i < movements.Length; i++)
            {
                if (movements[i] != null)
                    movements[i].enabled = movementEnabledStates[i];
            }

            Destroy(newEscapeBehaviour);
            return false;
        }

        StopWaitingForCarrier();
        carrier = selected;
        escapeBehaviour = newEscapeBehaviour;
        carrier.OnDied += HandleCarrierDied;
        SubscribeToDespawn();

        carrierMarker = BuildCarrierMarker(carrier.transform);
        carrierOffscreenIndicator =
            HUDManager.Instance?.CreateWorldEventMarker(
                carrier.transform,
                string.Empty
            );
        carrierOffscreenIndicator?.ConfigureAsCarrierIndicator();
        escapeBehaviour.SetIndicators(
            carrierMarker,
            carrierOffscreenIndicator
        );

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log(
            $"[CarrierHunt] Assignment ready: movementEnabledAfter=" +
            $"{primaryMovement.enabled}, escapeCreated=" +
            $"{escapeBehaviour != null}.",
            selected
        );
#endif

        enemySpawner?.SpawnAdditionalWave(
            carrier.transform.position,
            riskMode ? RiskEscortCount : StandardEscortCount,
            1.5f,
            3.5f,
            2f
        );
        return true;
    }

    private void HandleEnemySpawnConfigured(EnemyHealth spawnedEnemy)
    {
        if (!waitingForCarrier || resolved || spawnedEnemy == null)
            return;

        TryAssignCarrier();
    }

    private void HandleCarrierDied(EnemyHealth deadCarrier)
    {
        if (resolved || deadCarrier == null || deadCarrier != carrier)
            return;

        resolved = true;
        rewardPosition = deadCarrier.transform.position;
        hasRewardPosition = true;
        CompleteEvent();
    }

    private void HandleCarrierEscaped()
    {
        if (resolved || IsCompleted)
            return;

        resolved = true;
        FailEvent();
        Destroy(gameObject);
    }

    private void HandleEnemyDespawned(EnemyHealth despawnedEnemy)
    {
        if (resolved || despawnedEnemy == null ||
            despawnedEnemy != carrier)
        {
            return;
        }

        resolved = true;
        FailEvent();
        Destroy(gameObject);
    }

    protected override void CleanupEvent()
    {
        StopWaitingForCarrier();
        UnsubscribeFromDespawn();

        if (!ReferenceEquals(carrier, null))
            carrier.OnDied -= HandleCarrierDied;

        if (escapeBehaviour != null)
        {
            escapeBehaviour.StopAndRestore();
            Destroy(escapeBehaviour);
        }

        escapeBehaviour = null;

        if (carrierOffscreenIndicator != null)
        {
            HUDManager.Instance?.RemoveWorldEventMarker(
                carrierOffscreenIndicator
            );
        }

        carrierOffscreenIndicator = null;

        if (carrierMarker != null)
            Destroy(carrierMarker.gameObject);

        carrierMarker = null;
        carrier = null;
    }

    public override void CollectTacticalMapMarkers(
        System.Collections.Generic.List<TacticalMapMarkerDescriptor> markers)
    {
        base.CollectTacticalMapMarkers(markers);

        if (markers == null || carrier == null || IsCompleted)
            return;

        markers.Add(new TacticalMapMarkerDescriptor(
            TacticalMapMarkerKind.Target,
            carrier.transform.position
        ));
    }

    private void StopWaitingForCarrier()
    {
        if (!waitingForCarrier)
            return;

        waitingForCarrier = false;
        EnemyHealth.SpawnConfigured -= HandleEnemySpawnConfigured;
    }

    private void SubscribeToDespawn()
    {
        if (despawnSubscribed)
            return;

        despawnSubscribed = true;
        EnemyHealth.Despawned += HandleEnemyDespawned;
    }

    private void UnsubscribeFromDespawn()
    {
        if (!despawnSubscribed)
            return;

        despawnSubscribed = false;
        EnemyHealth.Despawned -= HandleEnemyDespawned;
    }

    private void ResolveSceneReferences()
    {
        if (gameplayArea == null)
            gameplayArea = GameplayAreaService.Instance;

        if (gameplayArea == null)
            gameplayArea = FindFirstObjectByType<GameplayAreaService>();

        if (enemySpawner == null)
            enemySpawner = FindFirstObjectByType<EnemySpawner>();
    }

    private CarrierTargetMarker BuildCarrierMarker(Transform target)
    {
        GameObject marker = new("Carrier Marker");
        marker.transform.SetParent(target, false);
        marker.transform.localPosition = new Vector3(0f, 1.45f, 0f);
        CarrierTargetMarker targetMarker =
            marker.AddComponent<CarrierTargetMarker>();
        targetMarker.Initialize(lineMaterial);
        return targetMarker;
    }

    private void BuildStartVisual()
    {
        if (lineMaterial == null)
            return;

        const int Segments = 40;
        startVisual = gameObject.AddComponent<LineRenderer>();
        startVisual.sharedMaterial = lineMaterial;
        startVisual.useWorldSpace = false;
        startVisual.loop = true;
        startVisual.positionCount = Segments;
        startVisual.startWidth = 0.1f;
        startVisual.endWidth = 0.1f;
        startVisual.startColor = Color.cyan;
        startVisual.endColor = Color.yellow;
        startVisual.sortingLayerName = "Midground";
        startVisual.sortingOrder = 1;

        for (int i = 0; i < Segments; i++)
        {
            float angle = i * Mathf.PI * 2f / Segments;
            startVisual.SetPosition(
                i,
                new Vector3(
                    Mathf.Cos(angle) * startRadius,
                    Mathf.Sin(angle) * startRadius,
                    0f
                )
            );
        }
    }

    private void OnDisable()
    {
        if (IsStarted && !IsCompleted)
        {
            resolved = true;
            FailEvent();
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, startRadius);
    }
}
