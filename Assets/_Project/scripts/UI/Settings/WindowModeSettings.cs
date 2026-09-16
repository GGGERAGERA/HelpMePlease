using UnityEngine;

public static class WindowModeSettings
{
    public const string FullscreenKey = "display.fullscreen";
    public static bool Fullscreen => PlayerPrefs.GetInt(FullscreenKey, Screen.fullScreen ? 1 : 0) != 0;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Restore()
    {
        if (PlayerPrefs.HasKey(FullscreenKey)) Apply(Fullscreen);
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
