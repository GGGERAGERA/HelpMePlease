#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

// A focused build entry point; does not run the test suite or save open scenes.
[InitializeOnLoad]
public static class TrailerStandaloneBuild
{
    private const string Output = "Artifacts/GeneratedQA/TrailerStandalone";
    static TrailerStandaloneBuild() => EditorApplication.update += CheckRequest;

    private static void CheckRequest()
    {
        string request = Output + "/build.request";
        if (!File.Exists(request) || EditorApplication.isCompiling || EditorApplication.isUpdating ||
            EditorApplication.isPlayingOrWillChangePlaymode || SessionState.GetBool("Subject42.Verification.AnyRunActive", false)) return;
        File.Delete(request);
        Build();
    }

    [MenuItem("Tools/Subject42/Build Trailer Development Standalone")]
    public static void Build()
    {
        Directory.CreateDirectory(Output);
        const string settingsPath = "ProjectSettings/ProjectSettings.asset";
        byte[] savedSettings = File.ReadAllBytes(settingsPath);
        string product = PlayerSettings.productName;
        int width = PlayerSettings.defaultScreenWidth, height = PlayerSettings.defaultScreenHeight;
        var mode = PlayerSettings.fullScreenMode;
        string exe = Path.GetFullPath("Builds/TrailerDevelopment/Subject42.exe");
        try
        {
            var capture = AssetDatabase.LoadAssetAtPath<TrailerCaptureConfig>("Assets/_Project/Dev/Resources/TrailerCapturePresets.asset");
            if (capture == null || capture.presets.Length != 5 || capture.rocketStormPrefab == null ||
                capture.worldRules.Length != 7 || capture.worldRules.Any(r => r == null))
                throw new InvalidOperationException("Trailer capture asset references are incomplete.");
            Directory.CreateDirectory(Path.GetDirectoryName(exe));
            // Unity PlayerPrefs and persistentDataPath use product identity. The trailer
            // executable gets its own save namespace rather than the ordinary release's.
            PlayerSettings.productName = "Subject42 Trailer Development";
            PlayerSettings.defaultScreenWidth = 1920;
            PlayerSettings.defaultScreenHeight = 1080;
            PlayerSettings.fullScreenMode = FullScreenMode.FullScreenWindow;
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray(),
                locationPathName = exe,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.Development | BuildOptions.DetailedBuildReport
            });
            string assets = string.Join("\n", report.packedAssets.SelectMany(p => p.contents)
                .Select(c => c.sourceAssetPath).Where(p => p.Contains("TrailerCapture") || p.Contains("WorldRule_") || p.Contains("RocketStorm")).Distinct());
            File.WriteAllText(Output + "/build-results.txt", $"{report.summary.result}\nExecutable: {exe}\nDevelopment: {(report.summary.options & BuildOptions.Development) != 0}\nErrors: {report.summary.totalErrors}\nWarnings: {report.summary.totalWarnings}\nSize: {report.summary.totalSize}\nCapture content:\n{assets}\n");
            if (report.summary.result != BuildResult.Succeeded) throw new InvalidOperationException("Standalone build failed: " + report.summary.result);
        }
        catch (Exception error) { File.AppendAllText(Output + "/build-results.txt", "FAIL\n" + error + "\n"); Debug.LogException(error); }
        finally
        {
            void RestoreSettings()
            {
                PlayerSettings.productName = product;
                PlayerSettings.defaultScreenWidth = width;
                PlayerSettings.defaultScreenHeight = height;
                PlayerSettings.fullScreenMode = mode;
                File.WriteAllBytes(settingsPath, savedSettings);
            }
            RestoreSettings();
            // Native build cleanup may serialize PlayerSettings again after returning
            // from this callback. Restore once more on the next editor update.
            EditorApplication.delayCall += () =>
            {
                RestoreSettings();
                Debug.Log($"[TrailerStandaloneBuild] restored project product={PlayerSettings.productName} mode={PlayerSettings.fullScreenMode}");
            };
        }
    }
}
#endif
