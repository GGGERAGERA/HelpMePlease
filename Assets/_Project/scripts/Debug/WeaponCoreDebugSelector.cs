using UnityEngine;

public enum WeaponCoreType
{
    None = 0,
    Rupture = 1,
    Chain = 2,
    Void = 3
}

public sealed class WeaponCoreDebugSelector : MonoBehaviour
{
    public static WeaponCoreType ActiveCore { get; private set; }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticState()
    {
        ActiveCore = WeaponCoreType.None;
    }

    public static void Select(WeaponCoreType core)
    {
        if (ActiveCore == core)
            return;

        ActiveCore = core;
        LogSelection();
    }

    private static void LogSelection()
    {
        Debug.Log(
            $"[WeaponCoreDebug] Active Core: {ActiveCore}."
        );
    }
#endif
}
