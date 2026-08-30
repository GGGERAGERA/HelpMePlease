using UnityEngine;

[DisallowMultipleComponent]
public sealed class BunkerGateVisual : MonoBehaviour
{
    [SerializeField] private GameObject openedDoor;
    [SerializeField] private GameObject closedDoor;
    [SerializeField] private bool startOpen;

    public bool IsOpen { get; private set; }

    private void Awake()
    {
        SetOpen(startOpen);
    }

    public void SetOpen(bool open)
    {
        IsOpen = open;

        if (openedDoor != null)
            openedDoor.SetActive(open);
        if (closedDoor != null)
            closedDoor.SetActive(!open);
    }

    public void Open() => SetOpen(true);

    public void Close() => SetOpen(false);
}
