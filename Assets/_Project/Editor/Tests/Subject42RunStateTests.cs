#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public sealed class Subject42RunStateTests
{
    private readonly List<UnityEngine.Object> cleanup = new();
    private RunStateManager runState;
    private StageProfileData stage;
    private WorldRuleData worldRule;
    private LocalAnomalyData anomaly;
    private int savedCasinoPending;
    private bool hadCasinoPending;
    private CurrencyManager savedCurrency;
    private int savedGold;
    private bool hadGold;

    [SetUp]
    public void SetUp()
    {
        ResetStatics();
        savedCurrency = CurrencyManager.Instance;
        CurrencyManager.Instance = null;
        hadGold = PlayerPrefs.HasKey("TOTAL_GOLD");
        savedGold = PlayerPrefs.GetInt("TOTAL_GOLD", 0);
        hadCasinoPending = PlayerPrefs.HasKey("ORBITAL_SLOT_PENDING");
        savedCasinoPending = PlayerPrefs.GetInt("ORBITAL_SLOT_PENDING", 0);
        PlayerPrefs.DeleteKey("ORBITAL_SLOT_PENDING");
        stage = Track(ScriptableObject.CreateInstance<StageProfileData>());
        worldRule = Track(ScriptableObject.CreateInstance<WorldRuleData>());
        anomaly = Track(ScriptableObject.CreateInstance<LocalAnomalyData>());
        runState = AddComponent<RunStateManager>("RunState Test");
        SetStaticProperty(typeof(RunStateManager), "Instance", runState);
        runState.BeginNewRun(null, null, stage, worldRule, anomaly);
    }

    [TearDown]
    public void TearDown()
    {
        for (int i = cleanup.Count - 1; i >= 0; i--)
        {
            if (cleanup[i] != null)
                UnityEngine.Object.DestroyImmediate(cleanup[i]);
        }

        cleanup.Clear();
        if (hadCasinoPending) PlayerPrefs.SetInt("ORBITAL_SLOT_PENDING", savedCasinoPending);
        else PlayerPrefs.DeleteKey("ORBITAL_SLOT_PENDING");
        if (hadGold) PlayerPrefs.SetInt("TOTAL_GOLD", savedGold);
        else PlayerPrefs.DeleteKey("TOTAL_GOLD");
        CurrencyManager.Instance = savedCurrency;
        PlayerPrefs.Save();
        ResetStatics();
    }

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

    [Test]
    public void EndRun_IsIdempotent_AndUsesCommittedRewardTotals()
    {
        CreateStats(5, 120f);
        runState.RegisterCompletedLevel();

        RunSummary first = runState.EndRun(RunEndReason.PlayerDied);
        RunSummary second = runState.EndRun(RunEndReason.PlayerDied);

        Assert.That(second, Is.SameAs(first));
        Assert.That(first.EndReason, Is.EqualTo(RunEndReason.PlayerDied));
        Assert.That(first.CompletedLevels, Is.EqualTo(1));
        Assert.That(first.Kills, Is.EqualTo(5));
        Assert.That(first.RunTime, Is.EqualTo(120f));
        Assert.That(first.GoldEarned, Is.EqualTo(83));
        Assert.That(runState.IsRunEnded, Is.True);
    }

    [Test]
    public void BeginNewRun_ClearsPreviousRun_AndIgnoresOldSceneStats()
    {
        CreateStats(6, 90f);
        runState.CommitCurrentSceneStats();
        runState.RegisterCompletedLevel();
        runState.EndRun(RunEndReason.ReturnedToBunker);

        runState.BeginNewRun(null, null, stage, worldRule, anomaly);

        Assert.That(runState.IsRunEnded, Is.False);
        Assert.That(runState.AccumulatedKills, Is.Zero);
        Assert.That(runState.AccumulatedRunTime, Is.Zero);
        Assert.That(runState.CompletedLevels, Is.Zero);
        Assert.That(runState.GetCurrentRunKills(), Is.Zero);
        Assert.That(runState.GetCurrentRunTime(), Is.Zero);
        Assert.That(runState.CurrentLevel, Is.EqualTo(1));
        Assert.That(runState.CurrentSector, Is.Not.Null);
        Assert.That(runState.CurrentSector.SectorNumber, Is.EqualTo(1));
        Assert.That(
            runState.TryConsumeLastRunSummary(out _),
            Is.False);
    }

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

    [Test]
    public void OldEndSignal_CannotEndReplacementRun()
    {
        int oldId = runState.RunId;
        runState.RestartRun(RunEndReason.PlayerDied, oldId);
        Assert.That(runState.EndRun(RunEndReason.Victory, oldId), Is.Null);
        Assert.That(runState.IsRunEnded, Is.False);
        Assert.That(runState.CurrentSector.SectorNumber, Is.EqualTo(1));
    }

    [Test]
    public void Cleanup_CancelsRewardsBeforeGameplay_AndRunsOnce()
    {
        var calls = new List<string>();
        runState.RegisterSceneCleanup(() => calls.Add("orbital"));
        runState.RegisterSceneCleanup(() => calls.Add("rewards"), RunSceneCleanupPhase.Rewards);
        runState.EndRun(RunEndReason.PlayerDied);
        runState.EndRun(RunEndReason.Victory);
        CollectionAssert.AreEqual(new[] { "rewards", "orbital" }, calls);
        Assert.That(Time.timeScale, Is.EqualTo(1f));
    }

    [Test]
    public void ReentrantBeginDuringCleanup_CannotReplaceRun()
    {
        int id = runState.RunId;
        runState.RegisterSceneCleanup(() => runState.BeginNewRun(null, null, stage, worldRule, anomaly));
        runState.EndRun(RunEndReason.PlayerDied);
        Assert.That(runState.RunId, Is.EqualTo(id));
        Assert.That(runState.IsRunEnded, Is.True);
        Assert.That(runState.OrbitalStationState, Is.Null);
    }

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

    [Test]
    public void RepeatedOldCleanup_PreservesNewBunkerCasinoWin()
    {
        runState.EndRun(RunEndReason.PlayerDied);
        PlayerPrefs.SetInt("ORBITAL_SLOT_PENDING", (int)OrbitalSlotSymbol.Impulse);
        runState.EndRun(RunEndReason.Victory);
        runState.ClearFinishedRunCompatibilityState();
        Assert.That(OrbitalSlotMachine.Pending, Is.EqualTo(OrbitalSlotSymbol.Impulse));
    }

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

    private RunStatsManager CreateStats(int kills, float runTime)
    {
        RunStatsManager stats = AddComponent<RunStatsManager>("Stats Test");
        InvokeLifecycle(stats, "Awake");

        for (int i = 0; i < kills; i++)
            stats.AddKill();

        SetAutoProperty(stats, nameof(RunStatsManager.RunTime), runTime);
        return stats;
    }

    private T AddComponent<T>(string objectName) where T : MonoBehaviour
    {
        GameObject gameObject = Track(new GameObject(objectName));
        return gameObject.AddComponent<T>();
    }

    private T Track<T>(T item) where T : UnityEngine.Object
    {
        cleanup.Add(item);
        return item;
    }

    private void DestroyTracked(UnityEngine.Object item)
    {
        cleanup.Remove(item);
        if (item != null)
            UnityEngine.Object.DestroyImmediate(item);
    }

    private static void SetAutoProperty(
        object target,
        string propertyName,
        object value)
    {
        FieldInfo field = target.GetType().GetField(
            $"<{propertyName}>k__BackingField",
            BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.That(field, Is.Not.Null);
        field.SetValue(target, value);
    }

    private static void InvokeLifecycle(object target, string methodName)
    {
        MethodInfo method = target.GetType().GetMethod(
            methodName,
            BindingFlags.Instance | BindingFlags.NonPublic);
        method?.Invoke(target, null);
    }

    private static void ResetStatics()
    {
        RunStatsManager.Instance = null;
        KillManager.Instance = null;
        SetStaticProperty(typeof(RunStateManager), "Instance", null);
    }

    private static void SetStaticProperty(
        Type type,
        string propertyName,
        object value)
    {
        PropertyInfo property = type.GetProperty(
            propertyName,
            BindingFlags.Static | BindingFlags.Public);
        property?.SetValue(null, value);
    }
}
#endif
