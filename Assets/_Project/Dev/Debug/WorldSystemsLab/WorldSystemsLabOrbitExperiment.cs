#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Subject42.Combat.OrbitalStation;
using UnityEngine;

// An opt-in, scene-local projection over the existing lab build. Never writes Cells or run loadout data.
[DefaultExecutionOrder(-350)]
public sealed class WorldSystemsLabOrbitExperiment : MonoBehaviour
{
    public enum Anomaly { None, Blue, Red, Glitch, Interference, Suppression }
    [SerializeField] private WorldSystemsLabController world;
    [SerializeField] private EnemyOrbitalLabController orbit;
    [SerializeField] private LocalAnomalyData[] zoneAssets;
    [Header("Temporary orbit effects")]
    [Range(.1f, 1f)] public float BlueRotationMultiplier = .5f;
    [Min(1f)] public float RedEnergyMultiplier = 1.75f;
    [Min(.1f)] public float GlitchInterval = 10f;
    [Min(0f)] public float GlitchWarning = 1f;
    public int GlitchSeed = 42031;
    [Range(0f, 12f)] public float LevelDeviation = 12f;
    [Range(0f, 5f)] public float WeaponDeviation = 5f;
    [Min(.1f)] public float LevelPeriod = 6f, WeaponPeriod = 2.3f;
    [Min(.01f)] public float InterferenceFadeIn = .5f;

    sealed class Zone
    {
        public LocalAnomalyZone Source;
        public BoxCollider2D Area;
        public LocalAnomalyData Data;
        public Anomaly Kind;
        public Renderer[] Visuals;
        public bool Contains(Vector2 point)
        {
            if (Source == null || !Source.gameObject.activeInHierarchy) return false;
            Vector2 local = Area.transform.InverseTransformPoint(point);
            local -= Area.offset;
            return Mathf.Abs(local.x) <= Area.size.x * .5f && Mathf.Abs(local.y) <= Area.size.y * .5f;
        }
    }
    readonly List<Zone> zones = new();
    OrbitalAnomalyProjection geometry;
    readonly HashSet<EnemyAnomalyEffects> affectedEnemies = new();
    MaterialPropertyBlock properties;
    System.Random random;
    Zone current;
    float elapsed, glitchClock, nextWarningFlash;
    string notice = "Tab — конструктор. Сборка общая для всех зон.";
    EnemyOrbitalLabTelemetry.Report previousComparison;
    float originalIncomingDamage;
    CorridorLab corridor;
    bool corridorWasEnabled, hasActivatedOrbit, mapWasVisible;
    GUIStyle text, heading, button;
    Vector2 panelScroll;
    public bool Active { get; private set; }
    public bool Ready => Active && orbit != null && orbit.Ready && orbit.SegmentedOrbit && orbit.Segmented.Bound;
    public Anomaly ActiveAnomaly => current?.Kind ?? Anomaly.None;
    public bool IsGlitchWarning => ActiveAnomaly == Anomaly.Glitch &&
        glitchClock >= Mathf.Max(0f, GlitchInterval - Mathf.Min(GlitchWarning, GlitchInterval));
    public int GlitchPulses { get; private set; }
    public float RotationSpeedFactor => ActiveAnomaly == Anomaly.Blue ? BlueRotationMultiplier : 1f;
    public float RotationDrainFactor => ActiveAnomaly == Anomaly.Red ? RedEnergyMultiplier : 1f;
    public EnemyOrbitalLabController Orbit => orbit;
    public int ZoneCount => zones.Count;
    public Rect PanelRect => new(Screen.width - Mathf.Min(330f,Screen.width * .28f) - 10,78,
        Mathf.Min(330f,Screen.width * .28f),Mathf.Min(590,Screen.height - 150));
    public bool BlocksPointer => Active && PanelRect.Contains(new Vector2(Input.mousePosition.x,Screen.height - Input.mousePosition.y));
    public static string Name(Anomaly kind) => kind switch
    {
        Anomaly.Blue => "СИНЯЯ", Anomaly.Red => "КРАСНАЯ", Anomaly.Glitch => "ГЛИТЧ",
        Anomaly.Interference => "ИНТЕРФЕРЕНЦИЯ", Anomaly.Suppression => "ПОДАВЛЕНИЕ", _ => "ВНЕ ЗОН"
    };
    public string Effects => ActiveAnomaly switch
    {
        Anomaly.Blue => $"Игрок и враги замедлены. Поворот Q/E ×{BlueRotationMultiplier:0.##}.",
        Anomaly.Red => $"Враги ускорены. Энергия Q/E ×{RedEnergyMultiplier:0.##}; скорость поворота прежняя.",
        Anomaly.Glitch => $"Перестановка внутри уровня каждые {GlitchInterval:0.#} с. Модули и связи следуют за пушками.",
        Anomaly.Interference => $"Уровни ±{LevelDeviation:0.#}°, оружие ±{WeaponDeviation:0.#}°. Меняется направление FIXED; Q/E работает.",
        Anomaly.Suppression => "Индивидуальные модули и бонусы связей временно выключены. Все пушки стреляют базово.",
        _ => "Базовая сборка. FIXED · 3 × 12 · ручной поворот Q/E за энергию."
    };

    public void Begin()
    {
        if (Active || world == null || orbit == null || gameObject.scene.path != WorldSystemsLabController.ScenePath) return;
        originalIncomingDamage = world.Player.GetComponent<PlayerHealth>().IncomingDamageMultiplier;
        mapWasVisible = world.TacticalMap != null && world.TacticalMap.IsVisible;
        world.TacticalMap?.SetVisible(false);
        world.ClearEvents(); world.ClearAnomalies(); world.ApplyWorldRule(null);
        corridor = world.GetComponent<CorridorLab>();
        if (corridor != null) { corridorWasEnabled = corridor.enabled; corridor.enabled = false; }
        Active = true; random = new System.Random(GlitchSeed); GlitchPulses = 0;
        orbit.AnomalyExperiment = this;
        foreach (var data in zoneAssets)
        {
            if (data == null || !world.SpawnNormalAnomaly(data)) continue;
            var source = world.SpawnedSites.Last().AnomalyZone;
            // Disabled MonoBehaviours can still receive trigger messages: retire the collider as well.
            source.enabled = false;
            var collider = (BoxCollider2D)source.FocusArea; collider.enabled = false;
            zones.Add(new Zone { Source = source, Area = collider, Data = data, Kind = Kind(data.AnomalyType),
                Visuals = source.GetComponentsInChildren<Renderer>(true) });
        }
        orbit.gameObject.SetActive(true);
        if (hasActivatedOrbit) orbit.RestartAttachedBuild();
        hasActivatedOrbit = true;
        StartCoroutine(Prepare());
    }
    IEnumerator Prepare()
    {
        float deadline = Time.realtimeSinceStartup + 15f;
        while ((!orbit.Ready || !orbit.SandboxReady) && Time.realtimeSinceStartup < deadline) yield return null;
        if (!orbit.Ready || !orbit.SandboxReady) { notice = "Не удалось подключить Segmented Orbit."; End(); yield break; }
        orbit.SetSegmentedOrbit(true);
        while ((!orbit.Ready || !orbit.Segmented.Bound) && Time.realtimeSinceStartup < deadline) yield return null;
        if (!Ready) { End(); yield break; }
        orbit.Segmented.SetMotion(SegmentedOrbitConfiguration.Motion.Manual);
        orbit.Segmented.LoadPreset("Multi-Layer");
        MoveTo(-1);
    }
    static Anomaly Kind(LocalAnomalyType type) => type switch
    {
        LocalAnomalyType.Stasis => Anomaly.Blue, LocalAnomalyType.Berserk => Anomaly.Red,
        LocalAnomalyType.Glitch => Anomaly.Glitch, LocalAnomalyType.Gravity => Anomaly.Interference,
        LocalAnomalyType.ExplosiveZone => Anomaly.Suppression, _ => Anomaly.None
    };
    void Update()
    {
        if (!Active) return;
        properties ??= new MaterialPropertyBlock();
        TickEffects(Time.deltaTime);
        foreach (var zone in zones)
            foreach (var renderer in zone.Visuals)
            {
                if (renderer == null) continue;
                renderer.GetPropertyBlock(properties);
                properties.SetFloat("_Fade", 1f); properties.SetFloat("_VisualTime", Time.time);
                renderer.SetPropertyBlock(properties);
            }
    }
    // Also used by the bounded scenario to advance the 10-second clock without a long real-time run.
    public void TickEffects(float deltaTime)
    {
        if (!Ready || orbit.Paused) { RestoreTransientEffects(); return; }
        var next = zones.FirstOrDefault(zone => zone.Contains(world.Player.position));
        if (next != current)
        {
            RestoreTransientEffects(); current = next;
            if (current != null)
            {
                geometry = new OrbitalAnomalyProjection(orbit.Station,m => orbit.Segmented.At(m.Ring.State.Order,m.MountIndex).Segment);
                geometry.Capture();
            }
        }
        elapsed += Mathf.Max(0f, deltaTime);
        orbit.WeaponModules.SetEffectsSuppressed(ActiveAnomaly == Anomaly.Suppression);
        ApplyMovement();
        if (ActiveAnomaly == Anomaly.Glitch)
        {
            glitchClock += Mathf.Max(0f, deltaTime);
            if (glitchClock >= Mathf.Max(.1f, GlitchInterval))
            {
                glitchClock %= Mathf.Max(.1f, GlitchInterval);
                ShuffleWithinLevels(); GlitchPulses++;
            }
            if (IsGlitchWarning && elapsed >= nextWarningFlash)
            {
                nextWarningFlash = elapsed + .12f;
                foreach (var ring in orbit.Station.Rings) ring.PulseVisual();
                foreach (var gun in orbit.Station.Modules)
                    orbit.Station.FlashLink(orbit.Station.transform.position, gun.WorldPosition, new Color(1f,.25f,.9f), .13f);
            }
        }
        ApplyGeometry();
    }
    void ShuffleWithinLevels()
    {
        geometry?.Shuffle(random);
    }
    void ApplyGeometry()
    {
        geometry?.Apply(elapsed,ActiveAnomaly == Anomaly.Interference,LevelDeviation,WeaponDeviation,LevelPeriod,WeaponPeriod,InterferenceFadeIn);
    }
    void ApplyMovement()
    {
        var movement = world.Player.GetComponent<CharacterMovement2D>();
        if (ActiveAnomaly == Anomaly.Blue) movement.SetAnomalySpeedMultiplier(this, current.Data.PlayerSpeedMultiplier);
        else movement.RemoveAnomalySpeedMultiplier(this);
        foreach (var enemy in orbit.LabEnemies)
        {
            if (enemy == null || enemy.IsDead) continue;
            var zone = zones.FirstOrDefault(z => z.Contains(enemy.transform.position));
            var effects = EnemyAnomalyEffects.GetOrCreate(enemy);
            if (zone != null && (zone.Kind == Anomaly.Blue || zone.Kind == Anomaly.Red))
            {
                effects.EnterZone(this, zone.Data.EnemySpeedMultiplier,
                    zone.Kind == Anomaly.Blue ? new Color(.45f,.78f,1f) : new Color(1f,.42f,.38f));
                affectedEnemies.Add(effects);
            }
            else { effects.ExitZone(this); affectedEnemies.Remove(effects); }
        }
    }
    public void RestoreTransientEffects()
    {
        geometry?.Restore(); geometry = null;
        orbit?.WeaponModules?.SetEffectsSuppressed(false);
        if (world != null && world.Player != null) world.Player.GetComponent<CharacterMovement2D>()?.RemoveAnomalySpeedMultiplier(this);
        foreach (var effects in affectedEnemies) if (effects != null) effects.ExitZone(this);
        affectedEnemies.Clear(); current = null; elapsed = glitchClock = nextWarningFlash = 0f;
    }
    public Vector2 ZoneCenter(int index) => zones[index].Area.transform.TransformPoint(zones[index].Area.offset);
    public Anomaly ZoneKind(int index) => zones[index].Kind;
    public bool ContainsAnyZone(Vector2 point) => zones.Any(zone => zone.Contains(point));
    // Existing telemetry should invalidate an edited loadout, not this experiment's transient projection.
    public string ConfigurationSignature() => !Ready ? "" :
        string.Join(";",orbit.Station.Modules.Select(g =>
            $"{g.StableModuleId}:{g.CurrentMount.Ring.RingId}:{g.CurrentMount.MountIndex}:" +
            (geometry?.OriginalPhase(g.CurrentMount) ?? g.CurrentMount.LocalPhase) + ":" +
            string.Join(",",Enumerable.Range(0,orbit.WeaponModules.SlotCount(g.StableModuleId)).Select(s => orbit.WeaponModules.GetModule(g.StableModuleId,s))) +
            (g is EnemyOrbitalLabWeapon gun ? JsonUtility.ToJson(gun.SourceProfile) : ""))) +
        string.Join(";",orbit.Segmented.Cells.Select(c => $"{c.Segment}:{c.Weapon}")) +
        string.Join(";",orbit.Segmented.Links.Select(l => $"{l.A}:{l.B}:{l.Module}")) +
        string.Join(";",orbit.Station.Rings.Select(r => $"{r.RingId}:{r.Radius}:{r.RotationSpeed}:{r.State.Direction}")) +
        $"|{orbit.Station.State.ShootingDamageMultiplier}:{orbit.Station.State.ShootingRateMultiplier}:" +
        $"{orbit.ModuleRapidMultiplier}:{orbit.ModulePierceBonus}:{orbit.ModuleMultishotAngle}:{orbit.SegmentedSkipStrength}:{orbit.SegmentedExtraShotCooldown}:" +
        $"{orbit.ManualRotationCapacity}:{orbit.ManualRotationSpeed}:{orbit.ManualRotationDrain}:{orbit.ManualRotationRecovery}:" +
        $"{orbit.Player.MaxHealth}:{orbit.Player.GetComponent<CharacterMovement2D>().RunUpgradeMoveSpeedMultiplier}:" +
        $"{orbit.SpeedMultiplier}:{orbit.EnemiesInvincible}:{EnemyDebugAiFreeze.IsFrozen}";
    Vector2 OutsidePoint()
    {
        Bounds bounds = GameplayAreaService.Instance.PlayableArea.bounds;
        Vector2 best = bounds.center; float distance = float.MaxValue;
        for (int y = 1; y < 20; y++) for (int x = 1; x < 20; x++)
        {
            Vector2 point = new(Mathf.Lerp(bounds.min.x,bounds.max.x,x / 20f), Mathf.Lerp(bounds.min.y,bounds.max.y,y / 20f));
            if (!ContainsAnyZone(point) && point.sqrMagnitude < distance) { best = point; distance = point.sqrMagnitude; }
        }
        return best;
    }
    public void MoveTo(int index)
    {
        if (!Active || index < -1 || index >= zones.Count) return;
        RestoreTransientEffects();
        Vector2 point = index < 0 ? OutsidePoint() : ZoneCenter(index);
        var body = world.Player.GetComponent<Rigidbody2D>();
        if (body != null) { body.position = point; body.linearVelocity = Vector2.zero; }
        world.Player.position = new Vector3(point.x,point.y,world.Player.position.z); Physics2D.SyncTransforms();
        if (orbit.CameraRig != null) orbit.CameraRig.transform.position = world.Player.position + orbit.CameraRig.offset;
        TickEffects(0f);
    }
    public void Compare(int index)
    {
        if (!Ready) return;
        if (orbit.Telemetry.Recording) previousComparison = orbit.Telemetry.Snapshot();
        orbit.ClearSandboxArena(); MoveTo(index);
        orbit.Station.State.RotationEnergy = orbit.ManualRotationCapacity;
        orbit.StartSandboxEncounter(2); orbit.SetPaused(false); TickEffects(0f);
        orbit.Telemetry.Begin("Аномалии: " + Name(ActiveAnomaly));
        notice = "Та же сборка; тот же повторяемый смешанный спаун. Энергия полная.";
    }
    public void End()
    {
        if (!Active) return;
        StopAllCoroutines(); RestoreTransientEffects(); Active = false;
        if (orbit != null)
        {
            orbit.CancelAttachedBuildReset();
            orbit.ClearSandboxArena(); orbit.Segmented?.Unbind(); orbit.Station?.Teardown(); orbit.gameObject.SetActive(false);
            if (orbit.ScenePlayer != null) orbit.ScenePlayer.SetIncomingDamageMultiplier(originalIncomingDamage);
        }
        world?.ClearAnomalies(); zones.Clear();
        world?.TacticalMap?.SetVisible(mapWasVisible);
        if (corridor != null) corridor.enabled = corridorWasEnabled;
    }
    void OnDisable() { if (Active) End(); }
    void OnGUI()
    {
        if (!Active) return;
        text ??= new GUIStyle(GUI.skin.label) { fontSize = 14, wordWrap = true };
        heading ??= new GUIStyle(text) { fontSize = 18, fontStyle = FontStyle.Bold };
        button ??= new GUIStyle(GUI.skin.button) { fontSize = 13, wordWrap = true, fixedHeight = 29 };
        Color savedColor = GUI.color;
        GUI.color = new Color(.025f,.035f,.045f,.97f); GUI.DrawTexture(PanelRect,Texture2D.whiteTexture); GUI.color = savedColor;
        GUILayout.BeginArea(PanelRect,GUI.skin.box);
        GUILayout.Label("АНОМАЛИИ × SEGMENTED ORBIT",heading);
        panelScroll = GUILayout.BeginScrollView(panelScroll);
        GUILayout.Label(Ready && orbit.Paused ? "КОНСТРУКТОР · эффекты сняты" : Name(ActiveAnomaly),heading);
        GUILayout.Label(Effects,text);
        if (IsGlitchWarning) GUILayout.Label($"⚠ ПЕРЕСТАНОВКА через {Mathf.Max(0,GlitchInterval - glitchClock):0.0} с",heading);
        GUILayout.Label("Быстрое сравнение: та же сборка / 12 врагов",text);
        GUI.enabled = Ready;
        if (GUILayout.Button("A · Вне зон",button)) Compare(-1);
        for (int i = 0; i < zones.Count; i++)
        { int chosen = i; if (GUILayout.Button("B · " + Name(zones[i].Kind),button)) Compare(chosen); }
        if (GUILayout.Button("Только выйти из зоны (без нового боя)",button)) MoveTo(-1);
        if (Ready)
        {
            var report = orbit.Telemetry.Snapshot();
            GUILayout.Label($"Сейчас: выстрелы {report.Weapons.Sum(w => w.Fired)}, попадания {report.Weapons.Sum(w => w.HitShots)}, урон {report.Weapons.Sum(w => w.Damage):0}",text);
            if (previousComparison != null)
                GUILayout.Label($"Предыдущий: {previousComparison.Scenario}\n{previousComparison.Elapsed:0.0} с · выстрелы {previousComparison.Weapons.Sum(w => w.Fired)} · урон {previousComparison.Weapons.Sum(w => w.Damage):0}",text);
        }
        GUILayout.Label(notice,text); GUI.enabled = true;
        GUILayout.EndScrollView();
        if (GUILayout.Button("Вернуться в World Systems Lab",button)) End();
        GUILayout.EndArea();
        var camera = Camera.main;
        if (camera == null) return;
        foreach (var zone in zones)
        {
            if (zone.Source == null) continue;
            Vector3 point = camera.WorldToScreenPoint(zone.Source.transform.position);
            GUI.Label(new Rect(point.x - 100,Screen.height - point.y - 65,200,26),Name(zone.Kind),heading);
        }
    }
}
#endif
