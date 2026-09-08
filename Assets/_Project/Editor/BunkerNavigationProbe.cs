using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class BunkerNavigationProbe
{
    static BunkerNavigationProbe()
    {
        EditorApplication.delayCall += Inspect;
        EditorApplication.update += Command;
    }
    private static void Command()
    {
        const string request = "Assets/_Project/Documentation/BunkerNavigationQA/author.request";
        if (!File.Exists(request) || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
        File.Delete(request);
        try { BunkerNavigationAuthoring.Build(); File.WriteAllText(request + ".result", "Authored and saved."); }
        catch (System.Exception e) { File.WriteAllText(request + ".result", e.ToString()); }
    }
    private static void Inspect()
    {
        const string output = "Assets/_Project/Documentation/BunkerNavigationQA";
        if (File.Exists(output + "/bindings.txt") || EditorApplication.isPlayingOrWillChangePlaymode) return;
        Directory.CreateDirectory(output);
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (scene.name != "MainMenu") return;
        try
        {
            var all = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true)).ToArray();
            var bindings = new System.Collections.Generic.List<string>();
            foreach (var t in all.Where(t => t.gameObject.activeInHierarchy))
            {
                var renderer = t.GetComponent<Renderer>();
                if (renderer != null && t.GetComponent<UnityEngine.Tilemaps.Tilemap>() != null)
                    bindings.Add("TILE " + t.name + " " + renderer.sortingLayerName + "/" + renderer.sortingOrder + " " + renderer.bounds);
                foreach(var c in t.GetComponents<MonoBehaviour>().Where(c=>c is BunkerContext || c is BunkerPlayerLoadoutController || c is BunkerIntroController || c is CameraFollow || c is BunkerRunStarter))
                {
                    var so=new SerializedObject(c); var p=so.GetIterator();
                    while(p.NextVisible(true)) if(p.propertyType==SerializedPropertyType.ObjectReference)
                        bindings.Add(c.GetType().Name+"."+p.propertyPath+"="+(p.objectReferenceValue!=null ? p.objectReferenceValue.name+" #"+p.objectReferenceValue.GetInstanceID() : "NULL"));
                }
            }
            File.WriteAllLines(output+"/bindings.txt",bindings);
            File.WriteAllLines(output + "/active.txt", all.Where(t => t.gameObject.activeInHierarchy && (t.GetComponent<BunkerStation>() || t.GetComponent<BunkerPlayerLoadoutController>() || t.GetComponent<CharacterMovement2D>())).Select(t => t.name + " " + t.position + " " + (t.GetComponent<BunkerPlayerLoadoutController>() ? EditorJsonUtility.ToJson(t.GetComponent<BunkerPlayerLoadoutController>()) : "")));
            File.WriteAllLines(output + "/layout.txt", all.Where(t =>
                t.GetComponent<BunkerStation>() || t.GetComponent<BunkerRoomAccess>() ||
                t.GetComponent<Camera>() || t.GetComponent<CharacterMovement2D>() ||
                t.GetComponent<BunkerContext>() || t.GetComponent<UnityEngine.Tilemaps.TilemapRenderer>())
                .Select(t => t.name + " pos=" + t.position + " components=" + string.Join(",", t.GetComponents<Component>().Select(c => c ? c.GetType().Name : "MISSING")) +
                (t.GetComponent<Renderer>() is Renderer r && r != null ? " layer=" + r.sortingLayerName + " order=" + r.sortingOrder : "") +
                (t.GetComponent<BunkerStation>() is BunkerStation s && s != null ? " station=" + new SerializedObject(s).FindProperty("stationType").intValue : "")));
            var camera = all.Select(t => t.GetComponent<Camera>()).First(c => c && c.orthographic);
            var oldPosition = camera.transform.position;
            var oldSize = camera.orthographicSize;
            camera.transform.position = new Vector3(33, -3, -10);
            camera.orthographicSize = 26;
            var rt = new RenderTexture(1800, 1200, 24);
            camera.targetTexture = rt; camera.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(1800, 1200, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, 1800, 1200), 0, 0); tex.Apply();
            File.WriteAllBytes(output + "/active.png", tex.EncodeToPNG());
            RenderTexture.active = null; camera.targetTexture = null;
            Object.DestroyImmediate(tex); Object.DestroyImmediate(rt);
            camera.transform.position = oldPosition; camera.orthographicSize = oldSize;
        }
        finally { }
    }
}

