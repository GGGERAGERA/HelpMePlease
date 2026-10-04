#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
[InitializeOnLoad]
public static class SurfaceMarkerAuthoring
{
    const string Folder = "Assets/_Project/Data/SurfaceMap/";
    static SurfaceMarkerAuthoring() => EditorApplication.update += Poll;
    static void Poll()
    {
        const string request = "Artifacts/SurfaceMap/markers-author.request";
        if (!File.Exists(request) || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
        File.Delete(request);
        try { Author(); File.WriteAllText("Artifacts/SurfaceMap/markers-author-result.txt", "SUCCESS"); }
        catch (Exception e) { File.WriteAllText("Artifacts/SurfaceMap/markers-author-result.txt", e.ToString()); }
    }
    [MenuItem("Tools/Subject42/Surface Map/Author Marker Demo")]
    public static void Author()
    {
        var unknown = Marker("Unknown", "unknown", "?", true, true, new Color(1,.8f,.3f));
        var mission = Marker("Mission", "mission", "!", true, false, new Color(.3f,.8f,1));
        Marker("Danger", "danger", "!!", false, false, new Color(1,.35f,.25f));
        Marker("Reward", "reward", "$", false, false, new Color(.4f,1,.5f));
        var prefab = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/_Project/prefabs" })
            .Select(g => AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(g)))
            .Select(p => p.GetComponent<FalseSignalEvent>()).First(p => p != null && p.AllowedInSite);
        var data = new SerializedObject(prefab);
        if (string.IsNullOrEmpty(data.FindProperty("eventId").stringValue)) data.FindProperty("eventId").stringValue = "false-signal";
        data.FindProperty("eventTag").stringValue = "signal"; data.ApplyModifiedPropertiesWithoutUndo(); PrefabUtility.SavePrefabAsset(prefab.gameObject);
        var activity = Asset<SurfaceSectorContent>("Content_UnknownSignal", c => { c.id = "unknown-signal"; c.marker = unknown; c.requiredEvent = prefab; c.requiredEventTag = "signal"; });
        var objective = Asset<SurfaceSectorContent>("Content_SignalMission", c => { c.id = "signal-mission"; c.marker = mission; c.title = "SIGNAL INVESTIGATION"; c.description = "Find the source of the signal";
            c.requiredEvent = prefab; c.requiredEventTag = "signal"; c.completionState = SurfaceMarkerState.Completed; c.experienceMultiplier = 1.1f; c.goldMultiplier = 1.15f; });
        var sector = AssetDatabase.LoadAssetAtPath<SurfaceSectorDefinition>(Folder + "Sector_D1.asset");
        data = new SerializedObject(sector); var items = data.FindProperty("content");
        if (items.arraySize == 0) { items.arraySize = 1; items.GetArrayElementAtIndex(0).objectReferenceValue = activity; data.ApplyModifiedPropertiesWithoutUndo(); }
        var map = AssetDatabase.LoadAssetAtPath<SurfaceMapDefinition>(SurfaceMapProductionAuthoring.MapPath);
        data = new SerializedObject(map); var catalog = data.FindProperty("contentCatalog");
        foreach (var asset in new[] { activity, objective })
        {
            bool exists = false; for (int i = 0; i < catalog.arraySize; i++) if (catalog.GetArrayElementAtIndex(i).objectReferenceValue == asset) exists = true;
            if (!exists) { int i = catalog.arraySize; catalog.arraySize++; catalog.GetArrayElementAtIndex(i).objectReferenceValue = asset; }
        }
        data.ApplyModifiedPropertiesWithoutUndo(); AssetDatabase.SaveAssets();
    }
    static SurfaceMarker Marker(string name, string id, string icon, bool pulse, bool hidden, Color color) => Asset<SurfaceMarker>("Marker_" + name, m => { m.typeId=id; m.icon=icon; m.pulse=pulse; m.concealDetails=hidden; m.color=color; });
    static T Asset<T>(string name, Action<T> configure) where T : ScriptableObject
    {
        string path = Folder+name+".asset"; var asset = AssetDatabase.LoadAssetAtPath<T>(path);
        if (asset != null) return asset;
        asset = ScriptableObject.CreateInstance<T>(); configure(asset); AssetDatabase.CreateAsset(asset,path); return asset;
    }
}
#endif
