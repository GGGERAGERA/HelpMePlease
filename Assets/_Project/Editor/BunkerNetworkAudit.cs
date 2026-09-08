using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class BunkerNetworkAudit
{
    static BunkerNetworkAudit() { EditorApplication.update += Command; }
    private static void Command()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
        const string request = "Artifacts/BunkerNetwork/refresh.request";
        if (!File.Exists(request)) return;
        File.Delete(request);
        AssetDatabase.Refresh();
    }
    private static string PathOf(Transform t) => t.parent == null ? t.name : PathOf(t.parent) + "/" + t.name;
    [MenuItem("Tools/Subject42/Bunker/Audit Navigation Bindings")]
    public static void Run()
    {
        var scene = SceneManager.GetActiveScene();
        if (scene.name != "MainMenu" || EditorApplication.isPlayingOrWillChangePlaymode) return;
        var all = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true)).ToArray();
        var lines = new System.Collections.Generic.List<string>();
        foreach (var c in all.SelectMany(t => t.GetComponents<MonoBehaviour>()).Where(c => c is BunkerRoomAccess || c is BunkerGateVisual || c is BunkerMinigame || c is FootballStartZone || c is BunkerStation))
        {
            lines.Add(PathOf(c.transform) + " position=" + c.transform.position + " active=" + c.gameObject.activeInHierarchy);
            var p = new SerializedObject(c).GetIterator();
            while (p.NextVisible(true))
            {
                if (p.propertyType == SerializedPropertyType.ObjectReference && p.objectReferenceValue is Component component)
                    lines.Add("  " + p.propertyPath + "=" + PathOf(component.transform) + " " + component.transform.position);
                else if (p.propertyType == SerializedPropertyType.ObjectReference && p.objectReferenceValue is GameObject go)
                    lines.Add("  " + p.propertyPath + "=" + PathOf(go.transform) + " " + go.transform.position);
            }
        }
        Directory.CreateDirectory("Artifacts/BunkerNetwork");
        foreach (var r in all.Select(t => t.GetComponent<Renderer>()).Where(r => r != null && r.gameObject.activeInHierarchy && r.bounds.max.x > 30 && r.bounds.min.x < 36 && r.bounds.max.y > -15 && r.bounds.min.y < -5))
            lines.Add("RENDER " + PathOf(r.transform) + " " + r.bounds + " sort=" + r.sortingLayerName + "/" + r.sortingOrder);
        File.WriteAllLines("Artifacts/BunkerNetwork/audit.txt", lines);
        BunkerNavigationPlayModeQA.Capture("audit-layout", 55, -4, 31);
    }
}
