using UnityEngine;
using UnityEngine.Events;

public sealed class BunkerStation : MonoBehaviour, IBunkerInteractable
{
    [Header("Station")]
    [SerializeField] private BunkerStationType stationType;
    [SerializeField] private string interactionText = "hud.interact";

    [Header("Start Run")]
    [SerializeField] private Transform runTransitionTarget;

    [Header("Panel Navigation")]
    [SerializeField] private BunkerPanelManager panelManager;

    [Header("Animation")]
    [SerializeField] private Animator animator;
    [SerializeField] private string animationTrigger = "Interact";

    [Header("Custom")]
    [SerializeField] private UnityEvent onInteract;

    private bool interactionEnabled = true;
    public bool InteractionEnabled => interactionEnabled;
    public event System.Action AvailabilityChanged;
    private void OnEnable() => AvailabilityChanged?.Invoke();
    private void OnDisable() => AvailabilityChanged?.Invoke();

    // Serialized shop ID is reserved, but the production feature is removed.
    public bool CanInteract => isActiveAndEnabled && interactionEnabled &&
        stationType != BunkerStationType.None &&
        stationType != BunkerStationType.Shop;
    public string InteractionText => LocalizationService.Instance.Get(interactionText);

    private BunkerPanelManager Panels => panelManager;

    public void Interact()
    {
        if (!CanInteract)
            return;

        switch (stationType)
        {
            case BunkerStationType.EscapeProtocol:
                Panels?.OpenEscapeProtocol();
                break;

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
                Panels?.OpenDepthSelect(runTransitionTarget);
                break;

            case BunkerStationType.Animation:
                if (animator != null)
                    animator.SetTrigger(animationTrigger);
                break;

            case BunkerStationType.OrbitalSlot:
                Panels?.OpenOrbitalSlot();
                break;

            case BunkerStationType.CustomEvent:
                onInteract?.Invoke();
                break;

            case BunkerStationType.AnomalyStabilizer:
                Panels?.OpenAnomalyStabilizers();
                break;
        }
    }

    public void SetInteractionEnabled(bool enabled)
    {
        if (interactionEnabled == enabled) return;
        interactionEnabled = enabled;
        AvailabilityChanged?.Invoke();
    }
}
