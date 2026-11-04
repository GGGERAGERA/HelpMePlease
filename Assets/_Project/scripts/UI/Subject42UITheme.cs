using UnityEngine;

/// <summary>Semantic colors shared by the game's UI and HUD.</summary>
public static class Subject42UITheme
{
    public static readonly Color32 Background = new(0x10, 0x19, 0x1F, 255);
    public static readonly Color32 Surface = new(0x1C, 0x2B, 0x32, 255);
    public static readonly Color32 Border = new(0x50, 0x6A, 0x72, 255);
    public static readonly Color32 PrimaryText = new(0xE7, 0xED, 0xE5, 255);
    public static readonly Color32 SecondaryText = new(0x98, 0xAD, 0xB1, 255);
    public static readonly Color32 Cyan = new(0x38, 0xD9, 0xE6, 255);
    public static readonly Color32 Amber = new(0xF2, 0xB9, 0x4B, 255);
    public static readonly Color32 Green = new(0x79, 0xD5, 0xA4, 255);
    public static readonly Color32 Coral = new(0xF1, 0x78, 0x69, 255);
    public static readonly Color32 Purple = new(0xB6, 0x9A, 0xF0, 255);

    public static Color32 WorldRule(WorldRuleType rule) => rule switch
    {
        WorldRuleType.Snow => PrimaryText,
        WorldRuleType.Rain => new Color32(0x8C, 0xB9, 0xFA, 255),
        WorldRuleType.Darkness => Purple,
        WorldRuleType.Wind => Cyan,
        WorldRuleType.Golden => Amber,
        WorldRuleType.Condensation => Green,
        _ => SecondaryText
    };
}
