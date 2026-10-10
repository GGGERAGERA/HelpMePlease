using UnityEngine;

public static class WindowModeSettings
{
    public const string FullscreenKey = "display.fullscreen";
    public static bool Fullscreen
    {
        get
        {
#if DEVELOPMENT_BUILD && !UNITY_EDITOR
            return Screen.fullScreen;
#else
            return PlayerPrefs.GetInt(FullscreenKey, Screen.fullScreen ? 1 : 0) != 0;
#endif
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Restore()
    {
#if DEVELOPMENT_BUILD && !UNITY_EDITOR
        UnityEngine.Rendering.Watermark.showDeveloperWatermark = false;
        Debug.developerConsoleVisible = false;
        Debug.developerConsoleEnabled = false;
        // Capture builds start predictably, independent of a saved windowed preference.
        // This changes the current display only, without writing a player preference.
        Screen.SetResolution(1920, 1080, FullScreenMode.FullScreenWindow);
        return;
#else
        if (PlayerPrefs.HasKey(FullscreenKey)) Apply(Fullscreen);
#endif
    }

    public static void SetFullscreen(bool fullscreen)
    {
        PlayerPrefs.SetInt(FullscreenKey, fullscreen ? 1 : 0);
        PlayerPrefs.Save();
        Apply(fullscreen);
    }

    private static void Apply(bool fullscreen)
    {
        // EditMode/PlayMode checks must not resize the developer's editor.
        if (Application.isEditor) return;
        Resolution desktop = Screen.currentResolution;
        int width = fullscreen ? desktop.width : Mathf.Min(1280, desktop.width);
        int height = fullscreen ? desktop.height : Mathf.Min(720, desktop.height);
        Screen.SetResolution(width, height,
            fullscreen ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed);
    }
}
