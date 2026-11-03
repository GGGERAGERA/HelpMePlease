#if UNITY_EDITOR || DEVELOPMENT_BUILD
using UnityEngine;

// Display changes apply immediately rather than waiting for PlayerPrefs.Save.
// Preserve the real pre-test display, including a development player's forced startup mode.
public static class BunkerDebugDisplayState
{
    private static bool captured;
    private static int width, height;
    private static FullScreenMode mode;
    public static void Capture()
    {
        if (Application.isEditor || captured) return;
        width = Screen.width; height = Screen.height; mode = Screen.fullScreenMode;
        captured = true;
    }
    public static void Restore()
    {
        if (!Application.isEditor && captured) Screen.SetResolution(width, height, mode);
    }
    public static void Forget() => captured = false;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reset() => Forget();
}
#endif
