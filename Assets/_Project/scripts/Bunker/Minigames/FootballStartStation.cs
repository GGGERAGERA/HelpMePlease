using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(BoxCollider2D))]
[RequireComponent(typeof(BunkerHoverOutline))]
public sealed class FootballStartStation : MonoBehaviour, IBunkerInteractable
{
    [SerializeField] private FootballMinigame minigame;
    [SerializeField] private GameObject interactionArrow;
    private readonly HashSet<Collider2D> playerContacts = new();

    public bool CanInteract => isActiveAndEnabled && minigame.CanStart && playerContacts.Count > 0;
    public string InteractionText => LocalizationService.Instance.Get("hud.interact");

    public void Interact()
    {
        if (CanInteract) minigame.StartGame();
    }

    private void OnTriggerEnter2D(Collider2D other) => TrackPlayer(other);
    private void OnTriggerStay2D(Collider2D other) => TrackPlayer(other);
    private void TrackPlayer(Collider2D other)
    {
        if (!isActiveAndEnabled || other.isTrigger ||
            other.GetComponentInParent<CharacterMovement2D>() == null) return;
        if (playerContacts.Add(other)) RefreshArrow();
    }
    private void OnTriggerExit2D(Collider2D other)
    {
        if (playerContacts.Remove(other)) RefreshArrow();
    }
    private void FixedUpdate()
    {
        if (playerContacts.RemoveWhere(c => c == null || !c.enabled || !c.gameObject.activeInHierarchy) > 0)
            RefreshArrow();
    }
    public void RefreshArrow()
    {
        // The scene may destroy the arrow before its station during teardown.
        if (interactionArrow != null) interactionArrow.SetActive(CanInteract);
    }
    private void OnDisable()
    {
        GetComponent<BunkerHoverOutline>().SetHovered(false);
        playerContacts.Clear();
        if (interactionArrow != null) interactionArrow.SetActive(false);
    }
}
