#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Subject42.Combat.OrbitalStation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

public sealed class OrbitalCombatV2TacticsTests
{
    const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    EnemyOrbitalLabController lab;
    OrbitalStationRuntime station;
    ProductionOrbitalCombatAdapter adapter;
    [Serializable] public sealed class Row { public int WeaponId, Fired, HitShots, Misses, Cancelled, Pending, BodyHits, ShieldBlocks, Kills; public float Damage; public string Name, Modules; }
    [Serializable] public sealed class Report { public Row[] Weapons; public string Build; public float Elapsed, DamageReceived; public bool SurvivalApplicable, ConfigurationChanged; public int PlayerImpacts; }
    [SetUp] public void Preserve() => CoreTestSupport.PreservePreferences();
    [UnitySetUp] public IEnumerator Open()
    {
        EditorSceneManager.OpenScene(EnemyOrbitalLabController.ScenePath);
        yield return new EnterPlayMode();
        lab = Object.FindFirstObjectByType<EnemyOrbitalLabController>();
        yield return CoreTestSupport.Await(() => lab.Ready && lab.SandboxReady);
        station = lab.Station; adapter = (ProductionOrbitalCombatAdapter)station.Combat;
        lab.ClearSandboxArena(); lab.SetPaused(true); lab.SetPlayerInvincible(true);
    }
    [UnityTearDown] public IEnumerator Cleanup() => CoreTestSupport.CleanupPlayMode();
    object Call(string name, params object[] args)
    {
        var method = lab.GetType().GetMethod(name, Flags);
        Assert.That(method, Is.Not.Null, "Missing tactical lab action: " + name);
        return method.Invoke(lab, args);
    }
    Report Read() => JsonUtility.FromJson<Report>((string)Call("CombatTelemetryJson"));
    static void Ready(OrbitalProjectileModule gun) => typeof(OrbitalModuleRuntime).GetProperty("RuntimeCooldown", Flags).SetValue(gun, 0f);
    [UnityTest] public IEnumerator ArchetypesFireOutwardWithoutTargetsAndUseTheirIndividualModules()
    {
        Call("ApplyTacticalPreset", "MIXED"); station.enabled = false;
        Assert.That(lab.AliveEnemies, Is.Zero);
        var guns = station.Modules.OfType<EnemyOrbitalLabWeapon>().ToArray();
        CollectionAssert.AreEquivalent(new[] { OrbitalModuleKind.Pistol, OrbitalModuleKind.Pistol, OrbitalModuleKind.Shotgun, OrbitalModuleKind.Sniper }, guns.Select(g => g.Kind));
        foreach (var gun in guns)
        {
            adapter.DebugClearProjectiles(); Ready(gun); gun.Tick(.001f);
            Assert.That(adapter.LabActiveProjectileCount, Is.EqualTo(gun.Settings.Pellets + (lab.WeaponModules.HasModule(gun.StableModuleId, "MULTISHOT") ? 1 : 0)));
            var shots = ((IEnumerable)adapter.GetType().GetField("projectiles", Flags).GetValue(adapter)).Cast<object>().Where(p => (bool)p.GetType().GetField("Active", Flags).GetValue(p));
            Vector2 radial = ((Vector2)gun.CurrentMount.Transform.position - (Vector2)station.transform.position).normalized;
            foreach (var shot in shots)
                Assert.That(Vector2.Angle(radial, (Vector2)shot.GetType().GetField("Direction", Flags).GetValue(shot)), Is.LessThanOrEqualTo(gun.Settings.SpreadDegrees * .5f + 6.01f));
        }
        Assert.That(guns.Single(g => g.Kind == OrbitalModuleKind.Sniper).Settings.BodyPierces, Is.EqualTo(lab.Sniper.BodyPierces + 2));
        Assert.That(guns.Single(g => g.Kind == OrbitalModuleKind.Shotgun).Settings.Pellets, Is.EqualTo(7));
        Assert.That(guns[0].Settings.FireInterval, Is.EqualTo(lab.Pistol.FireInterval / 1.5f).Within(.0001f));

        // End the isolated firing diagnostics before exercising the real restart path.
        adapter.DebugClearProjectiles(); station.enabled = true;
        Call("ApplyTacticalPreset", "MIXED");
        (int Ring, int Mount, OrbitalModuleKind Kind, float Phase, string[] Modules)[] WeaponComposition()
            => lab.Station.Modules.OrderBy(g => g.CurrentMount.Ring.State.Order)
                .ThenBy(g => g.CurrentMount.MountIndex)
                .Select(g => (g.CurrentMount.Ring.State.Order, g.CurrentMount.MountIndex, g.Kind,
                    g.CurrentMount.LocalPhase, Enumerable.Range(0, lab.WeaponModules.SlotCount(g.StableModuleId))
                        .Select(slot => lab.WeaponModules.GetModule(g.StableModuleId, slot)).ToArray())).ToArray();
        var expectedWeapons = WeaponComposition();
        var expectedOccupancy = station.Rings.OrderBy(r => r.State.Order)
            .Select(r => r.Mounts.Count(m => m.Occupied)).ToArray();
        var oldStation = station;
        lab.RestartScenario();
        yield return CoreTestSupport.Await(() => lab.Ready && lab.SandboxReady && lab.Station != oldStation);
        lab.SetPaused(true);
        station = lab.Station; adapter = (ProductionOrbitalCombatAdapter)station.Combat;
        var actualWeapons = WeaponComposition();
        var actualOccupancy = station.Rings.OrderBy(r => r.State.Order)
            .Select(r => r.Mounts.Count(m => m.Occupied)).ToArray();
        TestContext.WriteLine("Expected MIXED weapons: " + string.Join("; ", expectedWeapons.Select(w =>
            FormattableString.Invariant($"R{w.Ring}:M{w.Mount} {w.Kind} phase={w.Phase:R} modules=[{string.Join(",", w.Modules)}]"))));
        TestContext.WriteLine("Actual MIXED weapons: " + string.Join("; ", actualWeapons.Select(w =>
            FormattableString.Invariant($"R{w.Ring}:M{w.Mount} {w.Kind} phase={w.Phase:R} modules=[{string.Join(",", w.Modules)}]"))));
        TestContext.WriteLine("Expected/actual MIXED occupied mounts by ring: [" + string.Join(",", expectedOccupancy)
            + "] / [" + string.Join(",", actualOccupancy) + "]");
        CollectionAssert.AreEqual(expectedOccupancy, actualOccupancy, "Restart must preserve MIXED ring occupancy.");
        Assert.That(actualWeapons.Length, Is.EqualTo(expectedWeapons.Length), "Restart MIXED weapon count.");
        for (int i = 0; i < expectedWeapons.Length; i++)
        {
            var expected = expectedWeapons[i]; var actual = actualWeapons[i];
            Assert.That(actual.Ring, Is.EqualTo(expected.Ring), "MIXED weapon ring at index " + i);
            Assert.That(actual.Mount, Is.EqualTo(expected.Mount), "MIXED weapon mount at index " + i);
            Assert.That(actual.Kind, Is.EqualTo(expected.Kind), "MIXED weapon kind at index " + i);
            Assert.That(actual.Phase, Is.EqualTo(expected.Phase).Within(.001f), "MIXED weapon local phase at index " + i);
            CollectionAssert.AreEqual(expected.Modules, actual.Modules, "MIXED individual module slots at index " + i);
        }
    }
    [UnityTest] public IEnumerator TelemetryReconcilesPiercingOverkillMissesClearAndPoolReuse()
    {
        Call("ApplyTacticalPreset", "EVEN"); station.enabled = false;
        var guns = station.Modules.OfType<EnemyOrbitalLabWeapon>().ToArray();
        var ids = guns.Select(g => g.StableModuleId).ToArray();
        Call("ApplyTacticalPreset", "GROUPED");
        CollectionAssert.AreEqual(ids, station.Modules.Select(g => g.StableModuleId));
        Call("ApplyTacticalPreset", "EVEN");
        var ring = guns[0].CurrentMount.Ring; ring.State.CurrentPhase = 0; ring.Tick(0);
        foreach (var pair in new[] { (1f, 5f), (2f, 20f) })
        {
            var body = new GameObject("Telemetry body"); body.transform.SetParent(lab.transform);
            body.transform.position = guns[0].WorldPosition + Vector2.right * pair.Item1;
            var health = body.AddComponent<EnemyHealth>(); health.SetRuntimeMaxHealth(pair.Item2);
            var dummy = body.AddComponent<CombatFeelTestDummy>(); dummy.Initialize(null); dummy.Invulnerable = false;
            body.AddComponent<CircleCollider2D>().radius = .12f;
        }
        Physics2D.SyncTransforms(); lab.WeaponModules.SetModule(ids[0], 0, "PIERCE");
        Call("BeginCombatTelemetry", "Fixture");
        var beforeVolley = lab.Telemetry.Snapshot();
        string beforeVolleyJson = JsonUtility.ToJson(beforeVolley);
        Ready(guns[0]); guns[0].Tick(.001f);
        var inFlight = lab.Telemetry.Snapshot();
        string inFlightJson = JsonUtility.ToJson(inFlight);
        Assert.That(inFlight.Weapons.Single(w => w.WeaponId == ids[0]).Pending, Is.EqualTo(1));
        adapter.Tick(1f);
        lab.Telemetry.Tick(.25f);
        var afterVolley = lab.Telemetry.Snapshot();
        Assert.That(JsonUtility.ToJson(beforeVolley), Is.EqualTo(beforeVolleyJson), "Saved snapshot must survive real firing, Tick and another Snapshot.");
        Assert.That(afterVolley.Elapsed, Is.EqualTo(beforeVolley.Elapsed + .25f));
        Assert.That(afterVolley.Weapons.Single(w => w.WeaponId == ids[0]).Fired, Is.EqualTo(1));
        Assert.That(afterVolley.Weapons.Single(w => w.WeaponId == ids[0]).Pending, Is.Zero);
        Assert.That(JsonUtility.ToJson(inFlight), Is.EqualTo(inFlightJson), "Reconciliation must not change an earlier Pending count.");
        Assert.That(afterVolley.Weapons.Single(w => w.WeaponId == ids[0]).DirectionSectors.Sum(), Is.EqualTo(1));
        string afterVolleyJson = JsonUtility.ToJson(afterVolley);
        var mutable = lab.Telemetry.Snapshot();
        Assert.That(JsonUtility.ToJson(mutable), Is.EqualTo(afterVolleyJson), "Copy must preserve every serialized field and row order.");
        mutable.Scenario = "caller mutation"; mutable.Build = "caller mutation";
        mutable.Elapsed = -1f; mutable.DamageReceived = -1f; mutable.DistanceTravelled = -1f;
        mutable.MaximumCenterOffset = -1f; mutable.OrbitSlowSeconds = -1f;
        mutable.SurvivalApplicable = !mutable.SurvivalApplicable;
        mutable.ConfigurationChanged = !mutable.ConfigurationChanged; mutable.PlayerImpacts = -1;
        foreach (var row in mutable.Weapons)
        {
            row.WeaponId = -1; row.Name = "caller mutation"; row.Modules = "caller mutation";
            row.Fired = row.HitShots = row.Misses = row.Cancelled = row.Pending = -1;
            row.BodyHits = row.ShieldBlocks = row.Kills = -1; row.Damage = -1f;
            row.InitialRingId = row.InitialRingOrder = row.InitialRingDirection = -1;
            row.InitialLocalPhase = row.InitialRingRadius = row.InitialRingRotationSpeed = -1f;
            row.DirectionSectors[0] = -1;
            row.DirectionSectors = new[] { -1 };
        }
        mutable.Weapons[0] = null;
        mutable.Weapons = Array.Empty<EnemyOrbitalLabTelemetry.WeaponRow>();
        Assert.That(JsonUtility.ToJson(lab.Telemetry.Snapshot()), Is.EqualTo(afterVolleyJson), "Caller mutations must not reach live telemetry.");
        Assert.That(JsonUtility.ToJson(afterVolley), Is.EqualTo(afterVolleyJson), "Snapshots must also be independent of one another.");
        Ready(guns[1]); guns[1].Tick(.001f); adapter.Tick(1f);
        Ready(guns[3]); guns[3].Tick(.001f); adapter.DebugClearProjectiles();
        Ready(guns[0]); guns[0].Tick(.001f); adapter.Tick(1f);
        var report = Read(); var first = report.Weapons.Single(w => w.WeaponId == ids[0]);
        Assert.That(JsonUtility.ToJson(beforeVolley), Is.EqualTo(beforeVolleyJson));
        Assert.That(JsonUtility.ToJson(afterVolley), Is.EqualTo(afterVolleyJson), "Later hits, misses, clear and pool reuse must not rewrite a saved snapshot.");
        Assert.That(first.Fired, Is.EqualTo(2)); Assert.That(first.HitShots, Is.EqualTo(2));
        Assert.That(first.BodyHits, Is.EqualTo(3)); Assert.That(first.Damage, Is.EqualTo(21f)); Assert.That(first.Kills, Is.EqualTo(1));
        Assert.That(report.Weapons.Single(w => w.WeaponId == ids[1]).Misses, Is.EqualTo(1));
        Assert.That(report.Weapons.Single(w => w.WeaponId == ids[3]).Cancelled, Is.EqualTo(1));
        foreach (var row in report.Weapons) Assert.That(row.Fired, Is.EqualTo(row.HitShots + row.Misses + row.Cancelled + row.Pending));
        Assert.That(report.SurvivalApplicable, Is.False);
        bool originalInvincible = lab.PlayerInvincible;
        bool originalDebugInvulnerable = lab.Player.DebugInvulnerable;
        float originalDamageMultiplier = lab.Player.IncomingDamageMultiplier;
        void RestoreVulnerability()
        {
            lab.SetPlayerInvincible(false);
            lab.Player.DebugInvulnerable = false;
            lab.Player.SetIncomingDamageMultiplier(1f);
        }
        try
        {
            Call("ApplyTacticalPreset", "EVEN");
            var enableInvulnerability = new Action[]
            {
                () => { lab.SetPlayerInvincible(true); lab.Player.SetIncomingDamageMultiplier(1f); },
                () => lab.Player.DebugInvulnerable = true,
                () => lab.Player.SetIncomingDamageMultiplier(0f)
            };
            var reasons = new[] { "PlayerInvincible", "DebugInvulnerable", "IncomingDamageMultiplier" };
            Assert.That(lab.Paused, Is.True);
            for (int i = 0; i < enableInvulnerability.Length; i++)
            {
                RestoreVulnerability();
                Call("BeginCombatTelemetry", reasons[i]);
                Assert.That(lab.Telemetry.Snapshot().SurvivalApplicable, Is.True, "Fresh vulnerable recording: " + reasons[i]);
                enableInvulnerability[i]();
                var pausedReport = lab.Telemetry.Snapshot();
                Assert.That(pausedReport.SurvivalApplicable, Is.False, "Snapshot without Tick: " + reasons[i]);
                Assert.That(pausedReport.ConfigurationChanged, Is.False);
                Assert.That(lab.TacticalPreset, Is.EqualTo("EVEN"));
                RestoreVulnerability();
                Assert.That(lab.Telemetry.Snapshot().SurvivalApplicable, Is.False, "Inapplicability is irreversible: " + reasons[i]);
                lab.StopCombatTelemetry();
            }

            RestoreVulnerability();
            Call("BeginCombatTelemetry", "Stop without Snapshot");
            lab.Player.DebugInvulnerable = true;
            lab.StopCombatTelemetry();
            Assert.That(lab.Telemetry.Snapshot().SurvivalApplicable, Is.False, "Stop must reconcile without a prior Snapshot or Tick.");
            RestoreVulnerability();
            Assert.That(lab.Telemetry.Snapshot().SurvivalApplicable, Is.False);

            Call("BeginCombatTelemetry", "Completed vulnerable recording");
            lab.StopCombatTelemetry();
            var completed = lab.Telemetry.Snapshot();
            Assert.That(completed.SurvivalApplicable, Is.True, "A new vulnerable recording resets applicability.");
            foreach (var enable in enableInvulnerability)
            {
                enable();
                Assert.That(lab.Telemetry.Snapshot().SurvivalApplicable, Is.True, "Completed report must ignore later invulnerability.");
                Assert.That(completed.SurvivalApplicable, Is.True);
                RestoreVulnerability();
            }
        }
        finally
        {
            lab.StopCombatTelemetry();
            lab.SetPlayerInvincible(originalInvincible);
            lab.Player.DebugInvulnerable = originalDebugInvulnerable;
            lab.Player.SetIncomingDamageMultiplier(originalDamageMultiplier);
        }
        yield return null;
    }
    [Serializable] public sealed class PathSample
    {
        public float Seconds, TrackingError, HitKnockbackTimeRemaining;
        public Vector2 Position, Target, LinearVelocity, MovementIntent, CurrentVelocity, HitKnockbackVelocity;
        public int PlayerImpacts;
    }
    [Serializable] public sealed class RouteDivergence
    {
        public string Baseline, Current, Criterion;
        public int SampleIndex;
        public float PositionDifference, TargetDifference, BaselinePathLength, CurrentPathLength;
        public PathSample BaselineInitial, CurrentInitial, BaselineSample, CurrentSample;
    }
    [Serializable] public sealed class MountAngle { public int RingId, MountIndex; public float LocalPhase; }
    [Serializable] public sealed class ShiftSample
    {
        public int Frame;
        public float Seconds, WorldSeconds, DeltaTime, TimeScale;
        public bool Held, Slowing;
        public float[] Phases, Multipliers;
    }
    [Serializable] public sealed class MovementCell
    {
        public bool SeedIndependenceEstablished;
        public string SeedLimitation;
        public string Build, Motion, ShiftPolicy, Configuration, FinalConfiguration, Encounter;
        public int Scenario, Seed;
        public RigidbodyConstraints2D InitialBodyConstraints, FinalBodyConstraints;
        public float PathLength, MaximumError, ClosureError, WalkingSpeed, FixedDeltaTime;
        public Vector2 Start;
        public float[] Cooldowns, Radii, InitialPhases, BaseSpeeds;
        public int[] Directions, CooldownWeaponIds;
        public string[] Enemies, WeaponSettings, EnemyComposition;
        public bool Validated;
        public PathSample[] Trajectory;
        public MountAngle[] InitialMountAngles;
        public float SignedRouteDegrees;
        public ShiftSample[] ShiftTimeline;
        public EnemyOrbitalLabTelemetry.Report Telemetry;
        public string ModulePolicy, AllocatedModule;
        public ModuleEvidence[] ModuleDistribution;
        public int[] VolleyWeaponIds, Volleys;
    }
    [Serializable] public sealed class ModuleEvidence
    {
        public int WeaponId, RingId, RingOrder, BodyPierces;
        public string[] Modules;
        public bool HasExtraShot;
        public float ExtraShotAngle, FireInterval, BurstInterval;
    }
    [UnityTest] public IEnumerator CompareThreeBuildsInThreeExistingScenarios()
    {
        // New controlled variable: route direction, not nominal Unity seed variation.
        yield return MeasureMovementCells(false);
    }
    [UnityTest] public IEnumerator TwoArmedRingsCompareCoAndCounterRotationWhileMoving()
    {
        string output = Path.GetFullPath(Path.Combine(Application.dataPath,
            "../Artifacts/GeneratedQA/OrbitalTactics", "shotgun-multishot-inner-outer-melee-moving-" + DateTime.UtcNow.ToString("yyyyMMddTHHmmssfff") + "-" + Guid.NewGuid().ToString("N")));
        Directory.CreateDirectory(output);
        bool completed = false;
        try
        {
            lab.SetPaused(true);
            Assert.That(lab.FixedDirection, Is.True, "This experiment requires FIXED outward firing");
            var clockProperty = typeof(OrbitalModuleRuntime).GetProperty("RuntimeCooldown", Flags);
            var observedClocks = station.Modules.Select(g => (float)clockProperty.GetValue(g)).ToArray();
            Assert.That(observedClocks.Length, Is.EqualTo(4));
            // Use only the existing sandbox installation and validated layout actions.
            foreach (var gun in station.Modules.ToArray())
            {
                lab.SelectSandboxWeapon(gun.StableModuleId);
                Assert.That(lab.RemoveSandboxWeapon(), Is.True);
            }
            while (station.Rings.Count > 2)
            {
                lab.SelectSandboxRing(station.Rings.Count - 1);
                Assert.That(lab.ChangeSandboxRings(false), Is.True);
            }
            while (station.Rings.Count < 2) Assert.That(lab.ChangeSandboxRings(true), Is.True);
            for (int i = 0; i < 2; i++)
            {
                lab.SelectSandboxRing(i);
                Assert.That(lab.InstallSandboxWeapon(1), Is.True, "First shotgun: " + lab.SandboxMessage);
                Assert.That(lab.InstallSandboxWeapon(1), Is.True, "Second shotgun: " + lab.SandboxMessage);
                Assert.That(lab.Placement.ApplyRingLayout(station.Rings[i], "EVEN"), Is.True);
            }
            foreach (var gun in station.Modules)
                for (int slot = 0; slot < lab.WeaponModules.SlotCount(gun.StableModuleId); slot++)
                    Assert.That(lab.WeaponModules.SetModule(gun.StableModuleId, slot, "EMPTY"), Is.True);
            Assert.That(station.Modules.Count, Is.EqualTo(4));
            for (int i = 0; i < station.Modules.Count; i++)
                clockProperty.SetValue(station.Modules[i], observedClocks[i]);
            Assert.That(station.Rings.All(r => r.State.SpeedUpgradeLevel == 0), Is.True);
            Assert.That(station.Rings[0].Radius, Is.Not.EqualTo(station.Rings[1].Radius));
            yield return MeasureMovementCells(false, true, output, scenario: 1, motionModes: new[] { "LOOP" }, allocatedMultishot: true, builds: new[] { "CO" }, expectedWeaponKind: OrbitalModuleKind.Shotgun);
            completed = true;
        }
        finally
        {
            lab.SetPaused(true); lab.StopCombatTelemetry(); lab.Placement.Cancel();
            File.WriteAllText(Path.Combine(output, "protocol.txt"),
                "Experimental shotgun-multishot-inner-outer-melee-moving; two armed rings; melee; scenario=1; completed=" + completed + "\n" +
                "INNER/OUTER two MULTISHOT, CO only, same-sign LOOP, two cells of 12 seconds: INNER-CO, OUTER-CO.\n" +
                "Fired counts actual spawned projectiles (LabProjectileSpawned), not volleys.\n" +
                "LOOP retains knockback (FreezeRotation). No STATIC, COUNTER, mixed or shield cells. Radii, speeds and observed initial clocks are preserved.\n" +
                "Four real shotguns, seven base pellets each. Twelve authored pursuers, without shooters or shields. Exactly two MULTISHOT, no RAPID/PIERCE. Only selected guns add one ray: Fired = volleys * (7 + extra ray).\n" +
                "Compare INNER/OUTER per gun for HitShots, BodyHits, Damage and Misses with the seven-pellet baseline; aggregate rows do not isolate extra-ray hits. ShieldBlocks must remain zero.\n" +
                "If comparability fails, retain FAIL diagnostics without a comparative effectiveness conclusion.\n" +
                "No allocation advantage is required; this bot does not prove conscious human blind-zone compensation or stable causal placement effects.\n" +
                "Unity seed does not control instance-ID steering or private weapon System.Random.\n" +
                "Preparation/current state: " + lab.SandboxMessage + "\n" +
                string.Join(";", station.Rings.Select(r => FormattableString.Invariant($"{r.RingId}:{r.Radius:R}:{r.State.BaseRotationSpeed:R}:{r.Direction}"))) + "\n" + lab.CombatTelemetryJson());
            TestContext.WriteLine("Experimental shotgun MULTISHOT INNER/OUTER moving melee scenario=1 evidence: " + output);
        }
    }
    // Shared measurement protocol. The two-ring branch never runs the blocked reflected-route comparison.
    IEnumerator MeasureMovementCells(bool shiftPolicies, bool twoRings = false, string evidenceOutput = null,
        int scenario = 2, string[] motionModes = null, bool allocatedMultishot = false, string[] builds = null, string allocatedModule = "MULTISHOT",
        OrbitalModuleKind expectedWeaponKind = OrbitalModuleKind.Pistol)
    {
        Assert.That(allocatedModule, Is.EqualTo("MULTISHOT").Or.EqualTo("PIERCE").Or.EqualTo("RAPID"));
        const int seed = 42421028;
        const string seedLimitation = "Unity seed does not control sandbox spawn geometry, instance-ID crowd steering or the weapon's private System.Random. Not independent random conditions.";
        const float duration = 12f, radius = 3f, omega = 2f * Mathf.PI / 6f;
        string output = evidenceOutput ?? Path.GetFullPath(Path.Combine(Application.dataPath,
            "../Artifacts/GeneratedQA/OrbitalTactics", (shiftPolicies ? "shift-" : "route-direction-") + DateTime.UtcNow.ToString("yyyyMMddTHHmmssfff") + "-" + Guid.NewGuid().ToString("N")));
        Directory.CreateDirectory(output);
        var csv = new List<string> { "seed,seedIndependenceEstablished,build,motion,scenario,seconds,id,fired,hitShots,misses,cancelled,pending,bodyHits,shieldBlocks,damage,kills,playerImpacts,damageReceived,pathLength,maxError,closureError,signedRouteDegrees,initialMountAngles" };
        if (shiftPolicies) csv[0] = csv[0].Replace("build,motion,", "build,shiftPolicy,");
        if (twoRings) csv[0] += ",initialBodyConstraints,finalBodyConstraints";
        if (scenario == 3 || scenario == 1 || scenario == 2) csv[0] += ",encounter";
        if (allocatedMultishot) csv[0] += ",allocatedModule,modulePolicy,ringId,ringOrder,modules,extraShotAngle,bodyPierces,actualProjectiles,volleys,fireInterval,burstInterval";
        var movement = lab.Player.GetComponent<CharacterMovement2D>();
        var body = lab.Player.GetComponent<Rigidbody2D>();
        var intent = movement.MovementIntent;
        bool movementEnabled = movement.enabled, paused = lab.Paused;
        var constraints = body.constraints;
        Vector2 originalPosition = body.position, originalVelocity = body.linearVelocity;
        float originalAngularVelocity = body.angularVelocity;
        bool debugHeld = station.DebugBulletTimeHeld, stationEnabled = station.enabled;
        bool needsRelease = (bool)lab.CombatV2.GetType().GetField("needsRelease", Flags).GetValue(lab.CombatV2);
        bool slowing = lab.CombatV2.IsSlowingOrbits;
        var originalMultipliers = station.Rings.Select(r => r.LabRotationMultiplier).ToArray();
        float capture = Time.captureDeltaTime, scale = Time.timeScale, fixedDelta = Time.fixedDeltaTime;
        var random = UnityEngine.Random.state;
        Vector2 anchor = body.position, center = anchor - Vector2.right * radius;
        var cooldown = typeof(OrbitalModuleRuntime).GetProperty("RuntimeCooldown", Flags);
        var clocks = station.Modules.OrderBy(g => g.CurrentMount.MountIndex).Select(g => (float)cooldown.GetValue(g)).ToArray();
        var clocksById = station.Modules.ToDictionary(g => g.StableModuleId, g => (float)cooldown.GetValue(g));
        var originalDirections = station.Rings.Select(r => r.State.Direction).ToArray();
        var originalModules = station.Modules.ToDictionary(g => g.StableModuleId,
            g => Enumerable.Range(0, lab.WeaponModules.SlotCount(g.StableModuleId)).Select(s => lab.WeaponModules.GetModule(g.StableModuleId, s)).ToArray());
        ModuleEvidence[] ModuleDistribution() => station.Modules.Select(g => new ModuleEvidence {
            WeaponId = g.StableModuleId, RingId = g.CurrentMount.Ring.RingId, RingOrder = g.CurrentMount.Ring.State.Order,
            BodyPierces = ((EnemyOrbitalLabWeapon)g).Settings.BodyPierces,
            FireInterval = ((EnemyOrbitalLabWeapon)g).Settings.FireInterval, BurstInterval = ((EnemyOrbitalLabWeapon)g).Settings.BurstInterval,
            Modules = Enumerable.Range(0, lab.WeaponModules.SlotCount(g.StableModuleId)).Select(s => lab.WeaponModules.GetModule(g.StableModuleId, s)).ToArray(),
            HasExtraShot = lab.WeaponModules.ExtraShotAngle(g.StableModuleId).HasValue,
            ExtraShotAngle = lab.WeaponModules.ExtraShotAngle(g.StableModuleId) ?? 0f }).ToArray();
        string moduleIndependentConfiguration = null;
        var moduleRingIds = station.Modules.ToDictionary(g => g.StableModuleId, g => g.CurrentMount.Ring.RingId);
        int? totalVolleysBaseline = null, totalProjectilesBaseline = null;
        var starts = new Dictionary<string, string[]>();
        var configurations = new Dictionary<string, string>();
        var paths = new Dictionary<string, PathSample[]>();
        var routeCells = new Dictionary<string, MovementCell>();
        var pathLengths = new Dictionary<string, float>();
        // Read only: do not normalize/reset movement state to make routes comparable.
        var knockbackVelocity = typeof(CharacterMovement2D).GetField("hitKnockbackVelocity", Flags);
        var knockbackRemaining = typeof(CharacterMovement2D).GetField("hitKnockbackTimeRemaining", Flags);
        var currentVelocity = typeof(CharacterMovement2D).GetField("currentVelocity", Flags);
        var telemetryReport = lab.Telemetry.GetType().GetField("report", Flags);
        Assert.That(knockbackVelocity, Is.Not.Null);
        Assert.That(knockbackRemaining, Is.Not.Null);
        Assert.That(currentVelocity, Is.Not.Null);
        Assert.That(telemetryReport, Is.Not.Null);
        PathSample CapturePath(float seconds, Vector2 target) => new PathSample {
            Seconds = seconds, Position = body.position, Target = target, TrackingError = Vector2.Distance(body.position, target),
            LinearVelocity = body.linearVelocity, MovementIntent = movement.MovementIntent?.Invoke() ?? Vector2.zero,
            CurrentVelocity = (Vector2)currentVelocity.GetValue(movement),
            HitKnockbackVelocity = (Vector2)knockbackVelocity.GetValue(movement),
            HitKnockbackTimeRemaining = (float)knockbackRemaining.GetValue(movement),
            PlayerImpacts = ((EnemyOrbitalLabTelemetry.Report)telemetryReport.GetValue(lab.Telemetry)).PlayerImpacts };
        var presetAngles = new Dictionary<string, MountAngle[]>();
        MountAngle[] Angles() => station.Rings.SelectMany(r => r.Mounts.Select(m => new MountAngle
            { RingId = r.RingId, MountIndex = m.MountIndex, LocalPhase = m.LocalPhase })).ToArray();
        var setAngle = lab.Placement.GetType().GetMethod("SetAngle", Flags);
        Assert.That(setAngle, Is.Not.Null, "Missing placement angle owner action");
        void RestoreAngles(MountAngle[] angles)
        {
            foreach (var angle in angles)
            {
                var mount = station.Rings.Single(r => r.RingId == angle.RingId).Mounts.Single(m => m.MountIndex == angle.MountIndex);
                // Restore the owner's remembered angle as well as the live mount.
                setAngle.Invoke(lab.Placement, new object[] { mount, angle.LocalPhase });
            }
            lab.Placement.Synchronize();
            foreach (var angle in angles)
                Assert.That(station.Rings.Single(r => r.RingId == angle.RingId).Mounts.Single(m => m.MountIndex == angle.MountIndex).LocalPhase,
                    Is.EqualTo(angle.LocalPhase), "Exact mount angle restoration after Synchronize");
            foreach (var ring in station.Rings) ring.Tick(0f);
            Physics2D.SyncTransforms();
            string restored = (string)lab.Telemetry.GetType().GetMethod("Configuration", Flags).Invoke(lab.Telemetry, null);
            lab.Placement.Synchronize();
            Assert.That((string)lab.Telemetry.GetType().GetMethod("Configuration", Flags).Invoke(lab.Telemetry, null),
                Is.EqualTo(restored), "Repeated Synchronize must preserve restored configuration");
        }
        var originalAngles = Angles();
        var originalPhases = station.Rings.Select(r => r.Phase).ToArray();
        var originalPreset = lab.GetType().GetField("tacticalPreset", Flags).GetValue(lab);
        var originalTacticalConfiguration = lab.GetType().GetField("tacticalConfiguration", Flags).GetValue(lab);
        int[] firedBaseline = null;
        int[] weaponIds = null;
        string Configuration() => (string)lab.Telemetry.GetType().GetMethod("Configuration", Flags).Invoke(lab.Telemetry, null)
            + "|rings=" + string.Join(";", station.Rings.Select(r => FormattableString.Invariant($"{r.Radius:R}:{r.Direction}:{r.RotationSpeed:R}")))
            + (shiftPolicies ? "" : "|multipliers=" + string.Join(";", station.Rings.Select(r => r.LabRotationMultiplier.ToString("R", System.Globalization.CultureInfo.InvariantCulture))))
            + (twoRings ? "|structure=" + TwoRingInvariant() : "");
        string TwoRingInvariant(bool includeModules = true) => string.Join(";", station.Modules.Select(g =>
            FormattableString.Invariant($"{g.StableModuleId}:{g.CurrentMount.Ring.RingId}:{g.CurrentMount.MountIndex}:{g.CurrentMount.LocalPhase:R}:{g.Kind}:") +
            (includeModules ? string.Join(",", Enumerable.Range(0, lab.WeaponModules.SlotCount(g.StableModuleId)).Select(slot => lab.WeaponModules.GetModule(g.StableModuleId, slot))) : "") +
            (includeModules ? ":extra=" + lab.WeaponModules.ExtraShotAngle(g.StableModuleId)?.ToString("R", System.Globalization.CultureInfo.InvariantCulture) : ""))) +
            "|rings=" + string.Join(";", station.Rings.Select((r, i) => FormattableString.Invariant(
                $"{r.RingId}:{r.Radius:R}:{r.State.BaseRotationSpeed:R}:{r.RotationSpeed:R}:{r.RuntimeDirectionMultiplier}:{r.LabRotationMultiplier:R}:{(i == 1 ? 1 : r.Direction)}") + ":mounts=" + string.Join(",", r.Mounts.Select(m => FormattableString.Invariant($"{m.MountIndex}:{m.LocalPhase:R}:{m.Occupied}")))));
        Vector2 Target(float seconds, float sign) => center + radius * new Vector2(Mathf.Cos(omega * seconds), sign * Mathf.Sin(omega * seconds));
        try
        {
            Time.captureDeltaTime = .02f; Time.fixedDeltaTime = .02f;
            station.enabled = true;
            // Derive each layout once at the common pose, before any combat cell.
            lab.SetPaused(true);
            body.position = anchor; lab.Player.transform.position = anchor;
            foreach (string build in builds ?? (twoRings ? new[] { "CO", "COUNTER" } : new[] { "EVEN", "GROUPED" }))
            {
                foreach (var ring in station.Rings) { ring.State.CurrentPhase = 0f; ring.Tick(0f); }
                Physics2D.SyncTransforms();
                if (!twoRings) Call("ApplyTacticalPreset", build);
                presetAngles.Add(build, Angles());
            }
            foreach (string modulePolicy in allocatedMultishot ? new[] { "INNER", "OUTER" } : new[] { "UNCHANGED" })
            foreach (string build in builds ?? (twoRings ? new[] { "CO", "COUNTER" } : new[] { "EVEN", "GROUPED" }))
            foreach (string motion in motionModes ?? (twoRings ? new[] { "STATIC", "LOOP" } : shiftPolicies ? new[] { "OFF", "HOLD", "PULSE" } : new[] { "LOOP_CW", "LOOP_CCW" }))
            {
                string cellName = (allocatedMultishot ? modulePolicy + "-" : "") + build;
                float routeSign = motion == "LOOP_CW" ? -1f : 1f;
                lab.SetPaused(true); lab.ClearSandboxArena();
                if (shiftPolicies) { station.DebugBulletTimeHeld = false; lab.CombatV2.Suspend(); }
                // Reset the real controller's velocity/dash/knockback between cells.
                movement.enabled = false; movement.enabled = true;
                lab.Player.StopAllCoroutines();
                typeof(PlayerHealth).GetField("isInvulnerable", Flags).SetValue(lab.Player, false);
                lab.Player.SetRuntimeHealth(lab.Player.MaxHealth, lab.Player.MaxHealth);
                var expectedConstraints = twoRings && motion == "STATIC"
                    ? RigidbodyConstraints2D.FreezeAll : RigidbodyConstraints2D.FreezeRotation;
                body.constraints = expectedConstraints;
                var initialBodyConstraints = body.constraints;
                Assert.That(initialBodyConstraints, Is.EqualTo(expectedConstraints), "Body constraints must match the motion protocol");
                body.position = anchor; body.linearVelocity = Vector2.zero; lab.Player.transform.position = anchor;
                foreach (var ring in station.Rings) { ring.State.CurrentPhase = 0f; ring.Tick(0f); }
                Physics2D.SyncTransforms();
                RestoreAngles(presetAngles[build]);
                if (allocatedMultishot)
                {
                    foreach (var gun in station.Modules)
                    {
                        Assert.That(originalModules.ContainsKey(gun.StableModuleId), Is.True, "Unexpected weapon ID");
                        Assert.That(lab.WeaponModules.SlotCount(gun.StableModuleId), Is.EqualTo(originalModules[gun.StableModuleId].Length));
                        Assert.That(gun.CurrentMount.Ring.RingId, Is.EqualTo(moduleRingIds[gun.StableModuleId]), "Weapon moved between rings");
                        bool selected = moduleRingIds[gun.StableModuleId] == station.Rings[modulePolicy == "INNER" ? 0 : 1].RingId;
                        for (int slot = 0; slot < originalModules[gun.StableModuleId].Length; slot++)
                        {
                            string expected = selected && slot == 0 ? allocatedModule : "EMPTY";
                            Assert.That(lab.WeaponModules.SetModule(gun.StableModuleId, slot, expected), Is.True);
                            Assert.That(lab.WeaponModules.GetModule(gun.StableModuleId, slot), Is.EqualTo(expected), "Module address: " + gun.StableModuleId + ":" + slot);
                        }
                        float? expectedAngle = selected && allocatedModule == "MULTISHOT" ? Mathf.Clamp(lab.ModuleMultishotAngle, 1f, 30f) : (float?)null;
                        Assert.That(lab.WeaponModules.ExtraShotAngle(gun.StableModuleId), Is.EqualTo(expectedAngle), "Extra shot address: " + gun.StableModuleId);
                        var weapon = (EnemyOrbitalLabWeapon)gun;
                        int expectedPierces = weapon.SourceProfile.BodyPierces
                            + (selected && allocatedModule == "PIERCE" ? Mathf.Clamp(lab.ModulePierceBonus, 0, 10) : 0);
                        Assert.That(weapon.Settings.BodyPierces, Is.EqualTo(expectedPierces), "Actual body pierces: " + gun.StableModuleId);
                        float rapid = selected && allocatedModule == "RAPID" ? Mathf.Clamp(lab.ModuleRapidMultiplier, 1f, 5f) : 1f;
                        Assert.That(weapon.Settings.FireInterval, Is.EqualTo(weapon.SourceProfile.FireInterval / rapid).Within(.00001f), "Actual fire interval: " + gun.StableModuleId);
                        Assert.That(weapon.Settings.BurstInterval, Is.EqualTo(weapon.SourceProfile.BurstInterval / rapid).Within(.00001f), "Actual burst interval: " + gun.StableModuleId);
                        if (selected && allocatedModule == "RAPID")
                            Assert.That(weapon.Settings.FireInterval, Is.LessThan(weapon.SourceProfile.FireInterval), "RAPID must actually change cadence");
                        if (selected && allocatedModule == "PIERCE")
                            Assert.That(weapon.Settings.BodyPierces, Is.GreaterThan(weapon.SourceProfile.BodyPierces), "PIERCE must actually change penetration");
                    }
                    Assert.That(station.Modules.Count(g => g.CurrentMount.Ring == station.Rings[1]), Is.EqualTo(2));
                    Assert.That(ModuleDistribution().Count(m => m.HasExtraShot), Is.EqualTo(allocatedModule == "MULTISHOT" ? 2 : 0), "Additional rays must match the module");
                    Assert.That(ModuleDistribution().Sum(m => m.Modules.Count(value => value == allocatedModule)), Is.EqualTo(2), "Exactly two allocated modules");
                    if (allocatedModule == "RAPID") Assert.That(ModuleDistribution().Sum(m => m.Modules.Count(value => value == "PIERCE" || value == "MULTISHOT")), Is.Zero, "No PIERCE/MULTISHOT in the RAPID experiment");
                    if (allocatedModule == "PIERCE") Assert.That(ModuleDistribution().Sum(m => m.Modules.Count(value => value == "MULTISHOT")), Is.Zero, "No MULTISHOT in the PIERCE experiment");
                }
                if (twoRings)
                {
                    Assert.That(station.Rings.Count, Is.EqualTo(2));
                    for (int i = 0; i < 2; i++)
                    {
                        lab.SelectSandboxRing(i);
                        lab.SetSandboxRingDirection(i == 1 && build == "COUNTER" ? -1 : 1);
                        var guns = station.Rings[i].Mounts.Where(m => m.Occupied).ToArray();
                        Assert.That(guns.Length, Is.EqualTo(2), "Both rings must be armed");
                        Assert.That(Mathf.Abs(Mathf.DeltaAngle(guns[0].LocalPhase, guns[1].LocalPhase)), Is.EqualTo(180f));
                        Assert.That(guns.All(m => m.Module.Kind == expectedWeaponKind), Is.True, "Exact weapon kind: " + expectedWeaponKind);
                        if (expectedWeaponKind == OrbitalModuleKind.Shotgun)
                            foreach (var mount in guns)
                            {
                                var shotgun = mount.Module as EnemyOrbitalLabWeapon;
                                Assert.That(shotgun, Is.Not.Null, "A real lab shotgun is required");
                                Assert.That(shotgun.Settings.Pellets, Is.EqualTo(7), "Seven base pellets before the separate MULTISHOT ray");
                            }
                        Assert.That(station.Rings[i].Direction, Is.EqualTo(i == 1 && build == "COUNTER" ? -1 : 1));
                    }
                }
                // Restore attribution too: no repeated envelope-dependent layout calculation.
                lab.GetType().GetField("tacticalPreset", Flags).SetValue(lab, build);
                lab.GetType().GetField("tacticalConfiguration", Flags).SetValue(lab,
                    lab.Telemetry.GetType().GetMethod("Configuration", Flags).Invoke(lab.Telemetry, null));
                foreach (var gun in station.Modules) cooldown.SetValue(gun, twoRings ? clocksById[gun.StableModuleId] : clocks[gun.CurrentMount.MountIndex]);
                float[] phases = station.Rings.Select(r => r.Phase).ToArray();
                Assert.That(phases.All(p => p == 0f), Is.True);
                CollectionAssert.AreEqual(clocks, station.Modules.OrderBy(g => g.CurrentMount.MountIndex).Select(g => (float)cooldown.GetValue(g)).ToArray());
                string[] settings = station.Modules.OfType<EnemyOrbitalLabWeapon>().Select(g => g.Kind + ":" + JsonUtility.ToJson(g.Settings)).ToArray();
                string initialModules = allocatedMultishot ? JsonUtility.ToJson(new MovementCell { ModuleDistribution = ModuleDistribution() }) : null;
                string configuration = Configuration() + "|settings=" + string.Join(";", settings);
                string configurationKey = twoRings ? "two-ring:" + modulePolicy : seed + ":" + build;
                string comparableConfiguration = twoRings ? TwoRingInvariant() + "|settings=" + string.Join(";", settings) : configuration;
                if (allocatedMultishot)
                {
                    // Normalize only the separately asserted module effect, on a copy.
                    // All other profile fields remain in the strict cross-policy contract.
                    string[] moduleIndependentSettings = station.Modules.OfType<EnemyOrbitalLabWeapon>().Select(g => {
                        var copy = JsonUtility.FromJson<OrbitalWeaponSettings>(JsonUtility.ToJson(g.Settings));
                        if (allocatedModule == "PIERCE") copy.BodyPierces = g.SourceProfile.BodyPierces;
                        if (allocatedModule == "RAPID")
                        {
                            copy.FireInterval = g.SourceProfile.FireInterval;
                            copy.BurstInterval = g.SourceProfile.BurstInterval;
                        }
                        return g.Kind + ":" + JsonUtility.ToJson(copy);
                    }).ToArray();
                    string invariant = TwoRingInvariant(false) + "|settings=" + string.Join(";", moduleIndependentSettings);
                    if (moduleIndependentConfiguration == null) moduleIndependentConfiguration = invariant;
                    else Assert.That(invariant, Is.EqualTo(moduleIndependentConfiguration), "Unexpected cross-policy change beyond the asserted module effect");
                }
                if (twoRings) File.WriteAllText(Path.Combine(output, cellName + "-" + motion + "-configuration.txt"),
                    "scenario=" + scenario + (scenario == 3 ? "; shield" : scenario == 1 ? "; melee" : scenario == 2 ? "; mixed" : "") + "\nActual body constraints: " + body.constraints + "\nActual complete configuration:\n" + configuration + "\nComparison (only second Direction excluded):\n" + comparableConfiguration);
                if (configurations.TryGetValue(configurationKey, out var prior)) Assert.That(comparableConfiguration, Is.EqualTo(prior));
                else configurations.Add(configurationKey, comparableConfiguration);
                if (twoRings)
                    File.WriteAllText(Path.Combine(output, cellName + "-" + motion + "-start.json"), JsonUtility.ToJson(new MovementCell {
                        AllocatedModule = allocatedMultishot ? allocatedModule : null, ModulePolicy = modulePolicy, ModuleDistribution = ModuleDistribution(),
                        Build = build, Motion = motion, Scenario = scenario, Seed = seed, Encounter = scenario == 3 ? "shield" : scenario == 1 ? "melee" : scenario == 2 ? "mixed" : null,
                        Configuration = configuration, InitialMountAngles = Angles(),
                        InitialBodyConstraints = initialBodyConstraints, FinalBodyConstraints = body.constraints,
                        Cooldowns = clocks.ToArray(), CooldownWeaponIds = station.Modules.OrderBy(g => g.CurrentMount.MountIndex).Select(g => g.StableModuleId).ToArray(),
                        Radii = station.Rings.Select(r => r.Radius).ToArray(), BaseSpeeds = station.Rings.Select(r => r.State.BaseRotationSpeed).ToArray(),
                        Directions = station.Rings.Select(r => r.Direction).ToArray(), InitialPhases = phases, WeaponSettings = settings }, true));
                UnityEngine.Random.InitState(seed);
                lab.StartSandboxEncounter(scenario); lab.SetSandboxSpawning(false);
                var spawned = ((IEnumerable)lab.GetType().GetField("enemies", Flags).GetValue(lab)).Cast<EnemyHealth>().ToArray();
                string[] enemyComposition = null;
                if (scenario == 2)
                {
                    // Match StartSandboxEncounter(2): indices 0/4/8 use the first authored shooter.
                    int shooterIndex = Array.FindIndex(lab.EnemyPrefabs,
                        p => p != null && p.GetComponent<EnemyShooterMovement>() != null);
                    var shooterPrefab = shooterIndex < 0 ? null : lab.EnemyPrefabs[shooterIndex];
                    enemyComposition = spawned.Select((e, i) => e == null ? "null" :
                        FormattableString.Invariant($"index={i}:{e.name}:alive={!e.IsDead}:active={e.gameObject.activeInHierarchy}:") +
                        "components=" + string.Join(";", e.GetComponents<Component>().Select(c => c == null ? "missing" : c.GetType().FullName)) +
                        ":shooter=" + (e.GetComponentInChildren<EnemyShooterMovement>(true) != null) +
                        ":chase=" + (e.GetComponentInChildren<EnemyChaseMovement>(true) != null) +
                        ":shield=" + (e.GetComponentInChildren<EnemyDirectionalShield>(true) != null)).ToArray();
                    File.WriteAllText(Path.Combine(output, cellName + "-" + motion + "-mixed-scenario-2-composition.txt"),
                        "mixed; scenario=2; shooterIndex=" + shooterIndex + "; shooterSource=" + AssetDatabase.GetAssetPath(shooterPrefab) +
                        "; pursuerSource=" + AssetDatabase.GetAssetPath(lab.PursuerPrefab) +
                        "; Application.isFocused=" + Application.isFocused + "\n" + string.Join("\n", enemyComposition));
                    Assert.That(shooterPrefab, Is.Not.Null, "Do not accept the runtime fallback as a real shooter");
                    Assert.That(lab.PursuerPrefab, Is.Not.Null);
                    Assert.That(lab.PursuerPrefab.GetComponent<EnemyChaseMovement>(), Is.Not.Null);
                    Assert.That(spawned.Length, Is.EqualTo(12), "scenario=2 must contain twelve enemies before movement");
                    Assert.That(lab.AliveEnemies, Is.EqualTo(12));
                    Assert.That(spawned.Count(e => e != null && e.GetComponentInChildren<EnemyShooterMovement>(true) != null), Is.EqualTo(3));
                    Assert.That(spawned.Count(e => e != null && e.GetComponentInChildren<EnemyChaseMovement>(true) != null), Is.EqualTo(9));
                    for (int i = 0; i < spawned.Length; i++)
                    {
                        var enemy = spawned[i];
                        var prefab = i % 4 == 0 ? shooterPrefab : lab.PursuerPrefab;
                        Assert.That(enemy, Is.Not.Null);
                        Assert.That(enemy.IsDead, Is.False);
                        Assert.That(enemy.gameObject.activeInHierarchy, Is.True);
                        Assert.That(enemy.name, Is.EqualTo(prefab.name + " (Lab)"));
                        Assert.That(enemy.GetComponentInChildren<EnemyDirectionalShield>(true), Is.Null);
                        Assert.That(enemy.GetComponent<EnemyShooterMovement>() != null, Is.EqualTo(i % 4 == 0));
                        CollectionAssert.AreEquivalent(
                            prefab.GetComponents<Component>().Select(c => c.GetType().FullName),
                            enemy.GetComponents<Component>().Where(c => !(c is CombatFeelTestDummy)).Select(c => c.GetType().FullName),
                            "Root components must match the authored mixed encounter prefab at index " + i);
                    }
                }
                if (scenario == 1)
                {
                    // StartSandboxEncounter(1) -> SpawnSandboxEnemy -> SpawnAt(-3) -> PursuerPrefab.
                    // Save the actual composition before any setup assertion can reject the cell.
                    enemyComposition = spawned.Select(e => e == null ? "null" :
                        FormattableString.Invariant($"{e.name}:alive={!e.IsDead}:active={e.gameObject.activeInHierarchy}:") +
                        "components=" + string.Join(";", e.GetComponents<Component>().Select(c => c == null ? "missing" : c.GetType().FullName)) +
                        ":shooter=" + (e.GetComponentInChildren<EnemyShooterMovement>(true) != null) +
                        ":shield=" + (e.GetComponentInChildren<EnemyDirectionalShield>(true) != null)).ToArray();
                    File.WriteAllText(Path.Combine(output, cellName + "-" + motion + "-melee-scenario-1-composition.txt"),
                        "melee; scenario=1; SpawnAt type=-3; source=" + AssetDatabase.GetAssetPath(lab.PursuerPrefab) +
                        "; Application.isFocused=" + Application.isFocused + "\n" + string.Join("\n", enemyComposition));
                    Assert.That(lab.PursuerPrefab, Is.Not.Null);
                    Assert.That(lab.PursuerPrefab.GetComponent<EnemyChaseMovement>(), Is.Not.Null, "Authored pursuer must have real movement");
                    Assert.That(spawned.Length, Is.EqualTo(12), "scenario=1 must contain twelve pursuers before combat");
                    Assert.That(lab.AliveEnemies, Is.EqualTo(12));
                    foreach (var enemy in spawned)
                    {
                        Assert.That(enemy, Is.Not.Null);
                        Assert.That(enemy.IsDead, Is.False);
                        Assert.That(enemy.gameObject.activeInHierarchy, Is.True);
                        Assert.That(enemy.name, Is.EqualTo(lab.PursuerPrefab.name + " (Lab)"));
                        Assert.That(enemy.GetComponent<EnemyChaseMovement>(), Is.Not.Null);
                        Assert.That(enemy.GetComponentInChildren<EnemyShooterMovement>(true), Is.Null, "Pursuer must not be a shooter");
                        Assert.That(enemy.GetComponentInChildren<EnemyDirectionalShield>(true), Is.Null, "Pursuer must not carry a shield");
                        CollectionAssert.AreEquivalent(
                            lab.PursuerPrefab.GetComponents<Component>().Select(c => c.GetType().FullName),
                            enemy.GetComponents<Component>().Where(c => !(c is CombatFeelTestDummy)).Select(c => c.GetType().FullName),
                            "Root component types must match the SpawnAt(-3) prefab (apart from its lab marker)");
                    }
                }
                if (scenario == 3)
                {
                    // SpawnAt(-4) uses this authored prefab and configures a breakable shield.
                    // Persist actual components before assertions, so a rejected setup is diagnosable.
                    enemyComposition = spawned.Select(e =>
                    {
                        if (e == null) return "null";
                        var shield = e.GetComponent<EnemyDirectionalShield>();
                        return FormattableString.Invariant($"{e.name}:alive={!e.IsDead}:active={e.gameObject.activeInHierarchy}:") +
                            "components=" + string.Join(";", e.GetComponents<Component>().Select(c => c == null ? "missing" : c.GetType().FullName)) +
                            (shield == null ? ":shield=null" : FormattableString.Invariant(
                                $":shield={shield.Kind}:remaining={shield.RemainingBlockedHits}:maximum={shield.MaximumBlockedHits}:blocks={shield.Blocks}:collider={shield.ShieldCollider != null && shield.ShieldCollider.enabled}:forward={shield.Forward}"));
                    }).ToArray();
                    File.WriteAllText(Path.Combine(output, cellName + "-" + motion + "-shield-scenario-3-composition.txt"),
                        "shield; scenario=3; SpawnAt type=-4; source=" + AssetDatabase.GetAssetPath(lab.BreakableShieldBearerPrefab) +
                        "; Application.isFocused=" + Application.isFocused + "\n" + string.Join("\n", enemyComposition));
                    Assert.That(lab.BreakableShieldBearerPrefab, Is.Not.Null);
                    Assert.That(spawned.Length, Is.EqualTo(12), "scenario=3 must contain twelve shield bearers before combat");
                    Assert.That(lab.AliveEnemies, Is.EqualTo(12));
                    foreach (var enemy in spawned)
                    {
                        Assert.That(enemy, Is.Not.Null);
                        Assert.That(enemy.IsDead, Is.False);
                        Assert.That(enemy.gameObject.activeInHierarchy, Is.True);
                        Assert.That(enemy.name, Is.EqualTo(lab.BreakableShieldBearerPrefab.name + " (Lab)"));
                        Assert.That(enemy.GetComponent<EnemyShooterMovement>(), Is.Null, "Shield bearer must not be replaced by a shooter");
                        CollectionAssert.AreEquivalent(
                            lab.BreakableShieldBearerPrefab.GetComponents<Component>().Select(c => c.GetType().FullName),
                            enemy.GetComponents<Component>().Where(c => !(c is CombatFeelTestDummy)).Select(c => c.GetType().FullName),
                            "Root component types must match the SpawnAt(-4) prefab (apart from its lab marker)");
                        var shield = enemy.GetComponent<EnemyDirectionalShield>();
                        Assert.That(shield, Is.Not.Null);
                        Assert.That(shield.Kind, Is.EqualTo(EnemyDirectionalShield.ShieldKind.Breakable));
                        Assert.That(shield.MaximumBlockedHits, Is.EqualTo(Mathf.Max(1, lab.ShieldBlockedHits)));
                        Assert.That(shield.RemainingBlockedHits, Is.EqualTo(shield.MaximumBlockedHits));
                        Assert.That(shield.Blocks, Is.Zero);
                        Assert.That(shield.IsBroken, Is.False);
                        Assert.That(shield.ShieldCollider, Is.Not.Null);
                        Assert.That(shield.ShieldCollider.enabled, Is.True);
                    }
                }
                var enemies = spawned.Select(e => FormattableString.Invariant($"{e.name}:{e.transform.position.x:R}:{e.transform.position.y:R}")).ToArray();
                string startKey = seed + ":" + scenario;
                if (starts.TryGetValue(startKey, out var initial)) CollectionAssert.AreEqual(initial, enemies);
                else starts.Add(startKey, enemies);
                Assert.That(enemies.Length, Is.EqualTo(12));
                float startTime = Time.fixedTime;
                float worldStart = Time.time;
                movement.MovementIntent = () =>
                {
                    if (motion == "STATIC") return Vector2.zero;
                    float t = Mathf.Clamp(Time.fixedTime - startTime, 0f, duration);
                    Vector2 velocity = radius * omega * new Vector2(-Mathf.Sin(omega * t), routeSign * Mathf.Cos(omega * t));
                    return Vector2.ClampMagnitude((velocity + (Target(t, routeSign) - body.position) * 4f) / movement.WalkingSpeed, 1f);
                };
                Assert.That(movement.WalkingSpeed, Is.GreaterThan(radius * omega));
                var samples = new List<PathSample> { CapturePath(0f, anchor) };
                var initialVolleys = station.Modules.OfType<EnemyOrbitalLabWeapon>().ToDictionary(g => g.StableModuleId, g => g.ShotsFired);
                var shiftSamples = new List<ShiftSample>();
                float distance = 0f, error = 0f, signedDegrees = 0f;
                Physics2D.SyncTransforms(); lab.SetPaused(false);
                for (int frame = 0; frame < 600; frame++)
                {
                    bool held = motion == "HOLD" ? frame > 0 : motion == "PULSE" && (frame / 50) % 2 == 1;
                    if (shiftPolicies)
                    {
                        station.DebugBulletTimeHeld = held;
                        // The enabled station reads this through UpdateRightMouse before
                        // its normal TickStation. No forced eligibility or extra input tick.
                    }
                    yield return null;
                    if (twoRings) Assert.That(body.constraints, Is.EqualTo(expectedConstraints), "Body constraints changed at frame " + frame);
                    float t = Mathf.Clamp(Time.fixedTime - startTime, 0f, duration);
                    Vector2 point = body.position, target = motion == "STATIC" ? anchor : Target(t, routeSign);
                    Vector2 previousRadial = samples[samples.Count - 1].Position - center;
                    signedDegrees += Vector2.SignedAngle(previousRadial, point - center);
                    distance += Vector2.Distance(samples[samples.Count - 1].Position, point);
                    error = Mathf.Max(error, Vector2.Distance(point, target));
                    samples.Add(CapturePath(t, target));
                    if (shiftPolicies)
                        shiftSamples.Add(new ShiftSample { Frame = frame, Seconds = t, WorldSeconds = Time.time - worldStart,
                            DeltaTime = Time.deltaTime, TimeScale = Time.timeScale, Held = held,
                            Slowing = lab.CombatV2.IsSlowingOrbits,
                            Phases = station.Rings.Select(r => r.Phase).ToArray(),
                            Multipliers = station.Rings.Select(r => r.LabRotationMultiplier).ToArray() });
                }
                lab.SetPaused(true); lab.StopCombatTelemetry();
                var cell = new MovementCell { Build = build, Motion = shiftPolicies ? "LOOP" : motion,
                    AllocatedModule = allocatedMultishot ? allocatedModule : null, ModulePolicy = modulePolicy, ModuleDistribution = ModuleDistribution(),
                    VolleyWeaponIds = station.Modules.OfType<EnemyOrbitalLabWeapon>().Select(g => g.StableModuleId).ToArray(),
                    Volleys = station.Modules.OfType<EnemyOrbitalLabWeapon>().Select(g => g.ShotsFired - initialVolleys[g.StableModuleId]).ToArray(),
                    InitialBodyConstraints = initialBodyConstraints, FinalBodyConstraints = body.constraints,
                    SeedIndependenceEstablished = false, SeedLimitation = seedLimitation,
                    ShiftPolicy = shiftPolicies ? motion : "OFF", Scenario = scenario, Seed = seed, Encounter = scenario == 3 ? "shield" : scenario == 1 ? "melee" : scenario == 2 ? "mixed" : null,
                    Start = anchor, Configuration = configuration,
                    InitialMountAngles = presetAngles[build], SignedRouteDegrees = signedDegrees,
                    FinalConfiguration = Configuration() + "|settings=" + string.Join(";", station.Modules.OfType<EnemyOrbitalLabWeapon>().Select(g => g.Kind + ":" + JsonUtility.ToJson(g.Settings))),
                    Cooldowns = clocks.ToArray(), CooldownWeaponIds = station.Modules.OrderBy(g => g.CurrentMount.MountIndex).Select(g => g.StableModuleId).ToArray(), InitialPhases = phases, WeaponSettings = settings,
                    BaseSpeeds = station.Rings.Select(r => r.State.BaseRotationSpeed).ToArray(), Directions = station.Rings.Select(r => r.Direction).ToArray(),
                    Radii = station.Rings.Select(r => r.Radius).ToArray(), Enemies = enemies, EnemyComposition = enemyComposition, Trajectory = samples.ToArray(),
                    ShiftTimeline = shiftSamples.ToArray(),
                    WalkingSpeed = movement.WalkingSpeed, FixedDeltaTime = Time.fixedDeltaTime,
                    PathLength = distance, MaximumError = error, ClosureError = Vector2.Distance(body.position, anchor),
                    Telemetry = lab.Telemetry.Snapshot() };
                // Persist evidence before assertions so a rejected cell remains diagnosable.
                File.WriteAllText(Path.Combine(output, $"seed-{seed}-{scenario}-{cellName}-{motion}.json"), JsonUtility.ToJson(cell, true));
                string angleEvidence = string.Join(";", cell.InitialMountAngles.Select(a => FormattableString.Invariant($"{a.RingId}:{a.MountIndex}:{a.LocalPhase:R}")));
                foreach (var row in cell.Telemetry.Weapons)
                    csv.Add(FormattableString.Invariant($"{seed},False,{build},{motion},{scenario},{cell.Telemetry.Elapsed:R},{row.WeaponId},{row.Fired},{row.HitShots},{row.Misses},{row.Cancelled},{row.Pending},{row.BodyHits},{row.ShieldBlocks},{row.Damage:R},{row.Kills},{cell.Telemetry.PlayerImpacts},{cell.Telemetry.DamageReceived:R},{distance:R},{error:R},{cell.ClosureError:R},{signedDegrees:R},{angleEvidence}")
                        + (twoRings ? FormattableString.Invariant($",{(int)cell.InitialBodyConstraints},{(int)cell.FinalBodyConstraints}") : "")
                        + (scenario == 3 ? ",shield" : scenario == 1 ? ",melee" : scenario == 2 ? ",mixed" : "")
                        + (allocatedMultishot ? FormattableString.Invariant($",{allocatedModule},{modulePolicy},{cell.ModuleDistribution.Single(m => m.WeaponId == row.WeaponId).RingId},{cell.ModuleDistribution.Single(m => m.WeaponId == row.WeaponId).RingOrder},{string.Join("+", cell.ModuleDistribution.Single(m => m.WeaponId == row.WeaponId).Modules)},{cell.ModuleDistribution.Single(m => m.WeaponId == row.WeaponId).ExtraShotAngle:R},{cell.ModuleDistribution.Single(m => m.WeaponId == row.WeaponId).BodyPierces},{row.Fired},{cell.Volleys[Array.IndexOf(cell.VolleyWeaponIds, row.WeaponId)]},{cell.ModuleDistribution.Single(m => m.WeaponId == row.WeaponId).FireInterval:R},{cell.ModuleDistribution.Single(m => m.WeaponId == row.WeaponId).BurstInterval:R}") : ""));
                File.WriteAllLines(Path.Combine(output, "comparison.csv"), csv);
                string diagnosticKey = seed + ":" + scenario + (twoRings ? ":" + motion : "");
                if (paths.TryGetValue(diagnosticKey, out var baselinePath))
                {
                    int first = -1;
                    float positionDifference = 0f, targetDifference = 0f;
                    for (int i = 0; i < Math.Min(samples.Count, baselinePath.Length); i++)
                    {
                        var normalizedPosition = new Vector2(samples[i].Position.x, anchor.y + routeSign * (samples[i].Position.y - anchor.y));
                        var normalizedTarget = new Vector2(samples[i].Target.x, anchor.y + routeSign * (samples[i].Target.y - anchor.y));
                        positionDifference = Vector2.Distance(normalizedPosition, baselinePath[i].Position);
                        targetDifference = Vector2.Distance(normalizedTarget, baselinePath[i].Target);
                        if (positionDifference > .0001f || targetDifference > .0001f || Mathf.Abs(samples[i].Seconds - baselinePath[i].Seconds) > .0001f)
                        { first = i; break; }
                    }
                    var baselineCell = routeCells[diagnosticKey];
                    var diagnostic = new RouteDivergence {
                        Baseline = baselineCell.Build + ":" + baselineCell.ModulePolicy, Current = cellName,
                        Criterion = "First normalized position/target/time difference > 0.0001; diagnostic only, assertions unchanged. Samples contain raw world vectors; frame sampling can miss impacts between samples. No causal inference.",
                        SampleIndex = first, PositionDifference = positionDifference, TargetDifference = targetDifference,
                        BaselinePathLength = baselineCell.PathLength, CurrentPathLength = distance,
                        BaselineInitial = baselineCell.Trajectory[0], CurrentInitial = samples[0],
                        BaselineSample = first >= 0 ? baselineCell.Trajectory[first] : null,
                        CurrentSample = first >= 0 ? samples[first] : null };
                    File.WriteAllText(Path.Combine(output, $"first-divergence-{scenario}-{cellName}-{motion}.json"), JsonUtility.ToJson(diagnostic, true));
                }
                if (twoRings)
                {
                    Assert.That(cell.InitialBodyConstraints, Is.EqualTo(motion == "STATIC" ? RigidbodyConstraints2D.FreezeAll : RigidbodyConstraints2D.FreezeRotation));
                    Assert.That(cell.FinalBodyConstraints, Is.EqualTo(cell.InitialBodyConstraints), "Body constraints must remain unchanged during the cell");
                }
                Assert.That(cell.Telemetry.Elapsed, Is.EqualTo(duration).Within(.05f));
                Assert.That(Time.fixedTime - startTime, Is.EqualTo(duration).Within(.05f), "Physics duration differs from telemetry");
                Assert.That(cell.Telemetry.SurvivalApplicable, Is.False);
                Assert.That(cell.Telemetry.ConfigurationChanged, Is.False);
                Assert.That(cell.FinalConfiguration, Is.EqualTo(configuration));
                if (allocatedMultishot) Assert.That(JsonUtility.ToJson(new MovementCell { ModuleDistribution = cell.ModuleDistribution }),
                    Is.EqualTo(initialModules), "Modules and their actual effects must remain unchanged inside the cell");
                Assert.That(samples.All(s => Mathf.Abs(s.Position.x) < 28f && Mathf.Abs(s.Position.y) < 28f), Is.True, "Arena envelope exceeded");
                Assert.That(error, Is.LessThan(.75f), "Controller did not follow the prescribed route");
                Assert.That(cell.ClosureError, Is.LessThan(.75f));
                if (motion == "STATIC") Assert.That(distance, Is.LessThan(.01f), "Static player moved");
                else
                {
                    Assert.That(distance, Is.InRange(32f, 43f), "Two radius-3 loops must actually be travelled");
                    Assert.That(signedDegrees * routeSign, Is.InRange(690f, 750f), "Actual two-loop winding must match the requested direction");
                }
                string pathKey = seed + ":" + scenario + (twoRings ? ":" + motion : "");
                // Normalize CW to CCW by reflection in the horizontal line through start.
                var comparable = samples.Select(s => new PathSample { Seconds = s.Seconds,
                    Position = new Vector2(s.Position.x, anchor.y + routeSign * (s.Position.y - anchor.y)),
                    Target = new Vector2(s.Target.x, anchor.y + routeSign * (s.Target.y - anchor.y)) }).ToArray();
                if (paths.TryGetValue(pathKey, out var paired))
                {
                    Assert.That(samples.Count, Is.EqualTo(paired.Length));
                    // Same .021 s clock allowance as below, converted to maximum travel.
                    Assert.That(distance, Is.EqualTo(pathLengths[pathKey]).Within(movement.WalkingSpeed * .021f), "Reflected route lengths differ");
                    for (int i = 0; i < samples.Count; i++)
                    {
                        Assert.That(comparable[i].Seconds, Is.EqualTo(paired[i].Seconds).Within(.021f));
                        Assert.That(Vector2.Distance(comparable[i].Position, paired[i].Position), Is.LessThan(.2f), "Reflected paths diverged at sample " + i);
                        Assert.That(Vector2.Distance(comparable[i].Target, paired[i].Target), Is.LessThan(.2f), "Reflected route targets differ at sample " + i);
                    }
                }
                else { paths.Add(pathKey, comparable); pathLengths.Add(pathKey, distance); routeCells.Add(pathKey, cell); }
                Assert.That(cell.Telemetry.Weapons.Length, Is.EqualTo(4));
                foreach (var row in cell.Telemetry.Weapons)
                {
                    Assert.That(row.Fired, Is.EqualTo(row.HitShots + row.Misses + row.Cancelled + row.Pending));
                    if (allocatedMultishot)
                    {
                        var gun = station.Modules.OfType<EnemyOrbitalLabWeapon>().Single(g => g.StableModuleId == row.WeaponId);
                        if (allocatedModule == "RAPID")
                        {
                            Assert.That(gun.Settings.BurstShots, Is.LessThanOrEqualTo(1), "Cadence bound requires single-shot pistols");
                            Assert.That(gun.Settings.ChargeSeconds, Is.Zero, "Cadence bound excludes charge time");
                            float interval = Mathf.Max(.05f, gun.Settings.FireInterval / gun.FireRateMultiplier);
                            float initialClock = clocksById[gun.StableModuleId];
                            // Tick discards overshoot; each interval can cost one additional fixed step.
                            int minimum = Mathf.FloorToInt(Mathf.Max(0f, duration - Mathf.Max(0f, initialClock)) / (interval + Time.fixedDeltaTime));
                            int maximum = Mathf.CeilToInt(Mathf.Max(0f, duration - Mathf.Max(0f, initialClock)) / interval) + 1;
                            Assert.That(cell.Volleys[Array.IndexOf(cell.VolleyWeaponIds, row.WeaponId)], Is.InRange(minimum, maximum), "Actual individual RAPID cadence: " + row.WeaponId);
                        }
                        int projectilesPerVolley = Mathf.Max(1, gun.Settings.Pellets) + (cell.ModuleDistribution.Single(m => m.WeaponId == row.WeaponId).HasExtraShot ? 1 : 0);
                        Assert.That(row.Fired, Is.EqualTo(cell.Volleys[Array.IndexOf(cell.VolleyWeaponIds, row.WeaponId)] * projectilesPerVolley), "Spawned projectile count differs from actual volleys: " + row.WeaponId);
                    }
                }
                // RAPID reallocates cadence across IDs with distinct preserved initial clocks.
                // Its equal two-module budget is checked above; use each gun's cadence bound instead.
                if (allocatedMultishot && allocatedModule != "RAPID")
                {
                    int totalVolleys = cell.Volleys.Sum();
                    int totalProjectiles = cell.Telemetry.Weapons.Sum(w => w.Fired);
                    if (!totalVolleysBaseline.HasValue)
                    {
                        totalVolleysBaseline = totalVolleys;
                        totalProjectilesBaseline = totalProjectiles;
                    }
                    else
                    {
                        Assert.That(totalVolleys, Is.EqualTo(totalVolleysBaseline.Value), "Allocation/direction changed total volleys");
                        Assert.That(totalProjectiles, Is.EqualTo(totalProjectilesBaseline.Value), "Allocation/direction changed total spawned projectiles");
                    }
                }
                if (shiftPolicies)
                {
                    var fired = cell.Telemetry.Weapons.OrderBy(w => w.WeaponId).Select(w => w.Fired).ToArray();
                    var ids = cell.Telemetry.Weapons.OrderBy(w => w.WeaponId).Select(w => w.WeaponId).ToArray();
                    if (firedBaseline == null) { firedBaseline = fired; weaponIds = ids; }
                    else
                    {
                        CollectionAssert.AreEqual(weaponIds, ids);
                        CollectionAssert.AreEqual(firedBaseline, fired, "Shift changed weapon clocks");
                    }
                    foreach (var sample in shiftSamples)
                    {
                        Assert.That(sample.Slowing, Is.EqualTo(sample.Held), "Actual eligibility/policy differs at frame " + sample.Frame);
                        Assert.That(sample.TimeScale, Is.EqualTo(1f));
                        Assert.That(sample.DeltaTime, Is.EqualTo(.02f).Within(.00001f));
                        Assert.That(sample.WorldSeconds, Is.EqualTo((sample.Frame + 1) * .02f).Within(.025f));
                        for (int i = 0; i < station.Rings.Count; i++)
                        {
                            float multiplier = sample.Held ? OrbitalCombatV2LabControl.SlowMultiplier : 1f;
                            Assert.That(sample.Multipliers[i], Is.EqualTo(multiplier));
                            float before = sample.Frame == 0 ? phases[i] : shiftSamples[sample.Frame - 1].Phases[i];
                            float expected = station.Rings[i].RotationSpeed * station.Rings[i].Direction * multiplier * sample.DeltaTime;
                            Assert.That(Mathf.DeltaAngle(before, sample.Phases[i]), Is.EqualTo(expected).Within(.001f), "Ring phase at frame " + sample.Frame);
                        }
                    }
                    Assert.That(cell.Telemetry.OrbitSlowSeconds,
                        Is.EqualTo(shiftSamples.Count(s => s.Slowing) * .02f).Within(.025f));
                }
                cell.Validated = true;
                File.WriteAllText(Path.Combine(output, $"seed-{seed}-{scenario}-{cellName}-{motion}.json"), JsonUtility.ToJson(cell, true));
            }
        }
        finally
        {
            lab.SetPaused(true); lab.StopCombatTelemetry();
            if (allocatedMultishot)
                foreach (var gun in station.Modules)
                    for (int slot = 0; slot < originalModules[gun.StableModuleId].Length; slot++)
                    {
                        Assert.That(lab.WeaponModules.SetModule(gun.StableModuleId, slot, originalModules[gun.StableModuleId][slot]), Is.True);
                        Assert.That(lab.WeaponModules.GetModule(gun.StableModuleId, slot), Is.EqualTo(originalModules[gun.StableModuleId][slot]));
                    }
            movement.enabled = movementEnabled; movement.MovementIntent = intent;
            body.constraints = constraints;
            body.position = originalPosition; body.linearVelocity = originalVelocity;
            body.angularVelocity = originalAngularVelocity;
            lab.Player.transform.position = originalPosition;
            for (int i = 0; i < station.Rings.Count; i++) station.Rings[i].State.CurrentPhase = originalPhases[i];
            RestoreAngles(originalAngles);
            if (twoRings) for (int i = 0; i < station.Rings.Count; i++) { lab.SelectSandboxRing(i); lab.SetSandboxRingDirection(originalDirections[i]); }
            foreach (var gun in station.Modules) cooldown.SetValue(gun, twoRings ? clocksById[gun.StableModuleId] : clocks[gun.CurrentMount.MountIndex]);
            lab.GetType().GetField("tacticalPreset", Flags).SetValue(lab, originalPreset);
            lab.GetType().GetField("tacticalConfiguration", Flags).SetValue(lab, originalTacticalConfiguration);
            station.DebugBulletTimeHeld = debugHeld;
            lab.CombatV2.Suspend();
            lab.CombatV2.GetType().GetField("needsRelease", Flags).SetValue(lab.CombatV2, needsRelease);
            lab.CombatV2.GetType().GetProperty("IsSlowingOrbits", Flags).SetValue(lab.CombatV2, slowing);
            for (int i = 0; i < station.Rings.Count; i++) station.Rings[i].LabRotationMultiplier = originalMultipliers[i];
            Physics2D.SyncTransforms();
            station.enabled = stationEnabled;
            Time.captureDeltaTime = capture; Time.fixedDeltaTime = fixedDelta; Time.timeScale = scale;
            UnityEngine.Random.state = random;
            lab.SetPaused(paused);
            TestContext.WriteLine("Experimental movement/Shift evidence: " + output);
        }
    }
    // Historical MIXED slice: retained for reference, deliberately not rerun by the movement entry point.
    IEnumerator CompareHistoricalStaticBuilds()
    {
        typeof(EnemyOrbitalLabSandboxTests).GetMethod("SetGameView", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, null);
        string output = Path.GetFullPath(Path.Combine(Application.dataPath, "../Artifacts/GeneratedQA/OrbitalTactics"));
        Directory.CreateDirectory(output);
        var reports = new List<string>(); var csv = new List<string> { "build,scenario,seconds,weapon,id,fired,hitShots,misses,pending,cancelled,bodyHits,shieldBlocks,damage,kills,playerImpacts,survivalApplicable" };
        float originalCapture = Time.captureDeltaTime; var random = UnityEngine.Random.state;
        var body = lab.Player.GetComponent<Rigidbody2D>(); var constraints = body.constraints;
        Vector2 anchor = body.position;
        var cooldown = typeof(OrbitalModuleRuntime).GetProperty("RuntimeCooldown", Flags);
        float[] startingClocks = station.Modules.OrderBy(g => g.CurrentMount.MountIndex).Select(g => (float)cooldown.GetValue(g)).ToArray();
        try
        {
            Time.captureDeltaTime = .02f; body.constraints = RigidbodyConstraints2D.FreezeAll;
            foreach (string build in new[] { "EVEN", "GROUPED", "MIXED" })
            foreach (int scenario in new[] { 1, 3, 2 })
            {
                lab.SetPaused(true); lab.ClearSandboxArena(); Call("ApplyTacticalPreset", build);
                lab.Player.StopAllCoroutines();
                typeof(PlayerHealth).GetField("isInvulnerable", Flags).SetValue(lab.Player, false);
                body.position = anchor; body.linearVelocity = Vector2.zero; lab.Player.transform.position = anchor;
                foreach (var ring in station.Rings) { ring.State.CurrentPhase = 0f; ring.Tick(0f); }
                // Restore four distinct observed cooldowns, never align them into a volley.
                foreach (var gun in station.Modules) cooldown.SetValue(gun, startingClocks[gun.CurrentMount.MountIndex]);
                UnityEngine.Random.InitState(42421028);
                lab.StartSandboxEncounter(scenario); lab.SetSandboxSpawning(false); lab.SetPaused(false);
                Physics2D.SyncTransforms();
                for (int frame = 0; frame < 600; frame++) yield return null;
                lab.SetPaused(true); lab.StopCombatTelemetry();
                string json = (string)Call("CombatTelemetryJson"); reports.Add(json); var report = JsonUtility.FromJson<Report>(json);
                Assert.That(report.Elapsed, Is.EqualTo(12f).Within(.05f)); Assert.That(report.SurvivalApplicable, Is.False);
                foreach (var row in report.Weapons)
                {
                    Assert.That(row.Fired, Is.EqualTo(row.HitShots + row.Misses + row.Cancelled + row.Pending));
                    csv.Add(FormattableString.Invariant($"{build},{scenario},{report.Elapsed:F2},{row.Name},{row.WeaponId},{row.Fired},{row.HitShots},{row.Misses},{row.Pending},{row.Cancelled},{row.BodyHits},{row.ShieldBlocks},{row.Damage:F2},{row.Kills},{report.PlayerImpacts},{report.SurvivalApplicable}"));
                }
                File.WriteAllText(Path.Combine(output, "comparison.json"), "[" + string.Join(",", reports) + "]");
                File.WriteAllLines(Path.Combine(output, "comparison.csv"), csv);
                if (scenario == 2)
                {
                    lab.SetSandboxTab(0); yield return null;
                    string capture = Path.Combine(output, build.ToLowerInvariant() + ".png");
                    if (File.Exists(capture)) File.Delete(capture);
                    ScreenCapture.CaptureScreenshot(capture);
                    yield return CoreTestSupport.Await(() => File.Exists(capture));
                }
            }
        }
        finally { lab.SetPaused(true); Time.captureDeltaTime = originalCapture; UnityEngine.Random.state = random; if (body != null) body.constraints = constraints; }
    }
    [UnityTest] public IEnumerator MovementDirectionsAndOrbitControlsUseExistingRuntimeWithoutChangingWorldTime()
    {
        var movement = lab.Player.GetComponent<CharacterMovement2D>(); var body = lab.Player.GetComponent<Rigidbody2D>();
        var simulation = Physics2D.simulationMode; Vector2 origin = body.position;
        var intent = movement.MovementIntent;
        bool enabled = station.enabled, movementEnabled = movement.enabled, held = station.DebugBulletTimeHeld;
        bool paused = lab.Paused;
        float originalScale = Time.timeScale;
        Vector2 velocity = body.linearVelocity;
        var center = station.GetComponent<OrbitalCenterShift>();
        Vector2 offset = center.Offset;
        bool needsRelease = (bool)lab.CombatV2.GetType().GetField("needsRelease", Flags).GetValue(lab.CombatV2);
        bool slowing = lab.CombatV2.IsSlowingOrbits;
        float[] multipliers = station.Rings.Select(r => r.LabRotationMultiplier).ToArray();
        station.enabled = false; lab.SetPaused(false);
        try
        {
            Physics2D.simulationMode = SimulationMode2D.Script;
            foreach (Vector2 direction in new[] { Vector2.up, Vector2.left, Vector2.down, Vector2.right })
            {
                movement.enabled = false; movement.enabled = true;
                body.position = origin; body.linearVelocity = Vector2.zero; Physics2D.SyncTransforms();
                movement.MovementIntent = () => direction;
                // Advance the real controller and physics together; coroutine fixed-wait
                // wakeups in the Editor runner need not represent a completed physics step.
                for (int i = 0; i < 30; i++)
                {
                    typeof(CharacterMovement2D).GetMethod("FixedUpdate", Flags).Invoke(movement, null);
                    Physics2D.Simulate(.02f);
                }
                float distance = Vector2.Dot(body.position - origin, direction);
                TestContext.WriteLine($"Direction {direction}: {distance:0.000} m, speed {movement.speed:0.00}, fixedDelta {Time.fixedDeltaTime}");
                Assert.That(distance, Is.GreaterThan(.1f), "Direction " + direction);
            }
            Physics2D.simulationMode = simulation; movement.MovementIntent = null;
            typeof(OrbitalCenterShift).GetMethod("Advance", Flags).Invoke(center, new object[] { Vector2.right, true, .1f });
            Assert.That(center.Offset.x, Is.GreaterThan(0f));
            station.DebugBulletTimeHeld = false;
            station.GetType().GetMethod("UpdateRightMouse", Flags).Invoke(station, new object[] { .01f });
            station.DebugBulletTimeHeld = true;
            station.GetType().GetMethod("UpdateRightMouse", Flags).Invoke(station, new object[] { .01f });
            float phase = station.Rings[0].Phase;
            station.Rings[0].Tick(.5f);
            Assert.That(Mathf.Abs(Mathf.DeltaAngle(phase, station.Rings[0].Phase)), Is.EqualTo(station.Rings[0].RotationSpeed * .5f * .4f).Within(.001f));
            Assert.That(Time.timeScale, Is.EqualTo(1f));
        }
        finally
        {
            lab.SetPaused(paused); Time.timeScale = originalScale;
            Physics2D.simulationMode = simulation;
            movement.MovementIntent = intent; movement.enabled = movementEnabled;
            body.position = origin; body.linearVelocity = velocity; lab.Player.transform.position = origin;
            center.ResetOffset();
            center.GetType().GetProperty("Offset", Flags).SetValue(center, offset);
            center.Refresh();
            station.DebugBulletTimeHeld = held; lab.CombatV2.Suspend();
            lab.CombatV2.GetType().GetField("needsRelease", Flags).SetValue(lab.CombatV2, needsRelease);
            lab.CombatV2.GetType().GetProperty("IsSlowingOrbits", Flags).SetValue(lab.CombatV2, slowing);
            for (int i = 0; i < station.Rings.Count; i++) station.Rings[i].LabRotationMultiplier = multipliers[i];
            station.enabled = enabled; Physics2D.SyncTransforms();
        }
        yield return MeasureMovementCells(true);
    }
    [UnityTest] public IEnumerator NewGunDuringRecordingHasItsOwnCompleteRow()
    {
        bool wasEnabled = station.enabled;
        Call("ApplyTacticalPreset", "EVEN");
        var ring = station.Rings[0];
        float speed = ring.RotationSpeed;
        int direction = ring.State.Direction;
        var expected = new Dictionary<int, (int RingId, int Order, float Phase, float Radius, float Speed, int Direction)>();
        void Remember(EnemyOrbitalLabWeapon weapon)
        {
            var mount = weapon.CurrentMount; var owner = mount.Ring;
            expected.Add(weapon.StableModuleId, (owner.RingId, owner.State.Order, mount.LocalPhase,
                owner.State.Radius, owner.RotationSpeed, owner.State.Direction));
        }
        void CheckGeometry(EnemyOrbitalLabTelemetry.Report snapshot)
        {
            Assert.That(snapshot.Weapons.Length, Is.EqualTo(expected.Count));
            foreach (var row in snapshot.Weapons)
            {
                var placement = expected[row.WeaponId];
                Assert.That(row.InitialRingId, Is.EqualTo(placement.RingId));
                Assert.That(row.InitialRingOrder, Is.EqualTo(placement.Order));
                Assert.That(row.InitialLocalPhase, Is.EqualTo(placement.Phase).Within(.0001f));
                Assert.That(row.InitialRingRadius, Is.EqualTo(placement.Radius).Within(.0001f));
                Assert.That(row.InitialRingRotationSpeed, Is.EqualTo(placement.Speed).Within(.0001f));
                Assert.That(row.InitialRingDirection, Is.EqualTo(placement.Direction));
            }
        }
        try
        {
            station.enabled = false;
            Assert.That(lab.Placement.ApplyPreset("GROUPED"), Is.True);
            foreach (var weapon in station.Modules.OfType<EnemyOrbitalLabWeapon>()) Remember(weapon);
            Call("BeginCombatTelemetry", "Editing");
            CheckGeometry(lab.Telemetry.Snapshot());
            lab.RemoveSandboxWeapon(); Assert.That(lab.InstallSandboxWeapon(1), Is.True);
            var gun = station.Modules.OfType<EnemyOrbitalLabWeapon>().Single(g => g.Kind == OrbitalModuleKind.Shotgun);
            // Late registration must describe these conditions, not those at Begin.
            Assert.That(lab.Placement.ApplyPreset("EVEN"), Is.True);
            lab.SelectSandboxRing(0);
            lab.SetSandboxRingSpeed(73f); lab.SetSandboxRingDirection(-direction);
            Remember(gun);
            Ready(gun); gun.Tick(.001f); adapter.Tick(1f);
            var report = Read();
            Assert.That(report.Weapons.Any(w => w.WeaponId == gun.StableModuleId), Is.True, "A new ID must never silently disappear from totals.");
            Assert.That(report.Weapons.Single(w => w.WeaponId == gun.StableModuleId).Fired, Is.EqualTo(7));
            Assert.That(report.ConfigurationChanged, Is.True);
            CheckGeometry(lab.Telemetry.Snapshot());
            CheckGeometry(JsonUtility.FromJson<EnemyOrbitalLabTelemetry.Report>((string)Call("CombatTelemetryJson")));
            Assert.That(lab.Placement.ApplyPreset("GROUPED"), Is.True);
            Assert.That(station.Modules.Any(w => Mathf.Abs(Mathf.DeltaAngle(w.CurrentMount.LocalPhase,
                expected[w.StableModuleId].Phase)) > .01f), Is.True, "The lab API must really change placement.");
            lab.SetSandboxRingSpeed(111f); lab.SetSandboxRingDirection(direction);
            CheckGeometry(lab.Telemetry.Snapshot());
            CheckGeometry(JsonUtility.FromJson<EnemyOrbitalLabTelemetry.Report>((string)Call("CombatTelemetryJson")));
            Assert.That(Read().ConfigurationChanged, Is.True);
        }
        finally
        {
            lab.StopCombatTelemetry(); adapter.DebugClearProjectiles();
            lab.SelectSandboxRing(0); lab.SetSandboxRingSpeed(speed); lab.SetSandboxRingDirection(direction);
            Call("ApplyTacticalPreset", "EVEN"); station.enabled = wasEnabled;
        }
        yield return null;
    }
    [UnityTest] public IEnumerator ConfigurationHistoryAndCustomLabelSurviveRestoringTheOriginalModules()
    {
        bool wasEnabled = station.enabled;
        bool fixedDirection = lab.FixedDirection;
        Call("ApplyTacticalPreset", "EVEN");
        float damage = station.State.ShootingDamageMultiplier;
        float rate = station.State.ShootingRateMultiplier;
        var movement = lab.Player.GetComponent<CharacterMovement2D>();
        Assert.That(movement, Is.Not.Null);
        float moveSpeed = movement.RunUpgradeMoveSpeedMultiplier;
        float enemySpeed = lab.SpeedMultiplier;
        bool enemiesFrozen = EnemyDebugAiFreeze.IsFrozen;
        bool enemiesInvincible = lab.EnemiesInvincible;
        float maxHealth = lab.Player.MaxHealth;
        float currentHealth = lab.Player.CurrentHealth;
        var radiusRing = station.Rings[0];
        float baseRadius = radiusRing.State.Radius;
        float multishotAngle = lab.ModuleMultishotAngle;
        float rapidMultiplier = lab.ModuleRapidMultiplier;
        int pierceBonus = lab.ModulePierceBonus;
        var cooldown = typeof(OrbitalModuleRuntime).GetProperty("RuntimeCooldown", Flags);
        var clocks = station.Modules.ToDictionary(g => g, g => (float)cooldown.GetValue(g));
        var modules = station.Modules.ToDictionary(g => g.StableModuleId,
            g => Enumerable.Range(0, lab.WeaponModules.SlotCount(g.StableModuleId))
                .Select(s => lab.WeaponModules.GetModule(g.StableModuleId, s)).ToArray());
        void RestoreModules()
        {
            foreach (var pair in modules)
                for (int slot = 0; slot < pair.Value.Length; slot++)
                    lab.WeaponModules.SetModule(pair.Key, slot, pair.Value[slot]);
        }
        void RestoreShooting()
        {
            station.State.ShootingDamageMultiplier = damage;
            station.State.ShootingRateMultiplier = rate;
            foreach (var pair in clocks) cooldown.SetValue(pair.Key, pair.Value);
        }
        try
        {
            station.enabled = false;
            var gun = station.Modules[0];
            Call("BeginCombatTelemetry", "History");
            lab.WeaponModules.SetModule(gun.StableModuleId, 0, "RAPID");
            lab.Telemetry.Tick(.02f);
            lab.WeaponModules.SetModule(gun.StableModuleId, 0, "EMPTY");
            var history = Read();
            lab.WeaponModules.SetModule(gun.StableModuleId, 0, "RAPID");
            Call("BeginCombatTelemetry", "Custom"); var custom = Read();
            Assert.That(history.ConfigurationChanged, Is.True, "Running a changed build must remain marked after restoring it.");
            foreach (var upgrade in new[] { EnemyOrbitalLabController.LabUpgrade.ShootingDamage, EnemyOrbitalLabController.LabUpgrade.ShootingRate })
            {
                RestoreShooting();
                RestoreModules();
                Call("ApplyTacticalPreset", "EVEN");
                Assert.That(lab.TacticalPreset, Is.EqualTo("EVEN"), "Each upgrade needs a fresh reference build.");
                Call("BeginCombatTelemetry", upgrade.ToString());
                var before = Read();
                Assert.That(before.ConfigurationChanged, Is.False);
                Assert.That(before.Build, Is.EqualTo("EVEN"));
                Assert.That(lab.ApplyLabUpgrade(upgrade), Is.True, "Exercise the available lab upgrade: " + upgrade);
                Assert.That(station.State.ShootingDamageMultiplier,
                    Is.EqualTo(damage * (upgrade == EnemyOrbitalLabController.LabUpgrade.ShootingDamage ? 1.2f : 1f)).Within(.0001f));
                Assert.That(station.State.ShootingRateMultiplier,
                    Is.EqualTo(rate * (upgrade == EnemyOrbitalLabController.LabUpgrade.ShootingRate ? 1.15f : 1f)).Within(.0001f));
                var upgraded = Read(); // Snapshot must detect the change without waiting for Tick.
                Assert.That(upgraded.ConfigurationChanged, Is.True, upgrade.ToString());
                Assert.That(lab.TacticalPreset, Is.Null, "An upgraded reference build is custom.");
                Assert.That(upgraded.Build, Is.EqualTo(before.Build), "Keep the attribution at the start of recording.");
            }

            RestoreShooting();
            RestoreModules();
            movement.SetRunUpgradeMoveSpeedMultiplier(moveSpeed);
            Call("ApplyTacticalPreset", "EVEN");
            Assert.That(lab.TacticalPreset, Is.EqualTo("EVEN"), "Movement upgrade needs a fresh reference build.");
            Call("BeginCombatTelemetry", "MoveSpeed");
            var beforeMovement = Read();
            Assert.That(beforeMovement.ConfigurationChanged, Is.False);
            Assert.That(beforeMovement.Build, Is.EqualTo("EVEN"));
            Assert.That(lab.ApplyLabUpgrade(EnemyOrbitalLabController.LabUpgrade.MoveSpeed), Is.True);
            Assert.That(movement.RunUpgradeMoveSpeedMultiplier, Is.EqualTo(moveSpeed * 1.10f).Within(.0001f));
            var moved = Read(); // Snapshot must detect the upgrade without waiting for Tick.
            Assert.That(moved.ConfigurationChanged, Is.True, "MoveSpeed");
            Assert.That(lab.TacticalPreset, Is.Null, "A movement-upgraded reference build is custom.");
            Assert.That(moved.Build, Is.EqualTo(beforeMovement.Build), "Keep the attribution at the start of recording.");

            RestoreShooting();
            RestoreModules();
            movement.SetRunUpgradeMoveSpeedMultiplier(moveSpeed);
            lab.Player.SetRuntimeHealth(maxHealth, currentHealth);
            Call("ApplyTacticalPreset", "EVEN");
            Assert.That(lab.TacticalPreset, Is.EqualTo("EVEN"), "Health upgrade needs a fresh reference build.");
            Call("BeginCombatTelemetry", "MaxHealth");
            var beforeHealth = Read();
            Assert.That(beforeHealth.ConfigurationChanged, Is.False);
            Assert.That(beforeHealth.Build, Is.EqualTo("EVEN"));

            float changedHealth = currentHealth > 1f ? currentHealth - 1f : currentHealth + 1f;
            lab.Player.SetRuntimeHealth(maxHealth, changedHealth);
            Assert.That(lab.Player.CurrentHealth, Is.Not.EqualTo(currentHealth), "Exercise a current-health-only change.");
            Assert.That(lab.Player.MaxHealth, Is.EqualTo(maxHealth));
            Assert.That(Read().ConfigurationChanged, Is.False, "Current health is combat state, not build configuration.");
            Assert.That(lab.TacticalPreset, Is.EqualTo("EVEN"), "Current health must not remove the reference label.");
            lab.Player.SetRuntimeHealth(maxHealth, currentHealth);

            Assert.That(lab.ApplyLabUpgrade(EnemyOrbitalLabController.LabUpgrade.MaxHealth), Is.True);
            Assert.That(lab.Player.MaxHealth, Is.EqualTo(maxHealth + 20f).Within(.0001f));
            Assert.That(lab.Player.CurrentHealth, Is.EqualTo(currentHealth + 20f).Within(.0001f));
            var healthier = Read(); // Snapshot must detect the upgrade without waiting for Tick.
            Assert.That(healthier.ConfigurationChanged, Is.True, "MaxHealth");
            Assert.That(lab.TacticalPreset, Is.Null, "A health-upgraded reference build is custom.");
            Assert.That(healthier.Build, Is.EqualTo(beforeHealth.Build), "Keep the attribution at the start of recording.");

            RestoreShooting();
            RestoreModules();
            movement.SetRunUpgradeMoveSpeedMultiplier(moveSpeed);
            lab.Player.SetRuntimeHealth(maxHealth, currentHealth);
            lab.SetFixedDirection(true);
            Call("ApplyTacticalPreset", "EVEN");
            Assert.That(lab.TacticalPreset, Is.EqualTo("EVEN"), "Firing mode needs a fresh reference build.");
            Call("BeginCombatTelemetry", "FiringMode");
            var beforeMode = Read();
            Assert.That(beforeMode.ConfigurationChanged, Is.False);
            Assert.That(beforeMode.Build, Is.EqualTo("EVEN"));
            lab.SetFixedDirection(false);
            Assert.That(lab.FixedDirection, Is.False);
            var changedMode = Read(); // Snapshot must detect the change without waiting for Tick.
            Assert.That(changedMode.ConfigurationChanged, Is.True, "Firing mode");
            Assert.That(lab.TacticalPreset, Is.Null, "A reference build with a different firing mode is custom.");
            Assert.That(changedMode.Build, Is.EqualTo(beforeMode.Build), "Keep the attribution at the start of recording.");
            lab.SetFixedDirection(true);
            Assert.That(Read().ConfigurationChanged, Is.True, "Restoring FIXED must not erase mixed recording history.");

            Call("ApplyTacticalPreset", "EVEN");
            Call("BeginCombatTelemetry", "FreshFiringMode");
            Assert.That(Read().ConfigurationChanged, Is.False, "A new recording starts with clean configuration history.");
            Assert.That(Read().Build, Is.EqualTo("EVEN"));
            lab.StopCombatTelemetry();
            string completedModeJson = lab.CombatTelemetryJson();
            lab.SetFixedDirection(false);
            Assert.That(lab.TacticalPreset, Is.Null);
            Assert.That(lab.CombatTelemetryJson(), Is.EqualTo(completedModeJson), "Later firing-mode changes must not alter a completed report.");

            lab.SetFixedDirection(true);
            Call("ApplyTacticalPreset", "EVEN");
            Assert.That(lab.TacticalPreset, Is.EqualTo("EVEN"), "Base radius needs a fresh reference build.");
            Call("BeginCombatTelemetry", "BaseRadius");
            var beforeRadius = Read();
            Assert.That(beforeRadius.ConfigurationChanged, Is.False);
            Assert.That(beforeRadius.Build, Is.EqualTo("EVEN"));
            radiusRing.State.Radius = baseRadius + .25f;
            var changedRadius = Read(); // Snapshot alone must detect base geometry changes, without Tick.
            Assert.That(changedRadius.ConfigurationChanged, Is.True, "Base radius");
            Assert.That(lab.TacticalPreset, Is.Null, "A reference build with different base geometry is custom.");
            Assert.That(changedRadius.Build, Is.EqualTo("EVEN"), "Keep the attribution at the start of recording.");
            radiusRing.State.Radius = baseRadius;
            Assert.That(Read().ConfigurationChanged, Is.True, "Restoring radius must not erase mixed recording history.");
            Assert.That(Read().Build, Is.EqualTo("EVEN"));

            Call("ApplyTacticalPreset", "EVEN");
            Call("BeginCombatTelemetry", "FreshBaseRadius");
            Assert.That(Read().ConfigurationChanged, Is.False, "A new recording starts with clean configuration history.");
            Assert.That(Read().Build, Is.EqualTo("EVEN"));
            lab.StopCombatTelemetry();
            string completedRadiusJson = lab.CombatTelemetryJson();
            radiusRing.State.Radius = baseRadius + .25f;
            Assert.That(lab.TacticalPreset, Is.Null);
            Assert.That(lab.CombatTelemetryJson(), Is.EqualTo(completedRadiusJson), "Later radius changes must not alter a completed report.");

            // Independent effective-angle attribution, using the same real weapons on pause.
            radiusRing.State.Radius = baseRadius;
            RestoreShooting();
            RestoreModules();
            lab.SetPaused(true);
            foreach (var weapon in station.Modules)
                for (int slot = 0; slot < lab.WeaponModules.SlotCount(weapon.StableModuleId); slot++)
                    Assert.That(lab.WeaponModules.SetModule(weapon.StableModuleId, slot, "EMPTY"), Is.True);
            float initialAngle = Mathf.Clamp(multishotAngle, 1f, 30f);
            float changedAngle = initialAngle <= 15f ? initialAngle + 1f : initialAngle - 1f;
            lab.ModuleMultishotAngle = initialAngle;
            Call("ApplyTacticalPreset", "EVEN");
            Call("BeginCombatTelemetry", "UnusedMultishotAngle");
            Assert.That(station.Modules.All(g => !lab.WeaponModules.ExtraShotAngle(g.StableModuleId).HasValue), Is.True);
            Assert.That(Read().ConfigurationChanged, Is.False);
            lab.ModuleMultishotAngle = changedAngle;
            Assert.That(Read().ConfigurationChanged, Is.False, "An unused global angle must not change configuration.");
            Assert.That(lab.TacticalPreset, Is.EqualTo("EVEN"));
            Assert.That(Read().Build, Is.EqualTo("EVEN"));

            lab.ModuleMultishotAngle = initialAngle;
            var angleGun = station.Modules.OfType<EnemyOrbitalLabWeapon>().First();
            Assert.That(lab.WeaponModules.SetModule(angleGun.StableModuleId, 0, "MULTISHOT"), Is.True);
            var angleWeapons = station.Modules.ToArray();
            Call("ApplyTacticalPreset", "EVEN");
            CollectionAssert.AreEqual(angleWeapons, station.Modules, "Re-establish EVEN without replacing weapons.");
            Assert.That(lab.WeaponModules.ExtraShotAngle(angleGun.StableModuleId), Is.EqualTo(initialAngle));
            Assert.That(lab.TacticalPreset, Is.EqualTo("EVEN"));
            Call("BeginCombatTelemetry", "MultishotAngle");
            var beforeAngle = Read();
            Assert.That(beforeAngle.ConfigurationChanged, Is.False);
            Assert.That(beforeAngle.Build, Is.EqualTo("EVEN"));
            lab.ModuleMultishotAngle = changedAngle;
            Assert.That(lab.WeaponModules.ExtraShotAngle(angleGun.StableModuleId), Is.EqualTo(changedAngle));
            var changedMultishot = Read(); // No Tick between editing the angle and Snapshot.
            Assert.That(changedMultishot.ConfigurationChanged, Is.True, "Effective MULTISHOT angle");
            Assert.That(lab.TacticalPreset, Is.Null);
            Assert.That(changedMultishot.Build, Is.EqualTo(beforeAngle.Build), "Keep the initial attribution.");
            lab.ModuleMultishotAngle = initialAngle;
            Assert.That(Read().ConfigurationChanged, Is.True, "Restoring the angle must not erase mixed history.");
            Assert.That(Read().Build, Is.EqualTo("EVEN"));

            Call("ApplyTacticalPreset", "EVEN");
            Call("BeginCombatTelemetry", "FreshMultishotAngle");
            Assert.That(Read().ConfigurationChanged, Is.False, "A new recording starts clean.");
            Assert.That(Read().Build, Is.EqualTo("EVEN"));
            lab.StopCombatTelemetry();
            string completedAngleJson = lab.CombatTelemetryJson();
            lab.ModuleMultishotAngle = changedAngle;
            Assert.That(lab.TacticalPreset, Is.Null);
            Assert.That(lab.CombatTelemetryJson(), Is.EqualTo(completedAngleJson), "Later angle changes must not alter a completed report.");

            // Same module labels can acquire different effective settings through SetModule.
            lab.ModuleMultishotAngle = multishotAngle;
            foreach (string effect in new[] { "RAPID", "PIERCE" })
            {
                lab.ModuleRapidMultiplier = 2f;
                lab.ModulePierceBonus = 1;
                foreach (var weapon in station.Modules)
                    for (int slot = 0; slot < lab.WeaponModules.SlotCount(weapon.StableModuleId); slot++)
                        Assert.That(lab.WeaponModules.SetModule(weapon.StableModuleId, slot, "EMPTY"), Is.True);
                var settingsGun = station.Modules.OfType<EnemyOrbitalLabWeapon>().First();
                int id = settingsGun.StableModuleId;
                Assert.That(lab.WeaponModules.SetModule(id, 0, effect), Is.True);
                var sameWeapons = station.Modules.ToArray();
                Call("ApplyTacticalPreset", "EVEN");
                CollectionAssert.AreEqual(sameWeapons, station.Modules, "Keep real weapon instances and IDs.");
                var mounts = station.Modules.Select(w => w.CurrentMount).ToArray();
                var phases = mounts.Select(m => m.LocalPhase).ToArray();
                float fire = settingsGun.Settings.FireInterval;
                float burst = settingsGun.Settings.BurstInterval;
                int pierces = settingsGun.Settings.BodyPierces;
                Call("BeginCombatTelemetry", "Effective" + effect);
                var beforeSettings = Read();
                var moduleLabels = beforeSettings.Weapons.Select(w => w.Modules).ToArray();
                Assert.That(beforeSettings.ConfigurationChanged, Is.False);
                Assert.That(beforeSettings.Build, Is.EqualTo("EVEN"));

                if (effect == "RAPID") lab.ModuleRapidMultiplier = 3f;
                else lab.ModulePierceBonus = 2;
                Assert.That(settingsGun.Settings.FireInterval, Is.EqualTo(fire));
                Assert.That(settingsGun.Settings.BurstInterval, Is.EqualTo(burst));
                Assert.That(settingsGun.Settings.BodyPierces, Is.EqualTo(pierces));
                Assert.That(Read().ConfigurationChanged, Is.False, "Raw Inspector edits do not apply settings.");
                Assert.That(lab.TacticalPreset, Is.EqualTo("EVEN"));

                Assert.That(lab.WeaponModules.SetModule(id, 0, effect), Is.True);
                Assert.That(settingsGun.Settings.FireInterval, Is.EqualTo(effect == "RAPID" ? fire * 2f / 3f : fire).Within(.000001f));
                Assert.That(settingsGun.Settings.BurstInterval, Is.EqualTo(effect == "RAPID" ? burst * 2f / 3f : burst).Within(.000001f));
                Assert.That(settingsGun.Settings.BodyPierces, Is.EqualTo(effect == "PIERCE" ? pierces + 1 : pierces));
                CollectionAssert.AreEqual(sameWeapons, station.Modules);
                CollectionAssert.AreEqual(mounts, station.Modules.Select(w => w.CurrentMount).ToArray());
                CollectionAssert.AreEqual(phases, station.Modules.Select(w => w.CurrentMount.LocalPhase).ToArray());
                Assert.That(lab.WeaponModules.GetModule(id, 0), Is.EqualTo(effect));
                var changedSettings = Read(); // No Tick: Snapshot observes effective values.
                CollectionAssert.AreEqual(moduleLabels,
                    changedSettings.Weapons.Select(w => w.Modules).ToArray(), "Module labels stay unchanged.");
                Assert.That(changedSettings.ConfigurationChanged, Is.True, effect);
                Assert.That(lab.TacticalPreset, Is.Null);
                Assert.That(changedSettings.Build, Is.EqualTo("EVEN"), "Keep the initial Build.");

                lab.ModuleRapidMultiplier = 2f;
                lab.ModulePierceBonus = 1;
                Assert.That(lab.WeaponModules.SetModule(id, 0, effect), Is.True);
                Assert.That(settingsGun.Settings.FireInterval, Is.EqualTo(fire));
                Assert.That(settingsGun.Settings.BurstInterval, Is.EqualTo(burst));
                Assert.That(settingsGun.Settings.BodyPierces, Is.EqualTo(pierces));
                Assert.That(Read().ConfigurationChanged, Is.True, "Restoring settings must not erase mixed history.");
                Assert.That(Read().Build, Is.EqualTo("EVEN"));
                Call("ApplyTacticalPreset", "EVEN");
                Call("BeginCombatTelemetry", "FreshEffective" + effect);
                Assert.That(Read().ConfigurationChanged, Is.False, "A new recording starts clean.");
                Assert.That(Read().Build, Is.EqualTo("EVEN"));
                lab.StopCombatTelemetry();
            }
            // Independent enemy-speed attribution: only the effective clamped value matters.
            RestoreModules();
            lab.SetSpeed(1f);
            Call("ApplyTacticalPreset", "EVEN");
            Assert.That(lab.TacticalPreset, Is.EqualTo("EVEN"));
            Call("BeginCombatTelemetry", "EnemySpeed");
            var beforeEnemySpeed = Read();
            Assert.That(beforeEnemySpeed.ConfigurationChanged, Is.False);
            Assert.That(beforeEnemySpeed.Build, Is.EqualTo("EVEN"));
            lab.SetSpeed(.5f);
            Assert.That(lab.SpeedMultiplier, Is.EqualTo(.5f));
            var changedEnemySpeed = Read(); // Snapshot detects the change without Tick.
            Assert.That(changedEnemySpeed.ConfigurationChanged, Is.True, "Effective enemy speed");
            Assert.That(lab.TacticalPreset, Is.Null);
            Assert.That(changedEnemySpeed.Build, Is.EqualTo(beforeEnemySpeed.Build));
            lab.SetSpeed(1f);
            Assert.That(Read().ConfigurationChanged, Is.True, "Restoring speed must not erase mixed history.");
            Assert.That(Read().Build, Is.EqualTo("EVEN"));
            Call("ApplyTacticalPreset", "EVEN");
            Call("BeginCombatTelemetry", "FreshEnemySpeed");
            Assert.That(Read().ConfigurationChanged, Is.False, "A new recording starts clean.");
            Assert.That(Read().Build, Is.EqualTo("EVEN"));

            lab.SetSpeed(.05f);
            Call("ApplyTacticalPreset", "EVEN");
            Call("BeginCombatTelemetry", "EquivalentEnemySpeed");
            Assert.That(Read().ConfigurationChanged, Is.False);
            lab.SetSpeed(.08f);
            Assert.That(lab.SpeedMultiplier, Is.EqualTo(.08f));
            Assert.That(Read().ConfigurationChanged, Is.False, "Both positive speeds clamp to 0.1.");
            Assert.That(lab.TacticalPreset, Is.EqualTo("EVEN"));
            Assert.That(Read().Build, Is.EqualTo("EVEN"));

            // Independent administrative conditions: no Tick or enemy simulation is needed.
            lab.SetSpeed(enemySpeed);
            lab.SetFrozen(false);
            lab.SetEnemiesInvincible(false);
            foreach (bool freeze in new[] { true, false })
            {
                string condition = freeze ? "EnemyFrozen" : "EnemiesInvincible";
                void SetCondition(bool value)
                {
                    if (freeze) lab.SetFrozen(value);
                    else lab.SetEnemiesInvincible(value);
                }
                Call("ApplyTacticalPreset", "EVEN");
                Call("BeginCombatTelemetry", condition);
                var initial = Read();
                Assert.That(initial.ConfigurationChanged, Is.False, condition);
                Assert.That(initial.Build, Is.EqualTo("EVEN"), condition);
                SetCondition(false);
                Assert.That(Read().ConfigurationChanged, Is.False, "Repeated unchanged value: " + condition);
                Assert.That(lab.TacticalPreset, Is.EqualTo("EVEN"), condition);

                SetCondition(true);
                Assert.That(freeze ? EnemyDebugAiFreeze.IsFrozen : lab.EnemiesInvincible, Is.True, condition);
                var changed = Read(); // Snapshot detects the condition without Tick.
                Assert.That(changed.ConfigurationChanged, Is.True, condition);
                Assert.That(lab.TacticalPreset, Is.Null, condition);
                Assert.That(changed.Build, Is.EqualTo(initial.Build), condition);
                SetCondition(false);
                Assert.That(Read().ConfigurationChanged, Is.True, "Restoring must not erase mixed history: " + condition);
                Assert.That(Read().Build, Is.EqualTo("EVEN"), condition);

                Call("ApplyTacticalPreset", "EVEN");
                Call("BeginCombatTelemetry", "Fresh" + condition);
                Assert.That(Read().ConfigurationChanged, Is.False, "New recording: " + condition);
                Assert.That(Read().Build, Is.EqualTo("EVEN"), condition);
                lab.StopCombatTelemetry();
            }
        }
        finally
        {
            lab.StopCombatTelemetry();
            lab.SetSpeed(enemySpeed);
            lab.SetFrozen(enemiesFrozen);
            lab.SetEnemiesInvincible(enemiesInvincible);
            radiusRing.State.Radius = baseRadius;
            lab.ModuleMultishotAngle = multishotAngle;
            lab.ModuleRapidMultiplier = rapidMultiplier;
            lab.ModulePierceBonus = pierceBonus;
            lab.SetFixedDirection(fixedDirection);
            lab.Player.SetRuntimeHealth(maxHealth, currentHealth);
            movement.SetRunUpgradeMoveSpeedMultiplier(moveSpeed);
            RestoreShooting();
            RestoreModules();
            Call("ApplyTacticalPreset", "EVEN");
            RestoreShooting();
            station.enabled = wasEnabled;
        }
        yield return null;
    }
    [UnityTest] public IEnumerator EditedPresetStartsWithCustomAttribution()
    {
        Call("ApplyTacticalPreset", "EVEN"); station.enabled = false;
        lab.WeaponModules.SetModule(station.Modules[0].StableModuleId, 0, "RAPID");
        Call("BeginCombatTelemetry", "Custom");
        Assert.That(Read().Build, Is.EqualTo("Своя сборка"), "An edited preset is no longer the reference preset.");
        yield return null;
    }
    [UnityTest] public IEnumerator FixedGeometryChangesRealHitsForFrontalAndSurroundingTargets()
    {
        bool wasEnabled = station.enabled;
        var center = station.GetComponent<OrbitalCenterShift>();
        Vector2 initialOffset = center.Offset;
        var phases = station.Rings.Select(r => r.State.CurrentPhase).ToArray();
        var advance = typeof(OrbitalCenterShift).GetMethod("Advance", Flags);
        float move = (float)typeof(OrbitalCenterShift).GetField("moveSpeed", Flags).GetValue(center);
        float returning = (float)typeof(OrbitalCenterShift).GetField("returnSpeed", Flags).GetValue(center);
        try
        {
            station.enabled = false;
            center.ResetOffset();
            var rows = new List<string> { "targets,layout,fired,hitShots,damage" };
            var hits = new Dictionary<(bool, string), int>();
            foreach (bool frontal in new[] { true, false })
            foreach (string build in new[] { "EVEN", "GROUPED" })
            {
                lab.ClearSandboxArena(); Call("ApplyTacticalPreset", build);
                var ring = station.Rings[0]; ring.State.CurrentPhase = 0; ring.Tick(0);
                float[] angles = frontal ? new[] { 0f, 45f, 90f, 135f } : new[] { 0f, 90f, 180f, 270f };
                foreach (float angle in angles)
                {
                    Vector2 point = (Vector2)station.transform.position + (Vector2)(Quaternion.Euler(0, 0, angle) * Vector2.right) * 5f;
                    lab.GetType().GetMethod("SpawnAt", Flags).Invoke(lab, new object[] { -3, point });
                }
                Physics2D.SyncTransforms(); Call("BeginCombatTelemetry", "Geometry");
                foreach (var gun in station.Modules.OfType<EnemyOrbitalLabWeapon>()) { Ready(gun); gun.Tick(.001f); }
                adapter.Tick(1f); var report = Read();
                Assert.That(report.Weapons.Sum(w => w.Fired), Is.EqualTo(4));
                hits[(frontal, build)] = report.Weapons.Sum(w => w.HitShots);
                rows.Add(FormattableString.Invariant($"{(frontal ? "front" : "surround")},{build},{report.Weapons.Sum(w => w.Fired)},{report.Weapons.Sum(w => w.HitShots)},{report.Weapons.Sum(w => w.Damage):F1}"));
            }
            string output = Path.GetFullPath(Path.Combine(Application.dataPath,
                "../Artifacts/GeneratedQA/OrbitalTactics/center-geometry-" + DateTime.UtcNow.ToString("yyyyMMddTHHmmssfff") + "-" + Guid.NewGuid().ToString("N")));
            Directory.CreateDirectory(output);
            File.WriteAllLines(Path.Combine(output, "geometry.csv"), rows);
            Assert.That(hits[(false, "EVEN")], Is.GreaterThan(hits[(false, "GROUPED")]), "The same four real projectiles must reach different bodies when only mount angles change.");
            MeasureCenterTranslation(center, advance, move, returning, output);
            yield return MeasureShiftAngularWindows();
        }
        finally
        {
            center.ResetOffset();
            if (initialOffset.sqrMagnitude > 0f && move > 0f)
                advance.Invoke(center, new object[] { initialOffset.normalized, true, initialOffset.magnitude / move });
            for (int i = 0; i < phases.Length; i++)
            { station.Rings[i].State.CurrentPhase = phases[i]; station.Rings[i].Tick(0f); }
            station.enabled = wasEnabled;
        }
        yield return null;
    }
    [Serializable] public sealed class MovingCenterSample
    {
        public float Seconds;
        public Vector2 Player, Center, Offset;
    }
    [Serializable] public sealed class MovingCenterShot
    {
        public float Seconds;
        public int WeaponId;
        public Vector2 Muzzle, Direction;
    }
    [Serializable] public sealed class MovingCenterCell
    {
        public string Policy, Configuration;
        public Vector2 InitialPlayer, InitialCenter, InitialVelocity, Target, Radial, Perpendicular;
        public float MaximumOffset, Step, WalkingSpeed;
        public int[] WeaponIds;
        public float[] InitialCooldowns, InitialPhases;
        public string[] Settings;
        public MovingCenterSample[] Path;
        public MovingCenterShot[] Shots;
        public Report Report;
    }
    [UnityTest] public IEnumerator MovingCenterTranslationChangesOutwardShotCoverage()
    {
        // Independent of the route/Shift experiments. No yield while manually simulating.
        lab.ClearSandboxArena(); Call("ApplyTacticalPreset", "GROUPED");
        var movement = lab.Player.GetComponent<CharacterMovement2D>();
        var body = lab.Player.GetComponent<Rigidbody2D>();
        var center = station.GetComponent<OrbitalCenterShift>();
        var guns = station.Modules.OfType<EnemyOrbitalLabWeapon>().OrderBy(g => g.CurrentMount.MountIndex).ToArray();
        Assert.That(guns.Length, Is.EqualTo(4));
        Assert.That(guns.All(g => g.Kind == OrbitalModuleKind.Pistol), Is.True);
        Assert.That(lab.FixedDirection, Is.True, "The experiment requires outward firing without target checks");
        Assert.That(guns.All(g => g.Settings.BurstShots <= 1 && g.Settings.ChargeSeconds == 0f), Is.True);
        var cooldown = typeof(OrbitalModuleRuntime).GetProperty("RuntimeCooldown", Flags);
        var advance = typeof(OrbitalCenterShift).GetMethod("Advance", Flags);
        var fixedUpdate = typeof(CharacterMovement2D).GetMethod("FixedUpdate", Flags);
        var configuration = typeof(EnemyOrbitalLabTelemetry).GetMethod("Configuration", Flags);
        var rings = station.Rings.ToArray();
        var phases = rings.Select(r => r.State.CurrentPhase).ToArray();
        var clocks = guns.Select(g => (float)cooldown.GetValue(g)).ToArray();
        var settings = guns.Select(g => JsonUtility.ToJson(g.Settings)).ToArray();
        var simulation = Physics2D.simulationMode;
        var intent = movement.MovementIntent;
        bool stationEnabled = station.enabled, movementEnabled = movement.enabled, paused = lab.Paused;
        float timeScale = Time.timeScale, fixedDelta = Time.fixedDeltaTime;
        Vector2 origin = body.position, velocity = body.linearVelocity, savedOffset = center.Offset;
        Vector3 playerPosition = lab.Player.transform.position;
        // Restore the controller's actual integrator, including its previous physics sample.
        string[] stateNames = { "currentVelocity", "previousPhysicsPosition", "moveInput", "hadMovementIntent", "measuredMovementSpeed",
            "hitKnockbackVelocity", "hitKnockbackTimeRemaining", "dashDirection", "dashTimeRemaining", "dashCooldownRemaining", "isDashing" };
        var fields = stateNames.Select(n => typeof(CharacterMovement2D).GetField(n, Flags)).ToArray();
        Assert.That(fields.All(f => f != null), Is.True);
        var state = fields.Select(f => f.GetValue(movement)).ToArray();
        Assert.That((bool)fields.Single(f => f.Name == "isDashing").GetValue(movement), Is.False, "Walking protocol requires no active dash");
        string output = Path.GetFullPath(Path.Combine(Application.dataPath,
            "../Artifacts/GeneratedQA/OrbitalTactics/moving-center-translation-" + DateTime.UtcNow.ToString("yyyyMMddTHHmmssfff") + "-" + Guid.NewGuid().ToString("N")));
        Directory.CreateDirectory(output);
        File.WriteAllText(Path.Combine(output, "protocol.txt"),
            "Experimental: GROUPED; 100 x .02s; same observed clocks/phases/settings. Target = initial muzzle + radial*5 + perpendicular*(MaximumOffset*.75).\n" +
            "Movement = perpendicular; RETURN vs TRANSLATE via Advance. Not physical RMB, steering slowdown, Shift or human experience.\n" +
            "No mandatory hit advantage. Failed comparability forbids an efficacy conclusion. No geometry tuning after results.\n");
        var cells = new List<MovingCenterCell>();
        var csv = new List<string> { "policy,id,fired,hitShots,misses,cancelled,pending,bodyHits,damage" };
        try
        {
            station.enabled = false;
            lab.SetPaused(false);
            Time.fixedDeltaTime = .02f;
            Physics2D.simulationMode = SimulationMode2D.Script;
            // FixedUpdate uses fixedDeltaTime internally; do not silently simulate a different step.
            Assert.That(Time.fixedDeltaTime, Is.EqualTo(.02f));
            center.ResetOffset();
            foreach (var ring in rings) ring.Tick(0f);
            Vector2 radial = ((Vector2)guns[0].CurrentMount.Transform.position - (Vector2)station.transform.position).normalized;
            Vector2 side = new Vector2(radial.y, -radial.x);
            Vector2 target = guns[0].WorldPosition + radial * 5f + side * (center.MaximumOffset * .75f);
            Assert.That(center.MaximumOffset, Is.GreaterThan(0f));
            string baseline = (string)configuration.Invoke(lab.Telemetry, null);
            foreach (string policy in new[] { "RETURN", "TRANSLATE" })
            {
                lab.ClearSandboxArena();
                for (int i = 0; i < fields.Length; i++) fields[i].SetValue(movement, state[i]);
                body.position = origin; body.linearVelocity = velocity;
                lab.Player.transform.position = playerPosition;
                movement.MovementIntent = () => side;
                center.ResetOffset();
                for (int i = 0; i < rings.Length; i++) { rings[i].State.CurrentPhase = phases[i]; rings[i].Tick(0f); }
                for (int i = 0; i < guns.Length; i++) cooldown.SetValue(guns[i], clocks[i]);
                var before = Object.FindObjectsByType<EnemyHealth>(FindObjectsSortMode.None);
                Call("SpawnAt", -3, target);
                var dummy = Object.FindObjectsByType<EnemyHealth>(FindObjectsSortMode.None).Single(e => !before.Contains(e));
                foreach (var ai in dummy.GetComponents<EnemyMovement>()) ai.enabled = false;
                dummy.GetComponent<CombatFeelTestDummy>().Invulnerable = true;
                var dummyBody = dummy.GetComponent<Rigidbody2D>();
                Assert.That(dummyBody, Is.Not.Null);
                dummyBody.constraints = RigidbodyConstraints2D.FreezeAll;
                dummyBody.linearVelocity = Vector2.zero;
                Physics2D.SyncTransforms();
                Assert.That(Vector2.Distance(dummy.transform.position, target), Is.LessThan(.0001f), "No clamped target geometry");
                CollectionAssert.AreEqual(clocks, guns.Select(g => (float)cooldown.GetValue(g)).ToArray());
                CollectionAssert.AreEqual(phases, rings.Select(r => r.State.CurrentPhase).ToArray());
                Assert.That((string)configuration.Invoke(lab.Telemetry, null), Is.EqualTo(baseline));
                Call("BeginCombatTelemetry", "Moving center translation");
                var path = new List<MovingCenterSample>();
                var shots = new List<MovingCenterShot>();
                float seconds = 0f;
                void Sample() => path.Add(new MovingCenterSample { Seconds = seconds, Player = body.position,
                    Center = station.transform.position, Offset = center.Offset });
                Sample();
                Action<long, int, Vector2> capture = (sequence, id, direction) =>
                {
                    var gun = guns.Single(g => g.StableModuleId == id);
                    shots.Add(new MovingCenterShot { Seconds = seconds, WeaponId = id, Muzzle = gun.WorldPosition, Direction = direction });
                };
                adapter.LabProjectileSpawned += capture;
                try
                {
                    for (int tick = 0; tick < 100; tick++)
                    {
                        seconds = (tick + 1) * .02f;
                        fixedUpdate.Invoke(movement, null);
                        Physics2D.Simulate(.02f);
                        advance.Invoke(center, new object[] { side, policy == "TRANSLATE", .02f });
                        foreach (var ring in rings) ring.Tick(.02f);
                        foreach (var gun in guns) gun.Tick(.02f);
                        adapter.Tick(.02f);
                        lab.Telemetry.Tick(.02f);
                        Sample();
                    }
                }
                finally { adapter.LabProjectileSpawned -= capture; }
                var cell = new MovingCenterCell { Policy = policy, Configuration = baseline,
                    InitialPlayer = origin, InitialCenter = path[0].Center, InitialVelocity = velocity, Target = target, Radial = radial, Perpendicular = side,
                    MaximumOffset = center.MaximumOffset, Step = .02f, WalkingSpeed = movement.WalkingSpeed, WeaponIds = guns.Select(g => g.StableModuleId).ToArray(),
                    InitialCooldowns = clocks, InitialPhases = phases, Settings = settings,
                    Path = path.ToArray(), Shots = shots.ToArray(), Report = Read() };
                cells.Add(cell);
                File.WriteAllText(Path.Combine(output, policy + ".json"), JsonUtility.ToJson(cell, true));
                foreach (var row in cell.Report.Weapons)
                    csv.Add(FormattableString.Invariant($"{policy},{row.WeaponId},{row.Fired},{row.HitShots},{row.Misses},{row.Cancelled},{row.Pending},{row.BodyHits},{row.Damage:R}"));
                File.WriteAllLines(Path.Combine(output, "per-gun.csv"), csv);
                Assert.That(cell.Report.ConfigurationChanged, Is.False);
                Assert.That(cell.Report.Weapons.Length, Is.EqualTo(4));
                Assert.That(shots.Count, Is.GreaterThan(0), "The observed clocks must permit real shots during this pass");
                Assert.That(cell.Report.Elapsed, Is.EqualTo(2f).Within(.00001f));
                Assert.That(cell.Path.All(p => p.Offset.magnitude <= center.MaximumOffset + .0001f), Is.True);
                Assert.That(Vector2.Dot(body.position - origin, side), Is.GreaterThan(.1f));
                Assert.That(Vector2.Distance(dummy.transform.position, target), Is.LessThan(.0001f));
                CollectionAssert.AreEqual(settings, guns.Select(g => JsonUtility.ToJson(g.Settings)).ToArray());
                Assert.That(Time.timeScale, Is.EqualTo(1f));
                Assert.That(Time.fixedDeltaTime, Is.EqualTo(fixedDelta));
                foreach (var row in cell.Report.Weapons)
                {
                    Assert.That(row.Fired, Is.EqualTo(row.HitShots + row.Misses + row.Cancelled + row.Pending));
                    Assert.That(shots.Count(s => s.WeaponId == row.WeaponId), Is.EqualTo(row.Fired));
                }
            }
            for (int i = 0; i < cells[0].Path.Length; i++)
                Assert.That(Vector2.Distance(cells[0].Path[i].Player, cells[1].Path[i].Player), Is.LessThan(.0001f), "Player trajectory diverged at " + i);
            foreach (var row in cells[0].Report.Weapons)
                Assert.That(cells[1].Report.Weapons.Single(w => w.WeaponId == row.WeaponId).Fired, Is.EqualTo(row.Fired), "Per-gun shot budget");
            Assert.That(cells[0].Path.All(p => p.Offset.sqrMagnitude < .00000001f), Is.True);
            Assert.That(Vector2.Dot(cells[1].Path.Last().Offset, cells[1].Perpendicular), Is.GreaterThan(0f));
            File.WriteAllText(Path.Combine(output, "comparison.txt"),
                "Comparable controlled passes; RETURN hitShots=" + cells[0].Report.Weapons.Sum(w => w.HitShots) +
                "; TRANSLATE hitShots=" + cells[1].Report.Weapons.Sum(w => w.HitShots) +
                ". Observation only; equal hits are a valid negative result. MANUAL_PLAYTEST_REQUIRED.\n");
        }
        catch (Exception failure)
        {
            File.WriteAllText(Path.Combine(output, "failure.txt"), "No comparative efficacy conclusion.\n" + failure);
            throw;
        }
        finally
        {
            lab.Telemetry.Stop(); lab.ClearSandboxArena();
            Physics2D.simulationMode = simulation;
            movement.MovementIntent = intent; movement.enabled = movementEnabled;
            lab.SetPaused(paused); Time.timeScale = timeScale;
            Time.fixedDeltaTime = fixedDelta;
            body.position = origin; body.linearVelocity = velocity; lab.Player.transform.position = playerPosition;
            for (int i = 0; i < fields.Length; i++) fields[i].SetValue(movement, state[i]);
            center.ResetOffset(); typeof(OrbitalCenterShift).GetProperty("Offset", Flags).SetValue(center, savedOffset); center.Refresh();
            for (int i = 0; i < rings.Length; i++) { rings[i].State.CurrentPhase = phases[i]; rings[i].Tick(0f); }
            for (int i = 0; i < guns.Length; i++) cooldown.SetValue(guns[i], clocks[i]);
            station.enabled = stationEnabled; Physics2D.SyncTransforms();
        }
        TestContext.WriteLine("Moving center translation artifacts: " + output);
        yield return null;
    }
    [Serializable] public sealed class CenterGeometryShot
    {
        public int WeaponId;
        public Vector2 Muzzle, Direction;
    }
    [Serializable] public sealed class CenterGeometryCell
    {
        public string State, Configuration, Outcome;
        public Vector2 Center, Offset, Target;
        public float AdvanceSeconds, MaximumOffset, TargetRadiusBound;
        public float[] InitialRayDistances, Phases, Cooldowns;
        public string[] WeaponSettings;
        public bool OutsideInitialRayEnvelopes;
        public CenterGeometryShot[] Shots;
        public Report Report;
    }
    void MeasureCenterTranslation(OrbitalCenterShift center, MethodInfo advance, float move, float returning, string output)
    {
        lab.ClearSandboxArena(); Call("ApplyTacticalPreset", "GROUPED");
        center.ResetOffset();
        foreach (var ring in station.Rings) { ring.State.CurrentPhase = 0f; ring.Tick(0f); }
        var guns = station.Modules.OfType<EnemyOrbitalLabWeapon>().OrderBy(g => g.CurrentMount.MountIndex).ToArray();
        Assert.That(guns.Length, Is.EqualTo(4));
        Assert.That(move, Is.GreaterThan(0f)); Assert.That(returning, Is.GreaterThan(0f));
        Assert.That(center.MaximumOffset, Is.GreaterThan(0f));
        Vector2 origin = station.transform.position;
        Vector2 radial = ((Vector2)guns[0].CurrentMount.Transform.position - origin).normalized;
        Vector2 side = new Vector2(radial.y, -radial.x);
        float distance = center.MaximumOffset * .75f;
        // Predeclared geometry, never searched or adjusted in response to hit results.
        Vector2 target = guns[0].WorldPosition + radial * 3f + side * distance;
        var initialMuzzles = guns.Select(g => g.WorldPosition).ToArray();
        var radialDirections = guns.Select(g => ((Vector2)g.CurrentMount.Transform.position - origin).normalized).ToArray();
        var rayDistances = initialMuzzles.Select((m, i) =>
        {
            Vector2 delta = target - m;
            return (delta - radialDirections[i] * Mathf.Max(0f, Vector2.Dot(delta, radialDirections[i]))).magnitude;
        }).ToArray();
        var cooldown = typeof(OrbitalModuleRuntime).GetProperty("RuntimeCooldown", Flags);
        var savedClocks = guns.Select(g => (float)cooldown.GetValue(g)).ToArray();
        var settings = guns.Select(g => JsonUtility.ToJson(g.Settings)).ToArray();
        var configurationMethod = lab.Telemetry.GetType().GetMethod("Configuration", Flags);
        Assert.That(configurationMethod, Is.Not.Null, "Telemetry configuration accessor must exist.");
        string Configuration() => (string)configurationMethod.Invoke(lab.Telemetry, null);
        string configuration = Configuration();
        var csv = new List<string> { "state,advanceSeconds,offsetX,offsetY,fired,hitShots,damage" };
        var measuredHits = new List<int>();
        bool geometricallyBlind = true;
        try
        {
            foreach (string state in new[] { "origin", "translated", "returned" })
            {
                lab.ClearSandboxArena();
                float seconds = state == "origin" ? 0f : distance / (state == "translated" ? move : returning);
                if (state != "origin") advance.Invoke(center, new object[] { side, state == "translated", seconds });
                foreach (var ring in station.Rings) ring.Tick(0f);
                Assert.That(center.Offset.magnitude, Is.LessThanOrEqualTo(center.MaximumOffset + .0001f));
                Vector2 expected = state == "translated" ? side * distance : Vector2.zero;
                Assert.That(Vector2.Distance(center.Offset, expected), Is.LessThan(.0001f));
                Assert.That(Vector2.Distance((Vector2)station.transform.position, origin + expected), Is.LessThan(.0001f));
                Assert.That(Configuration(), Is.EqualTo(configuration));
                CollectionAssert.AreEqual(settings, guns.Select(g => JsonUtility.ToJson(g.Settings)));
                var previous = Object.FindObjectsByType<EnemyHealth>(FindObjectsSortMode.None);
                Call("SpawnAt", -3, target);
                var body = Object.FindObjectsByType<EnemyHealth>(FindObjectsSortMode.None).Single(e => !previous.Contains(e));
                Physics2D.SyncTransforms();
                Assert.That(Vector2.Distance(body.transform.position, target), Is.LessThan(.0001f), "SpawnAt must not clamp this predeclared target.");
                Vector2 actualTarget = body.transform.position;
                float targetBound = body.GetComponentsInChildren<Collider2D>().Max(c =>
                    Vector2.Distance(c.bounds.center, actualTarget) + ((Vector2)c.bounds.extents).magnitude);
                bool outside = guns.Select((g, i) => rayDistances[i] > targetBound + g.Settings.ProjectileRadius +
                    Vector2.Distance(initialMuzzles[i], target) * Mathf.Sin(g.Settings.SpreadDegrees * .5f * Mathf.Deg2Rad)).All(v => v);
                geometricallyBlind &= outside;
                Call("BeginCombatTelemetry", "Center geometry");
                foreach (var gun in guns) Ready(gun);
                var shots = new List<CenterGeometryShot>();
                Action<long, int, Vector2> capture = (sequence, id, direction) => shots.Add(new CenterGeometryShot
                { WeaponId = id, Muzzle = guns.Single(g => g.StableModuleId == id).WorldPosition, Direction = direction });
                var clocks = guns.Select(g => (float)cooldown.GetValue(g)).ToArray();
                adapter.LabProjectileSpawned += capture;
                try { foreach (var gun in guns) gun.Tick(.001f); }
                finally { adapter.LabProjectileSpawned -= capture; }
                adapter.Tick(1f);
                var report = Read();
                var cell = new CenterGeometryCell
                {
                    State = state, Configuration = configuration, Center = station.transform.position, Offset = center.Offset,
                    Target = actualTarget, AdvanceSeconds = seconds, MaximumOffset = center.MaximumOffset,
                    TargetRadiusBound = targetBound, InitialRayDistances = rayDistances,
                    OutsideInitialRayEnvelopes = outside, WeaponSettings = settings,
                    Phases = station.Rings.Select(r => r.State.CurrentPhase).ToArray(), Cooldowns = clocks,
                    Shots = shots.ToArray(), Report = report,
                    Outcome = report.Weapons.Sum(w => w.HitShots) > 0 ? "hit" : "no-hit"
                };
                File.WriteAllText(Path.Combine(output, state + ".json"), JsonUtility.ToJson(cell, true));
                csv.Add(FormattableString.Invariant($"{state},{seconds:R},{center.Offset.x:R},{center.Offset.y:R},{report.Weapons.Sum(w => w.Fired)},{report.Weapons.Sum(w => w.HitShots)},{report.Weapons.Sum(w => w.Damage):R}"));
                File.WriteAllLines(Path.Combine(output, "center-geometry.csv"), csv);
                measuredHits.Add(report.Weapons.Sum(w => w.HitShots));
                Assert.That(report.Weapons.Sum(w => w.Fired), Is.EqualTo(4));
                Assert.That(shots.Count, Is.EqualTo(4));
                CollectionAssert.AreEquivalent(guns.Select(g => g.StableModuleId), shots.Select(s => s.WeaponId));
                Assert.That(clocks.All(c => c == 0f), Is.True);
                Assert.That(cell.Phases.All(p => p == 0f), Is.True);
                for (int i = 0; i < guns.Length; i++)
                    Assert.That(Vector2.Distance(guns[i].WorldPosition, initialMuzzles[i] + expected), Is.LessThan(.0001f));
            }
            File.WriteAllText(Path.Combine(output, "conclusion.txt"),
                geometricallyBlind && measuredHits[0] == 0 && measuredHits[1] > 0 && measuredHits[2] == 0
                    ? "SUPPORTED in this fixed geometry only: origin miss, translated hit, returned miss. Not a tactical or physical RMB evaluation."
                    : "NOT SUPPORTED by these three fixed cells. No parameters or target positions were retuned. Inspect JSON geometry and actual directions; no tactical inference.");
            Debug.Log("[OrbitalTactics] Experimental center translation (not physical RMB): " + output);
        }
        finally
        {
            for (int i = 0; i < guns.Length; i++) cooldown.SetValue(guns[i], savedClocks[i]);
        }
    }
    [Serializable] public sealed class ShiftWindowShot
    {
        public int WeaponId;
        public float Seconds, Phase;
        public Vector2 Muzzle, Direction;
    }
    [Serializable] public sealed class ShiftWindowCell
    {
        public string Window, Policy, Limitations;
        public Vector2 Center, Target;
        public float Step, Duration, WorldScale, Multiplier;
        public float[] InitialPhases, FinalPhases, ReadyClocks, Speeds;
        public int[] Directions;
        public string[] WeaponSettings;
        public ShiftWindowShot[] Shots;
        public Report Report;
    }
    IEnumerator MeasureShiftAngularWindows()
    {
        lab.ClearSandboxArena(); Call("ApplyTacticalPreset", "GROUPED");
        var center = station.GetComponent<OrbitalCenterShift>();
        center.ResetOffset();
        var guns = station.Modules.OfType<EnemyOrbitalLabWeapon>().OrderBy(g => g.CurrentMount.MountIndex).ToArray();
        var rings = station.Rings.ToArray();
        var phases = rings.Select(r => r.Phase).ToArray();
        var multipliers = rings.Select(r => r.LabRotationMultiplier).ToArray();
        var cooldown = typeof(OrbitalModuleRuntime).GetProperty("RuntimeCooldown", Flags);
        var clocks = guns.Select(g => (float)cooldown.GetValue(g)).ToArray();
        var settings = guns.Select(g => JsonUtility.ToJson(g.Settings)).ToArray();
        var updateInput = station.GetType().GetMethod("UpdateRightMouse", Flags);
        var releaseField = lab.CombatV2.GetType().GetField("needsRelease", Flags);
        bool needsRelease = (bool)releaseField.GetValue(lab.CombatV2);
        bool slowing = lab.CombatV2.IsSlowingOrbits, held = station.DebugBulletTimeHeld;
        bool paused = lab.Paused, enabled = station.enabled;
        float scale = Time.timeScale;
        var random = UnityEngine.Random.state;
        string output = Path.GetFullPath(Path.Combine(Application.dataPath,
            "../Artifacts/GeneratedQA/OrbitalTactics/shift-window-" + DateTime.UtcNow.ToString("yyyyMMddTHHmmssfff") + "-" + Guid.NewGuid().ToString("N")));
        Directory.CreateDirectory(output);
        const float step = .02f, duration = 2f;
        var csv = new List<string> { "window,policy,multiplier,fired,hitShots,damage" };
        var fired = new List<int>();
        var hitCounts = new List<int>();
        int[] referenceGunCounts = null;
        var inputDiagnostics = new List<string>();
        void CaptureInput(string stage)
        {
            var input = station.InputOwner;
            inputDiagnostics.Add(FormattableString.Invariant(
                $"{stage}: frame={Time.frameCount}, Application.isFocused={Application.isFocused}, OwnerPresent={station.Owner != null}, Owner.CanControl={station.Owner?.CanControl}, IsRunEnded={station.IsRunEnded}, Time.timeScale={Time.timeScale:R}, InputOwnerPresent={input != null}, CanUseDebugPlacement={input?.CanUseDebugPlacement}, IsIdle={input?.IsIdle}, IsDebugPlacementActive={station.GetType().GetProperty("IsDebugPlacementActive", Flags).GetValue(station)}, UsesBulletTime={lab.CombatV2.UsesBulletTime}, needsRelease={releaseField.GetValue(lab.CombatV2)}, DebugBulletTimeHeld={station.DebugBulletTimeHeld}, IsSlowingOrbits={lab.CombatV2.IsSlowingOrbits}, IsGameplayPointerBlocked={input?.IsGameplayPointerBlocked}, IsRewardQueueIdle={input?.IsRewardQueueIdle}, consumedFrame={(input == null ? null : input.GetType().GetField("consumedFrame", Flags).GetValue(input))}, multipliers=[{string.Join(",", rings.Select(r => r.LabRotationMultiplier.ToString("R", System.Globalization.CultureInfo.InvariantCulture)))}]"));
        }
        void UpdateInput(string stage)
        {
            CaptureInput(stage + "/before");
            updateInput.Invoke(station, new object[] { step });
            CaptureInput(stage + "/after");
        }
        try
        {
            lab.SetPaused(false); station.enabled = false;
            CaptureInput("prepared");
            // Let input consumption from preset/pause preparation expire naturally.
            // Automatic station Update stays disabled; eligibility is never overridden.
            yield return null;
            CaptureInput("next-frame");
            foreach (var ring in rings) { ring.State.CurrentPhase = 0f; ring.Tick(0f); }
            Vector2 origin = station.transform.position;
            Vector2 radial = ((Vector2)guns[0].CurrentMount.Transform.position - origin).normalized;
            Vector2 initialTarget = guns[0].WorldPosition + radial * 3f;
            // Both target positions are fixed before any measured shots. This is geometry,
            // not a search for an angle that favours either policy.
            float oneSecondAngle = guns[0].CurrentMount.Ring.RotationSpeed * guns[0].CurrentMount.Ring.Direction;
            Vector2 laterTarget = origin + (Vector2)(Quaternion.Euler(0f, 0f, oneSecondAngle) * (initialTarget - origin));
            foreach (bool later in new[] { false, true })
            foreach (bool hold in new[] { false, true })
            {
                lab.ClearSandboxArena();
                UnityEngine.Random.state = random;
                foreach (var ring in rings) { ring.State.CurrentPhase = 0f; ring.Tick(0f); }
                string inputCell = (later ? "after-one-second" : "initial-ray") + (hold ? "/HOLD" : "/OFF");
                station.DebugBulletTimeHeld = false;
                UpdateInput(inputCell + "/release");
                station.DebugBulletTimeHeld = hold;
                UpdateInput(inputCell + "/policy");
                float expectedMultiplier = hold ? .4f : 1f;
                if (hold && !lab.CombatV2.IsSlowingOrbits)
                {
                    File.WriteAllText(Path.Combine(output, "limitation.txt"),
                        "HOLD unavailable through DebugBulletTimeHeld/UpdateRightMouse. See input-conditions.txt for actual release/hold eligibility and state. No focus or input eligibility bypass. Partial cells only; no hit comparison. MANUAL_PLAYTEST_REQUIRED.");
                    Assert.Inconclusive("Real lab Shift input unavailable; see " + output);
                }
                foreach (var ring in rings) Assert.That(ring.LabRotationMultiplier, Is.EqualTo(expectedMultiplier));
                Vector2 target = later ? laterTarget : initialTarget;
                var previous = Object.FindObjectsByType<EnemyHealth>(FindObjectsSortMode.None);
                Call("SpawnAt", -3, target);
                var body = Object.FindObjectsByType<EnemyHealth>(FindObjectsSortMode.None).Single(e => !previous.Contains(e));
                Physics2D.SyncTransforms();
                Assert.That(Vector2.Distance(body.transform.position, target), Is.LessThan(.0001f));
                Call("BeginCombatTelemetry", "Shift angular window");
                foreach (var gun in guns) Ready(gun);
                var readyClocks = guns.Select(g => (float)cooldown.GetValue(g)).ToArray();
                var initialPhases = rings.Select(r => r.Phase).ToArray();
                var shots = new List<ShiftWindowShot>();
                float seconds = 0f;
                Action<long, int, Vector2> capture = (sequence, id, direction) =>
                {
                    var gun = guns.Single(g => g.StableModuleId == id);
                    shots.Add(new ShiftWindowShot { WeaponId = id, Seconds = seconds,
                        Phase = gun.CurrentMount.Ring.Phase, Muzzle = gun.WorldPosition, Direction = direction });
                };
                adapter.LabProjectileSpawned += capture;
                try
                {
                    for (int tick = 0; tick < 100; tick++)
                    {
                        seconds = (tick + 1) * step;
                        UpdateInput(inputCell + "/tick-" + tick);
                        foreach (var ring in rings)
                        {
                            Assert.That(ring.LabRotationMultiplier, Is.EqualTo(expectedMultiplier));
                            float before = ring.Phase;
                            ring.Tick(step);
                            float expectedPhase = Mathf.Repeat(before + ring.RotationSpeed * ring.Direction * step * expectedMultiplier, 360f);
                            Assert.That(Mathf.Abs(Mathf.DeltaAngle(expectedPhase, ring.Phase)), Is.LessThan(.001f));
                        }
                        foreach (var gun in guns) gun.Tick(step);
                        adapter.Tick(step);
                        Assert.That(Time.timeScale, Is.EqualTo(1f), "Shift must not slow world time.");
                    }
                }
                finally { adapter.LabProjectileSpawned -= capture; }
                CollectionAssert.AreEqual(settings, guns.Select(g => JsonUtility.ToJson(g.Settings)));
                Assert.That(initialPhases.All(p => p == 0f), Is.True);
                Assert.That(readyClocks.All(c => c == 0f), Is.True);
                var report = Read();
                int count = report.Weapons.Sum(w => w.Fired), hits = report.Weapons.Sum(w => w.HitShots);
                var cell = new ShiftWindowCell
                {
                    Window = later ? "after-one-second" : "initial-ray", Policy = hold ? "HOLD" : "OFF",
                    Center = station.transform.position, Target = body.transform.position,
                    Step = step, Duration = duration, WorldScale = Time.timeScale, Multiplier = expectedMultiplier,
                    InitialPhases = initialPhases, FinalPhases = rings.Select(r => r.Phase).ToArray(), ReadyClocks = readyClocks,
                    Speeds = rings.Select(r => r.RotationSpeed).ToArray(), Directions = rings.Select(r => r.Direction).ToArray(),
                    WeaponSettings = settings, Shots = shots.ToArray(), Report = report,
                    Limitations = "Fixed stationary target; scripted fixed steps; no WASD, physical Shift or human tactical evaluation. Two-second snapshot includes projectiles still in flight; no post-window drain. Targets and parameters not retuned."
                };
                File.WriteAllText(Path.Combine(output, cell.Window + "-" + cell.Policy + ".json"), JsonUtility.ToJson(cell, true));
                csv.Add(FormattableString.Invariant($"{cell.Window},{cell.Policy},{expectedMultiplier:R},{count},{hits},{report.Weapons.Sum(w => w.Damage):R}"));
                File.WriteAllLines(Path.Combine(output, "shift-window.csv"), csv);
                fired.Add(count); hitCounts.Add(hits);
                Assert.That(count, Is.EqualTo(shots.Count));
                Assert.That(count, Is.GreaterThan(0));
                Assert.That(count, Is.EqualTo(fired[0]), "Shift must not change weapon clocks or projectile counts.");
                var gunCounts = guns.Select(g => report.Weapons.Single(w => w.WeaponId == g.StableModuleId).Fired).ToArray();
                if (referenceGunCounts == null) referenceGunCounts = gunCounts;
                CollectionAssert.AreEqual(referenceGunCounts, gunCounts, "Each weapon must retain its own projectile count across all four cells.");
                Assert.That(Vector2.Distance(body.transform.position, target), Is.LessThan(.0001f));
            }
            File.WriteAllText(Path.Combine(output, "conclusion.txt"),
                (hitCounts[0] != hitCounts[1] || hitCounts[2] != hitCounts[3]
                    ? "SUPPORTED only in these fixed angular windows: OFF/HOLD changed HitShots."
                    : "NOT SUPPORTED in these fixed angular windows: OFF/HOLD produced equal HitShots.") +
                " No tactical advantage inferred. Two-second snapshot; pending flights not drained. No retuning. Physical input and WASD: MANUAL_PLAYTEST_REQUIRED.");
            Debug.Log("[OrbitalTactics] Experimental Shift angular window: " + output);
        }
        finally
        {
            lab.SetPaused(paused); Time.timeScale = scale;
            station.DebugBulletTimeHeld = held; lab.CombatV2.Suspend();
            releaseField.SetValue(lab.CombatV2, needsRelease);
            lab.CombatV2.GetType().GetProperty("IsSlowingOrbits", Flags).SetValue(lab.CombatV2, slowing);
            for (int i = 0; i < rings.Length; i++)
            { rings[i].LabRotationMultiplier = multipliers[i]; rings[i].State.CurrentPhase = phases[i]; rings[i].Tick(0f); }
            for (int i = 0; i < guns.Length; i++) cooldown.SetValue(guns[i], clocks[i]);
            UnityEngine.Random.state = random; station.enabled = enabled;
            File.WriteAllLines(Path.Combine(output, "input-conditions.txt"), inputDiagnostics);
        }
    }
    const string ManualKey = "Subject42.OrbitalTactics.PrepareManual";
    [UnityTest] public IEnumerator CaptureThreeBuildsAtTheSameMixedScenarioStart()
    {
        typeof(EnemyOrbitalLabSandboxTests).GetMethod("SetGameView", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, null);
        string output = Path.GetFullPath(Path.Combine(Application.dataPath, "../Artifacts/GeneratedQA/OrbitalTactics"));
        var random = UnityEngine.Random.state;
        try
        {
            foreach (string build in new[] { "EVEN", "GROUPED", "MIXED" })
            {
                lab.SetPaused(true); Call("ApplyTacticalPreset", build);
                foreach (var ring in station.Rings) { ring.State.CurrentPhase = 0f; ring.Tick(0f); }
                UnityEngine.Random.InitState(42421028); lab.StartSandboxEncounter(2); lab.SetSandboxSpawning(false); lab.SetSandboxTab(2);
                Assert.That(lab.AliveEnemies, Is.EqualTo(12));
                yield return null; yield return null;
                string path = Path.Combine(output, build.ToLowerInvariant() + ".png");
                if (File.Exists(path)) File.Delete(path);
                ScreenCapture.CaptureScreenshot(path);
                yield return CoreTestSupport.Await(() => File.Exists(path));
                var picture = new Texture2D(2, 2); picture.LoadImage(File.ReadAllBytes(path));
                Assert.That(picture.width, Is.EqualTo(1600)); Assert.That(picture.height, Is.EqualTo(900)); Object.Destroy(picture);
            }
        }
        finally { UnityEngine.Random.state = random; }
    }
    public static void OpenForManualTesting()
    {
        SessionState.SetBool(ManualKey, true); PrepareAfterReload();
        EnemyOrbitalLabSandboxTests.OpenForManualTesting();
    }
    [InitializeOnLoadMethod] static void PrepareAfterReload()
    {
        if (!SessionState.GetBool(ManualKey, false)) return;
        EditorApplication.update -= PrepareManual; EditorApplication.update += PrepareManual;
    }
    static void PrepareManual()
    {
        if (!EditorApplication.isPlaying) return;
        var current = Object.FindFirstObjectByType<EnemyOrbitalLabController>();
        if (current == null || !current.Ready || !current.SandboxReady) return;
        EditorApplication.update -= PrepareManual; SessionState.SetBool(ManualKey, false);
        current.ApplyTacticalPreset("MIXED"); current.StartSandboxEncounter(2);
        current.SetSandboxSpawning(false); current.SetPaused(true); current.SetSandboxTab(2);
        Debug.Log("[OrbitalTactics] Mixed build and mixed encounter ready on pause at 1600x900.");
    }
}
#endif
