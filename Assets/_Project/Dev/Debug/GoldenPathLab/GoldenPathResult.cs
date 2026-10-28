#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using System.Linq;
using Subject42.Combat.OrbitalStation;
using UnityEngine;

[Serializable]
public sealed class GoldenPathFailure
{
    public string Assertion;
    public int Sector;
    public float SimulationTime;
    public string State;
    public string Reason;
}

public readonly struct GoldenBunkerComponentState
{
    public readonly string Key;
    public readonly bool Active, HasRunState;
    public GoldenBunkerComponentState(string key, bool active, bool hasRunState)
    { Key = key; Active = active; HasRunState = hasRunState; }
}

[Serializable]
public sealed class GoldenPathResult
{
    public static bool BunkerComponentsMatchBaseline(GoldenBunkerComponentState[] baseline,
        GoldenBunkerComponentState[] current, bool runObjectsDestroyed)
    {
        if (!runObjectsDestroyed || baseline.Any(s => s.HasRunState) || current.Any(s => s.HasRunState)) return false;
        // New bunker instances are expected after loading MainMenu. Match authored
        // idle components and their multiplicity; run-owned instance destruction
        // remains a separate mandatory condition.
        return baseline.Where(s => s.Active).Select(s => s.Key).OrderBy(key => key, StringComparer.Ordinal)
            .SequenceEqual(current.Where(s => s.Active).Select(s => s.Key).OrderBy(key => key, StringComparer.Ordinal));
    }
    public string Result = "RUNNING";
    public int Seed;
    public float Duration;
    public int SectorsCompleted, RewardsTaken;
    public bool BossKilled;
    public int FinalRingCount, FinalModuleCount, AssertionsPassed, AssertionsFailed;
    public int BossSpawns, VictoryCalls, ObjectivesCompleted, DirectMountSelections;
    public bool BunkerClean, SecondRunBaseline;
    public List<string> CheckedAssertions = new();
    public List<GoldenPathFailure> Failures = new();
    public List<string> RecentDamage = new();

    public static bool RewardDeltaIsExact(OrbitalRunState before, OrbitalRunState after, OrbitalRewardKind kind)
    {
        int rings = after.Rings.Count - before.Rings.Count;
        int modules = after.Modules.Count - before.Modules.Count;
        int mounts = after.Rings.Sum(r => r.MountCount) - before.Rings.Sum(r => r.MountCount);
        bool delta = kind switch
        {
            OrbitalRewardKind.NewRing => rings == 1 && modules == 0 && mounts == 1,
            OrbitalRewardKind.AddMount => rings == 0 && modules == 0 && mounts == 1,
            OrbitalRewardKind.RingCapacity => rings == 0 && modules == 0 && mounts == 0 &&
                after.Rings.Sum(r => r.MountCapacity) - before.Rings.Sum(r => r.MountCapacity) == 1,
            OrbitalRewardKind.Pistol => NewModulesAre(before, after, OrbitalModuleKind.Pistol, 1),
            OrbitalRewardKind.Sniper => NewModulesAre(before, after, OrbitalModuleKind.Sniper, 1),
            OrbitalRewardKind.ShootingDamage => CategoryDeltaIsExact(before, after, kind),
            OrbitalRewardKind.ShootingRate => CategoryDeltaIsExact(before, after, kind),
            OrbitalRewardKind.ContactDamage => CategoryDeltaIsExact(before, after, kind),
            OrbitalRewardKind.ContactSize => CategoryDeltaIsExact(before, after, kind),
            OrbitalRewardKind.Saw => NewModulesAre(before, after, OrbitalModuleKind.Saw, 1),
            OrbitalRewardKind.Shotgun => NewModulesAre(before, after, OrbitalModuleKind.Shotgun, 1),
            OrbitalRewardKind.ArcEmitter => NewModulesAre(before, after, OrbitalModuleKind.ArcEmitter, 1),
            OrbitalRewardKind.LinkPair => NewModulesAre(before, after, OrbitalModuleKind.LinkNode, 2),
            OrbitalRewardKind.RingPower => after.Rings.Sum(r => r.PowerUpgradeLevel) - before.Rings.Sum(r => r.PowerUpgradeLevel) == 1,
            OrbitalRewardKind.RingSpeed => after.Rings.Sum(r => r.SpeedUpgradeLevel) - before.Rings.Sum(r => r.SpeedUpgradeLevel) == 1,
            OrbitalRewardKind.ModuleDamage => after.Modules.Sum(m => m.DamageLevel) - before.Modules.Sum(m => m.DamageLevel) == 1,
            OrbitalRewardKind.LinkMatrix => after.LinkMatrixUpgradeLevel - before.LinkMatrixUpgradeLevel == 1,
            _ => false
        };
        return delta && after.Revision == before.Revision + 1;
    }

    private static bool CategoryDeltaIsExact(OrbitalRunState before, OrbitalRunState after, OrbitalRewardKind kind)
    {
        // Store expected products as float32, matching the production multiplier fields.
        var expected = new float[4]
        {
            before.ShootingDamageMultiplier * (kind == OrbitalRewardKind.ShootingDamage ? 1.2f : 1f),
            before.ShootingRateMultiplier * (kind == OrbitalRewardKind.ShootingRate ? 1.15f : 1f),
            before.ContactDamageMultiplier * (kind == OrbitalRewardKind.ContactDamage ? 1.2f : 1f),
            before.ContactSizeMultiplier * (kind == OrbitalRewardKind.ContactSize ? 1.15f : 1f)
        };
        return after.ShootingDamageMultiplier == expected[0] &&
            after.ShootingRateMultiplier == expected[1] &&
            after.ContactDamageMultiplier == expected[2] &&
            after.ContactSizeMultiplier == expected[3] &&
            before.Rings.Select(r => JsonUtility.ToJson(r)).SequenceEqual(after.Rings.Select(r => JsonUtility.ToJson(r))) &&
            before.Modules.Select(m => JsonUtility.ToJson(m)).SequenceEqual(after.Modules.Select(m => JsonUtility.ToJson(m)));
    }

    private static bool NewModulesAre(OrbitalRunState before, OrbitalRunState after, OrbitalModuleKind kind, int count)
    {
        var added = after.Modules.Where(m => before.FindModule(m.StableModuleId) == null).ToArray();
        return after.Rings.Count == before.Rings.Count && after.Modules.Count - before.Modules.Count == count &&
            added.Length == count && added.All(m => m.ModuleType == kind);
    }

    public static bool BaselineMatches(OrbitalRunState actual)
    {
        var expected = OrbitalRunState.CreateDefault(actual.RunId);
        var copy = JsonUtility.FromJson<OrbitalRunState>(JsonUtility.ToJson(actual));
        copy.Revision = expected.Revision; copy.RestoreCount = expected.RestoreCount;
        foreach (var ring in copy.Rings) ring.CurrentPhase = 0;
        foreach (var ring in expected.Rings) ring.CurrentPhase = 0;
        return JsonUtility.ToJson(copy) == JsonUtility.ToJson(expected);
    }

    public bool Check(string name, bool valid, int sector, float time, string state, string reason)
    {
        if (!CheckedAssertions.Contains(name)) CheckedAssertions.Add(name);
        if (valid) { AssertionsPassed++; return true; }
        AssertionsFailed++;
        Failures.Add(new GoldenPathFailure { Assertion = name, Sector = sector,
            SimulationTime = time, State = state, Reason = reason });
        Result = "FAIL";
        return false;
    }
}

[Serializable]
public sealed class GoldenPathBatchHistory
{
    public List<BotBatchResult> Batches = new();
}
#endif
