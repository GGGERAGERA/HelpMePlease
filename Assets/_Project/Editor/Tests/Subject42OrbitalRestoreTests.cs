#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using Subject42.Combat.OrbitalStation;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

public sealed class Subject42OrbitalRestoreTests
{
    private sealed class CallbackProbe
    {
        public bool Invoked;
        public void Mark() => Invoked = true;
    }

    public static IEnumerable<string> Malformations => new[] {
        "core", "rings", "modules", "null ring", "null module", "duplicate ring",
        "duplicate module", "ring ID", "module ID", "missing ring", "mount",
        "occupancy", "ring allocator", "module allocator", "enum", "version",
        "core cap", "speed cap", "power cap", "link matrix cap",
        "capacity", "core upgrade", "ring upgrade", "module upgrade", "phase", "uninitialized"
    };

    public static void Corrupt(OrbitalRunState s, string kind)
    {
        switch (kind)
        {
            case "core cap": s.CoreState.Level = OrbitalProgressionConfig.Default.MaxCoreLevel + 1; break;
            case "speed cap": s.Rings[0].SpeedUpgradeLevel = OrbitalProgressionConfig.Default.MaxSpeedUpgradeLevel + 1; break;
            case "power cap": s.Rings[0].PowerUpgradeLevel = OrbitalProgressionConfig.Default.MaxPowerUpgradeLevel + 1; break;
            case "link matrix cap": s.CoreState.LinkMatrixUpgradeLevel = OrbitalProgressionConfig.Default.MaxLinkMatrixLevel + 1; break;
            case "core": s.CoreState = null; break;
            case "rings": s.Rings = null; break;
            case "modules": s.Modules = null; break;
            case "null ring": s.Rings.Add(null); break;
            case "null module": s.Modules.Add(null); break;
            case "duplicate ring": s.AddRing().StableRingId = 1; break;
            case "duplicate module": s.InstallModule(OrbitalModuleKind.ArcEmitter, 1, 1, out var m); m.StableModuleId = 1; break;
            case "ring ID": s.Rings[0].StableRingId = 0; break;
            case "module ID": s.Modules[0].StableModuleId = 0; break;
            case "missing ring": s.Modules[0].StableRingId = 99; break;
            case "mount": s.Modules[0].MountIndex = 3; break;
            case "occupancy": s.InstallModule(OrbitalModuleKind.ArcEmitter, 1, 1, out var other); other.MountIndex = 0; break;
            case "ring allocator": s.NextStableRingId = 1; break;
            case "module allocator": s.NextStableModuleId = 1; break;
            case "enum": s.Modules[0].ModuleType = (OrbitalModuleKind)99; break;
            case "version": s.Version = 99; break;
            case "capacity": s.Rings[0].MountCapacity = -1; break;
            case "core upgrade": s.CoreState.CascadeUpgradeLevel = -1; break;
            case "ring upgrade": s.Rings[0].VisualUpgradeLevel = -1; break;
            case "module upgrade": s.Modules[0].DamageLevel = -1; break;
            case "phase": s.Rings[0].PhaseOffset = float.NaN; break;
            case "uninitialized": s.IsInitialized = false; break;
            default: throw new ArgumentException(kind);
        }
    }

    // Reflect every public data field, including null elements and diagnostic counters.
    // This is a test fingerprint, not a persistence format or state copy.
    public static string Snapshot(object value)
    {
        if (value == null) return "null";
        Type type = value.GetType();
        if (type.IsPrimitive || type.IsEnum || value is string)
            return Convert.ToString(value, CultureInfo.InvariantCulture);
        if (value is IEnumerable list)
            return "[" + string.Join(",", list.Cast<object>().Select(Snapshot)) + "]";
        return "{" + string.Join(",", type.GetFields(BindingFlags.Public | BindingFlags.Instance)
            .OrderBy(f => f.Name).Select(f => f.Name + "=" + Snapshot(f.GetValue(value)))) + "}";
    }

    [TestCaseSource(nameof(Malformations))]
    public void Validation_IsTotalAndNonMutating(string kind)
    {
        var state = OrbitalRunState.CreateDefault(1);
        Corrupt(state, kind);
        string before = Snapshot(state);
        Assert.That(state.Validate(out string error), Is.False);
        Assert.That(error, Is.Not.Null.And.Not.Empty);
        Assert.That(Snapshot(state), Is.EqualTo(before));
    }

    [Test]
    public void EmptyStation_IsNotSilentlyRepaired()
    {
        var state = OrbitalRunState.CreateDefault(1);
        state.RemoveRing(1, out _);
        Assert.That(state.Validate(out string error), Is.True, error);
        Assert.That(state.Rings, Is.Empty);
        Assert.That(state.Modules, Is.Empty);
    }

    public static void Set(object target, string name, object value) => target.GetType()
        .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
    public static void SetState(RunStateManager manager, OrbitalRunState state) =>
        typeof(RunStateManager).GetProperty(nameof(RunStateManager.OrbitalStationState))
            .SetValue(manager, state);
    public static void ExpectFailure() => LogAssert.Expect(LogType.Error,
        new Regex(@"\[OrbitalStation\] operation=restore RunId=.*scene=.*component=OrbitalStationRuntime reason=.+"));

    public static void Modify(OrbitalRunState state)
    {
        var second = state.AddRing();
        Assert.That(state.InstallModule(OrbitalModuleKind.ArcEmitter, second.StableRingId, 0, out var module), Is.True);
        Assert.That(state.UpgradeRingPower(1), Is.True);
        Assert.That(state.UpgradeRingSpeed(second.StableRingId), Is.True);
        Assert.That(state.AddMount(1, out _), Is.True);
        Assert.That(state.UpgradeCore(), Is.True);
        Assert.That(state.UpgradeModuleDamage(module.StableModuleId), Is.True);
        Assert.That(state.MoveModule(module.StableModuleId, 1, 2, out _), Is.True);
        state.SetPhase(1, 17.5f);
        state.SetPhase(second.StableRingId, 81.25f);
    }

    [UnityTest]
    public IEnumerator StrictRestore_PlayModeFailuresAndIdentity()
    {
        yield return new EnterPlayMode();
        var manager = RunStateManager.EnsureExists();
        var stage = ScriptableObject.CreateInstance<StageProfileData>();
        var rule = ScriptableObject.CreateInstance<WorldRuleData>();
        var anomaly = ScriptableObject.CreateInstance<LocalAnomalyData>();
        manager.BeginNewRun(null, null, stage, rule, anomaly);
        var first = manager.OrbitalStationState;
        Assert.That(first.CoreState, Is.Not.Null);
        Assert.That(first.Rings.Count, Is.EqualTo(1));
        Assert.That(first.Rings[0].MountCapacity, Is.EqualTo(3));
        Assert.That(first.Modules.Single().ModuleType, Is.EqualTo(OrbitalModuleKind.Pistol));
        Assert.That(first.NextStableRingId, Is.EqualTo(2));
        Assert.That(first.NextStableModuleId, Is.EqualTo(2));
        manager.BeginNewRun(null, null, stage, rule, anomaly);
        var state = manager.OrbitalStationState;
        Assert.That(state, Is.Not.SameAs(first));
        Assert.That(state.RunId, Is.EqualTo(first.RunId + 1));
        Assert.That(state.Modules.Count, Is.EqualTo(1));
        Modify(state);
        string before = Snapshot(state);
        var player = new GameObject("Strict restore fixture");
        var station = OrbitalStationRuntime.Ensure(player);
        Assert.That(station.IsInitialized, Is.True);
        for (int i = 0; i < 2; i++)
        {
            Assert.That(station.SimulateSectorRestore(), Is.True);
            Assert.That(station.State, Is.SameAs(state));
            Assert.That(manager.OrbitalStationState, Is.SameAs(state));
            Assert.That(Snapshot(state), Is.EqualTo(before));
            Assert.That(station.Modules.Count, Is.EqualTo(2));
            Assert.That(state.Modules.Count(m => m.ModuleType == OrbitalModuleKind.Pistol), Is.EqualTo(1));
        }
        Debug.Log("PASS1 fixture before/restore1/restore2 identical: " + before);
        Object.Destroy(player);
        yield return null;

        foreach (string kind in Malformations)
        {
            var malformed = OrbitalRunState.CreateDefault(100);
            Corrupt(malformed, kind);
            SetState(manager, malformed);
            string snapshot = Snapshot(malformed);
            player = new GameObject("Malformed " + kind);
            ExpectFailure();
            station = OrbitalStationRuntime.Ensure(player);
            Assert.That(station.IsInitialized, Is.False, kind);
            Assert.That(station.enabled, Is.False, kind);
            OrbitalStationRuntime.Ensure(player); // No automatic retry or repeated log.
            Assert.That(manager.OrbitalStationState, Is.SameAs(malformed), kind);
            Assert.That(Snapshot(malformed), Is.EqualTo(snapshot), kind);
            Object.Destroy(player);
            yield return null;
            Assert.That(Snapshot(malformed), Is.EqualTo(snapshot), kind + " after destruction");
        }
        SetState(manager, null);
        Assert.That(manager.TryGetOrbitalRunState(out var missing, out _), Is.False);
        Assert.That(missing, Is.Null);
        player = new GameObject("Missing state");
        ExpectFailure();
        station = OrbitalStationRuntime.Ensure(player);
        Assert.That(station.IsInitialized, Is.False);
        Assert.That(manager.OrbitalStationState, Is.Null);
        Object.Destroy(player);
        yield return null;

        // Direct launch remains missing until a user explicitly invokes the dev entry point.
        player = new GameObject("Explicit direct launch");
        var spawner = player.AddComponent<CharacterSpawner>();
        spawner.enabled = false;
        spawner.DebugStartDefaultRunIfMissing();
        var devState = manager.OrbitalStationState;
        Assert.That(devState, Is.Not.Null);
        Assert.That(devState.Modules.Count, Is.EqualTo(1));
        spawner.DebugStartDefaultRunIfMissing();
        Assert.That(manager.OrbitalStationState, Is.SameAs(devState));
        Object.Destroy(player);
        yield return null;

        // Existing committed Link survives failed restore; staged session terminates once.
        SetState(manager, state);
        Assert.That(state.InstallModule(OrbitalModuleKind.LinkNode, 2, 0, out var link), Is.True);
        player = new GameObject("Presentation failure");
        station = OrbitalStationRuntime.Ensure(player);
        var reward = station.RewardFlow;
        Set(reward, "firstRingId", link.StableRingId);
        Set(reward, "terminal", false);
        var upgrade = ScriptableObject.CreateInstance<OrbitalRewardData>();
        Set(reward, "reward", upgrade);
        var callback = new CallbackProbe();
        Set(reward, "cancelled", (Action)callback.Mark);
        var configField = typeof(OrbitalPresentationConfig).GetField("active", BindingFlags.Static | BindingFlags.NonPublic);
        var original = configField.GetValue(null);
        var broken = Object.Instantiate((OrbitalPresentationConfig)original);
        broken.PistolPrefab = null;
        before = Snapshot(state);
        try
        {
            configField.SetValue(null, broken);
            ExpectFailure();
            Assert.That(station.SimulateSectorRestore(), Is.False);
            Assert.That(station.enabled, Is.False);
            Assert.That(station.State, Is.SameAs(state));
            Assert.That(reward.enabled, Is.False);
            Assert.That(callback.Invoked, Is.True);
            Assert.That(manager.OrbitalStationState, Is.SameAs(state));
            Assert.That(Snapshot(state), Is.EqualTo(before));
            Assert.That(state.Validate(out string error), Is.True, error);
            OrbitalStationRuntime.Ensure(player);
            yield return null;
            Assert.That(Snapshot(state), Is.EqualTo(before));
        }
        finally { configField.SetValue(null, original); Object.Destroy(broken); }
        Assert.That(station.SimulateSectorRestore(), Is.True, "explicit retry after dependency fixed");
        Assert.That(station.State, Is.SameAs(state));
        Assert.That(Snapshot(state), Is.EqualTo(before));
        Object.Destroy(player);
        Object.Destroy(upgrade);
        Object.Destroy(stage); Object.Destroy(rule); Object.Destroy(anomaly);
        Object.Destroy(manager.gameObject);
        yield return null;
        yield return new ExitPlayMode();
    }
}
#endif

