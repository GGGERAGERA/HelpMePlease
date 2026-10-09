#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Subject42.Combat.OrbitalStation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using Object=UnityEngine.Object;

public sealed class FalseSignalPlayTests
{
    private const string Folder="Artifacts/GeneratedQA/FalseSignal/";
    [SetUp] public void Setup()=>CoreTestSupport.PreservePreferences();
    [UnityTearDown] public IEnumerator Cleanup()=>CoreTestSupport.CleanupPlayMode();
    private static T Read<T>(object instance,string field)=>(T)instance.GetType().GetField(field,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(instance);
    private static void Set(object instance,string field,object value)=>CorridorMigrationTestSupport.Set(instance,field,value);
    private static void Call(object instance,string method,params object[] values)=>CorridorMigrationTestSupport.Call(instance,method,values);
    private static FalseSignalEvent Spawn(WorldEventSpawner events,Vector2 preferred)
    {
        var prefab=AssetDatabase.LoadAssetAtPath<FalseSignalEvent>("Assets/_Project/prefabs/Environment/WorldEvents/FalseSignalEvent.prefab");
        events.ConfigureDebugEventPrefabs(events.EventPrefabs.ToArray());
        foreach(var offset in new[]{Vector2.zero,Vector2.right*12,Vector2.left*12,Vector2.up*12,Vector2.down*12})
        {
            bool admitted=events.SpawnConcurrentDebugEventAt(prefab,preferred+offset,true,out var instance);
            if(admitted) return (FalseSignalEvent)instance;
        }
        Assert.Fail("No valid separated transmitter placement in the production scene."); return null;
    }
    private static IEnumerator BeginProductionRun()
    {
        yield return CoreTestSupport.LoadBunker();
        RunSelectionManager.Instance.SelectCharacter(AssetDatabase.LoadAssetAtPath<CharacterData>("Assets/_Project/Data/Characters/01_Gera.asset"));
        var starter=Object.FindFirstObjectByType<BunkerRunStarter>(); starter.StartRun(starter.transform);
        yield return CoreTestSupport.Await(()=>UnityEngine.SceneManagement.SceneManager.GetActiveScene().name=="MVP" &&
            Object.FindFirstObjectByType<CharacterSpawner>()?.SpawnedPlayer?.GetComponentInChildren<OrbitalStationRuntime>() is { IsInitialized:true } && !SceneTransitionOverlay.IsTransitioning);
        yield return null;
        Object.FindFirstObjectByType<CharacterSpawner>().SpawnedPlayer.GetComponent<PlayerHealth>().AddMaxHealth(1000000);
    }
    private static void FreezeCamera(Vector2 center,float size=9)
    {
        var follow=Camera.main.GetComponentInParent<CameraFollow>(); if(follow!=null) follow.enabled=false;
        var shake=Camera.main.GetComponent<CameraShake>(); if(shake!=null) shake.enabled=false;
        Camera.main.transform.rotation=Quaternion.identity;
        Camera.main.transform.position=new Vector3(center.x,center.y,-10); Camera.main.orthographicSize=size;
    }
    private static IEnumerator Capture(string name)
    {
        float captureAt=Time.realtimeSinceStartup+.1f;
        while(Time.realtimeSinceStartup<captureAt) yield return null;
        string path=Folder+name+".png"; Directory.CreateDirectory(Folder);
        if(File.Exists(path)) File.Delete(path);
        ScreenCapture.CaptureScreenshot(path);
        yield return CoreTestSupport.Await(()=>File.Exists(path));
    }
    private static IEnumerator Wait(float seconds)
    {
        float deadline=Time.realtimeSinceStartup+seconds;
        while(Time.realtimeSinceStartup<deadline) yield return null;
    }
    private static void AlignModule(FalseSignalPoint point,Transform player,OrbitalModuleRuntime module,Vector2 shift=default)
    {
        FalseSignalTestSupport.Move(player,(Vector2)player.position+(Vector2)point.transform.position-module.WorldPosition-shift);
    }
    [UnityTest,Timeout(90000)] public IEnumerator SafeDiagnosticsTrapAndProductionReward()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
        yield return new EnterPlayMode();
        yield return ExerciseSafeDiagnosticsTrapAndProductionReward();
    }
    private static IEnumerator ExerciseSafeDiagnosticsTrapAndProductionReward()
    {
        yield return BeginProductionRun();
        LocalizationService.Instance.SetLanguage(GameLanguage.Russian);
        var player=Object.FindFirstObjectByType<CharacterSpawner>().SpawnedPlayer.transform;
        player.GetComponent<CharacterMovement2D>().MovementIntent=()=>Vector2.zero;
        var station=player.GetComponentInChildren<OrbitalStationRuntime>(); Assert.That(station,Is.Not.Null,"Player ORBITAL"); station.DebugRotationPaused=true;
        var events=Object.FindFirstObjectByType<WorldEventSpawner>(); Assert.That(events,Is.Not.Null,"Scene event spawner"); events.ConfigureSiteControlledMode(32);
        var enemies=Object.FindFirstObjectByType<EnemySpawner>(); Assert.That(enemies,Is.Not.Null,"Scene enemy spawner");
        enemies.ConfigureDebugExplorationPressure(new[]{AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/prefabs/Enemies/p_Enemy_default.prefab")},999,32,1);
        enemies.ClearDebugSpawnedEnemies();
        var game=Spawn(events,new Vector2(-20,-20));
        Assert.That(game,Is.Not.Null);
        FalseSignalTestSupport.Move(player,game.transform.position); game.Interact(); Assert.That(game.IsStarted && !game.IsCompleted,Is.True,"False Signal remains active after start.");
        Assert.That(game.IsStarted,Is.True);
        var points=game.GetComponentsInChildren<FalseSignalPoint>(); Assert.That(points,Has.Length.EqualTo(3));
        Assert.That(points.Count(p=>Read<bool>(p,"isReal")),Is.EqualTo(1));
        var real=points.Single(p=>Read<bool>(p,"isReal")); var falsePoints=points.Where(p=>p!=real).ToArray();
        var origin=(Vector2)game.transform.position; FreezeCamera(origin);
        yield return Capture("before-check");
        int count=enemies.DebugTrackedEnemyCount;
        // A body walking into the device and pressing E cannot select an unchecked source.
        FalseSignalTestSupport.Move(player,real.transform.position); real.Interact(); yield return Wait(.15f);
        Assert.That(real.State,Is.EqualTo(FalseSignalPointState.Unchecked)); Assert.That(game.IsCompleted,Is.False);
        // A single mounted weapon contact suffices; move the body away during the diagnostic.
        AlignModule(falsePoints[0],player,station.Modules[0]); yield return CoreTestSupport.Await(()=>falsePoints[0].State==FalseSignalPointState.Scanning);
        FalseSignalTestSupport.Move(player,origin); falsePoints[0].Interact();
        yield return Wait(.65f); yield return Capture("false-diagnostic");
        yield return CoreTestSupport.Await(()=>falsePoints[0].State==FalseSignalPointState.Verified);
        Assert.That(enemies.DebugTrackedEnemyCount,Is.EqualTo(count),"Scanning is safe.");
        AlignModule(real,player,station.Modules[0]); yield return CoreTestSupport.Await(()=>real.State==FalseSignalPointState.Scanning);
        FalseSignalTestSupport.Move(player,origin); yield return Wait(1.25f); yield return Capture("real-echo");
        yield return CoreTestSupport.Await(()=>real.State==FalseSignalPointState.Verified);
        yield return Capture("diagnostic-results");
        // Repeated weapon contacts preserve the visual memory instead of starting another scan.
        AlignModule(real,player,station.Modules[0]); yield return Wait(.15f);
        Assert.That(real.State,Is.EqualTo(FalseSignalPointState.Verified));
        FalseSignalTestSupport.Move(player,falsePoints[0].transform.position);
        var interactor=player.GetComponent<PlayerInteractor>(); Call(interactor,"FindInteractable");
        Assert.That(interactor.GetCurrentInteractable(),Is.SameAs(falsePoints[0]));
        interactor.GetCurrentInteractable().Interact(); falsePoints[0].Interact();
        Assert.That(falsePoints[0].State,Is.EqualTo(FalseSignalPointState.Warning));
        Assert.That(enemies.DebugTrackedEnemyCount,Is.EqualTo(count),"Warning precedes enemy spawning.");
        yield return CoreTestSupport.Await(()=>falsePoints[0].State==FalseSignalPointState.Disabled);
        int afterWave=enemies.DebugTrackedEnemyCount;
        Assert.That(afterWave-count,Is.InRange(1,3));
        falsePoints[0].Interact(); yield return Wait(.8f);
        Assert.That(enemies.DebugTrackedEnemyCount,Is.LessThanOrEqualTo(afterWave));
        Assert.That(real.CanInteract,Is.False,"The selected source still requires a nearby character.");
        // Existing site callback requests and commits the normal anomaly reward.
        var site=new GameObject("Production reward owner").AddComponent<ProductionAnomalySite>();
        Set(site,"initialized",true); Set(site,"activeEvent",game); Set(site,"eventSpawner",events);
        int completed=0; events.EventCompleted+=result=>{if(result==game){completed++;Call(site,"HandleEventCompleted",result);}};
        FalseSignalTestSupport.Move(player,real.transform.position); Call(interactor,"FindInteractable");
        Assert.That(interactor.GetCurrentInteractable(),Is.SameAs(real));
        interactor.GetCurrentInteractable().Interact(); real.Interact();
        yield return CoreTestSupport.Await(()=>completed==1);
        Assert.That(site.CompletedMainEvents,Is.EqualTo(1));
        var rewards=UpgradeManager.Instance; Assert.That(rewards.IsChoosingUpgrade,Is.True);
        Assert.That(rewards.DebugSelectCurrentChoice(0),Is.True);
        if(station.RewardFlow.PendingReward.HasValue) Assert.That(station.RewardFlow.DebugChooseFirstValidTarget(),Is.True);
        yield return CoreTestSupport.Await(()=>rewards.IsRewardQueueIdle);
        Assert.That(site.IsCompleted,Is.True); Assert.That(completed,Is.EqualTo(1));
        Assert.That(Object.FindObjectsByType<FalseSignalPoint>(FindObjectsSortMode.None),Is.Empty);
    }
    [UnityTest,Timeout(120000)] public IEnumerator EveryOrbitalBodyAndShiftedCenterCanProbe()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
        yield return new EnterPlayMode();
        yield return ExerciseEveryOrbitalBodyAndShiftedCenterCanProbe();
    }
    private static IEnumerator ExerciseEveryOrbitalBodyAndShiftedCenterCanProbe()
    {
        yield return BeginProductionRun();
        var player=Object.FindFirstObjectByType<CharacterSpawner>().SpawnedPlayer.transform;
        player.GetComponent<CharacterMovement2D>().MovementIntent=()=>Vector2.zero;
        var station=player.GetComponentInChildren<OrbitalStationRuntime>(); station.ApplyReadabilityTestPreset(); station.DebugRotationPaused=true;
        var events=Object.FindFirstObjectByType<WorldEventSpawner>(); Assert.That(events,Is.Not.Null,"Scene event spawner"); events.ConfigureSiteControlledMode(32);
        var shift=station.GetComponent<OrbitalCenterShift>();
        foreach(var kind in Enum.GetValues(typeof(OrbitalModuleKind)).Cast<OrbitalModuleKind>())
        {
            var module=station.Modules.First(m=>m.Kind==kind);
            foreach(var item in station.Modules) item.SetRewardPresentationVisible(item==module);
            var game=Spawn(events,new Vector2(20,-20));
            FalseSignalTestSupport.Move(player,game.transform.position); game.Interact(); Assert.That(game.IsStarted && !game.IsCompleted,Is.True,"False Signal remains active after start."); Assert.That(game.IsStarted,Is.True);
            var point=game.GetComponentsInChildren<FalseSignalPoint>().First();
            // The same Advance path is called by the Shift+WASD directional filter. Actor position stays fixed.
            AlignModule(point,player,module,Vector2.right*2);
            Vector2 characterPosition=player.position;
            Call(shift,"Advance",Vector2.right,true,.5f);
            yield return CoreTestSupport.Await(()=>point.State==FalseSignalPointState.Scanning);
            Assert.That(Vector2.Distance(player.position,characterPosition),Is.LessThan(.05f));
            Assert.That(shift.Offset.magnitude,Is.GreaterThan(1),"Shifted station contacts the point before its offset returns.");
            Assert.That(point.CanInteract,Is.False); shift.ResetOffset();
            yield return CoreTestSupport.Await(()=>point.State==FalseSignalPointState.Verified);
            game.Cancel(); yield return null;
            Assert.That(game.IsFailed,Is.True);
            Assert.That(Object.FindObjectsByType<FalseSignalPoint>(FindObjectsSortMode.None),Is.Empty);
        }
    }
    [UnityTest,Timeout(90000)] public IEnumerator CancellationWarningDeathAndSceneCleanup()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
        yield return new EnterPlayMode();
        yield return ExerciseCancellationWarningDeathAndSceneCleanup();
    }
    private static IEnumerator ExerciseCancellationWarningDeathAndSceneCleanup()
    {
        yield return BeginProductionRun();
        var player=Object.FindFirstObjectByType<CharacterSpawner>().SpawnedPlayer.transform;
        player.GetComponent<CharacterMovement2D>().MovementIntent=()=>Vector2.zero;
        var station=player.GetComponentInChildren<OrbitalStationRuntime>(); Assert.That(station,Is.Not.Null,"Player ORBITAL"); station.DebugRotationPaused=true;
        var events=Object.FindFirstObjectByType<WorldEventSpawner>(); Assert.That(events,Is.Not.Null,"Scene event spawner"); events.ConfigureSiteControlledMode(32);
        var game=Spawn(events,new Vector2(20,20)); Assert.That(game,Is.Not.Null);
        FalseSignalTestSupport.Move(player,game.transform.position); game.Interact(); Assert.That(game.IsStarted && !game.IsCompleted,Is.True,"False Signal remains active after start.");
        var points=game.GetComponentsInChildren<FalseSignalPoint>(); Assert.That(points,Has.Length.EqualTo(3));
        var point=points.First(p=>!Read<bool>(p,"isReal"));
        AlignModule(point,player,station.Modules[0]); yield return CoreTestSupport.Await(()=>point.State==FalseSignalPointState.Verified);
        FalseSignalTestSupport.Move(player,point.transform.position); point.Interact();
        Assert.That(point.State,Is.EqualTo(FalseSignalPointState.Warning));
        int count=Object.FindFirstObjectByType<EnemySpawner>().DebugTrackedEnemyCount;
        game.Cancel(); yield return Wait(.8f);
        Assert.That(Object.FindObjectsByType<FalseSignalPoint>(FindObjectsSortMode.None),Is.Empty);
        Assert.That(Object.FindFirstObjectByType<EnemySpawner>().DebugTrackedEnemyCount,Is.LessThanOrEqualTo(count+1));
        game=Spawn(events,new Vector2(20,20)); FalseSignalTestSupport.Move(player,game.transform.position); game.Interact(); Assert.That(game.IsStarted && !game.IsCompleted,Is.True,"False Signal remains active after start.");
        var scene=game.gameObject.scene;
        UnityEngine.SceneManagement.SceneManager.SetActiveScene(UnityEngine.SceneManagement.SceneManager.CreateScene("Signal scene cleanup"));
        yield return UnityEngine.SceneManagement.SceneManager.UnloadSceneAsync(scene);
        Assert.That(Object.FindObjectsByType<FalseSignalPoint>(FindObjectsSortMode.None),Is.Empty);
        yield return BeginProductionRun();
        player=Object.FindFirstObjectByType<CharacterSpawner>().SpawnedPlayer.transform;
        events=Object.FindFirstObjectByType<WorldEventSpawner>(); events.ConfigureSiteControlledMode(32);
        game=Spawn(events,new Vector2(20,20)); FalseSignalTestSupport.Move(player,game.transform.position); game.Interact();
        var health=player.GetComponent<PlayerHealth>(); Set(health,"isInvulnerable",false);
        Assert.That(health.TakeDamage(health.MaxHealth*2,Vector2.zero),Is.True);
        Assert.That(health.IsDead,Is.True); Assert.That(game.IsFailed,Is.True);
        yield return null;
        Assert.That(Object.FindObjectsByType<FalseSignalPoint>(FindObjectsSortMode.None),Is.Empty);
    }
    [UnityTest,Timeout(90000)] public IEnumerator CorridorEntranceMatchesStartAndPreservesRouteRules()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
        yield return new EnterPlayMode();
        yield return ExerciseCorridorEntranceMatchesStartAndPreservesRouteRules();
    }
    private static IEnumerator ExerciseCorridorEntranceMatchesStartAndPreservesRouteRules()
    {
        yield return BeginProductionRun(); LocalizationService.Instance.SetLanguage(GameLanguage.Russian);
        var player=Object.FindFirstObjectByType<CharacterSpawner>().SpawnedPlayer.transform;
        player.GetComponent<CharacterMovement2D>().MovementIntent=()=>Vector2.zero;
        var events=Object.FindFirstObjectByType<WorldEventSpawner>(); Assert.That(events,Is.Not.Null,"Scene event spawner"); events.ConfigureSiteControlledMode(32);
        var prefab=AssetDatabase.LoadAssetAtPath<CorridorEvent>(CorridorMigrationAuthoring.PrefabPath);
        CorridorEvent game=null;
        foreach(var start in new[]{new Vector2(-30,-30),new Vector2(30,30),new Vector2(-30,30),new Vector2(30,-30),Vector2.zero})
            if(events.SpawnSiteEventAt(prefab,start,start,new Vector2(8,8),true,out var instance)){game=(CorridorEvent)instance;break;}
        Assert.That(game,Is.Not.Null);
        FalseSignalTestSupport.Move(player,game.transform.position+(Vector3)Vector2.left*3);
        game.Interact(); Assert.That(game.IsStarted,Is.False);
        Assert.That(game.GetComponentInChildren<TMPro.TMP_Text>(true).text,Is.EqualTo("ВОЙДИТЕ В КОРИДОР"));
        FreezeCamera(game.transform.position,7); yield return Capture("corridor-entrance");
        FalseSignalTestSupport.Move(player,game.transform.position); game.Interact(); Assert.That(game.IsStarted && !game.IsCompleted,Is.True,"False Signal remains active after start."); Assert.That(game.IsStarted,Is.True);
        var startView=game.GetComponentInChildren<CorridorStartView>(); Assert.That(startView.GetComponentInChildren<TMPro.TMP_Text>(true).gameObject.activeSelf,Is.False);
        Assert.That(game.GetComponent<CircleCollider2D>().enabled,Is.False);
        yield return Capture("corridor-start-pulse");
        CorridorMigrationTestSupport.Traverse(game,player.GetComponent<Rigidbody2D>());
        Assert.That(game.IsCompleted,Is.True); Assert.That(game.IsFailed,Is.False);
    }
}
#endif
