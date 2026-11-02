#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

public sealed class BunkerDebugSmokeTests
{
    private static object Invoke(Component controller, string method, params object[] args) =>
        controller.GetType().GetMethod(method).Invoke(controller, args);
    private static bool Busy(Component controller) => (bool)controller.GetType().GetProperty("Busy").GetValue(controller);

    [UnityTearDown]
    public IEnumerator Cleanup()
    {
        // Even a failed restoration assertion must not flush pending test settings to native storage.
        if (Application.isPlaying)
        {
            if (!PlayerPrefs.IsSandboxActive) PlayerPrefs.BeginSandbox(false);
            yield return new ExitPlayMode();
        }
        PlayerPrefs.EndSandbox();
        EditorSceneManager.OpenScene("Assets/_Project/Scenes/MainBuild/MainMenu.unity");
    }

    [UnityTest, Timeout(90000)]
    public IEnumerator IsolatedBunkerControlsAndProductionReturn()
    {
        var type = typeof(CurrencyManager).Assembly.GetType("BunkerDebugController");
        Assert.That(type, Is.Not.Null, "MainMenu needs an independent developer controller.");
        BunkerDebugTools.SetGameView1600();
        PlayerPrefs.BeginSandbox(false);
        PlayerPrefs.SetInt(BunkerIntroController.ViewedPreferenceKey, 1);
        PlayerPrefs.SetInt(TutorialController.CompletionKey, 1);
        EditorSceneManager.OpenScene("Assets/_Project/Scenes/MainBuild/MainMenu.unity");
        yield return new EnterPlayMode();
        yield return CheckLoadedBunker();
    }

    [UnityTest, Timeout(45000)]
    public IEnumerator RestoreMainDiscardsPendingAudioAndLanguageCaches()
    {
        PlayerPrefs.BeginSandbox(false);
        PlayerPrefs.SetInt(BunkerIntroController.ViewedPreferenceKey, 1);
        EditorSceneManager.OpenScene("Assets/_Project/Scenes/MainBuild/MainMenu.unity");
        yield return new EnterPlayMode();
        yield return CheckSettingsRestoration();
    }

    private static IEnumerator CheckSettingsRestoration()
    {
        yield return CoreTestSupport.Await(() => ProductionSceneComposition.Active is { IsReady: true });
        float originalVolume = AudioSettingsService.Instance.MasterVolume;
        GameLanguage originalLanguage = LocalizationService.Instance.CurrentLanguage;
        WeaponControlMode originalControl = WeaponControlSettings.CurrentMode;
        var debug = (Component)Object.FindFirstObjectByType(typeof(CurrencyManager).Assembly.GetType("BunkerDebugController"));
        AudioSettingsService.Instance.SetMasterVolume(originalVolume > .5f ? .17f : .91f);
        LocalizationService.Instance.SetLanguage(originalLanguage == GameLanguage.Russian ? GameLanguage.English : GameLanguage.Russian);
        WeaponControlSettings.SetMode(originalControl == WeaponControlMode.Manual ? WeaponControlMode.AutoAim : WeaponControlMode.Manual);
        Assert.That((bool)Invoke(debug, "RestoreMainSave"), Is.True);
        yield return CoreTestSupport.Await(() => debug != null && !Busy(debug) && ProductionSceneComposition.Active is { IsReady: true });
        Assert.That(AudioSettingsService.Instance.MasterVolume, Is.EqualTo(originalVolume).Within(.001f), "Pending audio settings belong to the discarded test profile.");
        Assert.That(LocalizationService.Instance.CurrentLanguage, Is.EqualTo(originalLanguage));
        Assert.That(WeaponControlSettings.CurrentMode, Is.EqualTo(originalControl), "Discarded test auto-aim must not survive a profile switch.");
    }

    [UnityTest, Timeout(90000)]
    public IEnumerator MvpEventRewardAndReturnInIsolatedProfile()
    {
        PlayerPrefs.BeginSandbox(false);
        PlayerPrefs.SetInt(BunkerIntroController.ViewedPreferenceKey, 1);
        PlayerPrefs.SetInt(TutorialController.CompletionKey, 1);
        EditorSceneManager.OpenScene("Assets/_Project/Scenes/MainBuild/StartScreen.unity");
        yield return new EnterPlayMode();
        // EnterPlayMode must be yielded directly by the NUnit test, not by a nested fixture iterator.
        yield return Phase7SmokeTests.ExerciseProductionRoute();
    }

    [UnityTest, Timeout(60000)]
    public IEnumerator FreshTestStagesUseMissionReceiptTaskRewardAndEscapeAPIs()
    {
        PlayerPrefs.BeginSandbox(true);
        PlayerPrefs.SetInt(BunkerIntroController.ViewedPreferenceKey, 1);
        PlayerPrefs.SetInt(TutorialController.CompletionKey, 1);
        EditorSceneManager.OpenScene("Assets/_Project/Scenes/MainBuild/MainMenu.unity");
        yield return new EnterPlayMode();
        yield return CheckProgressionAPIs();
    }

    private static IEnumerator CheckProgressionAPIs()
    {
        yield return CoreTestSupport.Await(() => ProductionSceneComposition.Active is { IsReady: true });
        var debug = BunkerDebugController.Instance;
        var meta = MetaProgressionManager.Instance;
        var map = meta.SurfaceMap;
        CheckMissionStagesOnAvailableFixture(debug, meta);

        int nativeCores = UnityEngine.PlayerPrefs.GetInt(CurrencyManager.CoresKey, 0);
        Assert.That(debug.SetCores(31), Is.True);
        for (int i = 0; i < 3; i++)
        {
            Assert.That(debug.FundEscapeStage(), Is.True);
            Assert.That(meta.CurrentEscapeStage, Is.EqualTo(i + 1));
        }
        Assert.That(CurrencyManager.Instance.TotalCores, Is.EqualTo(1), "Authored stage costs are 5 + 10 + 15.");
        Assert.That(meta.EscapeProtocolFunded, Is.True);
        Assert.That(meta.EscapeProtocolCompleted, Is.False, "Funding must not bypass the production final trial.");
        Assert.That(map.GetStatus(meta.EscapeProtocol.FinalSector.Id), Is.EqualTo(SurfaceSectorStatus.Available));

        Assert.That(debug.SelectSector(map.Definition.StartingSectors[0]), Is.True);
        var sector = map.Definition.Find(map.SelectedSectorId);
        var task = sector.ExpeditionTasks.First(t => t.IsAvailable(sector.BuildRunConfig(map.Definition.Id)));
        Assert.That(debug.SelectTask(task.Id), Is.True);
        Assert.That(debug.StartNormalRun(), Is.True);
        yield return CoreTestSupport.Await(() => SceneManager.GetActiveScene().name == "MVP" && !SceneTransitionOverlay.IsTransitioning &&
            ProductionSceneComposition.Active is { IsReady: true });
        var expedition = RunStateManager.Instance.Expedition;
        Assert.That(expedition.Task.Id, Is.EqualTo(task.Id));
        Object.FindFirstObjectByType<EnemySpawner>().StopSpawning();
        // Explicit bounded fixture signal: no real-time full run and no production parameter changes.
        expedition.Observe(task.Condition == ExpeditionTaskCondition.Kills ? task.Target : 0,
            task.Condition == ExpeditionTaskCondition.SurviveMinutes ? task.Target * 60f : 0f,
            task.Condition == ExpeditionTaskCondition.CompletedLocations ? task.Target : 0);
        Assert.That(expedition.IsCompleted, Is.True);
        Assert.That(CurrencyManager.Instance.TotalCores, Is.EqualTo(1 + task.RewardCores));
        expedition.Observe(int.MaxValue, 100000f, 3);
        Assert.That(CurrencyManager.Instance.TotalCores, Is.EqualTo(1 + task.RewardCores), "Task reward is idempotent.");
        Assert.That(UnityEngine.PlayerPrefs.GetInt(CurrencyManager.CoresKey, 0), Is.EqualTo(nativeCores));
        Assert.That(UnityEngine.PlayerPrefs.HasKey(CurrencyManager.CoreRewardReceiptPrefix + expedition.Snapshot.RunToken + "/task"), Is.False);
        RunEndService.Instance.ReturnToBunker();
        yield return CoreTestSupport.Await(() => SceneManager.GetActiveScene().name == "MainMenu" && !SceneTransitionOverlay.IsTransitioning &&
            ProductionSceneComposition.Active is { IsReady: true });
        Assert.That(JsonUtility.FromJson<ExpeditionCoreRewards>(PlayerPrefs.GetString(ExpeditionTaskRun.LastRewardsKey)).TaskCompleted, Is.True);
        Assert.That(UnityEngine.PlayerPrefs.GetInt(CurrencyManager.CoresKey, 0), Is.EqualTo(nativeCores));
    }

    private static void CheckMissionStagesOnAvailableFixture(BunkerDebugController debug, MetaProgressionManager meta)
    {
        var map = meta.SurfaceMap;
        var original = meta.Missions.Catalog.Missions[0];
        Assert.That(meta.Missions.IsAvailable(original.MissionId), Is.False, "Current SIGNAL TRACE depends on disabled False Signal.");
        Assert.That(debug.AdvanceMission(original.MissionId), Is.False, "Debug must preserve production content availability.");
        // Same isolated available-content fixture pattern as MissionServiceTests.
        // Never edit or save the production catalog/mission/event assets.
        var catalog = Object.Instantiate(meta.Missions.Catalog);
        var mission = Object.Instantiate(original);
        var objective = Object.Instantiate(mission.Objectives[0]);
        var content = Object.Instantiate(objective.RunContent);
        try
        {
            content.requiredEvent = AssetDatabase.LoadAssetAtPath<OrbitalRelayEvent>("Assets/_Project/prefabs/Environment/WorldEvents/OrbitalRelayEvent.prefab");
            var objectiveData = new SerializedObject(objective);
            objectiveData.FindProperty("content").objectReferenceValue = content; objectiveData.ApplyModifiedPropertiesWithoutUndo();
            var missionData = new SerializedObject(mission);
            missionData.FindProperty("objectives").GetArrayElementAtIndex(0).objectReferenceValue = objective; missionData.ApplyModifiedPropertiesWithoutUndo();
            var catalogData = new SerializedObject(catalog);
            catalogData.FindProperty("missions").GetArrayElementAtIndex(0).objectReferenceValue = mission; catalogData.ApplyModifiedPropertiesWithoutUndo();
            var missions = new MissionService(catalog, map.Definition, new PlayerPrefsMissionStorage(map.Definition.Id));
            string receipt = CurrencyManager.GoldRewardReceiptPrefix + new PlayerPrefsMissionStorage(map.Definition.Id).ReceiptId(mission.MissionId);
            bool nativeReceipt = UnityEngine.PlayerPrefs.HasKey(receipt);
            Assert.That(debug.SetGold(0), Is.True);
            foreach (var expected in new[] { MissionState.Active, MissionState.ObjectiveCompleted, MissionState.ReadyToTurnIn, MissionState.Completed })
            {
                Assert.That(missions.DebugAdvanceTestStage(mission.MissionId), Is.True);
                Assert.That(missions.GetState(mission.MissionId), Is.EqualTo(expected));
            }
            Assert.That(CurrencyManager.Instance.TotalGold, Is.EqualTo(mission.Reward.Gold));
            Assert.That(PlayerPrefs.HasKey(receipt), Is.True);
            Assert.That(UnityEngine.PlayerPrefs.HasKey(receipt), Is.EqualTo(nativeReceipt));
            Assert.That(missions.DebugAdvanceTestStage(mission.MissionId), Is.False);
            Assert.That(missions.Claim(mission.MissionId), Is.False);
            Assert.That(CurrencyManager.Instance.TotalGold, Is.EqualTo(mission.Reward.Gold), "Mission payout is idempotent.");
        }
        finally { Object.DestroyImmediate(content); Object.DestroyImmediate(objective); Object.DestroyImmediate(mission); Object.DestroyImmediate(catalog); }
    }

    private static IEnumerator CheckLoadedBunker()
    {
        yield return CoreTestSupport.Await(() => ProductionSceneComposition.Active is { IsReady: true });
        var type = typeof(CurrencyManager).Assembly.GetType("BunkerDebugController");
        var debug = (Component)Object.FindFirstObjectByType(type);
        Assert.That(debug, Is.Not.Null);
        int nativeGold = UnityEngine.PlayerPrefs.GetInt("TOTAL_GOLD", 0);
        int nativeCores = UnityEngine.PlayerPrefs.GetInt("TOTAL_CORES", 0);
        Invoke(debug, "SetVisible", true);
        Assert.That((bool)type.GetProperty("Visible").GetValue(debug), Is.True);
        Assert.That(Object.FindFirstObjectByType<BunkerCursorInteractor>().enabled, Is.False, "Panel clicks must not reach bunker stations.");
        Assert.That(EventSystem.current.GetComponents<BaseInputModule>().Any(module => module.isActiveAndEnabled), Is.False,
            "Debug clicks and Submit must not reach an underlying production purchase/settings panel.");
        var uiInput = EventSystem.current.GetComponents<BaseInputModule>().First();
        uiInput.enabled = true; // A transition/intro owner can re-enable input after F9 was opened.
        yield return null;
        Assert.That(uiInput.enabled, Is.False, "The open debug panel must retain its input boundary until it closes.");
        Assert.That((bool)Invoke(debug, "SetGold", 1234), Is.True);
        Assert.That((bool)Invoke(debug, "AdjustGold", -34), Is.True);
        Assert.That(CurrencyManager.Instance.TotalGold, Is.EqualTo(1200));
        Assert.That((bool)Invoke(debug, "SetCores", 11), Is.True);
        Assert.That(CurrencyManager.Instance.TotalCores, Is.EqualTo(11));
        Assert.That(UnityEngine.PlayerPrefs.GetInt("TOTAL_GOLD", 0), Is.EqualTo(nativeGold));
        Assert.That(UnityEngine.PlayerPrefs.GetInt("TOTAL_CORES", 0), Is.EqualTo(nativeCores));

        var gates = Object.FindObjectsByType<BunkerGateVisual>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .Where(g => g.gameObject.scene == SceneManager.GetActiveScene()).ToArray();
        Assert.That(gates.Length, Is.GreaterThanOrEqualTo(7));
        var gate = gates.First(g => g.IsUnlocked);
        Invoke(debug, "SetGateOpen", gate, true);
        Assert.That(gate.IsOpen, Is.True);
        Invoke(debug, "SetGateOpen", gate, false);
        Assert.That(gate.IsOpen, Is.False);
        Assert.That(gate.GetComponentsInChildren<Collider2D>(true).Any(c => !c.isTrigger && c.isActiveAndEnabled), Is.True);
        Invoke(debug, "ResetGate", gate);
        Assert.That(gate.DebugHasOverride, Is.False);

        Invoke(debug, "SetMetaLevel", MetaUpgradeType.Hp, 2);
        Assert.That(MetaProgressionManager.Instance.HpLevel, Is.EqualTo(2));
        Invoke(debug, "SetStationLevel", BunkerStationId.Upgrades, 2);
        Assert.That(BunkerStationProgressionService.Instance.GetLevel(BunkerStationId.Upgrades), Is.EqualTo(2));
        var barks = Object.FindFirstObjectByType<BunkerAmbientBarks>();
        Assert.That((bool)Invoke(debug, "PlayBark", BunkerBarkCategory.Event, "story_reflection"), Is.True);
        Assert.That(barks.IsSpeaking, Is.True);
        Assert.That(barks.CurrentText, Is.EqualTo("Ладно. Посмотрим, что дальше."));
        Directory.CreateDirectory("Artifacts/GeneratedQA/BunkerDebug");
        for (int tab = 0; tab < 5; tab++) { Invoke(debug, "SelectTab", tab); yield return null; yield return null; }
        Invoke(debug, "SelectTab", 0);
        yield return new WaitForSecondsRealtime(.3f);
        ScreenCapture.CaptureScreenshot("Artifacts/GeneratedQA/BunkerDebug/economy.png");
        yield return new WaitForSecondsRealtime(.2f);
        Invoke(debug, "SelectTab", 1);
        yield return new WaitForSecondsRealtime(.1f);
        ScreenCapture.CaptureScreenshot("Artifacts/GeneratedQA/BunkerDebug/access.png");
        yield return new WaitForSecondsRealtime(.2f);
        Invoke(debug, "SetVisible", false);
        Assert.That(Object.FindFirstObjectByType<BunkerCursorInteractor>().enabled, Is.True);
        Assert.That(EventSystem.current.GetComponents<BaseInputModule>().Any(module => module.isActiveAndEnabled), Is.True);

        Assert.That((bool)Invoke(debug, "RestoreTestStart"), Is.True);
        yield return CoreTestSupport.Await(() => debug != null && !Busy(debug) && ProductionSceneComposition.Active is { IsReady: true });
        Assert.That(CurrencyManager.Instance.TotalGold, Is.EqualTo(nativeGold));
        Assert.That(CurrencyManager.Instance.TotalCores, Is.EqualTo(nativeCores));
        // Copy reset restores intro state too. Suppress it only inside this test profile.
        PlayerPrefs.SetInt(BunkerIntroController.ViewedPreferenceKey, 1);
        Invoke(debug, "SetVisible", false);
        var starter = Object.FindFirstObjectByType<BunkerRunStarter>();
        RunSelectionManager.Instance.SelectCharacter(AssetDatabase.LoadAssetAtPath<CharacterData>("Assets/_Project/Data/Characters/01_Gera.asset"));
        Assert.That((bool)Invoke(debug, "StartNormalRun"), Is.True);
        yield return CoreTestSupport.Await(() => SceneManager.GetActiveScene().name == "MVP" && !SceneTransitionOverlay.IsTransitioning &&
            Object.FindFirstObjectByType<CharacterSpawner>() is { SpawnedPlayer: not null });
        Assert.That(PlayerPrefs.IsSandboxActive, Is.True);
        Assert.That(RunStateManager.Instance.IsDevelopmentRun, Is.False, "The shortcut must use the normal production route.");
        RunEndService.Instance.ReturnToBunker();
        yield return CoreTestSupport.Await(() => SceneManager.GetActiveScene().name == "MainMenu" && !SceneTransitionOverlay.IsTransitioning &&
            ProductionSceneComposition.Active is { IsReady: true });
        debug = (Component)Object.FindFirstObjectByType(type);
        Assert.That(debug, Is.Not.Null);
        Assert.That((bool)Invoke(debug, "RestoreMainSave"), Is.True);
        yield return CoreTestSupport.Await(() => debug != null && !Busy(debug) && ProductionSceneComposition.Active is { IsReady: true });
        Assert.That(PlayerPrefs.IsSandboxActive, Is.False);
        Assert.That(CurrencyManager.Instance.TotalGold, Is.EqualTo(nativeGold));
        Assert.That(UnityEngine.PlayerPrefs.GetInt("TOTAL_GOLD", 0), Is.EqualTo(nativeGold));
        Assert.That(UnityEngine.PlayerPrefs.GetInt("TOTAL_CORES", 0), Is.EqualTo(nativeCores));
        Assert.That((bool)Invoke(debug, "SetGold", 999), Is.False, "Ordinary profile is read only from debug.");
    }
}
#endif
