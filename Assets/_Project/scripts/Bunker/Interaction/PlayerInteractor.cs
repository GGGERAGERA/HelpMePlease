using UnityEngine;

public class PlayerInteractor : MonoBehaviour
{
    private const int InteractionBufferSize = 128;

    [SerializeField] private float interactionRadius = 1.5f;

    private Interactable currentInteractable;
    private readonly Collider2D[] interactionBuffer =
        new Collider2D[InteractionBufferSize];
    private ContactFilter2D interactionFilter;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
    private bool useDebugNonAllocScan = true;

    public void ConfigureDebugNonAllocScan(bool enabled)
    {
        useDebugNonAllocScan = enabled;
    }
#endif

    private void Awake()
    {
        interactionFilter = new ContactFilter2D
        {
            useTriggers = true,
            useLayerMask = false,
            useDepth = false
        };
    }

    private void Update()
    {
        if (Time.timeScale <= 0f)
        {
            currentInteractable = null;
            return;
        }

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
        if (!useDebugNonAllocScan)
        {
            Collider2D[] debugHits = Physics2D.OverlapCircleAll(
                transform.position,
                interactionRadius
            );
            FindClosestInteractable(debugHits, debugHits.Length);
            return;
        }
#endif

        int hitCount = Physics2D.OverlapCircle(
            transform.position,
            interactionRadius,
            interactionFilter,
            interactionBuffer
        );
        FindClosestInteractable(interactionBuffer, hitCount);

        for (int i = 0; i < hitCount; i++)
            interactionBuffer[i] = null;
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
