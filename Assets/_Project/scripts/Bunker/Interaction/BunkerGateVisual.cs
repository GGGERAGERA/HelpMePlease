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
    private bool desiredOpen;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    // Runtime-only overrides: never write room unlocks or player progression.
    private bool? debugOpen;
    private bool? debugLocked;
    public bool DebugHasOverride => debugOpen.HasValue || debugLocked.HasValue;
    public bool DebugIsLocked => debugLocked ?? (!openOnPlayerProximity && !desiredOpen);
#endif

    public bool IsOpen { get; private set; }
    public event System.Action AvailabilityChanged;

    private void OnEnable()
    {
        if (openOnPlayerProximity) SetProximityOpen(false);
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
            SetProximityOpen(false);
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
        SetProximityOpen(near);
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
        // Story and minigame requests keep updating even while debug owns the visuals.
        desiredOpen = open;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        ApplyOpen(debugLocked == true ? false : debugOpen ?? desiredOpen);
#else
        ApplyOpen(desiredOpen);
#endif
    }

    private void SetProximityOpen(bool open)
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        // Automatic ticks must not replace the latest external story request during an override.
        if (DebugHasOverride) return;
#endif
        SetOpen(open);
    }

    private void ApplyOpen(bool open)
    {
        bool changed = IsOpen != open;
        IsOpen = open;

        if (openedDoor != null)
            openedDoor.SetActive(open);
        if (closedDoor != null)
            closedDoor.SetActive(!open);
        if (changed) AvailabilityChanged?.Invoke();
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    public void DebugSetOpen(bool open)
    {
        debugOpen = open;
        if (open) debugLocked = false;
        ApplyOpen(debugLocked == true ? false : open);
        AvailabilityChanged?.Invoke();
    }

    public void DebugSetLocked(bool locked)
    {
        debugLocked = !locked && openOnPlayerProximity ? (bool?)null : locked;
        ApplyOpen(locked ? false : debugOpen ?? desiredOpen);
        AvailabilityChanged?.Invoke();
    }

    public void DebugResetOverride()
    {
        debugOpen = null;
        debugLocked = null;
        ApplyOpen(desiredOpen);
        AvailabilityChanged?.Invoke();
    }
#endif

    public void Open() => SetOpen(true);

    public void Close() => SetOpen(false);
}
