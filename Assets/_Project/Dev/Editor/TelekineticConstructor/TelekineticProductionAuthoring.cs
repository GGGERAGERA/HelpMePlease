#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Subject42.Combat.OrbitalStation;
using Subject42.Combat.Telekinetic;
using Subject42.DebugLabs.Telekinetic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

[InitializeOnLoad]
public static class TelekineticProductionAuthoring
{
    public const string Output="Artifacts/GeneratedQA/TelekineticProduction";
    public const string PrefabPath="Assets/_Project/prefabs/Orbital/TelekineticRig.prefab";
    static TelekineticProductionAuthoring() => EditorApplication.update+=Poll;
    private static void Poll()
    {
        string request=Output+"/editor.request";
        if (EditorApplication.isCompiling || EditorApplication.isUpdating || !File.Exists(request)) return;
        string command=File.ReadAllText(request).Trim(); File.Delete(request);
        try
        {
            if (command=="author") Author();
            else if (command=="test")
            {
                const string tests="Artifacts/GeneratedQA/Tutorial/run-tests.request";
                Directory.CreateDirectory(Path.GetDirectoryName(tests));
                File.WriteAllText(tests+".tmp","TelekineticProductionTests"); File.Move(tests+".tmp",tests);
            }
            else if (command=="stop") EditorApplication.ExitPlaymode();
            else if (command=="quit" && !EditorApplication.isPlayingOrWillChangePlaymode) EditorApplication.Exit(0);
            else if (command=="capture") ScreenCapture.CaptureScreenshot(Output+"/manual.png");
            else if (command=="release-check") PlayerScriptCompilationCheck.Run();
            else if (command=="hud-hint") AuthorHudHint();
            File.WriteAllText(Output+"/command-result.txt",command+": OK "+DateTime.UtcNow.ToString("O"));
        }
        catch (Exception error) { Debug.LogException(error); File.WriteAllText(Output+"/command-result.txt",error.ToString()); }
    }
    [MenuItem("Tools/Subject42/Dev/Telekinetic Constructor/Author HUD hint")]
    public static void AuthorHudHint()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play before authoring");
        const string path="Assets/_Project/prefabs/UI/HUD/GameplayHUD.prefab";
        var stage=PrefabStageUtility.GetCurrentPrefabStage();
        if (stage!=null && stage.assetPath==path && stage.scene.isDirty)
            throw new InvalidOperationException("Preserve unsaved GameplayHUD Prefab Mode edits before authoring");
        string original=File.ReadAllText(path);
        var root=PrefabUtility.LoadPrefabContents(path);
        try
        {
            var hud=root.GetComponentInChildren<HUDManager>(true); var data=new SerializedObject(hud);
            var gold=(HudIconNumber)data.FindProperty("goldCounter").objectReferenceValue;
            var parent=gold.transform.parent;
            var existing=parent.Find("ConstructorHint");
            if (existing!=null) return;
            var template=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/prefabs/UI/HUD/HudIconNumber.prefab");
            var hint=(GameObject)PrefabUtility.InstantiatePrefab(template,parent);
            PrefabUtility.UnpackPrefabInstance(hint,PrefabUnpackMode.Completely,InteractionMode.AutomatedAction);
            hint.name="ConstructorHint";
            UnityEngine.Object.DestroyImmediate(hint.GetComponent<HudIconNumber>());
            var reference=(RectTransform)gold.transform; var rect=(RectTransform)hint.transform;
            rect.anchorMin=reference.anchorMin; rect.anchorMax=reference.anchorMax; rect.pivot=reference.pivot;
            rect.sizeDelta=reference.sizeDelta; rect.anchoredPosition=reference.anchoredPosition+Vector2.down*40;
            rect.localScale=Vector3.one;
            var label=hint.GetComponentInChildren<TMP_Text>(); label.text="Tab";
            var labelRect=label.rectTransform; labelRect.anchoredPosition=new Vector2(-16,0);
            label.alignment=TextAlignmentOptions.Center; label.raycastTarget=false;
            var icon=hint.transform.Find("Icon").GetComponent<Image>();
            var rig=AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath).GetComponent<TelekineticRig>();
            icon.sprite=rig.ItemIcons[(int)TelekineticPart.Frame]; icon.preserveAspect=true; icon.raycastTarget=false;
            icon.rectTransform.anchoredPosition=new Vector2(32,0);
            var surface=hint.transform.Find("Surface").GetComponent<Image>(); surface.raycastTarget=true;
            var button=hint.AddComponent<Button>(); button.targetGraphic=surface;
            button.navigation=new Navigation { mode=Navigation.Mode.None };
            var visibility=hint.AddComponent<CanvasGroup>(); visibility.alpha=1;
            var view=hint.AddComponent<ConstructorHudHint>(); var viewData=new SerializedObject(view);
            viewData.FindProperty("hud").objectReferenceValue=hud;
            viewData.FindProperty("visibility").objectReferenceValue=visibility;
            viewData.FindProperty("button").objectReferenceValue=button;
            viewData.ApplyModifiedPropertiesWithoutUndo();
            UnityEventTools.AddPersistentListener(button.onClick,view.Open);
            data.Update(); data.FindProperty("constructorHint").objectReferenceValue=view; data.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(root,path);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
        PreserveHudReferences(original,path);
    }
    private static void PreserveHudReferences(string original,string path)
    {
        // Load/SavePrefabContents may renumber stripped nested objects that MVP references.
        // Retain their original local IDs; no scene overrides need rewriting for a new child.
        const string block=@"(?ms)^--- !u!(\d+) &(\d+)(?: stripped)?\r?\n.*?(?=^--- !u!|\z)";
        string Key(Match m) => m.Groups[1].Value+"|"+
            Regex.Match(m.Value,@"(?m)^  m_CorrespondingSourceObject: (.+)").Groups[1].Value.Trim()+"|"+
            Regex.Match(m.Value,@"(?m)^  m_PrefabInstance: (.+)").Groups[1].Value.Trim();
        bool Stripped(Match m) => Regex.IsMatch(m.Value,@"^--- !u!\d+ &\d+ stripped\r?\n");
        var oldBlocks=Regex.Matches(original,block).Cast<Match>().ToDictionary(m=>m.Groups[2].Value,m=>m.Value);
        var oldIds=Regex.Matches(original,block).Cast<Match>().Where(Stripped).ToDictionary(Key,m=>m.Groups[2].Value);
        string current=File.ReadAllText(path);
        foreach (var m in Regex.Matches(current,block).Cast<Match>().Where(Stripped))
            if (oldIds.TryGetValue(Key(m),out string id) && id!=m.Groups[2].Value)
                current=Regex.Replace(current,@"(?<=\{fileID: )"+m.Groups[2].Value+@"(?=\})|(?<=&)"+m.Groups[2].Value+@"(?= stripped)",id);
        string Clean(string s) => Regex.Replace(s,@"(?m)[ \t]+(?=\r?$)","").Replace("\r\n","\n");
        current=Regex.Replace(current,@"(?m)[ \t]+(?=\r?$)","");
        current=Regex.Replace(current,block,m=>oldBlocks.TryGetValue(m.Groups[2].Value,out string old) && Clean(old)==Clean(m.Value)?old:m.Value);
        File.WriteAllText(path,current,new System.Text.UTF8Encoding(false));
        AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceUpdate);
    }
    [MenuItem("Tools/Subject42/Dev/Telekinetic Constructor/Author production rig")]
    public static void Author()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play before authoring");
        var labScene=EditorSceneManager.OpenPreviewScene(TelekineticConstructorLab.ScenePath);
        GameObject go=null;
        try
        {
            var lab=labScene.GetRootGameObjects().Select(g=>g.GetComponent<TelekineticConstructorLab>()).First(l=>l!=null);
            var config=AssetDatabase.LoadAssetAtPath<OrbitalPresentationConfig>("Assets/_Project/Data/Orbital/OrbitalPresentationConfig.asset");
            go=new GameObject("Telekinetic Rig / production");
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go,labScene);
            var rig=go.AddComponent<TelekineticRig>();
            rig.SpriteMaterial=lab.SpriteMaterial; rig.ModuleSprite=lab.ModuleSprite;
            rig.ItemIcons=new Sprite[lab.ItemIcons.Length]; rig.ShotClip=lab.ShotClip; rig.SawClip=lab.SawClip;
            const string icons="Assets/_Project/art/UI/TelekineticIcons";
            Directory.CreateDirectory(icons); AssetDatabase.Refresh();
            for (int i=0;i<lab.ItemIcons.Length;i++)
            {
                var icon=lab.ItemIcons[i]; if (icon==null) continue;
                string source=AssetDatabase.GetAssetPath(icon);
                if (source.StartsWith("Assets/_Project/Dev/",StringComparison.Ordinal))
                {
                    string target=icons+"/"+Path.GetFileName(source);
                    if (!File.Exists(target)) AssetDatabase.CopyAsset(source,target);
                    rig.ItemIcons[i]=AssetDatabase.LoadAssetAtPath<Sprite>(target);
                }
                else rig.ItemIcons[i]=icon;
            }
            rig.Modules=JsonUtility.FromJson<TelekineticModuleTuning>(JsonUtility.ToJson(lab.Modules));
            rig.Pistol=lab.Pistol; rig.Shotgun=lab.Shotgun; rig.Saw=lab.Saw; rig.Sniper=config.GetWeaponSettings(OrbitalModuleKind.Sniper);
            GameObject Visual(OrbitalModuleKind kind)
            {
                var art=(GameObject)PrefabUtility.InstantiatePrefab(config.GetPrefab(kind));
                art.transform.SetParent(go.transform,false); art.transform.localScale=Vector3.one*config.GetScale(kind);
                art.SetActive(false); return art;
            }
            rig.PistolVisual=Visual(OrbitalModuleKind.Pistol); rig.ShotgunVisual=Visual(OrbitalModuleKind.Shotgun);
            rig.SawVisual=Visual(OrbitalModuleKind.Saw); rig.SniperVisual=Visual(OrbitalModuleKind.Sniper);
            rig.StructureView=go.AddComponent<TelekineticStructureView>();
            // Preserve the V3 authored chassis settings exactly.
            EditorUtility.CopySerialized(lab.StructureView,rig.StructureView);
            var prefab=PrefabUtility.SaveAsPrefabAsset(go,PrefabPath);
            config.TelekineticRigPrefab=prefab.GetComponent<TelekineticRig>();
            EditorUtility.SetDirty(config); AssetDatabase.SaveAssets();
        }
        finally
        {
            if (go!=null) UnityEngine.Object.DestroyImmediate(go);
            EditorSceneManager.ClosePreviewScene(labScene);
        }
    }
}
#endif
