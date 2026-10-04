#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Subject42.Combat.OrbitalStation;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object=UnityEngine.Object;

public sealed class MissionProductionFlowTests
{
    [Serializable] sealed class Preference { public string key,text; public bool exists,integer; public int number; }
    [Serializable] sealed class Backup { public List<Preference> values=new(); }
    const string BackupKey="Subject42.MissionFlow.Preferences";
    static T Read<T>(Object target,string field)=>(T)target.GetType().GetField(field,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(target);
    [SetUp] public void Preserve()
    {
        CoreTestSupport.PreservePreferences();
        var map=AssetDatabase.LoadAssetAtPath<SurfaceMapDefinition>(SurfaceMapProductionAuthoring.MapPath);
        var mission=AssetDatabase.LoadAssetAtPath<MissionDefinition>(MissionProductionAuthoring.MissionPath);
        var storage=new PlayerPrefsMissionStorage(map.Id); var saved=new Backup();
        foreach(string key in new[]{storage.Key,PlayerPrefsSurfaceMapStorage.KeyPrefix+map.Id,PlayerPrefsSurfaceMapStorage.KeyPrefix+map.Id+":content",CurrencyManager.GoldRewardReceiptPrefix+storage.ReceiptId(mission.MissionId)})
        {
            bool integer=key.StartsWith(CurrencyManager.GoldRewardReceiptPrefix,StringComparison.Ordinal);
            saved.values.Add(new Preference{key=key,exists=PlayerPrefs.HasKey(key),integer=integer,text=integer?null:PlayerPrefs.GetString(key),number=integer?PlayerPrefs.GetInt(key):0});
            PlayerPrefs.DeleteKey(key);
        }
        SessionState.SetString(BackupKey,JsonUtility.ToJson(saved)); PlayerPrefs.Save();
    }
    [UnityTearDown] public IEnumerator Cleanup()
    {
        var cleanup=CoreTestSupport.CleanupPlayMode(); while(cleanup.MoveNext())yield return cleanup.Current;
        RestoreExtraPreferences();
    }
    public static void RestoreExtraPreferences()
    {
        string backup=SessionState.GetString(BackupKey,"");
        if(!string.IsNullOrEmpty(backup))foreach(var item in JsonUtility.FromJson<Backup>(backup).values)
        { if(!item.exists)PlayerPrefs.DeleteKey(item.key); else if(item.integer)PlayerPrefs.SetInt(item.key,item.number); else PlayerPrefs.SetString(item.key,item.text); }
        PlayerPrefs.Save(); SessionState.EraseString(BackupKey);
    }
    [UnityTest] public IEnumerator OperatorToEventToRealBossVictoryToTurnInAndReload()
    {
        EditorSceneManager.OpenScene("Assets/_Project/Scenes/MainBuild/StartScreen.unity");
        yield return new EnterPlayMode(); yield return Exercise();
    }
    static IEnumerator Exercise()
    {
        Object.FindFirstObjectByType<StartScreenController>().Begin();
        yield return CoreTestSupport.Await(()=>SceneManager.GetActiveScene().name==RunEndService.BunkerSceneName&&!SceneTransitionOverlay.IsTransitioning&&Object.FindFirstObjectByType<BunkerPlayerLoadoutController>() is { IsReady:true });
        var meta=MetaProgressionManager.Instance; var map=meta.SurfaceMap; var missions=meta.Missions;
        var provider=Object.FindObjectsByType<MissionProvider>(FindObjectsSortMode.None).Single(); var definition=provider.GetMission(); string id=definition.MissionId;
        Assert.That(missions.GetState(id),Is.EqualTo(MissionState.Available));
        var player=Read<Transform>(Object.FindFirstObjectByType<BunkerPlayerLoadoutController>(),"controlledPlayerRoot");
        player.position=provider.transform.position+new Vector3(20,-20); Physics2D.SyncTransforms();
        Assert.That(provider.CanInteract,Is.True,"Operator hover and click must be available at a distance like other bunker stations.");
        player.position=provider.transform.position+new Vector3(0,-1); Physics2D.SyncTransforms();
        Assert.That(provider.CanInteract,Is.True);
        var arrow=provider.transform.Find("PF_InteractionArrow");
        Assert.That(arrow,Is.Not.Null,"Operator must reuse the standard interaction arrow prefab.");
        Assert.That(arrow.gameObject.activeSelf,Is.True,"Available mission should show arrow.");
        var hover=provider.GetComponent<BunkerHoverOutline>(); hover.SetHovered(true);
        var stationMaterial=provider.GetComponent<SpriteRenderer>().material;
        Assert.That(stationMaterial.HasProperty("_EnableOutline"),Is.True);
        Assert.That(stationMaterial.GetFloat("_EnableOutline"),Is.EqualTo(1f));
        Assert.That(stationMaterial.GetColor("_OutlineColor"),Is.EqualTo(Color.white));
        hover.SetHovered(false); Assert.That(stationMaterial.GetFloat("_EnableOutline"),Is.Zero);
        Assert.That(Physics2D.OverlapPoint(provider.transform.position,1<<provider.gameObject.layer).GetComponent<BunkerInteractableCollider>().Interactable,Is.SameAs(provider));
        provider.Interact();
        var panel=Object.FindFirstObjectByType<BunkerMissionPanel>(); Assert.That(panel.IsOpen,Is.True);
        Read<Button>(panel,"actionButton").onClick.Invoke(); Assert.That(missions.GetState(id),Is.EqualTo(MissionState.Active)); Assert.That(arrow.gameObject.activeSelf,Is.False,"Active mission alone must not show arrow.");
        Read<Button>(panel,"leaveButton").onClick.Invoke(); Assert.That(panel.IsOpen,Is.False);
        map.Content.TryGetMarker(definition.TargetSectorId,out var marker); Assert.That(marker.Marker.icon,Is.EqualTo("!"));
        var exit=Object.FindObjectsByType<BunkerStation>(FindObjectsSortMode.None).First(s=>Read<BunkerStationType>(s,"stationType")==BunkerStationType.StartRun);
        exit.Interact(); map.TrySelect(definition.TargetSectorId); yield return null;
        var view=Object.FindFirstObjectByType<SurfaceMapView>(); Assert.That(Read<TMP_Text>(view,"details").text,Does.Contain("ACTIVE MISSION").And.Contain("100 GOLD").And.Contain("Investigate False Signal"));
        ScreenCapture.CaptureScreenshot("Artifacts/Missions/mission-map.png"); yield return null;
        Read<Button>(view,"startButton").onClick.Invoke();
        yield return CoreTestSupport.Await(()=>SceneManager.GetActiveScene().name=="MVP"&&!SceneTransitionOverlay.IsTransitioning&&Object.FindFirstObjectByType<OrbitalStationRuntime>() is { IsInitialized:true });
        var run=RunStateManager.Instance; var config=run.CurrentConfig;
        var runPlayer=Object.FindFirstObjectByType<CharacterSpawner>().SpawnedPlayer; runPlayer.GetComponent<PlayerHealth>().AddMaxHealth(1000000);
        var required=config.Content.GuaranteedEvents.Single();
        var site=ProductionAnomalySite.ActiveSites.Where(s=>!s.IsSpecial&&Read<WorldEvent>(s,"eventPrefab")==required).OrderByDescending(s=>s.SiteSize.x*s.SiteSize.y).First();
        var worldEvent=(FalseSignalEvent)Read<WorldEvent>(site,"activeEvent"); Assert.That(worldEvent.SourcePrefab,Is.SameAs(required));
        runPlayer.transform.position=worldEvent.transform.position; worldEvent.Interact();
        yield return CoreTestSupport.Await(()=>worldEvent.IsStarted);
        var real=Object.FindObjectsByType<FalseSignalPoint>(FindObjectsSortMode.None).First(p=>Read<bool>(p,"isReal"));
        worldEvent.ResolveSignal(real,true); Assert.That(worldEvent.IsCompleted,Is.True); Assert.That(missions.GetState(id),Is.EqualTo(MissionState.ObjectiveCompleted));
        var storage=new PlayerPrefsMissionStorage(map.Definition.Id); var provisionalSave=JsonUtility.FromJson<MissionSaveState>(storage.Load()).missions.Single(m=>m.missionId==id);
        Assert.That(provisionalSave.state,Is.EqualTo(MissionState.Active)); Assert.That(provisionalSave.committedObjectives,Is.Empty); Assert.That(missions.Claim(id),Is.False);
        yield return CoreTestSupport.Await(()=>UpgradeManager.Instance.DebugCurrentChoices.Count>0);
        Assert.That(UpgradeManager.Instance.DebugSelectCurrentChoice(0),Is.True); yield return CoreTestSupport.Await(()=>UpgradeManager.Instance.IsRewardQueueIdle);
        // Shorten only idle route traversal; the production guardian and victory confirmation remain real.
        var profile=AssetDatabase.LoadAssetAtPath<StageProfileData>("Assets/_Project/Data/Stages/StageProfiles/StageProfile_10.asset");
        run.SetCurrentSector(new RunSector(RunRoute.FinalSector,profile,config.WorldRule,config.LocalAnomaly));
        var flow=RunFlowController.Instance; flow.InitializeSector(profile); typeof(RunFlowController).GetProperty("IsExitUnlocked").SetValue(flow,true);
        Assert.That(flow.HandleExitReached(),Is.True); yield return CoreTestSupport.Await(()=>flow.FinalBoss!=null&&flow.Phase==RunPhase.FinalBossCombat);
        flow.FinalBoss.TakeDamage(flow.FinalBoss.CurrentHealth*2,flow.FinalBoss.transform.position);
        yield return CoreTestSupport.Await(()=>SceneManager.GetActiveScene().name==RunEndService.BunkerSceneName&&!SceneTransitionOverlay.IsTransitioning&&Object.FindFirstObjectByType<BunkerPlayerLoadoutController>() is { IsReady:true });
        Assert.That(meta.Missions,Is.SameAs(missions)); Assert.That(missions.GetState(id),Is.EqualTo(MissionState.ReadyToTurnIn));
        var committed=JsonUtility.FromJson<MissionSaveState>(storage.Load()).missions.Single(m=>m.missionId==id); Assert.That(committed.committedObjectives.Count,Is.EqualTo(1));
        provider=Object.FindFirstObjectByType<MissionProvider>(); arrow=provider.transform.Find("PF_InteractionArrow"); Assert.That(arrow.gameObject.activeSelf,Is.True,"ReadyToTurnIn should show arrow."); player=Read<Transform>(Object.FindFirstObjectByType<BunkerPlayerLoadoutController>(),"controlledPlayerRoot");
        player.position=provider.transform.position+new Vector3(0,-1); Physics2D.SyncTransforms(); Assert.That(provider.CanInteract,Is.True,"Provider must work immediately after the production return"); provider.Interact(); panel=Object.FindFirstObjectByType<BunkerMissionPanel>();
        Assert.That(panel,Is.Not.Null);
        Assert.That(Read<TMP_Text>(panel,"body").text,Does.Contain("SIGNAL TRACE COMPLETE"));
        ScreenCapture.CaptureScreenshot("Artifacts/Missions/mission-turn-in.png"); yield return null;
        int before=CurrencyManager.Instance.TotalGold; Read<Button>(panel,"actionButton").onClick.Invoke();
        Assert.That(CurrencyManager.Instance.TotalGold-before,Is.EqualTo(definition.Reward.Gold)); Assert.That(missions.GetState(id),Is.EqualTo(MissionState.Completed));
        Assert.That(missions.Claim(id),Is.False); Assert.That(storage.HasClaimReceipt(id),Is.True); Assert.That(arrow.gameObject.activeSelf,Is.False,"Completed provider should have no arrow.");
        int after=CurrencyManager.Instance.TotalGold;
        var reloaded=new MissionService(missions.Catalog,map.Definition,new PlayerPrefsMissionStorage(map.Definition.Id));
        map.Content.UnregisterSource(missions); typeof(MetaProgressionManager).GetProperty("Missions").SetValue(meta,reloaded); map.Content.RegisterSource(reloaded);
        yield return SceneManager.LoadSceneAsync(RunEndService.BunkerSceneName);
        yield return CoreTestSupport.Await(()=>Object.FindFirstObjectByType<MissionProvider>()!=null);
        Assert.That(reloaded.GetState(id),Is.EqualTo(MissionState.Completed)); Assert.That(reloaded.IsRewardClaimed(id),Is.True); Assert.That(reloaded.Claim(id),Is.False);
        Assert.That(CurrencyManager.Instance.TotalGold,Is.EqualTo(after));
        map.Content.TryGetMarker(definition.TargetSectorId,out marker); Assert.That(marker.Marker.icon,Is.EqualTo("?")); Assert.That(marker.State,Is.EqualTo(SurfaceMarkerState.Revealed),"Underlying unknown record survives the mission overlay");
    }
}
#endif
