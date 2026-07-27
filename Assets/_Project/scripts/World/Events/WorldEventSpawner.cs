using System.Collections.Generic;
using UnityEngine;

public class WorldEventSpawner : MonoBehaviour
{
    public event System.Action<WorldEvent> EventCompleted;
    public event System.Action<WorldEvent> EventFailed;
    public IReadOnlyList<WorldEvent> ActiveEvents => activeEventInstances;

    [Header("Event Prefabs")]
    [SerializeField] private WorldEvent[] eventPrefabs;
    [SerializeField] private WorldEventRewardChest rewardChestPrefab;
    [SerializeField] private DoubleOrLeave doubleOrLeave;
    [SerializeField, Min(1f)] private float riskDifficultyMultiplier = 1.5f;

    [Header("Spawn Timing")]
    [SerializeField] private float firstEventDelay = 45f;
    [SerializeField] private float eventInterval = 90f;

    [Header("Spawn Area")]
    [SerializeField] private float minDistanceFromPlayer = 8f;
    [SerializeField] private float maxDistanceFromPlayer = 14f;
    [SerializeField, Min(0f)] private float eventEdgePadding = 2f;
    [SerializeField, Min(1)] private int spawnPositionAttempts = 24;
    [SerializeField] private GameplayAreaService gameplayArea;

    [Header("Limits")]
    [SerializeField] private int maxActiveEvents = 1;

    private int nextEventIndex;

    private float timer;
    private int activeEvents;
    private bool holdPointEnabled;
    private readonly List<WorldEvent> activeEventInstances = new();

    private void OnEnable()
    {
        EventCompleted += SpawnRewardChest;
        EventFailed += HandleEventFailed;
    }

    private void OnDisable()
    {
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

        if (activeEvents >= maxActiveEvents)
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

        if (gameplayArea == null ||
            !gameplayArea.TryGetSpawnPosition(
                player.transform.position,
                minDistanceFromPlayer,
                maxDistanceFromPlayer,
                spawnPositionAttempts,
                eventEdgePadding,
                out Vector3 spawnPosition))
        {
            Debug.LogWarning(
                "[WorldEventSpawner] No valid position exists inside the spawn area.",
                this
            );
            return;
        }

        WorldEvent spawnedEvent = Instantiate(prefab, spawnPosition, Quaternion.identity);
        spawnedEvent.Initialize(this);

        if (doubleOrLeave != null &&
            doubleOrLeave.TryBeginRiskyEvent(spawnedEvent))
        {
            spawnedEvent.ApplyDifficultyMultiplier(riskDifficultyMultiplier);
        }

        activeEventInstances.Add(spawnedEvent);
        activeEvents++;
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
        activeEventInstances.Remove(worldEvent);
        activeEvents = Mathf.Max(0, activeEvents - 1);
        EventCompleted?.Invoke(worldEvent);
    }

    public void NotifyEventFailed(WorldEvent worldEvent)
    {
        activeEventInstances.Remove(worldEvent);
        activeEvents = Mathf.Max(0, activeEvents - 1);
        EventFailed?.Invoke(worldEvent);
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
            completedEvent.transform.position,
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
}
