using UnityEngine;

public sealed class BunkerContext : MonoBehaviour
{
    public static BunkerContext Instance { get; private set; }

    [field: SerializeField] public BunkerPanelManager Panels { get; private set; }
    [field: SerializeField] public BunkerNotificationManager Notifications { get; private set; }
    [field: SerializeField] public BunkerEventManager Events { get; private set; }
    [field: SerializeField] public BunkerShopService Shop { get; private set; }
    [field: SerializeField] public BunkerRunStarter RunStarter { get; private set; }
    [field: SerializeField] public BunkerContentRegistry ContentRegistry { get; private set; }
    public BunkerStationProgressionService StationProgression { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        StationProgression = GetComponent<BunkerStationProgressionService>();
        if (StationProgression == null)
            StationProgression = gameObject.AddComponent<BunkerStationProgressionService>();

        // MainMenu-only setup keeps scene-specific prototype gates out of gameplay scenes.
        if (GetComponent<BunkerProgressionSceneSetup>() == null)
            gameObject.AddComponent<BunkerProgressionSceneSetup>();
    }
}
