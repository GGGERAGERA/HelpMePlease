using UnityEngine;
public sealed class CorridorGate
{
    private readonly CorridorRoute route;
    private readonly float distance;
    public CorridorGate(CorridorRoute route, int ordinal)
    { this.route = route; distance = route.CheckpointDistance(ordinal); }
    public bool Crossed(Vector2 previous, Vector2 current) => route.Crosses(distance, previous, current);
}
