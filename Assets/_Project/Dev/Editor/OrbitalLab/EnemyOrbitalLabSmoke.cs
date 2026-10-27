#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Subject42.Combat.OrbitalStation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Build.Player;
using UnityEngine;
using Object = UnityEngine.Object;

// A single bounded lab smoke, deliberately independent of the test runners.
[InitializeOnLoad]
public static class EnemyOrbitalLabSmoke
{
    private const string Output = "Artifacts/EnemyOrbitalLab", Key = "EnemyOrbitalLab.OneSmoke";
    private static IEnumerator routine;
    private static readonly List<string> report = new();
    private static double next, deadline;
    private static int errors;
    private static double allowRequestAfter;
    static EnemyOrbitalLabSmoke()
    {
        EditorApplication.update += Poll;
        EditorApplication.playModeStateChanged += State;
        Application.logMessageReceived += Log;
    }
    [MenuItem("Tools/Subject42/Dev/OrbitalLab/Smoke artist weapons")]
    public static void Run()
    {
        Directory.CreateDirectory(Output);
        EditorSceneManager.OpenScene(EnemyOrbitalLabController.ScenePath);
        EnemyOrbitalLabAuthoring.Prepare();
        var compiled = PlayerBuildInterface.CompilePlayerScripts(new ScriptCompilationSettings
        {
            target = BuildTarget.StandaloneWindows64, group = BuildTargetGroup.Standalone,
            options = ScriptCompilationOptions.DevelopmentBuild
        }, "Artifacts/GeneratedQA/EnemyOrbitalLabCompilation");
        if (compiled.assemblies == null || compiled.assemblies.Count == 0) throw new Exception("Development compilation failed");
        File.WriteAllText(Output + "/compile.txt", "PASS: Editor domain loaded; Development scripts compiled (" + compiled.assemblies.Count + " assemblies).\n");
        SessionState.SetInt(Key, 1);
        EditorApplication.EnterPlaymode();
    }
    private static void State(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.EnteredEditMode) allowRequestAfter = EditorApplication.timeSinceStartup + 1;
        if (SessionState.GetInt(Key, 0) == 0) return;
        if (state == PlayModeStateChange.EnteredPlayMode)
        { routine = Checks(); report.Clear(); errors = 0; next = 0; deadline = EditorApplication.timeSinceStartup + 60; }
        if (state == PlayModeStateChange.EnteredEditMode && SessionState.GetInt(Key, 0) >= 2)
            SessionState.EraseInt(Key);
    }
    private static void Log(string message, string stack, LogType type)
    {
        if (routine != null && (type == LogType.Error || type == LogType.Exception || type == LogType.Assert))
        { errors++; report.Add("UNITY ERROR: " + message); }
    }
    private static void Poll()
    {
        if (File.Exists(Output + "/stop.request"))
        {
            File.Delete(Output + "/stop.request");
            EditorApplication.ExitPlaymode();
            allowRequestAfter = EditorApplication.timeSinceStartup + 1;
            return;
        }
        if (File.Exists(Output + "/smoke.request") && !EditorApplication.isCompiling &&
            !EditorApplication.isUpdating && !EditorApplication.isPlaying && !EditorApplication.isPlayingOrWillChangePlaymode &&
            EditorApplication.timeSinceStartup > allowRequestAfter)
        {
            File.Delete(Output + "/smoke.request");
            try { Run(); }
            catch (Exception error) { File.WriteAllText(Output + "/smoke.txt", "FAIL: " + error); Debug.LogException(error); }
        }
        if (routine == null || EditorApplication.timeSinceStartup < next) return;
        try
        {
            if (EditorApplication.timeSinceStartup > deadline) throw new Exception("Smoke timed out");
            if (!routine.MoveNext()) { Finish(errors == 0); return; }
            next = EditorApplication.timeSinceStartup + (routine.Current is float delay ? delay : .02);
        }
        catch (Exception error) { report.Add(error.ToString()); Finish(false); }
    }
    private static void Finish(bool pass)
    {
        routine = null; report.Add(pass ? "PASS: bounded Play Mode smoke, no runtime errors" : "FAIL");
        File.WriteAllLines(Output + "/smoke.txt", report);
        SessionState.SetInt(Key, pass ? 2 : 3); EditorApplication.ExitPlaymode();
    }
    private static void Require(bool value, string message)
    { if (!value) throw new Exception(message); report.Add("PASS: " + message); }
    private static IEnumerator Checks()
    {
        yield return .6f;
        var lab = Object.FindFirstObjectByType<EnemyOrbitalLabController>();
        Require(lab != null && lab.Ready, "lab initialized");
        // Artist/combat smoke explicitly requests its old mixed fixture; sandbox starts with four pistols.
        lab.SelectShootingBuild(EnemyOrbitalLabController.ShootingBuild.Mixed);
        lab.SetPlayerInvincible(true);
        lab.Station.enabled = false; lab.SetFrozen(true);
        Require(lab.Station.State.Rings.Count >= 3 && lab.Station.State.FreeBuiltMounts == 0 &&
            lab.Station.Modules.Count >= 9, "default mixed build fills multiple ordinary orbits");
        Require(lab.Station.Modules.OfType<EnemyOrbitalLabSaw>().Any() &&
            lab.Station.Modules.OfType<EnemyOrbitalLabWeapon>().Any(w => w.SourceProfile == lab.Shotgun) &&
            lab.Station.Modules.OfType<EnemyOrbitalLabWeapon>().Any(w => w.SourceProfile == lab.Sniper), "all three explicit weapon profiles installed");
        Require(lab.Station.Modules.All(module =>
        {
            var view = module.CurrentMount.Transform.GetComponentInChildren<OrbitalModuleView>();
            return view != null && view.Body != null && view.Body.GetComponentsInChildren<SpriteRenderer>().Length > 0;
        }), "all mounted artist bodies have visible authored sprites");
        Require(lab.Shotgun.ProjectileVisual == lab.Sniper.ProjectileVisual &&
            lab.Sniper.ProjectileVisual == lab.PistolProjectileVisual &&
            lab.PistolProjectileVisual.GetComponentsInChildren<TrailRenderer>(true).Length > 0,
            "all pellets and sniper shots use the existing large blue bullet with trail");

        // A stationary, single-shield fixture isolates collision rules without changing live input logic.
        lab.StartScenario(0);
        do { yield return .02f; } while (!lab.Ready);
        Fixture(lab);
        var shield = lab.GetComponentsInChildren<EnemyDirectionalShield>().Single();
        var health = shield.GetComponent<EnemyHealth>();
        var adapter = (ProductionOrbitalCombatAdapter)lab.Station.Combat;
        Require(lab.Sniper.ProjectileSpeed == 120f && lab.Shotgun.ProjectileSpeed == 10f && lab.Pistol.ProjectileSpeed == 13f,
            "independent Inspector profiles: sniper 120 m/s, shotgun 10 m/s, original pistol 13 m/s");
        Require(lab.Pistol.Damage == OrbitalPistolModule.BaseDamage && lab.Pistol.FireInterval == .55f &&
            lab.Pistol.Range == 8f && lab.Pistol.BurstShots == 1 && lab.Pistol.SpreadDegrees == 0f,
            "pistol retains the original module's damage, cadence, range and single straight shot");
        float originalTrail = lab.PistolProjectileVisual.GetComponentInChildren<TrailRenderer>(true).time;
        foreach (var profile in new[] { lab.Sniper, lab.Shotgun, lab.Pistol })
        {
            adapter.SpawnWeaponProjectile(new Vector2(-20, -20), health, Vector2.right, profile, null, 1f);
            var trail = lab.Station.GetComponentsInChildren<TrailRenderer>().Single();
            Vector2 from = trail.transform.position;
            adapter.Tick(.02f);
            Require(Mathf.Abs(Vector2.Distance(from, trail.transform.position) - profile.ProjectileSpeed * .02f) < .001f &&
                Mathf.Approximately(trail.time, profile.ProjectileTrailSeconds >= 0f ? profile.ProjectileTrailSeconds : originalTrail),
                $"actual flight {profile.ProjectileSpeed} m/s and per-shot trail {trail.time:0.000} s; shared-pool defaults restored");
            adapter.DebugClearProjectiles();
        }
        float hp = health.CurrentHealth;
        Shot(adapter, shield, lab.Shotgun, true);
        Require(health.CurrentHealth == hp && shield.RemainingBlockedHits == 3 &&
            shield.Kind == EnemyDirectionalShield.ShieldKind.Breakable, "single-melee scenario uses a breakable shield with four initial hits");
        int blocks = shield.Blocks;
        Shot(adapter, shield, lab.Spear, true);
        Require(health.CurrentHealth == hp && shield.Blocks > blocks, "legacy spear still stops at shields");
        adapter.SpawnWeaponProjectile(shield.Center + shield.Forward * 3f, health, -shield.Forward, lab.Sniper, null, lab.Sniper.Damage);
        // One 100 ms frame traverses the entire 12 m range, crossing both the shield and body.
        adapter.Tick(.1f);
        Require(Mathf.Approximately(hp - health.CurrentHealth, lab.Sniper.Damage) && shield.Blocks == blocks + 1,
            "120 m/s sniper sweeps a full 12 m frame, crosses the shield and hits the body exactly once");
        hp = health.CurrentHealth; int remaining = shield.RemainingBlockedHits;
        adapter.Tick(.1f);
        Require(health.CurrentHealth == hp && shield.RemainingBlockedHits == remaining && adapter.LabActiveProjectileCount == 0,
            "fast sniper retires after one body without spending shield durability or leaving a trail");
        hp = health.CurrentHealth;
        var volley = new ProductionOrbitalCombatAdapter.ProjectileVolley(80);
        Shot(adapter, shield, lab.Shotgun, false, volley, 2);
        Require(Mathf.Approximately(hp - health.CurrentHealth, lab.Shotgun.Damage * 2),
            "two independent pellets from the same volley both damage exposed body");
        shield.ResetForSpawn(); hp = health.CurrentHealth;
        for (int hit = 1; hit <= 4; hit++)
        {
            Shot(adapter, shield, lab.Pistol, true);
            Require(shield.RemainingBlockedHits == 4 - hit && health.CurrentHealth == hp,
                "each separate pistol bullet spends one shield durability: hit " + hit);
        }
        Require(shield.IsBroken && !shield.ShieldArc.enabled, "four pistol bullets destroy the shield without damaging the body");

        lab.StartScenario(3);
        do { yield return .02f; } while (!lab.Ready);
        Fixture(lab); shield = lab.GetComponentsInChildren<EnemyDirectionalShield>().Single();
        health = shield.GetComponent<EnemyHealth>(); adapter = (ProductionOrbitalCombatAdapter)lab.Station.Combat;
        int durability = shield.RemainingBlockedHits; hp = health.CurrentHealth;
        Shot(adapter, shield, lab.Shotgun, true, new ProductionOrbitalCombatAdapter.ProjectileVolley(81), 7);
        Require(shield.RemainingBlockedHits == durability - 1 && health.CurrentHealth == hp,
            "seven pellets spend only one durability on a breakable shield");
        durability = shield.RemainingBlockedHits; hp = health.CurrentHealth;
        Shot(adapter, shield, lab.Sniper, true);
        Require(shield.RemainingBlockedHits == durability && Mathf.Approximately(hp - health.CurrentHealth, lab.Sniper.Damage),
            "sniper crosses breakable shield without spending durability or duplicate damage");

        var saw = lab.Station.Modules.OfType<EnemyOrbitalLabSaw>().First();
        saw.CurrentMount.Transform.position = shield.Center + shield.Forward * shield.ShieldDistance;
        Physics2D.SyncTransforms(); hp = health.CurrentHealth; blocks = shield.Blocks;
        saw.ActivateCombat();
        Require(health.CurrentHealth == hp && shield.Blocks > blocks, "saw disk is blocked by the frontal plate");
        yield return .4f;
        saw.CurrentMount.Transform.position = shield.Center - shield.Forward * .35f;
        Physics2D.SyncTransforms(); saw.ActivateCombat();
        Require(Mathf.Approximately(hp - health.CurrentHealth, lab.Saw.Damage), "saw disk damages the exposed body");
        hp = health.CurrentHealth; saw.ActivateCombat();
        Require(health.CurrentHealth == hp && adapter.LabActiveProjectileCount == 0,
            "saw respects per-target repeat interval and emits no projectiles");

        var gun = lab.Station.Modules.OfType<EnemyOrbitalLabWeapon>().First();
        float radius = saw.ContactRadius, orbitRadius = saw.CurrentMount.Ring.Radius;
        float maxHp = lab.Player.MaxHealth;
        var movement = lab.Player.GetComponent<CharacterMovement2D>(); float speed = movement.speed;
        foreach (EnemyOrbitalLabController.LabUpgrade upgrade in Enum.GetValues(typeof(EnemyOrbitalLabController.LabUpgrade)))
            Require(lab.ApplyLabUpgrade(upgrade), "applicable category upgrade: " + upgrade);
        Require(Mathf.Approximately(gun.DamageMultiplier, 1.2f) && Mathf.Approximately(gun.FireRateMultiplier, 1.15f) &&
            Mathf.Approximately(saw.DamageMultiplier, 1.2f) && Mathf.Approximately(saw.ContactRadius, radius * 1.15f) &&
            Mathf.Approximately(saw.CurrentMount.Ring.Radius, orbitRadius) && lab.Station.State.Rings.All(r => r.SpeedUpgradeLevel == 1) &&
            Mathf.Approximately(lab.Player.MaxHealth, maxHp + 20) && Mathf.Approximately(movement.speed, speed * 1.1f),
            "shooting/contact/orbit/player upgrades affect their targets; contact size leaves orbit radius intact");
        lab.OpenUpgradeChoices();
        Require(lab.Paused && lab.LabUpgradePanel.gameObject.activeSelf &&
            lab.LabUpgradePanel.GetComponentsInChildren<UpgradeCardView>().Length == 3, "existing card panel offers three eligible choices");
        lab.LabUpgradePanel.GetComponentsInChildren<UpgradeCardView>()[0].GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
        Require(!lab.LabUpgradePanel.gameObject.activeSelf && !lab.Paused, "native card selection applies and closes without leaving the scene paused");

        foreach (var build in new[] { EnemyOrbitalLabController.ShootingBuild.Shotgun, EnemyOrbitalLabController.ShootingBuild.Sniper,
            EnemyOrbitalLabController.ShootingBuild.Saw, EnemyOrbitalLabController.ShootingBuild.Mixed, EnemyOrbitalLabController.ShootingBuild.Pistol })
        {
            lab.SelectShootingBuild(build);
            Require(lab.Station.State.FreeBuiltMounts == 0 && lab.Station.ValidateState(out _), "filled valid build: " + build);
        }
        lab.StartMassEncounter(10);
        do { yield return .02f; } while (!lab.Ready);
        Fixture(lab);
        Require(lab.AliveEnemies == 20 && lab.GetComponentsInChildren<EnemyDirectionalShield>().Length == 10, "10 shielded + 10 ordinary enemies");
        BreakAllShields(lab);
        lab.StartMassEncounter(20);
        do { yield return .02f; } while (!lab.Ready);
        Fixture(lab);
        Require(lab.AliveEnemies == 40 && lab.GetComponentsInChildren<EnemyDirectionalShield>().Length == 20, "20 shielded + 20 ordinary enemies");
        BreakAllShields(lab);
        var composition = Composition(lab);
        var target = lab.GetComponentsInChildren<EnemyHealth>().First(e => !e.IsDead);
        adapter = (ProductionOrbitalCombatAdapter)lab.Station.Combat;
        adapter.SpawnWeaponProjectile(lab.Player.transform.position, target, Vector2.right, lab.Sniper, null, 1);
        Require(adapter.LabActiveProjectileCount == 1, "pooled blue projectile spawns");
        int poolSize = adapter.LabPooledProjectileCount;
        lab.ClearEnemies();
        Require(lab.AliveEnemies == 0 && adapter.LabActiveProjectileCount == 0 &&
            lab.Station.GetComponentsInChildren<TrailRenderer>(true).All(t => t.positionCount == 0), "Clear removes enemies, shots and all trail points");
        lab.RestartScenario();
        do { yield return .02f; } while (!lab.Ready);
        Fixture(lab);
        Require(lab.AliveEnemies == 40 && Composition(lab) == composition && lab.ShootingDamageMultiplier == 1 &&
            lab.ContactSizeMultiplier == 1 && lab.Station.State.Rings.All(r => r.SpeedUpgradeLevel == 0),
            "Restart restores the same composition and positions with baseline upgrades");
        Require(lab.GetComponentsInChildren<EnemyDirectionalShield>().All(s => s.Kind == EnemyDirectionalShield.ShieldKind.Breakable &&
            s.MaximumBlockedHits == 4 && s.RemainingBlockedHits == 4 && s.DurabilityColor == EnemyDirectionalShield.Blue &&
            s.ShieldArc.enabled && s.DamageCracks.All(c => !c.enabled)),
            "Restart respawns every shield at full durability, blue, visible and without cracks");
        adapter = (ProductionOrbitalCombatAdapter)lab.Station.Combat;
        target = lab.GetComponentsInChildren<EnemyHealth>().First(e => !e.IsDead);
        poolSize = adapter.LabPooledProjectileCount;
        for (int i = 0; i < 4; i++)
        {
            adapter.SpawnWeaponProjectile(new Vector2(-10 + i * 5, -10), target, Vector2.up, lab.Sniper, null, 1);
            adapter.Tick(.02f); adapter.DebugClearProjectiles();
        }
        Require(poolSize == adapter.LabPooledProjectileCount && adapter.LabActiveProjectileCount == 0,
            $"repeated distant spawns reuse the pool without growing it or retaining shots ({poolSize}->{adapter.LabPooledProjectileCount}, active {adapter.LabActiveProjectileCount})");
        lab.SelectShootingBuild(EnemyOrbitalLabController.ShootingBuild.Mixed);
        lab.SetFrozen(false); lab.Station.enabled = true; lab.SetPlayerInvincible(true);
        lab.Player.GetComponent<CharacterMovement2D>().enabled = true;
        foreach (var enemy in lab.GetComponentsInChildren<EnemyHealth>())
            foreach (var movementComponent in enemy.GetComponents<EnemyMovement>()) movementComponent.enabled = true;
        yield return .2f;
        foreach (var weapon in lab.Station.Modules.OfType<EnemyOrbitalLabWeapon>())
        { weapon.ResetFiring(); weapon.Tick(.25f); }
        yield return .05f;
        Capture(lab);
    }
    private static string Composition(EnemyOrbitalLabController lab) => string.Join("|", lab.GetComponentsInChildren<EnemyHealth>()
        .Where(e => !e.IsDead).Select(e => e.name + "@" + e.transform.position.ToString("F2")).OrderBy(s => s));
    private static void BreakAllShields(EnemyOrbitalLabController lab)
    {
        var shields = lab.GetComponentsInChildren<EnemyDirectionalShield>();
        Require(shields.All(s => s.Kind == EnemyDirectionalShield.ShieldKind.Breakable && s.MaximumBlockedHits == 4 &&
            s.RemainingBlockedHits == 4 && s.DurabilityColor == EnemyDirectionalShield.Blue),
            $"all {shields.Length} melee/shooter shields start breakable, blue, with four durability");
        foreach (var shield in shields)
        {
            Vector2 point = shield.ShieldCollider.ClosestPoint(shield.Center + shield.Forward * shield.ShieldDistance);
            shield.FeedbackSource.mute = true;
            for (int hit = 0; hit < 3; hit++) shield.BlockProjectile(-shield.Forward, point);
            Require(shield.RemainingBlockedHits == 1 && shield.DurabilityColor == EnemyDirectionalShield.Red &&
                shield.DamageCracks.Any(c => c.enabled), "damaged shield becomes red and cracked");
            shield.BlockProjectile(-shield.Forward, point);
        }
        Require(shields.All(s => s.IsBroken && !s.ShieldArc.enabled && s.BreakShards.particleCount > 0),
            $"all {shields.Length} shields break on hit four and emit their existing fragments");
    }
    private static void Fixture(EnemyOrbitalLabController lab)
    {
        lab.Station.enabled = false; lab.SetFrozen(true); lab.SetPlayerInvincible(true);
        lab.Player.GetComponent<CharacterMovement2D>().enabled = false;
        var adapter = (ProductionOrbitalCombatAdapter)lab.Station.Combat;
        adapter.DebugClearProjectiles(); adapter.LabStraightProjectiles = true;
        foreach (var enemy in lab.GetComponentsInChildren<EnemyHealth>())
            foreach (var movement in enemy.GetComponents<EnemyMovement>()) movement.enabled = false;
        Physics2D.SyncTransforms();
    }
    private static void Shot(ProductionOrbitalCombatAdapter adapter, EnemyDirectionalShield shield,
        OrbitalWeaponSettings settings, bool front, ProductionOrbitalCombatAdapter.ProjectileVolley volley = null, int count = 1)
    {
        Vector2 direction = front ? -shield.Forward : shield.Forward;
        Vector2 origin = shield.Center - direction * 3f;
        for (int i = 0; i < count; i++) adapter.SpawnWeaponProjectile(origin, shield.GetComponent<EnemyHealth>(), direction, settings, volley, settings.Damage);
        for (int i = 0; i < 30; i++) adapter.Tick(.025f);
        adapter.DebugClearProjectiles();
    }
    private static void Capture(EnemyOrbitalLabController lab)
    {
        var camera = lab.CameraRig.ControlledCamera;
        var target = new RenderTexture(1280, 800, 24);
        var old = camera.targetTexture; var active = RenderTexture.active;
        var pixels = new Texture2D(1280, 800, TextureFormat.RGB24, false);
        try
        {
            camera.targetTexture = target; camera.Render(); RenderTexture.active = target;
            pixels.ReadPixels(new Rect(0, 0, 1280, 800), 0, 0); pixels.Apply();
            File.WriteAllBytes(Output + "/filled-orbits.png", pixels.EncodeToPNG());
        }
        finally { camera.targetTexture = old; RenderTexture.active = active; Object.DestroyImmediate(pixels); Object.DestroyImmediate(target); }
    }
}
#endif
