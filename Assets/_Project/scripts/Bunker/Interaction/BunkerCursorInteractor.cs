using UnityEngine;

public sealed class BunkerCursorInteractor : MonoBehaviour
{
    [SerializeField] private Camera targetCamera;
    [SerializeField] private LayerMask interactionMask;
    [SerializeField] private bool arrowsOnly;
    private readonly Collider2D[] hits = new Collider2D[32];

    [Header("Panel Navigation")]
    [SerializeField] private BunkerPanelManager panelManager;

    private BunkerInteractableCollider current;
    private BunkerInteractionIndicators indicators;

    private bool IsInputBlocked => Time.timeScale <= 0f || SceneTransitionOverlay.IsTransitioning ||
        (Panels != null && Panels.IsAnyPanelOpen);

    private BunkerPanelManager Panels => panelManager;

    private void Awake()
    {
        indicators = GetComponent<BunkerInteractionIndicators>();
        if (targetCamera == null)
            targetCamera = Camera.main;
    }

    private void OnDisable()
    {
        current?.Hoverable?.SetHovered(false);
        current = null;
    }

    private void Update()
    {
        if (targetCamera == null) return;
        Vector2 mouseWorld = targetCamera.ScreenToWorldPoint(Input.mousePosition);
        UpdateHoverAt(mouseWorld);

        if (Input.GetMouseButtonDown(0))
            TryInteractAt(mouseWorld);
    }

    public void UpdateHoverAt(Vector2 worldPosition)
    {
        if (IsInputBlocked)
        {
            current?.Hoverable?.SetHovered(false);
            current = null;
            return;
        }

        BunkerInteractableCollider next = RaycastInteractable(worldPosition);

        if (next == current)
            return;

        current?.Hoverable?.SetHovered(false);

        current = next;

        current?.Hoverable?.SetHovered(true);
    }

    public bool TryInteractAt(Vector2 worldPosition)
    {
        if (IsInputBlocked)
            return false;

        BunkerInteractableCollider target = RaycastInteractable(worldPosition);

        if (target == null)
            return false;

        IBunkerInteractable interactable = target.Interactable;

        if (interactable == null || !interactable.CanInteract)
            return false;

        interactable.Interact();
        return true;
    }

    private BunkerInteractableCollider RaycastInteractable(Vector2 worldPosition)
    {
        var filter = new ContactFilter2D { useTriggers = true };
        filter.SetLayerMask(interactionMask);
        int count = Physics2D.OverlapPoint(worldPosition, filter, hits);
        BunkerInteractableCollider best = null;
        float closest = float.PositiveInfinity;
        int bestPriority = int.MaxValue;
        for (int i = 0; i < count; i++)
        {
            var candidate = hits[i].GetComponent<BunkerInteractableCollider>();
            if (candidate == null || candidate.Interactable == null || !candidate.Interactable.CanInteract) continue;
            var arrow = candidate.GetComponent<BunkerInteractionArrow>();
            var indicator = arrow;
            if(indicator == null && indicators != null && candidate.Interactable is MonoBehaviour source)
                indicators.TryGetArrow(source,out indicator);
            // The arrow and station body share availability; pointer input has no player-distance gate.
            if (arrowsOnly && (indicator == null || !indicator.IsVisible || !indicator.CanInteractNow)) continue;
            int priority = arrow != null ? 0 : 1;
            float distance = ((Vector2)hits[i].bounds.center - worldPosition).sqrMagnitude;
            bool nearer = distance < closest || (Mathf.Approximately(distance, closest) &&
                (best == null || candidate.GetInstanceID() < best.GetInstanceID()));
            if (priority < bestPriority || (priority == bestPriority && nearer))
            { best = candidate; closest = distance; bestPriority = priority; }
        }
        return best;
    }
}
