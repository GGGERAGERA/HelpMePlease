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
    [UnityTest, Timeout(75000)]
    public IEnumerator CleanChassisAndSniperInMvp()
    {
        PlayerPrefs.BeginSandbox(false);
        PlayerPrefs.SetInt(BunkerIntroController.ViewedPreferenceKey, 1);
        PlayerPrefs.SetInt(TutorialController.CompletionKey, 1);
        EditorSceneManager.OpenScene("Assets/_Project/Scenes/MainBuild/StartScreen.unity");
        yield return new EnterPlayMode();
        yield return CaptureProduction(cleanChassis: true);
    }

    private static IEnumerator CaptureProduction(bool cleanChassis = false)
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
        if (cleanChassis)
        {
            yield return VerifyCleanChassis(station);
            RunEndService.Instance.ReturnToBunker();
            yield return CoreTestSupport.Await(() => SceneManager.GetActiveScene().name == RunEndService.BunkerSceneName && !SceneTransitionOverlay.IsTransitioning);
            yield break;
        }
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
        if (Phase == "columns")
        {
            yield return VerifyConstructorUi(station);
            RunEndService.Instance.ReturnToBunker();
            yield return CoreTestSupport.Await(() => SceneManager.GetActiveScene().name == RunEndService.BunkerSceneName && !SceneTransitionOverlay.IsTransitioning);
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
    private static IEnumerator VerifyCleanChassis(OrbitalStationRuntime station)
    {
        var rig = station.Rig;
        Assert.That(rig.Build.Items.Concat(rig.Build.Reserve).Any(item => item.Kind == TelekineticPart.Frame), Is.False);
        Assert.That(rig.StructureView.SimpleLinks, Is.True);
        var art = rig.SniperVisual.GetComponent<OrbitalModuleView>();
        var reward = OrbitalRewardIconResolver.Resolve(OrbitalModuleKind.Sniper);
        Assert.That(reward.Sprite, Is.SameAs(art.IconSource.sprite));
        Assert.That(reward.ImageTint, Is.EqualTo(art.IconSource.color));
        Assert.That(art.Sprites.Count(layer => layer.enabled && layer != art.Halo), Is.EqualTo(2));

        yield return Grant(station, OrbitalRewardKind.Sniper);
        yield return Grant(station, OrbitalRewardKind.Shotgun);
        yield return Grant(station, OrbitalRewardKind.DamageLink);
        yield return Grant(station, OrbitalRewardKind.FireRateLink);
        yield return Grant(station, OrbitalRewardKind.RigEnergy);
        Assert.That(station.Interaction.OpenConstructor(), Is.True);
        rig.AutoRotate = false;
        yield return Capture("sniper-stock");
        Assert.That(rig.Place(TelekineticPart.Sniper, Vector2Int.left, 1), Is.False, "The second cell cannot occupy the core");
        Assert.That(rig.Place(TelekineticPart.Sniper, Vector2Int.left, 2), Is.True);
        var sniper = rig.Build.At(Vector2Int.left);
        Assert.That(sniper.CellCount, Is.EqualTo(2));
        Assert.That(rig.Build.At(new Vector2Int(-1, -1)), Is.SameAs(sniper));
        EditorWindow.GetWindow(typeof(Editor).Assembly.GetType("UnityEditor.GameView")).Focus();
        Vector2 sniperPoint = UiCell(sniper.Position).center;
        yield return GameViewEvent(EventType.MouseDown, sniperPoint);
        yield return GameViewEvent(EventType.MouseUp, sniperPoint);
        yield return GameViewEvent(EventType.KeyDown, sniperPoint, key: KeyCode.R);
        Assert.That(rig.Build.At(new Vector2Int(-2, 0)), Is.SameAs(sniper));
        Assert.That(rig.Build.At(new Vector2Int(-1, -1)), Is.Null);
        Assert.That(rig.Place(TelekineticPart.Shotgun, new Vector2Int(1, -1)), Is.True);
        Assert.That(rig.Place(TelekineticPart.DMG, Vector2Int.up, 1), Is.True);
        Assert.That(rig.Place(TelekineticPart.FR, new Vector2Int(-1, 1)), Is.True);
        Assert.That(rig.Place(TelekineticPart.Energy, new Vector2Int(1, 2), 3), Is.True);
        yield return Capture("clean-constructor");
        station.Interaction.CloseConstructor();

        Assert.That(rig.StructureView.EdgeCount, Is.GreaterThan(5));
        Assert.That(rig.Build.ValidateInventory(), Is.True);
        yield return Capture("clean-combat");
        rig.AdvanceRotation(.4f, true, false);
        yield return Capture("clean-combat-turned");
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
        Assert.That(Cursor.visible,Is.True,"MVP uses the native pointer");
        Assert.That(Cursor.lockState,Is.EqualTo(CursorLockMode.None));
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
    private static Rect UiCell(Vector2Int p) => (Rect)Ui(null, "CellRect", p);

    private static IEnumerator GameViewEvent(EventType type, Vector2 point, Vector2 delta = default, KeyCode key = KeyCode.None)
    {
        // Game View's own input queue accepts coordinates in game pixels, independent of Editor zoom.
        var input = new Event { type = type, button = 0, mousePosition = point, delta = delta, keyCode = key };
        EditorGUIUtility.QueueGameViewInputEvent(input);
        yield return Frames(3);
    }

    private static IEnumerator VerifyConstructorUi(OrbitalStationRuntime station)
    {
        var rig = station.Rig;
        yield return Grant(station, OrbitalRewardKind.DamageLink);
        yield return Grant(station, OrbitalRewardKind.FireRateLink);
        yield return Grant(station, OrbitalRewardKind.Shotgun);
        Assert.That(station.Interaction.OpenConstructor(), Is.True);
        Assert.That(Cursor.visible, Is.True);
        Assert.That(Cursor.lockState, Is.EqualTo(CursorLockMode.None));
        yield return Capture("starter");
        yield return new WaitForSecondsRealtime(.35f);
        yield return Capture("starter-later");

        var gameView = EditorWindow.GetWindow(typeof(Editor).Assembly.GetType("UnityEditor.GameView"));
        gameView.Focus();
        yield return Frames(3);
        var stock = rig.Build.Reserve.First(item => item.Kind == TelekineticPart.DMG);
        Vector2 stockRow = new(1240, 195);
        Vector2 core = UiCell(Vector2Int.zero).center;
        int before = rig.Build.Items.Count;
        yield return GameViewEvent(EventType.MouseDown, stockRow);
        Assert.That(typeof(TelekineticRig).GetField("held", Hidden).GetValue(rig), Is.SameAs(stock));
        yield return GameViewEvent(EventType.MouseDrag, core);
        yield return Capture("held");
        yield return GameViewEvent(EventType.MouseUp, core);
        Assert.That(rig.Build.Reserve.Contains(stock), Is.True, "Invalid drop returns the same reward to reserve");

        yield return GameViewEvent(EventType.MouseDown, stockRow);
        yield return GameViewEvent(EventType.KeyDown, stockRow, key: KeyCode.R);
        var target = new Vector2Int(0, 1);
        Assert.That(rig.Build.CanPlace(stock.Kind, target, 1), Is.True);
        yield return GameViewEvent(EventType.MouseDrag, UiCell(target).center);
        yield return Capture("preview");
        yield return GameViewEvent(EventType.MouseUp, UiCell(target).center);
        Assert.That(rig.Build.Items.Count, Is.EqualTo(before + 1));
        Assert.That(rig.Build.Items.Contains(stock), Is.True);
        Assert.That(rig.Build.Reserve.Contains(stock), Is.False);
        Assert.That(stock.QuarterTurns, Is.EqualTo(1));
        yield return Capture("placed");

        var gun = rig.Build.Items.First(item => item.IsWeapon);
        var gunCell = gun.Position;
        float heading = gun.Heading;
        Vector2 gunPoint = UiCell(gunCell).center;
        yield return GameViewEvent(EventType.MouseDown, gunPoint);
        yield return GameViewEvent(EventType.MouseUp, gunPoint);
        yield return GameViewEvent(EventType.ScrollWheel, gunPoint, Vector2.up);
        Assert.That(Mathf.DeltaAngle(heading, gun.Heading), Is.EqualTo(-45).Within(.01f));
        Assert.That(gun.Position, Is.EqualTo(gunCell));
        var gunView = rig.Construction.Find(TelekineticBuild.Label(gun.Kind) + " #" + gun.Id);
        Assert.That(Mathf.DeltaAngle(gun.Heading, gunView.localEulerAngles.z), Is.EqualTo(0).Within(.01f));
        Assert.That(rig.Build.ValidateInventory(), Is.True);
        yield return Capture("turned");
        Vector2 close = new(1446, 66);
        yield return GameViewEvent(EventType.MouseDown, close);
        yield return GameViewEvent(EventType.MouseUp, close);
        Assert.That(rig.Editing, Is.False);
        Assert.That(Time.timeScale, Is.GreaterThan(0));
        yield return Capture("combat");
        yield return VerifyAreaGrowth(station);
    }

    private static IEnumerator VerifyAreaGrowth(OrbitalStationRuntime station)
    {
        var rig = station.Rig;
        var shotgun = rig.Build.Reserve.Single(item => item.Kind == TelekineticPart.Shotgun);
        var target = new Vector2Int(0, 2);
        Vector2 stock = new(124, 195);
        Vector2 targetPoint = UiCell(target).center;

        Assert.That(station.Interaction.OpenConstructor(), Is.True);
        Assert.That(rig.Build.AvailableRadius, Is.EqualTo(2));
        yield return Capture("area-5");
        yield return GameViewEvent(EventType.MouseDown, stock);
        yield return GameViewEvent(EventType.MouseDrag, targetPoint);
        yield return Capture("area-5-overflow");
        yield return GameViewEvent(EventType.MouseUp, targetPoint);
        Assert.That(rig.Build.Reserve, Does.Contain(shotgun), "The outer cell still blocks this drop");
        station.Interaction.CloseConstructor();

        yield return Grant(station, OrbitalRewardKind.RigExpansion);
        Assert.That(station.Interaction.OpenConstructor(), Is.True);
        Assert.That(rig.Build.AvailableRadius, Is.EqualTo(3));
        yield return Capture("area-7-reveal");
        yield return new WaitForSecondsRealtime(.4f);
        yield return Capture("area-7");
        yield return GameViewEvent(EventType.MouseDown, stock);
        yield return GameViewEvent(EventType.MouseDrag, targetPoint);
        yield return Capture("area-7-valid");
        yield return GameViewEvent(EventType.MouseUp, targetPoint);
        Assert.That(rig.Build.At(target + Vector2Int.up), Is.SameAs(shotgun), "The same drop fits after expansion");
        station.Interaction.CloseConstructor();

        yield return Grant(station, OrbitalRewardKind.RigExpansion);
        Assert.That(station.Interaction.OpenConstructor(), Is.True);
        Assert.That(rig.Build.AvailableRadius, Is.EqualTo(TelekineticBuild.Radius));
        yield return new WaitForSecondsRealtime(.4f);
        yield return Capture("area-9");
        Vector2 edge = UiCell(new Vector2Int(0, 4)).center;
        yield return GameViewEvent(EventType.MouseDown, targetPoint);
        yield return GameViewEvent(EventType.MouseDrag, edge);
        yield return Capture("area-9-overflow");
        yield return GameViewEvent(EventType.MouseUp, edge);
        Assert.That(shotgun.Position, Is.EqualTo(target), "The footprint cannot grow past the maximum radius");

        Vector2 modulePoint = UiCell(Vector2Int.up).center;
        Vector2 moduleHeader = new(1240, 143);
        Vector2 weaponHeader = new(108, 143);
        var support = rig.Build.At(Vector2Int.up);

        yield return GameViewEvent(EventType.MouseDown, modulePoint);
        yield return GameViewEvent(EventType.MouseDrag, moduleHeader);
        yield return Capture("stock-blocked");
        yield return GameViewEvent(EventType.MouseUp, moduleHeader);
        Assert.That(rig.Build.At(Vector2Int.up), Is.SameAs(support), "Removing the support would disconnect the shotgun");

        yield return GameViewEvent(EventType.MouseDown, targetPoint);
        yield return GameViewEvent(EventType.MouseUp, targetPoint);
        yield return Capture("component-selected");
        yield return GameViewEvent(EventType.MouseDown, targetPoint);
        yield return GameViewEvent(EventType.MouseDrag, moduleHeader);
        yield return GameViewEvent(EventType.MouseUp, moduleHeader);
        Assert.That(rig.Build.At(target), Is.SameAs(shotgun), "Weapons return to the weapon panel");

        yield return GameViewEvent(EventType.MouseDown, targetPoint);
        yield return GameViewEvent(EventType.MouseDrag, weaponHeader);
        yield return Capture("stock-weapon-target");
        yield return GameViewEvent(EventType.MouseUp, weaponHeader);
        Assert.That(rig.Build.Reserve, Does.Contain(shotgun), "The weapon panel header also accepts drops");

        yield return GameViewEvent(EventType.MouseDown, modulePoint);
        yield return GameViewEvent(EventType.MouseDrag, moduleHeader);
        yield return Capture("stock-module-target");
        yield return GameViewEvent(EventType.MouseUp, moduleHeader);
        Assert.That(rig.Build.Reserve, Does.Contain(support), "The module panel header also accepts drops");
        Assert.That(rig.Build.ValidateInventory(), Is.True);
        station.Interaction.CloseConstructor();
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
