#if DEVELOPMENT_BUILD && !UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using UnityEngine;
using Subject42.Combat.OrbitalStation;

// Opt-in observations and production run startup for the single standalone smoke.
// F1 input and preset application remain manual UI interactions.
public sealed class TrailerStandaloneDiagnostics : MonoBehaviour
{
    private bool wasOpen;
    private Vector3 lastPosition;
    private float movement;
    private bool observingMovement;

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
        var player = PlayerRuntimeReference.CachedPlayer;
        if (observingMovement && player != null)
        {
            movement += Vector3.Distance(lastPosition, player.transform.position);
            lastPosition = player.transform.position;
        }
        bool open = Subject42DebugMenu.IsDebugMenuOpen;
        if (open == wasOpen) return;
        wasOpen = open;
        var station = player != null ? player.GetComponentInChildren<OrbitalStationRuntime>() : null;
        Debug.Log($"[TrailerStandalone] F1 open={open} timeScale={Time.timeScale} rings={station?.Rings.Count} modules={station?.Modules.Count} blocked={station?.InputOwner?.IsGameplayInputBlocked} developmentRun={RunStateManager.Instance?.IsDevelopmentRun}");
        if (!open && station != null) StartCoroutine(ObserveResume(station));
    }

    private IEnumerator ObserveResume(OrbitalStationRuntime station)
    {
        float start = Time.time;
        movement = 0f;
        var player = PlayerRuntimeReference.CachedPlayer;
        lastPosition = player != null ? player.transform.position : Vector3.zero;
        observingMovement = true;
        yield return new WaitForSecondsRealtime(3f);
        observingMovement = false;
        Debug.Log($"[TrailerStandalone] resume elapsedGameplay={Time.time - start:0.000} movement={movement:0.000} timeScale={Time.timeScale} blocked={station?.InputOwner?.IsGameplayInputBlocked} fullscreen={Screen.fullScreen} screen={Screen.width}x{Screen.height}");
    }
}
#endif
