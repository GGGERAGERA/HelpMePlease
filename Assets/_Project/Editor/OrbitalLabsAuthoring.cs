#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class OrbitalLabsAuthoring
{
    private const string Production = "Assets/_Project/Scenes/MainBuild/MVP.unity";
    [MenuItem("Tools/Subject42/Orbital Labs/Open OrbitalRewardLab")]
    public static void OpenRewards() => Open(OrbitalRewardLabController.ScenePath);
    [MenuItem("Tools/Subject42/Orbital Labs/Open EnemyOrbitalLab")]
    public static void OpenEnemies() => Open(EnemyOrbitalLabController.ScenePath);
    private static void Open(string path)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        if (!File.Exists(path)) CreateMissingScenes();
        EditorSceneManager.OpenScene(path);
    }

    public static GameObject[] FindProductionEnemies()
    {
        // Restrict to actual production dependencies, excluding unused gallery/prepared variants and bosses.
        return AssetDatabase.GetDependencies(Production, true).Where(p => p.EndsWith(".prefab"))
            .Select(AssetDatabase.LoadAssetAtPath<GameObject>)
            .Where(p => p != null && p.TryGetComponent<EnemyHealth>(out var health) && health.enabled && !health.IsBoss &&
                (p.GetComponents<EnemyMovement>().Any(m => m.enabled) ||
                 p.TryGetComponent<TurretEnemyBehaviour>(out var turret) && turret.enabled))
            .Distinct().OrderBy(p => p.name, StringComparer.Ordinal).ToArray();
    }

    [MenuItem("Tools/Subject42/Orbital Labs/Refresh production enemy list")]
    public static void RefreshEnemies()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        var previous = SceneManager.GetActiveScene();
        var scene = SceneManager.GetSceneByPath(EnemyOrbitalLabController.ScenePath);
        bool opened = !scene.IsValid() || !scene.isLoaded;
        if (opened) scene = EditorSceneManager.OpenScene(EnemyOrbitalLabController.ScenePath, OpenSceneMode.Additive);
        var lab = Find<EnemyOrbitalLabController>(scene);
        lab.EnemyPrefabs = FindProductionEnemies();
        EditorUtility.SetDirty(lab);
        EditorSceneManager.SaveScene(scene);
        if (opened) EditorSceneManager.CloseScene(scene, true);
        if (previous.IsValid()) SceneManager.SetActiveScene(previous);
    }

    [MenuItem("Tools/Subject42/Orbital Labs/Create missing scenes")]
    public static void CreateMissingScenes()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
        var previous = SceneManager.GetActiveScene();
        var lights = UnityEngine.Object.FindObjectsByType<Light2D>(FindObjectsSortMode.None)
            .Where(l => l.enabled && l.lightType == Light2D.LightType.Global).ToArray();
        foreach (var light in lights) light.enabled = false;
        var production = EditorSceneManager.OpenPreviewScene(Production);
        foreach (var light in production.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Light2D>(true)))
            light.enabled = false;
        try
        {
            Create<OrbitalRewardLabController>(OrbitalRewardLabController.ScenePath, production);
            Create<EnemyOrbitalLabController>(EnemyOrbitalLabController.ScenePath, production);
        }
        finally
        {
            EditorSceneManager.ClosePreviewScene(production);
            foreach (var light in lights) if (light != null) light.enabled = true;
            if (previous.IsValid()) SceneManager.SetActiveScene(previous);
        }
        AssetDatabase.Refresh();
    }

    private static T Find<T>(Scene scene) where T : Component => scene.GetRootGameObjects()
        .SelectMany(r => r.GetComponentsInChildren<T>(true)).First();

    private static void Create<T>(string path, Scene production) where T : OrbitalLabSession
    {
        if (File.Exists(path)) return;
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        SceneManager.SetActiveScene(scene);
        try
        {
            var lab = new GameObject(typeof(T).Name).AddComponent<T>();
            lab.Character = AssetDatabase.LoadAssetAtPath<CharacterData>("Assets/_Project/Scriptable Objects/Characters/01_Gera.asset");
            var sourceRig = Find<CameraFollow>(production);
            var rig = new GameObject("Production Camera");
            var camera = rig.AddComponent<Camera>(); EditorUtility.CopySerialized(sourceRig.ControlledCamera, camera);
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.16f, .18f, .19f);
            var follow = rig.AddComponent<CameraFollow>(); EditorUtility.CopySerialized(sourceRig, follow);
            follow.target = null; follow.offset = new Vector3(0, 0, -10);
            Set(follow, "controlledCamera", camera);
            rig.tag = "MainCamera"; rig.transform.position = follow.offset;
            rig.AddComponent<AudioListener>();
            camera.GetUniversalAdditionalCameraData().renderPostProcessing = false;
            lab.CameraRig = follow;
            var light = new GameObject("Neutral arena light").AddComponent<Light2D>();
            light.lightType = Light2D.LightType.Global;
            var bounds = new GameObject("Arena bounds (60 x 60)");
            foreach (var wall in new[] { new Vector4(-30, 0, 1, 61), new Vector4(30, 0, 1, 61), new Vector4(0, -30, 61, 1), new Vector4(0, 30, 61, 1) })
            {
                var go = new GameObject("Wall"); go.transform.SetParent(bounds.transform);
                go.transform.position = new Vector3(wall.x, wall.y, 0);
                go.AddComponent<BoxCollider2D>().size = new Vector2(wall.z, wall.w);
            }
            if (lab is OrbitalRewardLabController)
            {
                var sourceManager = Find<UpgradeManager>(production);
                var sourcePanel = (UpgradePanelView)new SerializedObject(sourceManager).FindProperty("upgradePanelView").objectReferenceValue;
                var canvas = new GameObject("Production Reward Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
                EditorUtility.CopySerialized(sourcePanel.GetComponentInParent<Canvas>(), canvas.GetComponent<Canvas>());
                EditorUtility.CopySerialized(sourcePanel.GetComponentInParent<CanvasScaler>(), canvas.GetComponent<CanvasScaler>());
                canvas.GetComponent<Canvas>().worldCamera = camera;
                var panel = UnityEngine.Object.Instantiate(sourcePanel, canvas.transform, false);
                panel.name = "Production Upgrade Panel";
                var logic = new GameObject("Production Reward Logic");
                var applier = logic.AddComponent<UpgradeApplier>();
                var manager = logic.AddComponent<UpgradeManager>();
                EditorUtility.CopySerialized(sourceManager, manager);
                Set(manager, "upgradePanelView", panel); Set(manager, "upgradeApplier", applier);
                lab.Rewards = manager;
                new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            }
            if (lab is EnemyOrbitalLabController enemyLab)
            {
                enemyLab.EnemyPrefabs = FindProductionEnemies();
                if (enemyLab.EnemyPrefabs.Length == 0) throw new InvalidOperationException("No production enemy references found.");
            }
            EditorSceneManager.SaveScene(scene, path);
        }
        finally { EditorSceneManager.CloseScene(scene, true); }
    }
    private static void Set(UnityEngine.Object target, string property, UnityEngine.Object value)
    { var so = new SerializedObject(target); so.FindProperty(property).objectReferenceValue = value; so.ApplyModifiedPropertiesWithoutUndo(); }
}
#endif
