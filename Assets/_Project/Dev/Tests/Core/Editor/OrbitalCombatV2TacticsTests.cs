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
    [Serializable] public sealed class Row { public int WeaponId, Fired, HitShots, Misses, Cancelled, Pending, BodyHits, ShieldBlocks, Kills; public float Damage; public string Name; }
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
        yield return null;
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
        Ready(guns[0]); guns[0].Tick(.001f); adapter.Tick(1f);
        Ready(guns[1]); guns[1].Tick(.001f); adapter.Tick(1f);
        Ready(guns[3]); guns[3].Tick(.001f); adapter.DebugClearProjectiles();
        Ready(guns[0]); guns[0].Tick(.001f); adapter.Tick(1f);
        var report = Read(); var first = report.Weapons.Single(w => w.WeaponId == ids[0]);
        Assert.That(first.Fired, Is.EqualTo(2)); Assert.That(first.HitShots, Is.EqualTo(2));
        Assert.That(first.BodyHits, Is.EqualTo(3)); Assert.That(first.Damage, Is.EqualTo(21f)); Assert.That(first.Kills, Is.EqualTo(1));
        Assert.That(report.Weapons.Single(w => w.WeaponId == ids[1]).Misses, Is.EqualTo(1));
        Assert.That(report.Weapons.Single(w => w.WeaponId == ids[3]).Cancelled, Is.EqualTo(1));
        foreach (var row in report.Weapons) Assert.That(row.Fired, Is.EqualTo(row.HitShots + row.Misses + row.Cancelled + row.Pending));
        Assert.That(report.SurvivalApplicable, Is.False);
        yield return null;
    }
    [UnityTest] public IEnumerator CompareThreeBuildsInThreeExistingScenarios()
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
                station.Core.Reset();
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
        station.enabled = false; lab.SetPaused(false);
        var movement = lab.Player.GetComponent<CharacterMovement2D>(); var body = lab.Player.GetComponent<Rigidbody2D>();
        var simulation = Physics2D.simulationMode; Vector2 origin = body.position;
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
        }
        finally { Physics2D.simulationMode = simulation; movement.MovementIntent = null; }
        var center = station.GetComponent<OrbitalCenterShift>();
        typeof(OrbitalCenterShift).GetMethod("Advance", Flags).Invoke(center, new object[] { Vector2.right, true, .1f });
        Assert.That(center.Offset.x, Is.GreaterThan(0f));
        lab.CombatV2.RouteShift(.01f, false, true); lab.CombatV2.RouteShift(.01f, true, true);
        float phase = station.Rings[0].Phase;
        station.Rings[0].Tick(.5f);
        Assert.That(Mathf.Abs(Mathf.DeltaAngle(phase, station.Rings[0].Phase)), Is.EqualTo(station.Rings[0].RotationSpeed * .5f * .4f).Within(.001f));
        Assert.That(Time.timeScale, Is.EqualTo(1f));
        yield return null;
    }
    [UnityTest] public IEnumerator NewGunDuringRecordingHasItsOwnCompleteRow()
    {
        Call("ApplyTacticalPreset", "EVEN"); station.enabled = false;
        Call("BeginCombatTelemetry", "Editing");
        lab.RemoveSandboxWeapon(); Assert.That(lab.InstallSandboxWeapon(1), Is.True);
        var gun = station.Modules.OfType<EnemyOrbitalLabWeapon>().Single(g => g.Kind == OrbitalModuleKind.Shotgun);
        Ready(gun); gun.Tick(.001f); adapter.Tick(1f);
        var report = Read();
        Assert.That(report.Weapons.Any(w => w.WeaponId == gun.StableModuleId), Is.True, "A new ID must never silently disappear from totals.");
        Assert.That(report.Weapons.Single(w => w.WeaponId == gun.StableModuleId).Fired, Is.EqualTo(7));
        Assert.That(report.ConfigurationChanged, Is.True);
        yield return null;
    }
    [UnityTest] public IEnumerator ConfigurationHistoryAndCustomLabelSurviveRestoringTheOriginalModules()
    {
        Call("ApplyTacticalPreset", "EVEN"); station.enabled = false;
        var gun = station.Modules[0];
        Call("BeginCombatTelemetry", "History");
        lab.WeaponModules.SetModule(gun.StableModuleId, 0, "RAPID");
        lab.Telemetry.Tick(.02f);
        lab.WeaponModules.SetModule(gun.StableModuleId, 0, "EMPTY");
        var history = Read();
        lab.WeaponModules.SetModule(gun.StableModuleId, 0, "RAPID");
        Call("BeginCombatTelemetry", "Custom"); var custom = Read();
        Assert.That(history.ConfigurationChanged, Is.True, "Running a changed build must remain marked after restoring it.");
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
        station.enabled = false;
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
        string output = Path.GetFullPath(Path.Combine(Application.dataPath, "../Artifacts/GeneratedQA/OrbitalTactics/geometry.csv"));
        File.WriteAllLines(output, rows);
        Assert.That(hits[(false, "EVEN")], Is.GreaterThan(hits[(false, "GROUPED")]), "The same four real projectiles must reach different bodies when only mount angles change.");
        yield return null;
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
