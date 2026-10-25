#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class CombatReadabilityVerification
{
    public const string Output = "Artifacts/GeneratedQA/CombatReadability";
    static CombatReadabilityVerification() => EditorApplication.update += Poll;

    private static void Poll()
    {
        string request = Output + "/editor.request";
        if (!File.Exists(request) || EditorApplication.isCompiling || EditorApplication.isUpdating ||
            EditorApplication.isPlayingOrWillChangePlaymode || SessionState.GetBool("Subject42.Verification.AnyRunActive", false)) return;
        string command = File.ReadAllText(request).Trim();
        File.Delete(request);
        Directory.CreateDirectory(Output);
        try
        {
            if (command == "snapshot") Snapshot();
            else if (command == "build") Build();
            else if (command == "build-hierarchy") Build(true);
            else if (command == "build-environment") Build(false, true);
            else if (command == "build-enemy") Build(false, false, true);
            else if (command == "build-buffer") Build(false, false, false, true);
            File.WriteAllText(Output + "/editor-result.txt", command + " SUCCESS " + DateTime.UtcNow.ToString("O"));
        }
        catch (Exception exception)
        {
            File.WriteAllText(Output + "/editor-error.txt", exception.ToString());
            Debug.LogException(exception);
        }
    }

    private static void Snapshot()
    {
        var scene = SceneManager.GetActiveScene();
        File.WriteAllText(Output + "/open-scene.txt", scene.path + "\ndirty=" + scene.isDirty);
        if (!scene.isDirty || string.IsNullOrEmpty(scene.path)) return;
        File.Copy(scene.path, Output + "/open-scene-before-save.unity", true);
        if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Could not preserve the open scene.");
        File.Copy(scene.path, Output + "/open-scene-preserved.unity", true);
    }

    private static void Build(bool hierarchy = false, bool environment = false, bool enemy = false, bool buffer = false)
    {
        string results = buffer ? "Artifacts/GeneratedQA/ComputeBuffer" : enemy ? "Artifacts/GeneratedQA/EnemyFeedback" : environment ? "Artifacts/GeneratedQA/EnvironmentPace"
            : hierarchy ? "Artifacts/GeneratedQA/OrbitalHierarchy" : Output;
        Directory.CreateDirectory(results);
        const string settingsPath = "ProjectSettings/ProjectSettings.asset";
        byte[] settings = File.ReadAllBytes(settingsPath);
        string product = PlayerSettings.productName;
        int width = PlayerSettings.defaultScreenWidth, height = PlayerSettings.defaultScreenHeight;
        var mode = PlayerSettings.fullScreenMode;
        try
        {
            PlayerSettings.productName = "Subject42 Combat Readability QA";
            PlayerSettings.defaultScreenWidth = 1600;
            PlayerSettings.defaultScreenHeight = 900;
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            string executable = Path.GetFullPath(buffer ? "Builds/ComputeBufferRelease/Subject42.exe" : enemy ? "Builds/EnemyFeedbackRelease/Subject42.exe" : environment
                ? "Builds/EnvironmentPaceRelease/Subject42.exe" : hierarchy
                ? "Builds/OrbitalHierarchyRelease/Subject42.exe"
                : "Builds/CombatReadabilityRelease/Subject42.exe");
            Directory.CreateDirectory(Path.GetDirectoryName(executable));
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray(),
                locationPathName = executable,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None
            });
            File.WriteAllText(results + "/build-results.txt", report.summary.result +
                "\nExecutable=" + executable + "\nOptions=" + report.summary.options +
                "\nErrors=" + report.summary.totalErrors + "\nWarnings=" + report.summary.totalWarnings);
            if (report.summary.result != BuildResult.Succeeded) throw new InvalidOperationException("Release player build failed.");
        }
        finally
        {
            void Restore()
            {
                PlayerSettings.productName = product;
                PlayerSettings.defaultScreenWidth = width;
                PlayerSettings.defaultScreenHeight = height;
                PlayerSettings.fullScreenMode = mode;
                File.WriteAllBytes(settingsPath, settings);
            }
            Restore();
            EditorApplication.delayCall += Restore;
        }
    }
}
#endif
