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
    public IEnumerator CombatRewardPauseAndRestartLifecycleSmoke()
    {
        const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        var station = Object.FindFirstObjectByType<OrbitalStationRuntime>();
        var spawner = Object.FindFirstObjectByType<WorldEventSpawner>();
        var sector = ProductionExplorationSectorController.ActiveInstance;
        Object.FindFirstObjectByType<EnemySpawner>().StopSpawning();
        station.Owner.Transform.GetComponent<CharacterMovement2D>().MovementIntent = () => Vector2.zero;
        station.enabled = false; // Advance the production clock explicitly below.
        var ring = station.Rings[0];
        Assert.That(station.AddMount(ring.RingId, out string error), Is.True, error);
        var mount = ring.Mounts.First(value => value.Module == null);
        Assert.That(station.InstallModule(OrbitalModuleKind.ImpulseGun, ring.RingId, mount.MountIndex, out error), Is.True, error);
        var impulse = station.Modules.First(value => value.Kind == OrbitalModuleKind.ImpulseGun);
        var enemy = new GameObject("Phase 1 combat target").AddComponent<EnemyHealth>();
        enemy.SetRuntimeMaxHealth(10000f);
        enemy.transform.position = impulse.WorldPosition;
        var cooldown = typeof(OrbitalModuleRuntime).GetProperty("RuntimeCooldown", Private);
        foreach (var module in station.Modules) cooldown.SetValue(module, 100f);
        var reward = ScriptableObject.CreateInstance<OrbitalRewardData>();
        reward.RewardKind = OrbitalRewardKind.RingSpeed; reward.RequiresArenaSelection = true;
        float health = enemy.CurrentHealth;
        bool committed = false;
        Assert.That(station.RewardFlow.Begin(reward, () => committed = true, () => {}), Is.True);
        Assert.That(committed, Is.True);
        Assert.That(enemy.CurrentHealth, Is.EqualTo(health), "Reward feedback cannot activate mounted combat.");
        var pause = Object.FindFirstObjectByType<PauseMenuUI>();
        pause.Pause();
        cooldown.SetValue(impulse, 0f);
        var tick = typeof(OrbitalStationRuntime).GetMethod("TickStation", Private);
        tick.Invoke(station, new object[] { 0f });
        Assert.That(enemy.CurrentHealth, Is.EqualTo(health), "Paused module refresh cannot attack.");
        foreach (var module in station.Modules) cooldown.SetValue(module, 100f);
        Assert.That(station.AddMount(ring.RingId, out error), Is.True, error);
        var other = station.AddRing();
        Assert.That(station.InstallLinkPair(ring.RingId,
            ring.Mounts.First(value => value.Module == null).MountIndex,
            other.StableRingId, 0, out error), Is.True, error);
        var links = station.Modules.Where(value => value.Kind == OrbitalModuleKind.LinkNode).ToArray();
        enemy.transform.position = (links[0].WorldPosition + links[1].WorldPosition) * .5f;
        tick.Invoke(station, new object[] { 0f });
        Assert.That(enemy.CurrentHealth, Is.EqualTo(health), "Paused link refresh cannot damage a target on the segment.");
        station.Combat.SpawnProjectile(enemy.transform.position, enemy, 13f, 7f, Color.white);
        station.Combat.Tick(0f);
        Assert.That(enemy.CurrentHealth, Is.EqualTo(health), "Paused refresh cannot attack or resolve a projectile hit.");
        pause.Resume();
        tick.Invoke(station, new object[] { .02f });
        Assert.That(enemy.CurrentHealth, Is.LessThan(health), "Combat must resume on a positive gameplay delta.");
        Object.Destroy(enemy.gameObject); Object.Destroy(reward);
        station.enabled = true;

        var site = ProductionAnomalySite.ActiveSites.First(value => !value.IsSpecial);
        yield return CompleteSiteEvent(site);
        yield return RewardScenarioAssertions.ClickDisplayedCardAndVerifyGrant();
        Assert.That(site.IsCompleted, Is.True);
        var sites = ProductionAnomalySite.ActiveSites.ToArray();
        var events = spawner.SpawnedEvents.ToArray();
        var independent = new GameObject("Disabled spawner owned event").AddComponent<RelayPressureTestEvent>();
        independent.Initialize(spawner);
        ((System.Collections.Generic.List<WorldEvent>)typeof(WorldEventSpawner).GetField("spawnedEvents", Private)
            .GetValue(spawner)).Add(independent);
        spawner.enabled = false;
        var run = RunStateManager.Instance;
        Assert.That(run.RestartRun(RunEndReason.ReturnedToBunker, run.RunId), Is.True);
        // Repeating cleanup on the same owner must be harmless before Destroy is flushed.
        typeof(ProductionExplorationSectorController).GetMethod("ReleaseRunScene", Private).Invoke(sector, null);
        yield return null;
        Assert.That(sites.All(value => value == null), Is.True, "Restart must remove owned sites.");
        Assert.That(events.All(value => value == null), Is.True, "Restart must remove owned events.");
        Assert.That(independent == null, Is.True, "A disabled spawner retains cleanup responsibility for its events.");
        Assert.That(spawner.SpawnedEvents, Is.Empty);
        Assert.That(Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None)
            .Any(value => value.GetType().Name is "ProductionBeamSiteHazard" or "ProductionElectricSiteHazard"), Is.False);
    }

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
        Assert.That(station, Is.Not.Null, "Production station must survive site selection.");
        Assert.That(station.State, Is.Not.Null, "Production station must retain run state.");
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

    [UnityTest]
    public IEnumerator AssaultAdmissionAndActualSpawnHaveSeparateOwnedResults()
    {
        const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        var enemies = Object.FindFirstObjectByType<EnemySpawner>();
        void Set(string name, object value) => typeof(EnemySpawner).GetField(name, Private).SetValue(enemies, value);
        void Tick() => typeof(EnemySpawner).GetMethod("UpdateAssaultEvents", Private).Invoke(enemies, null);
        Set("assaultEventsEnabled", false);
        Set("assaultBreathDuration", 0f);
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/prefabs/Enemies/p_Enemy_classic.prefab");
        foreach (string name in new[] { "assaultBasicPrefab", "assaultFastPrefab", "assaultBomberPrefab", "assaultShooterPrefab" })
            Set(name, prefab);
        foreach (string name in new[] { "bomberRushCount", "shooterSquadCount", "encirclementCount", "crossfireCountPerSide", "stampedeCount" })
            Set(name, Vector2Int.one);
        var player = Object.FindFirstObjectByType<CharacterSpawner>().SpawnedPlayer;
        player.GetComponent<Rigidbody2D>().position = Vector2.zero;
        player.transform.position = Vector2.zero;
        var camera = Object.FindFirstObjectByType<CameraFollow>().ControlledCamera;
        camera.transform.position = new Vector3(0, 0, -10);
        var results = new System.Collections.Generic.List<AssaultRequestResult>();

        Time.timeScale = 0f;
        Assert.That(enemies.RequestRandomSiteAssault(results.Add).Status, Is.EqualTo(AssaultRequestStatus.Rejected));
        Time.timeScale = 1f;
        var accepted = enemies.RequestRandomSiteAssault(results.Add);
        Assert.That(accepted.Status, Is.EqualTo(AssaultRequestStatus.Accepted));
        Assert.That(accepted.SpawnedCount, Is.Zero);
        Assert.That(results, Is.Empty, "Admission does not acknowledge an enemy spawn.");
        Assert.That(enemies.RequestRandomSiteAssault(results.Add).Status, Is.EqualTo(AssaultRequestStatus.Rejected));
        enemies.CancelAssaultRequest(accepted.RequestId + 1);
        Tick();
        Assert.That(results, Has.Count.EqualTo(1));
        Assert.That(results[0].Status, Is.EqualTo(AssaultRequestStatus.Spawned));
        Assert.That(results[0].SpawnedCount, Is.GreaterThan(0));
        Assert.That(results[0].RequestId, Is.EqualTo(accepted.RequestId));
        Assert.That(enemies.IsAssaultActive, Is.True);
        Assert.That(enemies.HasStartedFirstAutomaticAssault, Is.False);

        enemies.StopSpawning(); enemies.ResumeSpawning();
        var pending = enemies.RequestRandomSiteAssault(results.Add);
        enemies.CancelAssaultRequest(accepted.RequestId);
        Assert.That(results, Has.Count.EqualTo(1), "A stale request cannot cancel a new owner.");
        enemies.CancelAssaultRequest(pending.RequestId); enemies.CancelAssaultRequest(pending.RequestId);
        Assert.That(results, Has.Count.EqualTo(2));
        Assert.That(results[1].Status, Is.EqualTo(AssaultRequestStatus.Cancelled));

        Set("assaultMinDistance", 10000f);
        accepted = enemies.RequestRandomSiteAssault(results.Add);
        Assert.That(accepted.Status, Is.EqualTo(AssaultRequestStatus.Accepted));
        Tick();
        Assert.That(results, Has.Count.EqualTo(3));
        Assert.That(results[2].Status, Is.EqualTo(AssaultRequestStatus.Failed));
        Assert.That(results[2].SpawnedCount, Is.Zero);
        Assert.That(enemies.IsAssaultActive, Is.False);
        Assert.That(enemies.HasStartedFirstAutomaticAssault, Is.False);
        enemies.StopSpawning(); enemies.ResumeSpawning();
        accepted = enemies.RequestRandomSiteAssault(results.Add);
        Assert.That(accepted.Status, Is.EqualTo(AssaultRequestStatus.Accepted));
        enemies.enabled = false;
        Assert.That(results, Has.Count.EqualTo(4));
        Assert.That(results[3].Status, Is.EqualTo(AssaultRequestStatus.Cancelled));
        Tick();
        Assert.That(results, Has.Count.EqualTo(4));
        yield return null;
    }

    [UnityTest]
    public IEnumerator ControllerClearCannotRetireSiteOwnedEnvironment()
    {
        var site = ProductionAnomalySite.ActiveSites.First(value => !value.IsSpecial && value.AnomalyZone != null);
        var neighbor = ProductionAnomalySite.ActiveSites.First(value => value != site && !value.IsSpecial && value.AnomalyZone != null);
        var zone = site.AnomalyZone; var otherZone = neighbor.AnomalyZone;
        var controller = Object.FindFirstObjectByType<LevelAnomalyController>();
        const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        var data = (LocalAnomalyData)typeof(LocalAnomalyZone).GetProperty("Data", Private).GetValue(zone);
        controller.NotifyLocalZoneEntered(zone, data);
        controller.Clear();
        yield return null;
        Assert.That(typeof(LevelAnomalyController).GetField("displayedLocalAnomaly", Private).GetValue(controller),
            Is.SameAs(data), "A surviving occupied environment retains its anomaly card after ambient clear.");
        controller.enabled = false; controller.enabled = true;
        Assert.That(typeof(LevelAnomalyController).GetField("displayedLocalAnomaly", Private).GetValue(controller), Is.SameAs(data));
        Assert.That(zone != null && zone.FocusArea.enabled, Is.True, "Controller reset cannot destroy another owner's environment.");
        var objective = (WorldEvent)typeof(ProductionAnomalySite).GetField("activeEvent", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(site);
        Assert.That(objective.transform.IsChildOf(site.transform), Is.True, "A site event lives under its lifetime owner.");
        site.DisposeSite(); site.DisposeSite();
        yield return null;
        Assert.That(zone == null || !zone.gameObject.activeInHierarchy, Is.True);
        Assert.That(otherZone != null && otherZone.FocusArea.enabled, Is.True, "Neighbouring site retains its independent environment.");
        controller.NotifyLocalZoneEntered(otherZone,
            (LocalAnomalyData)typeof(LocalAnomalyZone).GetProperty("Data", Private).GetValue(otherZone));
        Object.Destroy(controller);
        yield return null;
        Assert.DoesNotThrow(() => controller.NotifyLocalZoneExited(otherZone), "Scene unload may retire the controller before a site zone exits.");
    }

    [UnityTest]
    public IEnumerator SectorResetAndUnloadRetireAllDirectlyOwnedRoots()
    {
        const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        var sector = Object.FindFirstObjectByType<ProductionExplorationSectorController>();
        T Read<T>(string name) => (T)typeof(ProductionExplorationSectorController).GetField(name, Private).GetValue(sector);
        var area = Read<GameplayAreaService>("gameplayArea");
        var enemies = Read<EnemySpawner>("enemySpawner");
        var events = Read<WorldEventSpawner>("eventSpawner");
        var anomalies = Read<LevelAnomalyController>("anomalyController");
        var flow = Read<RunFlowController>("runFlow");
        var config = sector.Config;
        Assert.That(sector.DebugSpawnRewardChestNearPlayer(), Is.True);
        var roots = sector.transform.Cast<Transform>().Select(value => value.gameObject).ToArray();
        Assert.That(roots.Any(value => value.GetComponent<WorldLootChest>() != null), Is.True);
        var cleanup = typeof(ProductionExplorationSectorController).GetMethod("ReleaseRunScene", Private);
        cleanup.Invoke(sector, null); cleanup.Invoke(sector, null);
        yield return null;
        Assert.That(roots.All(value => value == null), Is.True, "Reset covers chests as well as sites, exit, props, resources and portals.");
        Assert.That(events.SpawnedEvents, Is.Empty);
        Assert.That(sector.Initialize(config, area, enemies, events, anomalies, flow), Is.True);
        yield return null;
        Assert.That(ProductionAnomalySite.ActiveSites, Has.Count.EqualTo(7));
        roots = sector.transform.Cast<Transform>().Select(value => value.gameObject).ToArray();
        var scene = sector.gameObject.scene;
        UnityEngine.SceneManagement.SceneManager.SetActiveScene(UnityEngine.SceneManagement.SceneManager.CreateScene("World owner unload check"));
        yield return UnityEngine.SceneManagement.SceneManager.UnloadSceneAsync(scene);
        Assert.That(roots.All(value => value == null), Is.True);
        Assert.That(ProductionAnomalySite.ActiveSites, Is.Empty);
        Assert.That(Object.FindObjectsByType<WorldEvent>(FindObjectsSortMode.None), Is.Empty);
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
            site.ClearObjectiveForDebug();
            typeof(ProductionAnomalySite).GetField("eventPrefab", Private).SetValue(site, prefab);
            Assert.That((bool)typeof(ProductionAnomalySite).GetMethod("SpawnEvent", Private).Invoke(site, null), Is.True);
            worldEvent = (WorldEvent)active.GetValue(site);
        }
        var relay = (OrbitalRelayEvent)worldEvent;
        var station = Object.FindFirstObjectByType<OrbitalStationRuntime>();
        Assert.That(station.IsInitialized, Is.True, "Relay fixture needs the active player station.");
        Assert.That(station.Owner, Is.Not.Null, "Relay fixture needs its production player owner.");
        var movement = station.Owner.Transform.GetComponent<CharacterMovement2D>();
        var previous = movement.MovementIntent;
        // This test covers objective/reward progression; start at the authored entry, then use real movement contacts.
        var start = (CircleCollider2D)typeof(OrbitalRelayEvent).GetField("startArea", Private).GetValue(relay);
        var body = movement.GetComponent<Rigidbody2D>();
        body.position = start.transform.TransformPoint(start.offset);
        movement.transform.position = body.position;
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
                if (!result.HasValue && UpgradeManager.Instance.IsChoosingUpgrade && station.RewardFlow.PendingReward == null)
                    yield return ResolveNonRingSelection(station);
                if (!result.HasValue && station.RewardFlow.PendingReward.HasValue)
                    Assert.That(station.RewardFlow.DebugChooseFirstValidTarget(), Is.True);
                yield return null;
            }
            if (!result.HasValue || !result.Value.Success)
                Assert.Fail($"Site Relay must finish through real movement/body contacts. result={result}, relay={relay != null}, started={relay != null && relay.IsStarted}, phase={(relay != null ? relay.Snapshot.Phase.ToString() : "destroyed")}, player={movement.transform.position}, event={(relay != null ? relay.transform.position.ToString() : "destroyed")}, modules={station.Modules.Count}, scale={Time.timeScale}, active={spawner.ActiveEvent}");
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
