#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public sealed partial class Subject42RunStateTests
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

    [Category("Core")]
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

    [Category("Core")]
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

    [Category("Core")]
    [Test]
    public void OldEndSignal_CannotEndReplacementRun()
    {
        int oldId = runState.RunId;
        runState.RestartRun(RunEndReason.PlayerDied, oldId);
        Assert.That(runState.EndRun(RunEndReason.Victory, oldId), Is.Null);
        Assert.That(runState.IsRunEnded, Is.False);
        Assert.That(runState.CurrentSector.SectorNumber, Is.EqualTo(1));
    }

    [Category("Core")]
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

    [Category("Core")]
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
