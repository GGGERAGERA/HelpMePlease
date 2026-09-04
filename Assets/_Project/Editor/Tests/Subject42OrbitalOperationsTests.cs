#if UNITY_EDITOR
using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using Subject42.Combat.OrbitalStation;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

public sealed class Subject42OrbitalOperationsTests
{
    private static object Get(object target, string field)
    {
        for (Type type = target.GetType(); type != null; type = type.BaseType)
        {
            var info = type.GetField(field, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (info != null) return info.GetValue(target);
        }
        throw new MissingFieldException(field);
    }
    private static void Set(object target, string field, object value)
    {
        for (Type type = target.GetType(); type != null; type = type.BaseType)
        {
            var info = type.GetField(field, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (info == null) continue;
            info.SetValue(target, value); return;
        }
        throw new MissingFieldException(field);
    }
    private static void Call(object target, string method, params object[] args) =>
        target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, args);
    private static string Snapshot(OrbitalRunState state) => Subject42OrbitalRestoreTests.Snapshot(state);

    [TestCase("occupied install")]
    [TestCase("bad ring install")]
    [TestCase("bad mount install")]
    [TestCase("bad kind install")]
    [TestCase("occupied move")]
    [TestCase("bad module move")]
    [TestCase("bad ring move")]
    [TestCase("bad mount move")]
    [TestCase("mount cap")]
    [TestCase("speed cap")]
    [TestCase("power cap")]
    [TestCase("power overflow")]
    [TestCase("core cap")]
    [TestCase("link damage")]
    [TestCase("ring cap")]
    [TestCase("module allocator limit")]
    [TestCase("malformed")]
    public void RejectedCommand_PreservesEveryField(string kind)
    {
        var s = OrbitalRunState.CreateDefault(1);
        s.InstallModule(OrbitalModuleKind.ArcEmitter, 1, 1, out _);
        Func<bool> command = kind switch
        {
            "occupied install" => () => s.InstallModule(OrbitalModuleKind.Pistol, 1, 0, out _),
            "bad ring install" => () => s.InstallModule(OrbitalModuleKind.Pistol, 99, 0, out _),
            "bad mount install" => () => s.InstallModule(OrbitalModuleKind.Pistol, 1, -1, out _),
            "bad kind install" => () => s.InstallModule((OrbitalModuleKind)99, 1, 2, out _),
            "occupied move" => () => s.MoveModule(2, 1, 0, out _),
            "bad module move" => () => s.MoveModule(99, 1, 2, out _),
            "bad ring move" => () => s.MoveModule(2, 99, 0, out _),
            "bad mount move" => () => s.MoveModule(2, 1, 3, out _),
            "mount cap" => () => s.AddMount(1, out _),
            "speed cap" => () => s.UpgradeRingSpeed(1),
            "power cap" => () => s.UpgradeRingPower(1),
            "power overflow" => () => s.UpgradeRingPower(1),
            "core cap" => () => s.UpgradeCore(),
            "link damage" => () => s.UpgradeModuleDamage(3),
            "ring cap" => () => s.AddRing() != null,
            "module allocator limit" => () => s.InstallModule(OrbitalModuleKind.ArcEmitter, 1, 2, out _),
            _ => () => s.MoveModule(2, 1, 2, out _)
        };
        if (kind == "mount cap") s.AddMount(1, out _);
        if (kind == "speed cap") while(s.UpgradeRingSpeed(1)) { }
        if (kind == "power overflow") s.Rings[0].PowerMultiplier = float.MaxValue;
        if (kind == "power cap") while(s.UpgradeRingPower(1)) { }
        if (kind == "core cap") while(s.UpgradeCore()) { }
        if (kind == "link damage") s.InstallModule(OrbitalModuleKind.LinkNode, 1, 2, out _);
        if (kind == "ring cap") while(s.AddRing() != null) { }
        if (kind == "module allocator limit") s.NextStableModuleId = int.MaxValue;
        if (kind == "malformed") s.Rings.Add(null);
        string before = Snapshot(s);
        Assert.That(command(), Is.False, kind);
        Assert.That(Snapshot(s), Is.EqualTo(before));
    }

    [Test]
    public void Milestone_CommitsOnce_AndDuplicateDoesNothing()
    {
        var state = OrbitalRunState.CreateDefault(1);
        int revision = state.Revision;
        Assert.That(state.ProcessPlayerLevelMilestone(2, out var ring), Is.True);
        Assert.That(ring.StableRingId, Is.EqualTo(2));
        Assert.That(state.Revision, Is.EqualTo(revision + 1));
        Assert.That(state.LastProcessedPlayerLevel, Is.EqualTo(2));
        string before = Snapshot(state);
        Assert.That(state.ProcessPlayerLevelMilestone(2, out _), Is.False);
        Assert.That(Snapshot(state), Is.EqualTo(before));
        Assert.That(state.ProcessPlayerLevelMilestone(5, out var noRing), Is.True);
        Assert.That(noRing, Is.Null);
        Assert.That(state.LastProcessedPlayerLevel, Is.EqualTo(5));
        Assert.That(state.Revision, Is.EqualTo(revision + 2));
    }

    private sealed class Fixture : IDisposable
    {
        public readonly RunStateManager Manager;
        public readonly OrbitalRunState State;
        public readonly GameObject Player;
        public readonly OrbitalStationRuntime Station;
        private readonly StageProfileData stage = ScriptableObject.CreateInstance<StageProfileData>();
        private readonly WorldRuleData rule = ScriptableObject.CreateInstance<WorldRuleData>();
        private readonly LocalAnomalyData anomaly = ScriptableObject.CreateInstance<LocalAnomalyData>();
        public Fixture()
        {
            Manager = RunStateManager.EnsureExists();
            Manager.BeginNewRun(null, null, stage, rule, anomaly);
            State = Manager.OrbitalStationState;
            Player = new GameObject("Operations fixture");
            Station = OrbitalStationRuntime.Ensure(Player);
            Assert.That(Station.IsInitialized, Is.True);
            Station.enabled = false; // Deterministic commit moment; Tick is invoked explicitly below.
        }
        public void Dispose()
        {
            Object.Destroy(Player); Object.Destroy(Manager.gameObject);
            Object.Destroy(stage); Object.Destroy(rule); Object.Destroy(anomaly);
            Time.timeScale = 1f;
        }
    }

    private static void AssertView(Fixture f)
    {
        Assert.That(f.State.Validate(out string error), Is.True, error);
        Assert.That(f.Station.Modules.Count, Is.EqualTo(f.State.Modules.Count));
        foreach (var data in f.State.Modules)
        {
            var runtime = f.Station.Modules.Single(m => m.StableModuleId == data.StableModuleId);
            Assert.That(runtime.CurrentMount.Ring.RingId, Is.EqualTo(data.StableRingId));
            Assert.That(runtime.CurrentMount.MountIndex, Is.EqualTo(data.MountIndex));
            Assert.That(runtime.CurrentMount.Module, Is.SameAs(runtime));
            Assert.That(((GameObject)Get(runtime, "Visual")).transform.parent, Is.SameAs(runtime.CurrentMount.Transform));
        }
        foreach (var ring in f.Station.Rings)
        foreach (var mount in ring.Mounts)
            Assert.That(mount.Module != null, Is.EqualTo(!f.State.IsMountFree(ring.RingId, mount.MountIndex)));
    }

    [UnityTest]
    public IEnumerator GoldenFlow_TransientCombatContinuity()
    {
        yield return new EnterPlayMode();
        yield return Golden();
        yield return new ExitPlayMode();
    }
    private static IEnumerator Golden()
    {
        using var f = new Fixture();
        var station = f.Station;
        var pistol = station.Modules.Single();
        var core = station.Core;
        var combat = station.Combat;
        var ring = station.Rings.Single();
        var enemyGo = new GameObject("Projectile continuity target");
        enemyGo.transform.position = new Vector3(4f, 0f, 0f);
        var enemy = enemyGo.AddComponent<EnemyHealth>();
        pistol.ActivateCombat(); // Actual initial Pistol fire through production combat adapter.
        var projectiles = (IList)Get(combat, "projectiles");
        Assert.That(projectiles.Count, Is.EqualTo(1));
        var projectile = projectiles[0];
        var projectileObject = (GameObject)Get(projectile, "GameObject");
        Set(pistol, "Cooldown", 0.37f);
        Set(core, "pulseTimer", 2.25f); Set(core, "cascadeTimer", 0.07f); Set(core, "cascadeIndex", 0);
        ring.State.CurrentPhase = 37.5f;
        OrbitalModuleRuntime arc = null;
        int revision = f.State.Revision;
        void Check()
        {
            Assert.That(f.State.Revision, Is.EqualTo(++revision));
            Assert.That(station, Is.SameAs(f.Player.GetComponentInChildren<OrbitalStationRuntime>()));
            Assert.That(station.Modules.Single(m => m.StableModuleId == 1), Is.SameAs(pistol));
            Assert.That(station.Core, Is.SameAs(core)); Assert.That(station.Combat, Is.SameAs(combat));
            Assert.That(station.Rings.Single(r => r.RingId == 1), Is.SameAs(ring));
            Assert.That(Get(pistol, "Cooldown"), Is.EqualTo(0.37f));
            Assert.That(Get(core, "pulseTimer"), Is.EqualTo(2.25f));
            Assert.That(Get(core, "cascadeTimer"), Is.EqualTo(0.07f));
            Assert.That(Get(core, "cascadeIndex"), Is.EqualTo(0));
            Assert.That(ring.State.CurrentPhase, Is.EqualTo(37.5f));
            Assert.That(projectiles.Count, Is.EqualTo(1)); Assert.That(projectiles[0], Is.SameAs(projectile));
            Assert.That((GameObject)Get(projectile, "GameObject"), Is.SameAs(projectileObject));
            Assert.That(Get(projectile, "Active"), Is.EqualTo(true));
            if (arc != null) Assert.That(Get(arc, "Cooldown"), Is.EqualTo(0.81f));
            AssertView(f);
        }
        var second = station.AddRing(); Assert.That(second, Is.Not.Null); Check();
        Assert.That(station.InstallModule(OrbitalModuleKind.ArcEmitter, second.StableRingId, 0, out _), Is.True); Check();
        arc = station.Modules.Single(m => m.StableModuleId == 2); Set(arc, "Cooldown", 0.81f);
        Assert.That(station.UpgradeRingSpeed(1), Is.True); Check();
        Assert.That(station.UpgradeRingPower(1), Is.True); Check();
        Assert.That(station.AddMount(1, out _), Is.True); Check();
        Assert.That(station.MoveModule(2, 1, 3, out _), Is.True); Check();
        Assert.That(station.Modules.Single(m => m.StableModuleId == 2), Is.SameAs(arc));
        Assert.That(station.UpgradeModuleDamage(2), Is.True); Check();
        Assert.That(station.UpgradeCore(), Is.True); Check();
        Assert.That(f.State.NextStableModuleId, Is.EqualTo(3)); Assert.That(f.State.NextStableRingId, Is.EqualTo(3));
        Assert.That(f.State.CoreState.Level, Is.EqualTo(1));
        Assert.That(f.State.FindRing(1).SpeedUpgradeLevel, Is.EqualTo(1));
        Assert.That(f.State.FindRing(1).PowerUpgradeLevel, Is.EqualTo(1));
        Assert.That(f.State.FindRing(1).MountCapacity, Is.EqualTo(4));
        Assert.That(f.State.FindModule(2).DamageLevel, Is.EqualTo(1));
        Assert.That(f.State.FindModule(2).MountIndex, Is.EqualTo(3));
        string beforeRejected = Snapshot(f.State);
        Assert.That(station.MoveModule(2, 1, 0, out _), Is.False);
        Assert.That(station.AddMount(1, out _), Is.False);
        Assert.That(station.InstallModule(OrbitalModuleKind.Pistol, 1, 0, out _), Is.False);
        Assert.That(Snapshot(f.State), Is.EqualTo(beforeRejected));
        AssertView(f);
        var position = projectileObject.transform.position;
        combat.Tick(0.01f);
        Assert.That(projectileObject.transform.position, Is.Not.EqualTo(position));
        Assert.That(station.Modules.Single(m => m.StableModuleId == 2), Is.SameAs(arc));
        Assert.That(Get(arc, "Cooldown"), Is.EqualTo(0.81f));
        float speed = ring.RotationSpeed;
        Assert.That(speed, Is.EqualTo(52.5f));
        ring.Tick(0.1f);
        Assert.That(ring.State.CurrentPhase, Is.EqualTo(Mathf.Repeat(37.5f + speed * 0.1f, 360f)));
        Debug.Log("PASS2 continuity: same Pistol/Arc/Core/Ring/Combat/projectile objects; Pistol=0.37 Arc=0.81 pulse=2.25 cascade=0.07 index=0 phase=37.5 projectile active count=1; preserved through 8 commands. Projectile continues flight; next ring Tick uses new speed.");
        Object.Destroy(enemyGo);
        yield return null;
    }

    [UnityTest]
    public IEnumerator CommittedState_SurvivesVisualFailures()
    {
        yield return new EnterPlayMode();
        yield return Failures();
        yield return new ExitPlayMode();
    }
    private static IEnumerator Failures()
    {
        using var f = new Fixture();
        var s = f.Station;
        Assert.That(s.InstallModule(OrbitalModuleKind.ArcEmitter, 1, 1, out _), Is.True);
        int revision = f.State.Revision;
        var target = s.Rings[0].Mounts[2];
        Object.DestroyImmediate(target.Transform.gameObject); // Real missing view dependency, no production test hook.
        LogAssert.Expect(LogType.Error, new Regex("operation=Move RunId=.*ring=1 module=2 state committed; incremental presentation sync failed"));
        Assert.That(s.MoveModule(2, 1, 2, out _), Is.True);
        Assert.That(f.State.Modules.Single(m => m.StableModuleId == 2).MountIndex, Is.EqualTo(2));
        Assert.That(f.State.Revision, Is.EqualTo(revision + 1));
        Assert.That(f.Manager.OrbitalStationState, Is.SameAs(f.State));
        Assert.That(s.IsInitialized, Is.False);
        Assert.That(s.SimulateSectorRestore(), Is.True);
        s.enabled = false;
        AssertView(f);
        // A deliberately wrong cache cannot veto state-free occupancy. It only fails visual sync after commit.
        var mount = s.Rings[0].Mounts[1];
        Set(mount, "<Module>k__BackingField", s.Modules.Single(m => m.StableModuleId == 1));
        Assert.That(mount.Occupied, Is.True);
        Assert.That(f.State.IsMountFree(1, 1), Is.True);
        revision = f.State.Revision; int allocator = f.State.NextStableModuleId;
        LogAssert.Expect(LogType.Error, new Regex("operation=Install RunId=.*state committed; incremental presentation sync failed"));
        Assert.That(s.InstallModule(OrbitalModuleKind.ArcEmitter, 1, 1, out _), Is.True);
        Assert.That(f.State.Revision, Is.EqualTo(revision + 1));
        Assert.That(f.State.NextStableModuleId, Is.EqualTo(allocator + 1));
        Assert.That(f.State.Modules.Count, Is.EqualTo(3));
        string committed = Snapshot(f.State);
        Assert.That(s.InstallModule(OrbitalModuleKind.ArcEmitter, 1, 1, out _), Is.False);
        Assert.That(Snapshot(f.State), Is.EqualTo(committed));
        Debug.Log("PASS2 failures: Move returns true and retains new mount; Install returns true, allocator/revision +1; cache cannot veto commit; retry on occupied mount false/no changes. " + committed);
        yield return null;
    }

    [UnityTest]
    public IEnumerator RewardsAndDrag_StillCompleteAndCancel()
    {
        yield return new EnterPlayMode();
        yield return Rewards();
        yield return new ExitPlayMode();
    }
    private static IEnumerator Rewards()
    {
        using var f = new Fixture();
        using var provider = new OrbitalRewardProvider(Array.Empty<UpgradeData>());
        var flow = f.Station.RewardFlow;
        int completed = 0, cancelled = 0;
        foreach (var kind in new[] { OrbitalRewardKind.RingSpeed, OrbitalRewardKind.RingPower, OrbitalRewardKind.AddMount })
        {
            Assert.That(flow.Begin(provider.GetDefinition(kind), () => completed++, () => cancelled++), Is.True);
            Assert.That(flow.DebugChooseRing(1), Is.True);
            Assert.That(flow.PendingReward, Is.Null);
            string committed = Snapshot(f.State);
            Assert.That(flow.DebugChooseRing(1), Is.False);
            Assert.That(Snapshot(f.State), Is.EqualTo(committed));
            AssertView(f);
        }
        Assert.That(completed, Is.EqualTo(3));
        Assert.That(flow.Begin(provider.GetDefinition(OrbitalRewardKind.ArcEmitter), () => completed++, () => cancelled++), Is.True);
        Assert.That(flow.DebugChooseMount(1, 1), Is.True);
        float deadline = Time.realtimeSinceStartup + 3f;
        while (flow.PendingReward != null && Time.realtimeSinceStartup < deadline) yield return null;
        Assert.That(completed, Is.EqualTo(4)); Assert.That(cancelled, Is.Zero);
        AssertView(f);
        var relocation = f.Player.GetComponentInChildren<OrbitalRelocationController>();
        relocation.enabled = false; // Invoke real drag methods deterministically, without hardware Input.
        var arc = f.Station.Modules.Single(m => m.Kind == OrbitalModuleKind.ArcEmitter);
        string before = Snapshot(f.State);
        yield return null;
        Call(relocation, "BeginDrag", arc);
        Assert.That(relocation.IsDragging, Is.True);
        arc.SetDragPosition(new Vector2(5f, 5f));
        relocation.CancelDrag("test cancel");
        Assert.That(relocation.IsDragging, Is.False);
        Assert.That(Time.timeScale, Is.EqualTo(1f));
        Assert.That(Snapshot(f.State), Is.EqualTo(before));
        AssertView(f);
        yield return null;
        Call(relocation, "BeginDrag", arc);
        Assert.That(f.Station.MoveModule(arc.StableModuleId, 1, 2, out _), Is.True);
        relocation.CancelDrag("test release cleanup");
        AssertView(f);
        Assert.That(flow.Begin(provider.GetDefinition(OrbitalRewardKind.ArcEmitter), () => completed++, () => cancelled++), Is.True);
        Assert.That(flow.DebugChooseMount(1, 1), Is.True);
        Object.DestroyImmediate(f.Station.Rings[0].Mounts[1].Transform.gameObject);
        LogAssert.Expect(LogType.Error, new Regex("operation=Install RunId=.*state committed; incremental presentation sync failed"));
        deadline = Time.realtimeSinceStartup + 3f;
        while (flow.PendingReward != null && Time.realtimeSinceStartup < deadline) yield return null;
        Assert.That(completed, Is.EqualTo(5), "committed reward completes despite missing view");
        Assert.That(cancelled, Is.Zero);
        Assert.That(f.State.Modules.Count, Is.EqualTo(3));
        Assert.That(f.Station.IsInitialized, Is.False);
        Debug.Log("PASS2 reward failure: flight target view removed; install committed once; completion callback count +1; no cancellation/retry/rollback.");
        Debug.Log("PASS2 rewards: speed/power/mount/install complete once each; drag preview cancel preserves state/timeScale; valid drag move retains runtime and syncs occupancy.");
        yield return null;
    }
    [UnityTest]
    public IEnumerator LinkInstallAndCancel_PreserveExistingSemantics()
    {
        yield return new EnterPlayMode();
        yield return Links();
        yield return new ExitPlayMode();
    }
    private static IEnumerator Links()
    {
        using var f = new Fixture();
        using var provider = new OrbitalRewardProvider(Array.Empty<UpgradeData>());
        var flow = f.Station.RewardFlow;
        var pistol = f.Station.Modules.Single();
        int completed = 0, cancelled = 0;
        Assert.That(flow.Begin(provider.GetDefinition(OrbitalRewardKind.LinkPair), () => completed++, () => cancelled++), Is.True);
        Assert.That(flow.DebugChooseMount(1, 1), Is.True);
        float deadline = Time.realtimeSinceStartup + 3f;
        while (flow.State != OrbitalRewardFlowState.SecondLinkPlacement && Time.realtimeSinceStartup < deadline) yield return null;
        Assert.That(flow.State, Is.EqualTo(OrbitalRewardFlowState.SecondLinkPlacement));
        Assert.That(f.State.Modules.Count, Is.EqualTo(1), "first Link is preview only");
        flow.CancelForSceneTransition();
        Assert.That(cancelled, Is.EqualTo(1));
        Assert.That(f.State.Modules.Count, Is.EqualTo(1));
        Assert.That(f.Station.Modules.Single(), Is.SameAs(pistol));
        AssertView(f);
        Assert.That(flow.Begin(provider.GetDefinition(OrbitalRewardKind.LinkPair), () => completed++, () => cancelled++), Is.True);
        Assert.That(flow.DebugChooseMount(1, 1), Is.True);
        deadline = Time.realtimeSinceStartup + 3f;
        while (flow.State != OrbitalRewardFlowState.SecondLinkPlacement && Time.realtimeSinceStartup < deadline) yield return null;
        Assert.That(flow.DebugChooseMount(1, 2), Is.True);
        deadline = Time.realtimeSinceStartup + 3f;
        while (flow.PendingReward != null && Time.realtimeSinceStartup < deadline) yield return null;
        Assert.That(completed, Is.EqualTo(1)); Assert.That(cancelled, Is.EqualTo(1));
        Assert.That(f.State.Modules.Count(m => m.ModuleType == OrbitalModuleKind.LinkNode), Is.EqualTo(2));
        Assert.That(f.Station.Modules.Single(m => m.StableModuleId == 1), Is.SameAs(pistol));
        AssertView(f);
        Debug.Log("PASS2 Link compatibility: staged endpoint cancellation has no state mutation; second attempt installs two endpoints and completes once; Pistol runtime preserved.");
    }

    [TestCase("same")]
    [TestCase("occupied")]
    [TestCase("missing")]
    [TestCase("allocator")]
    public void LinkPair_RejectedAtomically(string reason)
    {
        var state = OrbitalRunState.CreateDefault(1);
        if (reason == "allocator") state.NextStableModuleId = int.MaxValue - 1;
        string before = Snapshot(state);
        Assert.That(state.InstallLinkPair(1, 1, reason == "missing" ? 99 : 1,
            reason == "same" ? 1 : reason == "occupied" ? 0 : 2, out _, out _, out _), Is.False);
        Assert.That(Snapshot(state), Is.EqualTo(before));
    }

    [Test]
    public void LinkPairs_OrderMoveAndRemovalCompatibility()
    {
        var state = OrbitalRunState.CreateDefault(1);
        state.AddRing();
        int rev = state.Revision, next = state.NextStableModuleId;
        Assert.That(state.InstallLinkPair(1, 1, 1, 2, out var a, out var b, out _), Is.True);
        Assert.That(state.Revision, Is.EqualTo(rev + 1));
        Assert.That(state.NextStableModuleId, Is.EqualTo(next + 2));
        Assert.That(state.InstallLinkPair(2, 0, 2, 1, out var c, out var d, out _), Is.True);
        Assert.That(state.FindLinkPartner(a.StableModuleId), Is.EqualTo(b.StableModuleId));
        Assert.That(state.FindLinkPartner(c.StableModuleId), Is.EqualTo(d.StableModuleId));
        Assert.That(state.MoveModule(a.StableModuleId, 2, 2, out _), Is.True);
        Assert.That(state.FindLinkPartner(a.StableModuleId), Is.EqualTo(b.StableModuleId));
        Assert.That(state.RemoveModule(a.StableModuleId), Is.True);
        Assert.That(state.FindLinkPartner(b.StableModuleId), Is.EqualTo(c.StableModuleId));
        Assert.That(state.FindLinkPartner(d.StableModuleId), Is.Zero);
    }

    [UnityTest]
    public IEnumerator Pass3_TransactionsAndArbitration()
    {
        yield return new EnterPlayMode();
        yield return Transactions();
        yield return new ExitPlayMode();
    }
    private static void FinishCurrentFlight(OrbitalRewardFlowController flow, bool second)
    {
        Call(flow, "FinishFlight", flow.SessionToken, (ulong)Get(flow, "flightToken"), second);
    }
    private static IEnumerator Transactions()
    {
        using var f = new Fixture();
        using var provider = new OrbitalRewardProvider(Array.Empty<UpgradeData>());
        var flow = f.Station.RewardFlow;
        var owner = f.Station.InputOwner;
        int completed = 0, cancelled = 0;
        string baseline = Snapshot(f.State);
        foreach (string phase in new[] { "normal flight", "before target", "first flight", "first preview", "second flight", "disable" })
        {
            Assert.That(flow.Begin(provider.GetDefinition(phase == "normal flight" ? OrbitalRewardKind.ArcEmitter : OrbitalRewardKind.LinkPair),
                () => completed++, () => cancelled++), Is.True);
            Assert.That(owner.CanTransition, Is.False);
            if (phase != "before target") flow.DebugChooseMount(1, 1);
            if (phase == "first preview" || phase == "second flight" || phase == "disable")
            {
                FinishCurrentFlight(flow, false);
                Assert.That(Snapshot(f.State), Is.EqualTo(baseline));
                Assert.That(owner.Mode, Is.EqualTo(OrbitalInteractionMode.RewardSecondTarget));
                Assert.That(Get(flow, "firstLinkPreview"), Is.Not.Null);
            }
            if (phase == "second flight") flow.DebugChooseMount(1, 2);
            ulong token = flow.SessionToken, flight = (ulong)Get(flow, "flightToken");
            int cancellations = cancelled;
            if (phase == "disable") flow.enabled = false;
            else flow.CancelForSceneTransition();
            flow.CancelForSceneTransition();
            Call(flow, "FinishFlight", token, flight, phase == "second flight");
            Assert.That(cancelled, Is.EqualTo(cancellations + 1), phase);
            Assert.That(completed, Is.Zero);
            Assert.That(Snapshot(f.State), Is.EqualTo(baseline), phase);
            Assert.That(Get(flow, "modulePreview"), Is.Null);
            Assert.That(Get(flow, "firstLinkPreview"), Is.Null);
            Assert.That(owner.CanTransition, Is.True);
            Assert.That(owner.CanStartWorldTelekinesis, Is.False, "cancel frame cannot grab world");
            flow.enabled = true;
            Debug.Log("PASS3 cancel " + phase + " completed=" + completed + " cancelled=" + cancelled + " " + baseline);
            yield return null;
        }
        // A target becomes occupied after staging; the unrelated commit survives and no Link is installed.
        Assert.That(flow.Begin(provider.GetDefinition(OrbitalRewardKind.LinkPair), () => completed++, () => cancelled++), Is.True);
        flow.DebugChooseMount(1, 1); FinishCurrentFlight(flow, false);
        flow.DebugChooseMount(1, 2);
        f.Station.InstallModule(OrbitalModuleKind.ArcEmitter, 1, 2, out _);
        string occupied = Snapshot(f.State);
        FinishCurrentFlight(flow, true);
        Assert.That(Snapshot(f.State), Is.EqualTo(occupied));
        Assert.That(flow.State, Is.EqualTo(OrbitalRewardFlowState.SecondLinkPlacement));
        flow.CancelForSceneTransition();
        f.Station.RemoveModule(2);
        // Old callback while a newer session is active cannot complete its flight.
        ulong stale = flow.SessionToken;
        Assert.That(flow.Begin(provider.GetDefinition(OrbitalRewardKind.LinkPair), () => completed++, () => cancelled++), Is.True);
        flow.DebugChooseMount(1, 1);
        Call(flow, "FinishFlight", stale, (ulong)Get(flow, "flightToken"), false);
        Assert.That(flow.State, Is.EqualTo(OrbitalRewardFlowState.ModuleFlight));
        FinishCurrentFlight(flow, false);
        int rev = f.State.Revision, allocator = f.State.NextStableModuleId;
        Assert.That(f.State.Modules.Count, Is.EqualTo(1));
        flow.DebugChooseMount(1, 2);
        ulong session = flow.SessionToken, callback = (ulong)Get(flow, "flightToken");
        Call(flow, "FinishFlight", session, callback, true);
        string success = Snapshot(f.State);
        Call(flow, "FinishFlight", session, callback, true);
        flow.CancelForSceneTransition();
        Assert.That(Snapshot(f.State), Is.EqualTo(success));
        Assert.That(completed, Is.EqualTo(1));
        Assert.That(f.State.Revision, Is.EqualTo(rev + 1));
        Assert.That(f.State.NextStableModuleId, Is.EqualTo(allocator + 2));
        Assert.That(f.State.FindLinkPartner(allocator), Is.EqualTo(allocator + 1));
        AssertView(f);
        Debug.Log("PASS3 Link commit/duplicate: before Revision=" + rev + " NextModuleId=" + allocator +
            " completed=0; after completed=" + completed + " cancelled=" + cancelled + " " + success);
        yield return null;
        // Escape is consumed even when Pause asks after cancellation in the same frame.
        flow.Begin(provider.GetDefinition(OrbitalRewardKind.LinkPair), () => completed++, () => cancelled++);
        Assert.That(owner.TryConsumeEscape(), Is.True);
        Assert.That(owner.TryConsumeEscape(), Is.True);
        Assert.That(owner.CanTransition, Is.True);
        yield return null;
        Assert.That(owner.TryConsumeEscape(), Is.False);
        var relocation = f.Player.GetComponentInChildren<OrbitalRelocationController>();
        relocation.enabled = false;
        Call(relocation, "BeginDrag", f.Station.Modules[0]);
        Assert.That(owner.Mode, Is.EqualTo(OrbitalInteractionMode.Relocation));
        Assert.That(owner.CanTransition, Is.False);
        Assert.That(owner.TryConsumeEscape(), Is.True);
        Assert.That(relocation.IsDragging, Is.False);
        Assert.That(Time.timeScale, Is.EqualTo(1f));
        yield return null;
        Call(relocation, "BeginDrag", f.Station.Modules[0]);
        Time.timeScale = 0f; // Another owner's pause must survive drag cleanup.
        relocation.CancelDrag("external pause");
        Assert.That(Time.timeScale, Is.Zero);
        Time.timeScale = 1f;
        yield return null;
        Assert.That(owner.BeginWorldTelekinesis(), Is.True);
        Assert.That(owner.CanTransition, Is.False);
        Assert.That(owner.BeginRelocation(), Is.True, "relocation preempts world");
        owner.EndRelocation();
        Assert.That(owner.IsIdle, Is.True);
        // Production Link combat uses the same ordered resolver and unchanged matrix/cooldown formula.
        f.Station.UpgradeLinkMatrix();
        var firstNode = f.Station.Modules.Single(m => m.StableModuleId == allocator);
        var secondNode = f.Station.Modules.Single(m => m.StableModuleId == allocator + 1);
        var enemyGo = new GameObject("Link combat target");
        enemyGo.transform.position = (firstNode.WorldPosition + secondNode.WorldPosition) * 0.5f;
        var enemy = enemyGo.AddComponent<EnemyHealth>();
        float hp = (float)Get(enemy, "currentHealth");
        Call(f.Station, "UpdateLinkNodes", 0.02f);
        Assert.That((float)Get(enemy, "currentHealth"), Is.EqualTo(hp - 6.25f).Within(0.001f));
        Call(f.Station, "UpdateLinkNodes", 0.02f);
        Assert.That((float)Get(enemy, "currentHealth"), Is.EqualTo(hp - 6.25f).Within(0.001f), "pair cooldown prevents double hit");
        var newRing = f.Station.AddRing();
        Assert.That(f.Station.MoveModule(allocator, newRing.StableRingId, 0, out _), Is.True);
        Assert.That(f.State.FindLinkPartner(allocator), Is.EqualTo(allocator + 1));
        Assert.That(f.Station.Modules.Single(m => m.StableModuleId == allocator), Is.SameAs(firstNode));
        Object.Destroy(enemyGo);
        Debug.Log("PASS3 Link combat: base 5 * matrix 1.25 = 6.25, cooldown blocks repeat; move preserves partner and runtime.");
        // A real PlayerHealth death before final commit cancels the staged reward.
        var health = f.Player.AddComponent<PlayerHealth>();
        // Rebind the concrete production owner so it observes this health component.
        f.Station.SimulateSectorRestore(); f.Station.enabled = false;
        flow = f.Station.RewardFlow;
        string beforeDeath = Snapshot(f.State);
        flow.Begin(provider.GetDefinition(OrbitalRewardKind.LinkPair), () => completed++, () => cancelled++);
        // Both mounts are occupied from the committed pair; the session is still cancellable before selection.
        int beforeCancel = cancelled;
        Call(health, "Die");
        Call(f.Station.InputOwner, "Update");
        Assert.That(flow.PendingReward, Is.Null);
        Assert.That(cancelled, Is.EqualTo(beforeCancel + 1));
        Assert.That(Snapshot(f.State), Is.EqualTo(beforeDeath));
        Debug.Log("PASS3 arbitration: Escape consumed twice same frame, next frame idle; drag timeScale restored, external pause retained; world preempted; death cancels once without mutation.");
        yield return null;
    }

}
#endif
