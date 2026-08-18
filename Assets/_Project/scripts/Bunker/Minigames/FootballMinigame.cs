using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

public sealed class FootballMinigame : BunkerMinigame
{
    private const string BestScoreKey = "BunkerFootballBestScore";
    [Header("Arena geometry")]
    [SerializeField] private FootballArenaLayout arenaLayout;
    [FormerlySerializedAs("playAreaBounds")]
    [SerializeField] private BoxCollider2D arenaBounds;
    [SerializeField] private BoxCollider2D ballSpawnZone;
    [SerializeField] private BoxCollider2D anomalySpawnZone;
    [SerializeField] private BoxCollider2D targetSpawnZone;
    [SerializeField] private bool showDebugZones;
    [SerializeField] private bool showLaneDebug;
    [SerializeField] private FootballPlayerBoundary playerBoundary;
    [SerializeField] private FootballStartZone startZone;
    [SerializeField] private FootballMinigameHUD hud;

    [Header("Camera framing")]
    [SerializeField] private CameraFollow cameraFollow;
    [SerializeField, Min(0f)] private float cameraPadding = 1f;

    [Header("Runtime roots")]
    [SerializeField] private Transform ballsRuntime;
    [SerializeField] private Transform anomaliesRuntime;
    [SerializeField] private Transform targetsRuntime;

    [Header("Balls")]
    [SerializeField] private BallRollVisual ballPrefab;
    [SerializeField] private List<BallRollVisual> balls = new();
    [SerializeField] private Transform[] ballSpawnPoints;
    [SerializeField, Min(1)] private int initialBallCount = 4;
    [SerializeField, Min(0f)] private float ballRespawnDelay = 0.45f;
    [SerializeField, Min(0f)] private float outOfBoundsPadding = 1f;
    [SerializeField, Min(0f)] private float topOutOfBoundsMargin = 3f;
    [SerializeField, Min(0f)] private float stuckSpeed = 0.08f;
    [SerializeField, Min(1f)] private float stuckDuration = 8f;

    [Header("Gravity anomalies")]
    [SerializeField] private GravityZone gravityAnomalyPrefab;
    [SerializeField] private LocalAnomalyData gravityAnomalyData;
    [SerializeField] private Transform[] anomalySpawnPoints;
    [SerializeField] private FootballTargetLane[] anomalyLanes;
    [SerializeField, Range(1, 2)] private int activeAnomalyCount = 2;
    [SerializeField, Min(0f)] private float anomalyForce = 3.2f;
    [SerializeField] private Vector2 anomalyFieldSize = new(4.5f, 3.2f);
    [SerializeField, Min(0f)] private float anomalyMoveSpeed = 1.1f;

    [Header("Targets")]
    [SerializeField] private FootballScoreZone targetTemplate;
    [SerializeField] private FootballTargetLane[] targetLanes;
    [SerializeField, Min(1)] private int activeTargetCount = 3;
    [SerializeField, Min(0f)] private float targetRespawnDelay = 0.45f;
    [SerializeField, Min(0.1f)] private float targetBaseRadius = 0.8f;
    [SerializeField] private FootballTargetSettings greenTarget = new(
        FootballScoreZoneType.Green,
        new Color(0.15f, 0.9f, 0.25f, 0.9f),
        1.35f,
        1.5f,
        2);
    [SerializeField] private FootballTargetSettings yellowTarget = new(
        FootballScoreZoneType.Yellow,
        new Color(1f, 0.82f, 0.08f, 0.92f),
        1f,
        3f,
        5);
    [SerializeField] private FootballTargetSettings redTarget = new(
        FootballScoreZoneType.Red,
        new Color(1f, 0.12f, 0.08f, 0.92f),
        0.65f,
        5.5f,
        10);

    [Header("Gates")]
    [SerializeField] private GameObject gatePrefab;
    [SerializeField] private Transform gatesRuntime;
    [SerializeField, Min(0.1f)] private float gateVisualScale = 0.55f;
    [SerializeField] private Vector2 gateTriggerSize = new(3.6f, 1.8f);
    [SerializeField, Min(0)] private int gateScore = 20;

    [Header("Optional round timer")]
    [SerializeField] private bool useRoundTimer;
    [SerializeField, Min(1f)] private float roundDuration = 60f;

    [Header("Legacy scene compatibility")]
    [SerializeField, HideInInspector] private FootballScoreZone scoreZone;
    [SerializeField, HideInInspector] private BallRollVisual ball;
    [SerializeField, HideInInspector] private Transform ballSpawnPoint;
    [SerializeField, HideInInspector] private int goalsToComplete = 3;

    private readonly List<GravityZone> activeAnomalies = new();
    private readonly List<FootballScoreZone> activeTargets = new();
    private readonly List<FootballScoreZone> targetPool = new();
    private readonly List<FootballGateScoreZone> gates = new();
    private readonly Dictionary<FootballScoreZone, Coroutine> targetRespawns = new();
    private int currentScore;
    private int bestScore;
    private float remainingTime;
    private FootballZoneDebugView debugZoneView;

    public int Score => currentScore;
    public int BestScore => bestScore;
    public float RemainingTime => remainingTime;
    public IReadOnlyList<BallRollVisual> Balls => balls;
    public int ActiveBallCount => CountActiveBalls();
    public int ActiveAnomalyCount => activeAnomalies.Count;
    public int ActiveTargetCount => activeTargets.Count;
    public int GreenTargetCount => CountTargets(FootballScoreZoneType.Green);
    public int YellowTargetCount => CountTargets(FootballScoreZoneType.Yellow);
    public int RedTargetCount => CountTargets(FootballScoreZoneType.Red);
    public int GateCount => gates.Count;
    public BallRollVisual Ball => balls.Count > 0 ? balls[0] : ball;
    public int GoalsToComplete => goalsToComplete;
    public float ArenaWidth => arenaBounds != null ? arenaBounds.bounds.size.x : 0f;
    public float ArenaHeight => arenaBounds != null ? arenaBounds.bounds.size.y : 0f;
    public float BallZoneHeight => ballSpawnZone != null
        ? ballSpawnZone.bounds.size.y : 0f;
    public float AnomalyZoneHeight => anomalySpawnZone != null
        ? anomalySpawnZone.bounds.size.y : 0f;
    public float TargetZoneHeight => targetSpawnZone != null
        ? targetSpawnZone.bounds.size.y : 0f;
    public float CameraOrthographicSize => cameraFollow != null && cameraFollow.ControlledCamera != null
        ? cameraFollow.ControlledCamera.orthographicSize : 0f;
    public bool ShowDebugZones => showDebugZones;

    private void Awake()
    {
        bestScore = PlayerPrefs.GetInt(BestScoreKey, 0);
        remainingTime = roundDuration;
        EnsureTargetSettings();
        ResolveLegacyReferences();
        EnsurePlayerBoundary();
        SynchronizeArenaGeometry();
        EnsureBallPool();
        EnsureTargetPool();
        EnsureGates();
        playerBoundary?.RefreshCollisionExceptions();
        SynchronizeDebugZones();
        ResetRuntimeObjects();
        startZone?.SetAvailable(true);
        hud?.ShowIdle(roundDuration, bestScore);
    }

    private void Update()
    {
        if (!IsRunning)
            return;

        if (useRoundTimer)
        {
            remainingTime = Mathf.Max(0f, remainingTime - Time.deltaTime);
            if (remainingTime <= 0f)
            {
                CompleteGame();
                return;
            }
        }

        hud?.ShowRunning(useRoundTimer ? remainingTime : roundDuration, currentScore, bestScore);
    }

    public BallRollVisual GetRegisteredBall(Collider2D other)
    {
        if (other == null || other.isTrigger)
            return null;

        BallRollVisual candidate = other.GetComponent<BallRollVisual>();
        candidate ??= other.GetComponentInParent<BallRollVisual>();
        return candidate != null && balls.Contains(candidate) && candidate.gameObject.activeInHierarchy
            ? candidate : null;
    }

    public bool IsRegisteredBall(BallRollVisual candidate) => candidate != null && balls.Contains(candidate);
    public bool IsRegisteredBall(Collider2D other) => GetRegisteredBall(other) != null;

    public void OnBallEnteredScoreZone(FootballScoreZone target, BallRollVisual hitBall)
    {
        if (!IsRunning || target == null || !activeTargets.Contains(target))
            return;

        int laneIndex = target.LaneIndex;
        AddScore(target.Points);
        if (targetRespawns.TryGetValue(target, out Coroutine existing) && existing != null)
            StopCoroutine(existing);
        Coroutine routine = StartCoroutine(RespawnTarget(target, laneIndex));
        targetRespawns[target] = routine;
    }

    public void AddScore(int value)
    {
        if (!IsRunning || value <= 0)
            return;

        currentScore += value;
        hud?.ShowRunning(
            useRoundTimer ? remainingTime : roundDuration,
            currentScore,
            bestScore);
    }

    public void CancelCurrentRound()
    {
        if (!IsRunning)
            return;

        ResetGame();
    }

    public void OnGoalScored(FootballGoal goal) { }

    protected override void OnGameStarted()
    {
        BallRollVisual.CancelActiveSlowMotion();
        currentScore = 0;
        remainingTime = roundDuration;
        startZone?.SetAvailable(false);
        hud?.ShowRunning(roundDuration, currentScore, bestScore);
        SpawnInitialBalls();
        SpawnInitialAnomalies();
        SpawnInitialTargets();
        foreach (FootballGateScoreZone gate in gates)
            gate?.ResetContacts();
        FrameCamera();
        Debug.Log("[Football] V1 round started.", this);
    }

    protected override void OnGameCompleted()
    {
        bool newRecord = SaveBestScore();
        StopTargetRespawns();
        ResetRuntimeObjects();
        RestoreCamera();
        AllowRestart();
        startZone?.SetAvailable(true);
        hud?.ShowCompleted(currentScore, bestScore, newRecord);
    }

    protected override void OnGameFailed()
    {
        SaveBestScore();
        StopTargetRespawns();
        ResetRuntimeObjects();
        RestoreCamera();
        AllowRestart();
        startZone?.SetAvailable(true);
        hud?.ShowCompleted(currentScore, bestScore, false);
    }

    protected override void OnGameReset()
    {
        BallRollVisual.CancelActiveSlowMotion();
        StopTargetRespawns();
        ResetRuntimeObjects();
        RestoreCamera();
        currentScore = 0;
        remainingTime = roundDuration;
        startZone?.SetAvailable(true);
        hud?.ShowIdle(roundDuration, bestScore);
    }

    public void DebugAddBall()
    {
        if (!IsRunning) return;
        int index = CountActiveBalls();
        if (index < balls.Count) ActivateBall(index);
    }

    public void DebugSpawnAnomaly() { if (IsRunning) SpawnAnomaly(activeAnomalies.Count); }
    public void DebugSpawnTarget() { if (IsRunning) SpawnTarget(activeTargets.Count); }
    public void DebugAddScore(int value) => AddScore(value);

    public void DebugRerollTargets()
    {
        if (!IsRunning)
            return;

        StopTargetRespawns();
        for (int i = 0; i < activeTargets.Count; i++)
        {
            FootballScoreZone target = activeTargets[i];
            if (target != null)
                ConfigureRandomTarget(target, i, Random.value >= 0.5f);
        }
    }

    public void ToggleDebugZones()
    {
        showDebugZones = !showDebugZones;
        showLaneDebug = showDebugZones;
        SynchronizeDebugZones();
    }

    public void FrameCamera()
    {
        SynchronizeArenaGeometry();
        if (arenaBounds == null || cameraFollow == null)
            return;

        Camera targetCamera = cameraFollow.ControlledCamera;
        if (targetCamera == null || !targetCamera.orthographic)
            return;

        Bounds bounds = arenaBounds.bounds;
        float aspect = Mathf.Max(0.01f, targetCamera.aspect);
        float requiredByHeight = bounds.size.y * 0.5f;
        float requiredByWidth = bounds.size.x / (2f * aspect);
        float requiredSize = Mathf.Max(requiredByHeight, requiredByWidth) + cameraPadding;
        cameraFollow.BeginWorldBoundsFocus(this, bounds.center, requiredSize);
    }

    public void RestoreCamera()
    {
        if (cameraFollow != null)
            cameraFollow.EndWorldBoundsFocus(this);
    }

    public void SynchronizeArenaGeometry()
    {
        ResolveArenaLayout();
        arenaLayout?.RefreshLayout();
        SynchronizeDebugZones();
    }

    public void DebugClearBalls()
    {
        foreach (BallRollVisual item in balls)
            if (item != null) item.gameObject.SetActive(false);
    }

    public void DebugClearAnomalies()
    {
        for (int i = activeAnomalies.Count - 1; i >= 0; i--)
            if (activeAnomalies[i] != null) Destroy(activeAnomalies[i].gameObject);
        activeAnomalies.Clear();
    }

    public void DebugClearTargets()
    {
        StopTargetRespawns();
        foreach (FootballScoreZone target in activeTargets)
        {
            if (target == null) continue;
            target.Hide();
            target.ResetContacts();
            target.gameObject.SetActive(false);
        }
        activeTargets.Clear();
    }

    private void SpawnInitialBalls()
    {
        int count = Mathf.Min(initialBallCount, balls.Count, ballSpawnPoints?.Length ?? 0);
        for (int i = 0; i < count; i++) ActivateBall(i);
    }

    private void ActivateBall(int index)
    {
        if (index < 0 || index >= balls.Count || ballSpawnPoints == null || ballSpawnPoints.Length == 0)
            return;
        BallRollVisual item = balls[index];
        if (item == null) return;

        Transform spawn = ballSpawnPoints[index % ballSpawnPoints.Length];
        item.gameObject.SetActive(true);
        FootballBallRuntime runtime = item.GetComponent<FootballBallRuntime>();
        if (runtime == null)
            runtime = item.gameObject.AddComponent<FootballBallRuntime>();
        Bounds bounds = arenaBounds != null ? arenaBounds.bounds :
            ballSpawnZone != null ? ballSpawnZone.bounds : new Bounds(item.transform.position, Vector3.one * 50f);
        runtime.Configure(this, spawn, bounds, stuckSpeed, stuckDuration,
            outOfBoundsPadding, topOutOfBoundsMargin, ballRespawnDelay);
        runtime.RespawnNow();
    }

    private void SpawnInitialAnomalies()
    {
        int count = Mathf.Min(activeAnomalyCount, anomalySpawnPoints?.Length ?? 0);
        for (int i = 0; i < count; i++) SpawnAnomaly(i);
    }

    private void SpawnAnomaly(int index)
    {
        if (gravityAnomalyPrefab == null || gravityAnomalyData == null ||
            anomalySpawnPoints == null || anomalySpawnPoints.Length == 0) return;

        Transform spawn = anomalySpawnPoints[index % anomalySpawnPoints.Length];
        GravityZone anomaly = Instantiate(gravityAnomalyPrefab, spawn.position,
            Quaternion.identity, anomaliesRuntime);
        anomaly.name = $"FootballGravity_{index + 1}";
        anomaly.Initialize(gravityAnomalyData, null, anomalyFieldSize);
        anomaly.ConfigureForce(anomalyForce);
        anomaly.ConfigureAffectedColliderFilter(IsFootballBallCollider);

        if (anomalyLanes != null && anomalyLanes.Length > 0)
        {
            FootballTargetLane lane = anomalyLanes[index % anomalyLanes.Length];
            if (lane != null && lane.IsValid)
            {
                FootballPingPongMover mover = anomaly.gameObject.AddComponent<FootballPingPongMover>();
                mover.Configure(lane.LeftAnchor, lane.RightAnchor,
                    anomalyMoveSpeed > 0f ? anomalyMoveSpeed : lane.Speed, index % 2 == 0);
            }
        }
        activeAnomalies.Add(anomaly);
    }

    private void SpawnInitialTargets()
    {
        EnsureTargetPool();
        int count = Mathf.Min(activeTargetCount, targetLanes?.Length ?? 0, targetPool.Count);
        for (int i = 0; i < count; i++) SpawnTarget(i);
    }

    private void SpawnTarget(int laneIndex)
    {
        if (targetLanes == null || targetLanes.Length == 0 ||
            laneIndex < 0 || laneIndex >= targetPool.Count)
        {
            return;
        }

        FootballScoreZone target = targetPool[laneIndex];
        target.gameObject.SetActive(true);
        ConfigureRandomTarget(target, laneIndex, Random.value >= 0.5f);
        if (!activeTargets.Contains(target))
            activeTargets.Add(target);
    }

    private IEnumerator RespawnTarget(FootballScoreZone target, int laneIndex)
    {
        target.Hide();
        yield return new WaitForSeconds(targetRespawnDelay);
        if (IsRunning && target != null && targetLanes.Length > 0 && laneIndex >= 0)
            ConfigureRandomTarget(target, laneIndex, Random.value >= 0.5f);
        if (target != null)
            targetRespawns.Remove(target);
    }

    private void ConfigureRandomTarget(
        FootballScoreZone target,
        int laneIndex,
        bool moveRight)
    {
        if (target == null || targetLanes == null || targetLanes.Length == 0)
            return;

        FootballTargetSettings settings = GetRandomTargetSettings();
        int normalizedLane = laneIndex % targetLanes.Length;
        target.ConfigureOwner(this);
        target.ConfigureLane(
            targetLanes[normalizedLane],
            normalizedLane,
            settings.MoveSpeed,
            moveRight);
        target.Show(
            settings.Type,
            settings.Score,
            targetBaseRadius * settings.SizeScale,
            settings.Color);
    }

    private FootballTargetSettings GetRandomTargetSettings()
    {
        return Random.Range(0, 3) switch
        {
            0 => greenTarget,
            1 => yellowTarget,
            _ => redTarget
        };
    }

    private void ResetRuntimeObjects()
    {
        DebugClearAnomalies();
        DebugClearTargets();
        DebugClearBalls();
        if (targetTemplate != null)
        {
            targetTemplate.Hide();
            targetTemplate.gameObject.SetActive(false);
        }
        foreach (FootballGateScoreZone gate in gates)
            gate?.ResetContacts();
    }

    private void StopTargetRespawns()
    {
        foreach (Coroutine routine in targetRespawns.Values)
            if (routine != null) StopCoroutine(routine);
        targetRespawns.Clear();
    }

    private int CountActiveBalls()
    {
        int count = 0;
        foreach (BallRollVisual item in balls)
            if (item != null && item.gameObject.activeInHierarchy) count++;
        return count;
    }

    private bool SaveBestScore()
    {
        if (currentScore <= bestScore) return false;
        bestScore = currentScore;
        PlayerPrefs.SetInt(BestScoreKey, bestScore);
        PlayerPrefs.Save();
        return true;
    }

    private void EnsurePlayerBoundary()
    {
        if (playerBoundary != null)
            return;

        playerBoundary = GetComponentInChildren<FootballPlayerBoundary>(true);
        if (playerBoundary != null || !Application.isPlaying)
            return;

        GameObject boundaryObject = new("Player Boundary");
        boundaryObject.transform.SetParent(transform, false);
        boundaryObject.AddComponent<BoxCollider2D>();
        playerBoundary = boundaryObject.AddComponent<FootballPlayerBoundary>();
    }

    private void EnsureBallPool()
    {
        balls.RemoveAll(item => item == null);
        if (ballPrefab == null)
            ballPrefab = balls.Count > 0 ? balls[0] : ball;
        if (ballPrefab == null)
            return;

        Transform parent = ballsRuntime != null ? ballsRuntime : transform;
        while (balls.Count < initialBallCount)
        {
            BallRollVisual created = Instantiate(ballPrefab, parent);
            created.gameObject.SetActive(false);
            balls.Add(created);
        }

        for (int i = 0; i < balls.Count; i++)
        {
            if (balls[i] == null) continue;
            balls[i].name = $"FootballBall_{i + 1:00}";
            if (ballsRuntime != null)
                balls[i].transform.SetParent(ballsRuntime, true);
        }
    }

    private void EnsureTargetPool()
    {
        targetPool.RemoveAll(item => item == null);
        if (targetTemplate == null)
            return;

        if (targetsRuntime != null)
        {
            foreach (FootballScoreZone existing in
                targetsRuntime.GetComponentsInChildren<FootballScoreZone>(true))
            {
                if (existing != null && !targetPool.Contains(existing))
                    targetPool.Add(existing);
            }
        }

        if (!targetPool.Contains(targetTemplate))
            targetPool.Add(targetTemplate);

        int required = Mathf.Max(1, targetLanes?.Length ?? activeTargetCount);
        while (targetPool.Count < required)
        {
            FootballScoreZone created = Instantiate(targetTemplate, targetsRuntime);
            created.gameObject.SetActive(false);
            targetPool.Add(created);
        }

        for (int i = 0; i < targetPool.Count; i++)
        {
            FootballScoreZone target = targetPool[i];
            target.name = $"Target_{i + 1:00}";
            target.ConfigureOwner(this);
            if (targetsRuntime != null)
                target.transform.SetParent(targetsRuntime, true);
        }
    }

    private void EnsureTargetSettings()
    {
        greenTarget ??= new FootballTargetSettings(
            FootballScoreZoneType.Green,
            new Color(0.15f, 0.9f, 0.25f, 0.9f),
            1.35f,
            1.5f,
            2);
        yellowTarget ??= new FootballTargetSettings(
            FootballScoreZoneType.Yellow,
            new Color(1f, 0.82f, 0.08f, 0.92f),
            1f,
            3f,
            5);
        redTarget ??= new FootballTargetSettings(
            FootballScoreZoneType.Red,
            new Color(1f, 0.12f, 0.08f, 0.92f),
            0.65f,
            5.5f,
            10);
    }

    private void EnsureGates()
    {
        if (gatesRuntime == null)
        {
            Transform existing = transform.parent != null
                ? transform.parent.Find("Gates")
                : null;
            if (existing != null)
                gatesRuntime = existing;
            else if (Application.isPlaying)
            {
                GameObject root = new("Gates");
                root.transform.SetParent(transform.parent != null ? transform.parent : transform, false);
                gatesRuntime = root.transform;
            }
        }

        gates.Clear();
        if (gatesRuntime != null)
            gates.AddRange(gatesRuntime.GetComponentsInChildren<FootballGateScoreZone>(true));

        while (Application.isPlaying && gatePrefab != null && gatesRuntime != null && gates.Count < 2)
        {
            int index = gates.Count;
            Object clone = Object.Instantiate((Object)gatePrefab, gatesRuntime);
            GameObject visual = clone as GameObject;
            if (visual == null)
            {
                Debug.LogError(
                    "[Football] Gate prefab reference is not a GameObject. " +
                    "Persistent gate instances are required in MainMenu.unity.",
                    this);
                if (clone != null)
                    Destroy(clone);
                break;
            }
            visual.name = index == 0 ? "Gate_Left" : "Gate_Right";
            Vector3 prefabScale = visual.transform.localScale;
            visual.transform.localScale = new Vector3(
                prefabScale.x * gateVisualScale,
                prefabScale.y * gateVisualScale,
                prefabScale.z);
            StabilizeGate(visual);

            GameObject trigger = new("ScoreTrigger");
            trigger.transform.SetParent(visual.transform, false);
            trigger.AddComponent<BoxCollider2D>();
            FootballGateScoreZone scoreZone = trigger.AddComponent<FootballGateScoreZone>();
            gates.Add(scoreZone);
        }

        for (int i = 0; i < gates.Count; i++)
        {
            FootballGateScoreZone gate = gates[i];
            if (gate == null)
                continue;

            Transform gateRoot = gate.transform.parent != null
                ? gate.transform.parent
                : gate.transform;
            StabilizeGate(gateRoot.gameObject);
            gate.Configure(this, gateScore, gateTriggerSize);
        }

        ResolveArenaLayout();
        arenaLayout?.SynchronizeRuntimeGates(gates);
    }

    private static void StabilizeGate(GameObject gateRoot)
    {
        foreach (Rigidbody2D body in gateRoot.GetComponentsInChildren<Rigidbody2D>(true))
        {
            body.bodyType = RigidbodyType2D.Kinematic;
            body.linearVelocity = Vector2.zero;
            body.angularVelocity = 0f;
            body.constraints = RigidbodyConstraints2D.FreezeAll;
        }
    }

    private int CountTargets(FootballScoreZoneType type)
    {
        int count = 0;
        foreach (FootballScoreZone target in activeTargets)
        {
            if (target != null && target.IsAcceptingBalls && target.Type == type)
                count++;
        }
        return count;
    }

    private void EnsureDebugZoneView()
    {
        if (debugZoneView != null)
            return;

        debugZoneView = GetComponentInChildren<FootballZoneDebugView>(true);
        if (debugZoneView != null)
            return;

        GameObject root = new("Zone Debug Visuals");
        root.transform.SetParent(transform, false);
        debugZoneView = root.AddComponent<FootballZoneDebugView>();
    }

    private void SynchronizeDebugZones()
    {
        if (!Application.isPlaying)
            return;

        if (!showDebugZones)
        {
            debugZoneView?.Synchronize(
                ballSpawnZone,
                anomalySpawnZone,
                targetSpawnZone,
                false);
            return;
        }

        EnsureDebugZoneView();
        debugZoneView.Synchronize(
            ballSpawnZone,
            anomalySpawnZone,
            targetSpawnZone,
            showDebugZones);
    }

    private void ResolveLegacyReferences()
    {
        ResolveArenaLayout();
        targetTemplate ??= scoreZone;
        if (balls.Count == 0 && ball != null) balls.Add(ball);
        if ((ballSpawnPoints == null || ballSpawnPoints.Length == 0) && ballSpawnPoint != null)
            ballSpawnPoints = new[] { ballSpawnPoint };
    }

    private void ResolveArenaLayout()
    {
        if (arenaLayout != null)
            return;

        arenaLayout = GetComponentInParent<FootballArenaLayout>(true);
    }

    private static bool IsFootballBallCollider(Collider2D other)
    {
        FootballBallRuntime runtime = other != null
            ? other.GetComponentInParent<FootballBallRuntime>()
            : null;
        return runtime != null && runtime.IsPhysicalCollider(other);
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        EnsureTargetSettings();
        cameraPadding = Mathf.Max(0f, cameraPadding);
        targetBaseRadius = Mathf.Max(0.1f, targetBaseRadius);
        topOutOfBoundsMargin = Mathf.Max(0f, topOutOfBoundsMargin);
        SynchronizeArenaGeometry();
    }

    private void OnDrawGizmos()
    {
        if (showDebugZones)
        {
            DrawZone(arenaBounds, Color.white);
            DrawZone(ballSpawnZone, new Color(0.2f, 1f, 0.35f, 0.8f));
            DrawZone(anomalySpawnZone, new Color(1f, 0.25f, 0.25f, 0.8f));
            DrawZone(targetSpawnZone, new Color(0.15f, 0.65f, 1f, 0.8f));
            if (playerBoundary != null)
            {
                DrawZone(
                    playerBoundary.GetComponent<Collider2D>(),
                    new Color(1f, 0.3f, 1f, 0.95f));
            }
        }

        if (showLaneDebug)
        {
            DrawLanes(anomalyLanes, new Color(1f, 0.45f, 0.2f, 0.9f));
            DrawLanes(targetLanes, new Color(0.2f, 0.75f, 1f, 0.9f));
        }
    }

    private static void DrawZone(Collider2D zone, Color color)
    {
        if (zone == null) return;
        Gizmos.color = color;
        Gizmos.DrawWireCube(zone.bounds.center, zone.bounds.size);
    }

    private static void DrawLanes(FootballTargetLane[] lanes, Color color)
    {
        if (lanes == null) return;
        Gizmos.color = color;
        foreach (FootballTargetLane lane in lanes)
        {
            if (lane == null || !lane.IsValid) continue;
            Gizmos.DrawLine(lane.LeftAnchor.position, lane.RightAnchor.position);
            Gizmos.DrawWireSphere(lane.LeftAnchor.position, 0.15f);
            Gizmos.DrawWireSphere(lane.RightAnchor.position, 0.15f);
        }
    }
#endif
}
