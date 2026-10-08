#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Player;
using UnityEngine;

// Script compilation only. Does not build scenes, run gameplay or execute tests.
[InitializeOnLoad]
public static class PlayerScriptCompilationCheck
{
    private const string Output = "Artifacts/GeneratedQA/Phase6/Compilation";
    static PlayerScriptCompilationCheck() => EditorApplication.update += CheckRequest;

    private static void CheckRequest()
    {
        string request = Output + "/compile.request";
        if (!File.Exists(request) || EditorApplication.isCompiling || EditorApplication.isUpdating ||
            EditorApplication.isPlayingOrWillChangePlaymode || SessionState.GetBool("Subject42.Verification.AnyRunActive", false)) return;
        File.Delete(request);
        Run();
    }

    [MenuItem("Tools/Subject42/QA/Compile release and development scripts")]
    public static void Run()
    {
        Directory.CreateDirectory(Output);
        var report = "Editor domain loaded: " + DateTime.UtcNow.ToString("O") + "\n";
        try
        {
            foreach (bool development in new[] { false, true })
            {
                string configuration = development ? "Development" : "Release";
                string destination = Output + "/" + configuration;
                Directory.CreateDirectory(destination);
                var result = PlayerBuildInterface.CompilePlayerScripts(new ScriptCompilationSettings
                {
                    target = EditorUserBuildSettings.activeBuildTarget,
                    group = BuildPipeline.GetBuildTargetGroup(EditorUserBuildSettings.activeBuildTarget),
                    options = development ? ScriptCompilationOptions.DevelopmentBuild : ScriptCompilationOptions.None
                }, destination);
                if (result.assemblies == null || result.assemblies.Count == 0)
                    throw new InvalidOperationException(configuration + " produced no assemblies.");
                report += configuration + ": PASS\n" + string.Join("\n", result.assemblies) + "\n";
            }
            report += "PASS\n";
        }
        catch (Exception error) { report += "FAIL\n" + error + "\n"; Debug.LogException(error); }
        File.WriteAllText(Output + "/results.txt", report);
    }
}
#endif
