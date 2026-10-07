using UnityEngine;

public abstract class Interactable : MonoBehaviour
{
    [Tooltip("Localization key for the HUD prompt. Leave empty when the prefab presents its own prompt.")]
    [SerializeField] private string promptText = "hud.interact";

    public bool HasInteractionPrompt => !string.IsNullOrEmpty(promptText);
    public string PromptText => LocalizationService.Instance.Get(promptText);
    public virtual bool CanInteract => true;

    public abstract void Interact();
}
