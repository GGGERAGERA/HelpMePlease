#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>Presentation-only authoring. Never changes sector definitions, saves or run configuration.</summary>
[InitializeOnLoad]
public static class ExpeditionMapAuthoring
{
    private const string Folder = "Artifacts/GeneratedQA/ExpeditionMap";
    private const string PrefabPath = "Assets/_Project/prefabs/UI/SurfaceMap/PF_SurfaceMap.prefab";
    private const string ArtPath = "Assets/_Project/art/UI/SurfaceMap/";
    private static TMP_FontAsset font;
    static ExpeditionMapAuthoring() => EditorApplication.update += Poll;
    private static void Poll()
    {
        if (!File.Exists(Folder + "/author.request") || EditorApplication.isCompiling ||
            EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
        File.Delete(Folder + "/author.request");
        try { Author(); File.WriteAllText(Folder + "/author-result.txt", "OK"); }
        catch (Exception error) { File.WriteAllText(Folder + "/author-result.txt", error.ToString()); Debug.LogException(error); }
    }
    private static T Read<T>(Object owner, string field) where T : Object =>
        (T)new SerializedObject(owner).FindProperty(field).objectReferenceValue;
    private static void Set(Object owner, string field, Object value)
    {
        var data = new SerializedObject(owner); data.FindProperty(field).objectReferenceValue = value;
        data.ApplyModifiedPropertiesWithoutUndo();
    }
    private static void Place(RectTransform rect, Vector2 position, Vector2 size)
    {
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f,.5f);
        rect.anchoredPosition = position; rect.sizeDelta = size;
    }
    private static void Text(TMP_Text label,float size)
    {
        label.font=font; label.fontSharedMaterial=font.material; label.fontSize=size; label.enableAutoSizing=false;
        label.textWrappingMode=TextWrappingModes.Normal; label.overflowMode=TextOverflowModes.Overflow;
        label.color=new Color(.76f,.78f,.71f); label.raycastTarget=false; label.lineSpacing=18;
        label.characterSpacing=0; label.alignment=TextAlignmentOptions.TopLeft;
    }
    private static void Button(Button button,float size)
    {
        button.transition=Selectable.Transition.None; button.GetComponent<Image>().color=Color.clear;
        Text(button.GetComponentInChildren<TMP_Text>(true),size);
        var textRect=button.GetComponentInChildren<TMP_Text>(true).rectTransform;
        textRect.anchorMin=Vector2.zero; textRect.anchorMax=Vector2.one;
        textRect.offsetMin=textRect.offsetMax=Vector2.zero;
        button.GetComponentInChildren<TMP_Text>(true).alignment=TextAlignmentOptions.MidlineLeft;
        foreach(var outline in button.GetComponents<Outline>())Object.DestroyImmediate(outline);
        var layout=button.GetComponent<LayoutElement>()??button.gameObject.AddComponent<LayoutElement>();
        layout.minHeight=layout.preferredHeight=28;
    }
    private static Image Image(string name,Transform parent,Sprite sprite,Vector2 size)
    {
        var go=new GameObject(name,typeof(RectTransform),typeof(Image)); go.transform.SetParent(parent,false);
        var image=go.GetComponent<Image>(); image.sprite=sprite; image.raycastTarget=false;
        Place(image.rectTransform,Vector2.zero,size); return image;
    }
    private static Sprite Sprite(string name) => AssetDatabase.LoadAssetAtPath<Sprite>(ArtPath+name+".png");

    private static void LaunchButton(Button button,Transform card)
    {
        Button(button,14);
        var slot=new GameObject("Launch slot",typeof(RectTransform),typeof(HorizontalLayoutGroup),typeof(LayoutElement));
        slot.transform.SetParent(card,false); slot.GetComponent<LayoutElement>().preferredHeight=38;
        var row=slot.GetComponent<HorizontalLayoutGroup>(); row.childAlignment=TextAnchor.MiddleLeft;
        row.childControlWidth=row.childControlHeight=true; row.childForceExpandWidth=row.childForceExpandHeight=false;
        button.transform.SetParent(slot.transform,false);
        var size=button.GetComponent<LayoutElement>(); size.minWidth=size.preferredWidth=146; size.minHeight=size.preferredHeight=38;
        foreach(var child in button.transform.Cast<Transform>().Where(item=>item.name=="Launch frame").ToArray())Object.DestroyImmediate(child.gameObject);
        SurfaceMapPlate Plate(string name,Transform parent,float inset,Color color,float cut)
        {
            var go=new GameObject(name,typeof(RectTransform),typeof(SurfaceMapPlate)); go.transform.SetParent(parent,false);
            var rect=(RectTransform)go.transform; rect.anchorMin=Vector2.zero; rect.anchorMax=Vector2.one;
            rect.offsetMin=Vector2.one*inset; rect.offsetMax=-Vector2.one*inset;
            var plate=go.GetComponent<SurfaceMapPlate>(); plate.color=color; plate.Cut=cut; plate.raycastTarget=false; return plate;
        }
        var frame=Plate("Launch frame",button.transform,0,Color.white,4);
        Plate("Face",frame.transform,2,new Color(.09f,.13f,.14f),2);
        button.transition=Selectable.Transition.ColorTint; button.targetGraphic=frame;
        var colours=button.colors; colours.normalColor=new Color(.59f,.52f,.34f);
        colours.highlightedColor=colours.selectedColor=new Color(.9f,.77f,.44f);
        colours.pressedColor=new Color(.43f,.38f,.26f); colours.disabledColor=new Color(.29f,.33f,.32f);
        colours.fadeDuration=.07f; colours.colorMultiplier=1; button.colors=colours;
        var label=button.GetComponentInChildren<TMP_Text>(true); label.alignment=TextAlignmentOptions.Center;
        label.transform.SetAsLastSibling();
    }

    [MenuItem("Tools/Subject42/Surface Map/Author Pixel World Map")]
    public static void Author()
    {
        Directory.CreateDirectory(Folder);
        foreach(var file in Directory.GetFiles(ArtPath,"*.png"))
        {
            var importer=(TextureImporter)AssetImporter.GetAtPath(file.Replace('\\','/'));
            importer.textureType=TextureImporterType.Sprite; importer.spriteImportMode=SpriteImportMode.Single;
            importer.filterMode=FilterMode.Point; importer.textureCompression=TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled=false; importer.alphaIsTransparency=true; importer.npotScale=TextureImporterNPOTScale.None;
            importer.maxTextureSize=2048; importer.spritePixelsPerUnit=100;
            var settings=new TextureImporterSettings(); importer.ReadTextureSettings(settings);
            settings.spriteMeshType=SpriteMeshType.FullRect; importer.SetTextureSettings(settings); importer.SaveAndReimport();
        }
        font=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/_Project/Fonts/Subject42 Menu Pixel.asset");
        var root=PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            var view=root.GetComponent<SurfaceMapView>();
            var graph=Read<RectTransform>(view,"graph"); var viewport=(RectTransform)graph.parent;
            var details=Read<TMP_Text>(view,"details"); var selection=Read<TMP_Text>(view,"selectionLabel");
            var start=Read<Button>(view,"startButton"); var close=Read<Button>(view,"closeButton");
            var scan=Read<Button>(view,"scanButton"); var node=Read<Button>(view,"nodeTemplate"); var line=Read<Image>(view,"lineTemplate");
            foreach(var item in new Transform[]{details.transform,selection.transform,start.transform,scan.transform}) item.SetParent(root.transform,false);
            var keep=new Transform[]{viewport,details.transform,selection.transform,start.transform,scan.transform,close.transform};
            foreach(var child in root.transform.Cast<Transform>().ToArray())if(!keep.Contains(child))Object.DestroyImmediate(child.gameObject);
            foreach(var child in graph.Cast<Transform>().ToArray())if(child!=node.transform&&child!=line.transform)Object.DestroyImmediate(child.gameObject);
            foreach(var child in viewport.Cast<Transform>().ToArray())if(child!=graph)Object.DestroyImmediate(child.gameObject);
            Place((RectTransform)root.transform,Vector2.zero,new Vector2(1280,800));
            var rootRect=(RectTransform)root.transform;
            rootRect.anchorMin=Vector2.zero; rootRect.anchorMax=Vector2.one; rootRect.offsetMin=rootRect.offsetMax=Vector2.zero;
            root.GetComponent<Image>().sprite=null; root.GetComponent<Image>().color=new Color(.08f,.114f,.14f);
            root.GetComponent<Image>().type=UnityEngine.UI.Image.Type.Simple;
            Place(viewport,Vector2.zero,new Vector2(1280,800)); viewport.GetComponent<Image>().color=Color.clear;
            viewport.anchorMin=Vector2.zero; viewport.anchorMax=Vector2.one; viewport.offsetMin=viewport.offsetMax=Vector2.zero;
            Place(graph,Vector2.zero,new Vector2(1280,800));
            var worldObject=new GameObject("World",typeof(RectTransform)); worldObject.transform.SetParent(graph,false);
            var world=(RectTransform)worldObject.transform; Place(world,Vector2.zero,new Vector2(1280,800));
            var terrain=worldObject.AddComponent<Image>(); terrain.sprite=Sprite("AshTerrain"); terrain.raycastTarget=false;
            Set(view,"world",world);
            var fogObject=new GameObject("Unexplored territory",typeof(RectTransform),typeof(SurfaceMapFog));
            fogObject.transform.SetParent(viewport,false); var fogRect=(RectTransform)fogObject.transform;
            fogRect.anchorMin=Vector2.zero; fogRect.anchorMax=Vector2.one; fogRect.offsetMin=fogRect.offsetMax=Vector2.zero;
            fogObject.GetComponent<SurfaceMapFog>().raycastTarget=false;
            Set(view,"fog",fogObject.GetComponent<SurfaceMapFog>());
            string[] ids={"BUNKER","A1","A2","C1","C2","D1","D2"};
            string[] art={"BunkerGate",null,"ruins-yard","anomaly-golden",null,"anomaly-stasis",null};
            SurfaceLocationFamily[] families={SurfaceLocationFamily.Auto,SurfaceLocationFamily.Ruins,SurfaceLocationFamily.Ruins,
                SurfaceLocationFamily.Anomalous,SurfaceLocationFamily.AdvancedAnomalous,SurfaceLocationFamily.Anomalous,SurfaceLocationFamily.Technogenic};
            Vector2[] points={new(296,224),new(172,157),new(125,76),new(420,163),new(522,78),new(213,301),new(460,317)};
            var data=new SerializedObject(view); var landmarks=data.FindProperty("landmarks"); landmarks.arraySize=ids.Length;
            for(int i=0;i<ids.Length;i++)
            {
                var item=landmarks.GetArrayElementAtIndex(i);
                item.FindPropertyRelative("Id").stringValue=ids[i];
                item.FindPropertyRelative("Position").vector2Value=new Vector2((points[i].x-320)*2,(200-points[i].y)*2);
                item.FindPropertyRelative("Sprite").objectReferenceValue=art[i]!=null?Sprite(art[i]):null;
                item.FindPropertyRelative("Family").enumValueIndex=(int)families[i];
                item.FindPropertyRelative("Fog").objectReferenceValue=null;
            }
            string[] paths={"B-A1","A1-A2","B-C1","C1-C2","B-D1","D1-D2"};
            var trails=data.FindProperty("trails"); trails.arraySize=paths.Length;
            for(int i=0;i<paths.Length;i++)
            {
                var ends=paths[i].Split('-'); var item=trails.GetArrayElementAtIndex(i);
                item.FindPropertyRelative("From").stringValue=ends[0]=="B"?null:ends[0];
                item.FindPropertyRelative("To").stringValue=ends[1]; item.FindPropertyRelative("Sprite").objectReferenceValue=Sprite(paths[i]);
                item.FindPropertyRelative("HintFrom").objectReferenceValue=Sprite(paths[i]+"-HintFrom");
                item.FindPropertyRelative("HintTo").objectReferenceValue=Sprite(paths[i]+"-HintTo");
            }
            data.FindProperty("selectedSprite").objectReferenceValue=Sprite("Selected");
            data.FindProperty("defaultCharacter").objectReferenceValue=AssetDatabase.LoadAssetAtPath<CharacterData>("Assets/_Project/Data/Characters/01_Gera.asset");
            string[] templates={"ruins","technogenic","anomaly-stasis","weather","advanced-anomaly"};
            var icons=data.FindProperty("locationIcons"); icons.arraySize=templates.Length;
            for(int i=0;i<templates.Length;i++)
            {
                var icon=icons.GetArrayElementAtIndex(i); icon.FindPropertyRelative("Family").enumValueIndex=i+1;
                icon.FindPropertyRelative("Sprite").objectReferenceValue=Sprite(templates[i]);
            }
            data.FindProperty("resetButton").objectReferenceValue=null;
            data.ApplyModifiedPropertiesWithoutUndo();

            var card=new GameObject("Sector card",typeof(RectTransform),typeof(Image),typeof(VerticalLayoutGroup),typeof(ContentSizeFitter));
            card.transform.SetParent(root.transform,false); var cardRect=(RectTransform)card.transform;
            Place(cardRect,Vector2.zero,new Vector2(308,0)); cardRect.pivot=new Vector2(0,1);
            card.GetComponent<Image>().color=new Color(.098f,.125f,.133f,.97f);
            // The local popover consumes pointer events, so dragging/clicking it never operates a sector underneath.
            card.GetComponent<Image>().raycastTarget=true;
            var layout=card.GetComponent<VerticalLayoutGroup>(); layout.padding=new RectOffset(16,16,14,12); layout.spacing=8;
            layout.childControlWidth=layout.childControlHeight=true; layout.childForceExpandWidth=true; layout.childForceExpandHeight=false;
            card.GetComponent<ContentSizeFitter>().verticalFit=ContentSizeFitter.FitMode.PreferredSize;
            selection.transform.SetParent(card.transform,false); Text(selection,16); selection.color=new Color(.81f,.82f,.74f); selection.gameObject.SetActive(true);
            details.transform.SetParent(card.transform,false); Text(details,13); details.text=""; details.gameObject.SetActive(false);
            scan.transform.SetParent(card.transform,false); Button(scan,14); scan.onClick=new Button.ButtonClickedEvent();
            LaunchButton(start,card.transform);
            Set(view,"card",cardRect);
            Button(close,18); Place((RectTransform)close.transform,new Vector2(590,354),new Vector2(56,56));
            var closeRect=(RectTransform)close.transform;
            closeRect.anchorMin=closeRect.anchorMax=Vector2.one; closeRect.anchoredPosition=new Vector2(-50,-46);
            close.transform.SetAsLastSibling(); close.GetComponentInChildren<TMP_Text>(true).text="×";
            close.GetComponentInChildren<TMP_Text>(true).alignment=TextAlignmentOptions.Center;
            foreach(var child in root.GetComponentsInChildren<Transform>(true))child.gameObject.layer=5;
            foreach(var label in root.GetComponentsInChildren<TMP_Text>(true)) { label.font=font; label.fontSharedMaterial=font.material; label.raycastTarget=false; }
            PrefabUtility.SaveAsPrefabAsset(root,PrefabPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
        AssetDatabase.SaveAssets();
    }
}
#endif
