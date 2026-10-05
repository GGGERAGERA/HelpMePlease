#if UNITY_EDITOR
using NUnit.Framework;
using UnityEngine;
using System.Reflection;
using System.Collections.Generic;
public sealed class OrbitalRelayIntegrationTests
{
    [Test] public void ScopedPressureContractExists()
    {
        Assert.That(typeof(WorldEventSpawner).GetMethod("AcquireSpawnPressure"), Is.Not.Null);
        Assert.That(typeof(WorldEvent).GetProperty("UsesStandardSpawnPressure"), Is.Not.Null);
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
