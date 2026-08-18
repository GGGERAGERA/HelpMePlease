using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider2D))]
public sealed class FootballPlayerArea : MonoBehaviour
{
    [SerializeField] private FootballMinigame minigame;

    private readonly HashSet<Collider2D> playerColliders = new();

    public void Configure(FootballMinigame owner)
    {
        minigame = owner;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (IsPlayer(other))
            playerColliders.Add(other);
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (!IsPlayer(other))
            return;

        playerColliders.Remove(other);
        playerColliders.RemoveWhere(item => item == null);
        if (playerColliders.Count == 0)
            minigame?.CancelCurrentRound();
    }

    private void OnDisable()
    {
        playerColliders.Clear();
    }

    private static bool IsPlayer(Collider2D other)
    {
        return other != null && other.GetComponentInParent<CharacterMovement2D>() != null;
    }
}
