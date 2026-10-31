#if DEVELOPMENT_BUILD && !UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using UnityEngine;

// Opt-in observations and production run startup for the single standalone smoke.
// Production startup and F12 frame capture remain independent of debug menus.
public sealed class TrailerStandaloneDiagnostics : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Initialize()
    {
        // This dedicated capture executable has a separate profile and starts with
        // the current ORBITAL run. Do not save tutorial completion or unlocks.
        if (Application.productName == "Subject42 Trailer Development")
            RunDevelopmentOverrides.SuppressTutorial = () => true;
        if (Array.IndexOf(Environment.GetCommandLineArgs(), "--trailer-smoke") < 0) return;
        // Test startup skips the tutorial only in memory, without a saved unlock.
        RunDevelopmentOverrides.SuppressTutorial = () => true;
        GameplayRandom.Begin(12345);
        var owner = new GameObject("TrailerStandaloneDiagnostics");
        DontDestroyOnLoad(owner);
        owner.AddComponent<TrailerStandaloneDiagnostics>();
    }

    private IEnumerator Start()
    {
        yield return null;
        Debug.Log($"[TrailerStandalone] startup development={Debug.isDebugBuild} editor={Application.isEditor} screen={Screen.width}x{Screen.height} fullscreen={Screen.fullScreen} mode={Screen.fullScreenMode} watermark={UnityEngine.Rendering.Watermark.IsVisible()} product={Application.productName} savePath={Application.persistentDataPath}");
        var config = Resources.Load<TrailerCaptureConfig>("TrailerCapturePresets");
        Debug.Log($"[TrailerStandalone] assets config={config != null} presets={config?.presets.Length} rules={config?.worldRules.Length} rocket={config != null && config.rocketStormPrefab != null}");
        float deadline = Time.realtimeSinceStartup + 120f;
        BunkerRunStarter starter = null;
        while (Time.realtimeSinceStartup < deadline)
        {
            starter = FindFirstObjectByType<BunkerRunStarter>();
            if (starter != null && RunSelectionManager.Instance?.SelectedCharacter != null && !SceneTransitionOverlay.IsTransitioning) break;
            yield return null;
        }
        if (starter != null)
        {
            Debug.Log("[TrailerStandalone] starting ordinary run through BunkerRunStarter");
            starter.StartRun(starter.transform);
        }
    }

    private void Update()
    {
        if (Input.GetMouseButtonDown(0)) Debug.Log($"[TrailerStandalone] click mouse={Input.mousePosition} display={Screen.currentResolution.width}x{Screen.currentResolution.height}");
        // When Windows Graphics Capture cannot capture the fullscreen swapchain,
        // F12 supplies an actual rendered game frame for this opt-in smoke only.
        if (Input.GetKeyDown(KeyCode.F12))
        {
            string path = Path.Combine(Application.persistentDataPath, "TrailerSmoke.png");
            ScreenCapture.CaptureScreenshot(path);
            Debug.Log("[TrailerStandalone] screenshot=" + path);
        }
    }

}
#endif
