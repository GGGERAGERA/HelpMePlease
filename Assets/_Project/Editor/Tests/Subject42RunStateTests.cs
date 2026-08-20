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

    [SetUp]
    public void SetUp()
    {
        ResetStatics();
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
        ResetStatics();
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
