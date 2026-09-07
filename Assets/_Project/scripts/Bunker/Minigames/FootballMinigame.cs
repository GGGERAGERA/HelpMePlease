using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public sealed class FootballMinigame : BunkerMinigame
{
    [SerializeField] private BoxCollider2D cameraBounds;
    [SerializeField] private Collider2D[] walls;
    [SerializeField] private Transform playerStart;
    private Rect savedCameraRect;
    private bool hasCameraSession;
    private int lastScreenWidth, lastScreenHeight;
    private readonly Collider2D[] spawnOverlaps = new Collider2D[8];
    public Bounds PlayBounds => arenaBounds.bounds;
    public Transform[] BallSpawnPoints => ballSpawnPoints;
    public Transform PlayerStart => playerStart;

    private const string BestScoreKey = "BunkerFootballBestScore";
    [Header("Arena geometry")]
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

    [Header("Runtime roots")]
    [SerializeField] private Transform anomaliesRuntime;

    [Header("Balls")]
    [SerializeField] private List<BallRollVisual> balls = new();
    [SerializeField] private Transform[] ballSpawnPoints;
    [SerializeField, Min(1)] private int initialBallCount = 4;
    [SerializeField, Min(1f)] private float stuckDuration = 8f;

    [Header("Gravity anomalies")]
    [SerializeField] private GravityZone gravityAnomalyPrefab;
    [SerializeField] private LocalAnomalyData gravityAnomalyData;
    [SerializeField] private Transform[] anomalySpawnPoints;
    [SerializeField] private FootballTargetLane[] anomalyLanes;
    [SerializeField, Range(1, 2)] private int activeAnomalyCount = 2;
    [SerializeField, Min(0f)] private float anomalyForce = 3.2f;
    [SerializeField] private Vector2 anomalyFieldSize = new(7f, 3.2f);
    [SerializeField, Min(0f)] private float anomalyMoveSpeed = 1.1f;

    [SerializeField] private Vector2 anomalySwapInterval = new(10f, 15f);
    [SerializeField, Min(0.1f)] private float anomalySwapDuration = 2f;
    [SerializeField] private Color attractionColor = new(.32f, .16f, .72f, 1f);
    [SerializeField] private Color repulsionColor = new(.12f, .8f, 1f, 1f);
    private float anomalySwapRemaining;
    private float anomalyStartingPolarity = 1f;

    [Header("Targets")]
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


    [Header("Optional round timer")]
    [SerializeField] private bool useRoundTimer;
    [SerializeField, Min(1f)] private float roundDuration = 60f;

    private readonly List<GravityZone> activeAnomalies = new();
    private readonly List<FootballScoreZone> activeTargets = new();
    [SerializeField] private FootballScoreZone[] targetPool;
    [SerializeField] private FootballGateScoreZone[] gates;
    private readonly Dictionary<FootballScoreZone, Coroutine> targetRespawns = new();
    private int currentScore;
    private int bestScore;
    private float remainingTime;

    public int GoalCount { get; private set; }
    public int GoalScore { get; private set; }
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
    public int GateCount => gates.Length;
    public BallRollVisual Ball => balls[0];
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
        foreach (var target in targetPool) target.ConfigureOwner(this);
        ResetRuntimeObjects();
        startZone?.SetAvailable(true);
        hud?.ShowIdle(roundDuration, bestScore);
    }

    private void Update()
    {
        if (!IsRunning)
            return;
        if (Time.timeScale > 0f && Input.GetKeyDown(KeyCode.R)) ResetBall();

        if (useRoundTimer)
        {
            remainingTime = Mathf.Max(0f, remainingTime - Time.deltaTime);
            if (remainingTime <= 0f)
            {
                CompleteGame();
                return;
            }
        }

        UpdateAnomalyPolarity();
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
        target.ShowScoreFeedback();
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

    public void AddGoal(int points)
    {
        if (!IsRunning) return;
        GoalCount++;
        GoalScore += points;
        AddScore(points);
        hud.SetGoalStats(GoalCount, GoalScore);
    }

    public void CancelCurrentRound()
    {
        if (!IsRunning)
            return;

        ResetGame();
    }

    protected override void OnGameStarted()
    {
        BallRollVisual.CancelActiveSlowMotion();
        currentScore = 0;
        GoalCount = GoalScore = 0;
        hud.SetGoalStats(GoalCount, GoalScore);
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
        GoalCount = GoalScore = 0;
        hud.SetGoalStats(GoalCount, GoalScore);
        remainingTime = roundDuration;
        startZone?.SetAvailable(true);
        hud?.ShowIdle(roundDuration, bestScore);
    }

    public void ToggleDebugZones()
    {
        showDebugZones = !showDebugZones;
        showLaneDebug = showDebugZones;
    }

    private void ClearBalls()
    {
        foreach (BallRollVisual item in balls)
            if (item != null) item.gameObject.SetActive(false);
    }

    private void ClearAnomalies()
    {
        for (int i = activeAnomalies.Count - 1; i >= 0; i--)
            if (activeAnomalies[i] != null) Destroy(activeAnomalies[i].gameObject);
        activeAnomalies.Clear();
    }

    private void ClearTargets()
    {
        StopTargetRespawns();
        foreach (FootballScoreZone target in targetPool)
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

        runtime.Configure(this, stuckDuration);
        runtime.RespawnNow(spawn);
        RestoreBallBoundary(item);
    }

    private void ApplyAnomalyPolarity(float polarity)
    {
        for (int i = 0; i < activeAnomalies.Count; i++)
        {
            float value = i % 2 == 0 ? polarity : -polarity;
            activeAnomalies[i].ConfigureRadialPolarity(value,
                Color.Lerp(repulsionColor, attractionColor, (value + 1f) * 0.5f));
        }
    }

    private void UpdateAnomalyPolarity()
    {
        anomalySwapRemaining -= Time.deltaTime;
        float progress = 1f - Mathf.Clamp01(anomalySwapRemaining / anomalySwapDuration);
        ApplyAnomalyPolarity(Mathf.Lerp(anomalyStartingPolarity, -anomalyStartingPolarity,
            Mathf.SmoothStep(0f, 1f, progress)));
        if (anomalySwapRemaining <= 0f)
        {
            anomalyStartingPolarity = -anomalyStartingPolarity;
            anomalySwapRemaining = Random.Range(anomalySwapInterval.x, anomalySwapInterval.y);
        }
    }

    private void SpawnInitialAnomalies()
    {
        int count = Mathf.Min(activeAnomalyCount, anomalySpawnPoints?.Length ?? 0);
        for (int i = 0; i < count; i++) SpawnAnomaly(i);
        anomalyStartingPolarity = 1f;
        anomalySwapRemaining = Random.Range(anomalySwapInterval.x, anomalySwapInterval.y);
        ApplyAnomalyPolarity(anomalyStartingPolarity);
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
        int count = Mathf.Min(activeTargetCount, targetLanes?.Length ?? 0, targetPool.Length);
        for (int i = 0; i < count; i++) SpawnTarget(i);
    }

    private void SpawnTarget(int laneIndex)
    {
        if (targetLanes == null || targetLanes.Length == 0 ||
            laneIndex < 0 || laneIndex >= targetPool.Length)
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
        ClearAnomalies();
        ClearTargets();
        ClearBalls();
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
        targetBaseRadius = Mathf.Max(0.1f, targetBaseRadius);
    }

    private void OnDrawGizmos()
    {
        if (showDebugZones)
        {
            DrawZone(arenaBounds, Color.white);
            DrawZone(cameraBounds, Color.yellow);
            if (walls != null) foreach (var wall in walls) DrawZone(wall, Color.cyan);
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

    public void FrameCamera()
    {
        if (cameraFollow == null) return;
        Camera camera = cameraFollow.ControlledCamera;
        if (!hasCameraSession) savedCameraRect = camera.rect;
        hasCameraSession = true;
        Bounds bounds = cameraBounds.bounds;
        float arenaAspect = bounds.size.x / bounds.size.y;
        float screenAspect = (float)Screen.width / Mathf.Max(1, Screen.height);
        // Fit the authored CameraBounds exactly, with bars instead of empty world.
        Rect rect = new(0f, 0f, 1f, 1f);
        if (screenAspect > arenaAspect)
        {
            rect.width = arenaAspect / screenAspect;
            rect.x = (1f - rect.width) * .5f;
        }
        else
        {
            rect.height = screenAspect / arenaAspect;
            rect.y = (1f - rect.height) * .5f;
        }
        camera.rect = rect;
        hud.SetViewport(rect);
        cameraFollow.BeginWorldBoundsFocus(this, bounds.center, bounds.size.y * .5f);
        lastScreenWidth = Screen.width;
        lastScreenHeight = Screen.height;
    }
    public void RestoreCamera()
    {
        if (!hasCameraSession || cameraFollow == null) return;
        cameraFollow.EndWorldBoundsFocus(this);
        cameraFollow.ControlledCamera.rect = savedCameraRect;
        hasCameraSession = false;
    }
    private void LateUpdate()
    {
        if (hasCameraSession && (Screen.width != lastScreenWidth || Screen.height != lastScreenHeight))
            FrameCamera();
    }
    public void RestoreBallBoundary(BallRollVisual item) => playerBoundary.IgnoreBall(item);
    public void ResetBall()
    {
        if (!IsRunning) return;
        foreach (var item in balls) if (item.gameObject.activeInHierarchy) ResetBall(item);
    }
    public void ResetBall(BallRollVisual item)
    {
        if (!IsRunning) return;
        int first = balls.IndexOf(item);
        var filter = new ContactFilter2D().NoFilter();
        filter.useTriggers = false;
        Rigidbody2D body = item.GetComponent<Rigidbody2D>();
        float radius = item.GetComponent<CircleCollider2D>().radius * Mathf.Abs(item.transform.lossyScale.x);
        for (int i = 0; i < ballSpawnPoints.Length; i++)
        {
            Transform spawn = ballSpawnPoints[(first + i) % ballSpawnPoints.Length];
            int count = Physics2D.OverlapCircle(spawn.position, radius + .05f, filter, spawnOverlaps);
            bool blocked = count == spawnOverlaps.Length;
            for (int j = 0; j < count; j++) blocked |= spawnOverlaps[j].attachedRigidbody != body;
            if (blocked) continue;
            item.GetComponent<FootballBallRuntime>().RespawnNow(spawn);
            RestoreBallBoundary(item);
            return;
        }
    }
    public void OnPlayerLeftArena() => CancelCurrentRound();
    private void OnDisable()
    {
        BallRollVisual.CancelActiveSlowMotion();
        StopAllCoroutines();
        RestoreCamera();
        if (Application.isPlaying) ResetGame();
    }
}
