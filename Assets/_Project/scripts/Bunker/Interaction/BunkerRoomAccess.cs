using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// Switches artist-authored room objects and station access. It never creates visuals.
/// </summary>
public sealed class BunkerRoomAccess : MonoBehaviour
{
    [SerializeField] private BunkerRoomId roomId;
    [SerializeField] private GameObject openDoorVisual;
    [SerializeField] private GameObject closedDoorVisual;
    [SerializeField] private GameObject closedFog;
    [SerializeField] private BunkerStation[] stations;
    [SerializeField] private bool defaultUnlocked;

    [FormerlySerializedAs("interactionsRoot")]
    [SerializeField, HideInInspector] private GameObject legacyStationsRoot;

    private bool unlocked;

    public BunkerRoomId RoomId => roomId;
    public bool Unlocked => unlocked;
    public bool DefaultUnlocked => defaultUnlocked;

    private void Awake()
    {
        CacheLegacyStations();
        ResetToDefault();
    }

    private void OnEnable()
    {
        ApplyState();
    }

    public void SetUnlocked(bool value)
    {
        unlocked = value;
        ApplyState();
    }

    public void ResetToDefault() => SetUnlocked(defaultUnlocked);

    [ContextMenu("Room/Debug Open")]
    private void DebugOpenRoom() => SetUnlocked(true);

    [ContextMenu("Room/Debug Close")]
    private void DebugCloseRoom() => SetUnlocked(false);

    [ContextMenu("Room/Reset Default")]
    private void DebugResetRoom() => ResetToDefault();

    private void ApplyState()
    {
        SetActive(openDoorVisual, unlocked);
        SetActive(closedDoorVisual, !unlocked);
        SetActive(closedFog, !unlocked);

        if (stations == null)
            return;

        for (int i = 0; i < stations.Length; i++)
            stations[i]?.SetInteractionEnabled(unlocked);
    }

    private void CacheLegacyStations()
    {
        if ((stations != null && stations.Length > 0) || legacyStationsRoot == null)
            return;

        stations = legacyStationsRoot.GetComponentsInChildren<BunkerStation>(true);
    }

    private static void SetActive(GameObject target, bool active)
    {
        if (target != null && target.activeSelf != active)
            target.SetActive(active);
    }
}
