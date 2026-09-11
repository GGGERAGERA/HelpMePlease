#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Subject42.Combat.OrbitalStation;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

public sealed class Subject42BotBatchTests
{
    [Test]
    public void SeedStreamsRepeatAndIgnoreVfxAndOtherStreams()
    {
        var saved = UnityEngine.Random.state;
        try
        {
            UnityEngine.Random.InitState(97);
            var expectedGlobal = UnityEngine.Random.value;
            UnityEngine.Random.InitState(97);
            BotRunSeed.Begin(48151623);
            var spawn = Enumerable.Range(0, 12).Select(_ => BotRunSeed.SpawnRandom.insideUnitCircle).ToArray();
            float reward = BotRunSeed.RewardRandom.value;
            BotRunSeed.End();
            Assert.That(UnityEngine.Random.value, Is.EqualTo(expectedGlobal), "Seeded draws cannot mutate Unity Random");
            BotRunSeed.Begin(48151623);
            for (int i = 0; i < 100; i++) { _ = UnityEngine.Random.value; _ = BotRunSeed.DropRandom.value; _ = BotRunSeed.WorldRandom.value; }
            CollectionAssert.AreEqual(spawn, Enumerable.Range(0, 12).Select(_ => BotRunSeed.SpawnRandom.insideUnitCircle).ToArray());
            Assert.That(BotRunSeed.RewardRandom.value, Is.EqualTo(reward));
        }
        finally { BotRunSeed.End(); UnityEngine.Random.state = saved; }
    }
    [Test]
    public void AggregateExcludesAbortsAndComputesPercentilesRewardsAndSeeds()
    {
        var b = new BotBatchResult { RequestedRuns = 10 };
        b.Results.Add(new BotRunResult { Seed = 11, Result = "SectorCompleted", Duration = 10, DamageTaken = 0, Kills = 10, RewardsTaken = new() { "Pistol", "Pistol" } });
        b.Results.Add(new BotRunResult { Seed = 22, Result = "PlayerDead", Duration = 20, DamageTaken = 100, MinimumHPFraction = 0, Kills = 20, RewardsTaken = new() { "Mount" } });
        b.Results.Add(new BotRunResult { Seed = 33, Result = "Stuck", Duration = 30, DamageTaken = 20 });
        b.Results.Add(new BotRunResult { Seed = 44, Result = "Aborted", Duration = 10000, DamageTaken = 10000 });
        b.Recalculate();
        Assert.That(b.CompletedRuns, Is.EqualTo(3)); Assert.That(b.Aborted, Is.EqualTo(1));
        Assert.That(b.CompletionRate, Is.EqualTo(1f / 3f)); Assert.That(b.DeathRate, Is.EqualTo(1f / 3f));
        Assert.That(b.Duration.Average, Is.EqualTo(20)); Assert.That(b.Duration.Median, Is.EqualTo(20));
        Assert.That(b.Duration.P10, Is.EqualTo(12)); Assert.That(b.Duration.P90, Is.EqualTo(28));
        Assert.That(b.Rewards.Single(r => r.Reward == "Pistol").Count, Is.EqualTo(2));
        CollectionAssert.Contains(b.SuspiciousRuns.Select(r => r.Seed).ToArray(), 22);
        CollectionAssert.Contains(b.SuspiciousRuns.Select(r => r.Seed).ToArray(), 33);
        Assert.That(b.Csv().Split('\n').Length, Is.EqualTo(6));
        var empty = new BotBatchResult(); empty.Recalculate(); Assert.That(empty.CompletionRate, Is.Zero);
        Assert.That(BotDistribution.From(new[] { 2f, 8f }).Median, Is.EqualTo(5f));
    }
    [UnityTearDown]
    public IEnumerator Cleanup()
    {
        if (Application.isPlaying) yield return new ExitPlayMode();
        BotRunSeed.End();
    }

    [UnityTest, Timeout(1800000)]
    public IEnumerator Phase2_SeedReplay_ThreeThenTen_StopAndFailure()
    {
        yield return new EnterPlayMode();
        yield return RunSmoke();
        yield return new ExitPlayMode();
    }
    private static IEnumerator StartManualSector()
    {
        yield return SceneManager.LoadSceneAsync("MainMenu");
        yield return Wait(() => Object.FindFirstObjectByType<BunkerRunStarter>() != null, 25f, "bunker startup");
        var starter = Object.FindFirstObjectByType<BunkerRunStarter>();
        var character = AssetDatabase.FindAssets("t:CharacterData")
            .Select(id => AssetDatabase.LoadAssetAtPath<CharacterData>(AssetDatabase.GUIDToAssetPath(id)))
            .First(c => c != null && c.characterPrefab != null);
        RunSelectionManager.Instance.SelectCharacter(character);
        starter.StartRun((Transform)typeof(BunkerRunStarter).GetField("cameraRig", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(starter));
        yield return Wait(() => SceneManager.GetActiveScene().name == "MVP" && !SceneTransitionOverlay.IsTransitioning, 35f, "manual sector startup");
    }
    [UnityTest]
    public IEnumerator RewardPauseRestoresBaselineAfterHitStop()
    {
        yield return new EnterPlayMode();
        yield return StartManualSector();
        var tuning = Object.FindFirstObjectByType<ProductionFeelTuningController>();
        tuning.SetHitStopEnabled(true);
        var feedback = tuning.GetComponent<PhysicalCombatFeedbackRuntime>();
        var requestHitStop = typeof(PhysicalCombatFeedbackRuntime).GetMethod("RequestHitStop", BindingFlags.NonPublic | BindingFlags.Instance);
        var upgrades = UpgradeManager.Instance;
        var station = Object.FindFirstObjectByType<CharacterSpawner>().SpawnedPlayer.GetComponentInChildren<OrbitalStationRuntime>();
        var kinds = new[] { OrbitalRewardKind.RingSpeed, OrbitalRewardKind.RingPower, OrbitalRewardKind.AddMount };
        float[] speeds = { 1f, 5f, 10f };
        for (int i = 0; i < speeds.Length; i++)
        {
            Time.timeScale = speeds[i];
            requestHitStop.Invoke(feedback, new object[] { 1f });
            Assert.That(Time.timeScale, Is.EqualTo(speeds[i] * .05f).Within(.001f));
            Assert.That(upgrades.DebugForceOrbitalReward(kinds[i]), Is.True);
            yield return null;
            Assert.That(upgrades.TimeScaleAfterRewards, Is.EqualTo(speeds[i]));
            Assert.That(upgrades.DebugSelectCurrentChoice(0), Is.True);
            Assert.That(station.RewardFlow.DebugChooseFirstValidTarget(), Is.True);
            Assert.That(Time.timeScale, Is.EqualTo(speeds[i]));
        }
        tuning.SetHitStopEnabled(false);
        Time.timeScale = 1f;
        Assert.That(BotRunSeed.IsActive, Is.False);
        yield return new ExitPlayMode();
    }
    [UnityTest, Timeout(240000)]
    public IEnumerator ArtifactsOverwriteLatestAndBatchDoesNotWriteSingleRuns()
    {
        yield return new EnterPlayMode();
        yield return CheckArtifactStorage();
        yield return new ExitPlayMode();
    }
    private static IEnumerator CheckArtifactStorage()
    {
        yield return StartManualSector();
        var session = BotRunSession.Ensure();
        session.BindScene(Object.FindFirstObjectByType<CharacterSpawner>(), RunFlowController.Instance, UpgradeManager.Instance, GameplayAreaService.Instance);
        string runs = Path.GetFullPath("Artifacts/BotRuns"), batches = Path.GetFullPath("Artifacts/BotBatches");
        string latestRun = Path.Combine(runs, "latest_run.json");
        string latestBatch = Path.Combine(batches, "latest_batch.json");
        string[] originalRuns = Directory.Exists(runs) ? Directory.GetFiles(runs) : Array.Empty<string>();
        string[] originalBatches = Directory.Exists(batches) ? Directory.GetFiles(batches) : Array.Empty<string>();
        Assert.That(session, Is.Not.Null, "Bot session initialized");
        session.FixedSeed = 48151623;
        session.SelectedSpeed = 10f;
        var menu = Object.FindFirstObjectByType<Subject42DebugMenu>();
        var flags = BindingFlags.NonPublic | BindingFlags.Instance;
        Assert.That(menu, Is.Not.Null, "Production Bot Lab menu available");
        typeof(Subject42DebugMenu).GetMethod("SetOpen", flags).Invoke(menu, new object[] { true });
        var tab = Enum.Parse(typeof(Subject42DebugMenu).GetNestedType("DebugTab", BindingFlags.NonPublic), "QA");
        typeof(Subject42DebugMenu).GetMethod("SelectTab", flags).Invoke(menu, new[] { tab, (object)true });
        typeof(Subject42DebugMenu).GetMethod("SelectQaSection", flags).Invoke(menu, new object[] { 2 });
        yield return null; // Let the menu finish replacing the previous tab's controls.
        Object.FindObjectsByType<UnityEngine.UI.Button>(FindObjectsSortMode.None)
            .Single(b => b.GetComponentInChildren<TMPro.TextMeshProUGUI>()?.text == "RUN SEED").onClick.Invoke();
        yield return Wait(() => !session.IsStarting && !session.IsRunning, 60f, "RUN SEED save");
        Assert.That(session.OutputPath, Is.EqualTo(latestRun));
        string singleJson = File.ReadAllText(latestRun);
        Assert.That(JsonUtility.FromJson<BotRunResult>(singleJson).Seed, Is.EqualTo(48151623));
        var batch = session.gameObject.AddComponent<BotBatchRunner>();
        string previousId = null;
        for (int repeat = 0; repeat < 2; repeat++)
        {
            Assert.That(batch.StartBatch(2, BotSeedMode.Auto, 0, 10f), Is.True);
            yield return Wait(() => !batch.IsActive, 90f, "repeated batch save");
            AssertBatch(batch, 2);
            Assert.That(batch.OutputPath, Is.EqualTo(latestBatch));
            var saved = JsonUtility.FromJson<BotBatchResult>(File.ReadAllText(latestBatch));
            Assert.That(saved.BatchId, Is.EqualTo(batch.Result.BatchId).And.Not.EqualTo(previousId));
            Assert.That(saved.Results.Count, Is.EqualTo(2));
            Assert.That(File.ReadAllText(Path.ChangeExtension(latestBatch, ".csv")), Is.EqualTo(batch.Result.Csv()));
            Assert.That(File.ReadAllText(latestRun), Is.EqualTo(singleJson), "Batch must not overwrite the standalone run");
            Assert.That(session.OutputPath, Is.Null);
            previousId = saved.BatchId;
        }
        Assert.That(session.StartBotRun(771203, 10f), Is.True);
        yield return Wait(() => !session.IsStarting && !session.IsRunning, 60f, "single after batch");
        Assert.That(session.OutputPath, Is.EqualTo(latestRun));
        Assert.That(JsonUtility.FromJson<BotRunResult>(File.ReadAllText(latestRun)).Seed, Is.EqualTo(771203));
        CollectionAssert.AreEquivalent(originalRuns.Append(latestRun).Distinct(), Directory.GetFiles(runs));
        CollectionAssert.AreEquivalent(originalBatches.Concat(new[] { latestBatch, Path.ChangeExtension(latestBatch, ".csv") }).Distinct(), Directory.GetFiles(batches));
        Debug.Log("[Bot Batch QA] PASS latest RUN SEED, repeated batch JSON/CSV, no per-run batch files");
    }
    private static IEnumerator RunSmoke()
    {
        yield return StartManualSector();
        var session = BotRunSession.Ensure();
        session.BindScene(Object.FindFirstObjectByType<CharacterSpawner>(), RunFlowController.Instance, UpgradeManager.Instance, GameplayAreaService.Instance);
        Assert.That(BotRunSeed.IsActive, Is.False);
        const int seed = 48151623;
        Assert.That(session.StartBotRun(seed, 1f), Is.True);
        yield return Wait(() => !session.IsStarting && !session.IsRunning, 150f, "first 1x seeded run");
        var first = session.Result;
        Assert.That(first.Result, Is.EqualTo("SectorCompleted").Or.EqualTo("PlayerDead"));
        Assert.That(session.StartBotRun(seed, 1f), Is.True);
        yield return Wait(() => !session.IsStarting && !session.IsRunning, 150f, "replayed 1x seeded run");
        var second = session.Result;
        Assert.That(second.LayoutSignature, Is.EqualTo(first.LayoutSignature));
        CollectionAssert.AreEqual(first.InitialEnemyTypes.Take(3), second.InitialEnemyTypes.Take(3));
        Assert.That(second.Seed, Is.EqualTo(seed));
        Debug.Log($"[Bot Batch QA] Replay {seed}: first {first.Result} {first.Duration:F2}s kills={first.Kills} XP={first.XPCollected}; second {second.Result} {second.Duration:F2}s kills={second.Kills} XP={second.XPCollected}; layout and first 3 enemy types MATCH");

        var batch = session.gameObject.AddComponent<BotBatchRunner>();
        Assert.That(batch.StartBatch(3, BotSeedMode.Auto, 0, 5f), Is.True);
        yield return Wait(() => !batch.IsActive, 420f, "3-run 5x batch");
        AssertBatch(batch, 3);
        Debug.Log("[Bot Batch QA] THREE_5X " + batch.OutputPath);
        Assert.That(batch.StartBatch(10, BotSeedMode.Auto, 0, 10f), Is.True);
        yield return Wait(() => !batch.IsActive, 800f, "10-run 10x batch");
        AssertBatch(batch, 10);
        Assert.That(batch.Result.Results.Select(r => r.Seed).Distinct().Count(), Is.EqualTo(10));
        Debug.Log("[Bot Batch QA] TEN_10X " + batch.OutputPath);
        Assert.That(Object.FindObjectsByType<BotRunSession>(FindObjectsSortMode.None).Length, Is.EqualTo(1));

        // Force terminal outcomes to prove continuation without modifying the Survivor policy.
        Assert.That(batch.StartBatch(4, BotSeedMode.Fixed, seed, 5f), Is.True);
        yield return Wait(() => session.IsRunning, 35f, "continuation first run");
        session.Finish(BotRunOutcome.Stuck, "QA injected stuck");
        yield return Wait(() => session.IsRunning, 35f, "continuation after stuck");
        Assert.That(batch.Result.CompletedRuns, Is.EqualTo(1));
        session.Finish(BotRunOutcome.Error, "QA injected error");
        yield return Wait(() => session.IsRunning, 35f, "continuation after error");
        Assert.That(batch.Result.CompletedRuns, Is.EqualTo(2));
        var dying = Object.FindFirstObjectByType<CharacterSpawner>().SpawnedPlayer.GetComponent<PlayerHealth>();
        float deathDeadline = Time.realtimeSinceStartup + 5f;
        while (!dying.IsDead && Time.realtimeSinceStartup < deathDeadline)
        {
            dying.TakeDamage(100000f, Vector2.zero);
            yield return null;
        }
        yield return Wait(() => batch.Result.CompletedRuns == 3 && session.IsRunning, 35f, "continuation after death");
        Assert.That(batch.Result.PlayerDead, Is.EqualTo(1));
        var movement = Object.FindFirstObjectByType<CharacterSpawner>().SpawnedPlayer.GetComponent<CharacterMovement2D>();
        Assert.That(Time.timeScale, Is.EqualTo(5f));
        batch.StopBatch();
        Assert.That(batch.IsActive, Is.False); Assert.That(session.IsRunning, Is.False);
        Assert.That(movement.MovementIntent, Is.Null); Assert.That(BotRunSeed.IsActive, Is.False);
        Assert.That(Time.timeScale, Is.EqualTo(1f));
        Assert.That(batch.Result.Results.Count, Is.EqualTo(4)); Assert.That(batch.Result.Aborted, Is.EqualTo(1));
        Assert.That(File.Exists(batch.OutputPath), Is.True);
        yield return null; yield return null;
        Assert.That(session.IsStarting, Is.False, "STOP must not schedule another run");

        // Stop an accelerated run while the production reward pause owns timeScale.
        Assert.That(batch.StartBatch(3, BotSeedMode.Fixed, seed, 5f), Is.True);
        yield return Wait(() => session.IsRunning, 35f, "reward STOP");
        var upgrades = UpgradeManager.Instance;
        Assert.That(upgrades.DebugForceOrbitalReward(OrbitalRewardKind.AddMount), Is.True);
        batch.StopBatch();
        Assert.That(Time.timeScale, Is.Zero);
        Assert.That(upgrades.TimeScaleAfterRewards, Is.EqualTo(1f));
        upgrades.DebugSelectCurrentChoice(0);
        var station = Object.FindFirstObjectByType<CharacterSpawner>().SpawnedPlayer.GetComponentInChildren<OrbitalStationRuntime>();
        Assert.That(station.RewardFlow.DebugChooseFirstValidTarget(), Is.True);
        Assert.That(Time.timeScale, Is.EqualTo(1f));
        Assert.That(station.InputOwner.DebugSuppressPlayerInput, Is.False);
        Assert.That(station.GetComponentInParent<CharacterMovement2D>().MovementIntent, Is.Null);
        var menu = Object.FindFirstObjectByType<Subject42DebugMenu>();
        var flags = BindingFlags.NonPublic | BindingFlags.Instance;
        typeof(Subject42DebugMenu).GetMethod("SetOpen", flags).Invoke(menu, new object[] { true });
        var tab = Enum.Parse(typeof(Subject42DebugMenu).GetNestedType("DebugTab", BindingFlags.NonPublic), "QA");
        typeof(Subject42DebugMenu).GetMethod("SelectTab", flags).Invoke(menu, new[] { tab, (object)true });
        typeof(Subject42DebugMenu).GetMethod("SelectQaSection", flags).Invoke(menu, new object[] { 2 });
        yield return null; yield return null;
        ScreenCapture.CaptureScreenshot("Artifacts/GeneratedQA/BotLab/phase2-menu.png");
        yield return null;
        typeof(Subject42DebugMenu).GetMethod("SetOpen", flags).Invoke(menu, new object[] { false });
        Assert.That(Time.timeScale, Is.EqualTo(1f));
        Debug.Log("[Bot Batch QA] PASS STOP, reward resume, input, RNG release and Death/Stuck/Error continuation");
        int retained = batch.Result.Results.Count;
        typeof(BotBatchRunner).GetProperty("OutputPath").SetValue(batch, Path.GetDirectoryName(batch.OutputPath));
        typeof(BotBatchRunner).GetMethod("Complete", flags).Invoke(batch, new object[] { "Completed", null });
        Assert.That(batch.Result.Status, Is.EqualTo("Error"), "A failed final save must not report success");
        Assert.That(batch.Result.Results.Count, Is.EqualTo(retained));
    }
    private static void AssertBatch(BotBatchRunner batch, int count)
    {
        Assert.That(batch.Result.Status, Is.EqualTo("Completed"), batch.Result.StopReason);
        Assert.That(batch.Result.CompletedRuns, Is.EqualTo(count));
        Assert.That(batch.Result.Results.Count, Is.EqualTo(count));
        Assert.That(File.Exists(batch.OutputPath), Is.True);
        Assert.That(File.Exists(Path.ChangeExtension(batch.OutputPath, ".csv")), Is.True);
        Assert.That(batch.Result.Results.All(r => r.Duration > 0 && r.FinalOrbital != null), Is.True);
        Assert.That(batch.Result.Results.Sum(r => r.Kills), Is.GreaterThan(0));
        Assert.That(batch.Result.Results.Sum(r => r.XPCollected), Is.GreaterThan(0));
    }
    private static IEnumerator Wait(Func<bool> ready, float seconds, string label)
    {
        float deadline = Time.realtimeSinceStartup + seconds;
        while (!ready() && Time.realtimeSinceStartup < deadline) yield return null;
        Assert.That(ready(), Is.True, label + " timed out");
    }
}
#endif
