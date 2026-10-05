#if UNITY_EDITOR
using NUnit.Framework;
using UnityEngine;
using System.Reflection;
using System.Collections.Generic;
public sealed class OrbitalRelayIntegrationTests
{
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
        bool ownsRun = RunStateManager.Instance == null;
        try
        {
            var spawner = host.AddComponent<WorldEventSpawner>();
            // EditMode does not run the runtime lifecycle automatically.
            typeof(WorldEventSpawner).GetMethod("OnEnable", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(spawner, null);
            var e = target.AddComponent<RelayRewardTestEvent>();
            typeof(WorldEvent).GetField("<IsCompleted>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(e, true);
            ((List<WorldEvent>)typeof(WorldEventSpawner).GetField("spawnedEvents", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(spawner)).Add(e);
            ((HashSet<WorldEvent>)typeof(WorldEventSpawner).GetField("debugRewardSuppressedEvents", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(spawner)).Add(e);
            int notices = 0;
            spawner.EventCompleted += completed => { notices++; spawner.NotifyEventCompleted(completed); };
            spawner.NotifyEventCompleted(e); spawner.NotifyEventCompleted(e);
            Assert.That(notices, Is.EqualTo(1));
            Assert.That(spawner.IsRewardDeliverySuppressed(e), Is.False, "Dispatcher consumed preview without opening real rewards.");
            Assert.That(spawner.SpawnedEvents, Is.Empty);
            typeof(WorldEventSpawner).GetMethod("OnDisable", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(spawner, null);
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
            Assert.That(enemies.WorldEventSpawnPressureMultiplier, Is.EqualTo(1)); onDisable.Dispose();
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
}
public sealed class RelayRewardTestEvent : WorldEvent
{
    public override WorldEventRewardResult? CompletionReward => new WorldEventRewardResult(20, 1);
}
#endif
