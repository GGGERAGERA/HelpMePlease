using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class BunkerNavigationAuthoring
{
    private const string Folder = "Assets/_Project/art/BunkerNavigation";

    [MenuItem("Tools/Subject42/Bunker/Rebuild Authored Navigation")]
    public static void Build()
    {
        var scene = SceneManager.GetActiveScene();
        if (EditorApplication.isPlayingOrWillChangePlaymode || scene.name != "MainMenu")
            throw new InvalidOperationException("Open MainMenu in Edit Mode.");
        var all = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true)).ToArray();
        var rooms = all.Select(t => t.GetComponent<BunkerRoomAccess>()).Where(c => c != null && c.gameObject.activeInHierarchy).ToArray();
        foreach (BunkerRoomId id in Enum.GetValues(typeof(BunkerRoomId)))
            if (rooms.Count(r => r.RoomId == id) != 1) throw new InvalidOperationException("Ambiguous room binding: " + id);
        var gate = all.Select(t => t.GetComponent<BunkerGateVisual>()).Single(c => c != null && c.gameObject.activeInHierarchy);
        var mini = all.Select(t => t.GetComponent<FootballMinigame>()).Single(c => c != null && c.gameObject.activeInHierarchy);
        var start = mini.transform.parent.GetComponentInChildren<FootballStartZone>(true);
        if (start == null) throw new InvalidOperationException("Football arena has no start zone.");
        var material = AssetDatabase.LoadAssetAtPath<Material>(Folder + "/FloorLight.mat");
        if (material == null) throw new InvalidOperationException("Existing FloorLight material is missing.");
        // The base floor previously tied with props at order 0; reserve -1 for that tilemap.
        // Navigation can then use 0 without drawing across foreground lamp sprites.
        var baseFloor = all.Where(t => t.name == "Tilemap2" && t.gameObject.activeInHierarchy)
            .Select(t => t.GetComponent<UnityEngine.Tilemaps.TilemapRenderer>()).Single(r => r != null);
        Undo.RecordObject(baseFloor, "Place guidance above bunker floor");
        baseFloor.sortingOrder = -1;
        var old = scene.GetRootGameObjects().SingleOrDefault(r => r.name == "Bunker Floor Navigation");
        if (old != null) Undo.DestroyObjectImmediate(old);
        var root = new GameObject("Bunker Floor Navigation");
        Undo.RegisterCreatedObjectUndo(root, "Author bunker floor network");
        var view = root.AddComponent<BunkerNavigationView>();
        var main = Mesh(root, material, "MainCorridor", new Vector2[] {
            new(14.3f,-8.4f), new(25.8f,-8.4f), new(26.4f,-9f), new(30f,-9f),
            new(31f,-10f), new(31.4f,-10.4f), new(31.8f,-10.4f), new(33.9f,-10.4f), new(34.4f,-10.4f),
            new(36f,-12f), new(64f,-12f) }, false, 7);
        var routes = new List<BunkerNavigationView.Route>();
        foreach (var room in rooms.OrderBy(r => (int)r.RoomId))
        {
            string name = room.RoomId == BunkerRoomId.CharacterSelection ? "Character" :
                room.RoomId == BunkerRoomId.WeaponSelection ? "Weapon" : room.RoomId.ToString();
            Vector2 entrance = room.transform.position;
            float direction = entrance.y > -12f ? 1f : -1f;
            // Stop at the corridor side of the doorway; never continue through furniture.
            Vector2 end = entrance - Vector2.up * direction * (direction > 0 ? .65f : 4.2f);
            var floor = Mesh(root, material, name, new Vector2[] { new(entrance.x,-12f), end }, true);
            routes.Add(new BunkerNavigationView.Route { destination = name, floor = floor, room = room, brightness = .7f });
        }
        var gateFloor = Mesh(root, material, "RunGate", new Vector2[] { new(64,-12), new(64,.2f) }, true);
        routes.Add(new BunkerNavigationView.Route { destination = "RunGate", floor = gateFloor,
            gate = gate, brightness = 1 });
        Vector2 miniEnd = (Vector2)start.transform.position - Vector2.up * 4.8f;
        var miniFloor = Mesh(root, material, "MiniGame", new Vector2[] {
            new(64,-12), new(miniEnd.x-.8f,-12), new(miniEnd.x,-11.2f), miniEnd }, true);
        routes.Add(new BunkerNavigationView.Route { destination = "MiniGame", floor = miniFloor, minigame = mini, brightness = .7f });
        var so = new SerializedObject(view);
        so.FindProperty("mainLine").objectReferenceValue = main;
        var array = so.FindProperty("routes"); array.arraySize = routes.Count;
        for (int i = 0; i < routes.Count; i++)
        {
            var p = array.GetArrayElementAtIndex(i); var route = routes[i];
            p.FindPropertyRelative("destination").stringValue = route.destination;
            p.FindPropertyRelative("floor").objectReferenceValue = route.floor;
            p.FindPropertyRelative("room").objectReferenceValue = route.room;
            p.FindPropertyRelative("station").objectReferenceValue = route.station;
            p.FindPropertyRelative("gate").objectReferenceValue = route.gate;
            p.FindPropertyRelative("minigame").objectReferenceValue = route.minigame;
            p.FindPropertyRelative("brightness").floatValue = route.brightness;
        }
        so.ApplyModifiedPropertiesWithoutUndo();
        // Preview serialized defaults without invoking gameplay controllers in Edit Mode.
        main.enabled = true;
        foreach (var route in routes)
        {
            route.floor.enabled = route.room != null ? route.room.DefaultUnlocked : route.minigame != null;
            if (route.gate != null) route.floor.enabled = true;
        }
        EditorSceneManager.MarkSceneDirty(scene);
        AssetDatabase.SaveAssets();
        EditorSceneManager.SaveScene(scene);
    }

    private static MeshRenderer Mesh(GameObject root, Material material, string name, Vector2[] points, bool endpointMarker, int occludedSegment = -1)
    {
        var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
        go.transform.SetParent(root.transform, false);
        // Floor tilemap is Default/0 at z=0. Stay just above it and below furniture (order >=1).
        go.transform.localPosition = new Vector3(0, 0, -.01f);
        var vertices = new List<Vector3>(); var uv = new List<Vector2>(); var uv2 = new List<Vector2>(); var triangles = new List<int>();
        float distance = 0;
        void Strip(Vector2 a, Vector2 b, bool marker)
        {
            Vector2 normal = new Vector2(-(b-a).y, (b-a).x).normalized * .11f;
            int first = vertices.Count;
            vertices.Add(a-normal); vertices.Add(a+normal); vertices.Add(b-normal); vertices.Add(b+normal);
            float end = distance + Vector2.Distance(a,b);
            uv.Add(new(distance,-1)); uv.Add(new(distance,1)); uv.Add(new(end,-1)); uv.Add(new(end,1));
            for (int i=0;i<4;i++) uv2.Add(new Vector2(marker ? 1 : 0, 0));
            triangles.AddRange(new[]{first,first+1,first+2,first+2,first+1,first+3});
            distance = end;
        }
        // The tall pink lamp projects over the walkable gap. Leave its covered floor unlit
        // instead of painting guidance on its transparent sprite/SortingGroup.
        for(int i=1;i<points.Length;i++)
        {
            if (i == occludedSegment) { distance += Vector2.Distance(points[i-1], points[i]); continue; }
            Strip(points[i-1],points[i],false);
        }
        // Broken docking ring marks the interaction point, with no collider or input handler.
        var endpoint=points[points.Length-1];
        for(int i=0; endpointMarker && i<48;i++)
        {
            if(i%12>=9) continue;
            float a=i*Mathf.PI/24, b=(i+1)*Mathf.PI/24;
            Strip(endpoint+new Vector2(Mathf.Cos(a),Mathf.Sin(a))*.25f,
                endpoint+new Vector2(Mathf.Cos(b),Mathf.Sin(b))*.25f,true);
        }
        string path=Folder+"/"+name+".asset";
        var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(path);
        bool create=mesh==null;
        if(create) mesh=new Mesh {name="FloorGuide_"+name};
        else mesh.Clear();
        mesh.SetVertices(vertices); mesh.SetUVs(0,uv); mesh.SetUVs(1,uv2); mesh.SetTriangles(triangles,0); mesh.RecalculateBounds();
        if(create) AssetDatabase.CreateAsset(mesh,path);
        else EditorUtility.SetDirty(mesh);
        go.GetComponent<MeshFilter>().sharedMesh=mesh;
        var renderer=go.GetComponent<MeshRenderer>(); renderer.sharedMaterial=material;
        renderer.sortingLayerName="Default"; renderer.sortingOrder=0;
        renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off; renderer.receiveShadows=false;
        return renderer;
    }
}
