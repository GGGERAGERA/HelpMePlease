#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using Subject42.Visuals;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

public static class LiveVisualStudioAuthoring
{
    public const string QaDirectory = "Artifacts/GeneratedQA/LiveVisualStudio-20261027";
    private static TestRunnerApi tests;

    [MenuItem("Tools/Subject42/Visual Studio/Prepare production catalog %&v")]
    public static void Prepare()
    {
        if (Application.isPlaying) throw new InvalidOperationException("Подготовьте каталог вне Play Mode.");
        AssetDatabase.Refresh();
        var preset = AssetDatabase.LoadAssetAtPath<VisualTuningPreset>(VisualTuningPresetStorage.AssetPath);
        var baseline = AssetDatabase.LoadAssetAtPath<TextAsset>(LiveVisualBaselineAuthoring.OutputPath);
        if (preset == null || baseline == null) throw new InvalidOperationException("Нет production preset или Git baseline.");
        var original = JsonUtility.FromJson<VisualProfile>(baseline.text);
        original.Validate();
        Undo.RecordObject(preset, "Prepare Live Visual Studio catalog");
        preset.LiveOriginalBaseline = baseline;
        preset.LiveSpriteShader = Shader.Find("Subject42/Environment Tile Lit");
        string[] sourceFolders = { "Enemies", "fx", "Orbital", "Environment/Props", "Weapons", "miniWeapons", "projectiles", "Characters", "Bootstrap" };
        preset.LiveSources = AssetDatabase.FindAssets("t:Prefab", sourceFolders.Select(p => "Assets/_Project/prefabs/" + p).Where(AssetDatabase.IsValidFolder).ToArray())
            .Select(AssetDatabase.GUIDToAssetPath)
            .OrderBy(p => p, StringComparer.Ordinal)
            .Select(path => new LiveVisualSource { Prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path), AssetPath = path }).ToArray();
        preset.LiveHistoricalMaterials = original.MaterialSources.Select(m => m.AssetPath).Where(p => !string.IsNullOrEmpty(p)).Distinct()
            .OrderBy(p => p, StringComparer.Ordinal).Select(path => new LiveVisualMaterial { AssetPath = path,
                Material = AssetDatabase.LoadAssetAtPath<Material>(path) }).ToArray();
        EditorUtility.SetDirty(preset); AssetDatabase.SaveAssetIfDirty(preset);
        Debug.Log($"[LiveVisualStudio] Catalog prepared: {preset.LiveSources.Length} prefab sources; " +
            $"{original.Values.Count} historical values; Git {original.BaselineRevision}. No production overrides selected.");
    }

    [MenuItem("Tools/Subject42/Visual Studio/Verify profiles and production scenario %&t")]
    public static void Verify()
    {
        if (Application.isPlaying) throw new InvalidOperationException("Запустите проверку вне Play Mode.");
        for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
            if (UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).isDirty)
                throw new InvalidOperationException("Проверка не закрывает несохранённые сцены. Сначала сохраните свою сцену.");
        Directory.CreateDirectory(QaDirectory);
        SessionState.SetString("LiveVisualStudio.VerificationScene", UnityEngine.SceneManagement.SceneManager.GetActiveScene().path);
        File.WriteAllText(QaDirectory + "/editor-identity.txt", Application.dataPath + "\nUnity=" + Application.unityVersion +
            " PID=" + System.Diagnostics.Process.GetCurrentProcess().Id + "\nScene=Assets/_Project/Scenes/MainBuild/MVP.unity\n");
        tests = ScriptableObject.CreateInstance<TestRunnerApi>();
        tests.RegisterCallbacks(new Results());
        tests.Execute(new ExecutionSettings(new Filter { testMode = TestMode.EditMode,
            testNames = new[] { nameof(LiveVisualProfileTests), nameof(OrbitalVisualAdapterTests), nameof(PooledVisualConfigurationTests), nameof(LiveVisualProductionTests) } }));
    }

    private sealed class Results : ICallbacks
    {
        public void RunStarted(ITestAdaptor testsToRun) { }
        public void TestStarted(ITestAdaptor test) { }
        public void TestFinished(ITestResultAdaptor result) { }
        public void RunFinished(ITestResultAdaptor result)
        {
            TestRunnerApi.SaveResultToFile(result, QaDirectory + "/results.xml");
            File.WriteAllText(QaDirectory + "/result.txt", $"{result.TestStatus}: pass={result.PassCount}; fail={result.FailCount}; skipped={result.SkipCount}\n{result.Message}\n{result.StackTrace}");
            string scene = SessionState.GetString("LiveVisualStudio.VerificationScene", "");
            if (!Application.isPlaying && !string.IsNullOrEmpty(scene)) EditorSceneManager.OpenScene(scene);
            Debug.Log($"[LiveVisualStudio QA] {result.TestStatus}: {result.PassCount} passed, {result.FailCount} failed. " + QaDirectory);
        }
    }
}
#endif
