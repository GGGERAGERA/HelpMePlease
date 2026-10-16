#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections;
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
    [HideInInspector] public GameObject ShieldBearerPrefab, ShieldShooterPrefab;
    public GameObject BreakableShieldBearerPrefab, BreakableShieldShooterPrefab;
    [Min(1), Tooltip("Initial durability of every enemy shield spawned by this lab.")]
    public int ShieldBlockedHits = 4;
    public GameObject PursuerPrefab;
    public GameObject PistolProjectileVisual;
    [Header("Lab shooting profiles (not rewards)")]
    public OrbitalWeaponSettings Pistol = OrbitalWeaponSettings.Pistol();
    public OrbitalWeaponSettings Needle = new();
    public OrbitalWeaponSettings Shotgun = OrbitalWeaponSettings.Shotgun();
    public OrbitalWeaponSettings Spear = OrbitalWeaponSettings.Spear();
    public OrbitalWeaponSettings Sniper = OrbitalWeaponSettings.Sniper();
    public OrbitalWeaponSettings Saw = OrbitalWeaponSettings.Saw();
    [Range(3, 8)] public int BuildRingCount = 3;
    [Header("Scene-local upgrade choices (existing card view)")]
    public UpgradePanelView LabUpgradePanel;
    public float ShootingDamageMultiplier => Station != null ? Station.State.ShootingDamageMultiplier : 1f;
    public float ShootingRateMultiplier => Station != null ? Station.State.ShootingRateMultiplier : 1f;
    public float ContactDamageMultiplier => Station != null ? Station.State.ContactDamageMultiplier : 1f;
    public float ContactSizeMultiplier => Station != null ? Station.State.ContactSizeMultiplier : 1f;
    public enum LabUpgrade { ShootingDamage, ShootingRate, ContactDamage, ContactSize, OrbitSpeed, MaxHealth, MoveSpeed }
    private readonly Dictionary<UpgradeData, LabUpgrade> upgradeChoices = new();
    private readonly System.Random upgradeRandom = new(9107);
    private bool pausedBeforeUpgrade;
    [Min(.04f), Tooltip("Global scene budget: at most one frequent weapon sound per interval.")]
    public float WeaponSoundInterval = .10f;
    private float nextWeaponSound;
    [Range(0f, .035f)] public float ShootingShakeMagnitude = .018f;
    [Min(.15f)] public float ShootingShakeInterval = .25f;
    private float nextShootingShake;
    public enum ShootingBuild { Pistol, Contact, Needle, Shotgun, Spear, Mixed, Sniper, Saw }
    private ShootingBuild selectedBuild = ShootingBuild.Mixed;
    private OrbitalWeaponSettings installingProfile;
    private readonly Dictionary<int, OrbitalWeaponSettings> weaponProfiles = new();
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
    private int spawnIndex;
    private int tab;
    private int scenario = 6;
    public bool Paused { get; private set; }

    protected override void Awake()
    {
        base.Awake();
        ProductionOrbitalCombatAdapter.LabInterceptor = this;
    }

    protected override void Start()
    {
        base.Start();
        StartCoroutine(LoadInitialEncounter());
    }
    private IEnumerator LoadInitialEncounter()
    {
        while (!Ready) yield return null;
        SelectShootingBuild(selectedBuild);
        PopulateScenario();
    }

    public void StartScenario(int index)
    {
        scenario = Mathf.Clamp(index, 0, 7);
        SetPaused(false);
        ResetBuild(() =>
        {
            SelectShootingBuild(selectedBuild);
            PopulateScenario();
        });
    }
    public void StartMassEncounter(int perGroup) => StartScenario(perGroup >= 20 ? 7 : 6);
    private void PopulateScenario()
    {
        if (scenario >= 6)
        {
            int count = scenario == 7 ? 20 : 10;
            int[] shieldTypes = { -4, -6 };
            for (int i = 0; i < count * 2; i++)
            {
                float angle = i * Mathf.PI * 2f / (count * 2);
                float radius = 8f + i % 3 * 1.5f;
                AddScenarioEnemy(i % 2 == 0 ? shieldTypes[i / 2 % shieldTypes.Length] : (i / 2 % 2 == 0 ? -3 : -1),
                    new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius);
            }
            Notice = $"{count} shields + {count} ordinary enemies / {count * 2} total";
            return;
        }
        int type = scenario >= 4 ? -6 : -4;
        AddScenarioEnemy(type, new Vector2(6f, 0f));
        if (scenario == 1 || scenario == 2)
            for (int i = 0; i < 3; i++) AddScenarioEnemy(-3, new Vector2(-3f + i * 2f, 6f));
        if (scenario == 2) AddScenarioEnemy(-1, TestShooterPosition);
        Notice = "Shield: front 120° / open back. Move the station behind it.";
    }

    private void AddScenarioEnemy(int type, Vector2 position)
    { SpawnAt(type, position); layout.Add((type, position)); }

    public void RestartScenario()
    {
        var initialLayout = layout.ToArray();
        ResetBuild(() =>
        {
            SelectShootingBuild(selectedBuild);
            if (initialLayout.Length == 0) PopulateScenario();
            else foreach (var entry in initialLayout) AddScenarioEnemy(entry.type, entry.position);
            Notice = "Scenario restored / upgrades reset";
        });
    }

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
        // Build callbacks run as soon as Ready becomes true, before this controller's next Update.
        // Prepare the correct blue pool before any mounted gun can fire or a caller can inspect it.
        if (prototypeStation != Station) UpdateCenterPrototype();
        selectedBuild = build;
        Pistol.Kind = OrbitalModuleKind.Pistol; Shotgun.Kind = OrbitalModuleKind.Shotgun;
        Sniper.Kind = OrbitalModuleKind.Sniper; Saw.Kind = OrbitalModuleKind.Saw;
        var adapter = (ProductionOrbitalCombatAdapter)Station.Combat;
        adapter.LabModuleFactory = CreateLabModule;
        ClearModules();
        weaponProfiles.Clear();
        TargetRing = 0;
        adapter.DebugClearProjectiles(); Station.ClearLabFlashes();
        var profiles = build == ShootingBuild.Mixed ? new[] { Shotgun, Sniper, Saw }
            : new[] { build switch { ShootingBuild.Needle => Needle, ShootingBuild.Shotgun => Shotgun,
                ShootingBuild.Sniper => Sniper, ShootingBuild.Saw or ShootingBuild.Contact => Saw,
                ShootingBuild.Pistol => Pistol, _ => Spear } };
        while (Station.State.Rings.Count < Mathf.Min(BuildRingCount, OrbitalProgressionConfig.Default.MaxNormalRings) && Station.AddRing() != null) { }
        for (TargetRing = 0; TargetRing < Station.State.Rings.Count; TargetRing++)
        {
            var ring = Station.State.Rings[TargetRing];
            while (ring.MountCount < ring.MountCapacity && Station.AddMount(ring.StableRingId, out _)) { }
            for (int mount = 0; mount < ring.MountCount; mount++)
            {
                installingProfile = profiles[(TargetRing + mount) % profiles.Length];
                try { Give(installingProfile.Category == OrbitalUpgradeCategory.Contact
                    ? OrbitalRewardKind.Saw : OrbitalRewardKind.Pistol); }
                finally { installingProfile = null; }
            }
        }
        TargetRing = 0;
        foreach (var saw in Station.Modules.OfType<EnemyOrbitalLabSaw>()) saw.RefreshSize();
        Notice = build switch { ShootingBuild.Needle => "NEEDLE: accurate bursts / individual shield hits",
            ShootingBuild.Shotgun => "SHOTGUN: close fan / one durability per shield per volley",
            ShootingBuild.Spear => "SPEAR: charge / pierce bodies / stops at shields",
            ShootingBuild.Sniper => "SNIPER: high damage / shield bypass / one body by default",
            ShootingBuild.Saw or ShootingBuild.Contact => "SAW: contact disk / per-target interval / shields block",
            ShootingBuild.Mixed => "MIXED: artist shotgun + sniper + saw / all ordinary mounts filled", _ => build.ToString() };
    }

    private OrbitalModuleRuntime CreateLabModule(OrbitalStationRuntime station, OrbitalModuleState state)
    {
        if (installingProfile != null && !weaponProfiles.ContainsKey(state.StableModuleId))
            weaponProfiles[state.StableModuleId] = installingProfile;
        if (!weaponProfiles.TryGetValue(state.StableModuleId, out var profile)) return null;
        return profile.Category == OrbitalUpgradeCategory.Contact
            ? new EnemyOrbitalLabSaw(station, state.StableModuleId, profile, this)
            : new EnemyOrbitalLabWeapon(station, state.StableModuleId, profile, OnLabWeaponFired, this);
    }

    public void PlayWeaponSound(AudioCueId cue, Vector2 position)
    {
        if (Time.time < nextWeaponSound) return;
        nextWeaponSound = Time.time + Mathf.Max(.04f, WeaponSoundInterval);
        AudioService.Instance?.PlayAt(cue, position);
    }

    public bool IsUpgradeEligible(LabUpgrade upgrade) => Ready && (upgrade switch
    {
        LabUpgrade.ShootingDamage or LabUpgrade.ShootingRate => Station.Modules.OfType<EnemyOrbitalLabWeapon>()
            .Any(m => m.Settings.Category == OrbitalUpgradeCategory.Shooting),
        LabUpgrade.ContactDamage or LabUpgrade.ContactSize => Station.Modules.OfType<EnemyOrbitalLabSaw>().Any(),
        LabUpgrade.OrbitSpeed => Station.State.Rings.Any(r => r.SpeedUpgradeLevel < OrbitalProgressionConfig.Default.MaxSpeedUpgradeLevel),
        _ => Player != null && !Player.IsDead
    });
    public void OpenUpgradeChoices()
    {
        if (!CanEdit() || LabUpgradePanel == null || upgradeChoices.Count > 0) return;
        var pool = System.Enum.GetValues(typeof(LabUpgrade)).Cast<LabUpgrade>().Where(IsUpgradeEligible).ToList();
        var choices = new List<UpgradeData>();
        while (choices.Count < 3 && pool.Count > 0)
        {
            int index = upgradeRandom.Next(pool.Count);
            var kind = pool[index]; pool.RemoveAt(index);
            var card = ScriptableObject.CreateInstance<UpgradeData>();
            card.upgradeName = UpgradeLabel(kind);
            card.description = UpgradeEffect(kind);
            card.icon = OrbitalPresentationConfig.Active.CircleSprite;
            card.upgradeType = kind switch { LabUpgrade.MaxHealth => UpgradeType.MaxHealthFlat,
                LabUpgrade.MoveSpeed => UpgradeType.MoveSpeedPercent, LabUpgrade.ShootingRate => UpgradeType.FireRatePercent,
                LabUpgrade.ContactSize => UpgradeType.AttackSizePercent, _ => UpgradeType.WeaponDamagePercent };
            upgradeChoices.Add(card, kind); choices.Add(card);
        }
        if (choices.Count == 0) return;
        pausedBeforeUpgrade = Paused; SetPaused(true);
        LabUpgradePanel.Show("Лаборатория: улучшение", "Выберите один из трёх вариантов", choices, card =>
        {
            if (upgradeChoices.TryGetValue(card, out var kind)) ApplyLabUpgrade(kind);
            CloseUpgradeChoices();
        });
    }
    private static string UpgradeLabel(LabUpgrade upgrade) => upgrade switch
    {
        LabUpgrade.ShootingDamage => "Стрелковые · Урон", LabUpgrade.ShootingRate => "Стрелковые · Частота",
        LabUpgrade.ContactDamage => "Контактные · Урон", LabUpgrade.ContactSize => "Контактные · Размер",
        LabUpgrade.OrbitSpeed => "Орбиты · Вращение", LabUpgrade.MaxHealth => "Персонаж · Здоровье", _ => "Персонаж · Движение"
    };
    private static string UpgradeEffect(LabUpgrade upgrade) => upgrade switch
    {
        LabUpgrade.ShootingDamage => "Урон всех стрелковых оружий ×1.20",
        LabUpgrade.ShootingRate => "Частота всех стрелковых оружий ×1.15",
        LabUpgrade.ContactDamage => "Урон всех контактных оружий ×1.20",
        LabUpgrade.ContactSize => "Диаметр диска и зона контакта ×1.15. Радиусы орбит прежние.",
        LabUpgrade.OrbitSpeed => "Скорость всех орбит +25% (до 4 улучшений)",
        LabUpgrade.MaxHealth => "Максимальное и текущее здоровье +20", _ => "Скорость движения ×1.10"
    };
    public bool ApplyLabUpgrade(LabUpgrade upgrade)
    {
        if (!IsUpgradeEligible(upgrade)) return false;
        switch (upgrade)
        {
            case LabUpgrade.ShootingDamage: Station.State.ShootingDamageMultiplier *= 1.2f; break;
            case LabUpgrade.ShootingRate:
                Station.State.ShootingRateMultiplier *= 1.15f;
                foreach (var weapon in Station.Modules.OfType<EnemyOrbitalLabWeapon>()) weapon.RuntimeCooldown /= 1.15f;
                break;
            case LabUpgrade.ContactDamage: Station.State.ContactDamageMultiplier *= 1.2f; break;
            case LabUpgrade.ContactSize:
                Station.State.ContactSizeMultiplier *= 1.15f;
                foreach (var saw in Station.Modules.OfType<EnemyOrbitalLabSaw>()) saw.RefreshSize();
                break;
            case LabUpgrade.OrbitSpeed:
                foreach (var ring in Station.State.Rings.ToArray()) Station.UpgradeRingSpeed(ring.StableRingId);
                break;
            case LabUpgrade.MaxHealth: Player.AddMaxHealth(20f); break;
            case LabUpgrade.MoveSpeed:
                var movement = Player.GetComponent<CharacterMovement2D>();
                movement.SetRunUpgradeMoveSpeedMultiplier(movement.RunUpgradeMoveSpeedMultiplier * 1.1f);
                break;
        }
        Notice = UpgradeLabel(upgrade) + ": " + UpgradeEffect(upgrade);
        return true;
    }
    public void CloseUpgradeChoices()
    {
        if (LabUpgradePanel != null) LabUpgradePanel.Hide();
        foreach (var card in upgradeChoices.Keys) Destroy(card);
        bool hadChoices = upgradeChoices.Count > 0;
        upgradeChoices.Clear();
        if (hadChoices) SetPaused(pausedBeforeUpgrade);
    }

    private void OnLabWeaponFired(float strength)
    {
        if (Time.time < nextShootingShake || ShootingShakeMagnitude <= 0f) return;
        nextShootingShake = Time.time + Mathf.Max(.15f, ShootingShakeInterval);
        CameraShake.Instance?.Shake(.045f, ShootingShakeMagnitude * Mathf.Clamp01(strength));
    }

    public bool HandlesContact(OrbitalModuleRuntime module) => false;

    public void TickContacts(float deltaTime) { }
    public bool TryResolveDamage(EnemyHealth target, float damage, Vector2 point, out bool applied)
    { applied = false; return false; }
    public bool InterceptProjectile(EnemyHealth target, Vector2 from, Vector2 to, float damage, Vector2 incomingDirection)
    {
        return target.gameObject.scene == gameObject.scene && target.TryGetComponent<EnemyDirectionalShield>(out var shield) &&
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
        var prefab = type switch { -2 or -4 => BreakableShieldBearerPrefab, -3 => PursuerPrefab,
            -5 or -6 => BreakableShieldShooterPrefab, -1 => TestShooterPrefab, _ => EnemyPrefabs[type] };
        var instance = Instantiate(prefab, position, Quaternion.identity, transform);
        instance.name = prefab.name + " (Lab)";
        var health = instance.GetComponent<EnemyHealth>();
        if (type == -1) health.SetRuntimeMaxHealth(TestShooterHealth);
        if (instance.TryGetComponent<EnemyDirectionalShield>(out var shield))
        {
            shield.Kind = EnemyDirectionalShield.ShieldKind.Breakable;
            shield.MaximumBlockedHits = Mathf.Max(1, ShieldBlockedHits);
            shield.ResetForSpawn();
            shield.Bind(Player.transform);
        }
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
        nextWeaponSound = 0f;
        CameraShake.Instance?.StopAllShakes();
        if (Station != null)
        {
            foreach (var weapon in Station.Modules.OfType<EnemyOrbitalLabWeapon>()) weapon.ResetFiring();
            foreach (var saw in Station.Modules.OfType<EnemyOrbitalLabSaw>()) saw.ResetContact();
        }
        foreach (var enemy in enemies)
            if (enemy != null) { enemy.gameObject.SetActive(false); Destroy(enemy.gameObject); }
        enemies.Clear();
        ClearLooseObjects();
    }
    public void ClearEnemies() { DespawnEnemies(); spawnIndex = 0; Notice = "Enemies, projectiles and temporary effects cleared / Restart restores layout"; }
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
        CloseUpgradeChoices();
        if (Station != null) Station.State.ShootingDamageMultiplier = Station.State.ShootingRateMultiplier = Station.State.ContactDamageMultiplier = Station.State.ContactSizeMultiplier = 1f;
        SetFrozen(false); SetSpeed(1f); SetPlayerInvincible(false); SetEnemiesInvincible(false);
        weaponProfiles.Clear();
        SetPaused(false);
        centerControl?.Dispose(); centerControl = null; prototypeStation = null; centerMarker = null;
        ClearEnemies();
        layout.Clear();
    }
    public void ResetLab()
    {
        SetFrozen(false); SetSpeed(1f); SetPlayerInvincible(false); SetEnemiesInvincible(false);
        SelectedEnemy = 0; Pattern = SpawnPattern.AroundPlayer;
        selectedBuild = ShootingBuild.Mixed;
        StartMassEncounter(10);
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
            adapter.PrewarmLabProjectiles(Shotgun.ProjectileVisual, 96);
            adapter.PrewarmLabProjectiles(Spear.ProjectileVisual, 6);
            adapter.PrewarmLabProjectiles(Sniper.ProjectileVisual, 12);
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
    }

    private void ReleaseCenterInput() => centerControl?.ReleaseInput();

    private void OnDisable()
    {
        CloseUpgradeChoices();
        centerControl?.Dispose();
        if (centerMarker != null) centerMarker.enabled = false;
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
        Row(("10 со щитами + 10 без щитов", () => StartMassEncounter(10)), ("20 со щитами + 20 без щитов", () => StartMassEncounter(20)));
        Row(("MELEE / BREAKABLE", () => StartScenario(3)), ("SHOOTER / BREAKABLE", () => StartScenario(5)));
        Text($"All shields: breakable / {ShieldBlockedHits} blocked hits");
        Button("SHIELD + 3 PURSUERS", () => StartScenario(1));
        Button("SHIELD + PURSUERS + SHOOTER", () => StartScenario(2));
        Row(("RESTART SCENARIO", RestartScenario), ("CLEAR", ClearEnemies));
        Row(("ТОЛЬКО ДРОБОВИКИ", () => SelectShootingBuild(ShootingBuild.Shotgun)), ("ТОЛЬКО СНАЙПЕРКИ", () => SelectShootingBuild(ShootingBuild.Sniper)));
        Row(("ТОЛЬКО ПИЛЫ", () => SelectShootingBuild(ShootingBuild.Saw)), ("СМЕШАННАЯ", () => SelectShootingBuild(ShootingBuild.Mixed)));
        Button("ТОЛЬКО ПИСТОЛЕТЫ", () => SelectShootingBuild(ShootingBuild.Pistol));
        Text("Shotgun + saw: shields block | Sniper: passes shields, one body by default");
        Button("Получить улучшение", OpenUpgradeChoices);
        if (upgradeChoices.Count > 0) Button("ОТМЕНА ВЫБОРА", CloseUpgradeChoices);
        Text($"Стрелковые: урон ×{ShootingDamageMultiplier:0.00}, частота ×{ShootingRateMultiplier:0.00} | Контактные: урон ×{ContactDamageMultiplier:0.00}, размер ×{ContactSizeMultiplier:0.00}");
        Button(Paused ? "RESUME (P)" : "PAUSE (P)", () => SetPaused(!Paused));
        Text($"Alive {AliveEnemies} | Speed ×{SpeedMultiplier:0.0} | AI {(EnemyDebugAiFreeze.IsFrozen ? "frozen" : "running")}");
        Text($"Player invincible: {PlayerInvincible} | Enemies: {EnemiesInvincible}");
        Text($"Player HP {Player.CurrentHealth:0}/{Player.MaxHealth:0}");
        tab = GUILayout.Toolbar(tab, new[] { "Spawn", "AI / Health", "Build / Orbit" });
        if (tab == 2)
        {
            Heading("Filled builds: use the five loadouts above");
            DrawBuildInfo(true);
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
