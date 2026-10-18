#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;
using UnityEngine.SceneManagement;

// Narrow, repeatable authoring of two existing scene instances. Art prefabs are untouched.
[InitializeOnLoad]
public static class ArcadePrototypeAuthoring
{
    private const string ScenePath = "Assets/_Project/Scenes/MainBuild/MainMenu.unity";
    private const string Output = "Artifacts/GeneratedQA/Arcade";
    private const string Running = "Subject42.ArcadeSmoke.Running";
    static ArcadePrototypeAuthoring()
    {
        EditorApplication.update += Poll;
        ScriptableObject.CreateInstance<TestRunnerApi>().RegisterCallbacks(new Results());
    }
    private static void Poll()
    {
        string path = Output + "/run.request";
        if (!File.Exists(path) || EditorApplication.isCompiling || EditorApplication.isUpdating ||
            EditorApplication.isPlayingOrWillChangePlaymode || SessionState.GetBool("Subject42.Verification.AnyRunActive", false)) return;
        string command = File.ReadAllText(path).Trim();
        File.Delete(path);
        try
        {
            if (command == "setup") { Configure(); File.WriteAllText(Output + "/setup.txt", "PASS: pinball and existing red roulette table configured; poker retained but disabled."); }
            else if (command == "smoke" || command == "roulette" || command == "bunker-roulette" || command == "pinball" || command == "pinball-cursor" || command == "pinball-economy" || command == "pinball-cycle")
            {
                SessionState.SetBool(Running, true);
                string[] tests = command switch
                {
                    "pinball-economy" => new[] { "ArcadePrototypeSmokeTests.RewardsStopAfterSixCoins" },
                    "pinball-cycle" => new[] { "ArcadePrototypeSmokeTests.ProductionPinballPlaysAndRestoresBunker" },
                    "pinball-cursor" => new[] { "ArcadePrototypeSmokeTests.CursorHighlightsAndOpensPinball" },
                    "pinball" => new[] { "ArcadePrototypeSmokeTests.ProductionPinballPlaysAndRestoresBunker", "ArcadePrototypeSmokeTests.CursorHighlightsAndOpensPinball", "ArcadePrototypeSmokeTests.RestingFlippersAllowARealDrain", "ArcadePrototypeSmokeTests.RewardsStopAfterSixCoins" },
                    "bunker-roulette" => new[] { "RouletteSmokeTests", "BunkerDoorDebugTests" },
                    "roulette" => new[] { "RouletteSmokeTests" },
                    _ => new[] { "ArcadePrototypeSmokeTests", "RouletteSmokeTests" }
                };
                ScriptableObject.CreateInstance<TestRunnerApi>().Execute(new ExecutionSettings(new Filter
                { testMode = TestMode.EditMode, testNames = tests }));
            }
        }
        catch (Exception error) { File.WriteAllText(Output + "/error.txt", error.ToString()); Debug.LogException(error); }
    }
    [MenuItem("Tools/Subject42/Dev/Authoring/Bunker/Configure Arcade Prototypes")]
    public static void Configure()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode before authoring.");
        var scene = SceneManager.GetSceneByPath(ScenePath);
        bool opened = !scene.IsValid() || !scene.isLoaded;
        if (opened) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
        try
        {
            if (scene.isDirty) throw new InvalidOperationException("Save the existing MainMenu edits before authoring the arcade.");
            var all = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true)).ToArray();
            var panels = all.Select(t => t.GetComponent<BunkerPanelManager>()).Single(c => c != null);
            var camera = all.Select(t => t.GetComponent<CameraFollow>()).Single(c => c != null);
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/_Project/Fonts/Subject42 UI SDF.asset");
            ConfigureStation<AnomalyPinball>(all.Single(t => t.name == "p_bunkerArkade1").gameObject, panels, camera, font, "PINBALL");
            var poker = all.Single(t => t.name == "p_PockerTable1");
            foreach (var component in poker.GetComponents<MonoBehaviour>())
                if (component is DicePoker || component is ArcadeGameStation || component is BunkerHoverOutline) component.enabled = false;
            poker.Find("ArcadeInteraction")?.gameObject.SetActive(false);
            poker.Find("ArcadeLabel")?.gameObject.SetActive(false);
            ConfigureStation<EuropeanRoulette>(all.Single(t => t.name == "p_RouletteTable1").gameObject, panels, camera, font, "ROULETTE");
            // Only this room's entrance becomes usable; the other locked rooms retain their state.
            var gate = all.Single(t => t.name == "Door_Arcade_Locked").GetComponent<BunkerGateVisual>();
            var gateSo = new SerializedObject(gate);
            gateSo.FindProperty("startOpen").boolValue = true;
            gateSo.FindProperty("openOnPlayerProximity").boolValue = false;
            gateSo.ApplyModifiedPropertiesWithoutUndo();
            gate.SetOpen(true);
            PrefabUtility.RecordPrefabInstancePropertyModifications(gate);
            foreach (var target in gate.GetComponentsInChildren<Transform>(true))
                PrefabUtility.RecordPrefabInstancePropertyModifications(target.gameObject);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }
        finally { if (opened) EditorSceneManager.CloseScene(scene, true); }
    }
    private static void ConfigureStation<T>(GameObject root, BunkerPanelManager panels, CameraFollow camera, TMP_FontAsset font, string label) where T : ArcadeMinigame
    {
        var game = root.GetComponent<T>() ?? root.AddComponent<T>();
        var so = new SerializedObject(game);
        so.FindProperty("panels").objectReferenceValue = panels;
        so.FindProperty("cameraFollow").objectReferenceValue = camera;
        so.FindProperty("font").objectReferenceValue = font;
        so.ApplyModifiedPropertiesWithoutUndo();
        var station = root.GetComponent<ArcadeGameStation>() ?? root.AddComponent<ArcadeGameStation>();
        so = new SerializedObject(station);
        so.FindProperty("game").objectReferenceValue = game;
        so.ApplyModifiedPropertiesWithoutUndo();
        var hover = root.GetComponent<BunkerHoverOutline>() ?? root.AddComponent<BunkerHoverOutline>();
        var trigger = root.transform.Find("ArcadeInteraction");
        if (trigger == null)
        {
            trigger = new GameObject("ArcadeInteraction").transform;
            trigger.SetParent(root.transform, false);
            var original = root.GetComponents<BoxCollider2D>().FirstOrDefault(c => c.isTrigger) ?? root.GetComponent<BoxCollider2D>();
            if (original == null) throw new InvalidOperationException("Existing cabinet/table collider is missing.");
            var collider = trigger.gameObject.AddComponent<BoxCollider2D>();
            collider.isTrigger = true; collider.size = original.size; collider.offset = original.offset;
            var proxy = trigger.gameObject.AddComponent<BunkerInteractableCollider>();
            so = new SerializedObject(proxy);
            so.FindProperty("sourceRoot").objectReferenceValue = root;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
        int layer = LayerMask.NameToLayer("BunkerInteractable");
        if (layer < 0) throw new InvalidOperationException("Existing BunkerInteractable layer is missing.");
        trigger.gameObject.layer = layer;
        so = new SerializedObject(hover);
        so.FindProperty("outlineMaterialFallback").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/prefabs/Bunker/mat/BunkerElements2D.mat");
        so.FindProperty("useBoundsOutlineFallback").boolValue = true;
        so.FindProperty("boundsOutlineCollider").objectReferenceValue = trigger.GetComponent<Collider2D>();
        so.ApplyModifiedPropertiesWithoutUndo();
        if (root.transform.Find("ArcadeLabel") == null)
        {
            var text = new GameObject("ArcadeLabel", typeof(RectTransform), typeof(TextMeshPro)).GetComponent<TextMeshPro>();
            text.transform.SetParent(root.transform, false);
            text.transform.localPosition = new Vector3(0, 4.1f, 0);
            text.rectTransform.sizeDelta = new Vector2(5, .7f);
            text.font = font; text.fontSize = 2.5f; text.text = label;
            text.alignment = TextAlignmentOptions.Center;
            text.color = new Color32(94, 234, 179, 255);
            text.GetComponent<MeshRenderer>().sortingLayerName = "UI";
            text.GetComponent<MeshRenderer>().sortingOrder = 100;
        }
    }
    private sealed class Results : IErrorCallbacks
    {
        public void RunStarted(ITestAdaptor tests) { SessionState.SetBool("Subject42.Verification.AnyRunActive", true); }
        public void TestStarted(ITestAdaptor test) { }
        public void TestFinished(ITestResultAdaptor result) { }
        public void RunFinished(ITestResultAdaptor result)
        {
            if (!SessionState.GetBool(Running, false)) return;
            SessionState.EraseBool(Running);
            SessionState.EraseBool("Subject42.Verification.AnyRunActive");
            TestRunnerApi.SaveResultToFile(result, Output + "/results.xml");
        }
        public void OnError(string message)
        {
            if (!SessionState.GetBool(Running, false)) return;
            SessionState.EraseBool(Running);
            SessionState.EraseBool("Subject42.Verification.AnyRunActive");
            File.WriteAllText(Output + "/error.txt", message);
        }
    }
}
#endif
