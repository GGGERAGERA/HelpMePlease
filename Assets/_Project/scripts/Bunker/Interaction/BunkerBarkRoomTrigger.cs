using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent, RequireComponent(typeof(BoxCollider2D))]
public sealed class BunkerBarkRoomTrigger : MonoBehaviour
{
    [SerializeField] private BunkerAmbientBarks barks;
    [SerializeField] private string roomId;
    private readonly HashSet<Collider2D> occupants = new();
    public string RoomId => roomId;

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (barks == null || other.isTrigger || other.attachedRigidbody == null ||
            other.attachedRigidbody.transform != barks.Player) return;
        bool wasEmpty = occupants.Count == 0;
        occupants.Add(other);
        if (wasEmpty) barks.TryEnterRoom(roomId);
    }

    private void OnTriggerExit2D(Collider2D other) => occupants.Remove(other);
    private void OnDisable() => occupants.Clear();
    private void Reset() => GetComponent<BoxCollider2D>().isTrigger = true;
}
