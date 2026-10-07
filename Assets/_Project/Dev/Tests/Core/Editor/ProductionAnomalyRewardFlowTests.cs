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

    [UnityTest, Timeout(60000)]
    public IEnumerator SpecialBeamPresentationAndRestoredCoins()
    {
        var config = AssetDatabase.LoadAssetAtPath<ExplorationSectorConfig>(
            "Assets/_Project/Data/World/ExplorationSectorConfig.asset");
        foreach (string path in new[] { "Layout_A", "Layout_C", "Layout_D" })
        {
            var layout = AssetDatabase.LoadAssetAtPath<ExplorationSectorConfig>(
                $"Assets/_Project/Data/SurfaceMap/{path}.asset");
            Assert.That(layout.BeamVisualPrefab, Is.EqualTo(config.BeamVisualPrefab));
            Assert.That(layout.ElectricVisualPrefab, Is.EqualTo(config.ElectricVisualPrefab));
        }
        var player = Object.FindFirstObjectByType<CharacterSpawner>().SpawnedPlayer;
        var movement = player.GetComponent<CharacterMovement2D>();
        movement.MovementIntent = () => Vector2.zero;
        Vector2 center = player.transform.position;
        foreach (var existing in ProductionAnomalySite.ActiveSites.Where(value => value.IsSpecial).ToArray())
            existing.DisposeSite();
        yield return null;

        var coinPrefab = AssetDatabase.LoadAssetAtPath<GoldenCoinPickup>(
            "Assets/_Project/prefabs/Pickups/p_coin1.prefab");
        var coin = Object.Instantiate(coinPrefab, center + Vector2.left * 2f, Quaternion.identity);
        coin.Initialize(player.transform, 1, 30f, 0.01f, 0f, 0f, 0.5f);
        yield return null;
        var coinRenderer = coin.transform.Find("Coin1").GetComponent<SpriteRenderer>();
        Assert.That(coinRenderer.sharedMaterial.shader.name, Is.EqualTo("Sprites/Default"));
        Assert.That(coinRenderer.sharedMaterial.shader.isSupported, Is.True);
        Assert.That(AssetDatabase.GetAssetPath(coinRenderer.sprite), Does.EndWith("Icons3.png"));
        Assert.That(coinRenderer.sprite.texture.filterMode, Is.EqualTo(FilterMode.Point));

        foreach (var power in new[] { AnomalyPowerType.RedBeam, AnomalyPowerType.ArcNode })
        {
            var owner = new GameObject("Special presentation production fixture").AddComponent<ProductionAnomalySite>();
            var spawner = Object.FindFirstObjectByType<WorldEventSpawner>();
            spawner.ConfigureDebugConcurrentEventCapacity(16);
            Assert.That(owner.InitializeSpecial(center, new Vector2(12f, 10f), power,
                spawner.EventPrefabs[0], spawner, Object.FindFirstObjectByType<LevelAnomalyController>(),
                owner.gameObject, config, center + Vector2.one * 100f, 2.5f), Is.True);
            var view = owner.GetComponentInChildren<AnomalyBeamView>();
            Assert.That(view, Is.Not.Null);
            Assert.That(view.GetComponentsInChildren<LineRenderer>(), Is.Empty);
            var hazard = owner.GetComponents<MonoBehaviour>().Single(value =>
                value.GetType().Name is "ProductionBeamSiteHazard" or "ProductionElectricSiteHazard");
            yield return CoreTestSupport.Await(() => view.State == AnomalyBeamView.BeamState.Telegraph);
            hazard.enabled = false;
            float width = power == AnomalyPowerType.RedBeam ? 1.45f : 0.8f;
            Assert.That(view.DamageHalfWidth, Is.EqualTo(width));
            var segment = view.transform.Find("Segment");
            Assert.That(segment.Find("Footprint").GetComponent<SpriteRenderer>().size.y, Is.EqualTo(width * 2f));
            Assert.That(segment.Find("Core").GetComponent<SpriteRenderer>().size.y, Is.LessThan(0.2f));
            foreach (var renderer in view.GetComponentsInChildren<SpriteRenderer>())
            {
                Assert.That(renderer.sprite, Is.Not.Null, renderer.name);
                Assert.That(renderer.sharedMaterial.shader.name, Is.EqualTo("Sprites/Default"));
            }
            yield return CaptureSpecialPresentation(power + "-telegraph");
            hazard.enabled = true;
            yield return CoreTestSupport.Await(() => view.State == AnomalyBeamView.BeamState.Active);
            hazard.enabled = false;
            Vector3 pulse = segment.Find("EnergyPulse1").localPosition;
            yield return new WaitForSeconds(0.05f);
            Assert.That(segment.Find("EnergyPulse1").localPosition, Is.Not.EqualTo(pulse));
            yield return CaptureSpecialPresentation(power + "-active");
            hazard.enabled = true;
            yield return CoreTestSupport.Await(() => view.State == AnomalyBeamView.BeamState.Ending);
            hazard.enabled = false;
            yield return CoreTestSupport.Await(() => view.State == AnomalyBeamView.BeamState.Inactive);
            Assert.That(view.State, Is.EqualTo(AnomalyBeamView.BeamState.Inactive));
            Assert.That(segment.Find("Core").GetComponent<SpriteRenderer>().enabled, Is.False);
            owner.DisposeSite();
            yield return null;
            Assert.That(view == null, Is.True, "Owner reset must remove all owned beam visuals.");
        }
        Object.Destroy(coin.gameObject);
    }

    private static IEnumerator CaptureSpecialPresentation(string name)
    {
        const string folder = "Artifacts/GeneratedQA/SpecialAnomalies/";
        System.IO.Directory.CreateDirectory(folder);
        string path = folder + name + ".png";
        if (System.IO.File.Exists(path)) System.IO.File.Delete(path);
        yield return new WaitForSecondsRealtime(0.3f);
        ScreenCapture.CaptureScreenshot(path);
        yield return CoreTestSupport.Await(() => System.IO.File.Exists(path));
    }

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
