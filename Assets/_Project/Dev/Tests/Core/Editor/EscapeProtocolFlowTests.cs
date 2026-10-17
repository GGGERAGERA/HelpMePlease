#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
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
using Object = UnityEngine.Object;

public sealed class EscapeProtocolFlowTests
{
    const string BackupKey="Subject42.EscapeProtocolFlow.Backup";
    [Serializable] class SavedString {public string key,value;public bool exists;}
    [Serializable] class Backup {public List<SavedString> strings=new();public List<string> receipts=new();}
    static T Read<T>(Object owner,string field)=>(T)owner.GetType().GetField(field,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(owner);
    [SetUp] public void Preserve()
    {
        if(!string.IsNullOrEmpty(SessionState.GetString(BackupKey,"")))return;
        CoreTestSupport.PreservePreferences();
        var map=AssetDatabase.LoadAssetAtPath<SurfaceMapDefinition>("Assets/_Project/Data/SurfaceMap/SurfaceMap_MVP.asset");
        var backup=new Backup();foreach(string key in new[]{PlayerPrefsSurfaceMapStorage.KeyPrefix+map.Id,PlayerPrefsSurfaceMapStorage.KeyPrefix+map.Id+":content"})
            backup.strings.Add(new SavedString{key=key,exists=PlayerPrefs.HasKey(key),value=PlayerPrefs.GetString(key)});
        SessionState.SetString(BackupKey,JsonUtility.ToJson(backup));Directory.CreateDirectory(EscapeProtocolAuthoring.Output);
        File.WriteAllText(EscapeProtocolAuthoring.Output+"/preferences-original.json",SessionState.GetString("Subject42.Core.Preferences",""));
        File.WriteAllText(EscapeProtocolAuthoring.Output+"/map-original.json",JsonUtility.ToJson(backup));
        PlayerPrefs.DeleteKey(MetaProgressionManager.EscapeProtocolKey);PlayerPrefs.SetInt(MetaProgressionManager.EscapeAccessKey,0);
        PlayerPrefs.SetInt(CurrencyManager.CoresKey,0);PlayerPrefs.Save();
    }
    [UnityTearDown] public IEnumerator Cleanup()
    {
        var cleanup=CoreTestSupport.CleanupPlayMode();while(cleanup.MoveNext())yield return cleanup.Current;
        RestoreExtraPreferences();
    }
    public static void RestoreExtraPreferences()
    {
        string json=SessionState.GetString(BackupKey,"");if(string.IsNullOrEmpty(json))return;var backup=JsonUtility.FromJson<Backup>(json);
        foreach(var item in backup.strings)if(item.exists)PlayerPrefs.SetString(item.key,item.value);else PlayerPrefs.DeleteKey(item.key);
        foreach(var id in backup.receipts)PlayerPrefs.DeleteKey(CurrencyManager.CoreRewardReceiptPrefix+id);
        PlayerPrefs.Save();SessionState.EraseString(BackupKey);
    }
    static void Receipt(string id)
    {
        var backup=JsonUtility.FromJson<Backup>(SessionState.GetString(BackupKey,""));backup.receipts.Add(id);SessionState.SetString(BackupKey,JsonUtility.ToJson(backup));
    }
    static void Seed(int cores)
    {string id="qa_escape_flow_"+Guid.NewGuid().ToString("N");Receipt(id);Assert.That(CurrencyManager.Instance.TryGrantCoresOnce(id,cores,()=>{}),Is.True);}
    static IEnumerator Capture(string name)
    {
        Canvas.ForceUpdateCanvases();yield return null;
        ScreenCapture.CaptureScreenshot(EscapeProtocolAuthoring.Output+"/"+name+".png");yield return null;
    }
    static IEnumerator Ready(string scene)=>CoreTestSupport.Await(()=>SceneManager.GetActiveScene().name==scene&&!SceneTransitionOverlay.IsTransitioning&&
        (scene!="MVP"||Object.FindFirstObjectByType<OrbitalStationRuntime>() is {IsInitialized:true}));
    [UnityTest,Timeout(180000)] public IEnumerator TerminalFundingAndFinalVictoryAcrossSessions()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);yield return new EnterPlayMode();
        yield return ExerciseTerminalAndFinal();
        yield return new ExitPlayMode();EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);yield return new EnterPlayMode();
        yield return VerifyReloadAndNormalExpedition();
    }
    static IEnumerator ExerciseTerminalAndFinal()
    {
        yield return CoreTestSupport.LoadBunker();LocalizationService.Instance.SetLanguage(GameLanguage.Russian);
        var meta=MetaProgressionManager.Instance;var map=meta.SurfaceMap;var data=meta.EscapeProtocol;
        Assert.That(data,Is.Not.Null);Assert.That(meta.CurrentEscapeStage,Is.Zero);Assert.That(map.GetStatus("F1"),Is.EqualTo(SurfaceSectorStatus.Locked));
        var station=Object.FindObjectsByType<BunkerStation>(FindObjectsSortMode.None).Single(s=>new SerializedObject(s).FindProperty("stationType").intValue==(int)BunkerStationType.EscapeProtocol);
        Assert.That(station.CanInteract,Is.True);Seed(3);station.Interact();yield return null;
        var view=Object.FindFirstObjectByType<EscapeProtocolView>();Assert.That(Read<GameObject>(view,"projectWindow").activeInHierarchy,Is.True);
        Read<Button>(view,"investButton").onClick.Invoke();Assert.That(meta.GetEscapeInvestment(0),Is.EqualTo(3));Assert.That(CurrencyManager.Instance.TotalCores,Is.Zero);
        meta.ReloadFromStorage();Assert.That(meta.GetEscapeInvestment(0),Is.EqualTo(3));
        yield return Capture("escape-protocol-terminal");
        foreach(string field in new[]{"projectTitle","projectStages","projectProgress","projectNext","projectBalance","investLabel"})
        {var text=Read<TMP_Text>(view,field);text.ForceMeshUpdate();Assert.That(text.isTextOverflowing,Is.False,field);}
        var panels=Object.FindFirstObjectByType<BunkerPanelManager>();panels.CloseAll(false);yield return null;
        var hud=Object.FindFirstObjectByType<BunkerHudView>();Assert.That(Read<TMP_Text>(hud,"objectiveText").text,Does.Contain("3/5"));
        Seed(27);station.Interact();yield return null;
        while(!meta.EscapeProtocolFunded)
        {Read<Button>(view,"investButton").onClick.Invoke();yield return null;}
        Assert.That(CurrencyManager.Instance.TotalCores,Is.Zero);Assert.That(map.GetStatus("F1"),Is.EqualTo(SurfaceSectorStatus.Available));
        Assert.That(map.GetStatus("A1"),Is.Not.EqualTo(SurfaceSectorStatus.Locked));panels.CloseAll(false);
        var starter=Object.FindFirstObjectByType<BunkerRunStarter>();
        Assert.That(starter, Is.Not.Null);
        RunSelectionManager.Instance.SelectCharacter(AssetDatabase.LoadAssetAtPath<CharacterData>("Assets/_Project/Data/Characters/01_Gera.asset"));
        Assert.That(map.TrySelect("F1"),Is.True);map.TrySelectTask(null);starter.StartSurfaceRun(starter.transform);yield return Ready("MVP");
        var run=RunStateManager.Instance;
        Assert.That(run,Is.Not.Null,"Run service after scene transition");
        Assert.That(run.CurrentConfig,Is.Not.Null,"Run config after scene transition");
        Assert.That(run.CurrentConfig.IsEscapeFinal,Is.True);
        Assert.That(run.FinalObjectiveSatisfied,Is.False);
        Assert.That(run.IsDevelopmentRun,Is.False);
        Assert.That(run.Expedition,Is.Not.Null,"Final expedition reward context");
        Receipt(run.Expedition.Snapshot.RunToken+"/sector");Receipt(run.Expedition.Snapshot.RunToken+"/task");
        var characters=Object.FindFirstObjectByType<CharacterSpawner>();
        Assert.That(characters,Is.Not.Null,"Final character spawner");
        Assert.That(characters.SpawnedPlayer,Is.Not.Null,"Final spawned character");
        var health=characters.SpawnedPlayer.GetComponent<PlayerHealth>();health.AddMaxHealth(1000000);
        var profile=AssetDatabase.LoadAssetAtPath<StageProfileData>("Assets/_Project/Data/Stages/StageProfiles/StageProfile_10.asset");
        // Verify the final fight cannot start by reaching its exit before the Relay.
        var first=run.CurrentSector;run.SetCurrentSector(new RunSector(RunRoute.FinalSector,profile,run.CurrentConfig.WorldRule,run.CurrentConfig.LocalAnomaly));
        var flow=RunFlowController.Instance;
        Assert.That(flow,Is.Not.Null,"Final run flow");
        flow.InitializeSector(profile);typeof(RunFlowController).GetProperty("IsExitUnlocked").SetValue(flow,true);
        Assert.That(flow.HandleExitReached(),Is.False);run.SetCurrentSector(first);flow.InitializeSector(first.StageProfile);
        Object.FindFirstObjectByType<EnemySpawner>().StopSpawning();
        var relay=Object.FindObjectsByType<OrbitalRelayEvent>(FindObjectsSortMode.None).FirstOrDefault(e=>e.SourcePrefab==run.CurrentConfig.RequiredFinalEvent&&!e.IsCompleted);
        Assert.That(relay,Is.Not.Null,"The final route must really place its guaranteed authored Relay.");
        var movement=characters.SpawnedPlayer.GetComponent<CharacterMovement2D>();movement.transform.position=relay.transform.position;Physics2D.SyncTransforms();
        OrbitalRelayResult? result=null;relay.Finished+=value=>result=value;
        movement.MovementIntent=()=>relay==null?Vector2.zero:relay.IsStarted?OrbitalRelayBotSteering.GetDesiredMovement(relay,characters.Station,movement.transform.position):Vector2.ClampMagnitude(relay.transform.position-movement.transform.position,1f);
        float deadline=Time.realtimeSinceStartup+45f;
        while(!result.HasValue&&Time.realtimeSinceStartup<deadline)
        {
            if(relay!=null&&relay.CanInteract)relay.Interact();
            if(UpgradeManager.Instance.IsChoosingUpgrade)yield return RewardScenarioAssertions.ClickDisplayedCardAndVerifyGrant();
            yield return null;
        }
        movement.MovementIntent=()=>Vector2.zero;
        Assert.That(result.HasValue&&result.Value.Success,Is.True,"Relay must complete via real module/body contacts.");
        Assert.That(run.FinalObjectiveSatisfied,Is.True);
        while(!UpgradeManager.Instance.IsRewardQueueIdle)
        {if(UpgradeManager.Instance.IsChoosingUpgrade)yield return RewardScenarioAssertions.ClickDisplayedCardAndVerifyGrant();else yield return null;}
        while(run.CurrentSector.SectorNumber<RunRoute.FinalSector)
        {
            int nextSector=run.CurrentSector.SectorNumber+1;
            flow=RunFlowController.Instance;
            typeof(RunFlowController).GetProperty("SectorElapsedTime").SetValue(flow,run.CurrentSector.StageProfile.ExitActivationTime);
            yield return null;Assert.That(flow.IsExitUnlocked,Is.True);Assert.That(flow.HandleExitReached(),Is.True);
            var choices=Object.FindFirstObjectByType<LevelChoiceManager>();
            yield return CoreTestSupport.Await(()=>choices.IsChoosing);
            var panel=Object.FindFirstObjectByType<LevelChoicePanelView>();
            var card=Read<LevelChoiceCardView[]>(panel,"cardViews").First(c=>c.gameObject.activeInHierarchy);
            Read<Button>(card,"button").onClick.Invoke();Read<Button>(panel,"confirmButton").onClick.Invoke();
            yield return CoreTestSupport.Await(()=>!SceneTransitionOverlay.IsTransitioning&&run.CurrentSector.SectorNumber==nextSector&&
                Object.FindFirstObjectByType<OrbitalStationRuntime>() is {IsInitialized:true});
            Assert.That(run.CurrentConfig.IsEscapeFinal,Is.True);Assert.That(run.FinalObjectiveSatisfied,Is.True);
            Object.FindFirstObjectByType<CharacterSpawner>().SpawnedPlayer.GetComponent<PlayerHealth>().AddMaxHealth(1000000);
            Object.FindFirstObjectByType<EnemySpawner>().StopSpawning();
        }
        flow=RunFlowController.Instance;
        typeof(RunFlowController).GetProperty("SectorElapsedTime").SetValue(flow,run.CurrentSector.StageProfile.ExitActivationTime);
        yield return null;Assert.That(flow.IsExitUnlocked,Is.True);Assert.That(flow.HandleExitReached(),Is.True);
        yield return CoreTestSupport.Await(()=>flow.FinalBoss!=null&&flow.Phase==RunPhase.FinalBossCombat);
        flow.FinalBoss.TakeDamage(flow.FinalBoss.CurrentHealth*2,flow.FinalBoss.transform.position);yield return Ready(RunEndService.BunkerSceneName);
        Assert.That(meta.EscapeProtocolCompleted,Is.True);Assert.That(map.GetStatus("F1"),Is.EqualTo(SurfaceSectorStatus.Completed));
        yield return new WaitForSecondsRealtime(.7f);yield return Capture("escape-protocol-completed");
        }
    static IEnumerator VerifyReloadAndNormalExpedition()
    {
        yield return CoreTestSupport.LoadBunker();
        var meta=MetaProgressionManager.Instance;Assert.That(meta.EscapeProtocolCompleted,Is.True);Assert.That(meta.EscapeProtocolFunded,Is.True);
        var map=meta.SurfaceMap;Assert.That(map.TrySelect("A1"),Is.True);Assert.That(map.TryBuildRunConfig(out var normal),Is.True);Assert.That(normal.IsEscapeFinal,Is.False);
        var starter=Object.FindFirstObjectByType<BunkerRunStarter>();RunSelectionManager.Instance.SelectCharacter(AssetDatabase.LoadAssetAtPath<CharacterData>("Assets/_Project/Data/Characters/01_Gera.asset"));
        starter.StartSurfaceRun(starter.transform);yield return Ready("MVP");Assert.That(RunStateManager.Instance.CurrentConfig.IsEscapeFinal,Is.False);
        Receipt(RunStateManager.Instance.Expedition.Snapshot.RunToken+"/sector");Receipt(RunStateManager.Instance.Expedition.Snapshot.RunToken+"/task");
        RunEndService.Instance.ReturnToBunker();yield return Ready(RunEndService.BunkerSceneName);
    }
}
#endif
