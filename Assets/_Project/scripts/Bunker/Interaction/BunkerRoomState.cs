using System;
using System.Collections.Generic;
using UnityEngine;

public enum BunkerRoomAccessState
{
    Closed = 0,
    Open = 1
}

public interface IBunkerRoomStatePresentation
{
    void ApplyRoomState(BunkerRoomAccessState state);
}

public interface IBunkerRoomIdentityPresentation
{
    void ApplyRoomIdentity(BunkerRoomId roomId);
}

/// <summary>
/// Owns room access state. Station progression may decide when to call this API,
/// but it is deliberately not read or stored by this component.
/// </summary>
public sealed class BunkerRoomState : MonoBehaviour
{
    [Header("Identity")]
    [SerializeField] private BunkerRoomId roomId;

    [Header("State")]
    [SerializeField] private BunkerRoomAccessState state = BunkerRoomAccessState.Closed;

    [Header("Closed Visuals")]
    [SerializeField] private GameObject[] closedVisuals;
    [SerializeField] private GameObject redWarningVisual;
    [SerializeField] private GameObject darkVeil;

    [Header("Open Visuals")]
    [SerializeField] private GameObject[] openVisuals;

    [Header("Access Blocking")]
    [SerializeField] private Collider2D blockingCollider;
    [Tooltip("Interactable colliders below this root are cached and disabled while the room is closed.")]
    [SerializeField] private GameObject interactionsRoot;

    [Header("Optional Presentation Adapters")]
    [Tooltip("Components implementing IBunkerRoomStatePresentation, for example an existing BunkerRoomGate.")]
    [SerializeField] private MonoBehaviour[] presentationDrivers;

    private readonly List<ColliderState> interactionColliders = new();
    private bool initialized;

    public event Action<BunkerRoomState, BunkerRoomAccessState> StateChanged;

    public BunkerRoomId RoomId => roomId;
    public BunkerRoomAccessState State => state;
    public bool IsOpen => state == BunkerRoomAccessState.Open;

    private void Awake()
    {
        CacheInteractionColliders();
        initialized = true;
        ApplyPresentation();
    }

    private void OnEnable()
    {
        if (initialized)
            ApplyPresentation();
    }

    public void SetState(BunkerRoomAccessState newState)
    {
        bool changed = state != newState;
        state = newState;
        ApplyPresentation();

        if (changed)
            StateChanged?.Invoke(this, state);
    }

    public void OpenRoom() => SetState(BunkerRoomAccessState.Open);

    public void CloseRoom() => SetState(BunkerRoomAccessState.Closed);

    [ContextMenu("Room/Debug Open")]
    private void DebugOpenRoom() => OpenRoom();

    [ContextMenu("Room/Debug Close")]
    private void DebugCloseRoom() => CloseRoom();

    [ContextMenu("Room/Refresh Presentation")]
    private void RefreshPresentation() => ApplyPresentation();

    private void ApplyPresentation()
    {
        bool isOpen = IsOpen;
        SetActive(closedVisuals, !isOpen);
        SetActive(openVisuals, isOpen);

        if (redWarningVisual != null)
            redWarningVisual.SetActive(!isOpen);
        if (darkVeil != null)
            darkVeil.SetActive(!isOpen);
        if (blockingCollider != null)
            blockingCollider.enabled = !isOpen;

        for (int i = 0; i < interactionColliders.Count; i++)
        {
            ColliderState cached = interactionColliders[i];
            if (cached.Collider != null)
                cached.Collider.enabled = isOpen && cached.WasEnabled;
        }

        if (presentationDrivers == null)
            return;

        for (int i = 0; i < presentationDrivers.Length; i++)
        {
            if (presentationDrivers[i] is IBunkerRoomIdentityPresentation identityDriver)
                identityDriver.ApplyRoomIdentity(roomId);

            if (presentationDrivers[i] is IBunkerRoomStatePresentation driver)
                driver.ApplyRoomState(state);
        }
    }

    private void CacheInteractionColliders()
    {
        interactionColliders.Clear();
        if (interactionsRoot == null)
            return;

        BunkerInteractableCollider[] interactables =
            interactionsRoot.GetComponentsInChildren<BunkerInteractableCollider>(true);

        for (int i = 0; i < interactables.Length; i++)
        {
            Collider2D collider = interactables[i].GetComponent<Collider2D>();
            if (collider != null && collider != blockingCollider)
                interactionColliders.Add(new ColliderState(collider, collider.enabled));
        }
    }

    private static void SetActive(GameObject[] targets, bool active)
    {
        if (targets == null)
            return;

        for (int i = 0; i < targets.Length; i++)
        {
            if (targets[i] != null && targets[i].activeSelf != active)
                targets[i].SetActive(active);
        }
    }

    private readonly struct ColliderState
    {
        public ColliderState(Collider2D collider, bool wasEnabled)
        {
            Collider = collider;
            WasEnabled = wasEnabled;
        }

        public Collider2D Collider { get; }
        public bool WasEnabled { get; }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (presentationDrivers == null)
            return;

        for (int i = 0; i < presentationDrivers.Length; i++)
        {
            MonoBehaviour candidate = presentationDrivers[i];
            if (candidate != null && candidate is not IBunkerRoomStatePresentation)
            {
                Debug.LogWarning(
                    $"[BunkerRoomState] {candidate.name} does not implement " +
                    $"{nameof(IBunkerRoomStatePresentation)}.",
                    this);
            }
        }
    }
#endif
}
