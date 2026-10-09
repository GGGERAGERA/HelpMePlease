#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Subject42.Combat.OrbitalStation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using Object=UnityEngine.Object;

public sealed class FalseSignalAvailabilityPlayTests
{
    const string SignalPath="Assets/_Project/prefabs/Environment/WorldEvents/FalseSignalEvent.prefab";
    [SetUp] public void Setup()=>CoreTestSupport.PreservePreferences();
    [UnityTearDown] public IEnumerator Cleanup()=>CoreTestSupport.CleanupPlayMode();
    static void Set(WorldEvent target,string field,object value)=>typeof(WorldEvent).GetField(field,BindingFlags.NonPublic|BindingFlags.Instance).SetValue(target,value);
    static T Call<T>(object target,string method,params object[] args)=>(T)target.GetType().GetMethod(method,BindingFlags.NonPublic|BindingFlags.Instance).Invoke(target,args);
    [UnityTest,Timeout(90000)] public IEnumerator MainRunSelectionOverridesAndStaleGuaranteeCannotRestoreSignal()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
        yield return new EnterPlayMode();
        yield return ExerciseProduction();
    }
    static IEnumerator ExerciseProduction()
    {
        yield return CoreTestSupport.LoadBunker();
        var meta=MetaProgressionManager.Instance;
        var map=AssetDatabase.LoadAssetAtPath<SurfaceMapDefinition>(SurfaceMapProductionAuthoring.MapPath);
        var catalog=AssetDatabase.LoadAssetAtPath<MissionCatalog>(MissionProductionAuthoring.CatalogPath);
        var saved=new MissionSaveState(); saved.missions.Add(new MissionProgressRecord{missionId="MISSION_SIGNAL_TRACE",state=MissionState.Active});
        var storage=new MissionServiceTests.Storage{json=JsonUtility.ToJson(saved)};
        var missions=new MissionService(catalog,map,storage);
        typeof(MetaProgressionManager).GetProperty("Missions").SetValue(meta,missions);
        var surface=new SurfaceMapService(map,new SurfaceMarkerTests.Storage()); surface.Content.RegisterSource(missions);
        typeof(MetaProgressionManager).GetProperty("SurfaceMap").SetValue(meta,surface);
        Assert.That(surface.TrySelect("D1"),Is.True);
        Assert.That(surface.TryBuildRunConfig(out var config),Is.True);
        Assert.That(config.Content.GuaranteedEvents,Is.Empty);
        var provider=Object.FindFirstObjectByType<MissionProvider>();
        Assert.That(provider.GetMission().MissionId,Is.EqualTo("MISSION_SIGNAL_TRACE"));
        provider.Interact(); yield return null;
        var panel=Object.FindFirstObjectByType<BunkerMissionPanel>();
        var body=(TMPro.TMP_Text)typeof(BunkerMissionPanel).GetField("body",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(panel);
        Assert.That(body.text,Does.Contain(LocalizationService.Instance.Get(SurfaceSectorContent.UnavailableStatusKey)));
        panel.Hide();
        RunSelectionManager.Instance.SelectCharacter(AssetDatabase.LoadAssetAtPath<CharacterData>("Assets/_Project/Data/Characters/01_Gera.asset"));
        var starter=Object.FindFirstObjectByType<BunkerRunStarter>(); starter.StartSurfaceRun(starter.transform);
        yield return CoreTestSupport.Await(()=>UnityEngine.SceneManagement.SceneManager.GetActiveScene().name=="MVP" &&
            Object.FindFirstObjectByType<CharacterSpawner>()?.SpawnedPlayer?.GetComponentInChildren<OrbitalStationRuntime>() is {IsInitialized:true} && !SceneTransitionOverlay.IsTransitioning);
        Assert.That(ProductionAnomalySite.ActiveSites.Count,Is.EqualTo(typeof(ProductionExplorationSectorController).GetField("TotalSiteCount",BindingFlags.Static|BindingFlags.NonPublic).GetRawConstantValue()));
        foreach(var site in ProductionAnomalySite.ActiveSites)
        {
            var active=typeof(ProductionAnomalySite).GetField("activeEvent",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(site);
            Assert.That(active,Is.Not.Null,"No empty mandatory site"); Assert.That(active,Is.Not.InstanceOf<FalseSignalEvent>());
        }
        var spawner=Object.FindFirstObjectByType<WorldEventSpawner>();
        var signal=AssetDatabase.LoadAssetAtPath<FalseSignalEvent>(SignalPath);
        Assert.That(spawner.IsEventPrefabEnabled(signal),Is.False);
        Assert.That(spawner.SpawnSiteEventAt(signal,Vector3.zero,Vector2.zero,Vector2.one*24,true,out _),Is.False);
        Assert.That(spawner.SpawnDebugEventAt(signal,Vector3.zero,true,out _),Is.False,"Production debug overrides cannot restore unavailable content.");
        var run=RunStateManager.Instance;
        var pool=spawner.EventPrefabs.Where(p=>p.AllowedInSite && spawner.IsEventPrefabEnabled(p)).ToArray(); Assert.That(pool,Is.Not.Empty);
        var parameters=new RunConfigParameters{eventWeights=spawner.EventPrefabs.Select(p=>new RunPrefabWeight{prefab=p,multiplier=p==signal?100000:1}).ToArray()};
        var overridden=new RunConfig(map.Id,"D1",parameters);
        typeof(RunStateManager).GetProperty("CurrentConfig").SetValue(run,overridden);
        for(int i=0;i<12;i++)
        {
            var selected=Call<WorldEvent>(spawner,"GetNextEventPrefab");
            Assert.That(selected,Is.Not.Null); Assert.That(selected.AvailableInProduction,Is.True);
        }
        foreach(var weight in parameters.eventWeights)if(weight.prefab!=signal)weight.multiplier=0;
        overridden=new RunConfig(map.Id,"D1",parameters);
        typeof(RunStateManager).GetProperty("CurrentConfig").SetValue(run,overridden);
        var sector=Object.FindFirstObjectByType<ProductionExplorationSectorController>();
        var choices=Call<List<WorldEvent>>(sector,"BuildSiteEventPool",new Vector2[]{Vector2.one*20,Vector2.one*20,Vector2.one*20});
        Assert.That(choices,Is.Not.Empty); Assert.That(choices.All(p=>p.AvailableInProduction),Is.True);
        // Simulate an immutable run snapshot resolved while the event was still available.
        var copy=Object.Instantiate(signal); copy.gameObject.SetActive(false); Set(copy,"availableInProduction",true);
        var content=Object.Instantiate(catalog.Missions[0].Objectives[0].RunContent); content.requiredEvent=copy;
        content.experienceMultiplier=1.4f; content.goldMultiplier=1.7f; content.spawnPressureMultiplier=1.6f; content.threatGrowthMultiplier=0;
        var stale=RunContentResolver.Resolve(overridden,new[]{("old-save",content)});
        Assert.That(stale.Content.GuaranteedEvents.Count,Is.EqualTo(1)); Set(copy,"availableInProduction",false);
        var validated=stale.ValidateContentAvailability();
        Assert.That(validated.Content.GuaranteedEvents,Is.Empty); Assert.That(validated.Content.Objectives,Is.Empty);
        Assert.That(validated.Gold,Is.EqualTo(overridden.Gold)); Assert.That(validated.Experience,Is.EqualTo(overridden.Experience));
        Assert.That(validated.ThreatGrowth,Is.EqualTo(overridden.ThreatGrowth)); Assert.That(validated.SpawnPressure,Is.EqualTo(overridden.SpawnPressure));
        typeof(RunStateManager).GetProperty("CurrentConfig").SetValue(run,stale);
        choices=Call<List<WorldEvent>>(sector,"BuildSiteEventPool",new Vector2[]{Vector2.one*20,Vector2.one*20,Vector2.one*20});
        Assert.That(choices,Is.Not.Empty); Assert.That(choices.All(p=>p.AvailableInProduction),Is.True);
        typeof(RunStateManager).GetProperty("CurrentConfig").SetValue(run,RunConfig.Default);
        for(int i=0;i<12;i++)Assert.That(Call<WorldEvent>(spawner,"GetNextEventPrefab"),Is.Not.InstanceOf<FalseSignalEvent>());
        Assert.That(Object.FindObjectsByType<FalseSignalEvent>(FindObjectsSortMode.None),Is.Empty);
        Assert.That(missions.GetState("MISSION_SIGNAL_TRACE"),Is.EqualTo(MissionState.Active));
        Object.Destroy(copy.gameObject); Object.Destroy(content);
        typeof(RunStateManager).GetProperty("CurrentConfig").SetValue(run,config);
    }
    [UnityTest,Timeout(90000)] public IEnumerator WorldSystemsLabStillStartsFalseSignal()
    {
        EditorSceneManager.OpenScene(WorldSystemsLabController.ScenePath);
        yield return new EnterPlayMode();
        yield return ExerciseLab();
    }
    static IEnumerator ExerciseLab()
    {
        var lab=Object.FindFirstObjectByType<WorldSystemsLabController>();
        var signal=AssetDatabase.LoadAssetAtPath<FalseSignalEvent>(SignalPath);
        Assert.That(signal.AvailableInProduction,Is.False);
        Assert.That(lab.SpawnEvent(signal),Is.True);
        var game=Object.FindFirstObjectByType<FalseSignalEvent>();
        FalseSignalTestSupport.Move(lab.Player,game.transform.position); game.Interact();
        Assert.That(game.IsStarted,Is.True); Assert.That(game.GetComponentsInChildren<FalseSignalPoint>(),Has.Length.EqualTo(3));
        var station=lab.Player.GetComponentInChildren<OrbitalStationRuntime>(); station.DebugRotationPaused=true;
        var point=game.GetComponentsInChildren<FalseSignalPoint>()[0];
        FalseSignalTestSupport.Move(lab.Player,(Vector2)lab.Player.position+(Vector2)point.transform.position-station.Modules[0].WorldPosition);
        yield return CoreTestSupport.Await(()=>point.State==FalseSignalPointState.Verified);
        Assert.That(lab.Events.IsEventPrefabEnabled(signal),Is.False,"Preview must not change production availability.");
        lab.ClearEvents(); yield return null;
        Assert.That(Object.FindObjectsByType<FalseSignalPoint>(FindObjectsSortMode.None),Is.Empty);
    }
}
#endif
