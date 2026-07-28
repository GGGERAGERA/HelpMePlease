using UnityEngine;

public class PlayerInteractor : MonoBehaviour
{
    [SerializeField] private float interactionRadius = 1.5f;

    private Interactable currentInteractable;

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

        Collider2D[] hits =
            Physics2D.OverlapCircleAll(
                transform.position,
                interactionRadius
            );

        float closestDistance = float.MaxValue;

        foreach (Collider2D hit in hits)
        {
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
