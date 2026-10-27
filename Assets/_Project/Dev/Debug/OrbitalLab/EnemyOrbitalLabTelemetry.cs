#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using System.Linq;
using Subject42.Combat.OrbitalStation;
using UnityEngine;

// Passive scene-local counters. The existing adapter remains the only collision/damage owner.
public sealed class EnemyOrbitalLabTelemetry : IDisposable
{
    [Serializable] public sealed class WeaponRow
    {
        public int WeaponId, Fired, HitShots, Misses, Cancelled, Pending, BodyHits, ShieldBlocks, Kills;
        public float Damage;
        public string Name, Modules;
        public int[] DirectionSectors = new int[18];
    }
    [Serializable] public sealed class Report
    {
        public string Scenario, Build;
        public float Elapsed, DamageReceived, DistanceTravelled, MaximumCenterOffset, OrbitSlowSeconds;
        public bool SurvivalApplicable, ConfigurationChanged;
        public int PlayerImpacts;
        public WeaponRow[] Weapons;
    }
    sealed class Flight { public WeaponRow Weapon; public bool Hit; }
    readonly EnemyOrbitalLabController lab;
    readonly Dictionary<int, WeaponRow> weapons = new();
    readonly Dictionary<long, Flight> flights = new();
    ProductionOrbitalCombatAdapter adapter;
    PlayerHealth player;
    Report report = new() { Scenario = "Бой ещё не начат", Weapons = Array.Empty<WeaponRow>() };
    string configuration;
    Vector2 previousPosition;
    public bool Recording { get; private set; }
    public EnemyOrbitalLabTelemetry(EnemyOrbitalLabController owner) => lab = owner;
    public void Bind(ProductionOrbitalCombatAdapter combat, PlayerHealth health)
    {
        if (adapter == combat && player == health) return;
        Unbind(); adapter = combat; player = health;
        adapter.LabProjectileSpawned += Spawned; adapter.LabProjectileContact += Contact; adapter.LabProjectileEnded += Ended;
        player.DebugDamageApplied += PlayerHit;
    }
    internal string Configuration() => lab.Station == null ? "" : string.Join(";", lab.Station.Modules.Select(g =>
        $"{g.StableModuleId}:{g.CurrentMount?.Ring.RingId}:{g.CurrentMount?.LocalPhase}:" +
        string.Join(",", Enumerable.Range(0, lab.WeaponModules.SlotCount(g.StableModuleId)).Select(s => lab.WeaponModules.GetModule(g.StableModuleId, s))))) +
        string.Join(";", lab.Station.Rings.Select(r => $"{r.RingId}:{r.RotationSpeed}:{r.State.Direction}"));
    public void Begin(string scenario)
    {
        weapons.Clear(); flights.Clear();
        report = new Report { Scenario = scenario, Build = lab.TacticalPreset ?? "Своя сборка", SurvivalApplicable = !lab.PlayerInvincible && player != null && !player.DebugInvulnerable && player.IncomingDamageMultiplier > 0f };
        foreach (var gun in lab.Station.Modules.OfType<EnemyOrbitalLabWeapon>()) Register(gun);
        configuration = Configuration(); previousPosition = player.transform.position; Recording = true;
    }
    public static string WeaponName(OrbitalModuleKind kind) => kind switch
    { OrbitalModuleKind.Shotgun => "Дробовик", OrbitalModuleKind.Sniper => "Снайперка", _ => "Пистолет" };
    public void Tick(float deltaTime)
    {
        if (!Recording || deltaTime <= 0f) return;
        if (configuration != Configuration()) report.ConfigurationChanged = true;
        report.Elapsed += deltaTime;
        Vector2 position = player.transform.position;
        report.DistanceTravelled += Vector2.Distance(position, previousPosition); previousPosition = position;
        report.MaximumCenterOffset = Mathf.Max(report.MaximumCenterOffset, Vector2.Distance(lab.Station.transform.position, position));
        if (lab.CombatV2?.IsSlowingOrbits == true) report.OrbitSlowSeconds += deltaTime;
        if (lab.PlayerInvincible || player.DebugInvulnerable || player.IncomingDamageMultiplier <= 0f) report.SurvivalApplicable = false;
    }
    void Spawned(long sequence, int id, Vector2 direction)
    {
        if (!Recording) return;
        if (!weapons.TryGetValue(id, out var row))
        {
            var gun = lab.Station.Modules.OfType<EnemyOrbitalLabWeapon>().FirstOrDefault(g => g.StableModuleId == id);
            if (gun == null) return;
            row = Register(gun); report.ConfigurationChanged = true;
        }
        row.Fired++;
        int sector = Mathf.FloorToInt(Mathf.Repeat(Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg, 360f) / 20f);
        row.DirectionSectors[Mathf.Clamp(sector, 0, 17)]++;
        flights[sequence] = new Flight { Weapon = row };
    }
    WeaponRow Register(EnemyOrbitalLabWeapon gun)
    {
        var row = new WeaponRow { WeaponId = gun.StableModuleId, Name = $"{WeaponName(gun.Kind)} {weapons.Count + 1}", Modules = string.Join(" + ", Enumerable.Range(0, lab.WeaponModules.SlotCount(gun.StableModuleId)).Select(s => lab.WeaponModules.GetModule(gun.StableModuleId, s))) };
        weapons.Add(gun.StableModuleId, row); return row;
    }
    void Contact(long sequence, float damage, bool killed, bool shield)
    {
        if (!Recording || !flights.TryGetValue(sequence, out var flight)) return;
        if (shield) { flight.Weapon.ShieldBlocks++; return; }
        if (!flight.Hit) { flight.Hit = true; flight.Weapon.HitShots++; }
        flight.Weapon.BodyHits++; flight.Weapon.Damage += damage;
        if (killed) flight.Weapon.Kills++;
    }
    void Ended(long sequence, bool cancelled)
    {
        if (!Recording || !flights.Remove(sequence, out var flight) || flight.Hit) return;
        if (cancelled) flight.Weapon.Cancelled++; else flight.Weapon.Misses++;
    }
    void PlayerHit(float damage)
    {
        if (!Recording) return;
        report.PlayerImpacts++; report.DamageReceived += damage;
        if (lab.PlayerInvincible || player.IncomingDamageMultiplier <= 0f) report.SurvivalApplicable = false;
    }
    public Report Snapshot()
    {
        if (Recording && configuration != Configuration()) report.ConfigurationChanged = true;
        foreach (var row in weapons.Values) row.Pending = flights.Values.Count(f => f.Weapon == row && !f.Hit);
        report.Weapons = weapons.Values.ToArray(); return report;
    }
    public void Stop() { Snapshot(); Recording = false; }
    void Unbind()
    {
        Stop();
        if (adapter != null) { adapter.LabProjectileSpawned -= Spawned; adapter.LabProjectileContact -= Contact; adapter.LabProjectileEnded -= Ended; }
        if (player != null) player.DebugDamageApplied -= PlayerHit;
        adapter = null; player = null;
    }
    public void Dispose() => Unbind();
}
#endif
