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

public sealed class ExpeditionCoreFlowTests
{
    private const string BackupKey = "Subject42.ExpeditionCoreFlow.Backup";
    [Serializable] private sealed class Backup { public string mapKey, mapJson; public bool hadMap; public List<string> receipts = new(); }
    private static T Read<T>(Object target, string field) => (T)target.GetType()
        .GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);
    [SetUp] public void Preserve()
    {
        // NUnit repeats SetUp around Unity domain transitions. Keep the original
        // user's preferences, rather than backing up our own temporary test state.
        if (!string.IsNullOrEmpty(SessionState.GetString(BackupKey, ""))) return;
        CoreTestSupport.PreservePreferences();
        var map = AssetDatabase.LoadAssetAtPath<SurfaceMapDefinition>("Assets/_Project/Data/SurfaceMap/SurfaceMap_MVP.asset");
        string key = PlayerPrefsSurfaceMapStorage.KeyPrefix + map.Id;
        SessionState.SetString(BackupKey, JsonUtility.ToJson(new Backup { mapKey = key, hadMap = PlayerPrefs.HasKey(key), mapJson = PlayerPrefs.GetString(key) }));
        Directory.CreateDirectory(ExpeditionCoreAuthoring.Output);
        File.WriteAllText(ExpeditionCoreAuthoring.Output + "/preferences-original.json", SessionState.GetString("Subject42.Core.Preferences", ""));
        File.WriteAllText(ExpeditionCoreAuthoring.Output + "/map-original.json", SessionState.GetString(BackupKey, ""));
        PlayerPrefs.DeleteKey(key); PlayerPrefs.DeleteKey(CurrencyManager.CoresKey);
        foreach (var sector in map.Sectors) PlayerPrefs.DeleteKey(CurrencyManager.CoreRewardReceiptPrefix + "first/" + map.Id + "/" + sector.Id);
        PlayerPrefs.Save();
    }
    [UnityTearDown] public IEnumerator Cleanup()
    {
        var cleanup = CoreTestSupport.CleanupPlayMode();
        while (cleanup.MoveNext()) yield return cleanup.Current;
        RestoreExtraPreferences();
    }
    public static void RestoreExtraPreferences()
    {
        if (string.IsNullOrEmpty(SessionState.GetString(BackupKey, ""))) return;
        var backup = JsonUtility.FromJson<Backup>(SessionState.GetString(BackupKey, ""));
        if (backup.hadMap) PlayerPrefs.SetString(backup.mapKey, backup.mapJson); else PlayerPrefs.DeleteKey(backup.mapKey);
        foreach (var receipt in backup.receipts) PlayerPrefs.DeleteKey(CurrencyManager.CoreRewardReceiptPrefix + receipt);
        PlayerPrefs.Save(); SessionState.EraseString(BackupKey);
    }
    private static void RememberReceipts()
    {
        var run = RunStateManager.Instance.Expedition;
        var backup = JsonUtility.FromJson<Backup>(SessionState.GetString(BackupKey, ""));
        backup.receipts.Add(run.Snapshot.RunToken + "/task"); backup.receipts.Add(run.Snapshot.RunToken + "/sector");
        SessionState.SetString(BackupKey, JsonUtility.ToJson(backup));
    }
    private static IEnumerator Capture(string name)
    {
        Directory.CreateDirectory(ExpeditionCoreAuthoring.Output);
        ScreenCapture.CaptureScreenshot(ExpeditionCoreAuthoring.Output + "/" + name + ".png");
        yield return null;
    }
    private static IEnumerator BunkerReady() => CoreTestSupport.Await(() => SceneManager.GetActiveScene().name == RunEndService.BunkerSceneName &&
        !SceneTransitionOverlay.IsTransitioning && Object.FindFirstObjectByType<BunkerPlayerLoadoutController>() is { IsReady: true });
    private static IEnumerator Deploy(bool withTask, bool capture = false)
    {
        var starter = Object.FindFirstObjectByType<BunkerRunStarter>();
        var map = starter.SurfaceMap;
        Assert.That(map.TrySelect("A1"), Is.True);
        RunSelectionManager.Instance.SelectCharacter(AssetDatabase.LoadAssetAtPath<CharacterData>("Assets/_Project/Data/Characters/01_Gera.asset"));
        var view = Object.FindObjectsByType<SurfaceMapView>(FindObjectsInactive.Include, FindObjectsSortMode.None).Single();
        view.Show(map, starter, null, starter.transform);
        Read<Button>(view,"startButton").onClick.Invoke();
        var picker = Object.FindFirstObjectByType<ExpeditionTaskPicker>(); Assert.That(picker, Is.Not.Null);
        var hint = Read<TMP_Text>(picker,"hint"); hint.ForceMeshUpdate();
        Assert.That(hint.isTextOverflowing, Is.False, "The death-persistence rule must be fully readable.");
        if (withTask)
        {
            var choice = picker.GetComponentsInChildren<ExpeditionTaskChoice>().Single(row => Read<TMP_Text>(row,"title").text == SurfaceMapBriefing.Text("task.hunter.title"));
            choice.Button.onClick.Invoke(); Assert.That(map.SelectedTask.Id, Is.EqualTo("hunter"));
        }
        else { Read<Button>(picker,"noneButton").onClick.Invoke(); Assert.That(map.SelectedTask, Is.Null); }
        if (capture) yield return Capture("task-picker");
        Read<Button>(picker,"launchButton").onClick.Invoke();
        yield return CoreTestSupport.Await(() => SceneManager.GetActiveScene().name == "MVP" && !SceneTransitionOverlay.IsTransitioning &&
            Object.FindFirstObjectByType<OrbitalStationRuntime>() is { IsInitialized: true });
        RememberReceipts();
        Object.FindFirstObjectByType<CharacterSpawner>().SpawnedPlayer.GetComponent<PlayerHealth>().AddMaxHealth(1000000);
        Assert.That(RunStateManager.Instance.IsDevelopmentRun, Is.False);
    }
    private static IEnumerator Win()
    {
        var run = RunStateManager.Instance;
        var profile = AssetDatabase.LoadAssetAtPath<StageProfileData>("Assets/_Project/Data/Stages/StageProfiles/StageProfile_10.asset");
        run.SetCurrentSector(new RunSector(RunRoute.FinalSector, profile, run.CurrentConfig.WorldRule, run.CurrentConfig.LocalAnomaly));
        var flow = RunFlowController.Instance; flow.InitializeSector(profile);
        typeof(RunFlowController).GetProperty("IsExitUnlocked").SetValue(flow,true);
        Assert.That(flow.HandleExitReached(), Is.True);
        yield return CoreTestSupport.Await(() => flow.FinalBoss != null && flow.Phase == RunPhase.FinalBossCombat);
        flow.FinalBoss.TakeDamage(flow.FinalBoss.CurrentHealth * 2, flow.FinalBoss.transform.position);
        yield return BunkerReady();
    }
    [UnityTest] public IEnumerator SelectedTaskPaysBeforeRealDeathRepeatsOnVictoryAndReloadsInNewSession()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
        yield return new EnterPlayMode();
        yield return CoreTestSupport.LoadBunker();
        LocalizationService.Instance.SetLanguage(GameLanguage.Russian);
        yield return Deploy(true, true);
        var run = RunStateManager.Instance;
        var hud = Object.FindFirstObjectByType<ExpeditionTaskHud>();
        Assert.That(hud, Is.Not.Null);
        while (RunStatsManager.Instance.Kills < 74) RunStatsManager.Instance.AddKill();
        Assert.That(run.Expedition.IsCompleted, Is.False);
        yield return null;
        Assert.That(Read<TMP_Text>(hud,"progress").text, Does.Contain("74/75"));
        yield return Capture("task-progress");
        RunStatsManager.Instance.AddKill();
        Assert.That(run.Expedition.IsCompleted, Is.True);
        Assert.That(PlayerPrefs.GetInt(CurrencyManager.CoresKey), Is.EqualTo(2));
        var health = Object.FindFirstObjectByType<CharacterSpawner>().SpawnedPlayer.GetComponent<PlayerHealth>();
        typeof(PlayerHealth).GetField("isInvulnerable",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(health,false);
        Assert.That(health.TakeDamage(1000000000,Vector2.zero), Is.True);
        Assert.That(health.IsDead, Is.True);
        Assert.That(PlayerPrefs.GetInt(CurrencyManager.CoresKey), Is.EqualTo(2));
        Assert.That(run.GetRunSummarySnapshot(RunEndReason.PlayerDied).Cores.Total, Is.EqualTo(2));
        yield return Capture("death-paid-task");
        var death = Object.FindFirstObjectByType<DeathResultPresentation>();
        Read<Button>(death,"bunkerButton").onClick.Invoke();
        yield return BunkerReady();
        Assert.That(CurrencyManager.Instance.TotalCores, Is.EqualTo(2));
        yield return Deploy(true);
        while (RunStatsManager.Instance.Kills < 75) RunStatsManager.Instance.AddKill();
        Assert.That(CurrencyManager.Instance.TotalCores, Is.EqualTo(4));
        yield return Win();
        Assert.That(CurrencyManager.Instance.TotalCores, Is.EqualTo(6));
        var paid = JsonUtility.FromJson<ExpeditionCoreRewards>(PlayerPrefs.GetString(ExpeditionTaskRun.LastRewardsKey));
        Assert.That(paid.TaskCores, Is.EqualTo(2)); Assert.That(paid.SectorCores, Is.EqualTo(1)); Assert.That(paid.FirstClearCores, Is.EqualTo(1));
        yield return new WaitForSecondsRealtime(.6f);
        yield return Capture("victory-core-summary");
        yield return Deploy(false);
        Assert.That(RunStateManager.Instance.Expedition.Task, Is.Null);
        yield return null;
        Assert.That(Read<CanvasGroup>(Object.FindFirstObjectByType<ExpeditionTaskHud>(),"content").alpha, Is.Zero);
        yield return Win();
        Assert.That(CurrencyManager.Instance.TotalCores, Is.EqualTo(7));
        Assert.That(JsonUtility.FromJson<ExpeditionCoreRewards>(PlayerPrefs.GetString(ExpeditionTaskRun.LastRewardsKey)).FirstClearCores, Is.Zero);
        yield return new ExitPlayMode();
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
        yield return new EnterPlayMode();
        yield return CoreTestSupport.LoadBunker();
        Assert.That(CurrencyManager.Instance.TotalCores, Is.EqualTo(7), "A fresh service/session must load the persisted cores.");
        Assert.That(MetaProgressionManager.Instance.SurfaceMap.GetStatus("A1"), Is.EqualTo(SurfaceSectorStatus.Completed));
        var bunkerHud = Object.FindFirstObjectByType<BunkerHudView>();
        Assert.That(Read<HudIconNumber>(bunkerHud,"cores").GetComponentInChildren<TMP_Text>().text, Is.EqualTo("7"));
        yield return Capture("bunker-cores-after-reload");
    }
}
#endif
