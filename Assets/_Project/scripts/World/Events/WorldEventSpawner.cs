using System.Collections.Generic;
using UnityEngine;

public class WorldEventSpawner : MonoBehaviour
{
    public event System.Action<WorldEvent> EventCompleted;
    public event System.Action<WorldEvent> EventFailed;
    public IReadOnlyList<WorldEvent> SpawnedEvents => spawnedEvents;
    public WorldEvent ActiveEvent { get; private set; }

    [Header("Event Prefabs")]
    [SerializeField] private WorldEvent[] eventPrefabs;
    [SerializeField] private WorldEventRewardChest rewardChestPrefab;
    [SerializeField] private DoubleOrLeave doubleOrLeave;
    [SerializeField, Min(1f)] private float riskDifficultyMultiplier = 1.5f;

    [Header("Spawn Pressure")]
    [SerializeField, Min(1f)] private float standardEventPressure = 1.15f;
    [SerializeField, Min(1f)] private float riskEventPressure = 1.35f;

    [Header("Spawn Timing")]
    [SerializeField] private float firstEventDelay = 45f;
    [SerializeField] private float eventInterval = 90f;

    [Header("Spawn Area")]
    [SerializeField] private float minDistanceFromPlayer = 8f;
    [SerializeField] private float maxDistanceFromPlayer = 14f;
    [SerializeField, Min(0f)] private float eventEdgePadding = 2f;
    [SerializeField, Min(1)] private int spawnPositionAttempts = 24;
    [SerializeField, Range(0f, 1f)]
    private float eventInsideAnomalyChance = 0.35f;
    [SerializeField] private GameplayAreaService gameplayArea;

    [Header("Limits")]
    [SerializeField] private int maxActiveEvents = 1;

    private int nextEventIndex;

    private float timer;
    private int spawnedEventCount;
    private bool holdPointEnabled;
    private EnemySpawner enemySpawner;
    private WorldEvent pressureEvent;
    private readonly List<WorldEvent> spawnedEvents = new();
    private readonly List<LevelAnomalyController.LocalAnomalyZoneGeometry>
        localAnomalyZones = new();

    private void OnEnable()
    {
        EventCompleted += SpawnRewardChest;
        EventFailed += HandleEventFailed;
    }

    private void OnDisable()
    {
        ClearEventSpawnPressure();
        EventCompleted -= SpawnRewardChest;
        EventFailed -= HandleEventFailed;
    }

    private void Start()
    {
        ResolveGameplayArea();
        ResolveDoubleOrLeave();
        timer = firstEventDelay;
    }

    private void Update()
    {
        if (Time.timeScale == 0f)
            return;

        if (eventPrefabs == null || eventPrefabs.Length == 0)
            return;

        if (spawnedEventCount >= maxActiveEvents)
            return;

        timer -= Time.deltaTime;

        if (timer <= 0f)
        {
            SpawnRandomEvent();
            timer = eventInterval;
        }
    }

    private void SpawnRandomEvent()
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");

        if (player == null)
            return;

        WorldEvent prefab = GetNextEventPrefab();

        if (prefab == null)
            return;

        if (gameplayArea == null)
            ResolveGameplayArea();

        if (gameplayArea == null)
        {
            Debug.LogWarning(
                "[WorldEventSpawner] No valid position exists inside the spawn area.",
                this
            );
            return;
        }

        bool anomalyPlacementRequested =
            Random.value < eventInsideAnomalyChance;
        bool placedInsideAnomaly = false;
        bool usedFallback = false;
        LocalAnomalyType? selectedAnomalyType = null;
        Vector3 spawnPosition = default;

        if (anomalyPlacementRequested)
        {
            placedInsideAnomaly =
                TryGetPositionInsideLocalAnomaly(
                    prefab,
                    player.transform.position,
                    out spawnPosition,
                    out selectedAnomalyType
                );
            usedFallback = !placedInsideAnomaly;
        }

        if (!placedInsideAnomaly &&
            !gameplayArea.TryGetSpawnPosition(
                player.transform.position,
                minDistanceFromPlayer,
                maxDistanceFromPlayer,
                spawnPositionAttempts,
                eventEdgePadding,
                out spawnPosition))
        {
            Debug.LogWarning(
                "[WorldEventSpawner] No valid position exists inside the spawn area.",
                this
            );
            return;
        }

        WorldEvent spawnedEvent = Instantiate(prefab, spawnPosition, Quaternion.identity);
        spawnedEvent.Initialize(this);

        spawnedEvents.Add(spawnedEvent);
        spawnedEventCount++;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        string anomalyType = selectedAnomalyType.HasValue
            ? selectedAnomalyType.Value.ToString()
            : "None";
        Debug.Log(
            $"[WorldEventSpawner] Spawned '{prefab.name}': " +
            $"insideAnomaly={placedInsideAnomaly}, " +
            $"anomalyType={anomalyType}, fallback={usedFallback}.",
            this
        );
#endif
    }

    private bool TryGetPositionInsideLocalAnomaly(
        WorldEvent prefab,
        Vector3 playerPosition,
        out Vector3 position,
        out LocalAnomalyType? selectedAnomalyType)
    {
        position = default;
        selectedAnomalyType = null;

        LevelAnomalyController anomalyController =
            LevelAnomalyController.Instance;

        if (anomalyController == null)
            return false;

        anomalyController.CollectActiveLocalZones(localAnomalyZones);

        if (localAnomalyZones.Count == 0)
            return false;

        LevelAnomalyController.LocalAnomalyZoneGeometry zone =
            localAnomalyZones[
                Random.Range(0, localAnomalyZones.Count)
            ];
        selectedAnomalyType = zone.Type;

        float eventRadius = GetEventFootprintRadius(prefab);
        float availableRadius = zone.Radius - eventRadius;

        if (availableRadius < 0f)
            return false;

        int attempts = Mathf.Max(1, spawnPositionAttempts);
        float minimumPlayerDistance =
            Mathf.Max(0f, minDistanceFromPlayer);
        float minimumPlayerDistanceSquared =
            minimumPlayerDistance * minimumPlayerDistance;
        float playablePadding = Mathf.Max(
            eventRadius,
            eventEdgePadding
        );

        for (int i = 0; i < attempts; i++)
        {
            Vector2 candidate =
                zone.Center +
                Random.insideUnitCircle * availableRadius;

            if (((Vector2)playerPosition - candidate).sqrMagnitude <
                minimumPlayerDistanceSquared)
            {
                continue;
            }

            if (!gameplayArea.IsInsidePlayableArea(
                    candidate,
                    eventRadius) ||
                !gameplayArea.IsInsideSpawnArea(
                    candidate,
                    playablePadding) ||
                OverlapsActiveEvent(candidate, eventRadius))
            {
                continue;
            }

            position = new Vector3(
                candidate.x,
                candidate.y,
                playerPosition.z
            );
            return true;
        }

        return false;
    }

    private bool OverlapsActiveEvent(
        Vector2 candidate,
        float eventRadius)
    {
        for (int i = 0; i < spawnedEvents.Count; i++)
        {
            WorldEvent activeEvent = spawnedEvents[i];

            if (activeEvent == null)
                continue;

            float requiredDistance =
                eventRadius +
                GetEventFootprintRadius(activeEvent);

            if (Vector2.Distance(
                    candidate,
                    activeEvent.transform.position) <
                requiredDistance)
            {
                return true;
            }
        }

        return false;
    }

    private float GetEventFootprintRadius(WorldEvent worldEvent)
    {
        if (worldEvent == null)
            return Mathf.Max(0.1f, eventEdgePadding);

        CircleCollider2D[] colliders =
            worldEvent.GetComponentsInChildren<CircleCollider2D>(
                true
            );
        Vector2 eventCenter = worldEvent.transform.position;
        float radius = 0f;

        for (int i = 0; i < colliders.Length; i++)
        {
            CircleCollider2D collider = colliders[i];

            if (collider == null)
                continue;

            Vector2 colliderCenter =
                collider.transform.TransformPoint(collider.offset);
            Vector3 scale = collider.transform.lossyScale;
            float colliderRadius = collider.radius * Mathf.Max(
                Mathf.Abs(scale.x),
                Mathf.Abs(scale.y)
            );
            radius = Mathf.Max(
                radius,
                Vector2.Distance(eventCenter, colliderCenter) +
                colliderRadius
            );
        }

        return radius > Mathf.Epsilon
            ? radius
            : Mathf.Max(0.1f, eventEdgePadding);
    }

    public void SetHoldPointEnabled(bool enabled)
    {
        holdPointEnabled = enabled;
    }

    private WorldEvent GetNextEventPrefab()
    {
        for (int i = 0; i < eventPrefabs.Length; i++)
        {
            WorldEvent prefab = eventPrefabs[nextEventIndex];

            nextEventIndex++;
            if (nextEventIndex >= eventPrefabs.Length)
                nextEventIndex = 0;

            if (prefab is CaptureZoneEvent && !holdPointEnabled)
                continue;

            return prefab;
        }

        return null;
    }

    public void NotifyEventCompleted(WorldEvent worldEvent)
    {
        ClearEventSpawnPressure(worldEvent);

        if (ActiveEvent == worldEvent)
            ActiveEvent = null;

        spawnedEvents.Remove(worldEvent);
        spawnedEventCount = Mathf.Max(0, spawnedEventCount - 1);
        EventCompleted?.Invoke(worldEvent);
    }

    public void NotifyEventFailed(WorldEvent worldEvent)
    {
        ClearEventSpawnPressure(worldEvent);

        if (ActiveEvent == worldEvent)
            ActiveEvent = null;

        spawnedEvents.Remove(worldEvent);
        spawnedEventCount = Mathf.Max(0, spawnedEventCount - 1);
        EventFailed?.Invoke(worldEvent);
    }

    public bool CanStartEvent(WorldEvent worldEvent)
    {
        return worldEvent != null &&
            ActiveEvent == null &&
            spawnedEvents.Contains(worldEvent) &&
            !worldEvent.IsStarting &&
            !worldEvent.IsStarted &&
            !worldEvent.IsCompleted;
    }

    public bool TryStartEvent(WorldEvent worldEvent)
    {
        if (!CanStartEvent(worldEvent))
            return false;

        ActiveEvent = worldEvent;
        return true;
    }

    public void NotifyEventStarted(WorldEvent worldEvent, bool riskMode)
    {
        if (!isActiveAndEnabled ||
            worldEvent == null || ActiveEvent != worldEvent)
        {
            return;
        }

        ResolveEnemySpawner();
        pressureEvent = worldEvent;
        enemySpawner?.SetWorldEventSpawnPressureMultiplier(
            riskMode ? riskEventPressure : standardEventPressure
        );
    }

    public bool TryChooseAndStartEvent(WorldEvent worldEvent)
    {
        if (!CanStartEvent(worldEvent))
            return false;

        ResolveDoubleOrLeave();

        if (doubleOrLeave == null)
        {
            worldEvent.StartSelectedEvent(false);
            return true;
        }

        return doubleOrLeave.BeginEventChoice(
            worldEvent,
            risk =>
            {
                if (risk)
                worldEvent.ApplyDifficultyMultiplier(riskDifficultyMultiplier);

                worldEvent.StartSelectedEvent(risk);
            }
        );
    }

    private void SpawnRewardChest(WorldEvent completedEvent)
    {
        if (completedEvent == null)
            return;

        bool isImproved = doubleOrLeave != null &&
            doubleOrLeave.ResolveCompletedEvent(completedEvent);

        if (rewardChestPrefab == null)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogWarning(
                "[WorldEventSpawner] Reward chest prefab is not assigned."
            );
#endif
            return;
        }

        WorldEventRewardChest chest = Instantiate(
            rewardChestPrefab,
            completedEvent.RewardPosition,
            Quaternion.identity
        );
        chest.Initialize(isImproved, doubleOrLeave);
    }

    private void HandleEventFailed(WorldEvent failedEvent)
    {
        doubleOrLeave?.ResolveFailedEvent(failedEvent);
    }

    private void ResolveGameplayArea()
    {
        if (gameplayArea == null)
            gameplayArea = GameplayAreaService.Instance;

        if (gameplayArea == null)
            gameplayArea = FindFirstObjectByType<GameplayAreaService>();
    }

    private void ResolveDoubleOrLeave()
    {
        if (doubleOrLeave == null)
            doubleOrLeave = FindFirstObjectByType<DoubleOrLeave>();
    }

    private void ClearEventSpawnPressure(WorldEvent worldEvent = null)
    {
        if (worldEvent != null && pressureEvent != worldEvent)
            return;

        enemySpawner?.SetWorldEventSpawnPressureMultiplier(1f);
        pressureEvent = null;
    }

    private void ResolveEnemySpawner()
    {
        if (enemySpawner == null || !enemySpawner.gameObject.scene.IsValid())
            enemySpawner = FindFirstObjectByType<EnemySpawner>();
    }
}
