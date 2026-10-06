#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class CorridorMigrationAuthoring
{
    public const string ConfigPath = "Assets/_Project/Data/WorldEvents/Corridor/CorridorConfig.asset";
    public const string PrefabPath = "Assets/_Project/prefabs/Environment/WorldEvents/Corridor/PF_CorridorEvent.prefab";
    private const string Request = "Artifacts/GeneratedQA/CorridorMigration/author.request";
    static CorridorMigrationAuthoring() => EditorApplication.update += Poll;
    private static void Poll()
    {
        if (!File.Exists(Request) || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
        try { File.Delete(Request); } catch (IOException) { return; }
        try { Build(); File.WriteAllText(Request+".result", "PASS: config, production prefab and Lab references authored"); }
        catch (Exception exception) { File.WriteAllText(Request+".result",exception.ToString()); Debug.LogException(exception); }
    }
    [MenuItem("Tools/World Events/Bind Production Corridor")]
    public static void Build()
    {
        AssetDatabase.Refresh();
        var config = AssetDatabase.LoadAssetAtPath<CorridorConfig>(ConfigPath);
        if (config == null) { config = ScriptableObject.CreateInstance<CorridorConfig>(); AssetDatabase.CreateAsset(config,ConfigPath); }
        config.settings.routeDefinitions = new[] { "Straight", "L", "Zigzag" }.Select(name =>
            AssetDatabase.LoadAssetAtPath<CorridorRouteDefinition>("Assets/_Project/Data/WorldEvents/Corridor/Routes/Corridor"+name+".asset")).ToArray();
        config.settings.font = AssetDatabase.LoadAssetAtPath<TMPro.TMP_FontAsset>("Assets/_Project/Fonts/Subject42 UI SDF.asset");
        config.kit = AssetDatabase.LoadAssetAtPath<CorridorKit>("Assets/_Project/Data/WorldEvents/Corridor/CorridorKit.asset");
        config.rocket = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/prefabs/fx/p_fxRocket1.prefab");
        config.warning = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/prefabs/fx/fx_BossTaget1.prefab");
        config.explosion = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/prefabs/fx/fx_BomberExplosion.prefab").GetComponent<ParticleSystem>();
        if (!config.TryValidate(out var error)) throw new InvalidOperationException(error);
        EditorUtility.SetDirty(config);
        var root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            var data = new SerializedObject(root.GetComponent<CorridorEvent>());
            data.FindProperty("config").objectReferenceValue = config;
            data.ApplyModifiedPropertiesWithoutUndo();
            root.GetComponent<CircleCollider2D>().radius = 2.5f;
            PrefabUtility.SaveAsPrefabAsset(root,PrefabPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
        AssetDatabase.SaveAssets();
        var loaded = SceneManager.GetSceneByPath(WorldSystemsLabController.ScenePath);
        bool alreadyLoaded = loaded.IsValid() && loaded.isLoaded;
        var scene = alreadyLoaded ? loaded : EditorSceneManager.OpenScene(WorldSystemsLabController.ScenePath,OpenSceneMode.Additive);
        try
        {
            var lab = scene.GetRootGameObjects().SelectMany(go => go.GetComponentsInChildren<WorldSystemsLabController>(true)).First();
            var data = new SerializedObject(lab);
            data.FindProperty("corridorPrefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<CorridorEvent>(PrefabPath);
            data.FindProperty("corridorKit").objectReferenceValue = config.kit;
            var corridor = AssetDatabase.LoadAssetAtPath<CorridorEvent>(PrefabPath);
            var pool = data.FindProperty("eventPrefabs");
            for (int i=0;i<pool.arraySize;i++)
                if (pool.GetArrayElementAtIndex(i).objectReferenceValue == null) pool.GetArrayElementAtIndex(i).objectReferenceValue = corridor;
            data.ApplyModifiedPropertiesWithoutUndo();
            foreach (var spawner in scene.GetRootGameObjects().SelectMany(go=>go.GetComponentsInChildren<WorldEventSpawner>(true)))
            {
                var spawnerData = new SerializedObject(spawner);
                var prefabs = spawnerData.FindProperty("eventPrefabs");
                for (int i=0;i<prefabs.arraySize;i++)
                    if (prefabs.GetArrayElementAtIndex(i).objectReferenceValue == null) prefabs.GetArrayElementAtIndex(i).objectReferenceValue = corridor;
                spawnerData.ApplyModifiedPropertiesWithoutUndo();
            }
            EditorSceneManager.SaveScene(scene);
        }
        finally { if (!alreadyLoaded) EditorSceneManager.CloseScene(scene,true); }
    }
}
#endif
