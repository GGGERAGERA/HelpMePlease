using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(BoxCollider2D))]
[RequireComponent(typeof(BunkerHoverOutline))]
public sealed class FootballStartStation : MonoBehaviour, IBunkerInteractable
{
    [SerializeField] private FootballMinigame minigame;
    [SerializeField] private GameObject interactionArrow;
    [SerializeField] private Transform[] idleMarkers;
    public bool CanInteract => isActiveAndEnabled && minigame.CanStart;
    public string InteractionText => LocalizationService.Instance.Get("hud.interact");

    public void Interact()
    {
        if (CanInteract) minigame.StartGame();
    }

    private void OnEnable() => RefreshArrow();
    public void RefreshArrow()
    {
        // The scene may destroy the arrow before its station during teardown.
        if (interactionArrow != null) interactionArrow.SetActive(CanInteract);
    }
    public void SetRoundActive(bool running)
    {
        // The station stays available to the cursor from anywhere in the bunker.
        // Only its idle presentation hides while this round owns the room.
        foreach (Transform marker in idleMarkers) marker.gameObject.SetActive(!running);
        if (running) GetComponent<BunkerHoverOutline>().SetHovered(false);
        RefreshArrow();
    }
    private void OnDisable()
    {
        GetComponent<BunkerHoverOutline>().SetHovered(false);
        if (interactionArrow != null) interactionArrow.SetActive(false);
    }
}
