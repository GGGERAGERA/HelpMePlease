using TMPro;
using UnityEngine;

public class InteractionPromptUI : MonoBehaviour
{
    [SerializeField] private PlayerInteractor playerInteractor;
    [SerializeField] private GameObject promptPanel;
    [SerializeField] private TextMeshProUGUI promptText;

    private void Awake()
    {
        if (promptPanel != null)
            promptPanel.SetActive(false);
    }

    private void Update()
    {
        if (playerInteractor == null)
            return;

        Interactable interactable = playerInteractor.GetCurrentInteractable();

        if (interactable == null)
        {
            if (promptPanel != null)
                promptPanel.SetActive(false);

            return;
        }

        if (promptPanel != null)
            promptPanel.SetActive(true);

        if (promptText != null)
            promptText.text = $"[E] {interactable.PromptText}";
    }
}