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

    [Header("Fallback")]
    [SerializeField] private BunkerPanelManager panelManagerFallback;

    [Header("Animation")]
    [SerializeField] private Animator animator;
    [SerializeField] private string animationTrigger = "Interact";

    [Header("Custom")]
    [SerializeField] private UnityEvent onInteract;

    public bool CanInteract => stationType != BunkerStationType.None;
    public string InteractionText => interactionText;

    private BunkerPanelManager Panels =>
        BunkerContext.Instance != null && BunkerContext.Instance.Panels != null
            ? BunkerContext.Instance.Panels
            : panelManagerFallback;

    public void Interact()
    {
        switch (stationType)
        {
            case BunkerStationType.CharacterSelection:
                Panels?.OpenCharacterSelection();
                break;

            case BunkerStationType.WeaponSelection:
                Panels?.OpenWeaponSelection();
                break;

            case BunkerStationType.Shop:
                Panels?.OpenShop();
                break;

            case BunkerStationType.Map:
                Panels?.OpenMap();
                break;

            case BunkerStationType.Upgrade:
                Panels?.OpenUpgrade();
                break;

            case BunkerStationType.StartRun:
                Panels?.StartRun();
                break;

            case BunkerStationType.Animation:
                if (animator != null)
                    animator.SetTrigger(animationTrigger);
                break;

            case BunkerStationType.CustomEvent:
                onInteract?.Invoke();
                break;
        }

        if (progressionEnabled)
            Panels?.ShowStationProgression(progressionStationId);
    }
}
