using UnityEngine;

public sealed class WeaponCoreDebugSelector : MonoBehaviour
{
    public static WeaponCoreType ActiveCore { get; private set; }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        if (FindFirstObjectByType<WeaponCoreDebugSelector>() != null)
            return;

        GameObject selector = new("Weapon Core Debug Selector");
        DontDestroyOnLoad(selector);
        selector.AddComponent<WeaponCoreDebugSelector>();
        LogSelection();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticState()
    {
        ActiveCore = WeaponCoreType.None;
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Alpha0))
            Select(WeaponCoreType.None);
        else if (Input.GetKeyDown(KeyCode.Alpha2))
            Select(WeaponCoreType.Chain);

        if (Input.GetKeyDown(KeyCode.F5))
        {
            Select(ActiveCore == WeaponCoreType.Chain
                ? WeaponCoreType.None
                : WeaponCoreType.Chain);
        }
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
            $"[WeaponCoreDebug] Active Core: {ActiveCore}. " +
            "0=None, 2=Chain; F5 toggles None/Chain."
        );
    }
#endif
}
