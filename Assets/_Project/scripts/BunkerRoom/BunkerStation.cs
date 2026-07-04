using UnityEngine;
using UnityEngine.Events;

public sealed class BunkerStation : MonoBehaviour, IBunkerInteractable
{
    [Header("Station")]
    [SerializeField] private BunkerStationType stationType;
    [SerializeField] private string interactionText = "Взаимодействовать";

    [Header("UI")]
    [SerializeField] private BunkerPanelManager panelManager;

    [Header("Animation")]
    [SerializeField] private Animator animator;
    [SerializeField] private string animationTrigger = "Interact";

    [Header("Custom")]
    [SerializeField] private UnityEvent onInteract;

    public bool CanInteract => stationType != BunkerStationType.None;
    public string InteractionText => interactionText;

    public void Interact()
    {
        switch (stationType)
        {
            case BunkerStationType.CharacterSelection:
                panelManager.OpenCharacterSelection();
                break;

            case BunkerStationType.WeaponSelection:
                panelManager.OpenWeaponSelection();
                break;

            case BunkerStationType.Shop:
                panelManager.OpenShop();
                break;

            case BunkerStationType.Map:
                panelManager.OpenMap();
                break;

            case BunkerStationType.Upgrade:
                panelManager.OpenUpgrade();
                break;

            case BunkerStationType.Animation:
                if (animator != null)
                    animator.SetTrigger(animationTrigger);
                break;

            case BunkerStationType.CustomEvent:
                onInteract?.Invoke();
                break;
        }
    }
}