using System;
using UnityEngine;

[CreateAssetMenu(menuName = "Subject42/World Events/Orbital Relay")]
public sealed class OrbitalRelayConfig : ScriptableObject
{
    [Min(1)] public int requiredActivations = 3;
    [Min(.1f)] public float stabilizationDuration = 20f;
    [Min(.05f)] public float stabilizationContactTime = .6f;
    [Min(.05f)] public float transitionDuration = .75f;
    [Min(.1f)] public float bonusDuration = 15f;
    [Min(0)] public int goldPerActivation = 10;
    [Min(.1f)] public float comboWindow = 2.5f;
    [Min(1f)] public float bonusEnemyPressureMultiplier = 1.6f;

    public bool TryGetSettings(out OrbitalRelaySettings settings, out string error)
    {
        settings = new OrbitalRelaySettings(requiredActivations, stabilizationDuration,
            stabilizationContactTime, transitionDuration, bonusDuration,
            goldPerActivation, comboWindow, bonusEnemyPressureMultiplier);
        return settings.TryValidate(out error);
    }
}

public readonly struct OrbitalRelaySettings
{
    public readonly int RequiredActivations, GoldPerActivation;
    public readonly float StabilizationDuration, ContactTime, TransitionDuration,
        BonusDuration, ComboWindow, BonusEnemyPressureMultiplier;
    public OrbitalRelaySettings(int requiredActivations, float stabilizationDuration,
        float contactTime, float transitionDuration, float bonusDuration,
        int goldPerActivation, float comboWindow, float bonusEnemyPressureMultiplier)
    {
        RequiredActivations = requiredActivations;
        StabilizationDuration = stabilizationDuration;
        ContactTime = contactTime;
        TransitionDuration = transitionDuration;
        BonusDuration = bonusDuration;
        GoldPerActivation = goldPerActivation;
        ComboWindow = comboWindow;
        BonusEnemyPressureMultiplier = bonusEnemyPressureMultiplier;
    }
    public bool TryValidate(out string error)
    {
        bool Positive(float v) => v > 0 && !float.IsNaN(v) && !float.IsInfinity(v);
        if (RequiredActivations < 1 || GoldPerActivation < 0 ||
            !Positive(StabilizationDuration) || !Positive(ContactTime) ||
            !Positive(TransitionDuration) || !Positive(BonusDuration) ||
            !Positive(ComboWindow) || !Positive(BonusEnemyPressureMultiplier) ||
            BonusEnemyPressureMultiplier < 1)
        { error = "Orbital Relay settings contain invalid counts, durations or pressure."; return false; }
        error = null;
        return true;
    }
}
