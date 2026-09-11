#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Subject42.Combat.OrbitalStation;

public enum BotRunOutcome { SectorCompleted, PlayerDead, Aborted, Stuck, Error }

[Serializable]
public sealed class BotRunResult
{
    public string RunId = Guid.NewGuid().ToString("N");
    public string StartedUtc = DateTime.UtcNow.ToString("O");
    public int Seed;
    public int SeedVersion = BotRunSeed.Version;
    public string Strategy = "Survivor";
    public string Scene;
    public float SimulationSpeed = 1f;
    public string Timing = "Duration: scaled game seconds; inactivity/reward/startup watchdog: active real seconds";
    public string LayoutSignature;
    public List<string> InitialEnemyTypes = new();
    public string Character;
    public int Sector;
    public float Duration;
    public float WallDuration;
    public string Result;
    public string Reason;
    public int Kills;
    // Effective HP removed by ORBITAL, excluding overkill. Other damage sources lack attribution.
    public string DamageScope = "ORBITAL effective HP damage; kills include all enemy deaths";
    public float DamageDealt;
    public float DamageTaken;
    public int XPCollected;
    public int LevelsGained;
    public int Level;
    public List<string> RewardsTaken = new();
    public OrbitalRunState FinalOrbital;
    public float RemainingHP;
    public float MaximumHP;
    public float MinimumHP;
    public float MinimumHPFraction = 1f;
    public int EnemiesAlive;
    public int MaxEnemiesAlive;
    public float AverageEnemiesAlive;

    public string Report()
    {
        var text = new StringBuilder("=== BOT RUN COMPLETE ===\n");
        text.AppendLine($"RunId: {RunId}\nResult: {Result}\nReason: {Reason}");
        text.AppendLine($"Seed: {Seed} (v{SeedVersion}) · {SimulationSpeed}x · {Character} · {Scene}");
        text.AppendLine($"Duration: {TimeSpan.FromSeconds(Duration):mm\\:ss} (wall {WallDuration:0.0}s)");
        text.AppendLine($"Kills: {Kills}\nDamage dealt (ORBITAL): {DamageDealt:0.##}\nDamage taken: {DamageTaken:0.##}");
        text.AppendLine($"XP collected: {XPCollected}\nLevels gained: {LevelsGained}\nMinimum HP: {MinimumHPFraction:P0}\nRemaining HP: {RemainingHP:0.##}/{MaximumHP:0.##}");
        text.AppendLine("Rewards:\n" + string.Join("\n", RewardsTaken));
        if (FinalOrbital != null)
        {
            text.AppendLine($"Final ORBITAL: rings {FinalOrbital.Rings.Count}, mounts {FinalOrbital.Rings.Sum(r => r.MountCount)}, core level {FinalOrbital.CoreState.Level}");
            foreach (var group in FinalOrbital.Modules.GroupBy(m => m.ModuleType))
                text.AppendLine($"{group.Key} x{group.Count()}");
        }
        text.AppendLine($"Max enemies alive: {MaxEnemiesAlive}\nAverage enemies alive: {AverageEnemiesAlive:0.0}");
        return text.Append("=========================").ToString();
    }
}

public sealed class BotProgressWatchdog
{
    private float idle;
    public bool Tick(float seconds, bool active, bool progress)
    {
        if (progress) idle = 0f;
        else if (active) idle += seconds;
        return idle >= 30f;
    }
}
#endif
