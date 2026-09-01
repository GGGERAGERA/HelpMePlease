using System.Collections.Generic;
using UnityEngine;

public class WorldEventSpawner : MonoBehaviour
{
    private const WorldEventDifficulty ProductionEventDifficulty =
        WorldEventDifficulty.Standard;

    public event System.Action<WorldEvent> EventCompleted;
    public event System.Action<WorldEvent> EventFailed;
    public IReadOnlyList<WorldEvent> SpawnedEvents => spawnedEvents;
    public IReadOnlyList<WorldEvent> EventPrefabs => eventPrefabs;
    public WorldEvent ActiveEvent { get; private set; }
    public WorldEvent CurrentEvent
    {
        get
        {
            if (ActiveEvent != null)
                return ActiveEvent;

            for (int i = 0; i < spawnedEvents.Count; i++)
            {
                if (spawnedEvents[i] != null)
                    return spawnedEvents[i];
            }

            return null;
        }
    }

    [Header("Event Prefabs")]
    [SerializeField] private WorldEvent[] eventPrefabs;
    [SerializeField] private WorldBreakable eventRewardContainerPrefab;
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
    private readonly HashSet<WorldEvent> warnedUnsupportedEventPrefabs = new();
    private readonly HashSet<WorldEvent> siteRewardSuppressedEvents = new();
    private bool siteControlledMode;
    private readonly List<LevelAnomalyController.LocalAnomalyZoneGeometry>
        localAnomalyZones = new();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
    public void ConfigureDebugEventPrefabs(WorldEvent[] prefabs)
    {
        eventPrefabs = prefabs ?? System.Array.Empty<WorldEvent>();
        debugManualOnly = true;
    }

    public void ConfigureDebugRewardContainer(WorldBreakable prefab)
    {
        eventRewardContainerPrefab = prefab;
    }

    public void ConfigureDebugConcurrentEventCapacity(int capacity)
    {
        maxActiveEvents = Mathf.Max(1, capacity);
    }

    private WorldEvent debugEvent;
    private bool debugManualOnly;
    private readonly HashSet<WorldEvent> debugRewardSuppressedEvents =
        new();
#endif

    private void OnEnable()
    {
        EventCompleted += SpawnRewardContainer;
        EventFailed += HandleEventFailed;
    }

    private void OnDisable()
    {
        ClearEventSpawnPressure();
        siteRewardSuppressedEvents.Clear();
        EventCompleted -= SpawnRewardContainer;
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
        if (siteControlledMode)
            return;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (debugManualOnly)
            return;
#endif

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

        TrySpawnEvent(prefab, player, out _);
    }

    private bool TrySpawnEvent(
        WorldEvent prefab,
        GameObject player,
        out WorldEvent spawnedEvent)
    {
        spawnedEvent = null;

        if (prefab == null || player == null)
            return false;

        if (gameplayArea == null)
            ResolveGameplayArea();

        if (gameplayArea == null)
        {
            Debug.LogWarning(
                "[WorldEventSpawner] No valid position exists inside the spawn area.",
                this
            );
            return false;
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
            return false;
        }

        spawnedEvent = Instantiate(prefab, spawnPosition, Quaternion.identity);
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
        return true;
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
        Vector2 availableHalfSize = zone.Size * 0.5f -
            Vector2.one * eventRadius;

        if (availableHalfSize.x < 0f || availableHalfSize.y < 0f)
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
            Vector2 candidate = zone.Center + new Vector2(
                Random.Range(-availableHalfSize.x, availableHalfSize.x),
                Random.Range(-availableHalfSize.y, availableHalfSize.y)
            );

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

    public float GetSiteEventFootprintRadius(WorldEvent worldEvent)
    {
        return GetEventFootprintRadius(worldEvent);
    }

    public bool IsSiteEventPositionClear(
        WorldEvent prefab,
        Vector2 position,
        float extraClearance = 0f)
    {
        float radius = GetEventFootprintRadius(prefab) +
            Mathf.Max(0f, extraClearance);
        return !OverlapsActiveEvent(position, radius);
    }

    public void SetHoldPointEnabled(bool enabled)
    {
        holdPointEnabled = enabled;

        if (enabled)
            warnedUnsupportedEventPrefabs.Clear();
    }

    public void ConfigureSiteControlledMode(int concurrentSiteCount)
    {
        siteControlledMode = true;
        holdPointEnabled = true;
        maxActiveEvents = Mathf.Max(1, concurrentSiteCount);
        timer = eventInterval;
    }

    public bool SpawnSiteEventAt(
        WorldEvent prefab,
        Vector3 position,
        Vector2 siteCenter,
        Vector2 siteSize,
        bool suppressStandardReward,
        out WorldEvent spawnedEvent)
    {
        spawnedEvent = null;

        if (!isActiveAndEnabled || prefab == null ||
            !IsEventPrefabEnabled(prefab) ||
            spawnedEventCount >= maxActiveEvents)
        {
            return false;
        }

        spawnedEvent = Instantiate(prefab, position, Quaternion.identity);
        spawnedEvent.ConfigureSitePlacement(siteCenter, siteSize);
        spawnedEvent.Initialize(this);
        spawnedEvents.Add(spawnedEvent);
        spawnedEventCount++;

        if (suppressStandardReward)
            siteRewardSuppressedEvents.Add(spawnedEvent);

        return true;
    }

    public bool IsEventPrefabEnabled(WorldEvent eventPrefab)
    {
        if (eventPrefab == null || eventPrefabs == null)
            return false;

        for (int i = 0; i < eventPrefabs.Length; i++)
        {
            if (eventPrefabs[i] != eventPrefab)
                continue;

            return SupportsEventCapabilities(eventPrefab);
        }

        return false;
    }

    private WorldEvent GetNextEventPrefab()
    {
        for (int i = 0; i < eventPrefabs.Length; i++)
        {
            WorldEvent prefab = eventPrefabs[nextEventIndex];

            nextEventIndex++;
            if (nextEventIndex >= eventPrefabs.Length)
                nextEventIndex = 0;

            if (!SupportsEventCapabilities(prefab))
                continue;

            return prefab;
        }

        return null;
    }

    private bool SupportsEventCapabilities(WorldEvent eventPrefab)
    {
        if (eventPrefab == null)
            return false;

        if (!eventPrefab.RequiresHoldPointFeature || holdPointEnabled)
            return true;

        if (warnedUnsupportedEventPrefabs.Add(eventPrefab))
        {
            Debug.LogWarning(
                $"[WorldEventSpawner] Event '{eventPrefab.name}' requires " +
                "the hold-point feature, but it is not enabled. " +
                "The event will be skipped.",
                this
            );
        }

        return false;
    }

    public void NotifyEventCompleted(WorldEvent worldEvent)
    {
        ClearEventSpawnPressure(worldEvent);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (debugEvent == worldEvent)
            debugEvent = null;

#endif

        if (ActiveEvent == worldEvent)
            ActiveEvent = null;

        spawnedEvents.Remove(worldEvent);
        spawnedEventCount = Mathf.Max(0, spawnedEventCount - 1);
        EventCompleted?.Invoke(worldEvent);
    }

    public void NotifyEventFailed(WorldEvent worldEvent)
    {
        ClearEventSpawnPressure(worldEvent);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (debugEvent == worldEvent)
            debugEvent = null;

        debugRewardSuppressedEvents.Remove(worldEvent);
#endif

        if (ActiveEvent == worldEvent)
            ActiveEvent = null;

        spawnedEvents.Remove(worldEvent);
        siteRewardSuppressedEvents.Remove(worldEvent);
        spawnedEventCount = Mathf.Max(0, spawnedEventCount - 1);
        EventFailed?.Invoke(worldEvent);
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    public bool SpawnDebugEventAt(
        WorldEvent prefab,
        Vector3 position,
        bool suppressReward,
        out WorldEvent spawnedEvent)
    {
        spawnedEvent = null;

        if (!isActiveAndEnabled || prefab == null ||
            !IsEventPrefabEnabled(prefab))
        {
            return false;
        }

        if (debugEvent != null)
            ClearDebugEvent(debugEvent);

        if (!SpawnDebugEventAtInternal(
                prefab,
                position,
                suppressReward,
                out spawnedEvent))
        {
            return false;
        }

        debugEvent = spawnedEvent;
        return true;
    }

    public bool SpawnConcurrentDebugEventAt(
        WorldEvent prefab,
        Vector3 position,
        bool suppressReward,
        out WorldEvent spawnedEvent)
    {
        return SpawnDebugEventAtInternal(
            prefab,
            position,
            suppressReward,
            out spawnedEvent
        );
    }

    private bool SpawnDebugEventAtInternal(
        WorldEvent prefab,
        Vector3 position,
        bool suppressReward,
        out WorldEvent spawnedEvent)
    {
        spawnedEvent = null;

        if (!isActiveAndEnabled || prefab == null ||
            !IsEventPrefabEnabled(prefab))
        {
            return false;
        }

        if (spawnedEventCount >= maxActiveEvents)
            return false;

        spawnedEvent = Instantiate(prefab, position, Quaternion.identity);
        spawnedEvent.Initialize(this);
        spawnedEvents.Add(spawnedEvent);
        spawnedEventCount++;

        if (suppressReward)
            debugRewardSuppressedEvents.Add(spawnedEvent);

        timer = eventInterval;
        return true;
    }

    public bool SpawnDebugEvent(WorldEvent prefab)
    {
        if (!isActiveAndEnabled || prefab == null ||
            !IsEventPrefabEnabled(prefab))
        {
            return false;
        }

        if (debugEvent != null)
            ClearDebugEvent(debugEvent);

        if (spawnedEventCount >= maxActiveEvents)
            return false;

        GameObject player = GameObject.FindGameObjectWithTag("Player");

        if (!TrySpawnEvent(prefab, player, out WorldEvent spawnedEvent))
            return false;

        debugEvent = spawnedEvent;
        timer = eventInterval;
        return true;
    }

    public bool ClearDebugEvent()
    {
        return ClearDebugEvent(CurrentEvent);
    }

    public bool ClearDebugEvent(WorldEvent worldEvent)
    {
        if (worldEvent == null)
            return false;

        ClearEventSpawnPressure(worldEvent);

        if (ActiveEvent == worldEvent)
            ActiveEvent = null;

        if (debugEvent == worldEvent)
            debugEvent = null;

        debugRewardSuppressedEvents.Remove(worldEvent);

        spawnedEvents.Remove(worldEvent);
        spawnedEventCount = Mathf.Max(0, spawnedEventCount - 1);

        ResolveDoubleOrLeave();
        doubleOrLeave?.ResetState();
        worldEvent.ClearForDebug();

        // A debug event pauses the regular countdown. Restarting it here avoids
        // an immediate automatic spawn caused by previously accumulated time.
        timer = eventInterval;
        return true;
    }

    public void ClearAllDebugEvents()
    {
        for (int i = spawnedEvents.Count - 1; i >= 0; i--)
        {
            WorldEvent worldEvent = spawnedEvents[i];
            if (worldEvent != null)
                ClearDebugEvent(worldEvent);
        }
    }
#endif

    public bool CanStartEvent(WorldEvent worldEvent)
    {
        return worldEvent != null &&
            ActiveEvent == null &&
            spawnedEvents.Contains(worldEvent) &&
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

    public bool TryStartProductionEvent(WorldEvent worldEvent)
    {
        return TryStartEvent(worldEvent, ProductionEventDifficulty);
    }

    public bool TryStartEvent(
        WorldEvent worldEvent,
        WorldEventDifficulty difficulty)
    {
        if (!CanStartEvent(worldEvent))
            return false;

        bool riskMode = difficulty == WorldEventDifficulty.Risk;

        if (riskMode)
            worldEvent.ApplyDifficultyMultiplier(riskDifficultyMultiplier);

        ResolveDoubleOrLeave();
        doubleOrLeave?.TrackStartedEvent(worldEvent, difficulty);
        worldEvent.StartEvent(difficulty);
        return worldEvent.IsStarted;
    }

    private void SpawnRewardContainer(WorldEvent completedEvent)
    {
        if (completedEvent == null)
            return;

        bool isImproved = doubleOrLeave != null &&
            doubleOrLeave.ResolveCompletedEvent(completedEvent);

        if (siteRewardSuppressedEvents.Remove(completedEvent))
            return;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (debugRewardSuppressedEvents.Remove(completedEvent))
            return;
#endif

        if (eventRewardContainerPrefab == null)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogWarning(
                "[WorldEventSpawner] Event reward container is not assigned."
            );
#endif
            return;
        }

        WorldBreakable container = Instantiate(
            eventRewardContainerPrefab,
            completedEvent.RewardPosition,
            Quaternion.identity
        );
        container.InitializeEventReward(
            isImproved,
            numericOnly: siteControlledMode
        );
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
