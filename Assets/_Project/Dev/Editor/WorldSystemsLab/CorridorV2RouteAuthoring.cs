#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class CorridorRouteAuthoring
{
    public const string AssetFolder = "Assets/_Project/Data/WorldEvents/Corridor/Routes";
    private const string Request = "Artifacts/GeneratedQA/CorridorV2/build-routes.request";
    static CorridorRouteAuthoring() => EditorApplication.update += Poll;
    private static void Poll()
    {
        if (!File.Exists(Request) || EditorApplication.isCompiling || EditorApplication.isPlayingOrWillChangePlaymode) return;
        File.Delete(Request);
        Build();
        Directory.CreateDirectory("Artifacts/GeneratedQA/CorridorV2");
        File.WriteAllText("Artifacts/GeneratedQA/CorridorV2/build-routes.done", "Authored Straight, L and Zigzag route data are available.");
    }
    [MenuItem("Tools/Dev/World Systems Lab/Build Corridor V2 Route Data")]
    public static void Build()
    {
        Directory.CreateDirectory(AssetFolder);
        AssetDatabase.Refresh();
        foreach (var preset in new[] { CorridorRouteMode.Straight, CorridorRouteMode.L, CorridorRouteMode.Zigzag })
        {
            string path = AssetFolder + "/Corridor" + preset + ".asset";
            if (AssetDatabase.LoadAssetAtPath<CorridorRouteDefinition>(path) != null) continue;
            var definition = ScriptableObject.CreateInstance<CorridorRouteDefinition>();
            CorridorRouteDefaults.Populate(definition, preset);
            AssetDatabase.CreateAsset(definition, path);
        }
        AssetDatabase.SaveAssets();
    }
}
#endif
