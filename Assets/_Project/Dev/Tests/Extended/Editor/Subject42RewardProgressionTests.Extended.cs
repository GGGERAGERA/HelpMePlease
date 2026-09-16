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

    [Category("Extended")]
    [Test]
    public void NormalSnapshotAndChoiceHandShareDefinitions()
    {
        using var provider = new OrbitalRewardProvider(BodyAssets());
        var state = OrbitalRunState.CreateDefault(42);
        var slots = new RunItemSlots();
        var snapshot = provider.GetEligibleNormalRewards(state, slots);
        var hand = provider.BuildChoices(3, state, slots);
        Assert.That(hand.Count, Is.EqualTo(3));
        Assert.That(hand.All(snapshot.Contains), Is.True);
        Assert.That(snapshot.Cast<OrbitalRewardData>().Any(r => r.RewardKind == OrbitalRewardKind.NewRing), Is.False);
        Assert.That(provider.GetEligibleNormalRewards(null, slots), Is.Empty);
    }

    [Category("Extended")]
    [Test]
    public void PresentedSnapshotDoesNotRerollWhenModuleBecomesIneligible()
    {
        using var provider = new OrbitalRewardProvider(BodyAssets());
        var state = OrbitalRunState.CreateDefault(42);
        var slots = new RunItemSlots();
        Assert.That(state.TryAddMount(1, out _), Is.True);
        var snapshot = provider.GetEligibleNormalRewards(state, slots);
        var sword = snapshot.OfType<OrbitalRewardData>().Single(r => r.RewardKind == OrbitalRewardKind.LaserSword);
        Assert.That(state.TryInstallModule(OrbitalModuleKind.Pistol, 1, 1, out _, out _), Is.True);
        Assert.That(provider.IsEligible(sword.RewardKind, state, slots), Is.False);
        Assert.That(snapshot.Contains(sword), Is.True, "An already presented snapshot retains reward identity");
        Assert.That(provider.GetEligibleNormalRewards(state, slots).Contains(sword), Is.False);
        Assert.That(sword.RewardKind, Is.EqualTo(OrbitalRewardKind.LaserSword));
    }

    [Category("Extended")]
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

    [Category("Extended")]
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

    [Category("Extended")]
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
