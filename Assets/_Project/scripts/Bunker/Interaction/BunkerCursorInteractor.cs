using UnityEngine;

public sealed class BunkerCursorInteractor : MonoBehaviour
{
    [SerializeField] private Camera targetCamera;
    [SerializeField] private LayerMask interactionMask;

    [Header("Fallback")]
    [SerializeField] private BunkerPanelManager panelManagerFallback;

    private BunkerInteractableCollider current;

    private BunkerPanelManager Panels =>
        BunkerContext.Instance != null && BunkerContext.Instance.Panels != null
            ? BunkerContext.Instance.Panels
            : panelManagerFallback;

    private void Awake()
    {
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
        UpdateHover();

        if (Input.GetMouseButtonDown(0))
            TryInteract();
    }

    private void UpdateHover()
    {
        if (Panels != null && Panels.IsAnyPanelOpen)
        {
            current?.Hoverable?.SetHovered(false);
            current = null;
            return;
        }

        BunkerInteractableCollider next = RaycastInteractable();

        if (next == current)
            return;

        current?.Hoverable?.SetHovered(false);

        current = next;

        current?.Hoverable?.SetHovered(true);
    }

    private void TryInteract()
    {
        if (Panels != null && Panels.IsAnyPanelOpen)
            return;

        BunkerInteractableCollider target = RaycastInteractable();

        if (target == null)
            return;

        IBunkerInteractable interactable = target.Interactable;

        if (interactable == null || !interactable.CanInteract)
            return;

        interactable.Interact();
    }

    private BunkerInteractableCollider RaycastInteractable()
    {
        if (targetCamera == null)
            return null;

        Vector2 mouseWorld = targetCamera.ScreenToWorldPoint(Input.mousePosition);
        Collider2D hit = Physics2D.OverlapPoint(mouseWorld, interactionMask);

        if (hit == null)
            return null;

        return hit.GetComponent<BunkerInteractableCollider>();
    }
}
