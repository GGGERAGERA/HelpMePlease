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

[Serializable]
public sealed class GoldenPathResult
{
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
            OrbitalRewardKind.LaserSword => NewModulesAre(before, after, OrbitalModuleKind.LaserSword, 1),
            OrbitalRewardKind.ImpulseGun => NewModulesAre(before, after, OrbitalModuleKind.ImpulseGun, 1),
            OrbitalRewardKind.ArcEmitter => NewModulesAre(before, after, OrbitalModuleKind.ArcEmitter, 1),
            OrbitalRewardKind.LinkPair => NewModulesAre(before, after, OrbitalModuleKind.LinkNode, 2),
            OrbitalRewardKind.RingPower => after.Rings.Sum(r => r.PowerUpgradeLevel) - before.Rings.Sum(r => r.PowerUpgradeLevel) == 1,
            OrbitalRewardKind.RingSpeed => after.Rings.Sum(r => r.SpeedUpgradeLevel) - before.Rings.Sum(r => r.SpeedUpgradeLevel) == 1,
            OrbitalRewardKind.ModuleDamage => after.Modules.Sum(m => m.DamageLevel) - before.Modules.Sum(m => m.DamageLevel) == 1,
            OrbitalRewardKind.CoreUpgrade => after.CoreState.Level - before.CoreState.Level == 1,
            OrbitalRewardKind.LinkMatrix => after.CoreState.LinkMatrixUpgradeLevel - before.CoreState.LinkMatrixUpgradeLevel == 1,
            _ => false
        };
        return delta && after.Revision == before.Revision + 1;
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
