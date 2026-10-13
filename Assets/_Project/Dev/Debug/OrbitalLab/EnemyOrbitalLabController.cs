#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections.Generic;
using System.Linq;
using Subject42.Combat.OrbitalStation;
using UnityEngine;

public sealed class EnemyOrbitalLabController : OrbitalLabSession, IOrbitalCombatLabInterceptor
{
    public const string ScenePath = "Assets/_Project/Dev/Labs/OrbitalLab/EnemyOrbitalLab.unity";
    public GameObject[] EnemyPrefabs;
    [Header("Manual orbit center prototype (lab only)")]
    [Min(0f)] public float MaximumCenterOffset = 6f;
    [Min(0f)] public float CenterMoveSpeed = 6f;
    [Min(0f)] public float CenterReturnSpeed = 8f;
    public bool ShowCenterMarker = true;
    [Range(.1f, 1f)] public float SteeringSpeedMultiplier = .7f;
    public GameObject ShieldBearerPrefab;
    public GameObject BreakableShieldBearerPrefab, ShieldShooterPrefab, BreakableShieldShooterPrefab;
    public GameObject PursuerPrefab;
    public LineRenderer CenterRange;
    public GameObject PistolProjectileVisual;
    [Header("Lab shooting profiles (not rewards)")]
    public EnemyOrbitalLabWeaponSettings Needle = new();
    public EnemyOrbitalLabWeaponSettings Shotgun = EnemyOrbitalLabWeaponSettings.Shotgun();
    public EnemyOrbitalLabWeaponSettings Spear = EnemyOrbitalLabWeaponSettings.Spear();
    [Range(0f, .035f)] public float ShootingShakeMagnitude = .018f;
    [Min(.15f)] public float ShootingShakeInterval = .25f;
    private float nextShootingShake;
    public enum ShootingBuild { Pistol, Contact, Needle, Shotgun, Spear, Mixed }
    private ShootingBuild selectedBuild;
    private EnemyOrbitalLabWeaponSettings installingProfile;
    private readonly Dictionary<int, EnemyOrbitalLabWeaponSettings> weaponProfiles = new();
    [Header("Distant test shooter")]
    public GameObject TestShooterPrefab;
    public Vector2 TestShooterPosition = new(7f, 0f);
    [Min(.01f)] public float TestShooterHealth = 12f;
    private Subject42.Combat.OrbitalStation.OrbitalStationRuntime prototypeStation;
    private OrbitalLabCenterControl centerControl;
    private Vector2 centerOffset => centerControl?.Offset ?? Vector2.zero;
    private SpriteRenderer centerMarker;
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
    private int scenario;
    public bool Paused { get; private set; }

    protected override void Awake()
    {
        base.Awake();
        ProductionOrbitalCombatAdapter.LabInterceptor = this;
    }

    public void StartScenario(int index)
    {
        scenario = Mathf.Clamp(index, 0, 5);
        SetPaused(false);
        ResetBuild(() =>
        {
            SelectShootingBuild(selectedBuild);
            int type = scenario switch { 3 => -4, 4 => -5, 5 => -6, _ => -2 };
            AddScenarioEnemy(type, new Vector2(scenario >= 4 ? 5f : 1.6f, 0f));
            if (scenario == 1 || scenario == 2)
                for (int i = 0; i < 3; i++) AddScenarioEnemy(-3, new Vector2(-3f + i * 2f, 4f));
            if (scenario == 2) AddScenarioEnemy(-1, TestShooterPosition);
            Notice = "Shield: front 120° / open back. Move the station behind it.";
        });
    }

    private void AddScenarioEnemy(int type, Vector2 position)
    { SpawnAt(type, position); layout.Add((type, position)); }

    public void RestartScenario() => StartScenario(scenario);

    public void SetPaused(bool value)
    {
        if (Paused == value) return;
        Station?.InputOwner?.PrepareForExternalPause();
        ReleaseCenterInput();
        Paused = value;
        Time.timeScale = value ? 0f : 1f;
    }

    public void SelectTestWeapon(bool contact)
        => SelectShootingBuild(contact ? ShootingBuild.Contact : ShootingBuild.Pistol);

    public void SelectShootingBuild(ShootingBuild build)
    {
        if (!CanEdit()) return;
        selectedBuild = build;
        var adapter = (ProductionOrbitalCombatAdapter)Station.Combat;
        adapter.LabModuleFactory = CreateLabModule;
        ClearModules();
        weaponProfiles.Clear();
        TargetRing = 0;
        adapter.DebugClearProjectiles(); Station.ClearLabFlashes();
        if (build == ShootingBuild.Contact || build == ShootingBuild.Pistol)
            Give(build == ShootingBuild.Contact ? OrbitalRewardKind.LaserSword : OrbitalRewardKind.Pistol);
        else
        {
            var profiles = build == ShootingBuild.Mixed ? new[] { Needle, Shotgun, Spear }
                : new[] { build == ShootingBuild.Needle ? Needle : build == ShootingBuild.Shotgun ? Shotgun : Spear };
            foreach (var profile in profiles)
            {
                if (Station.State.FreeBuiltMounts == 0)
                {
                    var ring = Station.State.Rings.FirstOrDefault(r => r.MountCount < r.MountCapacity);
                    if (ring != null) Station.AddMount(ring.StableRingId, out _);
                    else Station.AddRing();
                }
                installingProfile = profile;
                try { Give(OrbitalRewardKind.Pistol); }
                finally { installingProfile = null; }
            }
        }
        Notice = build switch { ShootingBuild.Needle => "NEEDLE: accurate bursts / individual shield hits",
            ShootingBuild.Shotgun => "SHOTGUN: close fan / one durability per shield per volley",
            ShootingBuild.Spear => "SPEAR: charge / pierce bodies / stops at shields",
            ShootingBuild.Mixed => "MIXED: needle + shotgun + spear on ordinary mounts", _ => build.ToString() };
    }

    private OrbitalModuleRuntime CreateLabModule(OrbitalStationRuntime station, OrbitalModuleState state)
    {
        if (state.ModuleType != OrbitalModuleKind.Pistol) return null;
        if (installingProfile != null) weaponProfiles[state.StableModuleId] = installingProfile;
        return weaponProfiles.TryGetValue(state.StableModuleId, out var profile)
            ? new EnemyOrbitalLabWeapon(station, state.StableModuleId, profile, OnLabWeaponFired) : null;
    }

    private void OnLabWeaponFired(float strength)
    {
        if (Time.time < nextShootingShake || ShootingShakeMagnitude <= 0f) return;
        nextShootingShake = Time.time + Mathf.Max(.15f, ShootingShakeInterval);
        CameraShake.Instance?.Shake(.045f, ShootingShakeMagnitude * Mathf.Clamp01(strength));
    }

    public bool HandlesContact(OrbitalModuleRuntime module)
    {
        if (!Ready || module.Kind != OrbitalModuleKind.LaserSword || module.CurrentMount == null ||
            module.CurrentMount.Transform.gameObject.scene != gameObject.scene) return false;
        foreach (var enemy in enemies)
        {
            if (enemy == null || !enemy.TryGetComponent<EnemyOrbitalLabShield>(out var shield) || !shield.BlockContact(module)) continue;
            module.RuntimeCooldown = OrbitalLaserSwordModule.ContactInterval;
            module.TriggerUpgradePresentation();
            return true;
        }
        return false;
    }

    public void TickContacts(float deltaTime) { }
    public bool TryResolveDamage(EnemyHealth target, float damage, Vector2 point, out bool applied)
    { applied = false; return false; }
    public bool InterceptProjectile(EnemyHealth target, Vector2 from, Vector2 to, float damage, Vector2 incomingDirection)
    {
        return target.gameObject.scene == gameObject.scene && target.TryGetComponent<EnemyOrbitalLabShield>(out var shield) &&
            shield.BlockProjectile(incomingDirection, to);
    }

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
        var prefab = type switch { -2 => ShieldBearerPrefab, -3 => PursuerPrefab, -4 => BreakableShieldBearerPrefab,
            -5 => ShieldShooterPrefab, -6 => BreakableShieldShooterPrefab, -1 => TestShooterPrefab, _ => EnemyPrefabs[type] };
        var instance = Instantiate(prefab, position, Quaternion.identity, transform);
        instance.name = prefab.name + " (Lab)";
        var health = instance.GetComponent<EnemyHealth>();
        if (type == -1) health.SetRuntimeMaxHealth(TestShooterHealth);
        if (instance.TryGetComponent<EnemyOrbitalLabShield>(out var shield)) shield.Bind(Player.transform);
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
        (Station?.Combat as ProductionOrbitalCombatAdapter)?.DebugClearProjectiles();
        Station?.ClearLabFlashes();
        nextShootingShake = 0f;
        CameraShake.Instance?.StopAllShakes();
        if (Station != null)
            foreach (var weapon in Station.Modules.OfType<EnemyOrbitalLabWeapon>()) weapon.ResetFiring();
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
    protected override void BeforeBuildReset()
    {
        weaponProfiles.Clear();
        SetPaused(false);
        centerControl?.Dispose(); centerControl = null; prototypeStation = null; centerMarker = null;
        ClearEnemies();
    }
    public void ResetLab()
    {
        SetFrozen(false); SetSpeed(1f); SetPlayerInvincible(false); SetEnemiesInvincible(false);
        SelectedEnemy = 0; Pattern = SpawnPattern.AroundPlayer;
        ResetBuild();
    }
    protected override void ResetSession() => RestartScenario();
    protected override void Update()
    {
        base.Update();
        if (Ready && Input.GetKeyDown(KeyCode.P)) SetPaused(!Paused);
        if (Ready)
        {
            Player.SetIncomingDamageMultiplier(PlayerInvincible ? 0f : 1f);
            UpdateCenterPrototype();
        }
        else ReleaseCenterInput();
        enemies.RemoveAll(e => e == null);
    }

    private void UpdateCenterPrototype()
    {
        if (prototypeStation != Station)
        {
            centerControl?.Dispose();
            prototypeStation = Station;
            centerControl = new OrbitalLabCenterControl(Station, CameraRig.ControlledCamera, SteeringSpeedMultiplier);
            ((ProductionOrbitalCombatAdapter)Station.Combat).ConfigureLabProjectileVisual(PistolProjectileVisual);
            ((ProductionOrbitalCombatAdapter)Station.Combat).LabStraightProjectiles = true;
            var adapter = (ProductionOrbitalCombatAdapter)Station.Combat;
            adapter.LabModuleFactory = CreateLabModule;
            adapter.PrewarmLabProjectiles(Needle.ProjectileVisual, 24);
            adapter.PrewarmLabProjectiles(Shotgun.ProjectileVisual, 28);
            adapter.PrewarmLabProjectiles(Spear.ProjectileVisual, 6);
            adapter.LabBodyImpact = (point, direction, piercing) =>
            {
                Vector2 side = new(-direction.y, direction.x);
                float size = piercing ? .32f : .16f;
                Station.FlashLink(point - side * size, point + side * size, piercing ? Spear.Color : Color.white, .09f);
                Station.FlashLink(point, point - direction * size, Color.white, .07f);
            };
            var marker = new GameObject("ORBITAL center debug marker");
            marker.transform.SetParent(Station.transform, false);
            marker.transform.localScale = Vector3.one * .22f;
            centerMarker = marker.AddComponent<SpriteRenderer>();
            var core = Station.GetComponent<Subject42.Combat.OrbitalStation.OrbitalStationView>().Core;
            centerMarker.sprite = core.sprite;
            centerMarker.sharedMaterial = core.sharedMaterial;
            centerMarker.color = Color.magenta;
            centerMarker.sortingLayerID = core.sortingLayerID;
            centerMarker.sortingOrder = core.sortingOrder + 100;
        }

        centerControl.Tick(MaximumCenterOffset, CenterMoveSpeed, CenterReturnSpeed);
        centerMarker.enabled = ShowCenterMarker;
        if (CenterRange != null)
        {
            CenterRange.enabled = ShowCenterMarker;
            for (int i = 0; i < CenterRange.positionCount; i++)
            {
                float angle = i * Mathf.PI * 2f / CenterRange.positionCount;
                CenterRange.SetPosition(i, Player.transform.position +
                    new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * MaximumCenterOffset);
            }
        }
    }

    private void ReleaseCenterInput() => centerControl?.ReleaseInput();

    private void OnDisable()
    {
        centerControl?.Dispose();
        if (centerMarker != null) centerMarker.enabled = false;
        if (CenterRange != null) CenterRange.enabled = false;
        if (ReferenceEquals(ProductionOrbitalCombatAdapter.LabInterceptor, this)) ProductionOrbitalCombatAdapter.LabInterceptor = null;
        SetPaused(false);
    }
    private void OnEnable() => ProductionOrbitalCombatAdapter.LabInterceptor = this;
    protected override void DrawPanel()
    {
        Button("RESET LAB", ResetLab);
        Text("WASD: player · hold RMB: station → cursor · Shift: slow time · P: pause");
        Text($"Release RMB: return to player | offset {centerOffset.magnitude:0.00}/{MaximumCenterOffset:0.0} | walk ×{SteeringSpeedMultiplier:0.0}");
        Heading("Shield encounters");
        Row(("MELEE / DURABLE", () => StartScenario(0)), ("MELEE / BREAKABLE", () => StartScenario(3)));
        Row(("SHOOTER / DURABLE", () => StartScenario(4)), ("SHOOTER / BREAKABLE", () => StartScenario(5)));
        Button("SHIELD + 3 PURSUERS", () => StartScenario(1));
        Button("SHIELD + PURSUERS + SHOOTER", () => StartScenario(2));
        Row(("RESTART SCENARIO", RestartScenario), ("CLEAR", ClearEnemies));
        Row(("CONTACT: SWORD", () => SelectTestWeapon(true)), ("SHOOTING: PISTOL", () => SelectTestWeapon(false)));
        Row(("NEEDLE", () => SelectShootingBuild(ShootingBuild.Needle)), ("SHOTGUN", () => SelectShootingBuild(ShootingBuild.Shotgun)));
        Row(("SPEAR", () => SelectShootingBuild(ShootingBuild.Spear)), ("THREE GUNS", () => SelectShootingBuild(ShootingBuild.Mixed)));
        Text("Needle: precise bursts | Shotgun: close fan + push | Spear: charge + line pierce");
        Button(Paused ? "RESUME (P)" : "PAUSE (P)", () => SetPaused(!Paused));
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
