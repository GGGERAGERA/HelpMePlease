using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class BunkerGateVisual : MonoBehaviour
{
    [SerializeField] private GameObject openedDoor;
    [SerializeField] private GameObject closedDoor;
    [SerializeField] private bool startOpen;
    [SerializeField] private bool openOnPlayerProximity;
    [SerializeField, Min(0f)] private float proximityDistance = .35f;
    private Bounds closedDoorBounds;
    private bool hasDoorBounds;
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
        if (openOnPlayerProximity) CacheDoorBounds();
    }

    private void OnTriggerEnter2D(Collider2D other) => TrackPlayer(other);
    private void OnTriggerStay2D(Collider2D other) => TrackPlayer(other);

    private void TrackPlayer(Collider2D other)
    {
        if (!openOnPlayerProximity || !isActiveAndEnabled || other.isTrigger ||
            other.attachedRigidbody == null || !other.attachedRigidbody.CompareTag("Player"))
            return;

        playerContacts.Add(other);
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (openOnPlayerProximity && playerContacts.Remove(other) && playerContacts.Count == 0)
            SetOpen(false);
    }

    private void FixedUpdate()
    {
        if (!openOnPlayerProximity) return;
        playerContacts.RemoveWhere(contact => contact == null || !contact.enabled || !contact.gameObject.activeInHierarchy);
        bool near = false;
        float limit = proximityDistance + (IsOpen ? .15f : 0f);
        foreach (var contact in playerContacts)
        {
            var body = contact.bounds;
            float dx = Mathf.Max(0f, closedDoorBounds.min.x - body.max.x, body.min.x - closedDoorBounds.max.x);
            float dy = Mathf.Max(0f, closedDoorBounds.min.y - body.max.y, body.min.y - closedDoorBounds.max.y);
            if (hasDoorBounds && dx * dx + dy * dy <= limit * limit) { near = true; break; }
        }
        SetOpen(near);
    }

    private void CacheDoorBounds()
    {
        if (closedDoor == null) return;
        foreach (var collider in closedDoor.GetComponentsInChildren<Collider2D>(true))
        {
            if (collider.isTrigger || collider.bounds.size.sqrMagnitude <= .001f) continue;
            if (!hasDoorBounds) { closedDoorBounds = collider.bounds; hasDoorBounds = true; }
            else closedDoorBounds.Encapsulate(collider.bounds);
        }
        if (!hasDoorBounds)
            foreach (var renderer in closedDoor.GetComponentsInChildren<Renderer>(true))
            {
                if (!hasDoorBounds) { closedDoorBounds = renderer.bounds; hasDoorBounds = true; }
                else closedDoorBounds.Encapsulate(renderer.bounds);
            }
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
