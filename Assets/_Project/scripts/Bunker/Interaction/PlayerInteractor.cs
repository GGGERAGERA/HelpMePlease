using UnityEngine;

public class PlayerInteractor : MonoBehaviour
{
    [SerializeField] private float interactionRadius = 1.5f;

    private Interactable currentInteractable;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
    private const int DebugInteractionBufferSize = 128;
    private readonly Collider2D[] debugInteractionBuffer =
        new Collider2D[DebugInteractionBufferSize];
    private ContactFilter2D debugInteractionFilter;
    private bool useDebugNonAllocScan;

    public void ConfigureDebugNonAllocScan(bool enabled)
    {
        useDebugNonAllocScan = enabled;
        debugInteractionFilter = new ContactFilter2D
        {
            useTriggers = true,
            useLayerMask = false,
            useDepth = false
        };
    }
#endif

    private void Update()
    {
        FindInteractable();

        if (currentInteractable != null &&
            Input.GetKeyDown(KeyCode.E))
        {
            currentInteractable.Interact();
        }
    }

    private void FindInteractable()
    {
        currentInteractable = null;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (useDebugNonAllocScan)
        {
            int hitCount = Physics2D.OverlapCircle(
                transform.position,
                interactionRadius,
                debugInteractionFilter,
                debugInteractionBuffer
            );
            FindClosestInteractable(debugInteractionBuffer, hitCount);
            for (int i = 0; i < hitCount; i++)
                debugInteractionBuffer[i] = null;
            return;
        }
#endif

        Collider2D[] hits =
            Physics2D.OverlapCircleAll(
                transform.position,
                interactionRadius
            );

        FindClosestInteractable(hits, hits.Length);
    }

    private void FindClosestInteractable(Collider2D[] hits, int hitCount)
    {
        float closestDistance = float.MaxValue;

        for (int i = 0; i < hitCount; i++)
        {
            Collider2D hit = hits[i];
            if (hit == null)
                continue;

            Interactable interactable =
                hit.GetComponent<Interactable>();

            if (interactable == null || !interactable.CanInteract)
                continue;

            float distance =
                Vector2.Distance(
                    transform.position,
                    interactable.transform.position
                );

            if (distance < closestDistance)
            {
                closestDistance = distance;
                currentInteractable = interactable;
            }
        }
    }

    public Interactable GetCurrentInteractable()
    {
        return currentInteractable;
    }
}
