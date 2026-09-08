using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
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
        var context = all.Select(t => t.GetComponent<BunkerContext>()).Single(c => c != null);
        var loadout = context.GetComponent<BunkerPlayerLoadoutController>();
        var player = (Transform)new SerializedObject(loadout).FindProperty("controlledPlayerRoot").objectReferenceValue;
        if (player == null)
            throw new InvalidOperationException("Bunker player reference is missing; repair scene binding first.");
        var panels = context.Panels;
        var intro = all.Select(t => t.GetComponent<BunkerIntroController>()).Single(c => c != null);
        var stations = all.Select(t => t.GetComponent<BunkerStation>()).Where(c => c != null && c.gameObject.activeInHierarchy).ToArray();
        BunkerStation Station(BunkerStationType type) => stations.Single(c => new SerializedObject(c).FindProperty("stationType").intValue == (int)type);
        if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/_Project/art", "BunkerNavigation");
        var material = AssetDatabase.LoadAssetAtPath<Material>(Folder + "/FloorLight.mat");
        if (material == null)
        {
            material = new Material(Shader.Find("Subject42/Bunker Floor Navigation"));
            AssetDatabase.CreateAsset(material, Folder + "/FloorLight.mat");
        }
        var old = scene.GetRootGameObjects().FirstOrDefault(r => r.name == "Bunker Floor Navigation");
        if (old != null) Undo.DestroyObjectImmediate(old);
        var root = new GameObject("Bunker Floor Navigation");
        Undo.RegisterCreatedObjectUndo(root, "Author bunker floor navigation");
        var view = root.AddComponent<BunkerNavigationView>();
        var routes = new BunkerNavigationView.Route[3];
        // Coordinates follow the existing door openings, with chamfered turns.
        routes[0] = Route(root, material, BunkerOnboardingStep.Character, Station(BunkerStationType.CharacterSelection),
            "01 / CHARACTER", new Vector2(37.2f, -3.35f), new[] {
                new Vector2(14.3f,-8.4f), new(25.8f,-8.4f), new(26.4f,-9f), new(36.7f,-9f),
                new(37.5f,-8.2f), new(37.5f,-2.4f) });
        routes[1] = Route(root, material, BunkerOnboardingStep.Weapon, Station(BunkerStationType.WeaponSelection),
            "02 / WEAPON", new Vector2(46.2f, -3.35f), new[] {
                new Vector2(37.2f,-2.4f), new(37.2f,-8.5f),
                new(38f,-9.3f), new(45.7f,-9.3f), new(46.5f,-8.5f), new(46.5f,-2.4f) });
        routes[2] = Route(root, material, BunkerOnboardingStep.RunGate, Station(BunkerStationType.StartRun),
            "03 / RUN", new Vector2(64f, -.7f), new[] {
                new Vector2(46.2f,-2.4f), new(46.2f,-8.8f),
                new(47f,-10.6f), new(63.2f,-10.6f), new(64f,-9.8f), new(64f,.2f) });
        var so = new SerializedObject(view);
        so.FindProperty("player").objectReferenceValue = player;
        so.FindProperty("panels").objectReferenceValue = panels;
        so.FindProperty("intro").objectReferenceValue = intro;
        var array = so.FindProperty("routes"); array.arraySize = routes.Length;
        for (int i = 0; i < routes.Length; i++)
        {
            var p = array.GetArrayElementAtIndex(i); var route = routes[i];
            p.FindPropertyRelative("step").enumValueIndex = (int)route.step;
            p.FindPropertyRelative("floor").objectReferenceValue = route.floor;
            p.FindPropertyRelative("station").objectReferenceValue = route.station;
            p.FindPropertyRelative("label").objectReferenceValue = route.label;
            p.FindPropertyRelative("title").stringValue = route.title;
        }
        so.ApplyModifiedPropertiesWithoutUndo();
        view.Apply(BunkerOnboardingStep.Character, true);
        EditorSceneManager.MarkSceneDirty(scene);
        AssetDatabase.SaveAssets();
        EditorSceneManager.SaveScene(scene);
    }

    private static BunkerNavigationView.Route Route(GameObject root, Material material,
        BunkerOnboardingStep step, BunkerStation station, string title, Vector2 labelPosition, Vector2[] points)
    {
        var go = new GameObject(step.ToString(), typeof(MeshFilter), typeof(MeshRenderer));
        go.transform.SetParent(root.transform, false);
        var vertices = new List<Vector3>(); var uv = new List<Vector2>(); var uv2 = new List<Vector2>(); var triangles = new List<int>();
        float distance = 0;
        void Strip(Vector2 a, Vector2 b, bool marker)
        {
            Vector2 normal = new Vector2(-(b-a).y, (b-a).x).normalized * .14f;
            int first = vertices.Count;
            vertices.Add(a-normal); vertices.Add(a+normal); vertices.Add(b-normal); vertices.Add(b+normal);
            float end = distance + Vector2.Distance(a,b);
            uv.Add(new(distance,-1)); uv.Add(new(distance,1)); uv.Add(new(end,-1)); uv.Add(new(end,1));
            for (int i=0;i<4;i++) uv2.Add(new Vector2(marker ? 1 : 0, 0));
            triangles.AddRange(new[]{first,first+1,first+2,first+2,first+1,first+3});
            distance = end;
        }
        for(int i=1;i<points.Length;i++) Strip(points[i-1],points[i],false);
        // Broken docking ring marks the interaction point, with no collider or input handler.
        var endpoint=points[points.Length-1];
        for(int i=0;i<48;i++)
        {
            if(i%12>=9) continue;
            float a=i*Mathf.PI/24, b=(i+1)*Mathf.PI/24;
            Strip(endpoint+new Vector2(Mathf.Cos(a),Mathf.Sin(a))*.43f,
                endpoint+new Vector2(Mathf.Cos(b),Mathf.Sin(b))*.43f,true);
        }
        string path=Folder+"/"+step+".asset";
        var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(path);
        bool create=mesh==null;
        if(create) mesh=new Mesh {name="FloorGuide_"+step};
        else mesh.Clear();
        mesh.SetVertices(vertices); mesh.SetUVs(0,uv); mesh.SetUVs(1,uv2); mesh.SetTriangles(triangles,0); mesh.RecalculateBounds();
        if(create) AssetDatabase.CreateAsset(mesh,path);
        else EditorUtility.SetDirty(mesh);
        go.GetComponent<MeshFilter>().sharedMesh=mesh;
        var renderer=go.GetComponent<MeshRenderer>(); renderer.sharedMaterial=material;
        renderer.sortingLayerName="Default"; renderer.sortingOrder=1;
        renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off; renderer.receiveShadows=false;
        var labelObject=new GameObject("Station legend",typeof(TextMeshPro)); labelObject.transform.SetParent(root.transform,false);
        labelObject.transform.position=labelPosition;
        var label=labelObject.GetComponent<TextMeshPro>();
        label.text=title; label.font=TMP_Settings.defaultFontAsset; label.fontSize=2; label.alignment=TextAlignmentOptions.Center;
        label.rectTransform.sizeDelta=new Vector2(7,1); label.textWrappingMode=TextWrappingModes.NoWrap;
        label.GetComponent<MeshRenderer>().sortingLayerName="Default"; label.GetComponent<MeshRenderer>().sortingOrder=2;
        return new BunkerNavigationView.Route {step=step,floor=renderer,station=station,label=label,title=title};
    }
}
