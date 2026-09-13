#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public sealed class EnemyOrbitalLabController : OrbitalLabSession
{
    public const string ScenePath = "Assets/_Project/Scenes/Dev/Labs/EnemyOrbitalLab.unity";
    public GameObject[] EnemyPrefabs;
    public enum SpawnPattern { AroundPlayer, Line, Cluster, RandomArena }
    public SpawnPattern Pattern;
    public int SelectedEnemy;
    public float SpeedMultiplier { get; private set; } = 1f;
    public bool PlayerInvincible { get; private set; }
    public bool EnemiesInvincible { get; private set; }
    public int AliveEnemies => enemies.Count(e => e != null && !e.IsDead);
    private readonly List<EnemyHealth> enemies = new();
    private readonly List<(int type, Vector2 position)> layout = new();
    private static readonly string[] Presets = { "STARTER", "PISTOL", "LASER SWORD", "IMPULSE", "ARC", "MIXED", "MANY RINGS" };
    private int spawnIndex;
    private int tab;

    public void Spawn(int count)
    {
        if (!Ready || EnemyPrefabs == null || EnemyPrefabs.Length == 0) return;
        SelectedEnemy = Mathf.Clamp(SelectedEnemy, 0, EnemyPrefabs.Length - 1);
        for (int i = 0; i < count; i++)
        {
            Vector2 center = Player.transform.position;
            float angle = i * Mathf.PI * 2 / count;
            // Local PRNG: repeatable layouts without consuming production reward/combat randomness.
            var random = new System.Random(7301 + spawnIndex++);
            Vector2 position = Pattern switch
            {
                SpawnPattern.Line => center + new Vector2((i - (count - 1) * .5f) * 1.3f, 5),
                SpawnPattern.Cluster => center + new Vector2(5 + i % 5 * 1.1f, (i / 5 - 2) * 1.1f),
                SpawnPattern.RandomArena => new Vector2((float)random.NextDouble() * 48 - 24, (float)random.NextDouble() * 48 - 24),
                _ => center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * 6
            };
            position = new Vector2(Mathf.Clamp(position.x, -28, 28), Mathf.Clamp(position.y, -28, 28));
            SpawnAt(SelectedEnemy, position);
            layout.Add((SelectedEnemy, position));
        }
        Notice = $"Spawned {count} × {EnemyPrefabs[SelectedEnemy].name}";
    }

    private void SpawnAt(int type, Vector2 position)
    {
        position = new Vector2(Mathf.Clamp(position.x, -28, 28), Mathf.Clamp(position.y, -28, 28));
        var instance = Instantiate(EnemyPrefabs[type], position, Quaternion.identity, transform);
        instance.name = EnemyPrefabs[type].name + " (Lab)";
        var health = instance.GetComponent<EnemyHealth>();
        // Existing debug marker preserves real AI and combat, suppresses loot/progression on death.
        var debug = instance.AddComponent<CombatFeelTestDummy>();
        debug.Initialize(null);
        debug.Invulnerable = EnemiesInvincible;
        foreach (var movement in instance.GetComponents<EnemyMovement>()) movement.SetSpeedMultiplier(SpeedMultiplier);
        health.NotifySpawnConfigured();
        enemies.Add(health);
    }

    public void SpawnAll()
    {
        if (!Ready || EnemyPrefabs == null || EnemyPrefabs.Length == 0) return;
        int selected = SelectedEnemy;
        var pattern = Pattern;
        Pattern = SpawnPattern.AroundPlayer;
        for (int i = 0; i < EnemyPrefabs.Length; i++)
        {
            float angle = i * Mathf.PI * 2 / EnemyPrefabs.Length;
            Vector2 position = (Vector2)Player.transform.position + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * 6;
            SpawnAt(i, position); layout.Add((i, position));
        }
        SelectedEnemy = selected; Pattern = pattern;
        Notice = $"Spawned all {EnemyPrefabs.Length} production types";
    }

    private void DespawnEnemies()
    {
        foreach (var enemy in enemies)
            if (enemy != null) { enemy.gameObject.SetActive(false); Destroy(enemy.gameObject); }
        enemies.Clear();
        ClearLooseObjects();
    }
    public void ClearEnemies() { DespawnEnemies(); layout.Clear(); spawnIndex = 0; Notice = "Enemies and projectiles cleared"; }
    public void ResetEnemies()
    {
        DespawnEnemies();
        foreach (var entry in layout) SpawnAt(entry.type, entry.position);
        Notice = "Spawned layout restored at full health";
    }
    public void SetFrozen(bool frozen) { EnemyDebugAiFreeze.SetFrozen(frozen); Notice = frozen ? "AI frozen" : "AI resumed"; }
    public void SetSpeed(float multiplier)
    {
        SpeedMultiplier = multiplier;
        foreach (var enemy in enemies.Where(e => e != null))
            foreach (var movement in enemy.GetComponents<EnemyMovement>()) movement.SetSpeedMultiplier(multiplier);
    }
    public void SetPlayerInvincible(bool value)
    { PlayerInvincible = value; if (Player != null) Player.SetIncomingDamageMultiplier(value ? 0f : 1f); }
    public void SetEnemiesInvincible(bool value)
    {
        EnemiesInvincible = value;
        foreach (var enemy in enemies.Where(e => e != null)) enemy.GetComponent<CombatFeelTestDummy>().Invulnerable = value;
    }
    public void HealEnemies()
    {
        foreach (var enemy in enemies.Where(e => e != null && !e.IsDead)) enemy.SetRuntimeMaxHealth(enemy.MaxHealth);
    }
    public void KillEnemies()
    {
        foreach (var enemy in enemies.Where(e => e != null && !e.IsDead).ToArray())
        {
            enemy.GetComponent<CombatFeelTestDummy>().Invulnerable = false;
            enemy.TakeDamage(enemy.CurrentHealth + 1, enemy.transform.position);
        }
        Notice = "Killed enemies (debug reward suppression)";
    }
    protected override void BeforeBuildReset() => ClearEnemies();
    public void ResetLab()
    {
        SetFrozen(false); SetSpeed(1f); SetPlayerInvincible(false); SetEnemiesInvincible(false);
        SelectedEnemy = 0; Pattern = SpawnPattern.AroundPlayer;
        ResetBuild();
    }
    protected override void ResetSession() => ResetLab();
    protected override void Update()
    {
        base.Update();
        if (Ready) Player.SetIncomingDamageMultiplier(PlayerInvincible ? 0f : 1f);
        enemies.RemoveAll(e => e == null);
    }
    protected override void DrawPanel()
    {
        Button("RESET LAB", ResetLab);
        Text($"Alive {AliveEnemies} | Speed ×{SpeedMultiplier:0.0} | AI {(EnemyDebugAiFreeze.IsFrozen ? "frozen" : "running")}");
        Text($"Player invincible: {PlayerInvincible} | Enemies: {EnemiesInvincible}");
        Text($"Player HP {Player.CurrentHealth:0}/{Player.MaxHealth:0}");
        tab = GUILayout.Toolbar(tab, new[] { "Spawn", "AI / Health", "Build / Orbit" });
        if (tab == 2)
        {
            Heading("ORBITAL presets (clear enemies)");
            for (int i = 0; i < Presets.Length; i += 2)
            {
                string first = Presets[i];
                if (i + 1 < Presets.Length) { string second = Presets[i + 1]; Row((first, () => ApplyPreset(first)), (second, () => ApplyPreset(second))); }
                else Button(first, () => ApplyPreset(first));
            }
            DrawOrbitControls(); return;
        }
        if (tab == 0)
        {
        Heading("Production enemies");
        if (EnemyPrefabs == null || EnemyPrefabs.Length == 0) { Text("No production enemies found — refresh scene"); return; }
        for (int i = 0; i < EnemyPrefabs.Length; i++)
        {
            int index = i;
            Button((SelectedEnemy == i ? "● " : "") + EnemyPrefabs[i].name, () => SelectedEnemy = index);
        }
        Text("Selected: " + EnemyPrefabs[SelectedEnemy].name);
        Pattern = (SpawnPattern)GUILayout.SelectionGrid((int)Pattern,
            new[] { "Around Player", "Line", "Cluster", "Random Arena" }, 2);
        Row(("SPAWN 1", () => Spawn(1)), ("SPAWN 5", () => Spawn(5)));
        Row(("SPAWN 10", () => Spawn(10)), ("SPAWN 25", () => Spawn(25)));
        Button("SPAWN ALL TYPES", SpawnAll);
        Row(("CLEAR ENEMIES", ClearEnemies), ("RESET ENEMIES", ResetEnemies));
        return;
        }
        Heading("AI / survivability");
        Row(("Freeze AI", () => SetFrozen(true)), ("Resume AI", () => SetFrozen(false)));
        Row(("Speed ×0.5", () => SetSpeed(.5f)), ("Speed ×1", () => SetSpeed(1)), ("Speed ×2", () => SetSpeed(2)));
        Button("Invincible Player " + (PlayerInvincible ? "ON" : "OFF"), () => SetPlayerInvincible(!PlayerInvincible));
        Button("Invincible Enemies " + (EnemiesInvincible ? "ON" : "OFF"), () => SetEnemiesInvincible(!EnemiesInvincible));
        Row(("Heal All Enemies", HealEnemies), ("Kill All Enemies", KillEnemies));
    }
    protected override void OnDestroy() { EnemyDebugAiFreeze.SetFrozen(false); base.OnDestroy(); }
}
#endif
