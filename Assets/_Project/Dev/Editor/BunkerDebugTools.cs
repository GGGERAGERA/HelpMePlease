#if UNITY_EDITOR
using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Debug = UnityEngine.Debug;

// Fixed opt-in commands reuse TutorialVerificationRunner; this is not a test runner.
[InitializeOnLoad]
public static class BunkerDebugTools
{
    public const string Output = "Artifacts/GeneratedQA/BunkerDebug";
    private const string ShowOnPlay = "Subject42.BunkerDebug.ShowOnPlay";
    private const string MainMenu = "Assets/_Project/Scenes/MainBuild/MainMenu.unity";
    static BunkerDebugTools() => EditorApplication.update += Poll;

    [MenuItem("Tools/Subject42/Dev/Bunker/Play isolated MainMenu")]
    public static void PlayIsolated()
    {
        RequireEditMode();
        SetGameView1600();
        EditorSceneManager.OpenScene(MainMenu);
        if (!PlayerPrefs.IsSandboxActive) PlayerPrefs.BeginSandbox(false);
        PlayerPrefs.SetInt(BunkerIntroController.ViewedPreferenceKey, 1);
        PlayerPrefs.SetInt(TutorialController.CompletionKey, 1);
        SessionState.SetBool(ShowOnPlay, true);
        EditorApplication.EnterPlaymode();
    }
    [MenuItem("Tools/Subject42/QA/Fast smoke/Bunker debug")]
    public static void SmokeBunker() => Queue("BunkerDebugStorageTests;BunkerDebugSmokeTests.IsolatedBunkerControlsAndProductionReturn;BunkerDebugSmokeTests.RestoreMainDiscardsPendingAudioAndLanguageCaches;BunkerDebugSmokeTests.FreshTestStagesUseMissionReceiptTaskRewardAndEscapeAPIs");
    [MenuItem("Tools/Subject42/QA/Fast smoke/MVP event reward and return")]
    public static void SmokeMvp() => Queue("BunkerDebugSmokeTests.MvpEventRewardAndReturnInIsolatedProfile");
    private static void Queue(string filter)
    {
        RequireEditMode();
        if (SessionState.GetBool("Subject42.Verification.AnyRunActive", false)) throw new InvalidOperationException("Another verification is running.");
        const string request = "Artifacts/GeneratedQA/Tutorial/run-tests.request";
        if (File.Exists(request)) throw new InvalidOperationException("A test request is already queued.");
        SetGameView1600();
        Directory.CreateDirectory(Path.GetDirectoryName(request));
        File.WriteAllText(request + ".tmp", filter);
        File.Move(request + ".tmp", request);
    }
    private static void RequireEditMode()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
            throw new InvalidOperationException("Stop Play Mode and wait for import/compilation.");
        for (int i = 0; i < SceneManager.sceneCount; i++)
            if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save or discard your modified scenes before this command.");
    }
    public static void SetGameView1600()
    {
        // Unity 6000.3 API, shared with the existing presentation checks.
        var assembly = typeof(Editor).Assembly;
        var sizesType = assembly.GetType("UnityEditor.GameViewSizes");
        var sizes = typeof(ScriptableSingleton<>).MakeGenericType(sizesType).GetProperty("instance").GetValue(null);
        var group = sizesType.GetMethod("GetGroup").Invoke(sizes, new object[] { 0 });
        var type = group.GetType();
        int count = (int)type.GetMethod("GetTotalCount").Invoke(group, null), index = -1;
        for (int i = 0; i < count; i++)
        {
            var size = type.GetMethod("GetGameViewSize").Invoke(group, new object[] { i });
            if ((int)size.GetType().GetProperty("width").GetValue(size) == 1600 &&
                (int)size.GetType().GetProperty("height").GetValue(size) == 900) { index = i; break; }
        }
        if (index < 0)
        {
            var size = Activator.CreateInstance(assembly.GetType("UnityEditor.GameViewSize"), new object[]
                { Enum.ToObject(assembly.GetType("UnityEditor.GameViewSizeType"), 1), 1600, 900, "Subject42 QA 1600×900" });
            type.GetMethod("AddCustomSize").Invoke(group, new[] { size }); index = count;
        }
        var view = EditorWindow.GetWindow(assembly.GetType("UnityEditor.GameView"));
        view.GetType().GetProperty("selectedSizeIndex").SetValue(view, index); view.Show();
    }
    private static void Poll()
    {
        if (EditorApplication.isPlaying && SessionState.GetBool(ShowOnPlay, false) &&
            ProductionSceneComposition.Active is { IsReady: true } && BunkerDebugController.Instance != null)
        {
            SessionState.EraseBool(ShowOnPlay);
            BunkerDebugController.Instance.SetVisible(true);
        }
        string request = Output + "/editor.request";
        if (!File.Exists(request) || EditorApplication.isCompiling || EditorApplication.isUpdating ||
            SessionState.GetBool("Subject42.Verification.AnyRunActive", false)) return;
        string action = File.ReadAllText(request).Trim(); File.Delete(request);
        Directory.CreateDirectory(Output);
        try
        {
            switch (action)
            {
                case "play-copy": PlayIsolated(); break;
                case "open": RequireEditMode(); EditorSceneManager.OpenScene(MainMenu); break;
                case "stop": EditorApplication.ExitPlaymode(); break;
                case "bunker-smoke": SmokeBunker(); break;
                case "mvp-smoke": SmokeMvp(); break;
                case "measure-scene":
                    RequireEditMode(); var watch = Stopwatch.StartNew(); EditorSceneManager.OpenScene(MainMenu); watch.Stop();
                    File.WriteAllText(Output + "/scene-timing.txt", $"MainMenu OpenScene: {watch.Elapsed.TotalMilliseconds:F1} ms\n"); break;
                case "verify-player-types": VerifyPlayerTypes(); break;
                case "state": WriteState(); break;
                case "restore-test": BunkerDebugController.Instance?.RestoreTestStart(); break;
                default:
                    if (action.StartsWith("capture-") && int.TryParse(action.Substring(8), out int tab) && tab >= 0 && tab < 5 && BunkerDebugController.Instance != null)
                    {
                        BunkerDebugController.Instance.SelectTab(tab); BunkerDebugController.Instance.SetVisible(true);
                        EditorApplication.delayCall += () => ScreenCapture.CaptureScreenshot(Output + "/panel-" + tab + ".png");
                    }
                    else throw new ArgumentException("Unknown fixed bunker command: " + action);
                    break;
            }
        }
        catch (Exception error) { File.WriteAllText(Output + "/editor-error.txt", error.ToString()); Debug.LogException(error); }
    }
    private static void WriteState() => File.WriteAllText(Output + "/editor-state.txt",
        $"scene={SceneManager.GetActiveScene().name}\nplaying={EditorApplication.isPlaying}\nsandbox={PlayerPrefs.IsSandboxActive}\n" +
        $"debug={BunkerDebugController.Instance != null}\nvisible={BunkerDebugController.Instance?.Visible}\n" +
        $"gold={CurrencyManager.Instance?.TotalGold}\ncores={CurrencyManager.Instance?.TotalCores}\nresolution={Screen.width}x{Screen.height}\n");
    private static void VerifyPlayerTypes()
    {
        foreach (string configuration in new[] { "Release", "Development" })
        {
            string path = "Artifacts/GeneratedQA/Phase6/Compilation/" + configuration + "/Assembly-CSharp.dll";
            var assembly = Assembly.Load(File.ReadAllBytes(path));
            bool expected = configuration == "Development";
            foreach (string name in new[] { "BunkerDebugController", "BunkerDebugView" })
                if ((assembly.GetType(name) != null) != expected) throw new InvalidOperationException(configuration + " has wrong type boundary: " + name);
            if ((assembly.GetType("PlayerPrefs").GetMethod("BeginSandbox") != null) != expected)
                throw new InvalidOperationException(configuration + " has wrong sandbox API boundary.");
        }
        File.WriteAllText(Output + "/player-type-boundary.txt", "PASS: debug controller, view and sandbox API absent in Release; present in Development.\n");
    }
}
#endif
