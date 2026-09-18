using System;
using UnityEngine;

public enum ThreatTier
{
    Tier1 = 1,
    Tier2 = 2,
    Tier3 = 3,
    Tier4 = 4
}

public static class ThreatTierPresentation
{
    public const int TierCount = 4;
    public const float Tier2Minimum = 25f;
    public const float Tier3Minimum = 50f;
    public const float Tier4Minimum = 75f;

    public static ThreatTier FromPressure(float pressure)
    {
        float value = Mathf.Clamp(pressure, 0f, 100f);

        if (value >= Tier4Minimum)
            return ThreatTier.Tier4;

        if (value >= Tier3Minimum)
            return ThreatTier.Tier3;

        return value >= Tier2Minimum
            ? ThreatTier.Tier2
            : ThreatTier.Tier1;
    }

    public static string ToRoman(ThreatTier tier)
    {
        return tier switch
        {
            ThreatTier.Tier2 => "II",
            ThreatTier.Tier3 => "III",
            ThreatTier.Tier4 => "IV",
            _ => "I"
        };
    }

    public static string Format(ThreatTier tier)
    {
        return $"{ToRoman(tier)} / {ToRoman(ThreatTier.Tier4)}";
    }
}

[CreateAssetMenu(
    fileName = "RunThreatConfig",
    menuName = "Game/Run/Threat Config"
)]
public sealed class RunThreatConfig : ScriptableObject
{
    [Serializable]
    public sealed class Preset
    {
        [Range(0f, 100f)] public float minimumValue;
        [Min(0.1f)] public float spawnIntervalMultiplier = 1f;
        [Min(1)] public int maxAliveCap = 20;
        [Min(1)] public int batchSize = 1;
    }

    [SerializeField, Min(0f)] private float valuePerSecond = 0.12f;
    [SerializeField] private Preset[] presets = Array.Empty<Preset>();
    [Header("Early run pressure")]
    [SerializeField, Range(.5f, 1f)] private float earlyMeleeSpeed = .87f;
    [SerializeField, Min(.01f)] private float earlyMeleeResponseSeconds = .3f;
    [SerializeField, Min(0f)] private float openingDuration = 55f;
    [SerializeField, Min(1f)] private float openingSpawnIntervalMultiplier = 1.3f;
    [SerializeField, Min(1)] private int openingAliveCap = 12;

    public float MeleeSpeed(float pressure) => Mathf.Lerp(earlyMeleeSpeed, 1f,
        Mathf.InverseLerp(ThreatTierPresentation.Tier2Minimum, ThreatTierPresentation.Tier3Minimum, pressure));
    public float MeleeResponseSeconds(float pressure) => Mathf.Lerp(earlyMeleeResponseSeconds, 0f,
        Mathf.InverseLerp(ThreatTierPresentation.Tier2Minimum, ThreatTierPresentation.Tier3Minimum, pressure));
    public bool IsOpening(float pressure, float elapsed) => elapsed < openingDuration &&
        pressure < ThreatTierPresentation.Tier2Minimum;
    public float OpeningSpawnIntervalMultiplier => openingSpawnIntervalMultiplier;
    public int OpeningAliveCap => openingAliveCap;

    public float ValuePerSecond => Mathf.Max(0f, valuePerSecond);
    public int PresetCount => presets != null ? presets.Length : 0;

    public int GetPresetIndex(float threatValue)
    {
        if (presets == null || presets.Length == 0)
            return 0;

        float value = Mathf.Clamp(threatValue, 0f, 100f);
        int result = 0;

        for (int i = 0; i < presets.Length; i++)
        {
            Preset preset = presets[i];

            if (preset != null && value >= preset.minimumValue)
                result = i;
        }

        return Mathf.Clamp(result, 0, presets.Length - 1);
    }

    public Preset GetPreset(int index)
    {
        if (presets == null || presets.Length == 0)
            return null;

        return presets[Mathf.Clamp(index, 0, presets.Length - 1)];
    }
}
