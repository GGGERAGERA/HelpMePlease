using UnityEngine;
public sealed class CorridorExit
{
    private readonly CorridorRoute route;
    public CorridorExit(CorridorRoute route) => this.route = route;
    public bool Crossed(Vector2 previous, Vector2 current, bool isOpen) =>
        isOpen && route.Crosses(route.Length, previous, current);
}
