using UnityEngine;

/// <summary>Shared station UI palette. Components and hierarchy live in prefabs.</summary>
public static class StationPixelVisuals
{
    public static readonly Color Window = new(0.018f, 0.031f, 0.043f, 0.98f);
    public static readonly Color Panel = new(0.027f, 0.055f, 0.071f, 0.98f);
    public static readonly Color PanelRaised = new(0.04f, 0.086f, 0.105f, 1f);
    public static readonly Color Cyan = new(0.08f, 0.82f, 0.86f, 1f);
    public static readonly Color CyanMuted = new(0.08f, 0.42f, 0.46f, 1f);
    public static readonly Color SectionBorder = new(0.07f, 0.25f, 0.29f, 1f);
    public static readonly Color Text = new(0.9f, 0.94f, 0.95f, 1f);
    public static readonly Color MutedText = new(0.58f, 0.68f, 0.7f, 1f);
    public static readonly Color Gold = new(0.95f, 0.72f, 0.2f, 1f);
    public static readonly Color Disabled = new(0.24f, 0.29f, 0.3f, 1f);
}
