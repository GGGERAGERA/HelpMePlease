using System;
using UnityEngine;

public static class CorridorRouteBuilder
{
    public static bool HasDefinition(CorridorSettings settings, CorridorRouteMode mode)
    {
        if (settings?.routeDefinitions == null) return false;
        foreach (var definition in settings.routeDefinitions)
            if (definition != null && definition.preset == mode && definition.checkpointEndAnchors?.Length == 3)
                return true;
        return false;
    }
    public static bool TryBuild(CorridorSettings settings, CorridorRouteMode mode, int seed,
        int quarterTurns, Vector2 start, out CorridorRoute route, out string error)
    {
        route = null; error = null;
        if (settings == null || settings.checkpointCount != 3 ||
            (mode != CorridorRouteMode.Random && !HasDefinition(settings, mode)))
        { error = "Invalid or missing authored route definition."; return false; }
        try
        {
            var data = settings.Snapshot(); data.routePreset = mode; data.routeSeed = seed;
            route = new CorridorRoute(data, quarterTurns, start);
            for (int i = 1; i < route.Vertices.Count; i++)
            {
                var delta = route.Vertices[i] - route.Vertices[i - 1];
                if (delta.sqrMagnitude < .01f || (Mathf.Abs(delta.x) > .01f && Mathf.Abs(delta.y) > .01f))
                { route = null; error = "Route must use distinct cardinal anchors."; return false; }
            }
            return true;
        }
        catch (ArgumentException exception) { error = exception.Message; return false; }
    }
}
