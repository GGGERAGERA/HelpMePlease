#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using Subject42.Combat.OrbitalStation;
using UnityEngine;

public sealed class BotTelemetry : IDisposable
{
    private readonly BotRunResult result;
    private readonly PlayerHealth health;
    private readonly ExperienceManager experience;
    private readonly UpgradeManager rewards;
    private readonly IOrbitalCombatAdapter combat;
    private readonly HashSet<EnemyHealth> observed = new();
    private readonly int initialLevel;
    private float enemySeconds;
    public int ProgressVersion { get; private set; }

    public BotTelemetry(BotRunResult result, PlayerHealth health, ExperienceManager experience,
        UpgradeManager rewards, IOrbitalCombatAdapter combat)
    {
        this.result = result;
        this.health = health;
        this.experience = experience;
        this.rewards = rewards;
        this.combat = combat;
        initialLevel = experience.CurrentLevel;
        result.MinimumHP = health.CurrentHealth;
        health.DebugDamageApplied += DamageTaken;
        experience.DebugExperienceAdded += ExperienceAdded;
        rewards.DebugRewardCommitted += RewardTaken;
        combat.Hit += Hit;
        EnemyHealth.Spawned += Observe;
        EnemyHealth.SpawnConfigured += SpawnConfigured;
        EnemyHealth.Despawned += Forget;
        foreach (var enemy in EnemyHealth.ActiveInstances) Observe(enemy);
        Sample(0f);
    }

    private void Observe(EnemyHealth enemy)
    {
        if (observed.Add(enemy)) enemy.OnDied += Died;
    }
    private void SpawnConfigured(EnemyHealth enemy)
    {
        if (result.InitialEnemyTypes.Count < 16)
            result.InitialEnemyTypes.Add(enemy.GetComponent<EnemyIdentity>()?.EnemyId ?? enemy.name);
    }
    private void Forget(EnemyHealth enemy)
    {
        if (observed.Remove(enemy)) enemy.OnDied -= Died;
    }
    private void Died(EnemyHealth enemy) { result.Kills++; ProgressVersion++; }
    private void Hit(EnemyHealth enemy, float amount)
    {
        // Adapter publishes after TakeDamage. Negative remaining HP is the overkill portion.
        result.DamageDealt += Mathf.Max(0f, amount + Mathf.Min(0f, enemy.CurrentHealth));
    }
    private void DamageTaken(float amount) { result.DamageTaken += amount; SampleHealth(); }
    private void ExperienceAdded(int amount) { result.XPCollected += amount; ProgressVersion++; }
    private void RewardTaken(UpgradeData reward) { result.RewardsTaken.Add(reward.upgradeName); ProgressVersion++; }
    private void SampleHealth()
    {
        result.RemainingHP = Mathf.Max(0f, health.CurrentHealth);
        result.MaximumHP = health.MaxHealth;
        result.MinimumHP = Mathf.Min(result.MinimumHP, result.RemainingHP);
        result.MinimumHPFraction = Mathf.Min(result.MinimumHPFraction, result.RemainingHP / health.MaxHealth);
    }
    public void Sample(float seconds)
    {
        result.Duration += seconds;
        result.Level = experience.CurrentLevel;
        result.LevelsGained = result.Level - initialLevel;
        SampleHealth();
        int alive = 0;
        foreach (var enemy in EnemyHealth.ActiveInstances)
            if (enemy != null && !enemy.IsDead) alive++;
        result.EnemiesAlive = alive;
        result.MaxEnemiesAlive = Mathf.Max(result.MaxEnemiesAlive, alive);
        enemySeconds += alive * seconds;
        result.AverageEnemiesAlive = result.Duration > 0f ? enemySeconds / result.Duration : alive;
    }
    public void Dispose()
    {
        health.DebugDamageApplied -= DamageTaken;
        experience.DebugExperienceAdded -= ExperienceAdded;
        rewards.DebugRewardCommitted -= RewardTaken;
        combat.Hit -= Hit;
        EnemyHealth.Spawned -= Observe;
        EnemyHealth.SpawnConfigured -= SpawnConfigured;
        EnemyHealth.Despawned -= Forget;
        foreach (var enemy in observed) if (enemy != null) enemy.OnDied -= Died;
        observed.Clear();
    }
}
#endif
