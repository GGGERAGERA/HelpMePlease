#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public sealed partial class Subject42RunStateTests
{

    [Category("Extended")]
    [Test]
    public void EndRunClearsGameplayStateWithoutWaitingForBunkerPresentation()
    {
        runState.RegisterCompletedLevel();
        var summary = runState.EndRun(RunEndReason.Victory);
        Assert.That(summary.CompletedLevels, Is.EqualTo(1));
        Assert.That(runState.CurrentSector, Is.Null, "Cleanup must not depend on delayed UI");
        Assert.That(runState.CompletedLevels, Is.Zero);
        Assert.That(runState.OrbitalStationState, Is.Null);
        Assert.That(runState.PickedUpgrades, Is.Empty);
        Assert.That(runState.EndRun(RunEndReason.Victory), Is.SameAs(summary));
    }

    [Category("Extended")]
    [Test]
    public void CommitCurrentSceneStats_IsIdempotentAcrossSceneManagers()
    {
        RunStatsManager first = CreateStats(3, 65f);

        Assert.That(runState.GetCurrentRunKills(), Is.EqualTo(3));
        Assert.That(runState.GetCurrentRunTime(), Is.EqualTo(65f));

        runState.CommitCurrentSceneStats();
        runState.CommitCurrentSceneStats();

        Assert.That(runState.AccumulatedKills, Is.EqualTo(3));
        Assert.That(runState.AccumulatedRunTime, Is.EqualTo(65f));
        Assert.That(runState.GetCurrentRunKills(), Is.EqualTo(3));

        DestroyTracked(first.gameObject);
        RunStatsManager second = CreateStats(2, 10f);

        Assert.That(runState.GetCurrentRunKills(), Is.EqualTo(5));
        Assert.That(runState.GetCurrentRunTime(), Is.EqualTo(75f));

        runState.CommitCurrentSceneStats();

        Assert.That(runState.AccumulatedKills, Is.EqualTo(5));
        Assert.That(runState.AccumulatedRunTime, Is.EqualTo(75f));
        Assert.That(RunStatsManager.Instance, Is.SameAs(second));
    }

    [Category("Extended")]
    [TestCase(RunEndReason.ReturnedToBunker, 111)]
    [TestCase(RunEndReason.Victory, 111)]
    [TestCase(RunEndReason.PlayerDied, 83)]
    public void RewardCalculator_UsesReasonMultiplier(
        RunEndReason reason,
        int expected)
    {
        int reward = RunRewardCalculator.CalculateGold(
            5f,
            120f,
            1f,
            reason);

        Assert.That(reward, Is.EqualTo(expected));
    }

    [Category("Extended")]
    [Test]
    public void RewardCalculator_ClampsNegativeInputs()
    {
        int reward = RunRewardCalculator.CalculateGold(
            -50f,
            -100f,
            -2f,
            RunEndReason.ReturnedToBunker);

        Assert.That(reward, Is.Zero);
    }

    [Category("Extended")]
    [TestCase(RunEndReason.PlayerDied)]
    [TestCase(RunEndReason.Victory)]
    public void EndThenBegin_HasFreshRunIdentityAndState(RunEndReason reason)
    {
        int oldId = runState.RunId;
        var oldOrbital = runState.OrbitalStationState;
        runState.AdvanceThreat(10f, 2f);
        runState.RegisterUpgrade(Track(ScriptableObject.CreateInstance<UpgradeData>()));
        runState.EndRun(reason, oldId);
        runState.BeginNewRun(null, null, stage, worldRule, anomaly);
        Assert.That(runState.RunId, Is.GreaterThan(oldId));
        Assert.That(runState.OrbitalStationState, Is.Not.SameAs(oldOrbital));
        Assert.That(runState.ThreatValue, Is.Zero);
        Assert.That(runState.PickedUpgrades, Is.Empty);
        Assert.That(runState.CurrentSector.SectorNumber, Is.EqualTo(1));
        Assert.That(runState.ProductionRewardChestSpawned, Is.False);
    }

    [Category("Extended")]
    [Test]
    public void SectorRelease_PreservesRunData_ButClearsSceneReferences()
    {
        var orbital = runState.OrbitalStationState;
        int id = runState.RunId;
        runState.RegisterUpgrade(Track(ScriptableObject.CreateInstance<UpgradeData>()));
        runState.AdvanceThreat(5f, 2f);
        CreateStats(2, 10f);
        runState.CommitCurrentSceneStats();
        bool released = false;
        runState.RegisterSceneCleanup(() => released = true);
        Assert.That(runState.ReleaseCurrentSector(id), Is.True);
        Assert.That(released, Is.True);
        Assert.That(runState.OrbitalStationState, Is.SameAs(orbital));
        Assert.That(runState.PickedUpgrades.Count, Is.EqualTo(1));
        Assert.That(runState.AccumulatedKills, Is.EqualTo(2));
        Assert.That(runState.ThreatValue, Is.EqualTo(10f));
        Assert.That(runState.IsRunEnded, Is.False);
    }

    [Category("Extended")]
    [Test]
    public void CustomCasinoBonus_IsTransferredOnce_EvenIfDrawingNeverCompletes()
    {
        runState.EndRun(RunEndReason.PlayerDied);
        PlayerPrefs.SetInt("ORBITAL_SLOT_PENDING", (int)OrbitalSlotSymbol.Ring);
        var character = Track(ScriptableObject.CreateInstance<CharacterData>());
        character.orbitalPath = Subject42.Combat.OrbitalStation.OrbitalPathType.Custom;
        runState.BeginNewRun(character, null, stage, worldRule, anomaly);
        Assert.That(runState.OrbitalStationState.PendingCasinoRingId, Is.Not.Zero);
        Assert.That(OrbitalSlotMachine.Pending, Is.EqualTo(OrbitalSlotSymbol.None));
        runState.EndRun(RunEndReason.PlayerDied);
        runState.BeginNewRun(character, null, stage, worldRule, anomaly);
        Assert.That(runState.OrbitalStationState.PendingCasinoRingId, Is.Zero);
    }

    [Category("Extended")]
    [Test]
    public void RepeatedOldCleanup_PreservesNewBunkerCasinoWin()
    {
        runState.EndRun(RunEndReason.PlayerDied);
        PlayerPrefs.SetInt("ORBITAL_SLOT_PENDING", (int)OrbitalSlotSymbol.Impulse);
        runState.EndRun(RunEndReason.Victory);
        runState.ClearFinishedRunCompatibilityState();
        Assert.That(OrbitalSlotMachine.Pending, Is.EqualTo(OrbitalSlotSymbol.Impulse));
    }

    [Category("Extended")]
    [Test]
    public void MetaNotification_CannotReenterEndBeginOrConsumeSummary()
    {
        var go = Track(new GameObject("Isolated currency"));
        go.SetActive(false);
        var currency = go.AddComponent<CurrencyManager>();
        CurrencyManager.Instance = currency;
        CreateStats(5, 120f);
        int id = runState.RunId;
        int notifications = 0;
        currency.OnGoldUpdated += amount =>
        {
            notifications++;
            runState.EndRun(RunEndReason.Victory, id);
            runState.BeginNewRun(null, null, stage, worldRule, anomaly);
            Assert.That(runState.TryConsumeLastRunSummary(out _), Is.False);
        };
        var summary = runState.EndRun(RunEndReason.PlayerDied, id);
        Assert.That(runState.EndRun(RunEndReason.PlayerDied, id), Is.SameAs(summary));
        Assert.That(notifications, Is.EqualTo(1));
        Assert.That(currency.TotalGold, Is.EqualTo(summary.GoldEarned));
        Assert.That(runState.RunId, Is.EqualTo(id));
    }

    [Category("Extended")]
    [Test]
    public void DevelopmentRun_DoesNotConsumeCasinoOrCommitGold()
    {
        runState.EndRun(RunEndReason.PlayerDied);
        PlayerPrefs.SetInt("ORBITAL_SLOT_PENDING", (int)OrbitalSlotSymbol.Gun);
        var go = Track(new GameObject("Isolated currency"));
        go.SetActive(false);
        var currency = go.AddComponent<CurrencyManager>();
        CurrencyManager.Instance = currency;
        runState.BeginNewRun(null, null, stage, worldRule, anomaly, isDevelopmentRun: true);
        CreateStats(5, 120f);
        runState.EndRun(RunEndReason.PlayerDied);
        Assert.That(currency.TotalGold, Is.Zero);
        Assert.That(OrbitalSlotMachine.Pending, Is.EqualTo(OrbitalSlotSymbol.Gun));
    }
}
#endif
