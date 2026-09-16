using System.Collections;
using System.Linq;
using NUnit.Framework;
using Subject42.Combat.OrbitalStation;
using UnityEngine;
using UnityEditor.SceneManagement;
using UnityEngine.TestTools;

public sealed class Subject42OrbitalOperationsTests
{
    [SetUp] public void PreservePreferences() => CoreTestSupport.PreservePreferences();
    [UnityTearDown] public IEnumerator Cleanup() => CoreTestSupport.CleanupPlayMode();

    [Category("Core"), Test]
    public void RingMountAndModuleTransactionsCommitOnceAndRejectOccupiedTargets()
    {
        var state = OrbitalRunState.CreateDefault(1);
        int revision = state.Revision;
        Assert.That(state.TryAddRing(out var ring, out _), Is.True);
        Assert.That(state.AddMount(ring.StableRingId, out _), Is.True);
        Assert.That(ring.MountCount, Is.EqualTo(2));
        Assert.That(state.InstallModule(OrbitalModuleKind.ArcEmitter,
            ring.StableRingId, 0, out var module), Is.True);
        Assert.That(state.Revision, Is.EqualTo(revision + 3));
        int moduleId = module.StableModuleId;
        string before = JsonUtility.ToJson(state);
        Assert.That(state.MoveModule(moduleId, 1, 0, out _), Is.False);
        Assert.That(state.InstallModule(OrbitalModuleKind.Pistol,
            ring.StableRingId, 0, out _), Is.False);
        Assert.That(JsonUtility.ToJson(state), Is.EqualTo(before));
        Assert.That(state.MoveModule(moduleId, ring.StableRingId, 1, out _), Is.True);
        Assert.That(state.FindModule(moduleId), Is.SameAs(module));
        Assert.That(state.IsMountFree(ring.StableRingId, 0), Is.True);
        Assert.That(module.MountIndex, Is.EqualTo(1));
        Assert.That(state.RemoveModule(moduleId), Is.True);
        Assert.That(state.IsMountFree(ring.StableRingId, 1), Is.True);
        Assert.That(state.NextStableModuleId, Is.EqualTo(moduleId + 1));
        Assert.That(state.Revision, Is.EqualTo(revision + 5));
        Assert.That(state.Validate(out string error), Is.True, error);
    }

    [Category("Core"), Test]
    public void LinkPairCommitsAtomicallyPreservesPartnerOnMoveAndUpgradesCore()
    {
        var state = OrbitalRunState.CreateDefault(2);
        Assert.That(state.AddMount(1, out _), Is.True);
        Assert.That(state.AddMount(1, out _), Is.True);
        var ring = state.AddRing();
        Assert.That(ring, Is.Not.Null);
        string before = JsonUtility.ToJson(state);
        Assert.That(state.InstallLinkPair(1, 1, 1, 1, out _, out _, out _), Is.False);
        Assert.That(JsonUtility.ToJson(state), Is.EqualTo(before));
        int revision = state.Revision, nextId = state.NextStableModuleId;
        Assert.That(state.InstallLinkPair(1, 1, 1, 2, out var a, out var b, out _), Is.True);
        Assert.That(state.Revision, Is.EqualTo(revision + 1));
        Assert.That(state.NextStableModuleId, Is.EqualTo(nextId + 2));
        Assert.That(state.FindLinkPartner(a.StableModuleId), Is.EqualTo(b.StableModuleId));
        Assert.That(state.MoveModule(a.StableModuleId, ring.StableRingId, 0, out _), Is.True);
        Assert.That(state.FindLinkPartner(a.StableModuleId), Is.EqualTo(b.StableModuleId));
        Assert.That(state.UpgradeCore(), Is.True);
        Assert.That(state.UpgradeLinkMatrix(), Is.True);
        Assert.That(state.CoreState.Level, Is.EqualTo(1));
        Assert.That(state.CoreState.LinkMatrixUpgradeLevel, Is.EqualTo(1));
        Assert.That(state.RemoveModule(a.StableModuleId), Is.True);
        Assert.That(state.FindLinkPartner(b.StableModuleId), Is.Zero);
        Assert.That(state.Validate(out string error), Is.True, error);
    }

    [Category("Core"), Test]
    public void VikaCustomPathsFinalizeMultipleRingsInCreationOrder()
    {
        var state = OrbitalRunState.CreateDefault(3, customPaths: true);
        var first = state.NextPendingRing;
        Assert.That(state.TryAddRing(out var second, out _), Is.True);
        Assert.That(CustomOrbitPath.TryRestore(new[] {
            new Vector2(0f, .8f), new Vector2(.8f, 0f),
            new Vector2(0f, -.8f), new Vector2(-.8f, 0f)
        }, out var firstPath), Is.True);
        Assert.That(CustomOrbitPath.TryRestore(new[] {
            new Vector2(0f, 1.4f), new Vector2(1.2f, .2f),
            new Vector2(.1f, -1.1f), new Vector2(-1.3f, -.1f)
        }, out var secondPath), Is.True);
        Assert.That(state.PendingRingCount, Is.EqualTo(2));
        string before = JsonUtility.ToJson(state);
        Assert.That(state.TrySetCustomPath(second.StableRingId, secondPath, 2f, out _), Is.False);
        Assert.That(JsonUtility.ToJson(state), Is.EqualTo(before));
        Assert.That(state.TrySetCustomPath(first.StableRingId, firstPath, 2f, out _), Is.True);
        Assert.That(state.NextPendingRing, Is.SameAs(second));
        Assert.That(state.TrySetCustomPath(second.StableRingId, secondPath, 2f, out _), Is.True);
        Assert.That(first.CustomPath, Is.EqualTo(firstPath.CapturePoints()));
        Assert.That(second.CustomPath, Is.EqualTo(secondPath.CapturePoints()));
        Assert.That(state.PendingRingCount, Is.Zero);
        Assert.That(state.NextPendingRing, Is.Null);
        Assert.That(state.Validate(out string error), Is.True, error);
    }

    [Category("Core"), UnityTest]
    public IEnumerator SectorRestorePreservesCompleteOrbitalStateAndRebuildsOccupancy()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
        yield return new EnterPlayMode();
        var manager = RunStateManager.EnsureExists();
        var stage = ScriptableObject.CreateInstance<StageProfileData>();
        var rule = ScriptableObject.CreateInstance<WorldRuleData>();
        var anomaly = ScriptableObject.CreateInstance<LocalAnomalyData>();
        var player = new GameObject("Orbital restore test player");
        try
        {
            manager.BeginNewRun(null, null, stage, rule, anomaly);
            var state = manager.OrbitalStationState;
            var second = state.AddRing();
            Assert.That(second, Is.Not.Null);
            Assert.That(state.AddMount(1, out _), Is.True);
            Assert.That(state.InstallModule(OrbitalModuleKind.ArcEmitter,
                second.StableRingId, 0, out var arc), Is.True);
            Assert.That(state.MoveModule(arc.StableModuleId, 1, 1, out _), Is.True);
            Assert.That(state.UpgradeModuleDamage(arc.StableModuleId), Is.True);
            Assert.That(state.UpgradeRingPower(1), Is.True);
            Assert.That(state.UpgradeRingSpeed(second.StableRingId), Is.True);
            Assert.That(state.UpgradeCore(), Is.True);
            Assert.That(state.AddMount(second.StableRingId, out _), Is.True);
            Assert.That(state.InstallLinkPair(second.StableRingId, 0, second.StableRingId, 1, out _, out _, out _), Is.True);
            Assert.That(state.UpgradeLinkMatrix(), Is.True);
            state.SetPhase(1, 17.5f);
            state.SetPhase(second.StableRingId, 81.25f);
            string before = JsonUtility.ToJson(state);
            var station = OrbitalStationRuntime.CreateFromState(player, state);
            Assert.That(station, Is.Not.Null);
            Assert.That(station.IsInitialized, Is.True);
            Assert.That(station.SimulateSectorRestore(), Is.True);
            Assert.That(station.CaptureState(), Is.SameAs(state));
            Assert.That(manager.OrbitalStationState, Is.SameAs(state));
            Assert.That(JsonUtility.ToJson(state), Is.EqualTo(before));
            Assert.That(station.Rings.Count, Is.EqualTo(state.Rings.Count));
            Assert.That(station.Modules.Count, Is.EqualTo(state.Modules.Count));
            foreach (var module in state.Modules)
            {
                var restored = station.Modules.Single(m => m.StableModuleId == module.StableModuleId);
                Assert.That(restored.Kind, Is.EqualTo(module.ModuleType));
                Assert.That(restored.CurrentMount.Ring.RingId, Is.EqualTo(module.StableRingId));
                Assert.That(restored.CurrentMount.MountIndex, Is.EqualTo(module.MountIndex));
            }
        }
        finally
        {
            Object.DestroyImmediate(player);
            Object.DestroyImmediate(manager.gameObject);
            Object.DestroyImmediate(stage);
            Object.DestroyImmediate(rule);
            Object.DestroyImmediate(anomaly);
        }

    }
}
