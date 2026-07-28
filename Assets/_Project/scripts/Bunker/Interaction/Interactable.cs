using UnityEngine;

public abstract class Interactable : MonoBehaviour
{
    [SerializeField] private string promptText = "Press E";

    public string PromptText => promptText;
    public virtual bool CanInteract => true;

    public abstract void Interact();
}
