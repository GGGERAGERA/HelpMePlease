using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
public sealed class ExplorationSectorController : MonoBehaviour
{
    private const int NormalSiteCount = 3;
    private const float NormalRadius = 5f;
    private const float SpecialRadius = 11f;
    private const float ExitRadius = 2.2f;

    [Header("Threat timing")]
    [SerializeField, Min(1f)] private float threatStepSeconds = 30f;
    [Header("Performance comparison")]
    [SerializeField, Range(0.5f, 1f)]
    private float explorationEnemyCapScale = 1f;

    private static readonly Vector2[] PlayerAnchors =
    {
        new(-30f, -17f), new(-30f, 17f),
        new(30f, -17f), new(30f, 17f)
    };

    private static readonly Vector2[] SpecialAnchors =
    {
        new(-20f, -9f), new(-20f, 9f),
        new(0f, -9f), new(0f, 9f),
        new(20f, -9f), new(20f, 9f)
    };

    private static readonly Vector2[] NormalAnchors =
    {
        new(-28f, -15f), new(-28f, 0f), new(-28f, 15f),
        new(-14f, -15f), new(-14f, 0f), new(-14f, 15f),
        new(0f, -15f), new(0f, 15f),
        new(14f, -15f), new(14f, 0f), new(14f, 15f),
        new(28f, -15f), new(28f, 0f), new(28f, 15f)
    };

    private static readonly Vector2[] ExitAnchors =
    {
        new(-31f, -18f), new(-31f, 0f), new(-31f, 18f),
        new(0f, -19f), new(0f, 19f),
        new(31f, -18f), new(31f, 0f), new(31f, 18f)
    };

    private GameplayAreaService gameplayArea;
    private EnemySpawner enemySpawner;
    private WorldEventSpawner eventSpawner;
    private AnomalyPowerDebugController powerController;
    private PowerTestController powerTest;
    private ExitMassTestController massTest;
    private AnomalySiteDebugSelector siteSelector;
    private SectorVisualDebugController sectorVisual;
    private GravityAnomalySiteController gravitySite;
    private NormalAnomalySiteController[] normalSites;
    private LocalAnomalyData[] normalAnomalies;
    private GameObject[] basicEnemyPrefabs;
    private GameObject turretEnemyPrefab;
    private GameObject eyesEnemyPrefab;
    private CharacterSpawner characterSpawner;
    private Transform player;
    private PlayerHealth playerHealth;
    private float originalIncomingDamageMultiplier = 1f;
    private bool incomingDamageMultiplierCaptured;
    private GameObject worldVisualRoot;
    private GameObject exitVisual;
    private Material lineMaterial;
    private readonly Vector2[] normalPositions =
        new Vector2[NormalSiteCount];
    private Vector2 playerSpawn;
    private Vector2 specialPosition;
    private Vector2 exitPosition;
    private float elapsed;
    private int threatLevel;
    private int upgradeBaseline;
    private bool running;
    private bool completed;
    private bool mapVisible;
    private bool hudVisible = true;
    private bool invulnerabilityEnabled = true;
    private GUIStyle hudTitleStyle;
    private GUIStyle hudBodyStyle;
    private GUIStyle hudMapLabelStyle;
    private GUIStyle hudThreatCellStyle;
    private GUIStyle hudThreatActiveCellStyle;
    private float smoothedFrameSeconds = 1f / 60f;
    private float displayedFps = 60f;
    private float displayedFrameMs = 16.7f;
    private float nextHudRefreshTime;
    private float lastAppliedCapScale = -1f;
    private string explorationHudContent = string.Empty;
    private string statusHudContent = string.Empty;

    public bool IsRunning => running;
    public bool IsCompleted => completed;
    public bool HudVisible => hudVisible;
    public bool MapVisible => mapVisible;
    public bool InvulnerabilityEnabled => invulnerabilityEnabled;
    public float EnemyCapScale => explorationEnemyCapScale;
    public int ThreatLevel => threatLevel;
    public float Elapsed => elapsed;
    public int EnemiesAlive => EnemyHealth.ActiveInstances.Count;
    public int CurrentEnemyCap => CurrentThreatCap();
    public int SitesCompleted => CompletedNormalSites() +
        (gravitySite != null && gravitySite.IsCompleted ? 1 : 0);

    public void Configure(
        GameplayAreaService area,
        EnemySpawner spawner,
        WorldEventSpawner events,
        AnomalyPowerDebugController powers,
        PowerTestController test,
        ExitMassTestController oldMassTest,
        AnomalySiteDebugSelector selector,
        GravityAnomalySiteController special,
        NormalAnomalySiteController[] normals,
        LocalAnomalyData[] anomalies,
        GameObject[] basicPrefabs,
        GameObject turretPrefab,
        GameObject eyesPrefab,
        SectorVisualDebugController visualController)
    {
        gameplayArea = area;
        enemySpawner = spawner;
        eventSpawner = events;
        powerController = powers;
        powerTest = test;
        massTest = oldMassTest;
        siteSelector = selector;
        gravitySite = special;
        normalSites = normals;
        normalAnomalies = anomalies;
        basicEnemyPrefabs = basicPrefabs ?? System.Array.Empty<GameObject>();
        turretEnemyPrefab = turretPrefab;
        eyesEnemyPrefab = eyesPrefab;
        sectorVisual = visualController;
    }

    public void PrepareAsDefaultSandboxMode(CharacterSpawner spawner)
    {
        if (characterSpawner != null)
            characterSpawner.CharacterSpawned -= StartExplorationTest;

        characterSpawner = spawner;
        SuppressStandaloneDebugModes();

        if (characterSpawner == null)
            return;

        characterSpawner.CharacterSpawned += StartExplorationTest;
        if (characterSpawner.SpawnedPlayer != null)
            StartExplorationTest(characterSpawner.SpawnedPlayer);
    }

    public void StartExplorationTest(GameObject spawnedPlayer)
    {
        if (spawnedPlayer == null)
            return;

        player = spawnedPlayer.transform;
        playerHealth = spawnedPlayer.GetComponent<PlayerHealth>();
        spawnedPlayer.GetComponent<PlayerInteractor>()?
            .ConfigureDebugNonAllocScan(true);
        StartSector(true);
    }

    public void NewLayout() => StartSector(true);

    public void ResetSector() => StartSector(false);

    public void StopForStandaloneSiteTest()
    {
        running = false;
        completed = false;
        enemySpawner?.StopDebugExplorationPressure();
        enemySpawner?.ClearDebugSpawnedEnemies();
        eventSpawner?.ClearAllDebugEvents();
        ClearRewardChests();
        gravitySite?.StopSite();
        if (normalSites != null)
        {
            for (int i = 0; i < normalSites.Length; i++)
                normalSites[i]?.StopSite();
        }
        RestorePlayerIncomingDamage();
        powerController?.SetExplorationHudMode(false);
        siteSelector?.SetExplorationLocked(false);
        if (worldVisualRoot != null)
            worldVisualRoot.SetActive(false);
    }

    public void ReturnToExploration()
    {
        ResetThroughSceneReload();
    }

    public void SetInvulnerability(bool enabled)
    {
        invulnerabilityEnabled = enabled;
        if (enabled)
        {
            ResolvePlayer();
            EnsurePlayerInvulnerability();
        }
        else
        {
            RestorePlayerIncomingDamage();
        }
        RefreshHudCache();
    }

    public void SetHudVisible(bool visible) => hudVisible = visible;

    public void SetMapVisible(bool visible) => mapVisible = visible;

    public void SetEnemyCapScale(float scale)
    {
        explorationEnemyCapScale = Mathf.Clamp(scale, 0.5f, 1f);
        if (running)
            ApplyThreat(threatLevel);
        RefreshHudCache();
    }

    private void Update()
    {
        if (IsControlHeld() && Input.GetKeyDown(KeyCode.R))
        {
            ResetThroughSceneReload();
            return;
        }

        if ((running || completed) && Input.GetKeyDown(KeyCode.M))
            mapVisible = !mapVisible;

        if (!running)
            return;

        UpdatePerformanceSample();

        if (Input.GetKeyDown(KeyCode.LeftBracket))
            CycleEnemyCapScale(-1);
        if (Input.GetKeyDown(KeyCode.RightBracket))
            CycleEnemyCapScale(1);

        ResolvePlayer();
        if (player == null)
            return;

        if (invulnerabilityEnabled)
            EnsurePlayerInvulnerability();

        elapsed += Time.deltaTime;
        int nextThreat = Mathf.Clamp(
            Mathf.FloorToInt(elapsed / threatStepSeconds) + 1,
            1,
            4
        );
        if (nextThreat != threatLevel)
            ApplyThreat(nextThreat);

        float clampedCapScale = Mathf.Clamp(
            explorationEnemyCapScale,
            0.5f,
            1f
        );
        if (!Mathf.Approximately(clampedCapScale, lastAppliedCapScale))
            ApplyThreat(threatLevel);

        if (Time.unscaledTime >= nextHudRefreshTime)
        {
            nextHudRefreshTime = Time.unscaledTime + 0.25f;
            RefreshHudCache();
        }

        if (Vector2.Distance(player.position, exitPosition) <= ExitRadius)
            CompleteAtExit();
    }

    private static bool IsControlHeld()
    {
        return Input.GetKey(KeyCode.LeftControl) ||
            Input.GetKey(KeyCode.RightControl);
    }

    private void ResetThroughSceneReload()
    {
        Time.timeScale = 1f;
        if (RunStateManager.Instance != null)
            Destroy(RunStateManager.Instance.gameObject);
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    private void SuppressStandaloneDebugModes()
    {
        powerTest?.StopTest();
        massTest?.StopForOtherDebugTest();
        if (powerTest != null)
            powerTest.enabled = false;
        if (massTest != null)
            massTest.enabled = false;
        powerController?.SetExplorationHudMode(true);
        siteSelector?.SetExplorationLocked(true);
    }

    private void StartSector(bool generateNewLayout)
    {
        ResolvePlayer();
        if (player == null || gameplayArea == null ||
            enemySpawner == null || eventSpawner == null ||
            gravitySite == null || normalSites == null ||
            normalSites.Length < NormalSiteCount)
        {
            Debug.LogWarning(
                "[ExplorationSector] Required sandbox systems are missing."
            );
            return;
        }

        SuppressStandaloneDebugModes();
        eventSpawner.ConfigureDebugConcurrentEventCapacity(4);
        eventSpawner.ClearAllDebugEvents();
        ClearRewardChests();
        enemySpawner.ClearDebugSpawnedEnemies();
        gravitySite.StopSite();
        for (int i = 0; i < NormalSiteCount; i++)
            normalSites[i].StopSite();

        powerController?.ClearGravitySiteReward();
        powerController?.ClearElectricSiteReward();
        powerController?.ClearBeamSiteReward();

        if (generateNewLayout || !running && !completed)
            GenerateLayout();
        MovePlayer(playerSpawn);
        if (invulnerabilityEnabled)
            EnsurePlayerInvulnerability();
        BuildWorldVisuals();

        for (int i = 0; i < NormalSiteCount; i++)
        {
            LocalAnomalyData anomaly = normalAnomalies != null &&
                i < normalAnomalies.Length
                ? normalAnomalies[i]
                : null;
            normalSites[i].ConfigureExploration(
                normalPositions[i],
                anomaly,
                $"NORMAL SITE {i + 1}: " +
                (anomaly != null ? anomaly.AnomalyType.ToString() : "NONE")
            );
            normalSites[i].StartOrResetSite();
        }

        gravitySite.ConfigureExploration(specialPosition);
        gravitySite.StartOrResetSite();

        elapsed = 0f;
        threatLevel = 0;
        upgradeBaseline = GetTotalUpgradeCount();
        completed = false;
        running = true;
        mapVisible = false;
        smoothedFrameSeconds = 1f / 60f;
        nextHudRefreshTime = 0f;
        ApplyThreat(1);
        UpdatePerformanceSample();
        RefreshHudCache();
        enemySpawner.SpawnAdditionalWave(
            player.position,
            6,
            8f,
            15f,
            5f
        );
        Debug.Log("EXPLORATION SECTOR TEST STARTED");
    }

    private void ApplyThreat(int level)
    {
        threatLevel = Mathf.Clamp(level, 1, 4);
        float interval;
        int maxAlive;
        int batch;
        int basicWeight;
        int turretWeight;
        int eyesWeight;

        switch (threatLevel)
        {
            case 1:
                interval = 1.15f;
                maxAlive = 18;
                batch = 1;
                basicWeight = 8;
                turretWeight = 0;
                eyesWeight = 0;
                break;
            case 2:
                interval = 0.9f;
                maxAlive = 26;
                batch = 2;
                basicWeight = 6;
                turretWeight = 1;
                eyesWeight = 1;
                break;
            case 3:
                interval = 0.7f;
                maxAlive = 34;
                batch = 2;
                basicWeight = 4;
                turretWeight = 1;
                eyesWeight = 1;
                break;
            default:
                interval = 0.55f;
                maxAlive = 42;
                batch = 3;
                basicWeight = 3;
                turretWeight = 1;
                eyesWeight = 1;
                break;
        }

        maxAlive = ScaleEnemyCap(maxAlive);
        lastAppliedCapScale = Mathf.Clamp(
            explorationEnemyCapScale,
            0.5f,
            1f
        );
        enemySpawner.ConfigureDebugExplorationPressure(
            BuildComposition(basicWeight, turretWeight, eyesWeight),
            interval,
            maxAlive,
            batch
        );
        Debug.Log(
            $"EXPLORATION THREAT {ToRoman(threatLevel)}: " +
            $"interval {interval:0.00}, batch {batch}, cap {maxAlive} " +
            $"(scale {lastAppliedCapScale:0.00})"
        );
    }

    private GameObject[] BuildComposition(
        int basicWeight,
        int turretWeight,
        int eyesWeight)
    {
        List<GameObject> result = new();
        for (int repeat = 0; repeat < basicWeight; repeat++)
        {
            for (int i = 0; i < basicEnemyPrefabs.Length; i++)
            {
                if (basicEnemyPrefabs[i] != null)
                    result.Add(basicEnemyPrefabs[i]);
            }
        }
        for (int i = 0; i < turretWeight; i++)
        {
            if (turretEnemyPrefab != null)
                result.Add(turretEnemyPrefab);
        }
        for (int i = 0; i < eyesWeight; i++)
        {
            if (eyesEnemyPrefab != null)
                result.Add(eyesEnemyPrefab);
        }
        return result.ToArray();
    }

    private void GenerateLayout()
    {
        for (int attempt = 0; attempt < 100; attempt++)
        {
            playerSpawn = PlayerAnchors[Random.Range(0, PlayerAnchors.Length)];
            List<Vector2> specialCandidates =
                new(SpecialAnchors);
            Shuffle(specialCandidates);
            bool specialFound = false;
            for (int i = 0; i < specialCandidates.Count; i++)
            {
                Vector2 candidate = specialCandidates[i];
                if (Vector2.Distance(candidate, playerSpawn) < 20f ||
                    !IsInside(candidate, SpecialRadius + 1f))
                {
                    continue;
                }
                specialPosition = candidate;
                specialFound = true;
                break;
            }
            if (!specialFound)
                continue;

            List<Vector2> normalCandidates = new(NormalAnchors);
            Shuffle(normalCandidates);
            int normalCount = 0;
            for (int i = 0; i < normalCandidates.Count &&
                normalCount < NormalSiteCount; i++)
            {
                Vector2 candidate = normalCandidates[i];
                if (Vector2.Distance(candidate, playerSpawn) < 13f ||
                    Vector2.Distance(candidate, specialPosition) < 18f ||
                    !IsInside(candidate, NormalRadius + 1f) ||
                    OverlapsNormals(candidate, normalCount, 12f))
                {
                    continue;
                }
                normalPositions[normalCount++] = candidate;
            }
            if (normalCount < NormalSiteCount)
                continue;

            List<Vector2> exitCandidates = new(ExitAnchors);
            Shuffle(exitCandidates);
            for (int i = 0; i < exitCandidates.Count; i++)
            {
                Vector2 candidate = exitCandidates[i];
                if (Vector2.Distance(candidate, playerSpawn) < 28f ||
                    Vector2.Distance(candidate, specialPosition) < 17f ||
                    !IsInside(candidate, ExitRadius + 1f) ||
                    OverlapsNormals(candidate, NormalSiteCount, 10f))
                {
                    continue;
                }
                exitPosition = candidate;
                return;
            }
        }

        playerSpawn = new Vector2(-30f, -17f);
        specialPosition = new Vector2(10f, 7f);
        normalPositions[0] = new Vector2(-12f, 10f);
        normalPositions[1] = new Vector2(28f, -12f);
        normalPositions[2] = new Vector2(-10f, -12f);
        exitPosition = new Vector2(30f, 17f);
    }

    private bool IsInside(Vector2 position, float padding)
    {
        return gameplayArea != null &&
            gameplayArea.IsInsidePlayableArea(position, padding);
    }

    private bool OverlapsNormals(
        Vector2 candidate,
        int count,
        float requiredDistance)
    {
        for (int i = 0; i < count; i++)
        {
            if (Vector2.Distance(candidate, normalPositions[i]) <
                requiredDistance)
            {
                return true;
            }
        }
        return false;
    }

    private static void Shuffle(List<Vector2> values)
    {
        for (int i = values.Count - 1; i > 0; i--)
        {
            int swap = Random.Range(0, i + 1);
            (values[i], values[swap]) = (values[swap], values[i]);
        }
    }

    private void MovePlayer(Vector2 position)
    {
        Rigidbody2D body = player.GetComponent<Rigidbody2D>();
        if (body != null)
        {
            body.linearVelocity = Vector2.zero;
            body.position = position;
        }
        player.position = position;
    }

    private void CompleteAtExit()
    {
        if (!running)
            return;
        running = false;
        completed = true;
        enemySpawner.StopDebugExplorationPressure();
        RestorePlayerIncomingDamage();
        Debug.Log("SECTOR EXIT REACHED");
        Time.timeScale = 0f;
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
            playerObject.GetComponent<PlayerInteractor>()?
                .ConfigureDebugNonAllocScan(true);
        }
        else if (playerHealth == null)
        {
            playerHealth = player.GetComponent<PlayerHealth>();
        }
    }

    private void EnsurePlayerInvulnerability()
    {
        if (playerHealth == null)
            return;

        if (!incomingDamageMultiplierCaptured)
        {
            originalIncomingDamageMultiplier =
                playerHealth.IncomingDamageMultiplier;
            incomingDamageMultiplierCaptured = true;
        }

        if (playerHealth.IncomingDamageMultiplier != 0f)
            playerHealth.SetIncomingDamageMultiplier(0f);
    }

    private void RestorePlayerIncomingDamage()
    {
        if (playerHealth != null && incomingDamageMultiplierCaptured)
        {
            playerHealth.SetIncomingDamageMultiplier(
                originalIncomingDamageMultiplier
            );
        }

        incomingDamageMultiplierCaptured = false;
    }

    private int CompletedNormalSites()
    {
        int count = 0;
        if (normalSites == null)
            return count;
        for (int i = 0; i < NormalSiteCount; i++)
        {
            if (normalSites[i] != null && normalSites[i].IsCompleted)
                count++;
        }
        return count;
    }

    private int GetTotalUpgradeCount()
    {
        return RunStateManager.Instance != null
            ? RunStateManager.Instance.PickedUpgrades.Count
            : 0;
    }

    private int AcquiredUpgradeCount()
    {
        return Mathf.Max(0, GetTotalUpgradeCount() - upgradeBaseline);
    }

    private int CurrentKills()
    {
        return KillManager.Instance != null ? KillManager.Instance.Kills : 0;
    }

    private static string ToRoman(int level)
    {
        return level switch
        {
            1 => "I",
            2 => "II",
            3 => "III",
            _ => "IV"
        };
    }

    private void BuildWorldVisuals()
    {
        if (worldVisualRoot != null)
            Destroy(worldVisualRoot);
        worldVisualRoot = new GameObject("Exploration Sector Debug Visuals");
        Shader shader = Shader.Find("Sprites/Default");
        if (shader != null)
        {
            lineMaterial = new Material(shader)
            {
                name = "Exploration Sector Debug Material",
                hideFlags = HideFlags.HideAndDontSave
            };
        }

        BuildExitVisual();
    }

    private void BuildExitVisual()
    {
        exitVisual = new GameObject("Exploration Sector Exit");
        exitVisual.transform.SetParent(worldVisualRoot.transform, false);
        exitVisual.transform.position = exitPosition;
        CreateCircle(
            "Exit Ring Outer",
            exitPosition,
            ExitRadius,
            new Color(0.2f, 1f, 0.55f, 0.95f),
            0.12f
        );
        CreateCircle(
            "Exit Ring Inner",
            exitPosition,
            ExitRadius * 0.62f,
            new Color(0.75f, 1f, 0.9f, 0.8f),
            0.06f
        );
        CreateLine(
            "Exit Pillars",
            new[]
            {
                (Vector3)(exitPosition + new Vector2(-1.4f, -1.5f)),
                (Vector3)(exitPosition + new Vector2(-1.4f, 1.8f)),
                (Vector3)(exitPosition + new Vector2(1.4f, 1.8f)),
                (Vector3)(exitPosition + new Vector2(1.4f, -1.5f))
            },
            new Color(0.3f, 1f, 0.6f, 0.9f),
            0.14f,
            false
        );
    }

    private void CreateCircle(
        string name,
        Vector2 center,
        float radius,
        Color color,
        float width)
    {
        const int Points = 40;
        Vector3[] points = new Vector3[Points];
        for (int i = 0; i < Points; i++)
        {
            float angle = i * Mathf.PI * 2f / Points;
            points[i] = center + new Vector2(
                Mathf.Cos(angle),
                Mathf.Sin(angle)
            ) * radius;
        }
        CreateLine(name, points, color, width, true);
    }

    private void CreateLine(
        string name,
        Vector3[] points,
        Color color,
        float width,
        bool loop)
    {
        GameObject lineObject = new(name);
        lineObject.transform.SetParent(worldVisualRoot.transform, false);
        LineRenderer line = lineObject.AddComponent<LineRenderer>();
        line.useWorldSpace = true;
        line.loop = loop;
        line.positionCount = points.Length;
        line.SetPositions(points);
        line.startWidth = width;
        line.endWidth = width;
        line.startColor = color;
        line.endColor = color;
        line.sortingLayerName = "Background";
        line.sortingOrder = -20;
        if (lineMaterial != null)
            line.sharedMaterial = lineMaterial;
    }

    private static void ClearRewardChests()
    {
        WorldEventRewardChest[] chests =
            FindObjectsByType<WorldEventRewardChest>(FindObjectsSortMode.None);
        for (int i = 0; i < chests.Length; i++)
        {
            if (chests[i] != null)
                Destroy(chests[i].gameObject);
        }
    }

    private void OnGUI()
    {
        if (!running && !completed)
            return;

        float scale = GetHudScale();
        Matrix4x4 previousMatrix = GUI.matrix;
        GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
        EnsureHudStyles();

        float screenWidth = Screen.width / scale;
        if (hudVisible)
        {
            DrawExplorationPanel();
            DrawThreatPanel();
            DrawStatusPanel();
            DrawControlsPanel();
        }

        if (mapVisible)
            DrawDebugMap(screenWidth);

        if (completed)
            DrawResultPanel(screenWidth);

        GUI.matrix = previousMatrix;
    }

    private void DrawExplorationPanel()
    {
        if (string.IsNullOrEmpty(explorationHudContent))
            RefreshHudCache();
        DrawPanel(
            new Rect(14f, 14f, 350f, 270f),
            "EXPLORATION SECTOR TEST",
            explorationHudContent
        );
    }

    private void DrawThreatPanel()
    {
        Rect panel = new(14f, 294f, 350f, 82f);
        DrawPanel(panel, "THREAT", string.Empty);
        float x = panel.x + 18f;
        float y = panel.y + 40f;
        float width = 68f;
        for (int level = 1; level <= 4; level++)
        {
            bool active = level == threatLevel;
            Rect cell = new(x + (level - 1) * 78f, y, width, 28f);
            DrawFilledRect(
                cell,
                active
                    ? new Color(0.95f, 0.53f, 0.16f, 0.72f)
                    : new Color(0.07f, 0.12f, 0.15f, 0.92f)
            );
            DrawBorder(cell, active
                ? new Color(1f, 0.72f, 0.28f, 1f)
                : new Color(0.12f, 0.55f, 0.62f, 0.8f));
            GUI.Label(
                cell,
                ToRoman(level),
                active ? hudThreatActiveCellStyle : hudThreatCellStyle
            );
        }
    }

    private void DrawStatusPanel()
    {
        if (string.IsNullOrEmpty(statusHudContent))
            RefreshHudCache();
        DrawPanel(
            new Rect(14f, 386f, 350f, 260f),
            "STATUS",
            statusHudContent
        );
    }

    private void DrawControlsPanel()
    {
        const string content =
            "Ctrl+R   New Layout\n" +
            "M        Debug Map\n" +
            "E        Interact\n" +
            "T        Trajectory\n" +
            "Y        Prediction\n" +
            "[ / ]    Enemy Cap\n" +
            "F8       Kill All\n" +
            "F1       Weapon Menu";
        DrawPanel(new Rect(14f, 656f, 350f, 230f), "CONTROLS", content);
    }

    private void DrawDebugMap(float screenWidth)
    {
        Rect panel = new(screenWidth - 384f, 14f, 370f, 360f);
        DrawPanel(panel, "DEBUG MAP [M]", string.Empty);

        float legendY = panel.y + 40f;
        DrawLegendItem(panel.x + 18f, legendY, Color.white, "Player");
        DrawLegendItem(panel.x + 132f, legendY, Color.yellow, "Normal Site");
        DrawLegendItem(panel.x + 260f, legendY,
            new Color(0.75f, 0.35f, 1f), "Special");
        DrawLegendItem(panel.x + 18f, legendY + 24f, Color.green,
            "Completed Site");
        DrawLegendItem(panel.x + 178f, legendY + 24f, Color.cyan, "Exit");

        Rect map = new(
            panel.x + 18f,
            panel.y + 96f,
            panel.width - 36f,
            panel.height - 114f
        );
        DrawFilledRect(map, new Color(0.025f, 0.045f, 0.06f, 0.96f));
        DrawBorder(map, new Color(0.12f, 0.55f, 0.62f, 0.8f));
        DrawMapPoint(map, player != null ? (Vector2)player.position : playerSpawn,
            Color.white, "PLAYER");
        for (int i = 0; i < NormalSiteCount; i++)
        {
            bool done = normalSites[i] != null && normalSites[i].IsCompleted;
            DrawMapPoint(map, normalPositions[i],
                done ? Color.green : Color.yellow,
                $"N{i + 1} {(done ? "DONE" : "ACTIVE")}");
        }
        DrawMapPoint(map, specialPosition,
            gravitySite != null && gravitySite.IsCompleted
                ? Color.green
                : new Color(0.75f, 0.35f, 1f),
            gravitySite != null && gravitySite.IsCompleted
                ? "SPECIAL DONE"
                : "SPECIAL ACTIVE");
        DrawMapPoint(map, exitPosition, Color.cyan, "EXIT");
    }

    private void DrawLegendItem(
        float x,
        float y,
        Color color,
        string label)
    {
        Rect dot = new(x, y + 5f, 8f, 8f);
        DrawFilledRect(dot, color);
        GUI.Label(new Rect(x + 14f, y, 115f, 22f), label,
            hudMapLabelStyle);
    }

    private void DrawMapPoint(
        Rect map,
        Vector2 worldPosition,
        Color color,
        string label)
    {
        Bounds bounds = gameplayArea.PlayableArea.bounds;
        float x = Mathf.InverseLerp(bounds.min.x, bounds.max.x, worldPosition.x);
        float y = Mathf.InverseLerp(bounds.min.y, bounds.max.y, worldPosition.y);
        Rect dot = new(
            map.x + x * map.width - 4f,
            map.y + (1f - y) * map.height - 4f,
            8f,
            8f
        );
        Color previous = GUI.color;
        GUI.color = color;
        GUI.DrawTexture(dot, Texture2D.whiteTexture);
        GUI.color = previous;
        GUI.Label(new Rect(dot.x + 8f, dot.y - 5f, 120f, 22f), label,
            hudMapLabelStyle);
    }

    private void DrawResultPanel(float screenWidth)
    {
        string result =
            $"Time: {FormatElapsed(elapsed)}\n" +
            $"Threat: {ToRoman(threatLevel)}\n" +
            $"Normal Sites completed: {CompletedNormalSites()}/3\n" +
            $"Special Site completed: {(gravitySite != null && gravitySite.IsCompleted ? "YES" : "NO")}\n" +
            $"Upgrades acquired: {AcquiredUpgradeCount()}\n" +
            $"Anomaly Power acquired: {(powerController != null && powerController.GravityOrbEnabled ? "YES" : "NO")}\n" +
            $"Kills: {CurrentKills()}\n\n" +
            "Ctrl+R   NEW SECTOR";
        DrawPanel(
            new Rect(screenWidth * 0.5f - 220f, 30f, 440f, 250f),
            "SECTOR EXIT REACHED",
            result
        );
    }

    private void DrawPanel(Rect rect, string title, string content)
    {
        DrawFilledRect(rect, new Color(0.025f, 0.045f, 0.06f, 0.92f));
        DrawBorder(rect, new Color(0.08f, 0.72f, 0.78f, 0.9f));
        GUI.Label(
            new Rect(rect.x + 16f, rect.y + 10f, rect.width - 32f, 24f),
            title,
            hudTitleStyle
        );
        if (!string.IsNullOrEmpty(content))
        {
            GUI.Label(
                new Rect(
                    rect.x + 16f,
                    rect.y + 39f,
                    rect.width - 32f,
                    rect.height - 49f
                ),
                content,
                hudBodyStyle
            );
        }
    }

    private static void DrawFilledRect(Rect rect, Color color)
    {
        Color previous = GUI.color;
        GUI.color = color;
        GUI.DrawTexture(rect, Texture2D.whiteTexture);
        GUI.color = previous;
    }

    private static void DrawBorder(Rect rect, Color color)
    {
        DrawFilledRect(new Rect(rect.x, rect.y, rect.width, 1f), color);
        DrawFilledRect(new Rect(rect.x, rect.yMax - 1f, rect.width, 1f), color);
        DrawFilledRect(new Rect(rect.x, rect.y, 1f, rect.height), color);
        DrawFilledRect(new Rect(rect.xMax - 1f, rect.y, 1f, rect.height), color);
    }

    private void EnsureHudStyles()
    {
        if (hudTitleStyle != null)
            return;

        hudTitleStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 16,
            fontStyle = FontStyle.Bold,
            normal = { textColor = new Color(0.3f, 0.94f, 1f) }
        };
        hudBodyStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 15,
            richText = true,
            wordWrap = false,
            normal = { textColor = new Color(0.93f, 0.96f, 0.98f) }
        };
        hudMapLabelStyle = new GUIStyle(hudBodyStyle)
        {
            fontSize = 12
        };
        hudThreatCellStyle = new GUIStyle(hudBodyStyle)
        {
            alignment = TextAnchor.MiddleCenter
        };
        hudThreatActiveCellStyle = new GUIStyle(hudThreatCellStyle)
        {
            fontStyle = FontStyle.Bold
        };
    }

    private static float GetHudScale()
    {
        return Mathf.Clamp(
            Mathf.Min(Screen.width / 1280f, Screen.height / 900f),
            0.72f,
            1f
        );
    }

    private static string FormatElapsed(float seconds)
    {
        int totalSeconds = Mathf.Max(0, Mathf.FloorToInt(seconds));
        return $"{totalSeconds / 60:00}:{totalSeconds % 60:00}";
    }

    private int CurrentThreatCap()
    {
        int baseCap = threatLevel switch
        {
            1 => 18,
            2 => 26,
            3 => 34,
            _ => 42
        };
        return ScaleEnemyCap(baseCap);
    }

    private int ScaleEnemyCap(int baseCap)
    {
        return Mathf.Max(
            1,
            Mathf.RoundToInt(
                baseCap * Mathf.Clamp(
                    explorationEnemyCapScale,
                    0.5f,
                    1f
                )
            )
        );
    }

    private void CycleEnemyCapScale(int direction)
    {
        float[] values = { 0.5f, 0.75f, 1f };
        int currentIndex = 0;
        float closest = float.PositiveInfinity;
        for (int i = 0; i < values.Length; i++)
        {
            float difference = Mathf.Abs(
                explorationEnemyCapScale - values[i]
            );
            if (difference >= closest)
                continue;
            closest = difference;
            currentIndex = i;
        }

        currentIndex = Mathf.Clamp(
            currentIndex + direction,
            0,
            values.Length - 1
        );
        explorationEnemyCapScale = values[currentIndex];
        ApplyThreat(threatLevel);
        RefreshHudCache();
        Debug.Log(
            $"EXPLORATION ENEMY CAP SCALE: " +
            $"{explorationEnemyCapScale:0.00} " +
            $"(current cap {CurrentThreatCap()})"
        );
    }

    private void UpdatePerformanceSample()
    {
        float frameSeconds = Mathf.Max(0.0001f, Time.unscaledDeltaTime);
        float blend = 1f - Mathf.Exp(-5f * frameSeconds);
        smoothedFrameSeconds = Mathf.Lerp(
            smoothedFrameSeconds,
            frameSeconds,
            blend
        );

        if (Time.unscaledTime < nextHudRefreshTime)
            return;

        displayedFrameMs = smoothedFrameSeconds * 1000f;
        displayedFps = 1f / smoothedFrameSeconds;
    }

    private void RefreshHudCache()
    {
        string fpsColor = displayedFps >= 55f
            ? "#EEF5F8"
            : displayedFps >= 35f
                ? "#FFBE55"
                : "#FF6262";
        string invulnerability = running
            ? "<color=#71F5DD><b>INVULNERABLE</b></color>"
            : "<color=#AAB8C2>TEST COMPLETE</color>";
        explorationHudContent =
            $"<color=#FFBE55><b>Threat: {ToRoman(threatLevel)}</b></color>\n" +
            $"Elapsed: {FormatElapsed(elapsed)}\n" +
            $"Enemies: {EnemyHealth.ActiveInstances.Count} / {CurrentThreatCap()}\n" +
            $"Kills: {CurrentKills()}\n" +
            "\n" +
            $"Normal Sites: {CompletedNormalSites()} / 3\n" +
            $"Special Site: {(gravitySite != null && gravitySite.IsCompleted ? "COMPLETED" : "ACTIVE")}\n" +
            $"Power: {(powerController != null && powerController.GravityOrbEnabled ? "GRAVITY ORB" : "NONE")}\n\n" +
            invulnerability;

        string gravityStatus = powerController == null
            ? "LOCKED"
            : powerController.GravityOrbEnabled
                ? "ON"
                : powerController.GravityOrbSiteLocked
                    ? "LOCKED"
                    : "OFF";
        statusHudContent =
            $"Core: {WeaponCoreDebugSelector.ActiveCore.ToString().ToUpperInvariant()}\n" +
            $"Gravity Orb: {gravityStatus}\n" +
            $"Arc Node: {(powerController != null && powerController.ArcNodeEnabled ? "ON" : "OFF")}\n" +
            $"Red Beam: {(powerController != null && powerController.RedBeamEnabled ? "ON" : "OFF")}\n\n" +
            $"Gravity Trajectory: {(powerController != null && powerController.TrajectoryEnabled ? "ON" : "OFF")}\n" +
            $"Prediction: {(powerController != null ? powerController.TrajectoryPredictionTime : 1.5f):0.00} sec\n" +
            $"Targets: {(powerController != null ? powerController.TrajectoryTargetCount : 0)} / {(powerController != null ? powerController.TrajectoryMaxTargets : 5)}\n\n" +
            $"<color={fpsColor}>FPS: {displayedFps:0}\n" +
            $"Frame: {displayedFrameMs:0.0} ms</color>";
    }

    private void OnDestroy()
    {
        if (characterSpawner != null)
            characterSpawner.CharacterSpawned -= StartExplorationTest;
        RestorePlayerIncomingDamage();
        powerController?.SetExplorationHudMode(false);
        if (lineMaterial != null)
            Destroy(lineMaterial);
    }

    private void OnDisable()
    {
        RestorePlayerIncomingDamage();
        powerController?.SetExplorationHudMode(false);
    }

    private void OnEnable()
    {
        if (!running)
            return;

        powerController?.SetExplorationHudMode(true);
        ResolvePlayer();
        if (invulnerabilityEnabled)
            EnsurePlayerInvulnerability();
    }
}
#endif
