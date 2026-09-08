#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Subject42.Combat.OrbitalStation;
using Object = UnityEngine.Object;

public sealed class Subject42FinalBossFlowTests
{
    [Serializable] private sealed class SavedPreference { public string key; public bool exists; public int value; }
    [Serializable] private sealed class Preferences { public List<SavedPreference> values = new(); }
    private const string BackupKey = "Subject42FinalBossTests.Preferences";

    [SetUp]
    public void PreserveRewardsAndUnlockProgress()
    {
        var keys = new List<string> { "TOTAL_GOLD", BunkerIntroController.ViewedPreferenceKey };
        foreach (string guid in AssetDatabase.FindAssets("t:UnlockableContentData"))
        {
            var content = AssetDatabase.LoadAssetAtPath<UnlockableContentData>(AssetDatabase.GUIDToAssetPath(guid));
            keys.Add("Unlock_" + content.id);
            keys.Add("UnlockProgress_" + content.id);
        }
        var saved = new Preferences();
        foreach (string key in keys.Distinct()) saved.values.Add(new SavedPreference
            { key = key, exists = PlayerPrefs.HasKey(key), value = PlayerPrefs.GetInt(key) });
        SessionState.SetString(BackupKey, JsonUtility.ToJson(saved));
    }

    [UnityTearDown]
    public IEnumerator CleanupPlayMode()
    {
        if (Application.isPlaying)
        {
            // Unload scene UI while persistent services still exist, then exit Play Mode.
            Scene scene = SceneManager.GetActiveScene();
            Scene cleanup = SceneManager.CreateScene("FinalBossTestCleanup");
            SceneManager.SetActiveScene(cleanup);
            yield return SceneManager.UnloadSceneAsync(scene);
            yield return new ExitPlayMode();
        }
        var saved = JsonUtility.FromJson<Preferences>(SessionState.GetString(BackupKey, ""));
        foreach (var value in saved.values)
            if (value.exists) PlayerPrefs.SetInt(value.key, value.value);
            else PlayerPrefs.DeleteKey(value.key);
        PlayerPrefs.Save();
        SessionState.EraseString(BackupKey);
    }

    [Test]
    public void ProductionRouteEndsAtThreeWithoutAnExtraBossSector()
    {
        Assert.That(RunRoute.TotalSectors, Is.EqualTo(3));
        Assert.That(RunRoute.FinalSector, Is.EqualTo(RunRoute.TotalSectors));
        for (int sector = RunRoute.FirstSector; sector < RunRoute.TotalSectors; sector++)
            Assert.That(RunRoute.HasNextSector(sector), Is.True);
        Assert.That(RunRoute.HasNextSector(RunRoute.TotalSectors), Is.False);
        Assert.That(RunRoute.IsExplorationSector(RunRoute.TotalSectors), Is.True);
        Assert.That(RunRoute.IsExplorationSector(RunRoute.TotalSectors + 1), Is.False);
    }

    [UnityTest]
    public IEnumerator FinalZoneWaitsForRewardsSpawnsOneBossAndReturnsVictoryToBunker()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
        yield return new EnterPlayMode();
        yield return ExerciseVictory();
    }

    private static IEnumerator ExerciseVictory()
    {
        yield return StartRun();
        yield return ReachFinalSector();
        Assert.That(Application.isPlaying, Is.True);
        Assert.That(RunStateManager.Instance, Is.Not.Null, "RunState after sector transition");
        Assert.That(RunFlowController.Instance, Is.Not.Null, "RunFlow after sector transition");
        Assert.That(One<OrbitalStationRuntime>(), Is.Not.Null, "Station after sector transition");
        Assert.That(UpgradeManager.Instance, Is.Not.Null, "Upgrade manager after sector transition");
        var run = RunStateManager.Instance;
        var flow = RunFlowController.Instance;
        var station = One<OrbitalStationRuntime>();
        var state = station.State;
        var scene = SceneManager.GetActiveScene();
        var upgrades = UpgradeManager.Instance;
        Assert.That(upgrades.DebugForceOrbitalReward(OrbitalRewardKind.ModuleDamage), Is.True);
        // Simulate a zone callback arriving while card selection already owns pause.
        EnterZoneCallback();
        Assert.That(flow.Phase, Is.EqualTo(RunPhase.WaitingForRewards));
        Assert.That(One<LevelChoiceManager>().TryShowChoices(), Is.False);
        Assert.That(flow.HandleExitReached(), Is.False);
        for (int i = 0; i < 5; i++) yield return null;
        Assert.That(flow.FinalBoss, Is.Null);
        Assert.That(upgrades.DebugSelectCurrentChoice(0), Is.True);
        for (int i = 0; i < 5; i++) yield return null;
        Assert.That(flow.Phase, Is.EqualTo(RunPhase.WaitingForRewards), "Arena placement still owns the reward");
        Assert.That(station.RewardFlow.DebugChooseModule(station.Modules[0].StableModuleId), Is.True);
        yield return Await(() => upgrades.IsRewardQueueIdle && station.InputOwner.CanTransition);
        string before = BuildSnapshot(state);
        yield return Await(() => flow.Phase == RunPhase.FinalBossIntro);
        var spawner = One<EnemySpawner>();
        Assert.That(spawner.FinalBossPressureMultiplier, Is.GreaterThan(1f));
        Assert.That(spawner.IsSpawningEnabled, Is.True);
        Assert.That(flow.FinalBoss, Is.Null);
        yield return Await(() => flow.Phase == RunPhase.FinalBossCombat);
        Assert.That(SceneManager.GetActiveScene(), Is.EqualTo(scene));
        Assert.That(One<OrbitalStationRuntime>(), Is.SameAs(station));
        Assert.That(station.State, Is.SameAs(state));
        Assert.That(BuildSnapshot(state), Is.EqualTo(before));
        var boss = flow.FinalBoss;
        Assert.That(boss.IsBoss, Is.True);
        Assert.That(boss.name, Does.StartWith("p_Boss1"));
        Assert.That(Vector2.Distance(boss.transform.position, Player().transform.position), Is.GreaterThan(20f));
        Assert.That(GameplayAreaService.Instance.IsInsideSpawnArea(boss.transform.position), Is.True);
        Assert.That(EnemyHealth.ActiveInstances.Count(e => e.IsBoss), Is.EqualTo(1));
        Assert.That(spawner.IsSpawningEnabled, Is.True);
        Assert.That(EnemyHealth.ActiveInstances.Any(e => !e.IsBoss), Is.True);
        Assert.That(flow.HandleExitReached(), Is.False);
        Assert.That(station.RebuildRuntimeFromState(), Is.True);
        Assert.That(station.RebuildRuntimeFromState(), Is.True);
        Assert.That(flow.FinalBoss, Is.SameAs(boss));
        Assert.That(flow.Phase, Is.EqualTo(RunPhase.FinalBossCombat));
        flow.HandleBossDefeated(boss); // A live boss is not a victory callback.
        Assert.That(flow.Phase, Is.EqualTo(RunPhase.FinalBossCombat));
        Assert.That(run.CurrentSector.SectorNumber, Is.EqualTo(RunRoute.TotalSectors));
        int ringCount = state.Rings.Count, moduleCount = state.Modules.Count, coreLevel = state.CoreState.Level;
        boss.TakeDamage(float.MaxValue, boss.transform.position);
        flow.HandleBossDefeated(boss);
        Assert.That(flow.IsVictoryConfirmed, Is.True);
        Assert.That(spawner.IsSpawningEnabled, Is.False);
        Assert.That(spawner.FinalBossPressureMultiplier, Is.EqualTo(1f));
        int enemiesAtVictory = EnemyHealth.ActiveInstances.Count;
        spawner.SpawnAdditionalWave(Player().transform.position, 3);
        Assert.That(spawner.SpawnSpecificEnemyAround(run.CurrentSector.BossPrefab,
            Player().transform.position, 10f, 15f), Is.Null);
        Assert.That(EnemyHealth.ActiveInstances.Count, Is.EqualTo(enemiesAtVictory));
        yield return Await(() => SceneManager.GetActiveScene().name == "MainMenu");
        var summary = run.GetRunSummarySnapshot(RunEndReason.Victory);
        Assert.That(summary.EndReason, Is.EqualTo(RunEndReason.Victory));
        Assert.That(summary.CompletedLevels, Is.EqualTo(RunRoute.TotalSectors));
        Assert.That(summary.SectorNumber, Is.EqualTo(RunRoute.TotalSectors));
        Assert.That(summary.OrbitalRingCount, Is.EqualTo(ringCount));
        Assert.That(summary.OrbitalModuleCount, Is.EqualTo(moduleCount));
        Assert.That(summary.OrbitalCoreLevel, Is.EqualTo(coreLevel));
        int gold = CurrencyManager.Instance.TotalGold;
        Assert.That(run.EndRun(RunEndReason.Victory), Is.SameAs(summary));
        Assert.That(CurrencyManager.Instance.TotalGold, Is.EqualTo(gold));
        yield return Await(() => One<BunkerRunSummaryPresenter>() != null &&
            Get(One<BunkerRunSummaryPresenter>(), "notification") != null);
        Assert.That(((RectTransform)Get(One<BunkerRunSummaryPresenter>(), "notification")).gameObject.activeInHierarchy, Is.True);
    }

    [UnityTest] public IEnumerator DeathDuringIntroCancelsDelayedSpawn() => EnterDeathScenario(false);
    [UnityTest] public IEnumerator DeathDuringBossCombatRemainsDefeat() => EnterDeathScenario(true);

    private static IEnumerator EnterDeathScenario(bool afterSpawn)
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
        yield return new EnterPlayMode();
        yield return DeathScenario(afterSpawn);
    }

    private static IEnumerator DeathScenario(bool afterSpawn)
    {
        yield return StartRun();
        yield return ReachFinalSector();
        Assert.That(Application.isPlaying, Is.True);
        Assert.That(RunFlowController.Instance, Is.Not.Null, "RunFlow before final zone");
        Assert.That(Player(), Is.Not.Null, "Player before final zone");
        Assert.That(ProductionSectorExit.ActiveExits.Count, Is.EqualTo(1));
        var flow = RunFlowController.Instance;
        EnterZoneCallback();
        yield return Await(() => flow.Phase == (afterSpawn ? RunPhase.FinalBossCombat : RunPhase.FinalBossIntro));
        var station = One<OrbitalStationRuntime>();
        int rings = station.State.Rings.Count, modules = station.State.Modules.Count;
        Call(Player().GetComponent<PlayerHealth>(), "Die");
        Assert.That(flow.Phase, Is.EqualTo(RunPhase.Stopped));
        Assert.That(One<EnemySpawner>().IsSpawningEnabled, Is.False);
        Assert.That(One<EnemySpawner>().FinalBossPressureMultiplier, Is.EqualTo(1f));
        Assert.That(((TMP_Text)Get(One<DeathResultPresentation>(), "sector")).text,
            Is.EqualTo($"ÑÅÊÒÎÐ {RunRoute.TotalSectors} / {RunRoute.TotalSectors}"));
        if (afterSpawn)
        {
            flow.FinalBoss.TakeDamage(float.MaxValue, flow.FinalBoss.transform.position);
            Assert.That(flow.IsVictoryConfirmed, Is.False);
        }
        Time.timeScale = 10f; // Cancellation must hold even after some other UI unpauses.
        yield return new WaitForSeconds(6f);
        if (!afterSpawn) Assert.That(flow.FinalBoss, Is.Null);
        Assert.That(flow.HandleExitReached(), Is.False);
        var run = RunStateManager.Instance;
        GameOverManager.Instance.MainMenu();
        yield return Await(() => SceneManager.GetActiveScene().name == "MainMenu");
        var summary = run.GetRunSummarySnapshot(RunEndReason.PlayerDied);
        Assert.That(summary.EndReason, Is.EqualTo(RunEndReason.PlayerDied));
        Assert.That(summary.SectorNumber, Is.EqualTo(RunRoute.TotalSectors));
        Assert.That(summary.OrbitalRingCount, Is.EqualTo(rings));
        Assert.That(summary.OrbitalModuleCount, Is.EqualTo(modules));
    }

    private static IEnumerator StartRun()
    {
        var character = AssetDatabase.FindAssets("t:CharacterData").Select(g =>
            AssetDatabase.LoadAssetAtPath<CharacterData>(AssetDatabase.GUIDToAssetPath(g))).First(c => c.characterPrefab != null);
        SceneManager.LoadSceneAsync("MainMenu");
        yield return Await(() => SceneManager.GetActiveScene().name == "MainMenu" && One<BunkerContext>() != null);
        for (int i = 0; i < 5; i++) yield return null;
        var intro = One<BunkerIntroController>();
        if (intro != null) { intro.StopAllCoroutines(); Call(intro, "FinishIntro", false); }
        RunSelectionManager.Instance.SelectCharacter(character);
        var starter = One<BunkerRunStarter>();
        starter.StartRun((Transform)Get(starter, "cameraRig"));
        yield return Await(() => SceneManager.GetActiveScene().name == "MVP");
        yield return Ready();
    }

    private static IEnumerator ReachFinalSector()
    {
        while (RunRoute.HasNextSector(RunStateManager.Instance.CurrentLevel))
        {
            int next = RunStateManager.Instance.CurrentLevel + 1;
            var oldFlow = RunFlowController.Instance;
            var zone = ProductionSectorExit.ActiveExits.Single();
            Player().GetComponent<Rigidbody2D>().position = zone.transform.position;
            Physics2D.SyncTransforms();
            yield return Await(() => One<LevelChoiceManager>().IsChoosing);
            var choice = One<LevelChoiceManager>();
            var options = (Dictionary<WorldRuleData, RunSector>)Get(choice, "currentSectorOptions");
            Assert.That(options.Values.All(s => s.SectorNumber == next), Is.True);
            Call(choice, "SelectRule", options.Keys.First(rule => rule.RuleType != WorldRuleType.Wind));
            yield return Await(() => RunFlowController.Instance != null && RunFlowController.Instance != oldFlow);
            yield return Ready();
            Assert.That(RunStateManager.Instance.CurrentLevel, Is.EqualTo(next));
        }
    }

    public static IEnumerator EnterFinalBossAndDefeat()
    {
        var flow = RunFlowController.Instance;
        Time.timeScale = 1f;
        Assert.That(flow.HandleExitReached(), Is.True);
        yield return Await(() => flow.Phase == RunPhase.FinalBossCombat);
        flow.FinalBoss.TakeDamage(float.MaxValue, flow.FinalBoss.transform.position);
        Assert.That(flow.IsVictoryConfirmed, Is.True);
    }

    private static IEnumerator Ready()
    {
        yield return Await(() => One<OrbitalStationRuntime>() != null && One<OrbitalStationRuntime>().IsInitialized &&
            ProductionSectorExit.ActiveExits.Count == 1 && One<RunThreatController>().AppliedPresetIndex >= 0);
        yield return Await(() => !SceneTransitionOverlay.IsTransitioning);
        Player().GetComponent<PlayerHealth>().AddMaxHealth(1000000f);
        Time.timeScale = 1f;
        yield return null;
    }
    private static string BuildSnapshot(OrbitalRunState state)
    {
        var copy = JsonUtility.FromJson<OrbitalRunState>(JsonUtility.ToJson(state));
        // Rotation advances during combat; compare persisted build/upgrade data.
        foreach (var ring in copy.Rings) ring.CurrentPhase = 0f;
        return JsonUtility.ToJson(copy);
    }
    private static GameObject Player() => One<CharacterSpawner>().SpawnedPlayer;
    private static void EnterZoneCallback()
    {
        var collider = Player().GetComponent<Collider2D>();
        Assert.That(collider, Is.Not.Null, "Player trigger collider");
        Call(ProductionSectorExit.ActiveExits.Single(), "OnTriggerEnter2D", collider);
    }
    private static T One<T>() where T : Object => Object.FindFirstObjectByType<T>(FindObjectsInactive.Include);
    private static object Get(object value, string name) => value.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(value);
    private static void Call(object value, string name, params object[] args) => value.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(value, args);
    private static IEnumerator Await(Func<bool> condition)
    {
        float deadline = Time.realtimeSinceStartup + 40f;
        while (!condition() && Time.realtimeSinceStartup < deadline) yield return null;
        Assert.That(condition(), Is.True, "Timed out waiting for production run flow");
    }
}
#endif
