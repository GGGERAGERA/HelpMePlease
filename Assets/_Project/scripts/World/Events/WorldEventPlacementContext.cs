using System;
using UnityEngine;
public readonly struct WorldEventPlacementContext
{
    public readonly Vector2 Origin;
    public readonly Rect PlayableArea;
    public readonly Rect? SiteStartBounds;
    public readonly int Seed;
    private readonly Func<Rect, bool> footprintClear;
    public WorldEventPlacementContext(Vector2 origin, Rect playable, Rect? site, int seed, Func<Rect,bool> clear)
    { Origin = origin; PlayableArea = playable; SiteStartBounds = site; Seed = seed; footprintClear = clear; }
    public bool IsStaticFootprintClear(Rect footprint) => footprintClear == null || footprintClear(footprint);
}
