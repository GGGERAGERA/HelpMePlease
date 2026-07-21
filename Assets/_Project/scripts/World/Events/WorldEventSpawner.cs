using UnityEngine;

public class WorldEventSpawner : MonoBehaviour
{
    public event System.Action<WorldEvent> EventCompleted;

    [Header("Event Prefabs")]
    [SerializeField] private WorldEvent[] eventPrefabs;

    [Header("Spawn Timing")]
    [SerializeField] private float firstEventDelay = 45f;
    [SerializeField] private float eventInterval = 90f;

    [Header("Spawn Area")]
    [SerializeField] private float minDistanceFromPlayer = 8f;
    [SerializeField] private float maxDistanceFromPlayer = 14f;

    [Header("Limits")]
    [SerializeField] private int maxActiveEvents = 1;

    private int nextEventIndex;

    private float timer;
    private int activeEvents;
    private bool holdPointEnabled;

    private void Start()
    {
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

        Vector2 direction = Random.insideUnitCircle.normalized;
        float distance = Random.Range(minDistanceFromPlayer, maxDistanceFromPlayer);

        Vector3 spawnPosition = player.transform.position + (Vector3)(direction * distance);

        WorldEvent spawnedEvent = Instantiate(prefab, spawnPosition, Quaternion.identity);
        spawnedEvent.Initialize(this);

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
        activeEvents = Mathf.Max(0, activeEvents - 1);
        EventCompleted?.Invoke(worldEvent);
    }
}
