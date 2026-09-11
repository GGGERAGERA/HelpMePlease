#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

[Serializable]
public sealed class BotDistribution
{
    public float Average, Median, Minimum, Maximum, P10, P50, P90;
    public static BotDistribution From(IEnumerable<float> values)
    {
        var sorted = values.OrderBy(v => v).ToArray();
        if (sorted.Length == 0) return new BotDistribution();
        return new BotDistribution { Average = sorted.Average(), Minimum = sorted[0], Maximum = sorted[sorted.Length - 1],
            Median = Percentile(sorted, .5f), P10 = Percentile(sorted, .1f), P50 = Percentile(sorted, .5f), P90 = Percentile(sorted, .9f) };
    }
    // Linear interpolation at (N-1)*p; the same definition is used for median and percentiles.
    private static float Percentile(float[] sorted, float p)
    {
        float index = (sorted.Length - 1) * p;
        int lower = (int)index, upper = Math.Min(lower + 1, sorted.Length - 1);
        return sorted[lower] + (sorted[upper] - sorted[lower]) * (index - lower);
    }
}
[Serializable]
public sealed class BotRewardFrequency { public string Reward; public int Count; }
[Serializable]
public sealed class BotSuspiciousRun { public int RunIndex, Seed; public string Result, Reason; public float Duration, DamageTaken; }

[Serializable]
public sealed class BotBatchResult
{
    public string BatchId = Guid.NewGuid().ToString("N");
    public string StartedAt = DateTime.UtcNow.ToString("O");
    public string CompletedAt;
    public string Status = "Running";
    public string StopReason;
    public string Strategy = "Survivor";
    public string SeedMode;
    public int FixedSeed;
    public int SeedVersion = BotRunSeed.Version;
    public float SimulationSpeed;
    public int RequestedRuns, CompletedRuns, SectorCompleted, PlayerDead, StuckErrors, Aborted;
    public float CompletionRate, DeathRate;
    public List<BotRunResult> Results = new();
    public BotDistribution Duration = new(), DamageTaken = new();
    public float AverageKills, AverageDamageDealt, AverageMinimumHP, AverageXP, AverageLevelsGained, AverageMaxEnemiesAlive;
    public int ZeroDamageRuns, HighestMaxEnemiesAlive;
    public List<BotRewardFrequency> Rewards = new();
    public List<BotSuspiciousRun> SuspiciousRuns = new();

    public void Recalculate()
    {
        // Interrupted attempts remain in Results/CSV, but do not bias rates and averages.
        var complete = Results.Where(r => r.Result != BotRunOutcome.Aborted.ToString()).ToArray();
        CompletedRuns = complete.Length;
        Aborted = Results.Count - CompletedRuns;
        SectorCompleted = complete.Count(r => r.Result == BotRunOutcome.SectorCompleted.ToString());
        PlayerDead = complete.Count(r => r.Result == BotRunOutcome.PlayerDead.ToString());
        StuckErrors = complete.Count(r => r.Result == "Stuck" || r.Result == "Error");
        CompletionRate = CompletedRuns > 0 ? (float)SectorCompleted / CompletedRuns : 0f;
        DeathRate = CompletedRuns > 0 ? (float)PlayerDead / CompletedRuns : 0f;
        Duration = BotDistribution.From(complete.Select(r => r.Duration));
        DamageTaken = BotDistribution.From(complete.Select(r => r.DamageTaken));
        AverageKills = Mean(complete, r => r.Kills);
        AverageDamageDealt = Mean(complete, r => r.DamageDealt);
        AverageMinimumHP = Mean(complete, r => r.MinimumHPFraction);
        AverageXP = Mean(complete, r => r.XPCollected);
        AverageLevelsGained = Mean(complete, r => r.LevelsGained);
        AverageMaxEnemiesAlive = Mean(complete, r => r.MaxEnemiesAlive);
        HighestMaxEnemiesAlive = complete.Length > 0 ? complete.Max(r => r.MaxEnemiesAlive) : 0;
        ZeroDamageRuns = complete.Count(r => r.DamageTaken == 0f);
        Rewards = complete.SelectMany(r => r.RewardsTaken).GroupBy(r => r)
            .Select(g => new BotRewardFrequency { Reward = g.Key, Count = g.Count() })
            .OrderByDescending(r => r.Count).ThenBy(r => r.Reward, StringComparer.Ordinal).ToList();
        SuspiciousRuns.Clear();
        for (int i = 0; i < Results.Count; i++)
        {
            var r = Results[i];
            if (r.Result == "Aborted") continue;
            var reasons = new List<string>();
            if (r.Result == "PlayerDead" || r.Result == "Stuck" || r.Result == "Error") reasons.Add(r.Result);
            if (r.Duration > Math.Max(Duration.Average * 1.75f, Duration.Average + 30f)) reasons.Add("long duration");
            if (r.DamageTaken > Math.Max(DamageTaken.Average * 2f, DamageTaken.Average + 30f)) reasons.Add("high damage taken");
            if (r.MaxEnemiesAlive > AverageMaxEnemiesAlive * 1.5f + 5f) reasons.Add("high enemy count");
            if (r.MinimumHPFraction <= .1f) reasons.Add("HP <= 10%");
            if (reasons.Count > 0) SuspiciousRuns.Add(new BotSuspiciousRun { RunIndex = i + 1, Seed = r.Seed,
                Result = r.Result, Duration = r.Duration, DamageTaken = r.DamageTaken, Reason = string.Join(", ", reasons) });
        }
    }
    private static float Mean(BotRunResult[] results, Func<BotRunResult, float> value) => results.Length > 0 ? results.Average(value) : 0f;
    public string Report()
    {
        var text = new StringBuilder("=== BOT BATCH COMPLETE ===\n");
        text.AppendLine($"Batch: {BatchId}\nStatus: {Status} {StopReason}\nRuns: {CompletedRuns}/{RequestedRuns} · Aborted: {Aborted}");
        text.AppendLine($"Sector Completed: {SectorCompleted} ({CompletionRate:P1}) · Player Dead: {PlayerDead} ({DeathRate:P1}) · Stuck/Error: {StuckErrors}");
        text.AppendLine($"Duration (game seconds): avg {Duration.Average:0.0}, median {Duration.Median:0.0}, min/max {Duration.Minimum:0.0}/{Duration.Maximum:0.0}, P10/P50/P90 {Duration.P10:0.0}/{Duration.P50:0.0}/{Duration.P90:0.0}");
        text.AppendLine($"Combat averages: kills {AverageKills:0.0}, ORBITAL damage {AverageDamageDealt:0.0}, damage taken {DamageTaken.Average:0.0}");
        text.AppendLine($"Damage taken P10/P50/P90: {DamageTaken.P10:0.0}/{DamageTaken.P50:0.0}/{DamageTaken.P90:0.0}");
        text.AppendLine($"HP: average minimum {AverageMinimumHP:P1} · No damage: {ZeroDamageRuns}/{CompletedRuns}");
        text.AppendLine($"Progression averages: XP {AverageXP:0.0}, levels {AverageLevelsGained:0.0}");
        text.AppendLine($"Enemies: average max alive {AverageMaxEnemiesAlive:0.0}, highest {HighestMaxEnemiesAlive}");
        text.AppendLine("Rewards (grants):\n" + string.Join("\n", Rewards.Select(r => r.Reward + ": " + r.Count)));
        text.AppendLine("Suspicious Runs:\n" + string.Join("\n", SuspiciousRuns.Select(r => $"#{r.RunIndex} Seed {r.Seed}: {r.Result}, {r.Duration:0.0}s, damage {r.DamageTaken:0.0} · {r.Reason}")));
        return text.Append("==========================").ToString();
    }
    public string Csv()
    {
        var csv = new StringBuilder("RunIndex,Seed,Result,Duration,Kills,DamageDealt,DamageTaken,XP,Levels,MinHP,MaxEnemiesAlive\n");
        for (int i = 0; i < Results.Count; i++)
        {
            var r = Results[i];
            csv.AppendLine(string.Join(",", new object[] { i + 1, r.Seed, r.Result, r.Duration, r.Kills, r.DamageDealt,
                r.DamageTaken, r.XPCollected, r.LevelsGained, r.MinimumHPFraction, r.MaxEnemiesAlive }
                .Select(v => Convert.ToString(v, CultureInfo.InvariantCulture))));
        }
        return csv.ToString();
    }
}
#endif
