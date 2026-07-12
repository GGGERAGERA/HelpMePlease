using UnityEngine;

public class DoorInteractable : Interactable
{
    [Header("Teleport")]
    [SerializeField] private Transform targetPoint;
    [SerializeField] private Transform player;

    public override void Interact()
    {
        if (targetPoint == null)
        {
            Debug.LogWarning("DoorInteractable: targetPoint is not assigned.");
            return;
        }

        if (player == null)
        {
            GameObject playerObject = GameObject.FindGameObjectWithTag("Player");

            if (playerObject != null)
                player = playerObject.transform;
        }

        if (player == null)
        {
            Debug.LogWarning("DoorInteractable: player not found.");
            return;
        }

        player.position = targetPoint.position;
    }
}