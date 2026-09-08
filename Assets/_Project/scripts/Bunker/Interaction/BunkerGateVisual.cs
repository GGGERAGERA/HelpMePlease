using UnityEngine;

[DisallowMultipleComponent]
public sealed class BunkerGateVisual : MonoBehaviour
{
    [SerializeField] private GameObject openedDoor;
    [SerializeField] private GameObject closedDoor;
    [SerializeField] private bool startOpen;

    public bool IsOpen { get; private set; }
    public event System.Action AvailabilityChanged;

    private void OnEnable() => AvailabilityChanged?.Invoke();
    private void OnDisable() => AvailabilityChanged?.Invoke();

    private void Awake()
    {
        SetOpen(startOpen);
    }

    public void SetOpen(bool open)
    {
        bool changed = IsOpen != open;
        IsOpen = open;

        if (openedDoor != null)
            openedDoor.SetActive(open);
        if (closedDoor != null)
            closedDoor.SetActive(!open);
        if (changed) AvailabilityChanged?.Invoke();
    }

    public void Open() => SetOpen(true);

    public void Close() => SetOpen(false);
}
