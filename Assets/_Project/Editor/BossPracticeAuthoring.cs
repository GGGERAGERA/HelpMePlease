using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering.Universal;
using Subject42.Combat.OrbitalStation;

public static class BossPracticeAuthoring
{
    public const string ScenePath = "Assets/_Project/Scenes/Dev/BossPractice.unity";

    [MenuItem("Tools/Subject42/Open Boss Practice")]
    public static void Open()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        if (!File.Exists(ScenePath)) Create();
        EditorSceneManager.OpenScene(ScenePath);
    }

    public static void Create()
    {
        var previous = SceneManager.GetActiveScene();
        bool replaceEmptyScene = string.IsNullOrEmpty(previous.path) && previous.rootCount == 0;
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,
            replaceEmptyScene ? NewSceneMode.Single : NewSceneMode.Additive);
        SceneManager.SetActiveScene(scene);
        try
        {
            var arena = new GameObject("Boss Practice").AddComponent<BossPracticeArena>();
            arena.bossPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/prefabs/Enemies/p_Boss1.prefab");
            var character = AssetDatabase.LoadAssetAtPath<CharacterData>("Assets/_Project/Scriptable Objects/Characters/01_Gera.asset");
            var prefab = OrbitalPresentationConfig.Active.GetPlayerPrefab(character.characterPrefab);
            var player = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            player.name = "Player template (inactive)";
            foreach (var station in player.GetComponentsInChildren<OrbitalStationRuntime>(true))
                station.gameObject.SetActive(false);
            foreach (var component in player.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (!component.gameObject.activeInHierarchy) continue;
                if (component is CharacterMovement2D || component is PlayerHealth ||
                    component is PlayerWhiteFlash || component is Light2D || component is PlayerHitSound) continue;
                Object.DestroyImmediate(component);
            }
            player.SetActive(false);
            arena.playerTemplate = player;
            var camera = new GameObject("Practice Camera").AddComponent<Camera>();
            camera.tag = "MainCamera";
            camera.orthographic = true;
            camera.orthographicSize = 12;
            camera.transform.position = new Vector3(6, 2, -10);
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.10f, .13f, .16f);
            camera.gameObject.AddComponent<AudioListener>();
            arena.arenaCamera = camera;
            var light = new GameObject("Global Light").AddComponent<Light2D>();
            light.lightType = Light2D.LightType.Global;
            light.intensity = 1;

            // A visible grid provides speed/distance cues while dodging.
            var material = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Scenes/Dev/BossPracticeGrid.mat");
            if (material == null)
            {
                material = new Material(Shader.Find("Sprites/Default"));
                AssetDatabase.CreateAsset(material, "Assets/_Project/Scenes/Dev/BossPracticeGrid.mat");
            }
            var floor = new GameObject("Arena grid");
            for (int n = -30; n <= 30; n += 3)
            {
                Line(floor.transform, material, new Vector3(n, -30), new Vector3(n, 30));
                Line(floor.transform, material, new Vector3(-30, n), new Vector3(30, n));
            }
            Wall(new Vector2(-30, 0), new Vector2(1, 61));
            Wall(new Vector2(30, 0), new Vector2(1, 61));
            Wall(new Vector2(0, -30), new Vector2(61, 1));
            Wall(new Vector2(0, 30), new Vector2(61, 1));
            EditorSceneManager.SaveScene(scene, ScenePath);
        }
        finally
        {
            if (!replaceEmptyScene)
            {
                EditorSceneManager.CloseScene(scene, true);
                if (previous.IsValid()) SceneManager.SetActiveScene(previous);
            }
        }
    }
    static void Line(Transform parent, Material material, Vector3 a, Vector3 b)
    {
        var line = new GameObject("Grid line").AddComponent<LineRenderer>();
        line.transform.SetParent(parent);
        line.sharedMaterial = material;
        line.startColor = line.endColor = new Color(.19f, .24f, .28f);
        line.startWidth = line.endWidth = .035f;
        line.sortingOrder = -100;
        line.positionCount = 2;
        line.SetPosition(0, a); line.SetPosition(1, b);
    }
    static void Wall(Vector2 position, Vector2 size)
    {
        var wall = new GameObject("Arena boundary").AddComponent<BoxCollider2D>();
        wall.transform.position = position;
        wall.size = size;
    }
}
