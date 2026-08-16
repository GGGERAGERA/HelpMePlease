using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public sealed class FootballMinigame : BunkerMinigame
{
    private const string BestScoreKey = "BunkerFootballBestScore";

    [Header("Manual scene references")]
    [SerializeField] private Collider2D ballSpawnZone;
    [SerializeField] private Collider2D anomalySpawnZone;
    [SerializeField] private Collider2D targetSpawnZone;
    [SerializeField] private Collider2D playAreaBounds;
    [SerializeField] private FootballStartZone startZone;
    [SerializeField] private FootballMinigameHUD hud;

    [Header("Runtime roots")]
    [SerializeField] private Transform ballsRuntime;
    [SerializeField] private Transform anomaliesRuntime;
    [SerializeField] private Transform targetsRuntime;

    [Header("Balls")]
    [SerializeField] private List<BallRollVisual> balls = new();
    [SerializeField] private Transform[] ballSpawnPoints;
    [SerializeField, Min(1)] private int initialBallCount = 4;
    [SerializeField, Min(0f)] private float ballRespawnDelay = 0.45f;
    [SerializeField, Min(0f)] private float outOfBoundsPadding = 1f;
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
    [SerializeField, Min(0)] private int targetScore = 5;
    [SerializeField, Min(0.1f)] private float targetRadius = 0.8f;
    [SerializeField] private Color targetColor = new(0.15f, 0.75f, 1f, 0.86f);

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
    private readonly List<Coroutine> targetRespawns = new();
    private int currentScore;
    private int bestScore;
    private float remainingTime;

    public int Score => currentScore;
    public int BestScore => bestScore;
    public float RemainingTime => remainingTime;
    public IReadOnlyList<BallRollVisual> Balls => balls;
    public int ActiveBallCount => CountActiveBalls();
    public int ActiveAnomalyCount => activeAnomalies.Count;
    public int ActiveTargetCount => activeTargets.Count;
    public BallRollVisual Ball => balls.Count > 0 ? balls[0] : ball;
    public int GoalsToComplete => goalsToComplete;

    private void Awake()
    {
        bestScore = PlayerPrefs.GetInt(BestScoreKey, 0);
        remainingTime = roundDuration;
        ResolveLegacyReferences();
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
            remainingTime = Mathf.Max(0f, remainingTime - Time.unscaledDeltaTime);
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

        currentScore += target.Points;
        hud?.ShowRunning(useRoundTimer ? remainingTime : roundDuration, currentScore, bestScore);
        hitBall?.GetComponent<FootballBallRuntime>()?.RequestRespawn();
        int laneIndex = activeTargets.IndexOf(target);
        Coroutine routine = StartCoroutine(RespawnTarget(target, laneIndex));
        targetRespawns.Add(routine);
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
        Debug.Log("[Football] V1 round started.", this);
    }

    protected override void OnGameCompleted()
    {
        bool newRecord = SaveBestScore();
        StopTargetRespawns();
        ResetRuntimeObjects();
        startZone?.SetAvailable(false);
        hud?.ShowCompleted(currentScore, bestScore, newRecord);
    }

    protected override void OnGameFailed()
    {
        SaveBestScore();
        StopTargetRespawns();
        ResetRuntimeObjects();
        startZone?.SetAvailable(false);
        hud?.ShowCompleted(currentScore, bestScore, false);
    }

    protected override void OnGameReset()
    {
        BallRollVisual.CancelActiveSlowMotion();
        StopTargetRespawns();
        ResetRuntimeObjects();
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
            if (target == targetTemplate) target.gameObject.SetActive(false);
            else Destroy(target.gameObject);
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
        Bounds bounds = playAreaBounds != null ? playAreaBounds.bounds :
            ballSpawnZone != null ? ballSpawnZone.bounds : new Bounds(item.transform.position, Vector3.one * 50f);
        runtime.Configure(this, spawn, bounds, stuckSpeed, stuckDuration,
            outOfBoundsPadding, ballRespawnDelay);
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
        int count = Mathf.Min(activeTargetCount, targetLanes?.Length ?? 0);
        for (int i = 0; i < count; i++) SpawnTarget(i);
    }

    private void SpawnTarget(int laneIndex)
    {
        if (targetTemplate == null || targetLanes == null || targetLanes.Length == 0) return;

        FootballScoreZone target = activeTargets.Count == 0
            ? targetTemplate : Instantiate(targetTemplate, targetsRuntime);
        target.name = $"Target_{activeTargets.Count + 1:00}";
        if (targetsRuntime != null) target.transform.SetParent(targetsRuntime, true);
        target.gameObject.SetActive(true);
        target.ConfigureLane(targetLanes[laneIndex % targetLanes.Length], laneIndex % 2 == 0);
        target.Show(FootballScoreZoneType.Blue, targetScore, targetRadius, targetColor);
        activeTargets.Add(target);
    }

    private IEnumerator RespawnTarget(FootballScoreZone target, int laneIndex)
    {
        target.Hide();
        yield return new WaitForSecondsRealtime(targetRespawnDelay);
        if (IsRunning && target != null && targetLanes.Length > 0)
        {
            target.ConfigureLane(targetLanes[laneIndex % targetLanes.Length], laneIndex % 2 != 0);
            target.Show(FootballScoreZoneType.Blue, targetScore, targetRadius, targetColor);
        }
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
    }

    private void StopTargetRespawns()
    {
        foreach (Coroutine routine in targetRespawns)
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

    private void ResolveLegacyReferences()
    {
        targetTemplate ??= scoreZone;
        if (balls.Count == 0 && ball != null) balls.Add(ball);
        if ((ballSpawnPoints == null || ballSpawnPoints.Length == 0) && ballSpawnPoint != null)
            ballSpawnPoints = new[] { ballSpawnPoint };
    }

    private static bool IsFootballBallCollider(Collider2D other) =>
        other != null && other.GetComponentInParent<FootballBallRuntime>() != null;

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        DrawZone(ballSpawnZone, new Color(0.2f, 1f, 0.35f, 0.8f));
        DrawZone(anomalySpawnZone, new Color(1f, 0.25f, 0.25f, 0.8f));
        DrawZone(targetSpawnZone, new Color(0.15f, 0.65f, 1f, 0.8f));
    }

    private static void DrawZone(Collider2D zone, Color color)
    {
        if (zone == null) return;
        Gizmos.color = color;
        Gizmos.DrawWireCube(zone.bounds.center, zone.bounds.size);
    }
#endif
}
