#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>Targeted, explicit asset authoring. Does not open/save scenes or touch player progress.</summary>
[InitializeOnLoad]
public static class ExpeditionCoreAuthoring
{
    public const string Output = "Artifacts/GeneratedQA/ExpeditionCores";
    public const string TasksPath = "Assets/_Project/Data/Expeditions";
    private static TMP_FontAsset font;
    private static readonly Color Ink = new(.78f,.84f,.78f);
    private static readonly Color Teal = new(.43f,.88f,.8f);
    static ExpeditionCoreAuthoring() => EditorApplication.update += Poll;
    private static void Poll()
    {
        if (File.Exists(Output + "/recover.request"))
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            if (EditorApplication.isPlaying) { EditorApplication.isPlaying = false; return; }
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            string recovery = Output + "/preferences-restore.json";
            if (File.Exists(recovery)) SessionState.SetString("Subject42.Core.Preferences", File.ReadAllText(recovery));
            var cleanup = CoreTestSupport.CleanupPlayMode(); while (cleanup.MoveNext()) { }
            ExpeditionCoreFlowTests.RestoreExtraPreferences(); File.Delete(Output + "/recover.request");
            if (File.Exists(recovery)) File.Delete(recovery);
            return;
        }
        if (!File.Exists(Output + "/author.request") || EditorApplication.isCompiling ||
            EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode ||
            SessionState.GetBool("Subject42.Verification.AnyRunActive", false)) return;
        File.Delete(Output + "/author.request");
        try { Author(); File.WriteAllText(Output + "/author-result.txt", "SUCCESS"); }
        catch (Exception e) { File.WriteAllText(Output + "/author-result.txt", e.ToString()); Debug.LogException(e); }
    }
    private static void Set(Object target, string name, object value)
    {
        var field = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (field == null) throw new InvalidOperationException(target.GetType().Name + "." + name);
        field.SetValue(target, value); EditorUtility.SetDirty(target);
        if (PrefabUtility.IsPartOfPrefabInstance(target)) PrefabUtility.RecordPrefabInstancePropertyModifications(target);
    }
    private static T Read<T>(Object target, string name) => (T)target.GetType()
        .GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).GetValue(target);
    private static void EditPrefab(string path, Action<GameObject> edit)
    {
        if (PrefabStageUtilityCheck(path)) throw new InvalidOperationException("Save/close the prefab stage before authoring " + path);
        string original = File.ReadAllText(path);
        var root = PrefabUtility.LoadPrefabContents(path);
        try { edit(root); PrefabUtility.SaveAsPrefabAsset(root, path); }
        finally { PrefabUtility.UnloadPrefabContents(root); }
        PreserveNestedLocalIds(path, original);
    }
    // Some existing authored nested-prefab IDs equal their source IDs. Unity remaps
    // them on save; production scenes still target the original IDs in overrides.
    public static void PreserveNestedLocalIds(string path, string original)
    {
        const string blocks = @"(?ms)^--- !u!(?<type>\d+) &(?<id>-?\d+) stripped\r?\n(?<body>.*?)(?=^--- !u!|\z)";
        string Identity(Match block)
        {
            string body=block.Groups["body"].Value;
            return block.Groups["type"].Value + "/" + Regex.Match(body,@"m_CorrespondingSourceObject: [^\r\n]+").Value +
                "/" + Regex.Match(body,@"m_PrefabInstance: [^\r\n]+").Value;
        }
        var existing=Regex.Matches(original,blocks).Cast<Match>().ToDictionary(Identity,m=>m.Groups["id"].Value);
        string current=File.ReadAllText(path);
        var remap=new System.Collections.Generic.Dictionary<string,string>();
        foreach(Match block in Regex.Matches(current,blocks))
            if(existing.TryGetValue(Identity(block),out string id) && id!=block.Groups["id"].Value) remap[block.Groups["id"].Value]=id;
        if(remap.Count==0)return;
        current=Regex.Replace(current,@"(?<=fileID: )-?\d+|(?<=&)-?\d+(?= stripped)",m=>remap.TryGetValue(m.Value,out var id)?id:m.Value);
        File.WriteAllText(path,current,new System.Text.UTF8Encoding(false));
        AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceUpdate);
    }
    private static bool PrefabStageUtilityCheck(string path)
    {
        var stage = UnityEditor.SceneManagement.PrefabStageUtility.GetCurrentPrefabStage();
        return stage != null && stage.assetPath == path;
    }
    [MenuItem("Tools/Subject42/Expeditions/Author Core Rewards and Tasks")]
    public static void Author()
    {
        Directory.CreateDirectory(Output); Directory.CreateDirectory(TasksPath); AssetDatabase.Refresh();
        var mapPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/prefabs/UI/SurfaceMap/PF_SurfaceMap.prefab");
        font = Read<TMP_Text>(mapPrefab.GetComponent<SurfaceMapView>(), "details").font;
        Localize();
        var tasks = new[]
        {
            Task("Survivor", "survivor", ExpeditionTaskCondition.SurviveMinutes, 3, 1),
            Task("Hunter", "hunter", ExpeditionTaskCondition.Kills, 75, 2),
            Task("Traveller", "traveller", ExpeditionTaskCondition.CompletedLocations, 2, 2)
        };
        foreach (string id in new[] { "A1", "A2", "C1", "C2", "D1", "D2" })
        {
            var sector = AssetDatabase.LoadAssetAtPath<SurfaceSectorDefinition>("Assets/_Project/Data/SurfaceMap/Sector_" + id + ".asset");
            Set(sector, "victoryCores", id == "A1" ? 1 : id == "C1" || id == "D1" ? 2 : id == "A2" ? 3 : 4);
            Set(sector, "firstClearCores", id.EndsWith("1", StringComparison.Ordinal) ? 1 : 2);
            Set(sector, "expeditionTasks", tasks);
        }
        EditPrefab("Assets/_Project/prefabs/UI/Bunker/BunkerHUD.prefab", AuthorWallet);
        EditPrefab("Assets/_Project/prefabs/UI/SurfaceMap/PF_SurfaceMap.prefab", AuthorPicker);
        EditPrefab("Assets/_Project/prefabs/UI/HUD/GameplayHUD.prefab", AuthorGameplay);
        EditPrefab("Assets/_Project/prefabs/UI/SurfaceMap/PF_BunkerRunSummary.prefab", AuthorBunkerSummary);
        AssetDatabase.SaveAssets();
    }
    private static ExpeditionTaskDefinition Task(string name, string id, ExpeditionTaskCondition condition, int target, int reward)
    {
        string path = TasksPath + "/Task_" + name + ".asset";
        var task = AssetDatabase.LoadAssetAtPath<ExpeditionTaskDefinition>(path);
        if (task == null) { task = ScriptableObject.CreateInstance<ExpeditionTaskDefinition>(); AssetDatabase.CreateAsset(task, path); }
        Set(task,"id",id); Set(task,"title","task."+id+".title"); Set(task,"description","task."+id+".description");
        Set(task,"progressLabel","task."+id+".progress"); Set(task,"condition",condition); Set(task,"target",target); Set(task,"rewardCores",reward);
        return task;
    }
    private static RectTransform Rect(string name, Transform parent, Vector2 position, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform)); go.layer = 5; go.transform.SetParent(parent, false);
        var rect = (RectTransform)go.transform; rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.one * .5f;
        rect.anchoredPosition = position; rect.sizeDelta = size; return rect;
    }
    private static TMP_Text Label(string name, Transform parent, Vector2 pos, Vector2 size, string text, float points = 20)
    {
        var rect = Rect(name,parent,pos,size); var label = rect.gameObject.AddComponent<TextMeshProUGUI>();
        label.font = font; label.fontSize = points; label.text = text; label.color = Ink; label.raycastTarget = false;
        label.alignment = TextAlignmentOptions.MidlineLeft; label.textWrappingMode = TextWrappingModes.Normal;
        label.overflowMode = TextOverflowModes.Ellipsis; return label;
    }
    private static Graphic Frame(RectTransform rect, bool blocks = false)
    {
        var border = rect.gameObject.AddComponent<SurfaceMapPlate>(); border.Cut = 4;
        border.color = new Color(.3f,.37f,.35f); border.raycastTarget = blocks;
        var fill = Rect("Fill", rect, Vector2.zero, Vector2.zero);
        fill.anchorMin = Vector2.zero; fill.anchorMax = Vector2.one; fill.offsetMin = Vector2.one * 2; fill.offsetMax = -Vector2.one * 2;
        var inside = fill.gameObject.AddComponent<SurfaceMapPlate>(); inside.Cut = 3; inside.color = new Color(.065f,.09f,.09f,.98f); inside.raycastTarget = false;
        return border;
    }
    private static Button Button(string name, Transform parent, Vector2 pos, Vector2 size, string text)
    {
        var rect = Rect(name,parent,pos,size); var frame = Frame(rect,true); var button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = frame; var colors = button.colors;
        colors.highlightedColor = new Color(1.35f,1.35f,1.35f); colors.pressedColor = Teal; button.colors = colors;
        var label = Label("Label",rect,Vector2.zero,size - new Vector2(20,4),text);
        label.alignment = TextAlignmentOptions.Center; return button;
    }
    private static void Icon(Transform parent, Vector2 position, float size)
    { var rect = Rect("CoreIcon",parent,position,Vector2.one*size); rect.gameObject.AddComponent<CoreCurrencyIcon>().raycastTarget = false; }
    private static void RemoveExisting(Transform root, string name)
    { var old = root.Find(name); if (old != null) Object.DestroyImmediate(old.gameObject); }
    private static void AuthorWallet(GameObject root)
    {
        var view = root.GetComponent<BunkerHudView>(); var parent = view.CurrencyContainer;
        RemoveExisting(parent,"Cores");
        var rect = Rect("Cores",parent,Vector2.zero,new Vector2(208,32));
        var layout = rect.gameObject.AddComponent<LayoutElement>(); layout.preferredHeight=32;
        var row = rect.gameObject.AddComponent<HorizontalLayoutGroup>(); row.spacing=8; row.childAlignment=TextAnchor.MiddleRight;
        row.childControlWidth=row.childControlHeight=true;row.childForceExpandWidth=row.childForceExpandHeight=false;
        rect.gameObject.AddComponent<ContentSizeFitter>().horizontalFit=ContentSizeFitter.FitMode.PreferredSize;
        Icon(rect,new Vector2(-88,0),28);
        var iconLayout=rect.GetComponentInChildren<CoreCurrencyIcon>().gameObject.AddComponent<LayoutElement>();
        iconLayout.minWidth=iconLayout.preferredWidth=28;iconLayout.minHeight=iconLayout.preferredHeight=28;
        var text = Label("Value",rect,new Vector2(18,0),new Vector2(166,32),"0",22);
        text.textWrappingMode=TextWrappingModes.NoWrap; text.color=Teal; text.alignment=TextAlignmentOptions.MidlineLeft;
        var number = rect.gameObject.AddComponent<HudIconNumber>(); Set(number,"valueText",text); Set(view,"cores",number);
    }
    private static void AuthorPicker(GameObject root)
    {
        RemoveExisting(root.transform,"ExpeditionTaskPicker");
        var overlay = Rect("ExpeditionTaskPicker",root.transform,Vector2.zero,Vector2.zero);
        overlay.anchorMin=Vector2.zero;overlay.anchorMax=Vector2.one;overlay.offsetMin=overlay.offsetMax=Vector2.zero;
        var shade=overlay.gameObject.AddComponent<Image>();shade.color=new Color(.015f,.025f,.025f,.94f);shade.raycastTarget=true;
        var picker=overlay.gameObject.AddComponent<ExpeditionTaskPicker>();
        var panel=Rect("TaskPanel",overlay,Vector2.zero,new Vector2(640,580));Frame(panel);
        var title=Label("Title",panel,new Vector2(0,246),new Vector2(584,36),"",26);
        var reward=Label("SectorRewards",panel,new Vector2(18,208),new Vector2(548,30),"",18);reward.color=Teal;
        Icon(panel,new Vector2(-282,208),24);
        var hint=Label("Hint",panel,new Vector2(0,170),new Vector2(584,28),"",16);
        var list=Rect("Tasks",panel,new Vector2(0,-1),new Vector2(584,280));
        var vertical=list.gameObject.AddComponent<VerticalLayoutGroup>();vertical.spacing=10;
        vertical.childControlHeight=vertical.childControlWidth=true;vertical.childForceExpandHeight=false;vertical.childForceExpandWidth=true;
        var templateRect=Rect("TaskTemplate",list,Vector2.zero,new Vector2(584,86));var frame=Frame(templateRect,true);
        var button=templateRect.gameObject.AddComponent<Button>();button.targetGraphic=frame;
        var layout=templateRect.gameObject.AddComponent<LayoutElement>();layout.preferredHeight=86;
        var taskTitle=Label("Title",templateRect,new Vector2(-25,17),new Vector2(430,28),"",22);
        var description=Label("Description",templateRect,new Vector2(-25,-17),new Vector2(430,28),"",18);
        var amount=Label("Reward",templateRect,new Vector2(251,0),new Vector2(58,32),"",22);amount.color=Teal;
        Icon(templateRect,new Vector2(209,0),26);
        var selected=Label("Selected",templateRect,new Vector2(-266,17),new Vector2(30,26),"V",20);selected.color=Teal;
        var choice=templateRect.gameObject.AddComponent<ExpeditionTaskChoice>();
        Set(choice,"button",button);Set(choice,"title",taskTitle);Set(choice,"description",description);Set(choice,"reward",amount);
        Set(choice,"frame",frame);Set(choice,"selectedMark",selected.gameObject);templateRect.gameObject.SetActive(false);
        var none=Button("WithoutTask",panel,new Vector2(0,-176),new Vector2(584,40),"");
        var launch=Button("Launch",panel,new Vector2(142,-244),new Vector2(300,48),"");
        var back=Button("Back",panel,new Vector2(-160,-244),new Vector2(264,48),"");
        Set(picker,"title",title);Set(picker,"rewards",reward);Set(picker,"hint",hint);Set(picker,"list",list);Set(picker,"template",choice);
        Set(picker,"noneButton",none);Set(picker,"launchButton",launch);Set(picker,"backButton",back);
        Set(root.GetComponent<SurfaceMapView>(),"taskPicker",picker);overlay.gameObject.SetActive(false);
    }
    private static ExpeditionCoreSummaryView Summary(Transform parent, Vector2 pos, Vector2 size, float points)
    {
        RemoveExisting(parent,"CoreRewards");
        var rect=Rect("CoreRewards",parent,pos,size); var view=rect.gameObject.AddComponent<ExpeditionCoreSummaryView>();
        Icon(rect,new Vector2(-size.x*.5f+20,0),28);
        var text=Label("Breakdown",rect,new Vector2(20,0),size-new Vector2(64,4),"",points);text.color=Teal;
        text.lineSpacing=2;Set(view,"breakdown",text);return view;
    }
    private static void AuthorGameplay(GameObject root)
    {
        var hud=root.GetComponentInChildren<HUDManager>(true);
        var parent=Read<CanvasGroup>(hud,"informationGroup").transform;
        RemoveExisting(parent,"ExpeditionTask");
        var rect=Rect("ExpeditionTask",parent,new Vector2(24,-280),new Vector2(300,68));
        rect.anchorMin=rect.anchorMax=rect.pivot=new Vector2(0,1);Frame(rect);
        var content=rect.gameObject.AddComponent<CanvasGroup>();content.alpha=0;content.blocksRaycasts=content.interactable=false;
        Icon(rect,new Vector2(-128,0),24);
        var title=Label("Title",rect,new Vector2(16,14),new Vector2(240,24),"",19);
        var progress=Label("Progress",rect,new Vector2(16,-14),new Vector2(240,24),"",18);progress.color=Teal;
        var view=rect.gameObject.AddComponent<ExpeditionTaskHud>();
        Set(view,"content",content);Set(view,"title",title);Set(view,"progress",progress);Set(view,"hud",hud);
        Set(hud,"expeditionTaskHud",view);
        var death=root.GetComponentInChildren<DeathResultPresentation>(true);
        var window=Read<RectTransform>(death,"window");
        var comment=Read<TextMeshProUGUI>(death,"comment").rectTransform;
        foreach(RectTransform child in window)
        {
            if(child==comment || child.name=="CoreRewards")continue;
            var source=PrefabUtility.GetCorrespondingObjectFromSource(child);
            if(source==null)continue;
            child.anchoredPosition=source.anchoredPosition+new Vector2(0,child.name=="Footer"?-80:80);
            PrefabUtility.RecordPrefabInstancePropertyModifications(child);
        }
        window.sizeDelta=new Vector2(1000,1000);
        comment.anchoredPosition=new Vector2(0,-320);comment.sizeDelta=new Vector2(856,40);
        PrefabUtility.RecordPrefabInstancePropertyModifications(window);
        PrefabUtility.RecordPrefabInstancePropertyModifications(comment);
        var summary=Summary(window,new Vector2(0,-236),new Vector2(880,104),18);
        Set(death,"coreRewards",summary);
    }
    private static void AuthorBunkerSummary(GameObject root)
    {
        var presenter=root.GetComponent<BunkerRunSummaryPresenter>();
        var parent=Read<RectTransform>(presenter,"notification");
        Set(presenter,"coreRewards",Summary(parent,new Vector2(0,-58),new Vector2(528,112),18));
    }
    private static void Localize()
    {
        var table=AssetDatabase.LoadAssetAtPath<LocalizationTable>("Assets/_Project/Data/Localization/LocalizationTable.asset");
        var data=new SerializedObject(table);var entries=data.FindProperty("entries");
        void Add(string key,string ru,string en)
        {
            SerializedProperty row=null;
            for(int i=0;i<entries.arraySize;i++)if(entries.GetArrayElementAtIndex(i).FindPropertyRelative("key").stringValue==key){row=entries.GetArrayElementAtIndex(i);break;}
            if(row==null){int index=entries.arraySize++;row=entries.GetArrayElementAtIndex(index);}
            row.FindPropertyRelative("key").stringValue=key;row.FindPropertyRelative("ru").stringValue=ru;row.FindPropertyRelative("en").stringValue=en;
        }
        Add("expedition.rewards","Победа: +{0} • Впервые: +{1}","Victory: +{0} • First clear: +{1}");
        Add("expedition.choose_one","Ядра сохранятся при смерти.","Keep cores after death.");
        Add("expedition.none","Без задания","No task");Add("expedition.launch","В ПУТЬ","DEPLOY");Add("expedition.back","НАЗАД","BACK");
        Add("expedition.completed","Задание выполнено!","Task complete!");Add("expedition.task_paid","Ядра: +{0} • сохранено","Cores: +{0} • saved");
        Add("expedition.summary","Задание «{0}»: +{1}\nПрохождение сектора: +{2}\nПервое прохождение: +{3}\nВсего ядер: +{4}","Task “{0}”: +{1}\nSector clear: +{2}\nFirst clear: +{3}\nTotal cores: +{4}");
        Add("task.survivor.title","Выживший","Survivor");Add("task.survivor.description","Продержаться {0} минуты","Survive {0} minutes");Add("task.survivor.progress","Минуты: {0}/{1}","Minutes: {0}/{1}");
        Add("task.hunter.title","Охотник","Hunter");Add("task.hunter.description","Убить {0} врагов","Kill {0} enemies");Add("task.hunter.progress","Враги: {0}/{1}","Enemies: {0}/{1}");
        Add("task.traveller.title","Путник","Traveller");Add("task.traveller.description","Пройти {0} локации","Clear {0} locations");Add("task.traveller.progress","Локации: {0}/{1}","Locations: {0}/{1}");
        data.ApplyModifiedPropertiesWithoutUndo();EditorUtility.SetDirty(table);
    }
}
#endif
