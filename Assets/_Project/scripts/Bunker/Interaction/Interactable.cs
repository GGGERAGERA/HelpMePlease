using UnityEngine;

public abstract class Interactable : MonoBehaviour
{
    [SerializeField] private string promptText = "hud.interact";

    public string PromptText => LocalizationService.Instance.Get(promptText);
    public virtual bool CanInteract => true;

    public abstract void Interact();
}
