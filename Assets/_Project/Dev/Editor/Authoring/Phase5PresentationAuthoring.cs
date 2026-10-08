#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

// Authored Phase5 presentation. Rebuild is idempotent; no scene-composition redesign.
[InitializeOnLoad]
public static class Phase5PresentationAuthoring
{
    private const string Request = "Artifacts/GeneratedQA/Phase5/author.request";
    private static TMP_FontAsset font;
    private static readonly Color Ink=new(.025f,.055f,.08f,1f), Cyan=new(.34f,.85f,.94f,1f), Muted=new(.38f,.52f,.6f,1f);
    static Phase5PresentationAuthoring() => EditorApplication.update += Poll;
    private static void Poll()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode || !File.Exists(Request)) return;
        File.Delete(Request);
        try { Rebuild(); File.WriteAllText("Artifacts/GeneratedQA/Phase5/author.result","PASS: Phase5 presentation authored"); }
        catch(Exception error) { File.WriteAllText("Artifacts/GeneratedQA/Phase5/author.result",error.ToString()); Debug.LogException(error); }
    }
    private static T Read<T>(Object target,string field) where T : Object => (T)new SerializedObject(target).FindProperty(field).objectReferenceValue;
    private static void Set(Object target,string field,Object value) { var data=new SerializedObject(target); data.FindProperty(field).objectReferenceValue=value; data.ApplyModifiedPropertiesWithoutUndo(); }
    private static void Save(string path, Action<GameObject> apply)
    {
        var root=PrefabUtility.LoadPrefabContents(path);
        try { apply(root); PrefabUtility.SaveAsPrefabAsset(root,path); }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }
    [MenuItem("Tools/Subject42/Authoring/Rebuild UI Presentation")]
    public static void Rebuild()
    {
        if (SceneManager.GetActiveScene().isDirty) throw new InvalidOperationException("Save the current scene before presentation authoring.");
        var setup=EditorSceneManager.GetSceneManagerSetup();
        font=Read<TMP_Text>(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/prefabs/UI/SurfaceMap/PF_SurfaceMap.prefab").GetComponent<SurfaceMapView>(),"details").font;
        try
        {
            Save("Assets/_Project/prefabs/UI/SurfaceMap/PF_SurfaceMap.prefab",AuthorMap);
            Save("Assets/_Project/prefabs/UI/SceneTransitionOverlay.prefab",AuthorTransition);
            Save("Assets/_Project/prefabs/UI/SurfaceMap/PF_BunkerEvents.prefab",root => { var image=Read<Image>(root.GetComponent<BunkerEventManager>(),"fullscreenImage"); Set(image,"m_Sprite",null); image.enabled=false; });
            var tutorial=AuthorTutorial();
            Save("Assets/_Project/prefabs/Bootstrap/GameplayRun.prefab",root => {
                var flow=root.GetComponentInChildren<RunFlowController>(true);
                var presenter=root.GetComponentInChildren<TutorialOverlayPresenter>(true);
                if(presenter==null) presenter=((GameObject)PrefabUtility.InstantiatePrefab(tutorial,root.transform)).GetComponent<TutorialOverlayPresenter>();
                Set(flow,"tutorialPresentation",presenter);
            });
            AuthorPlayers(); MigrateEnemyPresentation(); AuthorSummary();
            foreach(string name in new[]{"StartScreen","MainMenu","MVP"})
            {
                var scene=EditorSceneManager.OpenScene("Assets/_Project/Scenes/MainBuild/"+name+".unity");
                var all=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<Component>(true)).ToArray();
                foreach(var hud in all.OfType<HUDManager>()) { Set(hud,"threat",all.OfType<RunThreatController>().Single()); Set(hud,"cameraShake",all.OfType<CameraShake>().Single()); }
                foreach(var presenter in all.OfType<TutorialOverlayPresenter>()) Set(presenter,"worldCamera",all.OfType<Camera>().Single(camera=>camera.CompareTag("MainCamera")));
                foreach(var atmosphere in all.OfType<StartScreenAtmosphere>()) AuthorCondensation(atmosphere);
                foreach(var summary in all.OfType<BunkerRunSummaryPresenter>().ToArray())
                {
                    if(Read<RectTransform>(summary,"notification")!=null) continue;
                    var instance=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/prefabs/UI/SurfaceMap/PF_BunkerRunSummary.prefab"),scene);
                    Object.DestroyImmediate(summary.gameObject);
                }
                EditorSceneManager.SaveScene(scene);
            }
            AssetDatabase.SaveAssets();
        }
        finally { EditorSceneManager.RestoreSceneManagerSetup(setup); }
    }
    public static void AuthorMap(GameObject root)
    {
        font=Read<TMP_Text>(root.GetComponent<SurfaceMapView>(),"details").font;
        if(root.transform.Find("Map Viewport")!=null)
        {
            var source=Read<TMP_Text>(root.GetComponent<SurfaceMapView>(),"details");
            foreach(var label in root.GetComponentsInChildren<TMP_Text>(true).Where(label=>new[]{"Uplink","Coordinates","Map caption","Selection","Navigation hint","Legend","Terminal ID"}.Contains(label.name)))
            { label.font=source.font;label.fontSharedMaterial=source.fontSharedMaterial; }
            return;
        }
        var view=root.GetComponent<SurfaceMapView>();
        var graph=Read<RectTransform>(view,"graph"); var nodeTemplate=Read<Button>(view,"nodeTemplate"); var lineTemplate=Read<Image>(view,"lineTemplate");
        var details=Read<TMP_Text>(view,"details"); var startButton=Read<Button>(view,"startButton"); var closeButton=Read<Button>(view,"closeButton");
        TMP_Text selectionLabel; SurfaceMapNavigation navigation;

        nodeTemplate.gameObject.SetActive(false); lineTemplate.gameObject.SetActive(false);
        var rootRect = (RectTransform)root.transform; rootRect.sizeDelta = new Vector2(1160,700);
        root.GetComponent<Image>().color = new Color(.025f,.045f,.065f,.99f);
        var title = root.transform.Find("Title").GetComponent<TMP_Text>();
        Place(title.rectTransform,new Vector2(-130,302),new Vector2(810,44));
        title.text = "SURFACE EXPEDITION MAP"; title.fontSize = 27; title.alignment = TextAlignmentOptions.Left; title.color = Cyan;
        Label("Uplink",root.transform,new Vector2(-110,269),new Vector2(850,24),"BUNKER UPLINK  /  SECTOR TELEMETRY  /  042",13,Muted);
        Bar(root.transform,new Vector2(0,247),new Vector2(1112,2),new Color(.17f,.4f,.49f));
        Bar(root.transform,new Vector2(-555,301),new Vector2(5,44),Cyan);
        var viewportObject = new GameObject("Map Viewport", typeof(RectTransform), typeof(Image), typeof(RectMask2D), typeof(SurfaceMapNavigation));
        var viewport = (RectTransform)viewportObject.transform; viewport.SetParent(graph.parent, false);
        Place(viewport,new Vector2(-185,-22),new Vector2(744,514));
        viewportObject.GetComponent<Image>().color = Ink;
        for (int x=-350;x<=350;x+=35) Bar(viewport,new Vector2(x,0),new Vector2(1,514),new Color(.1f,.25f,.3f,.26f));
        for (int y=-245;y<=245;y+=35) Bar(viewport,new Vector2(0,y),new Vector2(744,1),new Color(.1f,.25f,.3f,.26f));
        for (int y=-252;y<=252;y+=7) Bar(viewport,new Vector2(0,y),new Vector2(744,1),new Color(0,0,0,.09f));
        Label("Coordinates",viewport,new Vector2(-6,235),new Vector2(700,20),"X: 000   Y: 042                                     N / SURFACE",11,Muted);
        Label("Map caption",viewport,new Vector2(-6,-235),new Vector2(700,20),"SECTOR NETWORK                       UPLINK // STABLE",11,Muted);
        graph.SetParent(viewport, false); graph.anchoredPosition = new Vector2(-175,-62);
        navigation = viewportObject.GetComponent<SurfaceMapNavigation>(); navigation.Configure(graph);
        Bar(root.transform,new Vector2(204,-18),new Vector2(2,510),new Color(.16f,.33f,.4f));
        var card = Bar(root.transform,new Vector2(391,-2),new Vector2(330,473),new Color(.038f,.074f,.099f));
        card.transform.SetSiblingIndex(title.transform.GetSiblingIndex());
        selectionLabel = Label("Selection",root.transform,new Vector2(390,211),new Vector2(306,24),"SECTOR INTELLIGENCE",14,Cyan);
        Place(details.rectTransform,new Vector2(391,-5),new Vector2(298,386));
        details.fontSize=16; details.color=new Color(.72f,.83f,.87f); details.lineSpacing=8; details.richText=true;
        details.alignment=TextAlignmentOptions.TopLeft;
        Place(startButton.GetComponent<RectTransform>(),new Vector2(391,-260),new Vector2(330,49));
        StyleButton(startButton,"DEPLOY TO SECTOR",18,Cyan);
        Place(closeButton.GetComponent<RectTransform>(),new Vector2(530,303),new Vector2(44,38));
        StyleButton(closeButton,"X",18,Muted);
        Label("Navigation hint",root.transform,new Vector2(-230,-329),new Vector2(650,20),"DRAG TO PAN / SCROLL TO ZOOM",10,Muted);
        var legend = Label("Legend",root.transform,new Vector2(-190,-299),new Vector2(732,24),"<color=#57D9F0>AVAILABLE</color>  <color=#6BD9B5>COMPLETED</color>  <color=#3B5263>LOCKED</color>  <color=#FFCC4D>? UNKNOWN</color>  <color=#FFB247>! MISSION</color>",11,Muted);
        legend.richText=true;
        var reset=Object.Instantiate(closeButton,root.transform); reset.name="Reset View";
        Place((RectTransform)reset.transform,new Vector2(115,-329),new Vector2(134,23));
        StyleButton(reset,"RESET VIEW",10,Muted); reset.onClick=new Button.ButtonClickedEvent(); 
        Label("Terminal ID",root.transform,new Vector2(390,-307),new Vector2(330,25),"SUBJECT 42  //  EXPEDITION CONTROL",10,Muted);
        Set(view,"selectionLabel",selectionLabel); Set(view,"navigation",navigation); Set(view,"resetButton",reset);
    
    }
    private static void AuthorTransition(GameObject owner)
    {
        if(owner.GetComponentInChildren<SceneTransitionView>(true)!=null)
        {
            var failureLabel=Read<TextMeshProUGUI>(owner.GetComponentInChildren<SceneTransitionView>(true),"errorText");failureLabel.font=TMP_Settings.defaultFontAsset;return;
        }
        Canvas canvas; CanvasGroup group; Image scan;

        var root = new GameObject("Laboratory shutter", typeof(RectTransform), typeof(Canvas),
            typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(CanvasGroup));
        root.transform.SetParent(owner.transform, false);
        canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = short.MaxValue;
        var scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        group = root.GetComponent<CanvasGroup>();
        group.alpha = 0f;
        group.interactable = false;
        Image background = MakeImage("Cold blackout", root.transform, new Color(.012f, .022f, .038f, 1f));
        background.raycastTarget = true;
        for (int i = 0; i < 7; i++)
        {
            var seam = MakeImage("Panel seam", root.transform, new Color(.16f, .5f, .65f, .055f));
            seam.rectTransform.anchorMin = new Vector2((i + 1f) / 8f, 0f);
            seam.rectTransform.anchorMax = new Vector2((i + 1f) / 8f, 1f);
            seam.rectTransform.sizeDelta = new Vector2(1f, 0f);
        }
        scan = MakeImage("Connection scan", root.transform, Color.cyan);
        var scanRect = scan.rectTransform;
        scanRect.sizeDelta = new Vector2(3f, 0f);
        var signal = MakeImage("Connection indicator", root.transform, new Color(.2f, .65f, .8f, .45f));
        signal.rectTransform.anchorMin = signal.rectTransform.anchorMax = new Vector2(.5f, .12f);
        signal.rectTransform.sizeDelta = new Vector2(36f, 2f);
        canvas.enabled = false;
    
        var view=root.AddComponent<SceneTransitionView>();
        var label=Label("Connection recovery",root.transform,Vector2.zero,new Vector2(800,200),"",22,new Color(.4f,.75f,.85f));
        label.alignment=TextAlignmentOptions.Center; label.gameObject.SetActive(false);
        Set(view,"canvas",canvas); Set(view,"group",group); Set(view,"scan",scan); Set(view,"errorText",label);
        Set(owner.GetComponent<SceneTransitionOverlay>(),"view",view);
    }
    private static GameObject AuthorTutorial()
    {
        const string path="Assets/_Project/prefabs/UI/PF_TutorialOverlay.prefab";
        var existing=AssetDatabase.LoadAssetAtPath<GameObject>(path); if(existing!=null)
        {
            Save(path,authored=>Read<TextMeshProUGUI>(authored.GetComponent<TutorialOverlayPresenter>(),"caption").font=TMP_Settings.defaultFontAsset);return existing;
        }
        var root=new GameObject("Tutorial Overlay",typeof(RectTransform),typeof(Canvas),typeof(CanvasGroup),typeof(TutorialOverlayPresenter));
        try
        {
            var canvas=root.GetComponent<Canvas>(); canvas.renderMode=RenderMode.ScreenSpaceOverlay; canvas.sortingOrder=30000; canvas.enabled=false;
            var group=root.GetComponent<CanvasGroup>(); group.blocksRaycasts=false; group.interactable=false;
            var surface=new GameObject("Focus",typeof(RectTransform),typeof(TutorialOverlay)); surface.transform.SetParent(root.transform,false);
            var rect=(RectTransform)surface.transform; rect.anchorMin=Vector2.zero; rect.anchorMax=Vector2.one; rect.offsetMin=rect.offsetMax=Vector2.zero;
            var view=surface.GetComponent<TutorialOverlay>(); view.raycastTarget=false;
            var caption=Label("Instruction",surface.transform,Vector2.zero,new Vector2(1200,82),"",22,Color.white);
            caption.font=TMP_Settings.defaultFontAsset; caption.alignment=TextAlignmentOptions.Center; caption.enableAutoSizing=true; caption.textWrappingMode=TextWrappingModes.NoWrap;
            Set(root.GetComponent<TutorialOverlayPresenter>(),"view",view);Set(root.GetComponent<TutorialOverlayPresenter>(),"caption",caption);Set(root.GetComponent<TutorialOverlayPresenter>(),"overlayCanvas",canvas);
            return PrefabUtility.SaveAsPrefabAsset(root,path);
        }
        finally { Object.DestroyImmediate(root); }
    }
    private static void AuthorPlayers()
    {
        foreach(string guid in AssetDatabase.FindAssets("t:Prefab",new[]{"Assets/_Project/prefabs"}))
        {
            string path=AssetDatabase.GUIDToAssetPath(guid); var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if(prefab.GetComponent<PlayerHealth>()==null || prefab.GetComponent<PlayerHealthPresentation>()!=null)continue;
            Save(path,root=>{
                var presenter=root.AddComponent<PlayerHealthPresentation>();Set(presenter,"health",root.GetComponent<PlayerHealth>());Set(presenter,"flash",root.GetComponent<PlayerWhiteFlash>());Set(presenter,"hitSound",root.GetComponent<PlayerHitSound>());
                var data=new SerializedObject(presenter);var list=data.FindProperty("renderers");var renderers=root.GetComponentsInChildren<SpriteRenderer>(true);list.arraySize=renderers.Length;for(int i=0;i<renderers.Length;i++)list.GetArrayElementAtIndex(i).objectReferenceValue=renderers[i];data.ApplyModifiedPropertiesWithoutUndo();
            });
        }
    }
    private static void MigrateEnemyPresentation()
    {
        foreach(string guid in AssetDatabase.FindAssets("t:Prefab",new[]{"Assets/_Project/prefabs"}))
        {
            string path=AssetDatabase.GUIDToAssetPath(guid);var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if(prefab.GetComponent<EnemyHealth>()==null)continue;
            if(prefab.GetComponent<EnemyHealthPresentation>()==null)throw new InvalidOperationException("Author local enemy presentation on "+path);
            Save(path,root=>{}); // Serialize the migrated schema without obsolete health FX fields.
        }
    }
    private static void AuthorCondensation(StartScreenAtmosphere atmosphere)
    {
        if(Read<RawImage>(atmosphere,"condensation")!=null)return;
        var glass=new GameObject("Capsule condensation",typeof(RectTransform),typeof(RawImage));glass.layer=atmosphere.gameObject.layer;glass.transform.SetParent(atmosphere.transform,false);
        var image=glass.GetComponent<RawImage>();image.raycastTarget=false;image.color=Color.clear;
        var rect=image.rectTransform;rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.one;rect.offsetMin=rect.offsetMax=Vector2.zero;Set(atmosphere,"condensation",image);
    }
    private static void AuthorSummary()
    {
        const string path="Assets/_Project/prefabs/UI/SurfaceMap/PF_BunkerRunSummary.prefab";
        if(AssetDatabase.LoadAssetAtPath<GameObject>(path)!=null)return;
        var templates=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/prefabs/UI/SurfaceMap/PF_BunkerSummaryTemplates.prefab");
        var root=new GameObject("PostRunNotification",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(BunkerRunSummaryPresenter));
        try
        {
            root.GetComponent<Canvas>().renderMode=RenderMode.ScreenSpaceOverlay;root.GetComponent<Canvas>().sortingOrder=1100;
            var scaler=root.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1920,1080);scaler.matchWidthOrHeight=.5f;scaler.screenMatchMode=CanvasScaler.ScreenMatchMode.Expand;
            var panel=Object.Instantiate(templates.transform.Find("Panel").gameObject,root.transform).GetComponent<RectTransform>();panel.name="Notification";
            var group=panel.gameObject.AddComponent<CanvasGroup>();group.blocksRaycasts=false;group.interactable=false;
            panel.anchorMin=panel.anchorMax=panel.pivot=new Vector2(.5f,1f);panel.anchoredPosition=new Vector2(0,-24);panel.sizeDelta=new Vector2(520,100);panel.localScale=Vector3.one;
            var layout=panel.GetComponent<LayoutElement>();if(layout!=null)layout.ignoreLayout=true;
            var title=panel.GetComponentInChildren<TextMeshProUGUI>(true);
            var gold=Object.Instantiate(templates.transform.Find("Gold").gameObject,panel).GetComponent<TextMeshProUGUI>();gold.name="GoldEarned";
            var extra=Object.Instantiate(gold,panel);extra.name="ExtraGold";
            foreach(var graphic in panel.GetComponentsInChildren<Graphic>(true))graphic.raycastTarget=false;
            var presenter=root.GetComponent<BunkerRunSummaryPresenter>();Set(presenter,"notification",panel);Set(presenter,"notificationGroup",group);Set(presenter,"displayedTitle",title);Set(presenter,"displayedGold",gold);Set(presenter,"displayedExtraGold",extra);
            panel.gameObject.SetActive(false);PrefabUtility.SaveAsPrefabAsset(root,path);
        }
        finally { Object.DestroyImmediate(root); }
    }
    private static TMP_Text Label(string name,Transform parent,Vector2 position,Vector2 size,string value,float fontSize,Color color)
    {
        var go=new GameObject(name,typeof(RectTransform),typeof(TextMeshProUGUI)); go.transform.SetParent(parent,false);
        var label=go.GetComponent<TMP_Text>(); label.font=font;
        Place(label.rectTransform,position,size); label.text=value; label.fontSize=fontSize; label.color=color;
        label.raycastTarget=false; label.alignment=TextAlignmentOptions.Left; return label;
    }
    private static void Place(RectTransform rect,Vector2 position,Vector2 size)
    { rect.anchorMin=rect.anchorMax=rect.pivot=new Vector2(.5f,.5f); rect.anchoredPosition=position; rect.sizeDelta=size; }
    private static Image Bar(Transform parent,Vector2 position,Vector2 size,Color color)
    {
        var go=new GameObject("Terminal detail",typeof(RectTransform),typeof(Image)); go.transform.SetParent(parent,false);
        var image=go.GetComponent<Image>(); image.color=color; image.raycastTarget=false; Place(image.rectTransform,position,size); return image;
    }
    private static void StyleButton(Button button,string text,float size,Color color)
    {
        button.GetComponent<Image>().color=new Color(.08f,.21f,.28f);
        var label=button.GetComponentInChildren<TMP_Text>(); label.text=text; label.fontSize=size; label.color=color;
        Place(label.rectTransform,Vector2.zero,button.GetComponent<RectTransform>().sizeDelta-new Vector2(8,0));
    }
    private static Image MakeImage(string name, Transform parent, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(parent, false);
        var image = go.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        image.rectTransform.anchorMin = Vector2.zero;
        image.rectTransform.anchorMax = Vector2.one;
        image.rectTransform.sizeDelta = Vector2.zero;
        return image;
    }
}
#endif
