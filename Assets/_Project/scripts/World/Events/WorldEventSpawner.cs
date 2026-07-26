using System.Collections.Generic;
using UnityEngine;

public class WorldEventSpawner : MonoBehaviour
{
    public event System.Action<WorldEvent> EventCompleted;
    public IReadOnlyList<WorldEvent> ActiveEvents => activeEventInstances;

    [Header("Event Prefabs")]
    [SerializeField] private WorldEvent[] eventPrefabs;

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

    private void Start()
    {
        ResolveGameplayArea();
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

    private void ResolveGameplayArea()
    {
        if (gameplayArea == null)
            gameplayArea = GameplayAreaService.Instance;

        if (gameplayArea == null)
            gameplayArea = FindFirstObjectByType<GameplayAreaService>();
    }
}
