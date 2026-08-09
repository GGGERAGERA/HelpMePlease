using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
public sealed class GravityAnomalySiteController : MonoBehaviour
{
    private enum SiteState
    {
        Stopped,
        Dormant,
        Active,
        Collapsing,
        Completed
    }

    private const float SiteRadius = 11f;
    private const float HoldSeconds = 10f;
    private const float CollapseSeconds = 0.75f;
    private const int EventOneEnemyTarget = 28;
    private const float OrbitForceEnemies = 7f;
    private const float OrbitForcePlayer = 3.5f;
    private const float OrbitForceProjectiles = 2.5f;
    private const float InwardForceEnemies = 1f;
    private const float InwardForcePlayer = 0.7f;
    private const float InwardForceProjectiles = 0.35f;
    private const float OrbitPreviewDegreesPerSecond = 24f;

    private readonly HashSet<EnemyHealth> trialEnemies = new();
    private Vector3 sitePosition = Vector3.zero;
    private readonly Vector3 playerStartPosition = new(-8f, 0f, 0f);
    private readonly Vector3 eventOffset = new(-5f, -3f, 0f);

    private EnemySpawner enemySpawner;
    private WorldEventSpawner eventSpawner;
    private LevelAnomalyController anomalyController;
    private AnomalyPowerDebugController powerController;
    private PowerTestController powerTest;
    private LocalAnomalyData gravityData;
    private CaptureZoneEvent capturePrefab;
    private GameObject[] enemyPrefabs;
    private Transform player;
    private PlayerHealth playerHealth;
    private float originalPlayerDamageMultiplier = 1f;
    private bool playerDamageMultiplierCaptured;
    private bool playerInvulnerabilityRequested;
    private CaptureZoneEvent activeCapture;
    private WorldEvent activeSiteEvent;
    private LocalAnomalyZone activeGravityZone;
    private LineRenderer outerRing;
    private LineRenderer innerRing;
    private Transform orbitPreviewRoot;
    private SiteState state = SiteState.Stopped;
    private float collapseTimer;
    private float acquisitionMessageUntil;
    private string transientMessage;
    private bool resetPlayerWhenAvailable;
    private bool explorationMode;
    private bool showStandaloneHud = true;

    public bool IsOrbitalGravityActive => activeGravityZone != null &&
        (state == SiteState.Active || state == SiteState.Collapsing);
    public GravityZone ActiveOrbitZone => activeGravityZone as GravityZone;
    public bool IsCompleted => state == SiteState.Completed;
    public Vector3 SitePosition => sitePosition;

    public void Configure(
        EnemySpawner spawner,
        WorldEventSpawner worldEvents,
        LevelAnomalyController anomalies,
        AnomalyPowerDebugController powers,
        PowerTestController test,
        LocalAnomalyData gravity,
        CaptureZoneEvent capture,
        GameObject[] trialEnemyPrefabs)
    {
        enemySpawner = spawner;
        eventSpawner = worldEvents;
        anomalyController = anomalies;
        powerController = powers;
        powerTest = test;
        gravityData = gravity;
        capturePrefab = capture;
        enemyPrefabs = trialEnemyPrefabs ?? System.Array.Empty<GameObject>();

        if (eventSpawner != null)
        {
            eventSpawner.SetHoldPointEnabled(true);
            eventSpawner.EventCompleted += HandleEventCompleted;
            eventSpawner.EventFailed += HandleEventFailed;
        }

        BuildPreview();
        StopSite();
    }

    public void ConfigureExploration(Vector2 position)
    {
        sitePosition = position;
        explorationMode = true;
        showStandaloneHud = false;
        resetPlayerWhenAvailable = false;
    }

    private void Update()
    {
        ResolvePlayer();
        RemoveDeadTrialEnemies();

        if (state == SiteState.Collapsing)
        {
            collapseTimer -= Time.deltaTime;
            if (collapseTimer <= 0f)
                CompleteCollapse();
        }

        UpdatePreview();
    }

    public void StartOrResetSite()
    {
        powerTest?.StopTest();
        ClearActiveEvent();
        DespawnGravityZone();
        ClearTrialEnemies();
        powerController?.BeginGravitySiteRewardLock();
        state = SiteState.Active;
        activeSiteEvent = null;
        activeCapture = null;
        if (!explorationMode)
        {
            EnablePlayerInvulnerability();
            ResetPlayerPosition();
        }
        SpawnGravityZone();
        CreateHoldEvent();
        acquisitionMessageUntil = 0f;
        transientMessage = string.Empty;
        SetPreviewVisible(true);
    }

    public void StopSite()
    {
        ClearActiveEvent();
        DespawnGravityZone();
        ClearTrialEnemies();
        powerController?.ClearGravitySiteReward();
        RestorePlayerDamage();
        resetPlayerWhenAvailable = false;
        state = SiteState.Stopped;
        SetPreviewVisible(false);
    }

    private void CreateHoldEvent()
    {
        if (gravityData == null || gravityData.ZonePrefab == null ||
            capturePrefab == null || eventSpawner == null)
        {
            Debug.LogWarning(
                "[GravitySite] Gravity data or CaptureZone prefab is missing.",
                this
            );
            return;
        }

        Vector3 eventPosition = sitePosition + eventOffset;
        bool spawnedSuccessfully = explorationMode
            ? eventSpawner.SpawnConcurrentDebugEventAt(
                capturePrefab,
                eventPosition,
                true,
                out WorldEvent spawnedEvent)
            : eventSpawner.SpawnDebugEventAt(
                capturePrefab,
                eventPosition,
                true,
                out spawnedEvent);
        if (!spawnedSuccessfully)
        {
            Debug.LogWarning(
                "[GravitySite] CaptureZoneEvent could not be spawned.",
                this
            );
            return;
        }

        activeCapture = spawnedEvent as CaptureZoneEvent;
        if (activeCapture == null)
        {
            ClearActiveEvent();
            return;
        }

        activeCapture.ConfigureDebugHoldTime(HoldSeconds);
        activeSiteEvent = activeCapture;
        if (!explorationMode)
            EnsureTrialPopulation(EventOneEnemyTarget);
        state = SiteState.Active;
    }

    private void SpawnGravityZone()
    {
        activeGravityZone = Instantiate(
            gravityData.ZonePrefab,
            sitePosition,
            Quaternion.identity
        );
        activeGravityZone.name = "Gravity Anomaly Site - Orbit Zone";
        activeGravityZone.Initialize(
            gravityData,
            anomalyController,
            Vector2.one * SiteRadius * 2f
        );

        if (activeGravityZone is GravityZone gravityZone)
        {
            gravityZone.ConfigureDebugOrbit(
                OrbitForceEnemies,
                OrbitForcePlayer,
                OrbitForceProjectiles,
                InwardForceEnemies,
                InwardForcePlayer,
                InwardForceProjectiles
            );
        }
    }

    private void EnsureTrialPopulation(int targetAlive)
    {
        if (enemySpawner == null || enemyPrefabs.Length == 0)
            return;

        RemoveDeadTrialEnemies();
        int attempts = Mathf.Max(0, targetAlive - trialEnemies.Count) * 3;
        for (int i = 0; i < attempts &&
            trialEnemies.Count < targetAlive; i++)
        {
            GameObject prefab = enemyPrefabs[i % enemyPrefabs.Length];
            GameObject instance = enemySpawner.SpawnSpecificEnemyAround(
                prefab,
                sitePosition,
                1.5f,
                SiteRadius - 0.4f,
                0.75f,
                true,
                0.15f
            );
            EnemyHealth health = instance != null
                ? instance.GetComponent<EnemyHealth>()
                : null;
            if (health != null)
                trialEnemies.Add(health);
        }
    }

    private void HandleEventCompleted(WorldEvent worldEvent)
    {
        if (activeSiteEvent == null || worldEvent != activeSiteEvent)
            return;

        activeSiteEvent = null;
        activeCapture = null;
        state = SiteState.Collapsing;
        collapseTimer = CollapseSeconds;
    }

    private void HandleEventFailed(WorldEvent worldEvent)
    {
        if (activeSiteEvent == null || worldEvent != activeSiteEvent)
            return;

        activeSiteEvent = null;
        activeCapture = null;
        ClearTrialEnemies();
        powerController?.BeginGravitySiteRewardLock();
        state = SiteState.Active;

        if (activeGravityZone == null)
            SpawnGravityZone();
        CreateHoldEvent();
    }

    private void CompleteCollapse()
    {
        DespawnGravityZone();
        state = SiteState.Completed;
        powerController?.GrantGravityOrbFromSite();
        acquisitionMessageUntil = Time.unscaledTime + 3f;
        transientMessage = "GRAVITY ORB ACQUIRED";
        Debug.Log("GRAVITY ORB ACQUIRED");
    }

    private void ClearActiveEvent()
    {
        if (activeSiteEvent != null && eventSpawner != null)
            eventSpawner.ClearDebugEvent(activeSiteEvent);

        activeSiteEvent = null;
        activeCapture = null;
    }

    private void DespawnGravityZone()
    {
        if (activeGravityZone != null)
            activeGravityZone.Despawn();

        activeGravityZone = null;
    }

    private void ResolvePlayer()
    {
        if (player == null)
        {
            GameObject playerObject =
                GameObject.FindGameObjectWithTag("Player");
            if (playerObject == null)
                return;

            player = playerObject.transform;
            playerHealth = playerObject.GetComponent<PlayerHealth>();
        }

        if (resetPlayerWhenAvailable)
        {
            Rigidbody2D body = player.GetComponent<Rigidbody2D>();
            if (body != null)
                body.position = playerStartPosition;
            player.position = playerStartPosition;
            resetPlayerWhenAvailable = false;
        }

        if (playerInvulnerabilityRequested && playerHealth != null)
        {
            if (!playerDamageMultiplierCaptured)
            {
                originalPlayerDamageMultiplier =
                    playerHealth.IncomingDamageMultiplier;
                playerDamageMultiplierCaptured = true;
            }

            playerHealth.SetIncomingDamageMultiplier(0f);
        }
    }

    private void EnablePlayerInvulnerability()
    {
        playerInvulnerabilityRequested = true;
        ResolvePlayer();
    }

    private void ResetPlayerPosition()
    {
        resetPlayerWhenAvailable = true;
        ResolvePlayer();
    }

    private void RestorePlayerDamage()
    {
        playerInvulnerabilityRequested = false;

        if (playerHealth != null && playerDamageMultiplierCaptured)
        {
            playerHealth.SetIncomingDamageMultiplier(
                originalPlayerDamageMultiplier
            );
        }

        playerDamageMultiplierCaptured = false;
    }

    private void RemoveDeadTrialEnemies()
    {
        trialEnemies.RemoveWhere(enemy => enemy == null || enemy.IsDead);
    }

    private void ClearTrialEnemies()
    {
        foreach (EnemyHealth enemy in trialEnemies)
        {
            if (enemy != null)
                Destroy(enemy.gameObject);
        }

        trialEnemies.Clear();
    }

    private static EvacuationCorridorEvent FindCorridorPrefab(
        WorldEventSpawner spawner)
    {
        if (spawner == null)
            return null;

        System.Collections.Generic.IReadOnlyList<WorldEvent> prefabs =
            spawner.EventPrefabs;
        for (int i = 0; i < prefabs.Count; i++)
        {
            if (prefabs[i] is EvacuationCorridorEvent corridor)
                return corridor;
        }

        return null;
    }

    private void BuildPreview()
    {
        outerRing = CreateRing("Gravity Site Preview", SiteRadius, 72, 0.08f);
        innerRing = CreateRing("Gravity Site Center", 0.75f, 32, 0.07f);
        CreateOrbitPreview();
    }

    private void CreateOrbitPreview()
    {
        GameObject root = new("Gravity Orbit Direction Preview");
        root.transform.SetParent(transform, false);
        root.transform.position = sitePosition;
        orbitPreviewRoot = root.transform;

        const int guideCount = 6;
        const int pointsPerGuide = 6;
        const float guideRadius = SiteRadius * 0.7f;
        const float guideArcDegrees = 20f;

        for (int guideIndex = 0; guideIndex < guideCount; guideIndex++)
        {
            GameObject guideObject = new($"Orbit Guide {guideIndex + 1}");
            guideObject.transform.SetParent(orbitPreviewRoot, false);
            LineRenderer guide = guideObject.AddComponent<LineRenderer>();
            guide.useWorldSpace = false;
            guide.positionCount = pointsPerGuide;
            guide.widthMultiplier = 0.11f;
            guide.sharedMaterial = new Material(Shader.Find("Sprites/Default"));
            guide.sortingLayerName = "Foreground";
            guide.sortingOrder = 21;

            float baseDegrees = guideIndex * 360f / guideCount;
            for (int pointIndex = 0; pointIndex < pointsPerGuide; pointIndex++)
            {
                float fraction = pointIndex / (pointsPerGuide - 1f);
                float degrees = baseDegrees + guideArcDegrees * fraction;
                float radians = degrees * Mathf.Deg2Rad;
                guide.SetPosition(pointIndex, new Vector3(
                    Mathf.Cos(radians) * guideRadius,
                    Mathf.Sin(radians) * guideRadius,
                    0f
                ));
            }
        }
    }

    private LineRenderer CreateRing(
        string objectName,
        float radius,
        int segments,
        float width)
    {
        GameObject ringObject = new(objectName);
        ringObject.transform.SetParent(transform, false);
        ringObject.transform.position = sitePosition;
        LineRenderer ring = ringObject.AddComponent<LineRenderer>();
        ring.loop = true;
        ring.useWorldSpace = false;
        ring.positionCount = segments;
        ring.widthMultiplier = width;
        ring.sharedMaterial = new Material(Shader.Find("Sprites/Default"));
        ring.sortingLayerName = "Foreground";
        ring.sortingOrder = 20;

        for (int i = 0; i < segments; i++)
        {
            float angle = i * Mathf.PI * 2f / segments;
            ring.SetPosition(i, new Vector3(
                Mathf.Cos(angle) * radius,
                Mathf.Sin(angle) * radius,
                0f
            ));
        }

        return ring;
    }

    private void UpdatePreview()
    {
        if (outerRing == null || !outerRing.enabled)
            return;

        bool gravityVisible = state == SiteState.Dormant ||
            state == SiteState.Active || state == SiteState.Collapsing;
        if (orbitPreviewRoot != null)
        {
            orbitPreviewRoot.gameObject.SetActive(gravityVisible);
            if (gravityVisible)
            {
                float speed = state == SiteState.Collapsing
                    ? OrbitPreviewDegreesPerSecond * 0.35f
                    : OrbitPreviewDegreesPerSecond;
                orbitPreviewRoot.Rotate(
                    0f,
                    0f,
                    speed * Time.unscaledDeltaTime
                );
            }
        }

        float pulse = 0.55f + Mathf.Sin(Time.unscaledTime * 3f) * 0.2f;
        Color color = state switch
        {
            SiteState.Active => new Color(0.7f, 0.25f, 1f, 0.9f),
            SiteState.Collapsing => new Color(1f, 0.85f, 1f, pulse),
            SiteState.Completed => new Color(0.25f, 1f, 0.6f, 0.75f),
            _ => new Color(0.55f, 0.15f, 1f, pulse)
        };
        outerRing.startColor = outerRing.endColor = color;
        innerRing.startColor = innerRing.endColor = color;
    }

    private void SetPreviewVisible(bool visible)
    {
        if (outerRing != null)
            outerRing.enabled = visible;
        if (innerRing != null)
            innerRing.enabled = visible;
        if (orbitPreviewRoot != null)
            orbitPreviewRoot.gameObject.SetActive(visible);
    }

    private void OnGUI()
    {
        if (!showStandaloneHud || state == SiteState.Stopped)
            return;

        string progress = activeCapture != null
            ? $"{activeCapture.Progress * 100f:F0}% ({activeCapture.TimeRemaining:F1}s)"
            : "--";
        string gravityStatus = state switch
        {
            SiteState.Dormant => "ACTIVE",
            SiteState.Active => "ACTIVE",
            SiteState.Collapsing => "COLLAPSING",
            _ => "OFF"
        };
        string currentEvent = activeCapture != null
            ? "Hold Zone"
            : "None";
        string orbStatus = powerController != null &&
            powerController.GravityOrbEnabled
            ? "ACQUIRED"
            : "LOCKED";
        string status =
            "GRAVITY ANOMALY SITE\n" +
            $"Site: {state}\n" +
            $"Gravity: {gravityStatus}\n" +
            $"Player Invulnerable: {(playerInvulnerabilityRequested ? "YES" : "NO")}\n" +
            $"Current Event: {currentEvent}\n" +
            $"Gravity Orb: {orbStatus}\n" +
            $"Enemies Alive: {EnemyHealth.ActiveInstances.Count}\n" +
            $"Objective Progress: {progress}";
        GUI.Box(new Rect(14f, Screen.height - 210f, 330f, 195f), status);

        if (Time.unscaledTime < acquisitionMessageUntil)
        {
            GUI.Box(
                new Rect(Screen.width * 0.5f - 190f, 55f, 380f, 45f),
                transientMessage
            );
        }
    }

    private void OnDestroy()
    {
        if (eventSpawner != null)
        {
            eventSpawner.EventCompleted -= HandleEventCompleted;
            eventSpawner.EventFailed -= HandleEventFailed;
        }

        DespawnGravityZone();
        ClearTrialEnemies();
        RestorePlayerDamage();
    }
}
#endif
