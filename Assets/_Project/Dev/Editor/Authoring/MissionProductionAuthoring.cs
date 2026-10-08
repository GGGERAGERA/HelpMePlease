#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object=UnityEngine.Object;

/// <summary>Explicit, targeted mission authoring; no existing room or minigame authoring is invoked.</summary>
[InitializeOnLoad]
public static class MissionProductionAuthoring
{
    public const string CatalogPath="Assets/_Project/Data/Missions/MissionCatalog_Production.asset";
    public const string MissionPath="Assets/_Project/Data/Missions/MISSION_SIGNAL_TRACE.asset";
    public const string ProviderPath="Assets/_Project/prefabs/Bunker/Missions/PF_MissionOperator.prefab";
    public const string PanelPath="Assets/_Project/prefabs/Bunker/Missions/PF_MissionPanel.prefab";
    const string Output="Artifacts/GeneratedQA/Missions";
    static MissionProductionAuthoring() { EditorApplication.update+=Poll; }
    static void Poll()
    {
        string recover=Output+"/recover-test.request";
        if(File.Exists(recover))
        {
            if(EditorApplication.isCompiling||EditorApplication.isUpdating)return;
            if(EditorApplication.isPlaying) { EditorApplication.isPlaying=false; return; }
            if(EditorApplication.isPlayingOrWillChangePlaymode)return;
            var cleanup=CoreTestSupport.CleanupPlayMode(); while(cleanup.MoveNext()) { }
            MissionProductionFlowTests.RestoreExtraPreferences(); File.Delete(recover); return;
        }
        string request=Output+"/author.request";
        if(!File.Exists(request)||EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode)return;
        File.Delete(request);
        try { Author(); File.WriteAllText(Output+"/author-result.txt","SUCCESS"); }
        catch(Exception e) { File.WriteAllText(Output+"/author-result.txt",e.ToString()); Debug.LogException(e); }
    }
    static void Set(Object target,string name,object value)
    {
        for(var type=target.GetType();type!=null;type=type.BaseType)
        {
            var field=type.GetField(name,BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public|BindingFlags.DeclaredOnly);
            if(field==null)continue;
            field.SetValue(target,value); EditorUtility.SetDirty(target);
            if(PrefabUtility.IsPartOfPrefabInstance(target))PrefabUtility.RecordPrefabInstancePropertyModifications(target);
            return;
        }
        throw new InvalidOperationException(target.GetType().Name+"."+name);
    }
    static T Asset<T>(string path,Action<T> configure) where T:ScriptableObject
    {
        var value=AssetDatabase.LoadAssetAtPath<T>(path);
        if(value==null) { value=ScriptableObject.CreateInstance<T>(); AssetDatabase.CreateAsset(value,path); }
        configure(value); EditorUtility.SetDirty(value); return value;
    }
    static T[] All<T>(Scene scene) where T:Component => scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<T>(true)).ToArray();
    [MenuItem("Tools/Subject42/Missions/Author Production Slice")]
    public static void Author()
    {
        Directory.CreateDirectory(Output); Directory.CreateDirectory("Assets/_Project/Data/Missions"); Directory.CreateDirectory("Assets/_Project/prefabs/Bunker/Missions"); AssetDatabase.Refresh();
        var scene=SceneManager.GetSceneByPath(SurfaceMapProductionAuthoring.BunkerPath);
        bool opened=!scene.IsValid();
        if(!opened&&scene.isDirty)throw new InvalidOperationException("Production bunker has unsaved edits. Save it before mission authoring.");
        if(opened)scene=EditorSceneManager.OpenScene(SurfaceMapProductionAuthoring.BunkerPath,OpenSceneMode.Additive);
        var prior=SceneManager.GetActiveScene(); SceneManager.SetActiveScene(scene);
        try
        {
            var unknown=AssetDatabase.LoadAssetAtPath<SurfaceSectorContent>("Assets/_Project/Data/SurfaceMap/Content_UnknownSignal.asset");
            if(unknown==null)throw new InvalidOperationException("Unknown signal content is missing.");
            var marker=AssetDatabase.LoadAssetAtPath<SurfaceMarker>("Assets/_Project/Data/SurfaceMap/Marker_Mission.asset");
            var content=Asset<SurfaceSectorContent>("Assets/_Project/Data/Missions/Content_SignalTrace.asset",c=>
            {
                c.id="mission-signal-trace"; c.marker=marker; c.title="SIGNAL TRACE"; c.description="Locate the signal source.";
                c.requiredEvent=unknown.requiredEvent; c.requiredEventTag=unknown.requiredEventTag;
                c.experienceMultiplier=c.goldMultiplier=c.threatGrowthMultiplier=c.spawnPressureMultiplier=1;
            });
            var objective=Asset<CompleteEventObjectiveDefinition>("Assets/_Project/Data/Missions/Objective_SignalTrace.asset",o=>
            { Set(o,"id","investigate-signal"); Set(o,"description","Investigate False Signal"); Set(o,"content",content); });
            var reward=Asset<MissionRewardDefinition>("Assets/_Project/Data/Missions/Reward_SignalTrace.asset",r=>Set(r,"gold",100));
            var mission=Asset<MissionDefinition>(MissionPath,m=>
            {
                Set(m,"missionId","MISSION_SIGNAL_TRACE"); Set(m,"targetSectorId","D1"); Set(m,"title","SIGNAL TRACE");
                Set(m,"offerText","Мы поймали нестабильный сигнал.\nПроверь сектор D1 и найди источник."); Set(m,"description","Locate the signal source.");
                Set(m,"objectives",new MissionObjectiveDefinition[]{objective}); Set(m,"reward",reward); Set(m,"presentation",content);
            });
            var catalog=Asset<MissionCatalog>(CatalogPath,c=>Set(c,"missions",new[]{mission}));
            // Validate data without touching player storage.
            var map=AssetDatabase.LoadAssetAtPath<SurfaceMapDefinition>(SurfaceMapProductionAuthoring.MapPath);
            new MissionService(catalog,map,new ValidationStorage());
            string compositionPath="Assets/_Project/prefabs/Bootstrap/ProductionSceneComposition.prefab";
            var composition=PrefabUtility.LoadPrefabContents(compositionPath);
            try { Set(composition.GetComponent<ProductionSceneComposition>(),"missions",catalog); PrefabUtility.SaveAsPrefabAsset(composition,compositionPath); }
            finally { PrefabUtility.UnloadPrefabContents(composition); }
            if(AssetDatabase.LoadAssetAtPath<GameObject>(PanelPath)==null)BuildPanel();
            if(AssetDatabase.LoadAssetAtPath<GameObject>(ProviderPath)==null)BuildProvider(mission);
            var manager=All<BunkerPanelManager>(scene).Single();
            var panel=All<BunkerMissionPanel>(scene).SingleOrDefault();
            if(panel==null)
            {
                var parent=All<SurfaceMapView>(scene).Single().transform.parent;
                panel=((GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PanelPath),parent)).GetComponent<BunkerMissionPanel>();
            }
            panel.gameObject.SetActive(false); Set(manager,"missionPanel",panel);
            if(All<MissionProvider>(scene).Length==0)
            {
                var provider=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(ProviderPath),scene);
                var loadout=All<BunkerPlayerLoadoutController>(scene).Single();
                var player=(Transform)loadout.GetType().GetField("controlledPlayerRoot",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(loadout);
                provider.transform.position=player.position+new Vector3(-3,2);
            }
            foreach(var provider in All<MissionProvider>(scene)) Set(provider,"panelManager",manager);
            AssetDatabase.SaveAssets(); EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
        }
        finally { if(opened)EditorSceneManager.CloseScene(scene,true); if(prior.IsValid()&&prior.isLoaded)SceneManager.SetActiveScene(prior); }
    }
    static void BuildProvider(MissionDefinition mission)
    {
        var source=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/prefabs/Bunker/PF_EscapeProtocolStation.prefab");
        var art=source.GetComponentsInChildren<SpriteRenderer>(true).Where(r=>r.sprite!=null).OrderByDescending(r=>r.sprite.bounds.size.sqrMagnitude).First();
        var root=new GameObject("Mission Operator"); root.SetActive(false);
        root.layer=source.GetComponentInChildren<BunkerInteractableCollider>(true).gameObject.layer;
        var sprite=root.AddComponent<SpriteRenderer>(); sprite.sprite=art.sprite; sprite.sharedMaterial=art.sharedMaterial;
        sprite.color=new Color(.4f,.88f,.94f); sprite.sortingLayerID=art.sortingLayerID; sprite.sortingOrder=art.sortingOrder;
        root.transform.localScale=Vector3.one;
        var collider=root.AddComponent<BoxCollider2D>(); collider.isTrigger=true; collider.size=Vector2.Max(sprite.sprite.bounds.size,new Vector2(.6f,.6f));
        var provider=root.AddComponent<MissionProvider>(); Set(provider,"missionIds",new[]{mission.MissionId});
        root.AddComponent<BunkerInteractableCollider>(); root.AddComponent<BunkerHoverOutline>();
        var label=new GameObject("Operator label",typeof(TextMeshPro)); label.transform.SetParent(root.transform,false); label.transform.localPosition=new Vector3(0,1,0);
        var text=label.GetComponent<TextMeshPro>(); text.font=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/_Project/Fonts/Subject42 Intro SDF.asset");
        text.text="OPERATOR"; text.fontSize=2.3f; text.alignment=TextAlignmentOptions.Center; text.color=new Color(1,.7f,.28f);
        text.rectTransform.sizeDelta=new Vector2(3,1); text.GetComponent<MeshRenderer>().sortingOrder=50;
        root.SetActive(true); PrefabUtility.SaveAsPrefabAsset(root,ProviderPath); Object.DestroyImmediate(root);
    }
    static RectTransform Rect(string name,Transform parent,Vector2 position,Vector2 size)
    {
        var rect=new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>(); rect.SetParent(parent,false);
        rect.anchorMin=rect.anchorMax=rect.pivot=new Vector2(.5f,.5f); rect.anchoredPosition=position; rect.sizeDelta=size; return rect;
    }
    static TMP_Text Text(string name,Transform parent,Vector2 position,Vector2 size,string value,int fontSize,TMP_FontAsset font)
    {
        var text=Rect(name,parent,position,size).gameObject.AddComponent<TextMeshProUGUI>(); text.font=font; text.fontSize=fontSize;
        text.color=new Color(.7f,.9f,.94f); text.text=value; text.raycastTarget=false; text.alignment=TextAlignmentOptions.TopLeft; return text;
    }
    static Button Button(string name,Transform parent,Vector2 position,Vector2 size,string value,TMP_FontAsset font)
    {
        var rect=Rect(name,parent,position,size); var image=rect.gameObject.AddComponent<Image>(); image.color=new Color(.07f,.2f,.27f);
        var button=rect.gameObject.AddComponent<Button>(); button.targetGraphic=image;
        Text("Label",rect,Vector2.zero,size-new Vector2(8,0),value,17,font).alignment=TextAlignmentOptions.Center; return button;
    }
    static void BuildPanel()
    {
        var font=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/_Project/Fonts/Subject42 UI SDF.asset");
        var pixel=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/_Project/Fonts/Subject42 Intro SDF.asset");
        var root=Rect("Mission Dialogue",null,Vector2.zero,new Vector2(560,360)); root.gameObject.SetActive(false);
        root.gameObject.AddComponent<Image>().color=new Color(.025f,.055f,.08f,.99f);
        var rim=Rect("Rim",root,Vector2.zero,new Vector2(566,366)).gameObject.AddComponent<SurfaceMapPlate>(); rim.color=new Color(.34f,.85f,.94f); rim.raycastTarget=false; rim.transform.SetAsFirstSibling();
        var fill=Rect("Fill",rim.transform,Vector2.zero,new Vector2(560,360)).gameObject.AddComponent<SurfaceMapPlate>(); fill.color=new Color(.025f,.055f,.08f,.99f); fill.raycastTarget=false;
        var panel=root.gameObject.AddComponent<BunkerMissionPanel>();
        Set(panel,"speaker",Text("Speaker",root,new Vector2(0,134),new Vector2(504,34),"OPERATOR",22,pixel));
        Set(panel,"body",Text("Body",root,new Vector2(0,10),new Vector2(504,210),"",21,font));
        Set(panel,"actionButton",Button("Action",root,new Vector2(-105,-133),new Vector2(280,44),"ACCEPT",font));
        Set(panel,"leaveButton",Button("Leave",root,new Vector2(172,-133),new Vector2(120,44),"LEAVE",font));
        PrefabUtility.SaveAsPrefabAsset(root.gameObject,PanelPath); Object.DestroyImmediate(root.gameObject);
    }
    sealed class ValidationStorage:IMissionStorage
    { public string Load()=>null; public void Save(string json){} public bool HasClaimReceipt(string id)=>false; public bool TryClaimGold(string id,int amount,string state)=>false; }
}
#endif

