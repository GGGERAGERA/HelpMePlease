#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using NUnit.Framework;
using Subject42.Combat.OrbitalStation;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

[Category("Core")]
public sealed class ProductionAnomalyRewardFlowTests
{
    [SetUp]
    public void PreservePreferences() => CoreTestSupport.PreservePreferences();

    [UnitySetUp]
    public IEnumerator BeginRun() => CoreTestSupport.BeginRun();

    [UnityTearDown]
    public IEnumerator Cleanup() => CoreTestSupport.CleanupPlayMode();

    [UnityTest]
    public IEnumerator NormalAnomalyOpensCardsAndCompletesAfterTheDisplayedRewardIsGranted()
    {
        yield return CoreTestSupport.Await(() => ProductionAnomalySite.ActiveSites.Any(value => value != null && !value.IsSpecial));
        var site = ProductionAnomalySite.ActiveSites.First(value => value != null && !value.IsSpecial);
        int containers = WorldBreakable.ActiveInstances.Count;
        CompleteSiteEvent(site);
        Assert.That(UpgradeManager.Instance.DebugCurrentChoices, Has.Count.EqualTo(3));
        Assert.That(WorldBreakable.ActiveInstances.Count, Is.EqualTo(containers));
        Assert.That(site.IsCompleted, Is.False, "The site must wait for the reward grant.");
        yield return RewardScenarioAssertions.ClickDisplayedCardAndVerifyGrant();
        Assert.That(site.IsCompleted, Is.True);
        Assert.That(site.IsMapVisible, Is.False);
    }

    [UnityTest]
    public IEnumerator SpecialAnomalyGrantsOneRingAfterBothEvents()
    {
        yield return CoreTestSupport.Await(() => ProductionAnomalySite.ActiveSites.Any(value => value != null && value.IsSpecial));
        var site = ProductionAnomalySite.ActiveSites.Single(value => value != null && value.IsSpecial);
        var station = Object.FindFirstObjectByType<OrbitalStationRuntime>();
        int rings = station.State.Rings.Count;
        CompleteSiteEvent(site);
        Assert.That(site.CompletedMainEvents, Is.EqualTo(1));
        Assert.That(site.IsCompleted, Is.False);
        Assert.That(station.State.Rings.Count, Is.EqualTo(rings));
        yield return CoreTestSupport.Await(() => Object.FindFirstObjectByType<WorldEventSpawner>()
            .SpawnedEvents.Any(value => value != null && site.ContainsWorldPosition(value.transform.position)));
        CompleteSiteEvent(site);
        yield return RewardScenarioAssertions.FinishReward();
        Assert.That(site.CompletedMainEvents, Is.EqualTo(2));
        Assert.That(site.IsCompleted, Is.True);
        Assert.That(station.State.Rings.Count, Is.EqualTo(rings + 1));
        Assert.That(station.State.Validate(out string error), Is.True, error);
    }

    private static void CompleteSiteEvent(ProductionAnomalySite site)
    {
        var spawner = Object.FindFirstObjectByType<WorldEventSpawner>();
        Assert.That(spawner, Is.Not.Null);
        var worldEvent = spawner.SpawnedEvents.Single(value => value != null &&
            site.ContainsWorldPosition(value.transform.position));
        spawner.NotifyEventCompleted(worldEvent);
    }
}
#endif
