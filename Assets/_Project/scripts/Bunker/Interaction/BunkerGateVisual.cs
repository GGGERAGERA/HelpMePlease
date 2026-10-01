using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class BunkerGateVisual : MonoBehaviour
{
    [SerializeField] private GameObject openedDoor;
    [SerializeField] private GameObject closedDoor;
    [SerializeField] private bool startOpen;
    [SerializeField] private bool openOnPlayerProximity;
    private readonly HashSet<Collider2D> playerContacts = new();

    public bool IsOpen { get; private set; }
    public event System.Action AvailabilityChanged;

    private void OnEnable()
    {
        if (openOnPlayerProximity) SetOpen(false);
        AvailabilityChanged?.Invoke();
    }

    private void OnDisable()
    {
        playerContacts.Clear();
        AvailabilityChanged?.Invoke();
    }

    private void Awake()
    {
        SetOpen(!openOnPlayerProximity && startOpen);
    }

    private void OnTriggerEnter2D(Collider2D other) => TrackPlayer(other);
    private void OnTriggerStay2D(Collider2D other) => TrackPlayer(other);

    private void TrackPlayer(Collider2D other)
    {
        if (!openOnPlayerProximity || !isActiveAndEnabled || other.isTrigger ||
            other.attachedRigidbody == null || !other.attachedRigidbody.CompareTag("Player"))
            return;

        if (playerContacts.Add(other)) SetOpen(true);
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (openOnPlayerProximity && playerContacts.Remove(other) && playerContacts.Count == 0)
            SetOpen(false);
    }

    private void FixedUpdate()
    {
        // Disabled or destroyed player colliders may not send an exit callback.
        if (openOnPlayerProximity && playerContacts.RemoveWhere(
                contact => contact == null || !contact.enabled || !contact.gameObject.activeInHierarchy) > 0 &&
            playerContacts.Count == 0)
            SetOpen(false);
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
