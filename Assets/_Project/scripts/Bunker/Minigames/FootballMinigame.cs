using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public sealed class FootballMinigame : BunkerMinigame
{
    private const string BestScoreKey = "BunkerFootballBestScore";

    [Header("Scene")]
    [SerializeField] private List<BallRollVisual> balls = new();
    [SerializeField] private Collider2D playAreaBounds;
    [SerializeField] private FootballScoreZone scoreZone;
    [SerializeField] private FootballStartZone startZone;
    [SerializeField] private FootballMinigameHUD hud;
    [SerializeField] private Transform player;

    [Header("Round")]
    [SerializeField, Min(1f)] private float roundDuration = 60f;
    [SerializeField, Min(0f)] private float zoneRespawnDelay = 0.3f;

    [Header("Zone balance")]
    [SerializeField, Min(0.1f)] private float greenRadius = 1.6f;
    [SerializeField, Min(0.1f)] private float blueRadius = 1.1f;
    [SerializeField, Min(0.1f)] private float redRadius = 0.65f;
    [SerializeField, Min(0)] private int greenPoints = 3;
    [SerializeField, Min(0)] private int bluePoints = 6;
    [SerializeField, Min(0)] private int redPoints = 10;
    [SerializeField, Min(0f)] private float greenWeight = 50f;
    [SerializeField, Min(0f)] private float blueWeight = 35f;
    [SerializeField, Min(0f)] private float redWeight = 15f;

    [Header("Zone spawning")]
    [SerializeField, Min(0f)] private float spawnMargin = 1.5f;
    [SerializeField, Min(0f)] private float minDistanceFromPlayer = 3f;
    [SerializeField, Min(0f)] private float minDistanceFromBalls = 2.5f;
    [SerializeField, Min(1)] private int spawnAttempts = 20;

    [Header("Legacy scene compatibility")]
    [SerializeField, HideInInspector] private BallRollVisual ball;
    [SerializeField, HideInInspector] private Transform ballSpawnPoint;
    [SerializeField, HideInInspector] private int goalsToComplete = 3;

    private Coroutine respawnRoutine;
    private int currentScore;
    private int bestScore;
    private float remainingTime;
    private bool newRecord;
    private bool returningToIdleAfterRound;

    public int Score => currentScore;
    public int BestScore => bestScore;
    public float RemainingTime => remainingTime;
    public IReadOnlyList<BallRollVisual> Balls => balls;

    // Kept so the decorative legacy FootballGoal component remains binary-compatible.
    public BallRollVisual Ball => balls.Count > 0 ? balls[0] : ball;
    public int GoalsToComplete => goalsToComplete;

    private void Awake()
    {
        bestScore = PlayerPrefs.GetInt(BestScoreKey, 0);
        remainingTime = roundDuration;
        scoreZone?.Hide();
        startZone?.SetAvailable(true);
        hud?.ShowIdle(roundDuration, bestScore);
    }

    private void Update()
    {
        if (!IsRunning)
            return;

        remainingTime = Mathf.Max(0f, remainingTime - Time.unscaledDeltaTime);
        hud?.ShowRunning(remainingTime, currentScore, bestScore);

        if (remainingTime <= 0f)
            CompleteGame();
    }

    public bool IsRegisteredBall(BallRollVisual candidate)
    {
        return candidate != null && balls.Contains(candidate);
    }

    public bool IsRegisteredBall(Collider2D other)
    {
        if (other == null || other.isTrigger)
            return false;

        BallRollVisual candidate = other.GetComponent<BallRollVisual>();
        if (candidate == null)
            candidate = other.GetComponentInParent<BallRollVisual>();

        return IsRegisteredBall(candidate);
    }

    public void OnBallEnteredScoreZone(FootballScoreZone zone)
    {
        if (!IsRunning || zone == null || zone != scoreZone)
            return;

        zone.Hide();
        currentScore += zone.Points;
        hud?.ShowRunning(remainingTime, currentScore, bestScore);

        if (respawnRoutine != null)
            StopCoroutine(respawnRoutine);
        respawnRoutine = StartCoroutine(RespawnZone());
    }

    // Legacy goals are decorative in the score-zone version.
    public void OnGoalScored(FootballGoal goal) { }

    protected override void OnGameStarted()
    {
        Debug.Log("[Football] Started");

        currentScore = 0;
        remainingTime = roundDuration;
        newRecord = false;
        startZone?.SetAvailable(false);
        hud?.ShowRunning(remainingTime, currentScore, bestScore);

        BunkerNotificationManager notifications =
            BunkerContext.Instance != null && BunkerContext.Instance.Notifications != null
                ? BunkerContext.Instance.Notifications
                : BunkerNotificationManager.Instance;
        notifications?.ShowInfo("FOOTBALL TEST STARTED");

        SpawnNextZone();
    }

    protected override void OnGameCompleted()
    {
        StopZoneRespawn();
        scoreZone?.Hide();

        if (currentScore > bestScore)
        {
            bestScore = currentScore;
            newRecord = true;
            PlayerPrefs.SetInt(BestScoreKey, bestScore);
            PlayerPrefs.Save();
        }

        startZone?.SetAvailable(true);
        hud?.ShowCompleted(currentScore, bestScore, newRecord);

        ReturnToIdleAfterRound();
    }

    protected override void OnGameFailed()
    {
        StopZoneRespawn();
        scoreZone?.Hide();
        startZone?.SetAvailable(true);
        hud?.ShowCompleted(currentScore, bestScore, false);

        ReturnToIdleAfterRound();
    }

    protected override void OnGameReset()
    {
        if (returningToIdleAfterRound)
            return;

        StopZoneRespawn();
        currentScore = 0;
        remainingTime = roundDuration;
        newRecord = false;
        scoreZone?.Hide();
        startZone?.SetAvailable(true);
        hud?.ShowIdle(roundDuration, bestScore);
    }

    private IEnumerator RespawnZone()
    {
        yield return new WaitForSecondsRealtime(zoneRespawnDelay);
        respawnRoutine = null;

        if (IsRunning)
            SpawnNextZone();
    }

    private void SpawnNextZone()
    {
        if (scoreZone == null || playAreaBounds == null)
            return;

        FootballScoreZoneType type = ChooseZoneType();
        float radius = GetRadius(type);
        Vector2 position = FindSpawnPosition(radius);

        scoreZone.transform.position = position;
        scoreZone.Show(
            type,
            GetPoints(type),
            radius,
            GetColor(type));
    }

    private FootballScoreZoneType ChooseZoneType()
    {
        float total = greenWeight + blueWeight + redWeight;
        if (total <= 0f)
            return FootballScoreZoneType.Green;

        float roll = Random.value * total;
        if (roll < greenWeight)
            return FootballScoreZoneType.Green;
        if (roll < greenWeight + blueWeight)
            return FootballScoreZoneType.Blue;
        return FootballScoreZoneType.Red;
    }

    private Vector2 FindSpawnPosition(float radius)
    {
        Bounds bounds = playAreaBounds.bounds;
        float inset = spawnMargin + radius;
        float minX = bounds.min.x + inset;
        float maxX = bounds.max.x - inset;
        float minY = bounds.min.y + inset;
        float maxY = bounds.max.y - inset;

        if (minX > maxX)
            minX = maxX = bounds.center.x;
        if (minY > maxY)
            minY = maxY = bounds.center.y;

        Vector2 fallback = bounds.center;
        for (int attempt = 0; attempt < spawnAttempts; attempt++)
        {
            Vector2 candidate = new(
                Random.Range(minX, maxX),
                Random.Range(minY, maxY));
            fallback = candidate;

            if (player != null &&
                Vector2.Distance(candidate, player.position) < minDistanceFromPlayer + radius)
            {
                continue;
            }

            bool tooCloseToBall = false;
            foreach (BallRollVisual registeredBall in balls)
            {
                if (registeredBall != null &&
                    Vector2.Distance(candidate, registeredBall.transform.position) < minDistanceFromBalls + radius)
                {
                    tooCloseToBall = true;
                    break;
                }
            }

            if (!tooCloseToBall)
                return candidate;
        }

        return fallback;
    }

    private float GetRadius(FootballScoreZoneType type)
    {
        return type switch
        {
            FootballScoreZoneType.Blue => blueRadius,
            FootballScoreZoneType.Red => redRadius,
            _ => greenRadius
        };
    }

    private int GetPoints(FootballScoreZoneType type)
    {
        return type switch
        {
            FootballScoreZoneType.Blue => bluePoints,
            FootballScoreZoneType.Red => redPoints,
            _ => greenPoints
        };
    }

    private static Color GetColor(FootballScoreZoneType type)
    {
        return type switch
        {
            FootballScoreZoneType.Blue => new Color(0.1f, 0.55f, 1f, 0.82f),
            FootballScoreZoneType.Red => new Color(1f, 0.15f, 0.12f, 0.88f),
            _ => new Color(0.12f, 1f, 0.3f, 0.78f)
        };
    }

    private void StopZoneRespawn()
    {
        if (respawnRoutine == null)
            return;

        StopCoroutine(respawnRoutine);
        respawnRoutine = null;
    }

    private void ReturnToIdleAfterRound()
    {
        returningToIdleAfterRound = true;
        ResetGame();
        returningToIdleAfterRound = false;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        roundDuration = Mathf.Max(1f, roundDuration);
        zoneRespawnDelay = Mathf.Max(0f, zoneRespawnDelay);
        spawnAttempts = Mathf.Max(1, spawnAttempts);
        greenRadius = Mathf.Max(0.1f, greenRadius);
        blueRadius = Mathf.Max(0.1f, blueRadius);
        redRadius = Mathf.Max(0.1f, redRadius);
    }
#endif
}
