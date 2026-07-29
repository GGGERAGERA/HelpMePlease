using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public sealed class FalseSignalEvent : WorldEvent
{
    [Header("Start")]
    [SerializeField, Min(0.1f)] private float startRadius = 2.5f;
    [SerializeField] private Material lineMaterial;

    [Header("Signals")]
    [SerializeField] private FalseSignalPoint signalPointPrefab;
    [SerializeField, Min(3)] private int signalPointCount = 3;
    [SerializeField, Min(0f)] private float minPointDistance = 4f;
    [SerializeField, Min(0f)] private float maxPointDistance = 12f;
    [SerializeField, Min(0f)] private float minPointSeparation = 4f;
    [SerializeField, Min(0f)] private float pointEdgePadding = 1f;
    [SerializeField, Min(1)] private int positionAttempts = 24;

    [Header("Failure")]
    [SerializeField, Min(1f)] private float timeLimit = 45f;

    [Header("False Signal Wave")]
    [SerializeField, Min(0)] private int falseSignalEnemyCount = 5;
    [SerializeField, Min(0f)] private float minimumEnemyDistanceFromPlayer = 4f;
    [SerializeField, Min(0f)] private float minimumSpawnRadius = 3f;
    [SerializeField, Min(0f)] private float maximumSpawnRadius = 6f;
    [SerializeField] private EnemySpawner enemySpawner;

    [Header("Scene")]
    [SerializeField] private GameplayAreaService gameplayArea;

    private readonly List<FalseSignalPoint> signalPoints = new();
    private readonly Dictionary<FalseSignalPoint, WorldEventMarker>
        signalPointMarkers = new();
    private Collider2D startCollider;
    private LineRenderer startVisual;
    private float timeRemaining;
    private Vector3 rewardPosition;
    private bool hasRewardPosition;

    public override Vector3 RewardPosition => hasRewardPosition
        ? rewardPosition
        : base.RewardPosition;

    public override void Initialize(WorldEventSpawner spawner)
    {
        base.Initialize(spawner);
        ShowEventMarker(transform, "FALSE SIGNAL");
    }

    private void Awake()
    {
        startCollider = GetComponent<Collider2D>();
        BuildStartVisual();
    }

    private void Update()
    {
        if (!IsStarted || IsCompleted || Time.timeScale == 0f)
            return;

        timeRemaining -= Time.deltaTime;

        if (timeRemaining <= 0f)
            FailFalseSignal();
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
        timeRemaining = timeLimit;

        if (!SpawnSignalPoints())
        {
            FailFalseSignal();
            return;
        }

        RunMessageService.Instance?.ShowCustom(
            "НАЙДИТЕ НАСТОЯЩИЙ СИГНАЛ",
            "Проверьте сигнальные точки до истечения времени"
        );
    }

    public void ResolveSignal(FalseSignalPoint signalPoint, bool isReal)
    {
        if (!IsStarted || IsCompleted || signalPoint == null)
            return;

        signalPoints.Remove(signalPoint);
        RemoveSignalPointMarker(signalPoint);

        if (!isReal)
        {
            RunMessageService.Instance?.ShowCustom(
                "ЛОЖНЫЙ СИГНАЛ — ЗАСАДА",
                string.Empty
            );
            enemySpawner?.SpawnAdditionalWave(
                signalPoint.transform.position,
                falseSignalEnemyCount,
                minimumSpawnRadius,
                maximumSpawnRadius,
                minimumEnemyDistanceFromPlayer
            );
            return;
        }

        rewardPosition = signalPoint.transform.position;
        hasRewardPosition = true;
        RunMessageService.Instance?.ShowCustom(
            "СИГНАЛ ПОДТВЕРЖДЁН",
            string.Empty
        );
        CompleteEvent();
    }

    public void HandleSignalPointDestroyed(FalseSignalPoint signalPoint)
    {
        if (signalPoint == null)
            return;

        signalPoints.Remove(signalPoint);
        RemoveSignalPointMarker(signalPoint);
    }

    private bool SpawnSignalPoints()
    {
        if (signalPointPrefab == null || gameplayArea == null)
            return false;

        List<Vector3> positions = new();

        for (int i = 0; i < signalPointCount; i++)
        {
            if (!TryGetSignalPosition(positions, out Vector3 position))
                return false;

            positions.Add(position);
        }

        int realSignalIndex = Random.Range(0, positions.Count);

        for (int i = 0; i < positions.Count; i++)
        {
            FalseSignalPoint point = Instantiate(
                signalPointPrefab,
                positions[i],
                Quaternion.identity,
                transform
            );
            point.Initialize(this, i == realSignalIndex);
            signalPoints.Add(point);

            WorldEventMarker marker =
                HUDManager.Instance?.CreateWorldEventMarker(
                    point.transform,
                    "SIGNAL"
                );
            signalPointMarkers[point] = marker;
        }

        return true;
    }

    private bool TryGetSignalPosition(
        List<Vector3> existingPositions,
        out Vector3 position)
    {
        for (int i = 0; i < positionAttempts; i++)
        {
            if (!gameplayArea.TryGetSpawnPosition(
                    transform.position,
                    minPointDistance,
                    maxPointDistance,
                    1,
                    pointEdgePadding,
                    out Vector3 candidate))
            {
                continue;
            }

            bool separated = true;

            for (int j = 0; j < existingPositions.Count; j++)
            {
                if (Vector2.Distance(candidate, existingPositions[j]) <
                    minPointSeparation)
                {
                    separated = false;
                    break;
                }
            }

            if (!separated)
                continue;

            position = candidate;
            return true;
        }

        position = default;
        return false;
    }

    private void ResolveSceneReferences()
    {
        if (gameplayArea == null)
            gameplayArea = GameplayAreaService.Instance;

        if (enemySpawner == null)
            enemySpawner = FindFirstObjectByType<EnemySpawner>();
    }

    private void BuildStartVisual()
    {
        if (lineMaterial == null)
            return;

        const int Segments = 48;
        startVisual = gameObject.AddComponent<LineRenderer>();
        startVisual.sharedMaterial = lineMaterial;
        startVisual.useWorldSpace = false;
        startVisual.loop = true;
        startVisual.positionCount = Segments;
        startVisual.startWidth = 0.12f;
        startVisual.endWidth = 0.12f;
        startVisual.startColor = Color.magenta;
        startVisual.endColor = Color.magenta;
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

    private void FailFalseSignal()
    {
        FailEvent();
        Destroy(gameObject);
    }

    protected override void CleanupEvent()
    {
        CleanupSignalPointMarkers();

        for (int i = 0; i < signalPoints.Count; i++)
        {
            if (signalPoints[i] != null)
                Destroy(signalPoints[i].gameObject);
        }

        signalPoints.Clear();
    }

    private void RemoveSignalPointMarker(FalseSignalPoint signalPoint)
    {
        if (!signalPointMarkers.TryGetValue(
                signalPoint,
                out WorldEventMarker marker))
        {
            return;
        }

        HUDManager.Instance?.RemoveWorldEventMarker(marker);
        signalPointMarkers.Remove(signalPoint);
    }

    private void CleanupSignalPointMarkers()
    {
        foreach (WorldEventMarker marker in signalPointMarkers.Values)
            HUDManager.Instance?.RemoveWorldEventMarker(marker);

        signalPointMarkers.Clear();
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.magenta;
        Gizmos.DrawWireSphere(transform.position, startRadius);
    }
}
