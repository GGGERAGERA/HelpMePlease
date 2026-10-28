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
    [Range(1, 3)] public int BuildRingCount = 3;
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
    // Null means use the selected build; an empty dictionary is an intentionally empty lab loadout.
    private Dictionary<(int ring, int mount), (OrbitalWeaponSettings profile, OrbitalModuleKind kind)> sandboxWeapons;
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

    private int scenario = 6;
    public bool Paused { get; private set; }
    public bool FixedDirection { get; private set; } = true;
    public OrbitalCombatV2LabControl CombatV2 { get; private set; }
    public OrbitalCombatV2LabPlacement Placement { get; private set; }
    public OrbitalCombatV2WeaponModules WeaponModules { get; private set; }
    [Header("Lab-only individual weapon modules")]
    [Range(1f, 5f)] public float ModuleRapidMultiplier = 1.5f;
    [Range(0, 10)] public int ModulePierceBonus = 2;
    [Range(1f, 30f)] public float ModuleMultishotAngle = 6f;
    private bool restoringLabBuild;
    // Explicit sandbox choices only; never capture reward upgrades or transient rotation state.
    private readonly Dictionary<int, (float? speed, int? direction, float? radius)> sandboxRingSettings = new();
    internal bool IsRestoringLabBuild => restoringLabBuild;

    public void SetFixedDirection(bool value) => FixedDirection = value;

    private EnemyOrbitalLabSandboxView sandboxView;
    public bool SandboxReady { get; private set; }
    public EnemyOrbitalLabTelemetry Telemetry { get; private set; }
    private string tacticalPreset, tacticalConfiguration;
    public string TacticalPreset => tacticalPreset != null && tacticalConfiguration == Telemetry.Configuration() ? tacticalPreset : null;
    public void BeginCombatTelemetry(string scenario) => Telemetry.Begin(scenario);
    public string CombatTelemetryJson() => JsonUtility.ToJson(Telemetry.Snapshot(), true);
    public void StopCombatTelemetry() => Telemetry.Stop();
    public void ApplyTacticalPreset(string name)
    {
        if (name != "EVEN" && name != "GROUPED" && name != "MIXED" || !PrepareSandboxEdit()) return;
        Telemetry.Stop();
        bool fourPistols = Station.Modules.Count == 4 && Station.Modules.All(m => m is EnemyOrbitalLabWeapon gun &&
            ReferenceEquals(gun.SourceProfile, Pistol) && m.CurrentMount.Ring == Station.Rings[0]);
        if (!fourPistols || name == "MIXED") SelectWeaponModulePreset("A");
        if (name == "MIXED")
        {
            var ring = Station.Rings[0];
            var profiles = new[] { Shotgun, Sniper };
            Shotgun.Kind = OrbitalModuleKind.Shotgun; Sniper.Kind = OrbitalModuleKind.Sniper;
            for (int i = 0; i < profiles.Length; i++)
            {
                int oldId = ring.Mounts[i + 1].Module.StableModuleId;
                Station.RemoveModule(oldId); weaponProfiles.Remove(oldId);
                installingProfile = profiles[i];
                try { Station.InstallModule(installingProfile.Kind, ring.RingId, i + 1, out _); }
                finally { installingProfile = null; }
            }
            WeaponModules.SetModule(ring.Mounts[0].Module.StableModuleId, 0, "RAPID");
            WeaponModules.SetModule(ring.Mounts[1].Module.StableModuleId, 0, "MULTISHOT");
            WeaponModules.SetModule(ring.Mounts[2].Module.StableModuleId, 0, "PIERCE");
            WeaponModules.ClearPresetLabel();
        }
        SandboxRingIndex = 0;
        Placement.ApplyRingLayout(Station.Rings[0], name == "GROUPED" ? "GROUPED" : "EVEN");
        tacticalPreset = name; tacticalConfiguration = Telemetry.Configuration();
        SelectSandboxWeapon(Station.Rings[0].Mounts[0].Module.StableModuleId);
        SandboxMessage = name == "MIXED" ? "Пистолеты, дробовик и снайперка с разными модулями." : "Четыре пистолета. Углы изменены, часы и модули сохранены.";
        RememberSandboxWeapons();
    }
    public int SandboxSelectedWeaponId { get; private set; }
    public int SandboxTab { get; private set; }
    public int SandboxRingIndex { get; private set; }
    public string SandboxMessage { get; private set; } = "Выберите сборку, расставьте оружие и начните бой.";
    public bool SandboxSpawning { get; private set; }
    public const int SandboxEnemyLimit = 64;
    private int sandboxEncounter, sandboxSequence;
    private float sandboxSpawnTimer;
    public OrbitalRingRuntime SandboxRing => Ready && Station.Rings.Count > 0
        ? Station.Rings[Mathf.Clamp(SandboxRingIndex, 0, Station.Rings.Count - 1)] : null;
    public OrbitalModuleRuntime SandboxWeapon => Station?.Modules.FirstOrDefault(m => m.StableModuleId == SandboxSelectedWeaponId);
    public void SetSandboxTab(int value) => SandboxTab = Mathf.Clamp(value, 0, 3);
    public void SelectSandboxRing(int index) { Placement.Cancel(); SandboxRingIndex = Mathf.Clamp(index, 0, Station.Rings.Count - 1); }
    private bool PrepareSandboxEdit()
    {
        if (!Ready) return false;
        SetPaused(true);
        return CanEdit();
    }
    public void SelectSandboxWeapon(int id)
    {
        var weapon = Station?.Modules.FirstOrDefault(m => m.StableModuleId == id);
        if (weapon == null) return;
        Placement.Cancel(); SandboxSelectedWeaponId = id;
        SandboxRingIndex = weapon.CurrentMount.Ring.State.Order;
        WeaponModules.Select(id);
        foreach (var mounted in Station.Modules) mounted.SetHighlighted(mounted == weapon);
    }
    public void MoveSandboxWeapon()
    {
        if (PrepareSandboxEdit() && Placement.TrySelect(SandboxSelectedWeaponId))
            SandboxMessage = "Наведите на кольцо → ЛКМ: поставить · Esc: отмена";
    }
    public bool RemoveSandboxWeapon()
    {
        if (!PrepareSandboxEdit() || SandboxWeapon == null) return false;
        Placement.Cancel();
        bool removed = Station.RemoveModule(SandboxSelectedWeaponId);
        if (!removed) return false;
        weaponProfiles.Remove(SandboxSelectedWeaponId);
        RememberSandboxWeapons();
        SandboxSelectedWeaponId = 0; WeaponModules.ClearPresetLabel();
        SandboxMessage = "Оружие убрано. Выберите свободное кольцо и новое оружие.";
        return removed;
    }
    public bool InstallSandboxWeapon(int choice)
    {
        if (!PrepareSandboxEdit() || choice < 0 || choice > 3 || SandboxRing == null) return false;
        Placement.Cancel(); Placement.Synchronize();
        var ring = SandboxRing;
        var mount = ring.Mounts.FirstOrDefault(m => !m.Occupied);
        if (mount == null)
        {
            if (ring.MountCount >= 4) { SandboxMessage = "На кольце максимум четыре пушки. Добавьте кольцо."; return false; }
            if (ring.MountCount == ring.MountCapacity && !Station.UpgradeRingCapacity(ring.RingId)) return false;
            if (!Station.AddMount(ring.RingId, out _)) return false;
            mount = ring.Mounts.First(m => !m.Occupied);
        }
        installingProfile = new[] { Pistol, Shotgun, Sniper, Saw }[choice];
        installingProfile.Kind = new[] { OrbitalModuleKind.Pistol, OrbitalModuleKind.Shotgun, OrbitalModuleKind.Sniper, OrbitalModuleKind.Saw }[choice];
        try
        {
            if (!Station.InstallModule(installingProfile.Kind, ring.RingId, mount.MountIndex, out _)) return false;
            Placement.Synchronize();
            // Existing empty mounts may be close to a custom arrangement. Search a free angle
            // before accepting the installation, without moving any other gun or clock.
            var module = ring.Mounts[mount.MountIndex].Module;
            if (!Placement.TrySelect(module.StableModuleId)) { Station.RemoveModule(module.StableModuleId); return false; }
            float initial = mount.LocalPhase;
            bool placed = false;
            for (int step = 0; step < 720 && !placed; step++)
            {
                float angle = (ring.Phase + initial + step * .5f) * Mathf.Deg2Rad;
                Placement.PreviewWorld((Vector2)Station.transform.position + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * ring.Radius);
                if (Placement.PreviewValid) placed = Placement.Confirm();
            }
            if (!placed) { Placement.Cancel(); Station.RemoveModule(module.StableModuleId); SandboxMessage = "Нет места с безопасным зазором."; return false; }
            WeaponModules.ClearPresetLabel(); SelectSandboxWeapon(module.StableModuleId);
            RememberSandboxWeapons();
            SandboxMessage = "Установлено. «Переместить» — выбрать другой угол."; return true;
        }
        finally { installingProfile = null; }
    }
    public bool ChangeSandboxRings(bool add)
    {
        if (!PrepareSandboxEdit()) return false;
        Placement.Cancel();
        if (add)
        {
            if (Station.Rings.Count >= 3 || Station.AddRing() == null) return false;
            SandboxRingIndex = Station.Rings.Count - 1;
            var added = SandboxRing;
            // Reuse a vacant ordinary radius: inner deletion must not duplicate the outer ring
            // or push subsequent additions farther and farther outside the camera.
            added.State.Radius = Enumerable.Range(0, 3).Select(i => 1.25f + i * .72f)
                .First(radius => Station.Rings.All(r => r == added || Mathf.Abs(r.State.Radius - radius) > .001f));
            FinishSandboxRingPresentation(added); Placement.Synchronize();
        }
        else
        {
            var previousOrders = Station.Rings.ToDictionary(r => r, r => r.State.Order);
            if (Station.Rings.Count <= 1 || !Station.RemoveRing(SandboxRing.RingId, out _)) return false;
            var survivingSettings = Station.Rings
                .Where(r => sandboxRingSettings.ContainsKey(previousOrders[r]))
                .ToDictionary(r => r.State.Order, r => sandboxRingSettings[previousOrders[r]]);
            sandboxRingSettings.Clear();
            foreach (var entry in survivingSettings) sandboxRingSettings.Add(entry.Key, entry.Value);
            Placement.ReindexAfterRingRemoval(previousOrders);
            SandboxRingIndex = Mathf.Clamp(SandboxRingIndex, 0, Station.Rings.Count - 1);
            if (SandboxWeapon == null) SandboxSelectedWeaponId = 0;
        }
        // Remember only the edited base geometry, not animated/reward-expanded runtime radii.
        foreach (var ring in Station.Rings)
        {
            sandboxRingSettings.TryGetValue(ring.State.Order, out var settings);
            sandboxRingSettings[ring.State.Order] = (settings.speed, settings.direction, ring.State.Radius);
        }
        RememberSandboxWeapons();
        BuildRingCount = Station.Rings.Count; WeaponModules.ClearPresetLabel(); return true;
    }
    private void RememberSandboxWeapons()
    {
        sandboxWeapons = Station.Modules.ToDictionary(
            m => (m.CurrentMount.Ring.State.Order, m.CurrentMount.MountIndex),
            m => (weaponProfiles[m.StableModuleId], m.Kind));
    }
    private static void FinishSandboxRingPresentation(OrbitalRingRuntime ring)
    {
        // Only this ring's spawn presentation advances. No weapon/projectile/enemy tick;
        // restore the exact angular phase before the paused editor can see the result.
        float phase = ring.Phase;
        ring.Tick(.36f); ring.State.CurrentPhase = phase; ring.Tick(0f);
    }
    public void SetSandboxRingSpeed(float speed)
    {
        if (!PrepareSandboxEdit() || SandboxRing == null) return;
        SandboxRing.State.BaseRotationSpeed = Mathf.Clamp(speed, 10f, 180f) /
            Mathf.Pow(1f + OrbitalProgressionConfig.Default.SpeedIncrement, SandboxRing.State.SpeedUpgradeLevel);
        sandboxRingSettings.TryGetValue(SandboxRing.State.Order, out var settings);
        sandboxRingSettings[SandboxRing.State.Order] = (SandboxRing.State.BaseRotationSpeed, settings.direction, settings.radius);
    }
    public void SetSandboxRingDirection(int direction)
    {
        if (!PrepareSandboxEdit() || SandboxRing == null) return;
        SandboxRing.State.Direction = direction < 0 ? -1 : 1;
        sandboxRingSettings.TryGetValue(SandboxRing.State.Order, out var settings);
        sandboxRingSettings[SandboxRing.State.Order] = (settings.speed, SandboxRing.State.Direction, settings.radius);
    }
    public void ApplySandboxLayout(string name)
    {
        if (!PrepareSandboxEdit()) return;
        SandboxMessage = Placement.ApplyRingLayout(SandboxRing, name)
            ? "Расположение изменено. Состав и модули сохранены." : "Не хватает места или на кольце нет оружия.";
    }
    public void ApplySandboxBuild(string name)
    {
        SelectWeaponModulePreset(name);
        if (WeaponModules.SelectedId != 0) SelectSandboxWeapon(WeaponModules.SelectedId);
        SandboxMessage = "Сборка готова. Раздел «Враги» — начать бой.";
    }
    public void SetSandboxModule(int slot, string name)
    {
        if (!PrepareSandboxEdit()) return;
        WeaponModules.Select(SandboxSelectedWeaponId);
        SandboxMessage = WeaponModules.SetModule(SandboxSelectedWeaponId, slot, name)
            ? "Модификация применена к выбранной пушке." : "Этот модуль уже установлен или слот недоступен.";
    }
    public void UnlockSandboxSlot()
    {
        if (PrepareSandboxEdit() && WeaponModules.UnlockSecondSlot(SandboxSelectedWeaponId))
            SandboxMessage = "Второй слот открыт.";
    }
    public void StartSandboxEncounter(int kind)
    {
        if (!Ready || kind < 1 || kind > 4) return;
        Telemetry.Stop();
        sandboxEncounter = kind; sandboxSequence = 0; sandboxSpawnTimer = 0f;
        DespawnEnemies(false); layout.Clear();
        int count = kind == 4 ? 48 : 12;
        for (int i = 0; i < count; i++) SpawnSandboxEnemy();
        SandboxSpawning = true;
        BeginCombatTelemetry(kind switch { 1 => "Толпа", 2 => "Смешанная группа", 3 => "Щитоносцы", _ => "Плотный бой" });
        SandboxMessage = "Бой подготовлен. P — продолжить, Shift — замедлить орбиты.";
    }
    private void SpawnSandboxEnemy()
    {
        if (AliveEnemies >= SandboxEnemyLimit) return;
        int index = sandboxSequence++;
        int shooter = System.Array.FindIndex(EnemyPrefabs, p => p != null && p.GetComponent<EnemyShooterMovement>() != null);
        if (shooter < 0) shooter = 0;
        int type = sandboxEncounter switch { 1 => -3, 2 => index % 4 == 0 ? shooter : -3, 3 => -4,
            _ => index % 5 switch { 0 or 1 => -3, 2 => shooter, 3 => -4, _ => -6 } };
        float angle = index * 137.508f * Mathf.Deg2Rad;
        float radius = 5.5f + index % 4 * .6f;
        SpawnAt(type, (Vector2)Player.transform.position + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius);
    }
    public void SetSandboxSpawning(bool active) => SandboxSpawning = active && sandboxEncounter != 0;
    public void ClearSandboxArena()
    { SandboxSpawning = false; DespawnEnemies(false); Telemetry.Stop(); layout.Clear(); SandboxMessage = "Арена очищена. Сборка сохранена."; }
    public void RepeatSandboxEncounter()
    { if (sandboxEncounter != 0) StartSandboxEncounter(sandboxEncounter); else SandboxMessage = "Сначала выберите врагов."; }
    private void TickSandbox(bool pointerBlocked)
    {
        if (!Paused) Telemetry.Tick(Time.deltaTime);
        if (!Paused && SandboxSpawning)
        {
            sandboxSpawnTimer -= Time.deltaTime;
            if (sandboxSpawnTimer <= 0f)
            {
                sandboxSpawnTimer = 1f;
                int count = Mathf.Min(4, (sandboxEncounter == 4 ? 48 : 12) - AliveEnemies);
                for (int i = 0; i < count; i++) SpawnSandboxEnemy();
            }
        }
        if (Paused || pointerBlocked || !Application.isFocused || Station.InputOwner.IsGameplayPointerBlocked || !Input.GetMouseButtonDown(0)) return;
        var camera = CameraRig.ControlledCamera;
        Vector3 pointer = Input.mousePosition; pointer.z = -camera.transform.position.z;
        Vector2 world = camera.ScreenToWorldPoint(pointer);
        var hit = Station.Modules.Where(m => m.HitTest(world, .08f)).OrderBy(m => Vector2.SqrMagnitude(m.WorldPosition - world)).FirstOrDefault();
        if (hit != null) { SelectSandboxWeapon(hit.StableModuleId); SetSandboxTab(2); }
    }

    protected override void Awake()
    {
        base.Awake();
        Placement = new OrbitalCombatV2LabPlacement(this);
        Telemetry = new EnemyOrbitalLabTelemetry(this);
        sandboxView = new EnemyOrbitalLabSandboxView(this);
        WeaponModules = new OrbitalCombatV2WeaponModules(this);
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
        while (Station.Rings.Count < Mathf.Clamp(BuildRingCount, 1, 3) && Station.AddRing() != null) { }
        foreach (var ring in Station.Rings) FinishSandboxRingPresentation(ring);
        SelectWeaponModulePreset("A");
        ClearSandboxArena(); SetPlayerInvincible(true); SandboxReady = true;
    }

    public void StartScenario(int index)
    {
        scenario = Mathf.Clamp(index, 0, 7);
        SetPaused(false);
        ResetBuild(() =>
        {
            RestoreLabBuild();
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
            RestoreLabBuild();
            if (initialLayout.Length == 0) PopulateScenario();
            else foreach (var entry in initialLayout) AddScenarioEnemy(entry.type, entry.position);
            Notice = "Scenario restored / upgrades reset";
        });
    }

    public void SetPaused(bool value)
    {
        if (Paused == value) return;
        Station?.InputOwner?.PrepareForExternalPause();
        CombatV2?.Suspend();
        Placement?.Cancel();
        ReleaseCenterInput();
        Paused = value;
        Time.timeScale = value ? 0f : 1f;
    }

    public void SelectTestWeapon(bool contact)
        => SelectShootingBuild(contact ? ShootingBuild.Contact : ShootingBuild.Pistol);

    public void SelectShootingBuild(ShootingBuild build)
    {
        if (!CanEdit()) return;
        if (!restoringLabBuild) sandboxWeapons = null;
        if (!restoringLabBuild) WeaponModules.BeginLoadout();
        Placement.LeaveComparison();
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
        Placement.Synchronize();
        for (TargetRing = 0; TargetRing < Station.State.Rings.Count; TargetRing++)
        {
            var ring = Station.State.Rings[TargetRing];
            int count = Mathf.Max(ring.MountCapacity, Placement.RememberedMountCount(ring.Order));
            while (ring.MountCount < count)
            {
                if (ring.MountCount == ring.MountCapacity && !Station.UpgradeRingCapacity(ring.StableRingId)) break;
                if (!Station.AddMount(ring.StableRingId, out _)) break;
            }
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

    private void RestoreLabBuild()
    {
        restoringLabBuild = true;
        try
        {
            // Preset builders only populate the first ring; create the selected ring count first.
            while (Station.Rings.Count < Mathf.Clamp(BuildRingCount, 1, 3) && Station.AddRing() != null) { }
            // Placement builders must validate remembered angles against the edited geometry.
            foreach (var ring in Station.Rings)
                if (sandboxRingSettings.TryGetValue(ring.State.Order, out var settings))
                {
                    if (settings.radius.HasValue) ring.State.Radius = settings.radius.Value;
                }
            if (sandboxWeapons != null) RestoreSandboxWeapons();
            else if (WeaponModules.ActivePreset != null) BuildLabPistols(WeaponModules.ActivePreset == "A" ? 4 : WeaponModules.ActivePreset == "B" ? 1 : 2);
            else if (Placement.IsComparison) PreparePlacementComparison();
            else SelectShootingBuild(selectedBuild);
        }
        finally { restoringLabBuild = false; WeaponModules.FinishRestore(); }
        foreach (var ring in Station.Rings)
            if (sandboxRingSettings.TryGetValue(ring.State.Order, out var settings))
            {
                if (settings.speed.HasValue) ring.State.BaseRotationSpeed = settings.speed.Value;
                if (settings.direction.HasValue) ring.State.Direction = settings.direction.Value;
            }
        Placement.Synchronize();
    }

    private void RestoreSandboxWeapons()
    {
        if (prototypeStation != Station) UpdateCenterPrototype();
        ClearModules(); weaponProfiles.Clear();
        foreach (var ring in Station.Rings)
        {
            int count = Placement.RememberedMountCount(ring.State.Order);
            while (ring.MountCount < count)
            {
                if (ring.MountCount == ring.MountCapacity && !Station.UpgradeRingCapacity(ring.RingId))
                    throw new System.InvalidOperationException("Cannot restore lab mount capacity.");
                if (!Station.AddMount(ring.RingId, out _))
                    throw new System.InvalidOperationException("Cannot restore lab mount.");
            }
        }
        Placement.Synchronize();
        foreach (var entry in sandboxWeapons.OrderBy(e => e.Key.ring).ThenBy(e => e.Key.mount))
        {
            var ring = Station.Rings.Single(r => r.State.Order == entry.Key.ring);
            installingProfile = entry.Value.profile;
            try
            {
                if (!Station.InstallModule(entry.Value.kind, ring.RingId, entry.Key.mount, out string error))
                    throw new System.InvalidOperationException($"Cannot restore lab weapon R{entry.Key.ring}:M{entry.Key.mount}: {error}");
            }
            finally { installingProfile = null; }
        }
    }

    private bool PreparePlacementComparison()
    {
        if (!CanEdit()) return false;
        if (prototypeStation != Station) UpdateCenterPrototype();
        var ring = Station.Rings[0];
        if (Station.Modules.Count == 4 && Station.Modules.All(m => m is EnemyOrbitalLabWeapon gun &&
            ReferenceEquals(gun.SourceProfile, Pistol) && m.CurrentMount?.Ring == ring)) return true;
        if (!restoringLabBuild) WeaponModules.BeginLoadout();
        return BuildLabPistols(4);
    }

    private bool BuildLabPistols(int count)
    {
        if (!CanEdit()) return false;
        if (!restoringLabBuild) sandboxWeapons = null;
        if (prototypeStation != Station) UpdateCenterPrototype();
        var ring = Station.Rings[0];
        Placement.Cancel();
        ClearModules(); weaponProfiles.Clear();
        ((ProductionOrbitalCombatAdapter)Station.Combat).LabModuleFactory = CreateLabModule;
        while (ring.MountCount < count)
        {
            if (ring.MountCount == ring.MountCapacity && !Station.UpgradeRingCapacity(ring.RingId)) return false;
            if (!Station.AddMount(ring.RingId, out _)) return false;
        }
        Pistol.Kind = OrbitalModuleKind.Pistol;
        installingProfile = Pistol;
        try
        {
            for (int i = 0; i < count; i++)
                if (!Station.InstallModule(OrbitalModuleKind.Pistol, ring.RingId, i, out _)) return false;
        }
        finally { installingProfile = null; }
        selectedBuild = ShootingBuild.Pistol;
        Placement.Synchronize();
        return true;
    }

    public void SelectWeaponModulePreset(string name)
    {
        if (!Ready || name != "A" && name != "B" && name != "C") return;
        SetPaused(true); Placement.LeaveComparison(); WeaponModules.BeginPreset(name);
        int count = name == "A" ? 4 : name == "B" ? 1 : 2;
        if (!BuildLabPistols(count)) { Notice = "Не удалось подготовить оружие."; return; }
        Placement.ArrangeModuleComparison(count);
        var guns = Station.Modules.OfType<EnemyOrbitalLabWeapon>().ToArray();
        if (name == "B")
        {
            WeaponModules.UnlockSecondSlot(guns[0].StableModuleId);
            WeaponModules.SetModule(guns[0].StableModuleId, 0, "RAPID");
            WeaponModules.SetModule(guns[0].StableModuleId, 1, "PIERCE");
        }
        else if (name == "C")
        {
            WeaponModules.SetModule(guns[0].StableModuleId, 0, "RAPID");
            WeaponModules.SetModule(guns[1].StableModuleId, 0, "MULTISHOT");
        }
        WeaponModules.Select(guns[0].StableModuleId);
    }

    public void SelectPlacementPreset(string name)
    {
        if (!Ready || name != "EVEN" && name != "GROUPED" && name != "CUSTOM") return;
        SetPaused(true);
        if (!PreparePlacementComparison()) { Notice = "Не удалось подготовить 4 пистолета."; return; }
        Placement.BeginComparison();
        Placement.ApplyPreset(name);
    }

    private OrbitalModuleRuntime CreateLabModule(OrbitalStationRuntime station, OrbitalModuleState state)
    {
        if (installingProfile != null && !weaponProfiles.ContainsKey(state.StableModuleId))
            weaponProfiles[state.StableModuleId] = installingProfile;
        if (!weaponProfiles.TryGetValue(state.StableModuleId, out var profile))
        {
            profile = state.ModuleType switch { OrbitalModuleKind.Pistol => Pistol, OrbitalModuleKind.Shotgun => Shotgun,
                OrbitalModuleKind.Sniper => Sniper, OrbitalModuleKind.Saw => Saw, _ => null };
            if (profile == null) return null;
            weaponProfiles[state.StableModuleId] = profile;
        }
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

    private void DespawnEnemies(bool resetWeapons = true)
    {
        (Station?.Combat as ProductionOrbitalCombatAdapter)?.DebugClearProjectiles();
        Station?.ClearLabFlashes();
        nextShootingShake = 0f;
        nextWeaponSound = 0f;
        CameraShake.Instance?.StopAllShakes();
        if (Station != null && resetWeapons)
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
        Telemetry?.Dispose(); tacticalPreset = tacticalConfiguration = null;
        WeaponModules?.CaptureReset();
        Placement?.Unbind();
        CombatV2?.Dispose(); CombatV2 = null;
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
            bool pointerBlocked = PanelVisible && PanelRect.Contains(new Vector2(Input.mousePosition.x, Screen.height - Input.mousePosition.y));
            Placement.TickPointer(CameraRig.ControlledCamera, pointerBlocked || Station.InputOwner.IsGameplayPointerBlocked);
            if (Placement.HasSelection)
            {
                if (SandboxSelectedWeaponId != Placement.SelectedModuleId) SetSandboxTab(2);
                SandboxSelectedWeaponId = Placement.SelectedModuleId; WeaponModules.Select(Placement.SelectedModuleId);
            }
            TickSandbox(pointerBlocked);
        }
        else { ReleaseCenterInput(); CombatV2?.Suspend(); }
        enemies.RemoveAll(e => e == null);
    }

    private void UpdateCenterPrototype()
    {
        if (CombatV2 == null) CombatV2 = new OrbitalCombatV2LabControl(Station);
        Placement.Bind(Station);
        WeaponModules.Bind(Station);
        Telemetry.Bind((ProductionOrbitalCombatAdapter)Station.Combat, Player);
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
        Telemetry?.Dispose();
        sandboxView?.RestoreVisuals();
        WeaponModules?.HideSpokes();
        Placement?.Unbind();
        CombatV2?.Dispose(); CombatV2 = null;
        CloseUpgradeChoices();
        centerControl?.Dispose();
        if (centerMarker != null) centerMarker.enabled = false;
        if (ReferenceEquals(ProductionOrbitalCombatAdapter.LabInterceptor, this)) ProductionOrbitalCombatAdapter.LabInterceptor = null;
        SetPaused(false);
    }
    private void OnEnable() => ProductionOrbitalCombatAdapter.LabInterceptor = this;
    private void OnApplicationFocus(bool focused) { if (!focused) { CombatV2?.Suspend(); Placement?.Cancel(); } }
    private void OnApplicationPause(bool paused) { if (paused) { CombatV2?.Suspend(); Placement?.Cancel(); } }
    private void LateUpdate() { WeaponModules?.UpdateSpokes(); sandboxView?.Tick(PanelVisible, PanelRect); }

    protected override Rect PanelRect => new(0, 0, Mathf.Clamp(Screen.width * .25f, 350f, 420f), Screen.height);
    protected override void OnGUI() => sandboxView?.Draw(PanelVisible, PanelRect);
    protected override void DrawPanel() { }
    protected override void OnDestroy() { sandboxView?.Dispose(); EnemyDebugAiFreeze.SetFrozen(false); base.OnDestroy(); }
}
#endif
