#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Subject42.Combat.OrbitalStation;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEditor;
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
        yield return CompleteSiteEvent(site);
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
        yield return CompleteSiteEvent(site);
        Assert.That(site.CompletedMainEvents, Is.EqualTo(1));
        Assert.That(site.IsCompleted, Is.False);
        Assert.That(station.State.Rings.Count, Is.EqualTo(rings));
        yield return ResolveNonRingSelection(station);
        yield return CoreTestSupport.Await(() => Object.FindFirstObjectByType<WorldEventSpawner>()
            .SpawnedEvents.Any(value => value != null && site.ContainsWorldPosition(value.transform.position)));
        yield return CompleteSiteEvent(site);
        yield return ResolveNonRingSelection(station);
        Assert.That(site.CompletedMainEvents, Is.EqualTo(2));
        Assert.That(site.IsCompleted, Is.True);
        Assert.That(station.State.Rings.Count, Is.EqualTo(rings + 1));
        Assert.That(station.State.Validate(out string error), Is.True, error);
    }

    private static IEnumerator CompleteSiteEvent(ProductionAnomalySite site)
    {
        var spawner = Object.FindFirstObjectByType<WorldEventSpawner>();
        Assert.That(spawner, Is.Not.Null);
        const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        var active = typeof(ProductionAnomalySite).GetField("activeEvent", Private);
        var worldEvent = (WorldEvent)active.GetValue(site);
        var prefab = AssetDatabase.LoadAssetAtPath<OrbitalRelayEvent>(OrbitalRelayAuthoring.PrefabPath);
        // Select this fixture's event type before gameplay; don't manufacture completion or progress.
        if (worldEvent is not OrbitalRelayEvent)
        {
            if (worldEvent != null)
            {
                typeof(WorldEvent).GetMethod("DisposeForOwnerReset", Private).Invoke(worldEvent, null);
                Object.Destroy(worldEvent.gameObject);
            }
            active.SetValue(site, null);
            typeof(ProductionAnomalySite).GetField("eventPrefab", Private).SetValue(site, prefab);
            Assert.That((bool)typeof(ProductionAnomalySite).GetMethod("SpawnEvent", Private).Invoke(site, null), Is.True);
            worldEvent = (WorldEvent)active.GetValue(site);
        }
        var relay = (OrbitalRelayEvent)worldEvent;
        var station = Object.FindFirstObjectByType<OrbitalStationRuntime>();
        var movement = station.Owner.Transform.GetComponent<CharacterMovement2D>();
        var previous = movement.MovementIntent;
        OrbitalRelayResult? result = null;
        relay.Finished += value => result = value;
        movement.MovementIntent = () => relay == null ? Vector2.zero : relay.IsStarted
            ? OrbitalRelayBotSteering.GetDesiredMovement(relay, station, movement.transform.position)
            : Vector2.ClampMagnitude(relay.transform.position - movement.transform.position, 1f);
        try
        {
            float deadline = Time.realtimeSinceStartup + 45f;
            while (!result.HasValue && Time.realtimeSinceStartup < deadline)
            {
                if (relay != null && relay.CanInteract) relay.Interact();
                yield return null;
            }
            Assert.That(result.HasValue && result.Value.Success, Is.True, "Site Relay must finish through real movement/body contacts.");
        }
        finally { movement.MovementIntent = previous; }
    }
    private static IEnumerator ResolveNonRingSelection(OrbitalStationRuntime station)
    {
        int choice = UpgradeManager.Instance.DebugCurrentChoices.ToList().FindIndex(value =>
            value is OrbitalRewardData orbital && orbital.RewardKind != OrbitalRewardKind.NewRing);
        Assert.That(choice, Is.GreaterThanOrEqualTo(0));
        Assert.That(UpgradeManager.Instance.DebugSelectCurrentChoice(choice), Is.True);
        yield return RewardScenarioAssertions.FinishReward();
    }
}
#endif
