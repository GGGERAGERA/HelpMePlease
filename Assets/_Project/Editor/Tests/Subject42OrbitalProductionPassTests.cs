#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Subject42.Combat.OrbitalStation;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

public sealed class Subject42OrbitalProductionPassTests
{
    public const string Output = "Artifacts/GeneratedQA/OrbitalProductionPass/";
    [Test]
    public void Pity_OfferSkipAcquireAndCap_UseAuthoritativeState()
    {
        var s = OrbitalRunState.CreateDefault(1);
        var c = OrbitalProgressionConfig.Default;
        Assert.That(c.GetRingOfferChance(s.RingOfferMissCount), Is.EqualTo(.05f));
        Assert.That(s.BeginLevelUpOpportunity(2, .99f), Is.False);
        Assert.That(s.Rings.Count, Is.EqualTo(1));
        Assert.That(c.GetRingOfferChance(s.RingOfferMissCount), Is.EqualTo(.07f).Within(.00001f));
        Assert.That(s.BeginLevelUpOpportunity(3, 0f), Is.True);
        Assert.That(s.Rings.Count, Is.EqualTo(1), "offer does not acquire");
        Assert.That(s.RingOfferMissCount, Is.EqualTo(2), "skip retains opportunity");
        Assert.That(s.BeginLevelUpOpportunity(3, 0f), Is.False, "reopening cannot roll again");
        Assert.That(s.RingOfferMissCount, Is.EqualTo(2));
        var added = s.AddRing();
        Assert.That(added.MountCapacity, Is.EqualTo(3));
        Assert.That(added.VisualTier, Is.EqualTo(1));
        Assert.That(added.PowerMultiplier, Is.EqualTo(1));
        Assert.That(added.SpeedUpgradeLevel, Is.Zero);
        Assert.That(s.RingOfferMissCount, Is.Zero);
        for (int level = 4; level < 100; level++) Assert.That(s.BeginLevelUpOpportunity(level, .99f), Is.False);
        Assert.That(c.GetRingOfferChance(s.RingOfferMissCount), Is.EqualTo(.45f));
        Assert.That(s.Rings.Count, Is.EqualTo(2), "all former milestones are inert");
        var copy = JsonUtility.FromJson<OrbitalRunState>(JsonUtility.ToJson(s));
        Assert.That(copy.RingOfferMissCount, Is.EqualTo(s.RingOfferMissCount));
        while (s.AddRing() != null) { }
        s.BeginLevelUpOpportunity(100, 0f);
        int misses = s.RingOfferMissCount;
        Assert.That(s.AddRing(), Is.Null);
        Assert.That(s.RingOfferMissCount, Is.EqualTo(misses), "failed acquisition never resets");
    }

    [Test]
    public void Targets_EmptyPowerSpeedAndUnfilledCapacityAreExcluded()
    {
        var s = OrbitalRunState.CreateDefault(1);
        int empty = s.AddRing().StableRingId;
        Assert.That(s.CanTargetRingReward(OrbitalRewardKind.RingPower, empty), Is.False);
        Assert.That(s.CanTargetRingReward(OrbitalRewardKind.RingSpeed, empty), Is.False);
        Assert.That(s.CanTargetRingReward(OrbitalRewardKind.AddMount, 1), Is.True);
        Assert.That(s.CanTargetRingReward(OrbitalRewardKind.RingCapacity, 1), Is.False);
        s.AddMount(1, out _); s.AddMount(1, out _);
        s.InstallModule(OrbitalModuleKind.Pistol, 1, 1, out _);
        s.InstallModule(OrbitalModuleKind.Pistol, 1, 2, out _);
        Assert.That(s.CanTargetRingReward(OrbitalRewardKind.AddMount, 1), Is.False);
        Assert.That(s.CanTargetRingReward(OrbitalRewardKind.RingCapacity, 1), Is.True);
        s.UpgradeRingCapacity(1);
        Assert.That(s.CanTargetRingReward(OrbitalRewardKind.AddMount, 1), Is.True);
        s.MoveModule(1, empty, 0, out _);
        Assert.That(s.CanTargetRingReward(OrbitalRewardKind.RingPower, empty), Is.True);
        Assert.That(s.CanTargetRingReward(OrbitalRewardKind.RingSpeed, empty), Is.True);
    }

    [Test]
    public void MonteCarlo_100000CyclesPerPolicy()
    {
        Directory.CreateDirectory(Output);
        const int count = 100000;
        var config = OrbitalProgressionConfig.Default;
        var output = new System.Text.StringBuilder("Deterministic System.Random; production config formula; no anomaly, no station cap between independent cycles. 100000 cycles per policy.\n");
        output.AppendLine("scenario,mean,median,P75,P90,P95,P99,min,max,cap_cycles,cap_fraction");
        foreach (string policy in new[] { "OFFER", "ACQUIRE_ALWAYS", "ACQUIRE_50_PERCENT", "ACQUIRE_AFTER_TWO_SKIPS" })
        {
            var random = new System.Random(420900 + Array.IndexOf(new[] { "OFFER", "ACQUIRE_ALWAYS", "ACQUIRE_50_PERCENT", "ACQUIRE_AFTER_TWO_SKIPS" }, policy));
            var lengths = new int[count]; int cap = 0;
            for (int i = 0; i < count; i++)
            {
                int misses = 0, offers = 0; bool reachedCap = false;
                while (true)
                {
                    float chance = config.GetRingOfferChance(misses);
                    reachedCap |= chance >= config.MaxRingOfferChance;
                    bool offer = random.NextDouble() < chance;
                    misses++;
                    if (offer)
                    {
                        offers++;
                        if (policy == "OFFER" || policy == "ACQUIRE_ALWAYS" ||
                            (policy == "ACQUIRE_50_PERCENT" && random.NextDouble() < .5) ||
                            (policy == "ACQUIRE_AFTER_TWO_SKIPS" && offers >= 3)) break;
                    }
                }
                lengths[i] = misses; if (reachedCap) cap++;
            }
            Array.Sort(lengths);
            int Q(double q) => lengths[(int)Math.Ceiling(count * q) - 1];
            output.AppendLine(FormattableString.Invariant($"{policy},{lengths.Average():F4},{Q(.5)},{Q(.75)},{Q(.9)},{Q(.95)},{Q(.99)},{lengths[0]},{lengths[count-1]},{cap},{(double)cap/count:F6}"));
            File.WriteAllLines(Output + policy + "-histogram.csv", new[] { "level_ups,cycles" }.Concat(lengths.GroupBy(n => n).Select(g => $"{g.Key},{g.Count()}")));
        }
        File.WriteAllText(Output + "monte-carlo.csv", output.ToString());
    }

    public static bool IsModule(OrbitalRewardKind kind) => kind is OrbitalRewardKind.Pistol or OrbitalRewardKind.LaserSword or OrbitalRewardKind.ImpulseGun or OrbitalRewardKind.ArcEmitter;
    public static void Apply(OrbitalRunState s, OrbitalRewardKind kind)
    {
        using var provider = new OrbitalRewardProvider(Array.Empty<UpgradeData>());
        Subject42RewardProgressionTests.Apply(s, new RunItemSlots(), provider.GetDefinition(kind));
    }

    [UnityTest]
    public IEnumerator Provider_200Seeds15Choices_WithAndWithoutAnomaly()
    {
        Directory.CreateDirectory(Output);
        yield return new EnterPlayMode();
        Application.runInBackground = true;
        var randomBefore = UnityEngine.Random.state;
        var run = RunStateManager.EnsureExists();
        var stage = ScriptableObject.CreateInstance<StageProfileData>();
        var rule = ScriptableObject.CreateInstance<WorldRuleData>();
        var anomaly = ScriptableObject.CreateInstance<LocalAnomalyData>();
        var report = new System.Text.StringBuilder("Production provider + state commits; 200 seeds (42..241) x 15 choices (player levels 2..16). Policy: NewRing always, otherwise first weighted card. Guaranteed anomaly immediately after choice at level 6.\n");
        report.AppendLine("anomaly,hands,module_hands,early_no_module_hands,core_hands,new_ring_hands,mean_offers,mean_acquired_rings,mean_total_rings");
        try
        {
            using (var provider = new OrbitalRewardProvider(Array.Empty<UpgradeData>()))
            foreach (bool guaranteed in new[] { false, true })
            {
                int module = 0, earlyNoModule = 0, core = 0, newRing = 0, acquired = 0, total = 0;
                var distributions = new Dictionary<int,List<int>> { [5] = new(), [10] = new(), [15] = new() };
                for (int seed = 42; seed < 242; seed++)
                {
                    UnityEngine.Random.InitState(seed);
                    run.BeginNewRun(null, null, stage, rule, anomaly);
                    var s = run.OrbitalStationState;
                    for (int choice = 1; choice <= 15; choice++)
                    {
                        int level = choice + 1;
                        bool offer = s.BeginLevelUpOpportunity(level, UnityEngine.Random.value);
                        var hand = provider.BuildChoices(3, offer).Cast<OrbitalRewardData>().ToArray();
                        Assert.That(hand.All(d => OrbitalRewardProvider.IsDemoReward(d.RewardKind)), Is.True);
                        if (hand.Any(d => IsModule(d.RewardKind))) module++; else if (choice <= 3) earlyNoModule++;
                        if (hand.Any(d => d.RewardKind == OrbitalRewardKind.CoreUpgrade)) core++;
                        if (hand.Any(d => d.RewardKind == OrbitalRewardKind.NewRing)) newRing++;
                        if (hand.Length > 0) Apply(s, hand[0].RewardKind);
                        if (guaranteed && level == 6)
                        {
                            Assert.That(s.AddRing(), Is.Not.Null);
                            Assert.That(s.RingOfferMissCount, Is.Zero, "guaranteed ring reset");
                        }
                        if (distributions.ContainsKey(level)) distributions[level].Add(s.Rings.Count);
                        Assert.That(s.Validate(out string error), Is.True, error);
                    }
                    acquired += s.Rings.Count - 1; total += s.Rings.Count;
                }
                report.AppendLine(FormattableString.Invariant($"{guaranteed},3000,{module},{earlyNoModule},{core},{newRing},{newRing/200.0:F3},{acquired/200.0:F3},{total/200.0:F3}"));
                File.WriteAllLines(Output + $"ring-distribution-anomaly-{guaranteed}.csv", new[] { "player_level,rings,runs" }.Concat(distributions.SelectMany(k => k.Value.GroupBy(n => n).OrderBy(g => g.Key).Select(g => $"{k.Key},{g.Key},{g.Count()}"))));
            }
            File.WriteAllText(Output + "provider.csv", report.ToString());
        }
        finally
        {
            UnityEngine.Random.state = randomBefore;
            Object.Destroy(stage); Object.Destroy(rule); Object.Destroy(anomaly);
        }
        yield return new ExitPlayMode();
    }
    [UnityTearDown] public IEnumerator Cleanup() { if (Application.isPlaying) yield return new ExitPlayMode(); }
}
#endif
