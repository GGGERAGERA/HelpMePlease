using UnityEngine;

public sealed class AnomalyRunModifiers
{
    public static AnomalyRunModifiers None { get; } = new();

    public float ZoneSizeMultiplier { get; private set; } = 1f;
    public float GoldInsideAnomalyMultiplier { get; private set; } = 1f;
    public float StasisPlayerEffectMultiplier { get; private set; } = 1f;
    public float GravityPlayerForceMultiplier { get; private set; } = 1f;

    public static AnomalyRunModifiers From(AnomalyStabilizerData stabilizer)
    {
        AnomalyRunModifiers modifiers = new();

        if (stabilizer == null)
            return modifiers;

        float value = Mathf.Max(0f, stabilizer.Value);

        switch (stabilizer.EffectType)
        {
            case AnomalyStabilizerEffectType.ZoneSize:
                modifiers.ZoneSizeMultiplier = value;
                break;

            case AnomalyStabilizerEffectType.GoldInsideAnomaly:
                modifiers.GoldInsideAnomalyMultiplier = Mathf.Max(1f, value);
                break;

            case AnomalyStabilizerEffectType.StasisPlayerEffect:
                modifiers.StasisPlayerEffectMultiplier = Mathf.Clamp01(value);
                break;

            case AnomalyStabilizerEffectType.GravityPlayerForce:
                modifiers.GravityPlayerForceMultiplier = Mathf.Clamp01(value);
                break;
        }

        return modifiers;
    }
}
