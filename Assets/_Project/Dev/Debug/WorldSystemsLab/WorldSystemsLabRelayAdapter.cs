#if UNITY_EDITOR || DEVELOPMENT_BUILD
using Subject42.Combat.OrbitalStation;
using UnityEngine;

// Scene bootstrap only. All phases, contact, score, pressure leases and rewards belong to production.
public sealed class WorldSystemsLabRelayAdapter : MonoBehaviour
{
    [SerializeField] private GameObject supportPrefab;
    private GameObject support, pressureHost;
    private RunStateManager ownedRun;
    private CurrencyManager ownedCurrency;
    private OrbitalStationRuntime ownedStation;
    private WorldEventSpawner events;
    private OrbitalCenterShift centerShift;
    public OrbitalStationRuntime Station { get; private set; }
    public EnemySpawner Enemies { get; private set; }
    public bool EnableRewards { get; set; }
    public void ConfigureSupport(GameObject prefab) => supportPrefab = prefab;

    public bool Prepare(Transform player, CharacterData character, WorldEventSpawner events, GameObject debugEnemyPrefab, bool enableEnemyPressure)
    {
        Clear();
        if (player == null || character == null || events == null || supportPrefab == null) return false;
        var run = RunStateManager.Instance;
        if (run != null && !run.IsDevelopmentRun && run.TryGetOrbitalRunState(out _, out _))
        { Debug.LogWarning("[WorldSystemsLab] Exit the production run before preparing a relay preview.", this); return false; }
        bool ownsRun = run == null;
        run = RunStateManager.EnsureExists();
        if (ownsRun) ownedRun = run;
        if (!run.TryGetOrbitalRunState(out _, out _)) run.DebugResetOrbitalRunState();
        bool ownsStation = player.GetComponentInChildren<OrbitalStationRuntime>(true) == null;
        Station = OrbitalStationRuntime.Ensure(player.gameObject, character);
        Station?.Interaction?.BindCursor(UnityEngine.Object.FindFirstObjectByType<UICrosshairFollowMouse>(), Camera.main);
        if (ownsStation) ownedStation = Station;
        if (Station == null || !Station.IsInitialized) { Clear(); return false; }
        centerShift = Station.GetComponent<OrbitalCenterShift>();
        if (support == null) support = Instantiate(supportPrefab);
        else support.SetActive(true);
        var reward = support.GetComponentInChildren<UpgradeManager>(true);
        reward.gameObject.SetActive(EnableRewards);
        if (EnableRewards)
        {
            if (CurrencyManager.Instance == null) ownedCurrency = new GameObject("Lab currency owner").AddComponent<CurrencyManager>();
            reward.BindOrbitalStation(Station);
        }
        this.events = events;
        pressureHost = new GameObject("Lab scene enemy pipeline");
        Enemies = pressureHost.AddComponent<EnemySpawner>();
        Enemies.StopSpawning();
        events.ConfigureDebugEnemySpawner(Enemies);
        if (enableEnemyPressure && debugEnemyPrefab != null)
            Enemies.ConfigureDebugExplorationPressure(new[] { debugEnemyPrefab }, 1.5f, 16, 1, 9f, 15f);
        events.EventCompleted += OnTerminal;
        events.EventFailed += OnTerminal;
        return true;
    }
    private void Update() { if (centerShift != null) centerShift.Configure(2f, 6f, 8f); }
    private void OnTerminal(WorldEvent finished)
    {
        if (finished is OrbitalRelayEvent || finished is FalseSignalEvent) Enemies?.StopDebugExplorationPressure();
    }
    public void Clear()
    {
        if (events != null)
        {
            events.EventCompleted -= OnTerminal; events.EventFailed -= OnTerminal;
            events.ConfigureDebugEnemySpawner(null); events = null;
        }
        Enemies?.StopDebugExplorationPressure();
        Enemies?.ClearDebugSpawnedEnemies(); Enemies = null;
        if (centerShift != null) centerShift.ResetOffset(); centerShift = null;
        if (pressureHost != null) { pressureHost.SetActive(false); Destroy(pressureHost); } pressureHost = null;
        if (support != null) support.SetActive(false);
        Station = null;
    }
    private void OnDisable() => Clear();
    private void OnDestroy()
    {
        Clear();
        if (support != null) Destroy(support);
        if (ownedStation != null) Destroy(ownedStation);
        if (ownedCurrency != null) Destroy(ownedCurrency.gameObject);
        if (ownedRun != null) Destroy(ownedRun.gameObject);
    }
}
#endif
