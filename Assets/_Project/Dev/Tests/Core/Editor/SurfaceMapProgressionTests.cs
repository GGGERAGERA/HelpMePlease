#if UNITY_EDITOR
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public sealed class SurfaceMapProgressionTests
{
    private readonly List<Object> created = new();
    private sealed class Storage : ISurfaceMapStorage
    {
        public string json; public int writes;
        public string Load(string id) => json;
        public void Save(string id, string value) { json = value; writes++; }
    }
    [TearDown] public void Cleanup() { foreach (var asset in created) Object.DestroyImmediate(asset); created.Clear(); }
    private T Make<T>() where T : ScriptableObject { var value = ScriptableObject.CreateInstance<T>(); created.Add(value); return value; }
    private SurfaceMapDefinition Map()
    {
        var a = Make<SurfaceSectorDefinition>(); var b = Make<SurfaceSectorDefinition>();
        Set(a, "id", "first"); Set(a, "neighbours", new[] { "next" }); Set(b, "id", "next"); Set(b, "neighbours", new[] { "first" });
        var map = Make<SurfaceMapDefinition>(); Set(map, "sectors", new[] { a, b }); Set(map, "startingSectors", new[] { "first" }); return map;
    }
    private static void Set(Object target, string field, object value) => target.GetType().GetField(field, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(target, value);
    [Test] public void VictoryExpandsFrontierAndReloadsFromStorage()
    {
        var storage = new Storage(); var map = Map(); var service = new SurfaceMapService(map, storage);
        Assert.That(service.GetStatus("first"), Is.EqualTo(SurfaceSectorStatus.Available));
        Assert.That(service.TrySelect("next"), Is.False); Assert.That(service.TrySelect("unknown"), Is.False);
        Assert.That(service.TrySelect("first"), Is.True); Assert.That(service.TryBuildRunConfig(out var config), Is.True);
        Assert.That(service.CompleteVictory(config, RunEndReason.PlayerDied, false), Is.False);
        Assert.That(service.CompleteVictory(config, RunEndReason.ReturnedToBunker, false), Is.False);
        Assert.That(service.CompleteVictory(config, RunEndReason.Victory, false), Is.False, "Dev/unconfirmed victory cannot advance");
        Assert.That(storage.writes, Is.Zero);
        Assert.That(service.CompleteVictory(config, RunEndReason.Victory, true), Is.True);
        Assert.That(service.CompleteVictory(config, RunEndReason.Victory, true), Is.False);
        Assert.That(storage.writes, Is.EqualTo(1));
        var reloaded = new SurfaceMapService(map, storage);
        Assert.That(reloaded.GetStatus("first"), Is.EqualTo(SurfaceSectorStatus.Completed));
        Assert.That(reloaded.GetStatus("next"), Is.EqualTo(SurfaceSectorStatus.Available));
        Assert.That(reloaded.TrySelect("first"), Is.True, "Completed nodes remain replayable");
    }
    [Test] public void DuplicateIdsAndBrokenLinksAreRejected()
    {
        var map = Map(); Set(map.Sectors[1], "id", "first"); Assert.That(map.TryValidate(out _), Is.False);
        map = Map(); Set(map.Sectors[0], "neighbours", new[] { "missing" }); Assert.That(map.TryValidate(out _), Is.False);
    }
    [Test] public void RunConfigSnapshotsNumericValuesAndPrefabWeights()
    {
        var prefab = new GameObject("enemy"); created.Add(prefab);
        var parameters = new RunConfigParameters { experience = 1.5f, biomeId = "future", enemyWeights = new[] { new RunPrefabWeight { prefab = prefab, multiplier = 3f } } };
        var config = new RunConfig("map", "node", parameters); parameters.experience = 7; parameters.enemyWeights[0].multiplier = 0;
        Assert.That(config.Experience, Is.EqualTo(1.5f)); Assert.That(config.EnemyWeight(prefab), Is.EqualTo(3f)); Assert.That(config.BiomeId, Is.EqualTo("future"));
        Assert.That(RunConfig.Default.Gold, Is.EqualTo(1f));
    }
    [Test] public void UnknownSavedIdsAreIgnoredAndBrokenJsonRecovers()
    {
        var map = Map(); var storage = new Storage { json = "{\"unlocked\":[\"removed\"],\"completed\":[\"removed\"]}" };
        var service = new SurfaceMapService(map, storage);
        Assert.That(service.GetStatus("removed"), Is.EqualTo(SurfaceSectorStatus.Locked));
        Assert.That(service.GetStatus("first"), Is.EqualTo(SurfaceSectorStatus.Available));
        storage.json = "bad json"; Assert.DoesNotThrow(() => new SurfaceMapService(map, storage));
    }
}
#endif
