using System.Collections;
using NUnit.Framework;
using Subject42.Combat.OrbitalStation;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

public sealed class Subject42FinalBossFlowTests
{
    [SetUp]
    public void PreservePreferences() => CoreTestSupport.PreservePreferences();

    [UnitySetUp]
    public IEnumerator BeginRun() => CoreTestSupport.BeginRun();

    [UnityTearDown]
    public IEnumerator Cleanup() => CoreTestSupport.CleanupPlayMode();

    [Category("Core"), UnityTest]
    public IEnumerator PlayerDeathReturnsToBunkerAndStartsCleanSecondRun()
    {
        var run = RunStateManager.Instance;
        int previousRunId = run.RunId;
        var previousState = run.OrbitalStationState;
        var health = Object.FindFirstObjectByType<CharacterSpawner>()
            .SpawnedPlayer.GetComponent<PlayerHealth>();
        Assert.That(health.TakeDamage(float.MaxValue, Vector2.zero), Is.True);
        Assert.That(health.IsDead, Is.True);
        Assert.That(Time.timeScale, Is.Zero);
        Assert.That(RunFlowController.Instance.Phase, Is.EqualTo(RunPhase.Stopped));
        GameOverManager.Instance.MainMenu();
        yield return CoreTestSupport.Await(() =>
            SceneManager.GetActiveScene().name == "MainMenu" && !SceneTransitionOverlay.IsTransitioning);
        Assert.That(run.IsRunEnded, Is.True);
        Assert.That(run.GetRunSummarySnapshot(RunEndReason.PlayerDied).EndReason,
            Is.EqualTo(RunEndReason.PlayerDied));
        yield return StartAndAssertCleanSecondRun(previousRunId, previousState);
    }

    [Category("Core"), UnityTest]
    public IEnumerator VictoryEndBoundaryCommitsGoldOnceAndStartsCleanSecondRun()
    {
        // The golden path verifies actual boss death; this isolates the public end-run boundary.
        var run = RunStateManager.Instance;
        int previousRunId = run.RunId;
        var previousState = run.OrbitalStationState;
        run.RegisterCompletedLevel();
        int goldBefore = CurrencyManager.Instance.TotalGold;
        int payouts = 0;
        CurrencyManager.Instance.OnGoldUpdated += _ => payouts++;
        var summary = run.EndRun(RunEndReason.Victory, previousRunId);
        Assert.That(summary, Is.Not.Null);
        Assert.That(summary.EndReason, Is.EqualTo(RunEndReason.Victory));
        Assert.That(summary.GoldEarned, Is.GreaterThan(0));
        int expectedGold = CurrencyManager.Instance.TotalGold;
        Assert.That(expectedGold, Is.GreaterThan(goldBefore));
        Assert.That(payouts, Is.EqualTo(1));
        Assert.That(run.EndRun(RunEndReason.Victory, previousRunId), Is.SameAs(summary));
        Assert.That(CurrencyManager.Instance.TotalGold, Is.EqualTo(expectedGold));
        Assert.That(payouts, Is.EqualTo(1));
        RunEndService.RecoverToBunker();
        yield return CoreTestSupport.Await(() =>
            SceneManager.GetActiveScene().name == "MainMenu" && !SceneTransitionOverlay.IsTransitioning);
        Assert.That(run.GetRunSummarySnapshot(RunEndReason.Victory), Is.SameAs(summary));
        Assert.That(CurrencyManager.Instance.TotalGold, Is.EqualTo(expectedGold));
        yield return StartAndAssertCleanSecondRun(previousRunId, previousState);
        Assert.That(CurrencyManager.Instance.TotalGold, Is.EqualTo(expectedGold));
    }

    private static IEnumerator StartAndAssertCleanSecondRun(int previousRunId, OrbitalRunState previousState)
    {
        var starter = Object.FindFirstObjectByType<BunkerRunStarter>();
        Assert.That(starter, Is.Not.Null);
        starter.StartRun(starter.transform);
        yield return CoreTestSupport.Await(() =>
            SceneManager.GetActiveScene().name != "MainMenu" &&
            !SceneTransitionOverlay.IsTransitioning &&
            Object.FindFirstObjectByType<CharacterSpawner>()?.SpawnedPlayer != null);
        var run = RunStateManager.Instance;
        Assert.That(run.RunId, Is.GreaterThan(previousRunId));
        Assert.That(run.IsActiveRun(run.RunId), Is.True);
        Assert.That(run.CurrentSector.SectorNumber, Is.EqualTo(1));
        Assert.That(run.CompletedLevels, Is.Zero);
        Assert.That(run.PickedUpgrades, Is.Empty);
        Assert.That(Time.timeScale, Is.EqualTo(1f));
        Assert.That(RunFlowController.Instance.Phase, Is.EqualTo(RunPhase.NormalSector));
        Assert.That(UpgradeManager.Instance.IsRewardQueueIdle, Is.True);
        var health = Object.FindFirstObjectByType<CharacterSpawner>()
            .SpawnedPlayer.GetComponent<PlayerHealth>();
        Assert.That(health.IsDead, Is.False);
        Assert.That(health.CurrentHealth, Is.EqualTo(health.MaxHealth));
        Assert.That(run.OrbitalStationState, Is.Not.SameAs(previousState));
        Assert.That(run.OrbitalStationState.RunId, Is.EqualTo(run.RunId));
        Assert.That(run.OrbitalStationState.Rings.Count, Is.EqualTo(1));
        Assert.That(run.OrbitalStationState.Modules.Count, Is.EqualTo(1));
        Assert.That(run.OrbitalStationState.Modules[0].ModuleType, Is.EqualTo(OrbitalModuleKind.Pistol));
    }
}
