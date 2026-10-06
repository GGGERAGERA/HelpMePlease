using System.Collections.Generic;
using UnityEngine;
public static class CorridorPlacement
{
    // Includes endpoint caps and the rail's authored thickness; no circle-only approximation.
    public static IEnumerable<Rect> Footprints(CorridorRoute route)
    {
        float padding = route.HalfWidth + .4f;
        for (int i = 1; i < route.Vertices.Count; i++)
        {
            Vector2 min = Vector2.Min(route.Vertices[i-1], route.Vertices[i]) - Vector2.one * padding;
            Vector2 max = Vector2.Max(route.Vertices[i-1], route.Vertices[i]) + Vector2.one * padding;
            yield return new Rect(min, max-min);
        }
    }
    public static bool Admitted(CorridorRoute route, WorldEventPlacementContext context, out string error)
    {
        error = null;
        if (context.SiteStartBounds.HasValue && !context.SiteStartBounds.Value.Contains(route.Vertices[0]))
        { error = "Start lies outside site."; return false; }
        foreach (var box in Footprints(route))
        {
            if (box.xMin < context.PlayableArea.xMin || box.xMax > context.PlayableArea.xMax ||
                box.yMin < context.PlayableArea.yMin || box.yMax > context.PlayableArea.yMax)
            { error = "Whole route does not fit playable bounds."; return false; }
            if (!context.IsStaticFootprintClear(box)) { error = "Route intersects static obstacle or reserved exit."; return false; }
        }
        return true;
    }
    public static bool TryPrepare(CorridorSettings settings, WorldEventPlacementContext context,
        bool explicitSelection, int quarterTurns, out CorridorRoute route, out int selectedTurns, out string error)
    {
        route = null; error = null; selectedTurns = quarterTurns;
        int seed = settings.routeSeed != 0 ? settings.routeSeed : context.Seed;
        if (seed == 0) seed = 1;
        int candidates = explicitSelection || settings.routePreset != CorridorRouteMode.Random ? 1 : 16;
        for (int i = 0; i < candidates; i++)
        {
            int effectiveSeed = i == 0 ? seed : unchecked(seed + (i / 4) * 7919);
            int turn = candidates == 1 ? quarterTurns : (quarterTurns+i)%4;
            if (CorridorRouteBuilder.TryBuild(settings, settings.routePreset, effectiveSeed, turn,
                context.Origin, out var candidate, out error) && Admitted(candidate, context, out error))
            { route = candidate; selectedTurns = turn; return true; }
        }
        return false;
    }
}
