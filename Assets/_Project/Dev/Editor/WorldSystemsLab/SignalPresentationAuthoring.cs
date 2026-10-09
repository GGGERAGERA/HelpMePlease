#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

// Rebuilds the two authored presentations on explicit request only.
[InitializeOnLoad]
public static class SignalPresentationAuthoring
{
    private const string Root = "Assets/_Project/";
    private const string Art = Root + "art/WorldEvents/FalseSignal/";
    private const string Request = "Artifacts/GeneratedQA/FalseSignal/author.request";
    private static Material material;
    static SignalPresentationAuthoring() => EditorApplication.update += Poll;
    private static void Poll()
    {
        if (!File.Exists(Request) || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
        try { File.Delete(Request); Build(); File.WriteAllText(Request + ".result", "PASS: authored False Signal and Corridor entrance"); }
        catch (Exception e) { File.WriteAllText(Request + ".result", e.ToString()); Debug.LogException(e); }
    }
    [MenuItem("Tools/World Events/Author False Signal and Corridor Entrance")]
    public static void Build()
    {
        material = AssetDatabase.LoadAssetAtPath<Material>(Root + "Materials/Orbital/Visual.mat");
        Directory.CreateDirectory(Art);
        var body = Texture("Transmitter", 24, 32, (x,y) =>
        {
            Color32 ink = new(12, 19, 29, 255), metal = new(62, 79, 99, 255), edge = new(134, 151, 166, 255);
            if (x >= 2 && x <= 21 && y >= 1 && y <= 5) return y == 1 || x == 2 || x == 21 ? ink : metal;
            if (x >= 5 && x <= 18 && y >= 5 && y <= 14)
            {
                if (x == 5 || x == 18 || y == 14) return ink;
                if (y == 13 || x == 6) return edge;
                if (y < 8 && x > 8 && x < 16) return ink;
                return metal;
            }
            if (x >= 10 && x <= 13 && y >= 14 && y <= 27) return x == 11 ? edge : ink;
            if (y >= 26 && y <= 29 && x >= 7 && x <= 16) return y == 27 && x > 8 && x < 15 ? edge : ink;
            if ((x == 3 || x == 20) && y == 4) return new Color32(233, 180, 81, 255);
            return new Color32(0,0,0,0);
        });
        var light = Texture("Indicator", 4, 4, (x,y) => new Color32(255,255,255,255));
        var stable = Texture("StableEcho", 18, 11, (x,y) => y % 4 < 2 && x > 2 && x < 15 ? new Color32(154,255,243,255) : default);
        var broken = Texture("BrokenEcho", 18, 11, (x,y) =>
            ((y < 2 && x < 6) || (y > 3 && y < 6 && x > 7 && x < 13) || (y > 8 && x > 3 && x < 7)) ? new Color32(255,192,104,255) : default);
        var pulse = Texture("DiagnosticPulse", 9, 20, (x,y) =>
        {
            bool shape = x >= 2 && x <= 6 && y >= 1 && y <= 18;
            if (!shape) return default;
            return x == 2 || x == 6 || y == 1 || y == 18 ? new Color32(10,23,34,255) : new Color32(237,255,255,255);
        });
        var noise = Texture("Interference", 12, 5, (x,y) => (y == 1 && x < 7) || (y == 3 && x > 8) ? new Color32(213,233,242,255) : default);
        string path = Root + "prefabs/Environment/WorldEvents/FalseSignalPoint.prefab";
        var pointRoot = PrefabUtility.LoadPrefabContents(path);
        try
        {
            var point = pointRoot.GetComponent<FalseSignalPoint>();
            foreach (var line in pointRoot.GetComponents<LineRenderer>()) Object.DestroyImmediate(line);
            Remove(pointRoot.transform, "Presentation");
            var visual = Child(pointRoot.transform, "Presentation");
            var view = visual.AddComponent<FalseSignalPointView>();
            Set(view, "body", Sprite(visual.transform,"Device base and antenna",body,Vector2.zero,5));
            Set(view,"indicator",Sprite(visual.transform,"Status lamp",light,new Vector2(0,-.1f),7));
            Set(view,"stableResult",Sprite(visual.transform,"Coherent echo memory",stable,new Vector2(0,-1.2f),7));
            Set(view,"brokenResult",Sprite(visual.transform,"Fragmented echo memory",broken,new Vector2(0,-1.2f),7));
            var antenna = Child(visual.transform,"Emission origin"); antenna.transform.localPosition = new Vector3(0,.8f);
            Set(view,"antenna",antenna.transform);
            var interference = Enumerable.Range(0,3).Select(i => Sprite(visual.transform,"Local interference " + i,noise,
                new Vector2(i == 1 ? -.9f : .8f, .35f - i * .55f),6)).ToArray();
            var pulses = Enumerable.Range(0,3).Select(i => Sprite(visual.transform,"Diagnostic wavefront " + i,pulse,Vector2.zero,14)).ToArray();
            foreach(var item in pulses) item.enabled=false;
            Set(view,"interference",interference); Set(view,"pulses",pulses);
            view.Render(FalseSignalPointState.Unchecked,false,0,2.4f,Vector2.zero);
            var collider = pointRoot.GetComponent<CircleCollider2D>(); collider.radius=.7f; collider.offset=Vector2.zero;
            Set(point,"contactArea",collider); Set(point,"view",view); Set(point,"promptText","event.signal.activate");
            PrefabUtility.SaveAsPrefabAsset(pointRoot,path);
        }
        finally { PrefabUtility.UnloadPrefabContents(pointRoot); }
        AuthorFalseStart(); AuthorCorridor(); AssetDatabase.SaveAssets();
    }
    private static void AuthorFalseStart()
    {
        string path = Root + "prefabs/Environment/WorldEvents/FalseSignalEvent.prefab";
        var root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            Remove(root.transform,"Presentation");
            var visual = Child(root.transform,"Presentation");
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(Root + "art/WorldEvents/Corridor/GateEnergyNode.png");
            Sprite(visual.transform,"Receiver",sprite,Vector2.zero,5);
            var game = root.GetComponent<FalseSignalEvent>();
            Set(game,"startVisual",visual);
            Set(game,"timeLimit",90f);
            Set(game,"anomaly",AssetDatabase.LoadAssetAtPath<LocalAnomalyData>(Root + "Data/Anomalies/LocalAnomaly_Stasis.asset"));
            Set(game,"promptText","hud.startEvent");
            root.GetComponent<CircleCollider2D>().radius=2.5f;
            PrefabUtility.SaveAsPrefabAsset(root,path);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }
    private static void AuthorCorridor()
    {
        var area = Texture("CorridorStartArea",80,80,(x,y) =>
        {
            float dx=x-39.5f,dy=y-39.5f,r=Mathf.Sqrt(dx*dx+dy*dy);
            if(r>40) return default;
            bool corner=(Math.Abs(dx)>26 && Math.Abs(dx)<33 && Math.Abs(dy)>21 && Math.Abs(dy)<24) ||
                (Math.Abs(dy)>21 && Math.Abs(dy)<29 && Math.Abs(dx)>30 && Math.Abs(dx)<33);
            if(corner) return new Color32(255,226,139,255);
            if(r>37) return new Color32(11,22,35,230);
            if(r>36) return new Color32(160,234,245,230);
            return new Color32(22,72,100,82);
        });
        var beacon = Texture("CorridorBeacon",16,26,(x,y) =>
        {
            if(x<2||x>13||y<1||y>24) return default;
            if(x==2||x==13||y==1||y==24) return new Color32(9,17,28,255);
            if(y>15 && x>5 && x<10) return new Color32(255,221,120,255);
            if(y>5 && y<9) return new Color32(105,211,231,255);
            return x==3||y==23 ? new Color32(142,164,180,255) : new Color32(47,66,83,255);
        });
        string path=CorridorMigrationAuthoring.PrefabPath;
        var root=PrefabUtility.LoadPrefabContents(path);
        try
        {
            Remove(root.transform,"Presentation - Corridor activation area"); Remove(root.transform,"Start gates");
            var visual=Child(root.transform,"Start gates"); var graphics=Child(visual.transform,"Route direction");
            var view=visual.AddComponent<CorridorStartView>(); Set(view,"graphics",graphics.transform);
            Set(view,"area",Sprite(graphics.transform,"Actual activation circle",area,Vector2.zero,2));
            var left=Sprite(graphics.transform,"North beacon",beacon,new Vector2(0,2.5f),5);
            var right=Sprite(graphics.transform,"South beacon",beacon,new Vector2(0,-2.5f),5);
            Set(view,"beacons",new[]{left,right});
            var arrow=AssetDatabase.LoadAssetAtPath<Sprite>(Root+"art/WorldEvents/Corridor/GateDirectionArrow.png");
            var trails=Enumerable.Range(0,3).Select(i=>
            {
                var renderer=Sprite(graphics.transform,"Forward light " + i,arrow,new Vector2(-1.1f+i*1.1f,0),3);
                renderer.transform.localScale=Vector3.one*.3f; renderer.color=new Color(1,1,1,.8f); return renderer.transform;
            }).ToArray();
            Set(view,"flow",trails);
            var label=Child(visual.transform,"Start prompt").AddComponent<TextMeshPro>();
            label.font=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(Root+"Fonts/Subject42 UI SDF.asset");
            label.text="ВОЙДИТЕ В КОРИДОР"; label.fontSize=3.2f; label.color=new Color(1,.9f,.65f);
            label.alignment=TextAlignmentOptions.Center; label.textWrappingMode=TextWrappingModes.NoWrap;
            label.rectTransform.sizeDelta=new Vector2(9,1); label.transform.localPosition=new Vector3(0,-3.65f);
            label.GetComponent<MeshRenderer>().sortingLayerName="Midground"; label.GetComponent<MeshRenderer>().sortingOrder=6;
            Set(label.gameObject.AddComponent<LocalizedText>(),"localizationKey","event.corridor.start");
            Set(view,"prompt",label);
            var game=root.GetComponent<CorridorEvent>(); Set(game,"startView",view); Set(game,"startArea",root.GetComponent<CircleCollider2D>());
            PrefabUtility.SaveAsPrefabAsset(root,path);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }
    private static Sprite Texture(string name,int width,int height,Func<int,int,Color32> pixel)
    {
        var texture=new Texture2D(width,height,TextureFormat.RGBA32,false);
        var data=new Color32[width*height];
        for(int y=0;y<height;y++) for(int x=0;x<width;x++) data[y*width+x]=pixel(x,y);
        texture.SetPixels32(data); texture.Apply();
        string path=(name.StartsWith("Corridor", StringComparison.Ordinal) ? Root+"art/WorldEvents/Corridor/" : Art)+name+".png";
        File.WriteAllBytes(path,texture.EncodeToPNG()); Object.DestroyImmediate(texture);
        AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
        var importer=(TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType=TextureImporterType.Sprite; importer.spriteImportMode=SpriteImportMode.Single;
        importer.spritePixelsPerUnit=16; importer.filterMode=FilterMode.Point; importer.mipmapEnabled=false;
        importer.textureCompression=TextureImporterCompression.Uncompressed; importer.alphaIsTransparency=true;
        var settings=new TextureImporterSettings(); importer.ReadTextureSettings(settings); settings.spriteMeshType=SpriteMeshType.FullRect;
        importer.SetTextureSettings(settings); importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }
    private static SpriteRenderer Sprite(Transform parent,string name,Sprite sprite,Vector2 position,int order)
    {
        var renderer=Child(parent,name).AddComponent<SpriteRenderer>(); renderer.sprite=sprite;
        renderer.transform.localPosition=position; renderer.sortingLayerName="Midground"; renderer.sortingOrder=order;
        renderer.sharedMaterial=material; return renderer;
    }
    private static GameObject Child(Transform parent,string name)
    { var child=new GameObject(name); child.transform.SetParent(parent,false); return child; }
    private static void Remove(Transform parent,string name)
    { var child=parent.Find(name); if(child!=null) Object.DestroyImmediate(child.gameObject); }
    private static void Set(Object target,string field,object value) => OrbitalRelayAuthoring.Set(target,field,value);
}
#endif
