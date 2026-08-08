using UnityEngine;

public sealed class BunkerMinigameTerminal : MonoBehaviour
{
    [SerializeField] private BunkerMinigame minigame;

    public void Interact()
    {
        Debug.Log("[Minigame] Terminal interact: Football");

        if (minigame != null && minigame.CanStart)
            minigame.StartGame();
    }
}
