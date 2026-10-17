#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class EscapeProtocolAuthoring
{
    public const string Output = "Artifacts/GeneratedQA/EscapeProtocol";
    public const string DefinitionPath = "Assets/_Project/Data/Meta/EscapeProtocol.asset";
    public const string FinalPath = "Assets/_Project/Data/SurfaceMap/Sector_F1.asset";
    public const string TerminalPath = "Assets/_Project/prefabs/UI/PF_RunDepthSelection.prefab";
    private static TMP_FontAsset font;
    static EscapeProtocolAuthoring() => EditorApplication.update += Poll;
    static void Poll()
    {
        if (File.Exists(Output+"/recover.request"))
        {
            if(EditorApplication.isCompiling || EditorApplication.isUpdating)return;
            if(EditorApplication.isPlaying){EditorApplication.isPlaying=false;return;}
            if(EditorApplication.isPlayingOrWillChangePlaymode)return;
            var cleanup=CoreTestSupport.CleanupPlayMode();while(cleanup.MoveNext()){}
            EscapeProtocolFlowTests.RestoreExtraPreferences();File.Delete(Output+"/recover.request");return;
        }
        if(!File.Exists(Output+"/author.request") || EditorApplication.isCompiling || EditorApplication.isUpdating ||
            EditorApplication.isPlayingOrWillChangePlaymode || SessionState.GetBool("Subject42.Verification.AnyRunActive",false))return;
        File.Delete(Output+"/author.request");
        try { Author();File.WriteAllText(Output+"/author-result.txt","SUCCESS"); }
        catch(Exception e){File.WriteAllText(Output+"/author-result.txt",e.ToString());Debug.LogException(e);}
    }
    static void Set(Object target,string field,object value)
    {
        target.GetType().GetField(field,BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public).SetValue(target,value);
        EditorUtility.SetDirty(target);
        if(PrefabUtility.IsPartOfPrefabInstance(target))PrefabUtility.RecordPrefabInstancePropertyModifications(target);
    }
    static T Read<T>(Object target,string field)=>(T)target.GetType().GetField(field,BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public).GetValue(target);
    static void Edit(string path,Action<GameObject> action)
    {
        var stage=UnityEditor.SceneManagement.PrefabStageUtility.GetCurrentPrefabStage();
        if(stage!=null&&stage.assetPath==path)throw new InvalidOperationException("Save and close the prefab stage before authoring "+path);
        string original=File.ReadAllText(path);var root=PrefabUtility.LoadPrefabContents(path);
        try {action(root);PrefabUtility.SaveAsPrefabAsset(root,path);}
        finally {PrefabUtility.UnloadPrefabContents(root);}
        ExpeditionCoreAuthoring.PreserveNestedLocalIds(path,original);
    }
    [MenuItem("Tools/Subject42/Escape Protocol/Author Production Progression")]
    public static void Author()
    {
        Directory.CreateDirectory(Output);Directory.CreateDirectory("Assets/_Project/Data/Meta");AssetDatabase.Refresh();
        var final=AssetDatabase.LoadAssetAtPath<SurfaceSectorDefinition>(FinalPath);
        if(final==null)
        {
            final=Object.Instantiate(AssetDatabase.LoadAssetAtPath<SurfaceSectorDefinition>("Assets/_Project/Data/SurfaceMap/Sector_D2.asset"));
            final.name="Sector_F1";AssetDatabase.CreateAsset(final,FinalPath);
            Set(final,"id","F1");Set(final,"displayName","escape.final.title");Set(final,"description","escape.final.briefing");
            Set(final,"mapPosition",new Vector2(-48,260));Set(final,"storySector",true);Set(final,"requiresScan",false);
            Set(final,"escapeFinal",true);Set(final,"neighbours",Array.Empty<string>());Set(final,"content",Array.Empty<SurfaceSectorContent>());
            Set(final,"requiredFinalEvent",AssetDatabase.LoadAssetAtPath<OrbitalRelayEvent>("Assets/_Project/prefabs/Environment/WorldEvents/OrbitalRelayEvent.prefab"));
            Set(final,"requiredFinalEventTag","hold_zone");Set(final,"victoryCores",4);Set(final,"firstClearCores",2);
            var p=Read<RunConfigParameters>(final,"parameters");p.initialThreat=25;p.enemyHealth=1;p.spawnPressure=1.15f;p.threatGrowth=1.2f;
            // Its full arena cannot fit every ordinary anomaly site in this route.
            p.eventWeights=p.eventWeights.Concat(new[]{new RunPrefabWeight{
                prefab=AssetDatabase.LoadAssetAtPath<RocketStormEvent>("Assets/_Project/prefabs/Environment/WorldEvents/RocketStormEvent.prefab"),multiplier=0}}).ToArray();
            // No gold change: copied sector rewards/multipliers retain their existing rules.
            EditorUtility.SetDirty(final);
        }
        var definition=AssetDatabase.LoadAssetAtPath<EscapeProtocolDefinition>(DefinitionPath);
        if(definition==null)
        {
            definition=ScriptableObject.CreateInstance<EscapeProtocolDefinition>();AssetDatabase.CreateAsset(definition,DefinitionPath);
            Set(definition,"stages",new[]
            {
                new EscapeProtocolDefinition.Stage{id="restore_access",title="escape.stage.access",objective="escape.goal.access",cost=5},
                new EscapeProtocolDefinition.Stage{id="prepare_protocol",title="escape.stage.prepare",objective="escape.goal.prepare",cost=10},
                new EscapeProtocolDefinition.Stage{id="final_clearance",title="escape.stage.final",objective="escape.goal.final",cost=15}
            });
            Set(definition,"finalSector",final);
        }
        var map=AssetDatabase.LoadAssetAtPath<SurfaceMapDefinition>("Assets/_Project/Data/SurfaceMap/SurfaceMap_MVP.asset");
        if(!map.Sectors.Contains(final))Set(map,"sectors",map.Sectors.Concat(new[]{final}).ToArray());
        if(!map.TryValidate(out string error))throw new InvalidOperationException(error);
        Edit("Assets/_Project/prefabs/Bootstrap/MetaProgressionManager.prefab",root=>Set(root.GetComponent<MetaProgressionManager>(),"escapeProtocol",definition));
        font=Read<TMP_Text>(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/prefabs/UI/SurfaceMap/PF_SurfaceMap.prefab").GetComponent<SurfaceMapView>(),"details").font;
        Edit(TerminalPath,AuthorTerminal);
        Localize();AssetDatabase.SaveAssets();
    }
    static RectTransform Rect(string name,Transform parent,Vector2 pos,Vector2 size)
    {
        var go=new GameObject(name,typeof(RectTransform));go.layer=5;go.transform.SetParent(parent,false);
        var rect=(RectTransform)go.transform;rect.anchorMin=rect.anchorMax=rect.pivot=Vector2.one*.5f;rect.anchoredPosition=pos;rect.sizeDelta=size;return rect;
    }
    static TMP_Text Label(string name,Transform parent,Vector2 pos,Vector2 size,float points,Color? tint=null)
    {
        var rect=Rect(name,parent,pos,size);var text=rect.gameObject.AddComponent<TextMeshProUGUI>();text.font=font;text.fontSize=points;
        text.color=tint??new Color(.78f,.84f,.78f);text.raycastTarget=false;text.alignment=TextAlignmentOptions.MidlineLeft;
        text.textWrappingMode=TextWrappingModes.Normal;text.overflowMode=TextOverflowModes.Ellipsis;return text;
    }
    static Graphic Frame(RectTransform rect,bool raycast=false)
    {
        var border=rect.gameObject.AddComponent<SurfaceMapPlate>();border.Cut=4;border.color=new Color(.31f,.43f,.4f);border.raycastTarget=raycast;
        var fill=Rect("Fill",rect,Vector2.zero,Vector2.zero);fill.anchorMin=Vector2.zero;fill.anchorMax=Vector2.one;fill.offsetMin=Vector2.one*2;fill.offsetMax=-Vector2.one*2;
        var face=fill.gameObject.AddComponent<SurfaceMapPlate>();face.Cut=3;face.color=new Color(.055f,.08f,.08f,.99f);face.raycastTarget=false;return border;
    }
    static Button Button(string name,Transform parent,Vector2 pos,Vector2 size,out TMP_Text label)
    {
        var rect=Rect(name,parent,pos,size);var border=Frame(rect,true);var button=rect.gameObject.AddComponent<Button>();button.targetGraphic=border;
        label=Label("Label",rect,Vector2.zero,size-new Vector2(24,4),20);label.alignment=TextAlignmentOptions.Center;
        label.textWrappingMode=TextWrappingModes.NoWrap;label.enableAutoSizing=true;label.fontSizeMin=16;label.fontSizeMax=20;
        return button;
    }
    static void AuthorTerminal(GameObject root)
    {
        var view=root.GetComponentInChildren<EscapeProtocolView>(true);var parent=view.transform;
        var old=parent.Find("CoreProject");if(old!=null)Object.DestroyImmediate(old.gameObject);
        var legacy=parent.Find("LegacyDepthWindow");
        if(legacy==null)
        {
            var children=parent.Cast<Transform>().ToArray();var wrap=Rect("LegacyDepthWindow",parent,Vector2.zero,Vector2.zero);
            wrap.anchorMin=Vector2.zero;wrap.anchorMax=Vector2.one;wrap.offsetMin=wrap.offsetMax=Vector2.zero;
            foreach(var child in children)child.SetParent(wrap,false);legacy=wrap;
        }
        var overlay=Rect("CoreProject",parent,Vector2.zero,Vector2.zero);overlay.anchorMin=Vector2.zero;overlay.anchorMax=Vector2.one;overlay.offsetMin=overlay.offsetMax=Vector2.zero;
        var backdrop=overlay.gameObject.AddComponent<Image>();backdrop.color=new Color(.015f,.025f,.025f,.91f);backdrop.raycastTarget=true;
        var panel=Rect("ProtocolTerminal",overlay,Vector2.zero,new Vector2(780,520));Frame(panel);
        var title=Label("Title",panel,new Vector2(0,214),new Vector2(704,40),26,new Color(.45f,.86f,.78f));
        var progress=Label("ProjectProgress",panel,new Vector2(0,172),new Vector2(704,30),18);
        var stages=Label("Stages",panel,new Vector2(0,68),new Vector2(704,150),18);
        var next=Label("CurrentStage",panel,new Vector2(0,-62),new Vector2(704,64),18);
        var icon=Rect("CoreIcon",panel,new Vector2(-335,-135),new Vector2(26,26));icon.gameObject.AddComponent<CoreCurrencyIcon>().raycastTarget=false;
        var balance=Label("Balance",panel,new Vector2(18,-135),new Vector2(668,32),20,new Color(.45f,.86f,.78f));
        var pay=Button("Invest",panel,new Vector2(96,-210),new Vector2(512,48),out var payLabel);
        var close=Button("CloseProject",panel,new Vector2(-272,-210),new Vector2(160,48),out _);
        Set(view,"legacyWindow",legacy.gameObject);Set(view,"projectWindow",overlay.gameObject);Set(view,"projectTitle",title);Set(view,"projectStages",stages);
        Set(view,"projectProgress",progress);Set(view,"projectNext",next);Set(view,"projectBalance",balance);Set(view,"investButton",pay);Set(view,"investLabel",payLabel);Set(view,"projectClose",close);
        legacy.gameObject.SetActive(false);overlay.gameObject.SetActive(true);
    }
    static void Localize()
    {
        var table=AssetDatabase.LoadAssetAtPath<LocalizationTable>("Assets/_Project/Data/Localization/LocalizationTable.asset");var data=new SerializedObject(table);var entries=data.FindProperty("entries");
        void Add(string key,string ru,string en)
        {
            SerializedProperty row=null;for(int i=0;i<entries.arraySize;i++)if(entries.GetArrayElementAtIndex(i).FindPropertyRelative("key").stringValue==key){row=entries.GetArrayElementAtIndex(i);break;}
            if(row==null){int index=entries.arraySize++;row=entries.GetArrayElementAtIndex(index);}
            row.FindPropertyRelative("key").stringValue=key;row.FindPropertyRelative("ru").stringValue=ru;row.FindPropertyRelative("en").stringValue=en;
        }
        Add("escape.stage.access","Восстановление доступа","Restore access");Add("escape.stage.prepare","Подготовка протокола","Prepare protocol");Add("escape.stage.final","Финальный допуск","Final clearance");
        Add("escape.goal.access","Восстановить доступ — {0}/{1} ядер","Restore access — {0}/{1} cores");
        Add("escape.goal.prepare","Подготовить протокол — {0}/{1} ядер","Prepare protocol — {0}/{1} cores");
        Add("escape.goal.final","Открыть финальный сектор — {0}/{1} ядер","Open final sector — {0}/{1} cores");
        Add("escape.objective.final","Завершить Escape Protocol","Complete Escape Protocol");Add("escape.objective.completed","Escape Protocol завершён","Escape Protocol complete");
        Add("escape.project_progress","Общий прогресс: {0}/{1} ядер","Project progress: {0}/{1} cores");Add("escape.stage_title","Этап {0} — {1}","Stage {0} — {1}");
        Add("escape.next","{0}\nВложено: {1}/{2} ядер","{0}\nInvested: {1}/{2} cores");Add("escape.balance","Ядра: {0}","Cores: {0}");
        Add("escape.close","ЗАКРЫТЬ","CLOSE");Add("escape.invest","ВЛОЖИТЬ ЯДРА: {0}","INVEST CORES: {0}");Add("escape.need_cores","НУЖНЫ ЯДРА","CORES REQUIRED");
        Add("escape.funded","ВЛОЖЕНИЯ ЗАВЕРШЕНЫ","FUNDING COMPLETE");Add("escape.done","ЗАВЕРШЕНО","COMPLETE");
        Add("escape.final_open","Финальный сектор открыт на карте.\nПройдите завершающее испытание.","Final sector is open on the map.\nComplete the final trial.");
        Add("escape.final.node","ФИНАЛ","FINAL");Add("escape.final.title","Финальное испытание F1","Final trial F1");
        Add("escape.final.briefing","Активируйте орбитальное реле.\nПройдите 3 локации и победите Стража.","Activate an Orbital Relay.\nClear 3 locations and defeat the Guardian.");
        Add("escape.final.unavailable","Финальное испытание недоступно: реле отключено.","Final trial unavailable: Relay content is disabled.");
        Add("escape.final.relay_objective","АКТИВИРУЙТЕ РЕЛЕ","ACTIVATE THE RELAY");
        Add("escape.completion_message","Финальное испытание пройдено","Final trial cleared");
        data.ApplyModifiedPropertiesWithoutUndo();EditorUtility.SetDirty(table);
    }
}
#endif
