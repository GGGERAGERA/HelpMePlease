using UnityEngine;
using UnityEngine.Events;

public sealed class BunkerStation : MonoBehaviour, IBunkerInteractable
{
    [Header("Station")]
    [SerializeField] private BunkerStationType stationType;
    [SerializeField] private string interactionText = "Взаимодействовать";

    [Header("Progression")]
    [SerializeField] private bool progressionEnabled;
    [SerializeField] private BunkerStationId progressionStationId;

    [Header("Start Run")]
    [SerializeField] private Transform runTransitionTarget;

    [Header("Fallback")]
    [SerializeField] private BunkerPanelManager panelManagerFallback;

    [Header("Animation")]
    [SerializeField] private Animator animator;
    [SerializeField] private string animationTrigger = "Interact";

    [Header("Custom")]
    [SerializeField] private UnityEvent onInteract;

    private bool interactionEnabled = true;

    // Serialized shop ID is reserved, but the production feature is removed.
    public bool CanInteract => interactionEnabled &&
        stationType != BunkerStationType.None &&
        stationType != BunkerStationType.Shop;
    public string InteractionText => interactionText;

    private BunkerPanelManager Panels =>
        BunkerContext.Instance != null && BunkerContext.Instance.Panels != null
            ? BunkerContext.Instance.Panels
            : panelManagerFallback;

    public void Interact()
    {
        if (!CanInteract)
            return;

        switch (stationType)
        {
            case BunkerStationType.CharacterSelection:
                Panels?.OpenCharacterSelection();
                break;

            case BunkerStationType.WeaponSelection:
                Panels?.OpenWeaponSelection();
                break;

            case BunkerStationType.Map:
                Panels?.OpenMap();
                break;

            case BunkerStationType.Upgrade:
                Panels?.OpenUpgrade();
                break;

            case BunkerStationType.StartRun:
                Panels?.StartRun(runTransitionTarget);
                break;

            case BunkerStationType.Animation:
                if (animator != null)
                    animator.SetTrigger(animationTrigger);
                break;

            case BunkerStationType.CustomEvent:
                onInteract?.Invoke();
                break;

            case BunkerStationType.AnomalyStabilizer:
                Panels?.OpenAnomalyStabilizers();
                break;
        }

        // Character progression is presented inside the existing selection frame.
        // Other stations continue using the reusable floating panel.
        if (progressionEnabled && progressionStationId != BunkerStationId.Character)
            Panels?.ShowStationProgression(progressionStationId);
    }

    public void SetInteractionEnabled(bool enabled)
    {
        interactionEnabled = enabled;
    }
}
