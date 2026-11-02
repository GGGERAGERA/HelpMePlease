#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using NUnit.Framework;
using Subject42.Combat.OrbitalStation;
using Subject42.Combat.Telekinetic;
using Subject42.DebugLabs.Telekinetic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object=UnityEngine.Object;

// Bounded visual fixture. It runs in the existing user Editor and captures its real Game View.
// No scene/prefab saving, no player builds, no changes to the persistent profile.
public sealed class TelekineticVisualCorrectionTests
{
    public const string Output="Artifacts/GeneratedQA/TelekineticVisualCorrection";
    private const string SceneKey="Subject42.TelekineticVisualCorrection.SceneSetup";
    private const BindingFlags Hidden=BindingFlags.Instance|BindingFlags.NonPublic;
    [Serializable] private sealed class SceneRecord { public string Path; public bool Loaded,Active; }
    [Serializable] private sealed class SceneSetupData { public SceneRecord[] Scenes; }
    private static string Phase => File.Exists(Output+"/phase.txt") ? File.ReadAllText(Output+"/phase.txt").Trim() : "before";
    private static string CapturePath(string name) => Output+"/"+Phase+"-"+name+".png";

    [Test] public void ConstructorHintIsAuthoredWithStandardHudAndCursor()
    {
        var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/prefabs/UI/HUD/GameplayHUD.prefab");
        var hud=prefab.GetComponentInChildren<HUDManager>(true);
        var hint=prefab.GetComponentsInChildren<RectTransform>(true).FirstOrDefault(t=>t.name=="ConstructorHint");
        Assert.That(hint,Is.Not.Null,"Tab must be an authored HUD object visible in Edit Mode, not rig OnGUI");
        Assert.That(hint.gameObject.activeSelf,Is.True);
        var button=hint.GetComponent<UnityEngine.UI.Button>(); Assert.That(button,Is.Not.Null);
        Assert.That(button.targetGraphic.raycastTarget,Is.True);
        Assert.That(button.onClick.GetPersistentEventCount(),Is.EqualTo(1));
        Assert.That(hint.GetComponent<CanvasGroup>().alpha,Is.EqualTo(1),"Authored Edit Mode visibility");
        var gold=(HudIconNumber)new SerializedObject(hud).FindProperty("goldCounter").objectReferenceValue;
        Assert.That(hint.parent,Is.EqualTo(gold.transform.parent));
        Assert.That(button.targetGraphic.color,Is.EqualTo(gold.GetComponentInChildren<UnityEngine.UI.Image>().color));
        var label=hint.GetComponentInChildren<TMPro.TMP_Text>(); Assert.That(label.text,Is.EqualTo("Tab"));
        Assert.That(label.font,Is.EqualTo(gold.GetComponentInChildren<TMPro.TMP_Text>().font));
        Assert.That(hint.GetComponentsInChildren<UnityEngine.UI.Image>().Single(i=>i.name=="Icon").sprite,Is.Not.Null);
        var cursor=hud.OrbitalCursor.GetComponent<Canvas>();
        Assert.That(cursor.overrideSorting,Is.True);
        Assert.That(cursor.sortingOrder,Is.GreaterThan(hint.GetComponentInParent<Canvas>(true).sortingOrder),"Use the existing cursor Canvas above the HUD");
        var hintReference=new SerializedObject(hud).FindProperty("constructorHint");
        Assert.That(((Component)hintReference.objectReferenceValue).transform,Is.EqualTo(hint));
        var scene=EditorSceneManager.OpenPreviewScene("Assets/_Project/Scenes/MainBuild/MVP.unity");
        try
        {
            var sceneHint=scene.GetRootGameObjects().SelectMany(go=>go.GetComponentsInChildren<ConstructorHudHint>(true)).Single();
            Assert.That(sceneHint.gameObject.activeInHierarchy,Is.True,"MVP Edit Mode inherits the authored hint");
            Assert.That(sceneHint.GetComponent<CanvasGroup>().alpha,Is.EqualTo(1));
            var sceneHud=scene.GetRootGameObjects().SelectMany(go=>go.GetComponentsInChildren<HUDManager>(true)).Single();
            var hudData=new SerializedObject(sceneHud);
            Assert.That(hudData.FindProperty("timerText").objectReferenceValue,Is.Not.Null,"Keep the existing timer source ID");
            var pause=(PauseMenuUI)hudData.FindProperty("pauseMenu").objectReferenceValue;
            Assert.That(pause,Is.Not.Null,"Keep the existing pause source ID");
            Assert.That(new SerializedObject(pause).FindProperty("characterSpawner").objectReferenceValue,Is.Not.Null,"MVP override still resolves to authored PauseMenuUI");
            var upgrades=scene.GetRootGameObjects().SelectMany(go=>go.GetComponentsInChildren<UpgradeManager>(true)).Single();
            Assert.That(new SerializedObject(upgrades).FindProperty("pauseMenu").objectReferenceValue,Is.SameAs(pause));
        }
        finally { EditorSceneManager.ClosePreviewScene(scene); }
    }

    [SetUp] public void PreserveEditor()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode) return; // NUnit repeats setup after EnterPlayMode's domain reload.
        for(int i=0;i<SceneManager.sceneCount;i++)
            Assert.That(SceneManager.GetSceneAt(i).isDirty,Is.False,"Preserve unsaved scene edits before visual verification: "+SceneManager.GetSceneAt(i).path);
        Directory.CreateDirectory(Output);
        SessionState.SetString(SceneKey,JsonUtility.ToJson(new SceneSetupData { Scenes=EditorSceneManager.GetSceneManagerSetup().Select(scene=>new SceneRecord { Path=scene.path,Loaded=scene.isLoaded,Active=scene.isActive }).ToArray() }));
        BunkerDebugTools.SetGameView1600();
    }
    [UnityTearDown] public IEnumerator RestoreEditor()
    {
        if(Application.isPlaying) yield return new ExitPlayMode();
        PlayerPrefs.EndSandbox();
        string setup=SessionState.GetString(SceneKey,"");
        if(!string.IsNullOrEmpty(setup))
        {
            var scenes=JsonUtility.FromJson<SceneSetupData>(setup).Scenes;
            if(scenes!=null && scenes.Any(scene=>scene.Loaded) && scenes.Count(scene=>scene.Active)==1)
                EditorSceneManager.RestoreSceneManagerSetup(scenes.Select(scene=>new SceneSetup { path=scene.Path,isLoaded=scene.Loaded,isActive=scene.Active }).ToArray());
        }
        SessionState.EraseString(SceneKey);
    }
    [UnityTest,Timeout(45000)] public IEnumerator LabReference()
    {
        EditorSceneManager.OpenScene(TelekineticConstructorLab.ScenePath);
        yield return new EnterPlayMode();
        yield return CaptureLab();
    }
    private static IEnumerator CaptureLab()
    {
        yield return CoreTestSupport.Await(()=>Object.FindFirstObjectByType<TelekineticConstructorLab>() is {Player:not null,Construction:not null});
        var lab=Object.FindFirstObjectByType<TelekineticConstructorLab>();
        lab.EnemyDensity=0;
        foreach(int preset in new[]{3,4})
        {
            lab.LoadPreset(preset);
            string name=preset==3?"compact":"heavy";
            File.WriteAllText(Output+"/"+name+"-layout.json",JsonUtility.ToJson(lab.Build,true));
            Select(lab,lab.Build.Items.First(c=>c.Kind==TelekineticPart.Crit));
            yield return Capture("lab-"+name);
        }
        Dump(lab.Player.transform,"lab-sorting");
    }
    [UnityTest,Timeout(75000)] public IEnumerator ProductionViews()
    {
        PlayerPrefs.BeginSandbox(false);
        PlayerPrefs.SetInt(BunkerIntroController.ViewedPreferenceKey,1);
        PlayerPrefs.SetInt(TutorialController.CompletionKey,1);
        EditorSceneManager.OpenScene("Assets/_Project/Scenes/MainBuild/StartScreen.unity");
        yield return new EnterPlayMode();
        yield return CaptureProduction();
    }
    private static IEnumerator CaptureProduction()
    {
        Object.FindFirstObjectByType<StartScreenController>().Begin();
        yield return CoreTestSupport.Await(()=>SceneManager.GetActiveScene().name==RunEndService.BunkerSceneName && Object.FindFirstObjectByType<BunkerRunStarter>()!=null && !SceneTransitionOverlay.IsTransitioning);
        RunSelectionManager.Instance.SelectCharacter(AssetDatabase.LoadAssetAtPath<CharacterData>("Assets/_Project/Data/Characters/01_Gera.asset"));
        var starter=Object.FindFirstObjectByType<BunkerRunStarter>(); starter.StartRun(starter.transform);
        yield return CoreTestSupport.Await(()=>SceneManager.GetActiveScene().name=="MVP" && ProductionSceneComposition.Active is {IsReady:true} && !SceneTransitionOverlay.IsTransitioning);
        var player=Object.FindFirstObjectByType<CharacterSpawner>();
        var station=player.Station; var rig=station.Rig;
        player.SpawnedPlayer.GetComponent<PlayerHealth>().DebugInvulnerable=true;
        Object.FindFirstObjectByType<EnemySpawner>().StopSpawning();
        EnemyDebugAiFreeze.SetFrozen(true);
        MoveToNeutral(station);
        if(Phase=="catalogue")
        {
            yield return RigCatalogueFxCheck.Run(station,Capture);
            RunEndService.Instance.ReturnToBunker();
            yield return CoreTestSupport.Await(()=>SceneManager.GetActiveScene().name==RunEndService.BunkerSceneName && !SceneTransitionOverlay.IsTransitioning);
            yield break;
        }
        if(Phase=="hud-minimal" || Phase=="hud-authored")
        {
            yield return Frames(12);
            yield return Capture("combat");
            if(Phase=="hud-authored") yield return VerifyAuthoredHint(station);
            RunEndService.Instance.ReturnToBunker();
            yield return CoreTestSupport.Await(()=>SceneManager.GetActiveScene().name==RunEndService.BunkerSceneName && !SceneTransitionOverlay.IsTransitioning);
            File.WriteAllText(Output+"/hud-minimal-completed.txt","Real Unity Editor Game View 1600x900; compact Tab + constructor icon, no control strip; returned to bunker.");
            yield break;
        }
        while(rig.Build.AvailableRadius<TelekineticBuild.Radius) yield return Grant(station,OrbitalRewardKind.RigExpansion);
        foreach(string name in new[]{"compact","heavy"})
        {
            var reference=JsonUtility.FromJson<TelekineticBuild>(File.ReadAllText(Output+"/"+name+"-layout.json"));
            Assert.That(station.Interaction.OpenConstructor(),Is.True);
            StowAll(rig); station.Interaction.CloseConstructor();
            foreach(var group in reference.Items.GroupBy(c=>c.Kind))
                while(rig.Build.Reserve.Count(c=>c.Kind==group.Key)<group.Count()+1)
                {
                    var reward=Enum.GetValues(typeof(OrbitalRewardKind)).Cast<OrbitalRewardKind>().First(k=>OrbitalRewardProvider.GetRigPart(k)==group.Key);
                    yield return Grant(station,reward);
                }
            Assert.That(station.Interaction.OpenConstructor(),Is.True);
            foreach(var item in reference.Items) Assert.That(rig.Place(item.Kind,item.Position,item.QuarterTurns),Is.True,rig.Build.Message);
            Select(rig,rig.Build.Items.First(c=>c.Kind==TelekineticPart.Crit));
            yield return Capture("production-"+name);
            station.Interaction.CloseConstructor();
        }
        if(Phase=="columns")
        {
            Assert.That(station.Interaction.OpenConstructor(),Is.True);
            VerifyConstructorUi(rig);
            station.Interaction.CloseConstructor();
            RunEndService.Instance.ReturnToBunker();
            yield return CoreTestSupport.Await(()=>SceneManager.GetActiveScene().name==RunEndService.BunkerSceneName && !SceneTransitionOverlay.IsTransitioning);
            File.WriteAllText(Output+"/columns-completed.txt","1600x900 Game View; filled three-column UI; reserve drag/drop + rotation + invalid placement + swap + merge + Delete handler; returned to bunker.");
            yield break;
        }
        // Read real renderer hierarchy before screenshots; no sorting override in the fixture.
        Dump(station.Owner.Transform,"production-sorting");
        if(Phase=="after") AssertSorting(station);
        yield return Frames(20);
        yield return Capture("combat");
        float before=rig.Angle;
        rig.AdvanceRotation(.6f,true,false);
        yield return Frames(12);
        Assert.That(Mathf.Abs(Mathf.DeltaAngle(before,rig.Angle)),Is.GreaterThan(10));
        File.WriteAllText(Output+"/"+Phase+"-rotation.txt",before+" -> "+rig.Angle);
        yield return Capture("combat-rotating");
        Dump(station.Owner.Transform,"production-sorting-combat");
        if(Phase=="after") AssertSorting(station);
        RunEndService.Instance.ReturnToBunker();
        yield return CoreTestSupport.Await(()=>SceneManager.GetActiveScene().name==RunEndService.BunkerSceneName && !SceneTransitionOverlay.IsTransitioning);
        File.WriteAllText(Output+"/"+Phase+"-completed.txt","Real Editor Game View 1600x900; lab and MVP; reward-owned reserve and valid layouts; returned to bunker.");
    }
    private static IEnumerator Grant(OrbitalStationRuntime station,OrbitalRewardKind kind)
    {
        yield return CoreTestSupport.Await(()=>UpgradeManager.Instance.IsRewardQueueIdle);
        Assert.That(UpgradeManager.Instance.DebugForceOrbitalReward(kind),Is.True,"Reward fixture: "+kind);
        Assert.That(UpgradeManager.Instance.DebugSelectCurrentChoice(0),Is.True);
        yield return CoreTestSupport.Await(()=>station.InputOwner.IsConstructing);
        station.Interaction.CloseConstructor();
        yield return CoreTestSupport.Await(()=>!UpgradeManager.Instance.IsChoosingUpgrade && Time.timeScale>0);
    }
    private static void StowAll(TelekineticRig rig)
    {
        while(rig.Build.Items.Count>0)
        {
            int count=rig.Build.Items.Count;
            foreach(var item in rig.Build.Items.Reverse().ToArray()) rig.Remove(item);
            Assert.That(rig.Build.Items.Count,Is.LessThan(count),"Whole-item stowing must preserve connectivity");
        }
    }
    private static void Select(object owner,TelekineticItem item) => owner.GetType().GetField("selected",Hidden).SetValue(owner,item);
    private static IEnumerator VerifyAuthoredHint(OrbitalStationRuntime station)
    {
        var hint=Object.FindFirstObjectByType<ConstructorHudHint>(); Assert.That(hint,Is.Not.Null);
        var group=hint.GetComponent<CanvasGroup>(); var button=hint.GetComponent<UnityEngine.UI.Button>();
        Assert.That(group.alpha,Is.EqualTo(1)); Assert.That(button.interactable,Is.True);
        var rect=(RectTransform)hint.transform;
        var screen=RectTransformUtility.WorldToScreenPoint(null,rect.TransformPoint(rect.rect.center));
        var pointer=new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current)
            {position=screen,button=UnityEngine.EventSystems.PointerEventData.InputButton.Left};
        var hits=new System.Collections.Generic.List<UnityEngine.EventSystems.RaycastResult>();
        UnityEngine.EventSystems.EventSystem.current.RaycastAll(pointer,hits);
        Assert.That(hits.Count,Is.GreaterThan(0));
        Assert.That(hits[0].gameObject.GetComponentInParent<ConstructorHudHint>(),Is.SameAs(hint),"Tab participates in the standard UI raycaster");
        var cursor=HUDManager.Instance.OrbitalCursor;
        bool cursorWasEnabled=cursor.enabled;
        try
        {
            // Position the existing UI cursor above Tab for an actual rendering capture.
            cursor.enabled=false;
            cursor.GetComponent<RectTransform>().position=screen;
            yield return Frames(2);
            yield return Capture("cursor-over-tab");
        }
        finally { cursor.enabled=cursorWasEnabled; }
        UnityEngine.EventSystems.ExecuteEvents.ExecuteHierarchy(hits[0].gameObject,pointer,UnityEngine.EventSystems.ExecuteEvents.pointerClickHandler);
        Assert.That(station.InputOwner.IsConstructing,Is.True,"Standard UI click follows the same guarded entry as Tab");
        Assert.That(Time.timeScale,Is.Zero);
        yield return Frames(2); Assert.That(group.alpha,Is.Zero,"Hide with the rest of the HUD while editing");
        Assert.That(station.InputOwner.TryToggleConstructor(),Is.True);
        yield return Frames(2); Assert.That(group.alpha,Is.EqualTo(1)); Assert.That(Time.timeScale,Is.GreaterThan(0));
        var pause=(PauseMenuUI)new SerializedObject(HUDManager.Instance).FindProperty("pauseMenu").objectReferenceValue;
        Assert.That(pause.isActiveAndEnabled,Is.True);
        var escape=typeof(PauseMenuUI).GetMethod("HandleEscape",Hidden);
        escape.Invoke(pause,null); Assert.That(pause.IsPaused,Is.True);
        yield return Frames(2); Assert.That(group.alpha,Is.Zero);
        escape.Invoke(pause,null); Assert.That(pause.IsPaused,Is.False);
        yield return Frames(2); Assert.That(group.alpha,Is.EqualTo(1)); Assert.That(Time.timeScale,Is.GreaterThan(0));
    }
    private static object Ui(TelekineticRig rig,string method,params object[] args) => typeof(TelekineticRig).GetMethod(method,Hidden|BindingFlags.Static).Invoke(rig,args);
    private static Vector2 UiPoint(Vector2Int p) => (Vector2)typeof(TelekineticRig).GetMethod("Center",Hidden|BindingFlags.Static).Invoke(null,new object[]{p});
    private static Rect UiCell(Vector2Int p) => (Rect)typeof(TelekineticRig).GetMethod("CellRect",Hidden|BindingFlags.Static).Invoke(null,new object[]{p});
    private static Event Mouse(EventType type,Vector2Int p) => new() {type=type,button=0,mousePosition=UiPoint(p)};
    private static void VerifyConstructorUi(TelekineticRig rig)
    {
        var build=rig.Build;
        int[] allIds=build.Items.Concat(build.Reserve).Select(c=>c.Id).OrderBy(id=>id).ToArray();
        var item=build.Reserve.First(c=>c.Kind==TelekineticPart.Energy);
        int items=build.Items.Count,reserve=build.Reserve.Count;
        Ui(rig,"HandleReserveInput",item,new Event {type=EventType.MouseDown,button=0});
        Ui(rig,"RotateInteraction");
        Ui(rig,"HandleReserveDrop",Mouse(EventType.MouseUp,Vector2Int.zero));
        Assert.That(build.Items.Count,Is.EqualTo(items)); Assert.That(build.Reserve.Count,Is.EqualTo(reserve));
        Assert.That(build.Reserve.Contains(item),Is.True,"Invalid stock drop must keep the actual earned item");
        Vector2Int? target=null;
        for(int y=-4;y<=4 && !target.HasValue;y++) for(int x=-4;x<=4 && !target.HasValue;x++)
            if(build.CanPlace(item.Kind,new Vector2Int(x,y),1)) target=new Vector2Int(x,y);
        Assert.That(target.HasValue,Is.True,"Fixture needs a legal rotated two-cell placement");
        Ui(rig,"HandleReserveInput",item,new Event {type=EventType.MouseDown,button=0});
        Ui(rig,"RotateInteraction");
        Ui(rig,"HandleReserveDrop",Mouse(EventType.MouseUp,target.Value));
        Assert.That(build.Items.Contains(item),Is.True); Assert.That(build.Reserve.Contains(item),Is.False);
        Assert.That(item.QuarterTurns,Is.EqualTo(1)); Assert.That(build.ValidateInventory(),Is.True);
        CollectionAssert.AreEqual(allIds,build.Items.Concat(build.Reserve).Select(c=>c.Id).OrderBy(id=>id).ToArray());
        foreach(var stock in build.Reserve.Concat(build.Items))
        {
            string tooltip=(string)Ui(rig,"ItemTooltip",stock);
            StringAssert.Contains(TelekineticBuild.Label(stock.Kind),tooltip);
            StringAssert.Contains(TelekineticBuild.Roman(stock.Level),tooltip);
            Assert.That(tooltip.Split('\n').Length,Is.InRange(2,3));
            if(stock.IsModule || stock.IsWeapon) StringAssert.Contains("КД",tooltip);
            else Assert.That(tooltip.Split('\n').Length,Is.EqualTo(2),"Passive parts have no cooldown line");
        }
        // The same item input handler used by the grid still merges only a compatible whole item.
        var keep=build.Items.First(c=>c!=item && c.Kind==item.Kind && c.Level==item.Level);
        Select(rig,keep); typeof(TelekineticRig).GetField("merging",Hidden).SetValue(rig,true);
        Ui(rig,"HandleCell",item.Position,item,UiCell(item.Position),Mouse(EventType.MouseDown,item.Position));
        Assert.That(keep.Level,Is.EqualTo(2)); Assert.That(build.Items.Contains(item),Is.False);
        Assert.That(build.ValidateInventory(),Is.True);
        // Exercise move/swap via mouse-down/up, not a direct model mutation.
        var pair=(from a in build.Items from b in build.Items where a!=b && build.CanMove(a,b.Position,a.QuarterTurns) select (a,b)).First();
        var first=pair.a.Position; var second=pair.b.Position;
        Ui(rig,"HandleCell",first,pair.a,UiCell(first),Mouse(EventType.MouseDown,first));
        Ui(rig,"HandleCell",second,pair.b,UiCell(second),Mouse(EventType.MouseUp,second));
        Assert.That(pair.a.Position,Is.EqualTo(second)); Assert.That(pair.b.Position,Is.EqualTo(first));
        Assert.That(build.ValidateInventory(),Is.True);
        // Delete/stow uses the unchanged connectivity rule; choose a removable leaf.
        var leaf=build.Items.First(c=>
        {
            var snapshot=JsonUtility.FromJson<TelekineticBuild>(JsonUtility.ToJson(build));
            return snapshot.Stow(snapshot.Items.First(copy=>copy.Id==c.Id));
        });
        Select(rig,leaf); typeof(TelekineticRig).GetField("merging",Hidden).SetValue(rig,true);
        Ui(rig,"RemoveSelected");
        Assert.That(build.Reserve.Contains(leaf),Is.True); Assert.That(build.Items.Contains(leaf),Is.False);
        Assert.That(build.ValidateInventory(),Is.True);
        var click=build.Items.First();
        Ui(rig,"HandleCell",click.Position,click,UiCell(click.Position),Mouse(EventType.MouseDown,click.Position));
        bool canSelectAfterStow=typeof(TelekineticRig).GetField("selected",Hidden).GetValue(rig)==click;
        rig.CancelSelection();
        var weapon=build.Items.Where(c=>c.IsWeapon).First(c=>
        {
            var snapshot=JsonUtility.FromJson<TelekineticBuild>(JsonUtility.ToJson(build));
            return snapshot.Stow(snapshot.Items.First(copy=>copy.Id==c.Id));
        });
        var originalPosition=weapon.Position;
        var rapid=(System.Collections.Generic.IDictionary<int,float>)typeof(TelekineticItem).GetField("Rapid",Hidden).GetValue(weapon);
        rapid[99999]=2;
        Assert.That(rig.RateFactor(weapon),Is.GreaterThan(1));
        Assert.That(rig.Remove(weapon),Is.True);
        string stowedTooltip=(string)Ui(rig,"ItemTooltip",weapon);
        Ui(rig,"HandleReserveInput",weapon,new Event {type=EventType.MouseDown,button=0});
        Ui(rig,"HandleReserveDrop",Mouse(EventType.MouseUp,originalPosition));
        Assert.That(build.Items.Contains(weapon),Is.True);
        string installedTooltip=(string)Ui(rig,"ItemTooltip",weapon);
        Assert.That(rig.RateFactor(weapon),Is.EqualTo(1),"Installing clears the pending FR charge");
        Assert.That(canSelectAfterStow && stowedTooltip==installedTooltip,Is.True,
            "Merge → Delete allows selection: "+canSelectAfterStow+"; reserve CD must match reinstalled weapon without stale FR.\nReserve: "+stowedTooltip+"\nInstalled: "+installedTooltip);
    }
    private static void AssertSorting(OrbitalStationRuntime station)
    {
        var rig=station.Rig; var root=rig.Construction.GetComponent<SortingGroup>();
        var playerGroup=station.Owner.Transform.GetComponent<SortingGroup>();
        Assert.That(root,Is.Not.Null); Assert.That(root.sortAtRoot,Is.False);
        Assert.That(playerGroup.sortingLayerName,Is.EqualTo("Player"));
        Assert.That(playerGroup.sortingOrder,Is.Zero,"Keep the authored global player order");
        var body=station.Owner.Transform.GetComponent<CharacterMovement2D>().VisualRoot.GetComponentsInChildren<SpriteRenderer>().First(r=>r.enabled);
        Assert.That(root.sortingLayerID,Is.EqualTo(body.sortingLayerID));
        foreach(var renderer in station.Owner.Transform.GetComponentsInChildren<SpriteRenderer>().Where(r=>r.enabled &&
            !r.transform.IsChildOf(station.transform) && r.sortingLayerID==root.sortingLayerID && r.GetComponentInParent<SortingGroup>()==playerGroup))
            Assert.That(root.sortingOrder,Is.LessThan(renderer.sortingOrder),"Rig below character/shadow: "+renderer.name);
        var chassis=rig.Construction.Cast<Transform>().First(t=>t.name=="Telekinetic chassis / visual only").GetComponent<SortingGroup>();
        Assert.That(chassis.sortingLayerID,Is.EqualTo(root.sortingLayerID)); Assert.That(chassis.sortingOrder,Is.Zero);
        foreach(var item in rig.Build.Items)
        {
            var view=rig.Construction.Find(TelekineticBuild.Label(item.Kind)+" #"+item.Id);
            Assert.That(view.localPosition.z,Is.Zero);
            if(item.IsWeapon)
            {
                var group=view.GetComponent<SortingGroup>(); Assert.That(group,Is.Not.Null);
                Assert.That(group.sortingLayerID,Is.EqualTo(root.sortingLayerID)); Assert.That(group.sortingOrder,Is.EqualTo(3));
                foreach(var nested in view.GetComponentsInChildren<SortingGroup>(true)) Assert.That(nested.sortAtRoot,Is.False,nested.name);
            }
            else
            {
                var icon=view.Find("Pixel node").GetComponent<SpriteRenderer>();
                Assert.That(icon.sortingLayerID,Is.EqualTo(root.sortingLayerID)); Assert.That(icon.sortingOrder,Is.EqualTo(1));
                if(item.IsModule) Assert.That(view.Find("Independent cooldown charge").GetComponent<SpriteRenderer>().sortingOrder,Is.EqualTo(2));
            }
        }
    }
    private static IEnumerator Capture(string name)
    {
        yield return Frames(4);
        Assert.That(Screen.width,Is.EqualTo(1600)); Assert.That(Screen.height,Is.EqualTo(900));
        string path=CapturePath(name); if(File.Exists(path)) File.Delete(path);
        ScreenCapture.CaptureScreenshot(path);
        yield return CoreTestSupport.Await(()=>File.Exists(path) && new FileInfo(path).Length>1000);
        var png=new Texture2D(2,2); Assert.That(png.LoadImage(File.ReadAllBytes(path)),Is.True);
        Assert.That(png.width,Is.EqualTo(1600)); Assert.That(png.height,Is.EqualTo(900)); Object.Destroy(png);
    }
    private static IEnumerator Frames(int count) { for(int i=0;i<count;i++) yield return null; }
    private static void MoveToNeutral(OrbitalStationRuntime station)
    {
        var bounds=GameplayAreaService.Instance.PlayableArea.bounds;
        for(int y=2;y<18;y++) for(int x=2;x<18;x++)
        {
            var point=new Vector2(Mathf.Lerp(bounds.min.x,bounds.max.x,x/20f),Mathf.Lerp(bounds.min.y,bounds.max.y,y/20f));
            if(LevelAnomalyController.Instance.FindOrbitZone(point)!=null) continue;
            station.Owner.Transform.position=point;
            if(station.Owner.Transform.TryGetComponent<Rigidbody2D>(out var body)){body.position=point;body.linearVelocity=Vector2.zero;}
            Physics2D.SyncTransforms(); return;
        }
        Assert.Fail("No neutral capture point");
    }
    private static void Dump(Transform player,string label)
    {
        var text=new StringBuilder();
        string Path(Transform t) => t==null?"<none>":t.parent==null?t.name:Path(t.parent)+"/"+t.name;
        foreach(var group in player.GetComponentsInChildren<SortingGroup>(true))
            text.AppendLine($"GROUP {Path(group.transform)} | active={group.isActiveAndEnabled} | layer={group.sortingLayerName}:{SortingLayer.GetLayerValueFromID(group.sortingLayerID)} | order={group.sortingOrder} | root={group.sortAtRoot} | world={group.transform.position} | local={group.transform.localPosition}");
        foreach(var renderer in player.GetComponentsInChildren<Renderer>(true))
        {
            var group=renderer.GetComponentInParent<SortingGroup>();
            text.AppendLine($"RENDER {Path(renderer.transform)} | active={renderer.enabled && renderer.gameObject.activeInHierarchy} | type={renderer.GetType().Name} | layer={renderer.sortingLayerName}:{SortingLayer.GetLayerValueFromID(renderer.sortingLayerID)} | order={renderer.sortingOrder} | group={Path(group!=null?group.transform:null)} | world={renderer.transform.position} | local={renderer.transform.localPosition} | shader={renderer.sharedMaterial?.shader?.name} | queue={renderer.sharedMaterial?.renderQueue}");
        }
        File.WriteAllText(Output+"/"+Phase+"-"+label+".txt",text.ToString());
    }
}
#endif
