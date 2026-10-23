using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class BunkerGateVisual : MonoBehaviour
{
    [SerializeField] private GameObject openedDoor;
    [SerializeField] private GameObject closedDoor;
    [SerializeField] private SpriteRenderer[] availabilityIndicators = System.Array.Empty<SpriteRenderer>();
    [SerializeField] private Color openIndicatorColor = new(.3f, .95f, .64f, 1f);
    [SerializeField] private Color closedIndicatorColor = new(1f, .18f, .12f, 1f);
    [SerializeField] private bool startUnlocked = true;
    [SerializeField] private bool startOpen;
    [SerializeField] private bool openOnPlayerProximity;
    [SerializeField, Min(0f)] private float proximityDistance = .35f;
    private Bounds closedDoorBounds;
    private bool hasDoorBounds;
    private readonly HashSet<Collider2D> playerContacts = new();
    private bool desiredOpen;
    private bool desiredUnlocked;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    // Runtime-only overrides: never write room unlocks or player progression.
    private bool? debugOpen;
    private bool? debugLocked;
    public bool DebugHasOverride => debugOpen.HasValue || debugLocked.HasValue;
    public bool DebugIsLocked => !IsUnlocked;
#endif

    public bool IsOpen { get; private set; }
    public bool IsUnlocked { get; private set; }
    public event System.Action AvailabilityChanged;

#if UNITY_EDITOR
    private void OnValidate()
    {
        UnityEditor.EditorApplication.delayCall -= ApplyAuthoredState;
        UnityEditor.EditorApplication.delayCall += ApplyAuthoredState;
    }

    private void ApplyAuthoredState()
    {
        if (this != null && !UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode)
        {
            desiredUnlocked = startUnlocked;
            desiredOpen = !openOnPlayerProximity && startOpen;
            ApplyState();
        }
    }
#endif

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
        SetUnlocked(startUnlocked);
        SetOpen(!openOnPlayerProximity && startOpen);
        if (openOnPlayerProximity) CacheDoorBounds();
    }

    private void OnTriggerEnter2D(Collider2D other) => TrackPlayer(other);
    private void OnTriggerStay2D(Collider2D other) => TrackPlayer(other);

    private void TrackPlayer(Collider2D other)
    {
        if (!IsUnlocked || !openOnPlayerProximity || !isActiveAndEnabled || other.isTrigger ||
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
        if (!openOnPlayerProximity || !IsUnlocked) return;
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
        ApplyState();
    }

    public void SetUnlocked(bool unlocked)
    {
        if (!unlocked || desiredUnlocked != unlocked)
            desiredOpen = false;
        desiredUnlocked = unlocked;
        if (!unlocked)
        {
            playerContacts.Clear();
        }
        ApplyState();
    }

    private void SetProximityOpen(bool open)
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        // Automatic ticks must not replace the latest external story request during an override.
        if (debugOpen.HasValue || debugLocked == true) return;
#endif
        SetOpen(open);
    }

    private void ApplyState()
    {
        bool unlocked = desiredUnlocked;
        bool open = desiredOpen;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        unlocked = debugLocked.HasValue ? !debugLocked.Value : unlocked;
        open = debugOpen ?? open;
#endif
        open &= unlocked;
        bool changed = IsOpen != open || IsUnlocked != unlocked;
        IsUnlocked = unlocked;
        IsOpen = open;

        if (openedDoor != null)
            openedDoor.SetActive(open);
        if (closedDoor != null)
            closedDoor.SetActive(!open);
        Color indicatorColor = unlocked ? openIndicatorColor : closedIndicatorColor;
        foreach (var indicator in availabilityIndicators)
            indicator.color = indicatorColor;
        if (changed) AvailabilityChanged?.Invoke();
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    public void DebugSetOpen(bool open)
    {
        debugOpen = open;
        if (open) debugLocked = false;
        ApplyState();
        AvailabilityChanged?.Invoke();
    }

    public void DebugSetLocked(bool locked)
    {
        debugLocked = locked;
        ApplyState();
        AvailabilityChanged?.Invoke();
    }

    public void DebugResetOverride()
    {
        debugOpen = null;
        debugLocked = null;
        ApplyState();
        AvailabilityChanged?.Invoke();
    }
#endif

    public void Open() => SetOpen(true);

    public void Close() => SetOpen(false);
}
