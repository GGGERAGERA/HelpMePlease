using TMPro;
using UnityEngine;

public class InteractionPromptUI : MonoBehaviour
{
    [SerializeField] private PlayerInteractor playerInteractor;
    [SerializeField] private GameObject promptPanel;
    [SerializeField] private TextMeshProUGUI promptText;
    [SerializeField, Min(0f)] private float worldHeightOffset = 1.5f;

    private Camera targetCamera;

    private void Awake()
    {
        if (promptPanel != null)
            promptPanel.SetActive(false);

        targetCamera = Camera.main;
    }

    private void Update()
    {
        if (playerInteractor == null)
            playerInteractor = FindFirstObjectByType<PlayerInteractor>();

        if (playerInteractor == null)
        {
            if (promptPanel != null)
                promptPanel.SetActive(false);

            return;
        }

        Interactable interactable = playerInteractor.GetCurrentInteractable();

        if (interactable == null)
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
            promptText.text = $"[E] {interactable.PromptText}";
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
