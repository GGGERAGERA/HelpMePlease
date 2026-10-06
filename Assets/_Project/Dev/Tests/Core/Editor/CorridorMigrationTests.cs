#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

public static class CorridorMigrationTestSupport
{
    public static void Set(object instance,string field,object value) =>
        instance.GetType().GetField(field,BindingFlags.Instance|BindingFlags.NonPublic).SetValue(instance,value);
    public static void Call(object instance,string method,params object[] args) =>
        instance.GetType().GetMethod(method,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(instance,args);
    public static void Move(CorridorEvent game,Rigidbody2D body,Vector2 point)
    { body.position=point; body.transform.position=point; Physics2D.SyncTransforms(); Call(game,"Tick",.01f); }
    // Test-controlled physical samples exercise the same ordered crossing path, never setters.
    public static void Traverse(CorridorEvent game,Rigidbody2D body)
    { DriveToFinalPush(game,body); Finish(game,body); }
    public static void DriveToFinalPush(CorridorEvent game,Rigidbody2D body)
    {
        var route=game.Route;
        Move(game,body,route.Vertices[0]);
        for(float distance=1;distance<route.Length-2;distance+=1) Move(game,body,route.Sample(distance));
        Assert.That(game.State.Completed,Is.EqualTo(3));
    }
    public static void Finish(CorridorEvent game,Rigidbody2D body)
    {
        var route=game.Route;
        Call(game,"Tick",game.Settings.finalPushDuration+.02f);
        Assert.That(game.ExitReady,Is.True);
        Vector2 exit=route.Point(route.Length,out var direction);
        Move(game,body,exit-direction); Move(game,body,exit+direction);
        Call(game,"Tick",.5f);
        Assert.That(Vector2.Distance(body.position,exit+direction),Is.LessThan(.01f),"Completion itself preserves the physical Exit position.");
    }
    public static IEnumerator Capture(string name)
    {
        yield return new WaitForSecondsRealtime(1f); // Let the production following camera settle.
        string path="Artifacts/GeneratedQA/CorridorMigration/"+name+".png";
        if(File.Exists(path)) File.Delete(path);
        ScreenCapture.CaptureScreenshot(path);
        float deadline=Time.realtimeSinceStartup+8;
        while(!File.Exists(path)&&Time.realtimeSinceStartup<deadline) yield return null;
        Assert.That(File.Exists(path),Is.True);
    }
}

public sealed class CorridorSiteLifecycleTests
{
    [SetUp] public void Setup()=>CoreTestSupport.PreservePreferences();
    [UnityTearDown] public IEnumerator Cleanup()=>CoreTestSupport.CleanupPlayMode();
    [UnityTest,Timeout(90000)] public IEnumerator ProductionSitePreservesEnvironmentAndRewardsOnce()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
        yield return new EnterPlayMode();
        Assert.That(Application.isPlaying,Is.True);
        yield return VerifyProductionSite();
    }
    private static IEnumerator VerifyProductionSite()
    {
        yield return CoreTestSupport.LoadBunker();
        Debug.Log("[Corridor QA] Bunker loaded");
        RunSelectionManager.Instance.SelectCharacter(AssetDatabase.LoadAssetAtPath<CharacterData>("Assets/_Project/Data/Characters/01_Gera.asset"));
        var starter=Object.FindFirstObjectByType<BunkerRunStarter>(); starter.StartRun(starter.transform);
        Debug.Log("[Corridor QA] run requested");
        yield return CoreTestSupport.Await(()=>UnityEngine.SceneManagement.SceneManager.GetActiveScene().name=="MVP"&&!SceneTransitionOverlay.IsTransitioning&&
            Object.FindFirstObjectByType<Subject42.Combat.OrbitalStation.OrbitalStationRuntime>() is { IsInitialized:true } &&
            Object.FindFirstObjectByType<CharacterSpawner>()?.SpawnedPlayer != null);
        Debug.Log("[Corridor QA] production await finished");
        var player=Object.FindFirstObjectByType<CharacterSpawner>().SpawnedPlayer;
        var body=player.GetComponent<Rigidbody2D>();
        var movement=player.GetComponent<CharacterMovement2D>();
        Debug.Log($"[Corridor QA] production actor={player}, body={body}, movement={movement}");
        Assert.That(body,Is.Not.Null); Assert.That(movement,Is.Not.Null);
        movement.MovementIntent=()=>Vector2.zero;
        var spawner=Object.FindFirstObjectByType<WorldEventSpawner>();
        var prefab=AssetDatabase.LoadAssetAtPath<CorridorEvent>(CorridorMigrationAuthoring.PrefabPath);
        Debug.Log($"[Corridor QA] spawner={spawner}, prefab={prefab}");
        Assert.That(spawner,Is.Not.Null); Assert.That(spawner.EventPrefabs,Is.Not.Null);
        Assert.That(prefab,Is.Not.Null); Assert.That(prefab.Config,Is.Not.Null);
        Debug.Log("[Corridor QA] production assets valid");
        Assert.That(spawner.EventPrefabs.Any(item=>item==prefab),Is.True,"Production catalog resolves migrated component.");
        spawner.ConfigureDebugConcurrentEventCapacity(16);
        Debug.Log("[Corridor QA] production capacity set");
        CorridorEvent game=null;
        Debug.Log("[Corridor QA] production pool ready");
        // Small bounded admission search in the real authored production scene; no arena/props changes.
        foreach(var start in new[]{new Vector2(-30,-30),new Vector2(30,30),new Vector2(-30,30),new Vector2(30,-30),Vector2.zero})
        {
            if(!spawner.SpawnSiteEventAt(prefab,start,start,new Vector2(8,8),true,out var instance)) continue;
            game=(CorridorEvent)instance; break;
        }
        Assert.That(game,Is.Not.Null,"Production whole-route admission must find a valid offer.");
        // Use actual authored anomaly data without relying on one config path.
        var anomaly=AssetDatabase.FindAssets("t:LocalAnomalyData").Select(guid=>AssetDatabase.LoadAssetAtPath<LocalAnomalyData>(AssetDatabase.GUIDToAssetPath(guid)))
            .First(data=>data.AnomalyType==LocalAnomalyType.Stasis);
        var controller=Object.FindFirstObjectByType<LevelAnomalyController>();
        Vector2 zonePosition=game.Route.Sample(15);
        var zone=controller.SpawnSiteZone(anomaly,zonePosition,new Vector2(10,10));
        Assert.That(zone,Is.Not.Null);
        var siteRoot=new GameObject("Corridor owner site fixture"); var site=siteRoot.AddComponent<ProductionAnomalySite>();
        CorridorMigrationTestSupport.Set(site,"initialized",true);
        CorridorMigrationTestSupport.Set(site,"anomalyController",controller);
        CorridorMigrationTestSupport.Set(site,"anomalyZone",zone);
        CorridorMigrationTestSupport.Set(site,"activeEvent",game);
        CorridorMigrationTestSupport.Set(site,"eventSpawner",spawner);
        spawner.EventCompleted += completed=>CorridorMigrationTestSupport.Call(site,"HandleEventCompleted",completed);
        int notifications=0; spawner.EventCompleted += completed=>{if(completed==game)notifications++;};
        body.position=game.Route.Vertices[0]; player.transform.position=body.position; Physics2D.SyncTransforms();
        Assert.That(spawner.TryStartProductionEvent(game),Is.True);
        Assert.That(game.IsStarted,Is.True); yield return null;
        yield return CorridorMigrationTestSupport.Capture("production-smoke");
        CorridorMigrationTestSupport.DriveToFinalPush(game,body);
        yield return CorridorMigrationTestSupport.Capture("production-exit-locked");
        Assert.That(game.ExitReady,Is.False);
        CorridorMigrationTestSupport.Call(game,"Tick",game.Settings.finalPushDuration+.02f);
        yield return CorridorMigrationTestSupport.Capture("production-exit-open");
        CorridorMigrationTestSupport.Finish(game,body);
        Vector2 final=body.position;
        Assert.That(notifications,Is.EqualTo(1));
        var rewards=UpgradeManager.Instance;
        Assert.That(rewards.IsChoosingUpgrade,Is.True,"Existing normal-site choices are offered.");
        CorridorMigrationTestSupport.Call(site,"HandleEventCompleted",game);
        var queue=(ICollection)typeof(UpgradeManager).GetField("pendingChoices",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(rewards);
        Assert.That(queue.Count,Is.Zero,"Duplicate completion cannot enqueue another reward.");
        int index=rewards.DebugCurrentChoices.Select((reward,i)=>(reward,i))
            .Where(pair=>pair.reward is Subject42.Combat.OrbitalStation.OrbitalRewardData data&&!data.RequiresArenaSelection).Select(pair=>pair.i).DefaultIfEmpty(-1).First();
        if(index<0) index=0; // Authored choices may all require a valid station target.
        int committed=0; rewards.DebugRewardCommitted += _=>committed++;
        Assert.That(rewards.DebugSelectCurrentChoice(index),Is.True);
        var station=player.GetComponentInChildren<Subject42.Combat.OrbitalStation.OrbitalStationRuntime>();
        if(station.RewardFlow.PendingReward.HasValue)
            Assert.That(station.RewardFlow.DebugChooseFirstValidTarget(),Is.True);
        yield return null;
        Assert.That(committed,Is.EqualTo(1),"One existing reward is actually committed.");
        Assert.That(site.IsCompleted,Is.True,"Objective settles through existing reward callback.");
        Assert.That(site.CompletedMainEvents,Is.EqualTo(1));
        Assert.That(site.EnvironmentAlive,Is.True); Assert.That(zone!=null&&zone.FocusArea.enabled,Is.True);
        Assert.That(Vector2.Distance(body.position,final),Is.LessThan(.5f),"Player remains at Exit after reward resumes normal physics.");
        site.RemoveForLayout();
        // Existing anomaly owner fades its presentation before destruction.
        yield return CoreTestSupport.Await(()=>zone==null||!zone.gameObject.activeInHierarchy);
        Assert.That(zone==null||!zone.gameObject.activeInHierarchy,Is.True,"Only environment owner reset retires zone.");
    }
}

public sealed class CorridorProductionIntegrationTests
{
    [Test] public void CatalogAssetsNoDevAndNoLegacyReferences()
    {
        string path=CorridorMigrationAuthoring.PrefabPath;
        var prefab=AssetDatabase.LoadAssetAtPath<CorridorEvent>(path);
        Assert.That(prefab,Is.Not.Null); Assert.That(prefab.TryValidateConfiguration(out var error),Is.True,error);
        Assert.That(AssetDatabase.AssetPathToGUID(path),Is.EqualTo("e47c3f95a8a34c18a619b70ec785f142"));
        foreach(string dependency in AssetDatabase.GetDependencies(path,true))
            Assert.That(dependency,Does.Not.Contain("/_Project/Dev/"),dependency);
        foreach(var scene in new[]{"Assets/_Project/Scenes/MainBuild/MVP.unity","Assets/_Project/Data/SurfaceMap/Sector_D1.asset","Assets/_Project/Data/SurfaceMap/Sector_D2.asset"})
            Assert.That(File.ReadAllText(scene),Does.Contain("e47c3f95a8a34c18a619b70ec785f142"));
        foreach(string file in Directory.GetFiles("Assets/_Project","*",SearchOption.AllDirectories).Where(file=>new[]{".cs",".prefab",".unity",".asset",".mat"}.Contains(Path.GetExtension(file))))
        {
            string content=File.ReadAllText(file);
            Assert.That(content,Does.Not.Contain("guid: "+"e47c3f95a8a34c18a619b70ec785f141"),file);
            Assert.That(content,Does.Not.Contain("guid: "+"60a86a73f1bb434fa6c0ba457b9b3e33"),file);
            Assert.That(content,Does.Not.Contain("guid: "+"57cbf95ee3934c25ac1e09b2c11db741"),file);
        }
        Assert.That(prefab.Config.kit.gate.GetComponentsInChildren<TMPro.TMP_Text>(true),Is.Empty);
    }
}
#endif
