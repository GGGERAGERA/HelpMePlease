public interface IBunkerInteractable
{
    bool CanInteract { get; }
    string InteractionText { get; }

    void Interact();
}