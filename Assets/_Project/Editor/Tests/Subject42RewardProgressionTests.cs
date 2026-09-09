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

public sealed class Subject42RewardProgressionTests
{
    public const string Output = "Artifacts/GeneratedQA/RewardProgression/";
    public static UpgradeData[] BodyAssets() => AssetDatabase.FindAssets("t:UpgradeData")
        .Select(g => AssetDatabase.LoadAssetAtPath<UpgradeData>(AssetDatabase.GUIDToAssetPath(g)))
        .Where(d => d.upgradeType is UpgradeType.MaxHealthFlat or UpgradeType.MoveSpeedPercent).ToArray();

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

    [Test]
    public void LegacyMigrationPreservesBuiltMountsAndOccupancy()
    {
        var s = OrbitalRunState.CreateDefault(42);
        s.AddMount(1, out _); s.AddMount(1, out _);
        s.InstallModule(OrbitalModuleKind.ImpulseGun, 1, 2, out _);
        string old = JsonUtility.ToJson(s).Replace("\"Version\":2", "\"Version\":1")
            .Replace("\"MountCount\":3,", "");
        var copy = JsonUtility.FromJson<OrbitalRunState>(old);
        Assert.That(copy.Version, Is.EqualTo(OrbitalRunState.CurrentVersion));
        Assert.That(copy.Rings[0].MountCount, Is.EqualTo(3));
        Assert.That(copy.Modules.Last().MountIndex, Is.EqualTo(2));
        Assert.That(copy.Validate(out var error), Is.True, error);
        copy.Rings[0].MountCount = 1;
        Assert.That(copy.Validate(out _), Is.False, "new state cannot occupy unbuilt mounts");
    }

    [Test]
    public void ProductionPoolAndCapsExcludeImpossibleCards()
    {
        var body = BodyAssets();
        Assert.That(body.Select(d => d.upgradeType).Distinct().Count(), Is.EqualTo(2));
        using var p = new OrbitalRewardProvider(body);
        var s = OrbitalRunState.CreateDefault(1);
        var slots = new RunItemSlots();
        Assert.That(Enum.GetValues(typeof(OrbitalRewardKind)).Cast<OrbitalRewardKind>()
            .Count(OrbitalRewardProvider.IsDemoReward), Is.EqualTo(13));
        foreach (var item in body.GroupBy(d => d.upgradeType).Select(g => g.First()))
            for (int i = 0; i < RunItemSlots.MaxItemLevel; i++) slots.TryAdd(item);
        Assert.That(p.IsEligible(OrbitalRewardKind.MaxHealth, s, slots), Is.False);
        Assert.That(p.IsEligible(OrbitalRewardKind.MoveSpeed, s, slots), Is.False);
        while (s.AddRing() != null) { }
        while (s.UpgradeCore()) { }
        Assert.That(p.IsEligible(OrbitalRewardKind.NewRing, s, slots), Is.False);
        Assert.That(p.IsEligible(OrbitalRewardKind.CoreUpgrade, s, slots), Is.False);
        var random = UnityEngine.Random.state;
        try
        {
            for (int i = 0; i < 100; i++)
                foreach (OrbitalRewardData card in p.BuildChoices(3, s, slots, true))
                {
                    Assert.That(OrbitalRewardProvider.IsDemoReward(card.RewardKind), Is.True);
                    Assert.That(p.IsEligible(card.RewardKind, s, slots), Is.True);
                }
        }
        finally { UnityEngine.Random.state = random; }
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

    [Test]
    public void ProviderSimulation_1000Seeds15ChoicesFourPolicies()
    {
        Directory.CreateDirectory(Output);
        var random = UnityEngine.Random.state;
        var rows = new List<string> { "policy,kind,hands,offer_hands,offer_percent" };
        var builds = new List<string> { "policy,seed,player_level,rings,built,capacity,modules" };
        try
        {
            using var provider = new OrbitalRewardProvider(BodyAssets());
            foreach (string policy in new[] { "A_first", "B_new_toy", "C_build", "D_balanced" })
            {
                var counts = new Dictionary<OrbitalRewardKind, int>(); int moduleHands = 0, statsOnly = 0, eligibleModuleHands = 0, offeredWhenEligible = 0;
                for (int seed = 0; seed < 1000; seed++)
                {
                    UnityEngine.Random.InitState(420000 + seed);
                    var s = OrbitalRunState.CreateDefault(seed);
                    var slots = new RunItemSlots();
                    for (int choice = 1; choice <= 15; choice++)
                    {
                        bool offer = s.BeginLevelUpOpportunity(choice + 1, UnityEngine.Random.value);
                        var hand = provider.BuildChoices(3, s, slots, offer).Cast<OrbitalRewardData>().ToArray();
                        Assert.That(hand.Length, Is.GreaterThan(0));
                        foreach (var card in hand)
                        {
                            Assert.That(provider.IsEligible(card.RewardKind, s, slots), Is.True);
                            counts[card.RewardKind] = counts.GetValueOrDefault(card.RewardKind) + 1;
                        }
                        bool hasModule = hand.Any(d => ModuleKinds.Contains(d.RewardKind));
                        if (hasModule) moduleHands++;
                        if (!hasModule && hand.All(d => d.RewardKind != OrbitalRewardKind.NewRing && d.RewardKind != OrbitalRewardKind.CoreUpgrade)) statsOnly++;
                        if (s.FreeBuiltMounts > 0) { eligibleModuleHands++; if (hasModule) offeredWhenEligible++; }
                        float Score(OrbitalRewardData d)
                        {
                            if (policy == "A_first") return 0;
                            bool module = ModuleKinds.Contains(d.RewardKind);
                            if (policy == "B_new_toy") return module ? 100 : d.RewardKind == OrbitalRewardKind.NewRing ? 90 : d.RewardKind == OrbitalRewardKind.AddMount ? 80 : d.RewardKind == OrbitalRewardKind.RingCapacity ? 70 : 0;
                            if (policy == "C_build") return d.RewardKind == OrbitalRewardKind.NewRing ? 100 : d.RewardKind == OrbitalRewardKind.AddMount ? 90 : d.RewardKind == OrbitalRewardKind.RingCapacity ? 80 : module ? 70 : 0;
                            if (module) return 90;
                            if (d.RewardKind == OrbitalRewardKind.NewRing) return 85;
                            if (s.FreeBuiltMounts == 0 && d.RewardKind == OrbitalRewardKind.AddMount) return 95;
                            if (s.FreeBuiltMounts == 0 && d.RewardKind == OrbitalRewardKind.RingCapacity) return 80;
                            return d.RewardKind == OrbitalRewardKind.CoreUpgrade ? 75 : d.BodyUpgrade != null ? 60 : 50;
                        }
                        Apply(s, slots, hand.OrderByDescending(Score).First());
                        Assert.That(s.Validate(out var error), Is.True, error);
                        if (new[] { 5, 10, 15 }.Contains(choice + 1))
                            builds.Add($"{policy},{seed},{choice + 1},{s.Rings.Count},{s.Rings.Sum(r => r.MountCount)},{s.Rings.Sum(r => r.MountCapacity)},{s.Modules.Count}");
                    }
                }
                foreach (var kind in Enum.GetValues(typeof(OrbitalRewardKind)).Cast<OrbitalRewardKind>().Where(OrbitalRewardProvider.IsDemoReward))
                    rows.Add(FormattableString.Invariant($"{policy},{kind},15000,{counts.GetValueOrDefault(kind)},{counts.GetValueOrDefault(kind)/150.0:F2}"));
                rows.Add(FormattableString.Invariant($"{policy},ANY_MODULE,15000,{moduleHands},{moduleHands/150.0:F2}"));
                rows.Add(FormattableString.Invariant($"{policy},INFRA_STATS_ONLY,15000,{statsOnly},{statsOnly/150.0:F2}"));
                rows.Add(FormattableString.Invariant($"{policy},MODULE_WHEN_ELIGIBLE,{eligibleModuleHands},{offeredWhenEligible},{100.0*offeredWhenEligible/eligibleModuleHands:F2}"));
            }
            File.WriteAllLines(Output + "provider-offers.csv", rows);
            File.WriteAllLines(Output + "build-distributions.csv", builds);
        }
        finally { UnityEngine.Random.state = random; }
    }
}
#endif
