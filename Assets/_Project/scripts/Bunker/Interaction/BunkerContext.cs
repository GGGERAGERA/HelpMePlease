using UnityEngine;

public sealed class BunkerContext : MonoBehaviour
{
    public static BunkerContext Instance { get; private set; }

    [field: SerializeField] public BunkerPanelManager Panels { get; private set; }
    [field: SerializeField] public BunkerNotificationManager Notifications { get; private set; }
    [field: SerializeField] public BunkerEventManager Events { get; private set; }
    [field: SerializeField] public BunkerRunStarter RunStarter { get; private set; }
    [field: SerializeField] public BunkerStationProgressionService StationProgression { get; private set; }
    [SerializeField] private BunkerPlayerLoadoutController playerLoadout;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        if (StationProgression == null || playerLoadout == null)
        {
            Debug.LogError("[BunkerContext] Authored progression/loadout components are missing.", this);
            enabled = false;
            return;
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        EnsureDebugMenu();
#endif
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    private void EnsureDebugMenu()
    {
        Subject42DebugMenu debugMenu = GetComponent<Subject42DebugMenu>();
        if (debugMenu == null)
            gameObject.AddComponent<Subject42DebugMenu>();
    }
#endif
}
