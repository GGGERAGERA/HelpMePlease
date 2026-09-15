#if UNITY_EDITOR
using System.IO;
using Subject42.Combat.OrbitalStation;
using Subject42.DebugLabs;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

public static class CustomOrbitLabAuthoring
{
    [MenuItem("Tools/Subject42/Orbital Labs/Smoke test production custom orbits")]
    public static void SmokeTestProduction()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        const string output = "Artifacts/GeneratedQA/CustomOrbitProduction/";
        Directory.CreateDirectory(output);
        SessionState.SetString("Subject42.QAOutput", output);
        ScriptableObject.CreateInstance<TestRunnerApi>().Execute(new ExecutionSettings(new Filter {
            testMode = TestMode.EditMode, testNames = new[] { "CustomOrbitProductionSmokeTests" } }));
    }
    [MenuItem("Tools/Subject42/Orbital Labs/Publish drawing settings to production")]
    public static void PublishDrawingPrefab()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        var scene = EditorSceneManager.OpenPreviewScene(CustomOrbitLab.ScenePath);
        GameObject root = null;
        try
        {
            CustomOrbitLab lab = null;
            foreach (var item in scene.GetRootGameObjects())
                if (item.TryGetComponent<CustomOrbitLab>(out var found)) lab = found;
            if (lab == null) throw new System.InvalidOperationException("CustomOrbitLab source is missing.");
            root = new GameObject("Custom Orbit Drawing");
            root.SetActive(false);
            var draw = root.AddComponent<CustomOrbitDrawing>();
            draw.PointMinDistance = lab.PointMinDistance;
            draw.CloseThreshold = lab.CloseThreshold;
            draw.Smoothing = lab.Smoothing;
            draw.PathWidth = lab.PathWidth;
            draw.MaxDrawRadius = lab.MaxDrawRadius;
            draw.PathLine = Object.Instantiate(lab.PathLine, root.transform, false);
            draw.StartCircle = Object.Instantiate(lab.StartCircle, root.transform, false);
            draw.DrawArea = Object.Instantiate(lab.DrawArea, root.transform, false);
            draw.PathLine.positionCount = 0;
            const string path = "Assets/_Project/Resources/OrbitalStation/Authored/CustomOrbitDrawing.prefab";
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            var config = AssetDatabase.LoadAssetAtPath<OrbitalPresentationConfig>("Assets/_Project/Resources/OrbitalStation/OrbitalPresentationConfig.asset");
            config.CustomDrawingPrefab = prefab.GetComponent<CustomOrbitDrawing>();
            EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssetIfDirty(config);
        }
        finally
        {
            if (root != null) Object.DestroyImmediate(root);
            EditorSceneManager.ClosePreviewScene(scene);
        }
    }
    [MenuItem("Tools/Subject42/Orbital Labs/Smoke test CustomOrbitLab")]
    public static void SmokeTest()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        CreateMissingScene();
        var api = ScriptableObject.CreateInstance<TestRunnerApi>();
        api.Execute(new ExecutionSettings(new Filter { testMode = TestMode.EditMode, testNames = new[] { "CustomOrbitLabSmokeTests" } }));
    }

    [MenuItem("Tools/Subject42/Orbital Labs/Open CustomOrbitLab")]
    public static void Open()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        CreateMissingScene();
        EditorSceneManager.OpenScene(CustomOrbitLab.ScenePath);
    }

    public static void CreateMissingScene()
    {
        if (File.Exists(CustomOrbitLab.ScenePath)) return;
        var previous = SceneManager.GetActiveScene();
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        SceneManager.SetActiveScene(scene);
        try
        {
            var config = AssetDatabase.LoadAssetAtPath<OrbitalPresentationConfig>("Assets/_Project/Resources/OrbitalStation/OrbitalPresentationConfig.asset");
            var character = AssetDatabase.LoadAssetAtPath<CharacterData>("Assets/_Project/Scriptable Objects/Characters/03_Vika.asset");
            var player = (GameObject)PrefabUtility.InstantiatePrefab(character.characterPrefab, scene);
            PrefabUtility.UnpackPrefabInstance(player, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            player.name = "Vika (lab movement and visuals only)";
            player.transform.position = Vector3.zero;
            // Strip combat in the authored scene, before any runtime lifecycle can start.
            foreach (var station in player.GetComponentsInChildren<OrbitalStationView>(true))
                if (station.gameObject != player) Object.DestroyImmediate(station.gameObject);
            foreach (var behaviour in player.GetComponentsInChildren<MonoBehaviour>(true))
                if (!(behaviour is CharacterMovement2D) && !(behaviour is Light2D)) Object.DestroyImmediate(behaviour);
            var movement = player.GetComponent<CharacterMovement2D>();
            player.SetActive(true);

            var cameraObject = new GameObject("Lab Camera", typeof(Camera), typeof(CameraFollow), typeof(AudioListener));
            cameraObject.tag = "MainCamera";
            cameraObject.transform.position = new Vector3(0, 0, -10);
            var camera = cameraObject.GetComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 7;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.035f, .05f, .075f);
            camera.GetUniversalAdditionalCameraData().renderPostProcessing = false;
            var follow = cameraObject.GetComponent<CameraFollow>();
            follow.target = player.transform;
            var cameraData = new SerializedObject(follow);
            cameraData.FindProperty("controlledCamera").objectReferenceValue = camera;
            cameraData.ApplyModifiedPropertiesWithoutUndo();

            var light = new GameObject("Lab global light").AddComponent<Light2D>();
            light.lightType = Light2D.LightType.Global;
            var orbitRoot = new GameObject("Custom orbit (translation only)").transform;
            orbitRoot.SetParent(player.transform, false);
            var lab = new GameObject("CustomOrbitLab").AddComponent<CustomOrbitLab>();
            lab.Player = movement;
            lab.DrawingCamera = camera;
            lab.OrbitRoot = orbitRoot;
            lab.PathLine = CreateLine("Drawn path", orbitRoot, config.VisualMaterial, .055f, false);
            lab.StartCircle = CreateLine("START closure radius", orbitRoot, config.VisualMaterial, .025f, true);
            lab.StartCircle.startColor = lab.StartCircle.endColor = new Color(1, .8f, .2f, .7f);
            lab.StartCircle.gameObject.SetActive(false);
            AddDrawArea(lab);
            lab.Mounts = new OrbitalMountView[16];
            for (int i = 0; i < lab.Mounts.Length; i++)
            {
                var mount = Object.Instantiate(config.MountPrefab, orbitRoot);
                mount.name = $"Test mount {i + 1:00}";
                foreach (var behaviour in mount.GetComponentsInChildren<MonoBehaviour>(true))
                    if (!(behaviour is OrbitalMountView)) Object.DestroyImmediate(behaviour);
                mount.DepthGroup.enabled = false;
                mount.Marker.sortingOrder = 30;
                mount.Halo.sortingOrder = 29;
                mount.Marker.color = Color.Lerp(new Color(.2f, 1, .9f), new Color(1, .5f, .2f), i / 15f);
                mount.gameObject.SetActive(false);
                lab.Mounts[i] = mount;
            }
            Directory.CreateDirectory(Path.GetDirectoryName(CustomOrbitLab.ScenePath));
            EditorSceneManager.SaveScene(scene, CustomOrbitLab.ScenePath);
        }
        finally
        {
            EditorSceneManager.CloseScene(scene, true);
            if (previous.IsValid()) SceneManager.SetActiveScene(previous);
        }
    }

    private static LineRenderer CreateLine(string name, Transform parent, Material material, float width, bool loop)
    {
        var line = new GameObject(name).AddComponent<LineRenderer>();
        line.transform.SetParent(parent, false);
        line.sharedMaterial = material;
        line.useWorldSpace = false;
        line.widthMultiplier = width;
        line.numCornerVertices = 4;
        line.numCapVertices = 4;
        line.sortingOrder = 10;
        line.positionCount = 0;
        line.loop = loop;
        return line;
    }

    public static void AddDrawArea(CustomOrbitLab lab)
    {
        if (lab.DrawArea != null) return;
        lab.DrawArea = CreateLine("DRAW AREA (authoring boundary)", lab.OrbitRoot, lab.PathLine.sharedMaterial, .018f, true);
        lab.DrawArea.sortingOrder = 9;
        lab.DrawArea.startColor = lab.DrawArea.endColor = new Color(.55f, .9f, 1f, .25f);
        lab.DrawArea.positionCount = 96;
        for (int i = 0; i < 96; i++)
        {
            float angle = i * Mathf.PI * 2 / 96;
            lab.DrawArea.SetPosition(i, new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0));
        }
        lab.DrawArea.transform.localScale = Vector3.one * lab.MaxDrawRadius;
    }
}
#endif
