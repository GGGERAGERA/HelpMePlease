using TMPro;
using UnityEngine;

public class InteractionPromptUI : MonoBehaviour
{
    [SerializeField] private PlayerInteractor playerInteractor;
    [SerializeField] private GameObject promptPanel;
    [SerializeField] private TextMeshProUGUI promptText;
    [SerializeField, Min(0f)] private float worldHeightOffset = 1.5f;

    private Camera targetCamera;

    public void Bind(PlayerInteractor interactor)
    {
        playerInteractor = interactor;
        promptPanel.SetActive(false);
    }

    private void Awake()
    {
        if (promptPanel != null)
            promptPanel.SetActive(false);

        targetCamera = Camera.main;
    }

    private void LateUpdate()
    {
        if (playerInteractor == null || Time.timeScale <= 0f || SceneTransitionOverlay.IsTransitioning)
        {
            if (promptPanel != null)
                promptPanel.SetActive(false);

            return;
        }

        Interactable interactable = playerInteractor.GetCurrentInteractable();

        if (interactable == null || !interactable.isActiveAndEnabled || !interactable.CanInteract)
        {
            if (promptPanel != null)
                promptPanel.SetActive(false);

            return;
        }

        if (promptPanel != null)
        {
            promptPanel.SetActive(true);
            PositionPromptAbovePlayer();
        }

        if (promptText != null)
            promptText.text = interactable is WorldEvent
                ? "[E] " + LocalizationService.Instance.Get("hud.interact")
                : $"[E] {interactable.PromptText}";
    }

    private void PositionPromptAbovePlayer()
    {
        if (promptPanel == null || playerInteractor == null)
            return;

        if (targetCamera == null)
            targetCamera = Camera.main;

        if (targetCamera == null)
            return;

        Vector3 worldPosition =
            playerInteractor.transform.position +
            Vector3.up * worldHeightOffset;

        promptPanel.transform.position =
            targetCamera.WorldToScreenPoint(worldPosition);
    }
}
