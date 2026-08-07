using System.Collections.Generic;
using UnityEngine;

public enum TacticalMapMarkerKind
{
    Event,
    Objective,
    Target,
    Corridor
}

public readonly struct TacticalMapMarkerDescriptor
{
    public TacticalMapMarkerKind Kind { get; }
    public Vector2 Position { get; }
    public Vector2 Size { get; }
    public float Rotation { get; }

    public bool IsArea => Size.x > Mathf.Epsilon && Size.y > Mathf.Epsilon;

    public TacticalMapMarkerDescriptor(
        TacticalMapMarkerKind kind,
        Vector2 position,
        Vector2 size = default,
        float rotation = 0f)
    {
        Kind = kind;
        Position = position;
        Size = size;
        Rotation = rotation;
    }
}

public interface ITacticalMapMarkerProvider
{
    void CollectTacticalMapMarkers(
        List<TacticalMapMarkerDescriptor> markers);
}
