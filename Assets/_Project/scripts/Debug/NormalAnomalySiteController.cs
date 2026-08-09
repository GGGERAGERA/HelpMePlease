using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
public sealed class NormalAnomalySiteController : MonoBehaviour
{
    private enum SiteState { Stopped, Active, Completed }

    private const float SiteRadius = 5f;
    private const float HoldSeconds = 9f;
    private const int EnemyTarget = 18;

    private readonly HashSet<EnemyHealth> trialEnemies = new();
    private EnemySpawner enemySpawner;
    private WorldEventSpawner eventSpawner;
    private LevelAnomalyController anomalyController;
    private PowerTestController powerTest;
    private LocalAnomalyData stasisData;
    private CaptureZoneEvent capturePrefab;
    private GameObject[] enemyPrefabs;
    private CaptureZoneEvent activeEvent;
    private LocalAnomalyZone activeZone;
    private Transform player;
    private bool resetPlayerWhenAvailable;
    private Vector3 sitePosition;
    private bool explorationMode;
    private bool showStandaloneHud = true;
    private string siteLabel = "NORMAL STASIS SITE";
    private SiteState state = SiteState.Stopped;

    public bool IsCompleted => state == SiteState.Completed;
    public bool IsActive => state == SiteState.Active;
    public Vector3 SitePosition => sitePosition;

    public void Configure(
        EnemySpawner spawner,
        WorldEventSpawner events,
        LevelAnomalyController anomalies,
        PowerTestController test,
        LocalAnomalyData stasis,
        CaptureZoneEvent capture,
        GameObject[] prefabs)
    {
        enemySpawner = spawner;
        eventSpawner = events;
        anomalyController = anomalies;
        powerTest = test;
        stasisData = stasis;
        capturePrefab = capture;
        enemyPrefabs = prefabs ?? System.Array.Empty<GameObject>();
        eventSpawner.SetHoldPointEnabled(true);
        eventSpawner.EventCompleted += HandleEventCompleted;
        eventSpawner.EventFailed += HandleEventFailed;
        StopSite();
    }

    public void ConfigureExploration(
        Vector2 position,
        LocalAnomalyData anomaly,
        string label)
    {
        sitePosition = position;
        stasisData = anomaly;
        siteLabel = string.IsNullOrWhiteSpace(label)
            ? "NORMAL SITE"
            : label;
        explorationMode = true;
        showStandaloneHud = false;
        resetPlayerWhenAvailable = false;
    }

    public void StartOrResetSite()
    {
        powerTest?.StopTest();
        ClearEvent();
        ClearZone();
        ClearEnemies();
        if (!explorationMode)
            ClearRewardChests();
        resetPlayerWhenAvailable = !explorationMode;
        if (resetPlayerWhenAvailable)
            ResolvePlayer();
        SpawnZone();
        CreateEvent();
        state = SiteState.Active;
    }

    public void StopSite()
    {
        ClearEvent();
        ClearZone();
        ClearEnemies();
        if (!explorationMode)
            ClearRewardChests();
        resetPlayerWhenAvailable = false;
        state = SiteState.Stopped;
    }

    private void Update()
    {
        ResolvePlayer();
        trialEnemies.RemoveWhere(enemy => enemy == null || enemy.IsDead);
    }

    private void SpawnZone()
    {
        if (stasisData == null || stasisData.ZonePrefab == null)
            return;
        activeZone = Instantiate(
            stasisData.ZonePrefab,
            sitePosition,
            Quaternion.identity
        );
        activeZone.name = siteLabel;
        activeZone.Initialize(
            stasisData,
            anomalyController,
            Vector2.one * SiteRadius * 2f
        );
    }

    private void CreateEvent()
    {
        if (capturePrefab == null || eventSpawner == null)
            return;
        bool spawnedSuccessfully = explorationMode
            ? eventSpawner.SpawnConcurrentDebugEventAt(
                capturePrefab,
                sitePosition,
                false,
                out WorldEvent spawned)
            : eventSpawner.SpawnDebugEventAt(
                capturePrefab,
                sitePosition,
                false,
                out spawned);
        if (!spawnedSuccessfully)
        {
            return;
        }
        activeEvent = spawned as CaptureZoneEvent;
        if (activeEvent == null)
            return;
        activeEvent.ConfigureDebugHoldTime(HoldSeconds);
        if (!explorationMode)
            EnsureEnemies(EnemyTarget);
    }

    private void HandleEventCompleted(WorldEvent worldEvent)
    {
        if (activeEvent == null || worldEvent != activeEvent)
            return;
        activeEvent = null;
        ClearZone();
        state = SiteState.Completed;
    }

    private void HandleEventFailed(WorldEvent worldEvent)
    {
        if (activeEvent == null || worldEvent != activeEvent)
            return;
        activeEvent = null;
        ClearEnemies();
        CreateEvent();
    }

    private void EnsureEnemies(int target)
    {
        if (enemySpawner == null || enemyPrefabs.Length == 0)
            return;
        int attempts = Mathf.Max(0, target - trialEnemies.Count) * 3;
        for (int i = 0; i < attempts && trialEnemies.Count < target; i++)
        {
            GameObject instance = enemySpawner.SpawnSpecificEnemyAround(
                enemyPrefabs[i % enemyPrefabs.Length],
                sitePosition,
                1f,
                SiteRadius - 0.3f,
                0.75f,
                true,
                0.1f
            );
            EnemyHealth health = instance != null
                ? instance.GetComponent<EnemyHealth>()
                : null;
            if (health != null)
                trialEnemies.Add(health);
        }
    }

    private void ResolvePlayer()
    {
        if (player == null)
            player = GameObject.FindGameObjectWithTag("Player")?.transform;
        if (player == null || !resetPlayerWhenAvailable)
            return;
        Vector2 start = new(-8f, 0f);
        Rigidbody2D body = player.GetComponent<Rigidbody2D>();
        if (body != null)
            body.position = start;
        player.position = start;
        resetPlayerWhenAvailable = false;
    }

    private void ClearEvent()
    {
        if (activeEvent != null && eventSpawner != null)
            eventSpawner.ClearDebugEvent(activeEvent);
        activeEvent = null;
    }

    private void ClearZone()
    {
        if (activeZone != null)
            activeZone.Despawn();
        activeZone = null;
    }

    private void ClearEnemies()
    {
        foreach (EnemyHealth enemy in trialEnemies)
        {
            if (enemy != null)
                Destroy(enemy.gameObject);
        }
        trialEnemies.Clear();
    }

    private static void ClearRewardChests()
    {
        WorldEventRewardChest[] chests =
            FindObjectsByType<WorldEventRewardChest>(FindObjectsSortMode.None);
        for (int i = 0; i < chests.Length; i++)
        {
            if (chests[i] != null)
                Destroy(chests[i].gameObject);
        }
    }

    private void OnGUI()
    {
        if (!showStandaloneHud || state == SiteState.Stopped)
            return;
        string text = siteLabel + "\n" +
            $"Site: {state}\nAnomaly: {(state == SiteState.Active ? "ACTIVE" : "OFF")}\n" +
            $"Current Event: {(activeEvent != null ? "Hold Zone" : "None")}\n" +
            "Reward: STANDARD UPGRADE CHEST\n" +
            $"Enemies Alive: {EnemyHealth.ActiveInstances.Count}";
        GUI.Box(new Rect(14f, Screen.height - 165f, 330f, 150f), text);
    }

    private void OnDestroy()
    {
        if (eventSpawner != null)
        {
            eventSpawner.EventCompleted -= HandleEventCompleted;
            eventSpawner.EventFailed -= HandleEventFailed;
        }
        ClearEvent();
        ClearZone();
        ClearEnemies();
    }
}
#endif
