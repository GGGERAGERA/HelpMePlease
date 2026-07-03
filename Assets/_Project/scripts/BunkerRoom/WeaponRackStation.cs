using UnityEngine;

public sealed class WeaponRackStation : MonoBehaviour, IBunkerInteractable
{
    [SerializeField] private SelectionPanelController selectionPanelController;

    public bool CanInteract => selectionPanelController != null;
    public string InteractionText => "Выбрать оружие";

    public void Interact()
    {
        selectionPanelController.ShowWeapons();
    }
}