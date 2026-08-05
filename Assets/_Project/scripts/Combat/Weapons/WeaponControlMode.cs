using System;
using UnityEngine;

public enum WeaponControlMode
{
    Manual = 0,
    AutoAim = 1
}

public static class WeaponControlSettings
{
    private const string PreferenceKey = "accessibility.weapon.autoAim";

    private static bool loaded;
    private static WeaponControlMode currentMode;

    public static event Action<WeaponControlMode> ModeChanged;

    public static WeaponControlMode CurrentMode
    {
        get
        {
            EnsureLoaded();
            return currentMode;
        }
    }

    public static bool AutomaticFireEnabled =>
        CurrentMode == WeaponControlMode.AutoAim;

    public static void SetAutomaticFire(bool enabled)
    {
        SetMode(enabled
            ? WeaponControlMode.AutoAim
            : WeaponControlMode.Manual);
    }

    public static void SetMode(WeaponControlMode mode)
    {
        EnsureLoaded();

        if (currentMode == mode)
            return;

        currentMode = mode;
        PlayerPrefs.SetInt(PreferenceKey, (int)currentMode);
        PlayerPrefs.Save();
        ModeChanged?.Invoke(currentMode);
    }

    private static void EnsureLoaded()
    {
        if (loaded)
            return;

        int storedValue = PlayerPrefs.GetInt(
            PreferenceKey,
            (int)WeaponControlMode.Manual
        );

        currentMode = Enum.IsDefined(typeof(WeaponControlMode), storedValue)
            ? (WeaponControlMode)storedValue
            : WeaponControlMode.Manual;
        loaded = true;
    }

    [RuntimeInitializeOnLoadMethod(
        RuntimeInitializeLoadType.SubsystemRegistration
    )]
    private static void ResetRuntimeState()
    {
        loaded = false;
        currentMode = WeaponControlMode.Manual;
        ModeChanged = null;
    }
}
