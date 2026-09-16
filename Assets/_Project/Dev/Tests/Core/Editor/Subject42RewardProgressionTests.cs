#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Subject42.Combat.OrbitalStation;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public sealed partial class Subject42RewardProgressionTests
{
    public const string Output = "Artifacts/GeneratedQA/RewardProgression/";
    public static UpgradeData[] BodyAssets() => AssetDatabase.FindAssets("t:UpgradeData")
        .Select(g => AssetDatabase.LoadAssetAtPath<UpgradeData>(AssetDatabase.GUIDToAssetPath(g)))
        .Where(d => d.upgradeType is UpgradeType.MaxHealthFlat or UpgradeType.MoveSpeedPercent).ToArray();

    [Category("Core")]
    [Test]
    public void BuiltMountsCapacityAndLinkFollowPlayerScenario()
    {
        var s = OrbitalRunState.CreateDefault(42);
        var slots = new RunItemSlots();
        using var p = new OrbitalRewardProvider(BodyAssets());
        Assert.That(s.Rings.Count, Is.EqualTo(1));
        Assert.That(s.Modules.Single().ModuleType, Is.EqualTo(OrbitalModuleKind.Pistol));
        Assert.That(s.Rings[0].MountCount, Is.EqualTo(1));
        Assert.That(s.Rings[0].MountCapacity, Is.EqualTo(3));
        Assert.That(s.FreeBuiltMounts, Is.Zero);
        Assert.That(p.IsEligible(OrbitalRewardKind.AddMount, s, slots), Is.True);
        Assert.That(p.IsEligible(OrbitalRewardKind.RingCapacity, s, slots), Is.False);
        foreach (var kind in ModuleKinds)
            Assert.That(p.IsEligible(kind, s, slots), Is.False, kind.ToString());
        Assert.That(s.InstallModule(OrbitalModuleKind.Pistol, 1, 1, out _), Is.False);
        Assert.That(s.AddMount(1, out _), Is.True);
        Assert.That(s.Rings[0].MountCount, Is.EqualTo(2));
        Assert.That(p.IsEligible(OrbitalRewardKind.Pistol, s, slots), Is.True);
        Assert.That(p.IsEligible(OrbitalRewardKind.LinkPair, s, slots), Is.False);
        Assert.That(s.UpgradeRingCapacity(1), Is.False);
        Assert.That(s.AddMount(1, out _), Is.True);
        Assert.That(s.FreeBuiltMounts, Is.EqualTo(2));
        Assert.That(p.IsEligible(OrbitalRewardKind.LinkPair, s, slots), Is.True);
        Assert.That(p.IsEligible(OrbitalRewardKind.AddMount, s, slots), Is.False);
        Assert.That(p.IsEligible(OrbitalRewardKind.RingCapacity, s, slots), Is.True);
        string before = JsonUtility.ToJson(s);
        Assert.That(s.InstallLinkPair(1, 1, 1, 0, out _, out _, out _), Is.False);
        Assert.That(JsonUtility.ToJson(s), Is.EqualTo(before), "stale/occupied second target is atomic");
        Assert.That(s.InstallModule(OrbitalModuleKind.ArcEmitter, 1, 1, out _), Is.True);
        Assert.That(p.IsEligible(OrbitalRewardKind.LinkPair, s, slots), Is.False);
        var empty = s.AddRing();
        Assert.That(empty.MountCount, Is.EqualTo(1));
        Assert.That(s.CanTargetRingReward(OrbitalRewardKind.RingPower, empty.StableRingId), Is.False);
        Assert.That(s.CanTargetRingReward(OrbitalRewardKind.RingSpeed, empty.StableRingId), Is.False);
        int revision = s.Revision;
        Assert.That(s.InstallLinkPair(1, 2, empty.StableRingId, 0, out _, out _, out _), Is.True);
        Assert.That(s.Revision, Is.EqualTo(revision + 1));
        Assert.That(s.ResolveLinkPairs().Count(), Is.EqualTo(1));
        for (int cap = 4; cap <= 6; cap++)
        {
            Assert.That(s.UpgradeRingCapacity(1), Is.True);
            Assert.That(s.Rings[0].MountCapacity, Is.EqualTo(cap));
            Assert.That(s.Rings[0].MountCount, Is.EqualTo(cap - 1));
            Assert.That(s.UpgradeRingCapacity(1), Is.False);
            Assert.That(s.AddMount(1, out _), Is.True);
            Assert.That(s.AddMount(1, out _), Is.False);
        }
        Assert.That(s.UpgradeRingCapacity(1), Is.False);
        Assert.That(s.Validate(out var error), Is.True, error);
        var copy = JsonUtility.FromJson<OrbitalRunState>(JsonUtility.ToJson(s));
        Assert.That(JsonUtility.ToJson(copy), Is.EqualTo(JsonUtility.ToJson(s)));
    }

    [Category("Core")]
    [Test]
    public void PendingCasinoGrantPreservesIdentityAndPublishesOnlyCandidate()
    {
        foreach (var (symbol, kind) in new[] { (OrbitalSlotSymbol.Gun, OrbitalModuleKind.Pistol), (OrbitalSlotSymbol.Sword, OrbitalModuleKind.LaserSword), (OrbitalSlotSymbol.Impulse, OrbitalModuleKind.ImpulseGun), (OrbitalSlotSymbol.Arc, OrbitalModuleKind.ArcEmitter), (OrbitalSlotSymbol.Link, OrbitalModuleKind.LinkNode) })
        {
            TestContext.Progress.WriteLine("PendingCasinoGrantPreservesIdentityAndPublishesOnlyCandidate: " + symbol);
            AssertPendingCasinoGrantPreservesIdentityAndPublishesOnlyCandidate(symbol, kind);
        }
    }

    private void AssertPendingCasinoGrantPreservesIdentityAndPublishesOnlyCandidate(
        OrbitalSlotSymbol symbol, OrbitalModuleKind kind)
    {
        var state = OrbitalRunState.CreateDefault(42);
        string before = JsonUtility.ToJson(state);
        Assert.That(UpgradeManager.TryGrantPendingCasinoBonus(state, symbol, out var granted), Is.True);
        Assert.That(JsonUtility.ToJson(state), Is.EqualTo(before));
        var added = granted.Modules.Skip(state.Modules.Count).ToArray();
        Assert.That(added.Length, Is.EqualTo(symbol == OrbitalSlotSymbol.Link ? 2 : 1));
        Assert.That(added.All(m => m.ModuleType == kind), Is.True);
        Assert.That(granted.Validate(out var error), Is.True, error);
    }

    [Category("Core")]
    [Test]
    public void PendingCasinoRingUsesStateCommands()
    {
        foreach (bool custom in new[] { false, true })
        {
            TestContext.Progress.WriteLine("PendingCasinoRingUsesStateCommands: " + custom);
            AssertPendingCasinoRingUsesStateCommands(custom);
        }
    }

    private void AssertPendingCasinoRingUsesStateCommands(bool custom)
    {
        var state = OrbitalRunState.CreateDefault(42, customPaths: custom);
        state.BeginLevelUpOpportunity(2, 1f);
        Assert.That(UpgradeManager.TryGrantPendingCasinoBonus(state, OrbitalSlotSymbol.Ring, out var granted), Is.True);
        Assert.That(granted.Rings.Count, Is.EqualTo(state.Rings.Count + 1));
        Assert.That(granted.PendingCasinoRingId != 0, Is.EqualTo(custom));
        Assert.That(granted.RingOfferMissCount, Is.Zero, "Actual ring acquisition retains the existing pity reset");
        Assert.That(granted.LastProcessedPlayerLevel, Is.EqualTo(state.LastProcessedPlayerLevel));
        Assert.That(granted.Validate(out var error), Is.True, error);
    }

    [Category("Core")]
    [Test]
    public void RejectedPendingCasinoGrantDoesNotPublishOrMutate()
    {
        var state = OrbitalRunState.CreateDefault(42);
        while (state.TryAddRing(out _, out _)) { }
        string before = JsonUtility.ToJson(state);
        Assert.That(UpgradeManager.TryGrantPendingCasinoBonus(state, OrbitalSlotSymbol.Ring, out var granted), Is.False);
        Assert.That(granted, Is.Null);
        Assert.That(JsonUtility.ToJson(state), Is.EqualTo(before));
    }

    public static readonly OrbitalRewardKind[] ModuleKinds = {
        OrbitalRewardKind.Pistol, OrbitalRewardKind.LaserSword, OrbitalRewardKind.ImpulseGun,
        OrbitalRewardKind.ArcEmitter, OrbitalRewardKind.LinkPair };

    public static void Apply(OrbitalRunState s, RunItemSlots slots, OrbitalRewardData card)
    {
        var kind = card.RewardKind;
        if (card.BodyUpgrade != null)
        {
            Assert.That(slots.TryAdd(card.BodyUpgrade), Is.EqualTo(ItemGrantResult.Added).Or.EqualTo(ItemGrantResult.LeveledUp));
            return;
        }
        if (kind == OrbitalRewardKind.NewRing) { Assert.That(s.AddRing(), Is.Not.Null); return; }
        if (kind == OrbitalRewardKind.CoreUpgrade) { Assert.That(s.UpgradeCore(), Is.True); return; }
        if (ModuleKinds.Contains(kind))
        {
            var free = s.Rings.SelectMany(r => Enumerable.Range(0, r.MountCount)
                .Where(m => s.IsMountFree(r.StableRingId, m)).Select(m => (ring: r.StableRingId, mount: m))).ToArray();
            if (kind == OrbitalRewardKind.LinkPair)
                Assert.That(s.InstallLinkPair(free[0].ring, free[0].mount, free[1].ring, free[1].mount, out _, out _, out _), Is.True);
            else Assert.That(s.InstallModule((OrbitalModuleKind)Enum.Parse(typeof(OrbitalModuleKind), kind.ToString()), free[0].ring, free[0].mount, out _), Is.True);
            return;
        }
        int id = s.Rings.First(r => s.CanTargetRingReward(kind, r.StableRingId)).StableRingId;
        Assert.That(kind switch {
            OrbitalRewardKind.AddMount => s.AddMount(id, out _),
            OrbitalRewardKind.RingCapacity => s.UpgradeRingCapacity(id),
            OrbitalRewardKind.RingPower => s.UpgradeRingPower(id),
            OrbitalRewardKind.RingSpeed => s.UpgradeRingSpeed(id), _ => false }, Is.True);
    }

}
#endif
