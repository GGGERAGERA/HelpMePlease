#if UNITY_EDITOR
using NUnit.Framework;
using UnityEngine;
using System.Reflection;
using System.Collections.Generic;
public sealed class OrbitalRelayIntegrationTests
{
    [Test] public void BotEvadesNearbyThreatWithZeroTransitionTracking()
    {
        var host = new GameObject("relay steering contract"); var threat = new GameObject("nearby threat");
        var relayRoot = Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(OrbitalRelayAuthoring.PrefabPath));
        bool ownsRun = RunStateManager.Instance == null;
        BotController bot = null;
        try
        {
            var movement = host.AddComponent<CharacterMovement2D>();
            var bounds = host.AddComponent<BoxCollider2D>(); bounds.size = Vector2.one * 100;
            var area = host.AddComponent<GameplayAreaService>(); area.ConfigureDebugAreas(bounds, bounds);
            var events = host.AddComponent<WorldEventSpawner>();
            var enemy = threat.AddComponent<EnemyHealth>(); threat.transform.position = Vector3.right * 2;
            typeof(EnemyHealth).GetMethod("OnEnable", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(enemy, null);
            var relay = relayRoot.GetComponent<OrbitalRelayEvent>();
            var state = new OrbitalRelayState(new OrbitalRelaySettings(1, 20, .6f, .75f, 15, 10, 2.5f, 1.6f), 3, p => (p + 1) % 3);
            state.Start(); state.Tick(.6f, true);
            typeof(OrbitalRelayEvent).GetField("state", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(relay, state);
            typeof(WorldEvent).GetField("<IsStarted>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(relay, true);
            bot = new BotController(movement, area);
            typeof(BotController).GetField("objective", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(bot, relay);
            bot.TickGoldenPath(0, null, events, null);
            Assert.That(movement.MovementIntent().magnitude, Is.GreaterThan(.9f), "Transition must preserve safety movement.");
            Assert.That(Vector2.Dot(movement.MovementIntent(), Vector2.left), Is.GreaterThan(0), "Move away from the real registered threat.");
        }
        finally
        {
            bot?.Dispose();
            var enemy = threat.GetComponent<EnemyHealth>();
            if (enemy != null) typeof(EnemyHealth).GetMethod("OnDisable", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(enemy, null);
            Object.DestroyImmediate(threat); Object.DestroyImmediate(relayRoot); Object.DestroyImmediate(host);
            if (ownsRun && RunStateManager.Instance != null) Object.DestroyImmediate(RunStateManager.Instance.gameObject);
        }
    }
    [Test] public void TutorialAndBotConsumeProductionRelay()
    {
        Assert.That(typeof(TutorialController).GetProperty("TargetEvent").PropertyType, Is.EqualTo(typeof(OrbitalRelayEvent)));
        Assert.That(typeof(ProductionExplorationSectorController).GetMethod("TryRespawnTutorialRelay"), Is.Not.Null);
        Assert.That(typeof(BotController).Assembly.GetType("OrbitalRelayBotSteering"), Is.Not.Null);
    }
    [Test] public void TutorialRetryDoesNotDuplicateEntrySubscriptions()
    {
        var host = new GameObject("tutorial bindings");
        var first = new GameObject("first relay"); var next = new GameObject("retry relay");
        try
        {
            var tutorial = host.AddComponent<TutorialController>();
            var a = first.AddComponent<OrbitalRelayEvent>(); var b = next.AddComponent<OrbitalRelayEvent>();
            var entry = typeof(OrbitalRelayEvent).GetField("PlayerEntered", BindingFlags.Instance | BindingFlags.NonPublic);
            tutorial.ConfigureTarget(a); tutorial.ConfigureTarget(a);
            Assert.That(((System.Action)entry.GetValue(a)).GetInvocationList().Length, Is.EqualTo(1));
            tutorial.ConfigureTarget(b);
            Assert.That(entry.GetValue(a), Is.Null);
            Assert.That(((System.Action)entry.GetValue(b)).GetInvocationList().Length, Is.EqualTo(1));
            Assert.That(tutorial.GoalCompleted, Is.False);
            typeof(TutorialController).GetMethod("OnDisable", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(tutorial, null);
            Assert.That(entry.GetValue(b), Is.Null);
            Assert.That(OrbitalRelayBotSteering.GetDesiredMovement(b, null, Vector2.zero), Is.EqualTo(Vector2.zero));
        }
        finally { Object.DestroyImmediate(host); Object.DestroyImmediate(first); Object.DestroyImmediate(next); }
    }
    [Test] public void TutorialCompletesOnlyAfterSharedRewardBarrier()
    {
        var host = new GameObject("tutorial barrier"); var rewardHost = new GameObject("reward barrier");
        var target = new GameObject("completed relay");
        try
        {
            var tutorial = host.AddComponent<TutorialController>(); var rewards = rewardHost.AddComponent<UpgradeManager>();
            var relay = target.AddComponent<OrbitalRelayEvent>(); tutorial.ConfigureTarget(relay);
            OrbitalRelayAuthoring.Set(tutorial, "rewards", rewards);
            OrbitalRelayAuthoring.Set(tutorial, "<Step>k__BackingField", TutorialStep.FirstEvent);
            OrbitalRelayAuthoring.Set(rewards, "isChoosingUpgrade", true);
            typeof(TutorialController).GetMethod("OnEventCompleted", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(tutorial, new object[] { relay });
            Assert.That(tutorial.GoalCompleted, Is.False);
            OrbitalRelayAuthoring.Set(rewards, "isChoosingUpgrade", false);
            typeof(UpgradeManager).GetMethod("InvokeIdleCallbacksIfReady", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(rewards, null);
            Assert.That(tutorial.GoalCompleted, Is.True);
            Assert.That(tutorial.Step, Is.EqualTo(TutorialStep.FirstEvent), "Exit unlock belongs to ordinary RunFlow update.");
        }
        finally { Object.DestroyImmediate(host); Object.DestroyImmediate(target); Object.DestroyImmediate(rewardHost); }
    }
    [Test] public void TutorialRespawnsRegisteredProductionPrefabAtRememberedPlacement()
    {
        var runHost = new GameObject("tutorial run metadata"); var host = new GameObject("tutorial placement owner");
        var localizationHost = new GameObject("tutorial fixture localization");
        var localizationInstance = typeof(LocalizationService).GetField("<Instance>k__BackingField", BindingFlags.Static | BindingFlags.NonPublic);
        object previousLocalization = localizationInstance.GetValue(null);
        var instance = typeof(RunStateManager).GetField("<Instance>k__BackingField", BindingFlags.Static | BindingFlags.NonPublic);
        object previous = instance.GetValue(null);
        OrbitalRelayEvent first = null, retry = null;
        try
        {
            var localization = localizationHost.AddComponent<LocalizationService>();
            OrbitalRelayAuthoring.Set(localization, "table", UnityEditor.AssetDatabase.LoadAssetAtPath<LocalizationTable>("Assets/_Project/Data/Localization/LocalizationTable.asset"));
            localizationInstance.SetValue(null, localization);
            var run = runHost.AddComponent<RunStateManager>(); instance.SetValue(null, run);
            run.SetCurrentSector(new RunSector(RunRoute.TutorialSector, null, null, null));
            var spawner = host.AddComponent<WorldEventSpawner>();
            var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<OrbitalRelayEvent>(OrbitalRelayAuthoring.PrefabPath);
            spawner.ConfigureDebugEventPrefabs(new WorldEvent[] { prefab }); spawner.ConfigureSiteControlledMode(1);
            var sector = host.AddComponent<ProductionExplorationSectorController>();
            OrbitalRelayAuthoring.Set(sector, "eventSpawner", spawner);
            OrbitalRelayAuthoring.Set(sector, "tutorialRelayPrefab", prefab);
            OrbitalRelayAuthoring.Set(sector, "tutorialRelayPosition", new Vector2(12, 7));
            OrbitalRelayAuthoring.Set(sector, "tutorialRelaySize", new Vector2(18, 18));
            Assert.That(sector.TryRespawnTutorialRelay(out first), Is.True);
            Assert.That(first.SourcePrefab, Is.SameAs(prefab));
            typeof(WorldEvent).GetMethod("DisposeForOwnerReset", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(first, null);
            Assert.That(sector.TryRespawnTutorialRelay(out retry), Is.True);
            Assert.That(retry.SourcePrefab, Is.SameAs(prefab));
            Assert.That(retry.transform.position, Is.EqualTo(new Vector3(12, 7, 0)));
            Assert.That(retry.IsStarted, Is.False); Assert.That(retry.Snapshot.Phase, Is.EqualTo(OrbitalRelayPhase.Inactive));
            Assert.That(spawner.SpawnedEvents.Count, Is.EqualTo(1));
        }
        finally
        {
            if (first != null) Object.DestroyImmediate(first.gameObject);
            if (retry != null) Object.DestroyImmediate(retry.gameObject);
            Object.DestroyImmediate(host); Object.DestroyImmediate(runHost); instance.SetValue(null, previous);
            localizationInstance.SetValue(null, previousLocalization); Object.DestroyImmediate(localizationHost);
        }
    }
    [Test] public void ScopedPressureContractExists()
    {
        Assert.That(typeof(WorldEventSpawner).GetMethod("AcquireSpawnPressure"), Is.Not.Null);
        Assert.That(typeof(WorldEvent).GetProperty("UsesStandardSpawnPressure"), Is.Not.Null);
    }
    [Test] public void StandardPressureAndRuleMultiplierSurviveScopedModifier()
    {
        var host = new GameObject("ordinary event pressure"); var target = new GameObject("ordinary event");
        bool ownsRun = RunStateManager.Instance == null;
        try
        {
            var spawner = host.AddComponent<WorldEventSpawner>(); var enemies = host.AddComponent<EnemySpawner>();
            enemies.StopSpawning(); spawner.ConfigureDebugEnemySpawner(enemies);
            enemies.SetWorldRuleSpawnPressureMultiplier(1.4f);
            var source = target.AddComponent<RelayRewardTestEvent>();
            var modifier = target.AddComponent<WorldEventPressureModifier>();
            typeof(WorldEventSpawner).GetField("<ActiveEvent>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(spawner, source);
            spawner.NotifyEventStarted(source);
            Assert.That(enemies.WorldEventSpawnPressureMultiplier, Is.EqualTo(1.15f));
            modifier.SetBonusActive(spawner, source, true, 1.6f); modifier.SetBonusActive(spawner, source, true, 1.6f);
            Assert.That(enemies.WorldEventSpawnPressureMultiplier, Is.EqualTo(1.15f * 1.6f).Within(.0001f));
            modifier.Release(); modifier.Release();
            Assert.That(enemies.WorldEventSpawnPressureMultiplier, Is.EqualTo(1.15f));
            spawner.NotifyEventFailed(source);
            Assert.That(enemies.WorldEventSpawnPressureMultiplier, Is.EqualTo(1));
            float external = (float)typeof(EnemySpawner).GetMethod("GetExternalSpawnPressureMultiplier", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(enemies, null);
            Assert.That(external, Is.EqualTo(1.4f).Within(.0001f));
            Assert.That(enemies.IsSpawningEnabled, Is.False);
        }
        finally
        {
            Object.DestroyImmediate(target); Object.DestroyImmediate(host);
            if (ownsRun && RunStateManager.Instance != null) Object.DestroyImmediate(RunStateManager.Instance.gameObject);
        }
    }
    [Test] public void CompletionRewardContractExists() { Assert.That(typeof(WorldEvent).GetProperty("CompletionReward"), Is.Not.Null); }
    [Test] public void PreviewCompletionDispatchesOnceWithoutCurrencyOrContainer()
    {
        var host = new GameObject("reward owner"); var target = new GameObject("reward target");
        var failedTarget = new GameObject("failed reward target");
        bool ownsRun = RunStateManager.Instance == null;
        try
        {
            var spawner = host.AddComponent<WorldEventSpawner>();
            // EditMode does not run the runtime lifecycle automatically.
            typeof(WorldEventSpawner).GetMethod("OnEnable", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(spawner, null);
            var e = target.AddComponent<RelayRewardTestEvent>();
            typeof(WorldEvent).GetField("<IsCompleted>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(e, true);
            ((List<WorldEvent>)typeof(WorldEventSpawner).GetField("spawnedEvents", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(spawner)).Add(e);
            ((Dictionary<WorldEvent, System.Action<WorldEvent>>)typeof(WorldEventSpawner).GetField("eventRootOwners", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(spawner))
                .Add(e, value => Object.DestroyImmediate(value.gameObject));
            ((HashSet<WorldEvent>)typeof(WorldEventSpawner).GetField("debugRewardSuppressedEvents", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(spawner)).Add(e);
            int rootReleases = 0;
            var owners = (Dictionary<WorldEvent, System.Action<WorldEvent>>)typeof(WorldEventSpawner)
                .GetField("eventRootOwners", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(spawner);
            owners[e] = value => { rootReleases++; Object.DestroyImmediate(value.gameObject); };
            int notices = 0;
            spawner.EventCompleted += completed =>
            {
                Assert.That(spawner.IsRewardDeliverySuppressed(completed), Is.False,
                    "Reward dispatch must finish before public completion, including after re-enable.");
                notices++; spawner.NotifyEventCompleted(completed);
                typeof(WorldEventSpawner).GetMethod("ReleaseRunScene", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(spawner, null);
            };
            typeof(WorldEventSpawner).GetMethod("OnDisable", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(spawner, null);
            typeof(WorldEventSpawner).GetMethod("OnEnable", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(spawner, null);
            spawner.NotifyEventCompleted(e); spawner.NotifyEventCompleted(e);
            Assert.That(notices, Is.EqualTo(1));
            Assert.That(rootReleases, Is.EqualTo(1), "Reentrant cleanup must preserve the in-flight root owner.");
            Assert.That(spawner.IsRewardDeliverySuppressed(e), Is.False, "Dispatcher consumed preview without opening real rewards.");
            Assert.That(spawner.SpawnedEvents, Is.Empty);
            var failed = failedTarget.AddComponent<RelayPressureTestEvent>();
            ((List<WorldEvent>)typeof(WorldEventSpawner).GetField("spawnedEvents", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(spawner)).Add(failed);
            typeof(WorldEvent).GetField("<IsCompleted>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(failed, true);
            typeof(WorldEvent).GetField("<IsFailed>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(failed, true);
            owners.Add(failed, value => { rootReleases++; Object.DestroyImmediate(value.gameObject); });
            int failures = 0;
            spawner.EventFailed += value =>
            {
                failures++;
                typeof(WorldEventSpawner).GetMethod("ReleaseRunScene", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(spawner, null);
            };
            spawner.NotifyEventFailed(failed); spawner.NotifyEventFailed(failed);
            Assert.That(failures, Is.EqualTo(1));
            Assert.That(rootReleases, Is.EqualTo(2), "Failure reset also preserves the in-flight root owner.");
            typeof(WorldEventSpawner).GetMethod("OnDisable", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(spawner, null);
        }
        finally
        {
            Object.DestroyImmediate(failedTarget); Object.DestroyImmediate(target); Object.DestroyImmediate(host);
            if (ownsRun && RunStateManager.Instance != null) Object.DestroyImmediate(RunStateManager.Instance.gameObject);
        }
    }
    [Test] public void OlderEventCannotHideCurrentMarker()
    {
        var host = new GameObject("marker HUD"); var visual = new GameObject("marker visual");
        var first = new GameObject("first marker owner"); var second = new GameObject("second marker owner");
        const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        var instance = typeof(HUDManager).GetField("<Instance>k__BackingField", BindingFlags.Static | BindingFlags.NonPublic);
        object previous = instance.GetValue(null);
        try
        {
            var hud = host.AddComponent<HUDManager>(); instance.SetValue(null, hud);
            var marker = visual.AddComponent<WorldEventMarker>();
            typeof(HUDManager).GetField("worldEventMarker", Private).SetValue(hud, marker);
            var a = first.AddComponent<RelayPressureTestEvent>(); var b = second.AddComponent<RelayPressureTestEvent>();
            a.PresentMarker(first.transform); b.PresentMarker(second.transform);
            a.ReleaseMarker();
            Assert.That(typeof(WorldEventMarker).GetField("target", Private).GetValue(marker), Is.SameAs(second.transform));
            b.ReleaseMarker(); b.ReleaseMarker();
            Assert.That(typeof(WorldEventMarker).GetField("target", Private).GetValue(marker), Is.Null);
        }
        finally
        {
            Object.DestroyImmediate(first); Object.DestroyImmediate(second);
            Object.DestroyImmediate(visual); Object.DestroyImmediate(host); instance.SetValue(null, previous);
        }
    }
    [Test] public void SiteRewardSuppressionSurvivesOwnerReenable()
    {
        var host = new GameObject("site reward owner"); var target = new GameObject("site reward target");
        bool ownsRun = RunStateManager.Instance == null;
        try
        {
            const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
            var spawner = host.AddComponent<WorldEventSpawner>();
            var e = target.AddComponent<RelayPressureTestEvent>();
            ((List<WorldEvent>)typeof(WorldEventSpawner).GetField("spawnedEvents", Private).GetValue(spawner)).Add(e);
            ((Dictionary<WorldEvent, System.Action<WorldEvent>>)typeof(WorldEventSpawner).GetField("eventRootOwners", Private).GetValue(spawner))
                .Add(e, value => Object.DestroyImmediate(value.gameObject));
            var suppression = (HashSet<WorldEvent>)typeof(WorldEventSpawner).GetField("siteRewardSuppressedEvents", Private).GetValue(spawner);
            suppression.Add(e);
            typeof(WorldEventSpawner).GetMethod("OnDisable", Private).Invoke(spawner, null);
            typeof(WorldEventSpawner).GetMethod("OnEnable", Private).Invoke(spawner, null);
            Assert.That(suppression.Contains(e), Is.True, "Site suppression belongs to the event lifetime.");
            typeof(WorldEvent).GetField("<IsCompleted>k__BackingField", Private).SetValue(e, true);
            int rootReleases = 0;
            var owners = (Dictionary<WorldEvent, System.Action<WorldEvent>>)typeof(WorldEventSpawner)
                .GetField("eventRootOwners", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(spawner);
            owners[e] = value => { rootReleases++; Object.DestroyImmediate(value.gameObject); };
            int notices = 0;
            spawner.EventCompleted += completed =>
            {
                Assert.That(suppression.Contains(completed), Is.False);
                notices++; spawner.NotifyEventCompleted(completed);
                typeof(WorldEventSpawner).GetMethod("ReleaseRunScene", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(spawner, null);
            };
            spawner.NotifyEventCompleted(e); spawner.NotifyEventCompleted(e);
            Assert.That(notices, Is.EqualTo(1));
            Assert.That(rootReleases, Is.EqualTo(1), "Reentrant cleanup must preserve the in-flight root owner.");
        }
        finally
        {
            Object.DestroyImmediate(target); Object.DestroyImmediate(host);
            if (ownsRun && RunStateManager.Instance != null) Object.DestroyImmediate(RunStateManager.Instance.gameObject);
        }
    }
    [Test] public void PressureLeaseCleanupAndStaleOwnership()
    {
        var host = new GameObject("pressure owner");
        var source = new GameObject("pressure source");
        var nextSource = new GameObject("next pressure source");
        bool ownsRun = RunStateManager.Instance == null;
        try
        {
            var spawner = host.AddComponent<WorldEventSpawner>();
            var enemies = host.AddComponent<EnemySpawner>(); enemies.StopSpawning();
            spawner.ConfigureDebugEnemySpawner(enemies);
            var first = source.AddComponent<RelayPressureTestEvent>();
            var next = nextSource.AddComponent<RelayPressureTestEvent>();
            var active = typeof(WorldEventSpawner).GetField("<ActiveEvent>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic);
            active.SetValue(spawner, first); spawner.NotifyEventStarted(first);
            Assert.That(enemies.WorldEventSpawnPressureMultiplier, Is.EqualTo(1));
            var old = spawner.AcquireSpawnPressure(first, 1.6f);
            Assert.That(enemies.WorldEventSpawnPressureMultiplier, Is.EqualTo(1.6f));
            spawner.NotifyEventFailed(first);
            Assert.That(enemies.WorldEventSpawnPressureMultiplier, Is.EqualTo(1));
            active.SetValue(spawner, next); spawner.NotifyEventStarted(next);
            var current = spawner.AcquireSpawnPressure(next, 1.8f); old.Dispose();
            Assert.That(enemies.WorldEventSpawnPressureMultiplier, Is.EqualTo(1.8f));
            current.Dispose(); current.Dispose();
            Assert.That(enemies.WorldEventSpawnPressureMultiplier, Is.EqualTo(1));
            Assert.That(enemies.IsSpawningEnabled, Is.False);
            var onDisable = spawner.AcquireSpawnPressure(next, 1.6f); spawner.enabled = false; typeof(WorldEventSpawner).GetMethod("OnDisable", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(spawner, null);
            Assert.That(enemies.WorldEventSpawnPressureMultiplier, Is.EqualTo(1));
            spawner.enabled = true;
            typeof(WorldEventSpawner).GetMethod("OnEnable", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(spawner, null);
            Assert.That(enemies.WorldEventSpawnPressureMultiplier, Is.EqualTo(1.6f), "Re-enable restores the same event's live lease.");
            onDisable.Dispose();
            Assert.That(enemies.WorldEventSpawnPressureMultiplier, Is.EqualTo(1));
        }
        finally
        {
            Object.DestroyImmediate(source); Object.DestroyImmediate(nextSource); Object.DestroyImmediate(host);
            if (ownsRun && RunStateManager.Instance != null) Object.DestroyImmediate(RunStateManager.Instance.gameObject);
        }
    }
}
public sealed class RelayPressureTestEvent : WorldEvent
{
    public override bool UsesStandardSpawnPressure => false;
    public void PresentMarker(Transform target) => ShowEventMarker(target, "fixture marker");
    public void ReleaseMarker() => HideEventMarker();
}
public sealed class RelayRewardTestEvent : WorldEvent
{
    public override WorldEventRewardResult? CompletionReward => new WorldEventRewardResult(20, 1);
}
#endif
