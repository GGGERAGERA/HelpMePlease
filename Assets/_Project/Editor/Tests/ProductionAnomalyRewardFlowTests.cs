#if UNITY_EDITOR
using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Subject42.Combat.OrbitalStation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

public sealed class ProductionAnomalyRewardFlowTests
{
    private readonly Subject42FinalBossFlowTests preferences = new();

    [SetUp]
    public void PreservePreferences() =>
        preferences.PreserveRewardsAndUnlockProgress();

    [UnityTearDown]
    public IEnumerator Cleanup() => preferences.CleanupPlayMode();

    [TearDown]
    public void RestoreLogChecks() => LogAssert.ignoreFailingMessages = false;

    [UnityTest]
    public IEnumerator SitesResolveRewardsWithoutPhysicalContainers()
    {
        LogAssert.ignoreFailingMessages = true;
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
        yield return new EnterPlayMode();
        yield return SceneManager.LoadSceneAsync("MainMenu");
        float deadline = Time.realtimeSinceStartup + 30f;
        while ((Object.FindFirstObjectByType<BunkerRunStarter>() == null ||
                SceneTransitionOverlay.IsTransitioning) &&
            Time.realtimeSinceStartup < deadline)
            yield return null;
        Assert.That(Object.FindFirstObjectByType<BunkerRunStarter>(), Is.Not.Null);
        var character = AssetDatabase.FindAssets("t:CharacterData")
            .Select(id => AssetDatabase.LoadAssetAtPath<CharacterData>(
                AssetDatabase.GUIDToAssetPath(id)))
            .First(data => data.characterName == "Gera");
        RunSelectionManager.Instance.SelectCharacter(character);
        var starter = Object.FindFirstObjectByType<BunkerRunStarter>();
        starter.StartRun(Get<Transform>(starter, "cameraRig"));
        deadline = Time.realtimeSinceStartup + 30f;
        while (SceneManager.GetActiveScene().name != "MVP" &&
            Time.realtimeSinceStartup < deadline)
            yield return null;
        Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("MVP"));
        deadline = Time.realtimeSinceStartup + 30f;
        while ((!ProductionAnomalySite.ActiveSites.Any(site =>
                    site != null && !site.IsSpecial) ||
                ProductionAnomalySite.ActiveSites.Count(site =>
                    site != null && site.IsSpecial) != 1 ||
                UpgradeManager.Instance == null ||
                Object.FindFirstObjectByType<OrbitalStationRuntime>() == null) &&
            Time.realtimeSinceStartup < deadline)
            yield return null;
        Assert.That(ProductionAnomalySite.ActiveSites.Any(site =>
            site != null && !site.IsSpecial), Is.True, "normal production site");
        Assert.That(ProductionAnomalySite.ActiveSites.Count(site =>
            site != null && site.IsSpecial), Is.EqualTo(1), "special production site");
        var upgrades = UpgradeManager.Instance;
        var station = Object.FindFirstObjectByType<OrbitalStationRuntime>();
        Assert.That(upgrades, Is.Not.Null, "production UpgradeManager");
        Assert.That(station, Is.Not.Null, "production OrbitalStationRuntime");

        var normal = ProductionAnomalySite.ActiveSites.First(site =>
            site != null && !site.IsSpecial);
        var normalEvent = Get<WorldEvent>(normal, "activeEvent");
        var normalEvents = Get<WorldEventSpawner>(normal, "eventSpawner");
        Assert.That(normalEvent, Is.Not.Null, "normal site event");
        Assert.That(normalEvents, Is.Not.Null, "normal site event owner");
        int containersBeforeNormal = WorldBreakable.ActiveInstances.Count;

        normalEvents.NotifyEventCompleted(normalEvent);

        Assert.That(WorldBreakable.ActiveInstances.Count,
            Is.EqualTo(containersBeforeNormal),
            "Normal anomaly must not spawn an EventRewardContainer.");
        Assert.That(upgrades.DebugCurrentChoices, Is.Not.Null,
            "Normal anomaly must open its reward cards immediately.");
        Assert.That(upgrades.DebugCurrentChoices.Count, Is.EqualTo(3));
        Assert.That(GetRequestField<bool>(upgrades, "NumericOnly"), Is.True);
        Assert.That(normal.IsCompleted, Is.False,
            "The site remains active until the reward choice closes.");

        Assert.That(upgrades.DebugSelectCurrentChoice(0), Is.True);
        Assert.That(normal.IsCompleted, Is.True);
        Assert.That(upgrades.IsRewardQueueIdle, Is.True);
        Assert.That(normal.IsMapVisible, Is.False);

        var special = ProductionAnomalySite.ActiveSites.Single(site =>
            site != null && site.IsSpecial);
        int ringsBeforeSpecial = station.State.Rings.Count;
        int containersBeforeSpecial = WorldBreakable.ActiveInstances.Count;
        var firstSpecialEvent = Get<WorldEvent>(special, "activeEvent");
        var specialEvents = Get<WorldEventSpawner>(special, "eventSpawner");
        Set(special, "completedMainEvents", 1);
        specialEvents.NotifyEventCompleted(firstSpecialEvent);

        Assert.That(WorldBreakable.ActiveInstances.Count,
            Is.EqualTo(containersBeforeSpecial),
            "Special anomaly must retain its direct Ring flow without a container.");
        Assert.That(special.IsCompleted, Is.True);
        Assert.That(station.State.Rings.Count, Is.EqualTo(ringsBeforeSpecial + 1));
        Assert.That(upgrades.IsRewardQueueIdle, Is.True);
    }

    private static IEnumerator Await(Func<bool> condition)
    {
        float deadline = Time.realtimeSinceStartup + 30f;
        while (!condition() && Time.realtimeSinceStartup < deadline)
            yield return null;
        Assert.That(condition(), Is.True, "Timed out waiting for production flow.");
    }

    private static T Get<T>(object target, string fieldName) =>
        (T)target.GetType().GetField(
            fieldName,
            BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);

    private static void Set(object target, string fieldName, object value) =>
        target.GetType().GetField(fieldName,
            BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);

    private static T GetRequestField<T>(UpgradeManager upgrades, string fieldName)
    {
        object request = upgrades.DebugCurrentRewardRequest;
        Assert.That(request, Is.Not.Null);
        return (T)request.GetType().GetField(
            fieldName,
            BindingFlags.Instance | BindingFlags.Public).GetValue(request);
    }
}
#endif
