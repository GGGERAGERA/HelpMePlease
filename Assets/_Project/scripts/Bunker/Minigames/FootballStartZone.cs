using System.Collections.Generic;
using TMPro;
using UnityEngine;

[RequireComponent(typeof(BoxCollider2D))]
public sealed class FootballStartZone : MonoBehaviour
{
    [SerializeField] private FootballMinigame minigame;
    [SerializeField] private SpriteRenderer[] visualRenderers;
    [SerializeField] private TMP_Text startText;
    private readonly HashSet<Collider2D> playerContacts = new();

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.isTrigger || other.GetComponentInParent<CharacterMovement2D>() == null) return;
        bool firstContact = playerContacts.Count == 0;
        playerContacts.Add(other);
        if (firstContact && minigame.CanStart) minigame.StartGame();
    }

    private void OnTriggerExit2D(Collider2D other) => playerContacts.Remove(other);
    private void OnDisable() => playerContacts.Clear();

    public void SetAvailable(bool available)
    {
        foreach (var renderer in visualRenderers) renderer.enabled = available;
        startText.gameObject.SetActive(available);
        startText.text = "ВОЙДИТЕ — СТАРТ";
    }
}
