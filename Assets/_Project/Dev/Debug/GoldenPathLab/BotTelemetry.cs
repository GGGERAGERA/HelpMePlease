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
    private readonly BotController controller;
    private readonly HashSet<EnemyHealth> observed = new();
    private readonly int initialLevel;
    private float enemySeconds;
    public int ProgressVersion { get; private set; }

    public BotTelemetry(BotRunResult result, PlayerHealth health, ExperienceManager experience,
        UpgradeManager rewards, IOrbitalCombatAdapter combat, BotController controller)
    {
        this.result = result;
        this.health = health;
        this.experience = experience;
        this.rewards = rewards;
        this.combat = combat;
        this.controller = controller;
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
    private void DamageTaken(float amount)
    {
        result.DamageTaken += amount;
        SampleHealth();
        if (result.GoldenPath == null) return;
        var trace = new System.Diagnostics.StackTrace();
        string source = trace.FrameCount > 2 ? trace.GetFrame(2).GetMethod()?.DeclaringType?.Name : "unknown";
        var callers = new List<string>();
        bool siteHazard = false;
        bool collisionDamage = false;
        for (int i = 2; i < Math.Min(trace.FrameCount, 10); i++)
        {
            var method = trace.GetFrame(i).GetMethod();
            callers.Add($"{method?.DeclaringType?.Name}.{method?.Name}");
            siteHazard |= method?.DeclaringType?.Name == "ProductionSiteHazardUtility";
            collisionDamage |= method?.DeclaringType == typeof(EnemyCollisionHandler);
        }
        var boss = RunFlowController.Instance?.FinalBoss;
        string beams = siteHazard ? CaptureBeamSnapshot() : "";
        string collisions = collisionDamage ? CaptureCollisionSnapshot() : "";
        result.GoldenPath.RecentDamage.Add($"t={result.Duration:F1}; sector={result.Sector}; source={source}; damage={amount}; hp={health.CurrentHealth}; position={health.transform.position}; bossHP={boss?.CurrentHealth}; botState={controller?.State}; direction={controller?.DebugLastDirection}; callers={string.Join(" > ", callers)}{beams}{collisions}");
        if (result.GoldenPath.RecentDamage.Count > 20) result.GoldenPath.RecentDamage.RemoveAt(0);
    }
    private string CaptureCollisionSnapshot()
    {
        var candidates = new List<EnemyCollisionHandler>();
        foreach (var candidate in UnityEngine.Object.FindObjectsByType<EnemyCollisionHandler>(FindObjectsSortMode.None))
            if (candidate != null && candidate.isActiveAndEnabled) candidates.Add(candidate);
        Vector3 playerPosition = health.transform.position;
        candidates.Sort((a, b) => ((Vector2)(a.transform.position - playerPosition)).sqrMagnitude
            .CompareTo(((Vector2)(b.transform.position - playerPosition)).sqrMagnitude));
        var snapshots = new List<string>();
        for (int i = 0; i < Math.Min(4, candidates.Count); i++)
        {
            var candidate = candidates[i];
            var enemy = candidate.GetComponentInParent<EnemyHealth>();
            snapshots.Add($"scene={candidate.gameObject.scene.name}; path={HierarchyPath(candidate.transform)}; position={candidate.transform.position}; damage={candidate.damage}; damageCooldown={candidate.damageCooldown}; isBoss={enemy?.IsBoss}; isDead={enemy?.IsDead}; distanceToPlayer={Vector2.Distance(candidate.transform.position, playerPosition):F3}; colliders=[{ColliderBounds(candidate)}]");
        }
        // Proximity at the damage callback does not identify the collider that caused damage.
        return $"; collisionPlayerPosition={playerPosition}; collisionPlayerColliders=[{ColliderBounds(health)}]; nearbyCollisionCandidates=[{string.Join(" | ", snapshots)}] (proximity does not prove damage source)";
    }
    private static string HierarchyPath(Transform target)
    {
        string path = target.name;
        for (var parent = target.parent; parent != null; parent = parent.parent)
            path = parent.name + "/" + path;
        return path;
    }
    private static string ColliderBounds(Component owner)
    {
        var snapshots = new List<string>();
        foreach (var collider in owner.GetComponentsInChildren<Collider2D>())
        {
            if (!collider.enabled || !collider.gameObject.activeInHierarchy) continue;
            var bounds = collider.bounds;
            snapshots.Add($"path={HierarchyPath(collider.transform)}; type={collider.GetType().Name}; isTrigger={collider.isTrigger}; center={bounds.center}; min={bounds.min}; max={bounds.max}");
        }
        return string.Join(" | ", snapshots);
    }
    private static string CaptureBeamSnapshot()
    {
        var snapshots = new List<string>();
        foreach (var behaviour in UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None))
        {
            if (behaviour == null || !behaviour.isActiveAndEnabled ||
                behaviour.GetType().Name != "ProductionBeamSiteHazard") continue;
            var type = behaviour.GetType();
            object Read(string name) => type.GetField(name,
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)?.GetValue(behaviour);
            string path = behaviour.name;
            for (var parent = behaviour.transform.parent; parent != null; parent = parent.parent)
                path = parent.name + "/" + path;
            var until = Read("stateUntil");
            string remaining = until is float deadline ? (deadline - Time.time).ToString("F3") : "missing";
            snapshots.Add($"scene={behaviour.gameObject.scene.name}; path={path}; beamStart={Read("beamStart")}; beamEnd={Read("beamEnd")}; state={Read("state")}; remaining={remaining}; patternIndex={Read("patternIndex")}");
            if (snapshots.Count == 4) break;
        }
        // FireBeam applies damage before assigning Firing: Telegraph here does not imply early damage.
        return "; beams=[" + string.Join(" | ", snapshots) + "] (callback precedes Firing assignment)";
    }
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
