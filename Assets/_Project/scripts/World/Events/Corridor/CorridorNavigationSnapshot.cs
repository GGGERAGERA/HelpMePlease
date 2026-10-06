using System.Collections.Generic;
using UnityEngine;
public readonly struct CorridorStrikeThreat
{
    public readonly Vector2 Position;
    public readonly float Radius, Remaining;
    public CorridorStrikeThreat(Vector2 position, float radius, float remaining)
    { Position = position; Radius = radius; Remaining = remaining; }
}
public readonly struct CorridorNavigationSnapshot
{
    public readonly CorridorRoute Route;
    public readonly CorridorPhase Phase;
    public readonly int Completed;
    public readonly bool ExitOpen;
    public readonly float FrontDistance;
    public readonly IReadOnlyList<CorridorStrikeThreat> Threats;
    public CorridorNavigationSnapshot(CorridorRoute route, CorridorRuntimeState state, float front,
        IReadOnlyList<CorridorStrikeThreat> threats)
    { Route = route; Phase = state.Phase; Completed = state.Completed; ExitOpen = state.ExitOpen;
        FrontDistance = front; Threats = threats; }
    public Vector2 Objective
    {
        get
        {
            if (Completed < Route.CheckpointCount)
            {
                Vector2 gate=Route.Point(Route.CheckpointDistance(Completed),out var incoming);
                return gate+incoming; // At a bend, cross the incoming gate plane before turning.
            }
            Vector2 end = Route.Point(Route.Length, out var forward);
            return end + forward * (ExitOpen ? 1 : -2);
        }
    }
}
