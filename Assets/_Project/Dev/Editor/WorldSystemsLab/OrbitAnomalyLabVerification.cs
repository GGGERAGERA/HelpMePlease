#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Subject42.Combat.OrbitalStation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

// Explicit request only; no import-triggered test suites or production scene writes.
[InitializeOnLoad]
public static class OrbitAnomalyLabVerification
{
    public const string Output = "Artifacts/GeneratedQA/OrbitAnomalyLab";
    const string Key = "Subject42.OrbitAnomalyLab.Smoke";
    static IEnumerator routine;
    static readonly List<string> report = new();
    static double deadline, next;
    static double readyAfter = EditorApplication.timeSinceStartup + 2;
    static int errors;
    static OrbitAnomalyLabVerification()
    {
        EditorApplication.update += Poll;
        EditorApplication.playModeStateChanged += state =>
        {
            if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(Key,false))
            { report.Clear(); errors = 0; deadline = EditorApplication.timeSinceStartup + 45; routine = Checks(); }
            if (state == PlayModeStateChange.EnteredEditMode) SessionState.EraseBool(Key);
        };
        Application.logMessageReceived += (message,stack,type) =>
        {
            if (routine != null && (type == LogType.Error || type == LogType.Exception || type == LogType.Assert))
            { errors++; report.Add("UNITY ERROR: " + message); }
        };
    }
    static void Poll()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode ||
            SessionState.GetBool("Subject42.Verification.AnyRunActive",false)) readyAfter = EditorApplication.timeSinceStartup + 2;
        string path = Output + "/editor.request";
        if (File.Exists(path) && !EditorApplication.isCompiling && !EditorApplication.isUpdating &&
            EditorApplication.timeSinceStartup >= readyAfter &&
            !SessionState.GetBool("Subject42.Verification.AnyRunActive",false) &&
            !EditorApplication.isPlayingOrWillChangePlaymode)
        {
            string command = File.ReadAllText(path).Trim(); File.Delete(path);
            try
            {
                OrbitAnomalyLabAuthoring.Prepare();
                if (command == "smoke") { SessionState.SetBool(Key,true); SessionState.SetBool("Subject42.Verification.AnyRunActive",true); EditorApplication.EnterPlaymode(); }
                else File.WriteAllText(Output + "/result.txt", "PASS: existing WorldSystemsLab prepared.");
            }
            catch (Exception e) { File.WriteAllText(Output + "/result.txt", "FAIL: " + e); }
        }
        if (routine == null || EditorApplication.timeSinceStartup < next) return;
        try
        {
            if (EditorApplication.timeSinceStartup > deadline) throw new Exception("Bounded smoke timed out.");
            if (!routine.MoveNext()) { Finish(errors == 0); return; }
            next = EditorApplication.timeSinceStartup + (routine.Current is float delay ? delay : .02);
        }
        catch (Exception e) { report.Add(e.ToString()); Finish(false); }
    }
    static void Finish(bool pass)
    {
        routine = null; report.Add(pass ? "PASS: bounded five-zone smoke; no runtime errors." : "FAIL");
        SessionState.EraseBool("Subject42.Verification.AnyRunActive");
        File.WriteAllLines(Output + "/result.txt",report); EditorApplication.ExitPlaymode();
    }
    static void Require(bool condition, string message)
    { if (!condition) throw new Exception(message); report.Add("PASS: " + message); }
    static void Near(float actual, float expected, string message) => Require(Mathf.Abs(actual - expected) < .002f,$"{message}: {actual:0.###} (expected {expected:0.###})");
    static float Cooldown(OrbitalModuleRuntime gun) => (float)typeof(OrbitalModuleRuntime)
        .GetProperty("RuntimeCooldown",BindingFlags.Instance | BindingFlags.NonPublic).GetValue(gun);
    static string Build(EnemyOrbitalLabController lab) =>
        string.Join(";",lab.Segmented.Cells.Select((c,i) => $"{i}:{c.Segment}:{c.Weapon}:{c.IndividualSlots}:{c.First}:{c.Second}")) +
        string.Join(";",lab.Segmented.Links.Select(l => $"{l.A}:{l.B}:{l.Module}")) +
        string.Join(";",lab.Station.Modules.Select(g => $"{g.StableModuleId}:{g.CurrentMount.Ring.RingId}:{g.CurrentMount.MountIndex}:" +
            string.Join(",",Enumerable.Range(0,lab.WeaponModules.SlotCount(g.StableModuleId)).Select(s => lab.WeaponModules.GetModule(g.StableModuleId,s)))));
    static Vector2 Direction(EnemyOrbitalLabController lab, EnemyOrbitalLabWeapon gun)
    {
        var adapter = (ProductionOrbitalCombatAdapter)lab.Station.Combat;
        Vector2 result = Vector2.zero;
        Action<long,int,Vector2> capture = (_,id,direction) => { if (id == gun.StableModuleId && result == Vector2.zero) result = direction; };
        adapter.LabProjectileSpawned += capture;
        try { Require(gun.FireLabExtraShot(),"real FIXED shot emitted"); }
        finally { adapter.LabProjectileSpawned -= capture; }
        Require(result.sqrMagnitude > .9f,"projectile direction captured from shared combat adapter");
        adapter.DebugClearProjectiles(); return result;
    }
    static IEnumerator Checks()
    {
        var experiment = Object.FindFirstObjectByType<WorldSystemsLabOrbitExperiment>();
        Require(experiment != null,"experiment exists in the existing WorldSystemsLab");
        var world = Object.FindFirstObjectByType<WorldSystemsLabController>();
        var originalPlayer = world.Player;
        float originalDamage = originalPlayer.GetComponent<PlayerHealth>().IncomingDamageMultiplier;
        experiment.Begin();
        while (!experiment.Ready || experiment.Orbit.Segmented.Movement != SegmentedOrbitConfiguration.Motion.Manual ||
            experiment.Orbit.Segmented.Preset != "Multi-Layer") yield return .02f;
        var lab = experiment.Orbit; var station = lab.Station;
        Require(lab.Player.transform == originalPlayer && world.Player == originalPlayer,"the authored player and world references are retained");
        Require(experiment.ZoneCount == 5 && station.Rings.Count == 3 && station.Rings.All(r => r.MountCount == 12),"five existing zones and Segmented Orbit 3 × 12");
        Require(!station.UsesSegmentedOrbit && station.DebugRotationPaused,"lab 3 × 12 projection uses manual FIXED mode, no auto-rotation or production V2-A conversion");
        lab.SetPaused(true);
        var gun = station.Modules.OfType<EnemyOrbitalLabWeapon>().First();
        lab.WeaponModules.UnlockSecondSlot(gun.StableModuleId);
        lab.WeaponModules.SetModule(gun.StableModuleId,0,"RAPID");
        lab.WeaponModules.SetModule(gun.StableModuleId,1,"PIERCE");
        var multiGun = station.Modules.OfType<EnemyOrbitalLabWeapon>().Skip(1).First();
        lab.WeaponModules.SetModule(multiGun.StableModuleId,0,"MULTISHOT");
        lab.Segmented.CaptureIndividualModules();
        string originalBuild = Build(lab);
        var phases = station.Rings.SelectMany(r => r.Mounts).ToDictionary(m => m,m => m.LocalPhase);
        lab.SetPaused(false); station.enabled = false; // Only the scenario advances combat/rotation below.
        experiment.MoveTo(-1);
        Require(experiment.ActiveAnomaly == WorldSystemsLabOrbitExperiment.Anomaly.None,"outside reference position is neutral");
        float walking = lab.Player.GetComponent<CharacterMovement2D>().WalkingSpeed;
        int Index(WorldSystemsLabOrbitExperiment.Anomaly kind) => Enumerable.Range(0,experiment.ZoneCount).Single(i => experiment.ZoneKind(i) == kind);
        void Restore(string label)
        {
            experiment.MoveTo(-1);
            Require(Build(lab) == originalBuild,label + ": weapon IDs, mounts, modules, links and constructor cells unchanged");
            Require(phases.All(p => p.Key.LocalPhase == p.Value),label + ": exact original local geometry restored");
            Require(!lab.WeaponModules.EffectsSuppressed,label + ": individual effects restored");
            Require(station.ValidateState(out string error),label + ": valid station: " + error);
        }
        experiment.MoveTo(Index(WorldSystemsLabOrbitExperiment.Anomaly.Blue));
        Require(lab.Player.GetComponent<CharacterMovement2D>().WalkingSpeed < walking,"blue retains player slowdown");
        station.State.RotationEnergy = 100; float phase = station.Rings[0].Phase;
        lab.Segmented.TickManualRotation(1,true,false,true);
        Near(Mathf.DeltaAngle(phase,station.Rings[0].Phase),45,"blue Q/E angle"); Near(station.RotationEnergy,75,"blue original energy cost");
        Direction(lab,gun); Restore("blue exit");

        experiment.MoveTo(Index(WorldSystemsLabOrbitExperiment.Anomaly.Red));
        station.State.RotationEnergy = 100; phase = station.Rings[0].Phase;
        lab.Segmented.TickManualRotation(1,true,false,true);
        Near(Mathf.DeltaAngle(phase,station.Rings[0].Phase),90,"red original Q/E speed"); Near(station.RotationEnergy,56.25f,"red energy ×1.75");
        lab.Pattern = EnemyOrbitalLabController.SpawnPattern.AroundPlayer;
        lab.SelectedEnemy = Array.FindIndex(lab.EnemyPrefabs,p => p.GetComponent<EnemyChaseMovement>() != null);
        Require(lab.SelectedEnemy >= 0,"existing chase enemy fixture is available"); lab.Spawn(1);
        var enemy = lab.LabEnemies.First(); enemy.transform.position = originalPlayer.position;
        experiment.TickEffects(0);
        var movement = enemy.GetComponent<EnemyChaseMovement>();
        Require(movement != null && (float)typeof(EnemyChaseMovement).GetField("anomalySpeedMultiplier",BindingFlags.NonPublic | BindingFlags.Instance).GetValue(movement) > 1,"red retains enemy acceleration");
        Direction(lab,gun); Restore("red exit"); lab.ClearSandboxArena();

        experiment.MoveTo(Index(WorldSystemsLabOrbitExperiment.Anomaly.Glitch));
        experiment.TickEffects(9f);
        Require(experiment.IsGlitchWarning && experiment.GlitchPulses == 0,"Glitch warns one second before the first 10-second shuffle");
        experiment.TickEffects(1f);
        Require(experiment.GlitchPulses == 1 && !experiment.IsGlitchWarning,"first shuffle exactly at 10 game seconds");
        Require(station.Modules.Any(g => g.CurrentMount.LocalPhase != phases[g.CurrentMount]),"Glitch really moves occupied weapon mounts");
        foreach (var ring in station.Rings)
        {
            var available = ring.Mounts.Where(m => lab.Segmented.At(ring.State.Order,m.MountIndex).Segment).ToArray();
            Require(available.Select(m => m.LocalPhase).OrderBy(v => v).SequenceEqual(available.Select(m => phases[m]).OrderBy(v => v)),"Glitch is a unique permutation of available mounts within level " + ring.State.Order);
        }
        var controlled = station.Rings.Select(r => r.Phase).ToArray();
        lab.Segmented.TickManualRotation(.1f,true,false,true);
        Direction(lab,gun); experiment.TickEffects(10f);
        Require(experiment.GlitchPulses == 2,"second Glitch pulse uses the same within-level restriction");
        Restore("Glitch exit");
        for (int i = 0; i < 3; i++) Near(Mathf.DeltaAngle(controlled[i],station.Rings[i].Phase),9,"Glitch exit preserves Q/E on level " + i);

        Vector2 outsideDirection = Direction(lab,gun);
        experiment.MoveTo(Index(WorldSystemsLabOrbitExperiment.Anomaly.Interference));
        experiment.TickEffects(.7f);
        Vector2 deviated = Direction(lab,gun);
        float deviation = Vector2.SignedAngle(outsideDirection,deviated);
        Require(Mathf.Abs(deviation) > .1f && Mathf.Abs(deviation) <= 17.01f,"interference changes an actual FIXED projectile direction within combined angular bounds");
        controlled = station.Rings.Select(r => r.Phase).ToArray();
        lab.Segmented.TickManualRotation(.1f,false,true,true);
        experiment.TickEffects(.3f); Restore("interference exit");
        for (int i = 0; i < 3; i++) Near(Mathf.DeltaAngle(controlled[i],station.Rings[i].Phase),-9,"interference exit preserves clockwise Q/E on level " + i);
        Near(Vector2.SignedAngle(outsideDirection,Direction(lab,gun)),-9,"restored actual shot follows only the player's Q/E");

        float rapidInterval = gun.Settings.FireInterval; int pierces = gun.Settings.BodyPierces;
        Require(station.Modules.Any(w => lab.Segmented.RateFactor(w.StableModuleId) > 1f) &&
            station.Modules.Any(w => lab.Segmented.Ricochets(w.StableModuleId) > 0),"Fire Rate and Ricochet links work before suppression");
        var extra = lab.Segmented.Links.First(l => l.Module == SegmentedOrbitConfiguration.Link.ExtraShot && lab.Segmented.Active(l.Id));
        var extraTarget = (EnemyOrbitalLabWeapon)lab.Segmented.Gun(extra.B);
        int extraShots = extraTarget.ShotsFired;
        lab.Segmented.OnOrdinaryShot(lab.Segmented.Gun(extra.A).StableModuleId);
        Require(extraTarget.ShotsFired == extraShots + 1,"Extra Shot link works before suppression");
        // Put this fixture's link back in its initial ready state so suppression is not masked by cooldown.
        typeof(SegmentedOrbitConfiguration.Connection).GetField("NextExtraShot",BindingFlags.Instance | BindingFlags.NonPublic).SetValue(extra,0f);
        experiment.MoveTo(Index(WorldSystemsLabOrbitExperiment.Anomaly.Suppression));
        Near(gun.Settings.FireInterval,gun.SourceProfile.FireInterval,"suppression removes RAPID");
        Require(gun.Settings.BodyPierces == gun.SourceProfile.BodyPierces && lab.WeaponModules.ExtraShotAngle(multiGun.StableModuleId) == null,"suppression removes PIERCE and MULTISHOT without deleting them");
        extraShots = extraTarget.ShotsFired;
        lab.Segmented.OnOrdinaryShot(lab.Segmented.Gun(extra.A).StableModuleId);
        Require(extraTarget.ShotsFired == extraShots,"suppression blocks a ready Extra Shot link");
        foreach (var weapon in station.Modules.OfType<EnemyOrbitalLabWeapon>())
        {
            Near(lab.Segmented.DamageFactor(weapon.StableModuleId),1,"suppressed Damage Link");
            Near(lab.Segmented.RateFactor(weapon.StableModuleId),1,"suppressed Fire Rate Link");
            Require(lab.Segmented.Ricochets(weapon.StableModuleId) == 0,"suppressed Ricochet Link");
            int shots = weapon.ShotsFired; float cooldown = Cooldown(weapon);
            Direction(lab,weapon); Near(Cooldown(weapon),cooldown,"extra-shot path keeps ordinary cooldown");
            Require(weapon.ShotsFired == shots + 1,"suppression allows base fire without Extra Shot Link recursion");
        }
        Restore("suppression exit"); Near(gun.Settings.FireInterval,rapidInterval,"RAPID restored");
        Require(gun.Settings.BodyPierces == pierces && lab.WeaponModules.ExtraShotAngle(multiGun.StableModuleId).HasValue,"PIERCE and MULTISHOT restored");
        lab.SetPaused(true);
        var damage = lab.Segmented.FindConnection(11,0); var previousKind = damage.Module;
        Require(lab.Segmented.SetLink(damage.Id,SegmentedOrbitConfiguration.Link.Damage),"existing link can carry a Damage fixture");
        lab.SetPaused(false);
        Require(lab.Segmented.DamageFactor(gun.StableModuleId) > 1f,"Damage Link works before suppression");
        experiment.MoveTo(Index(WorldSystemsLabOrbitExperiment.Anomaly.Suppression));
        Near(lab.Segmented.DamageFactor(gun.StableModuleId),1,"suppression removes an enabled Damage Link");
        experiment.MoveTo(-1);
        Require(lab.Segmented.DamageFactor(gun.StableModuleId) > 1f,"Damage Link restores after exit");
        lab.SetPaused(true); Require(lab.Segmented.SetLink(damage.Id,previousKind),"original link restored after fixture"); lab.SetPaused(false);
        Require(Build(lab) == originalBuild,"all four link tests retain the original build");

        experiment.MoveTo(Index(WorldSystemsLabOrbitExperiment.Anomaly.Interference)); experiment.TickEffects(1);
        experiment.MoveTo(Index(WorldSystemsLabOrbitExperiment.Anomaly.Suppression));
        Require(phases.All(p => p.Key.LocalPhase == p.Value) && lab.WeaponModules.EffectsSuppressed,"direct zone switch restores old geometry before new suppression");
        lab.SetPaused(true);
        Require(!lab.WeaponModules.EffectsSuppressed && phases.All(p => p.Key.LocalPhase == p.Value),"constructor pause restores all temporary effects");
        lab.SetPaused(false); experiment.MoveTo(-1);
        station.State.RotationEnergy = 0; int fired = gun.ShotsFired;
        gun.ResetFiring(); gun.Tick(1f);
        Require(gun.ShotsFired > fired,"ordinary FIXED fire continues at zero Q/E energy");
        lab.Segmented.TickManualRotation(1,false,false,true);
        Near(station.RotationEnergy,12,"normal energy recovery remains available");

        station.enabled = true;
        experiment.Compare(-1); Require(lab.AliveEnemies == 12,"A comparison reuses mixed encounter spawner");
        yield return .2f;
        experiment.Compare(Index(WorldSystemsLabOrbitExperiment.Anomaly.Interference));
        Require(lab.AliveEnemies == 12 && Build(lab) == originalBuild,"B comparison retains the identical build and encounter size");
        yield return .3f;
        ScreenCapture.CaptureScreenshot(Output + "/interference.png");
        yield return .2f;
        Require(lab.Telemetry.Snapshot().Weapons.Sum(w => w.Fired) > 0,"existing telemetry observes real combat fire");
        Require(!lab.Telemetry.Snapshot().ConfigurationChanged,"expected anomaly projection is not reported as a changed constructor build");
        experiment.MoveTo(Index(WorldSystemsLabOrbitExperiment.Anomaly.Suppression)); experiment.TickEffects(.1f); lab.Telemetry.Tick(.1f);
        Require(!lab.Telemetry.Snapshot().ConfigurationChanged,"suppressed runtime profiles do not invalidate same-build telemetry");
        experiment.End();
        Require(!experiment.Active && world.Player == originalPlayer,"end restores the original world lab and player");
        Near(originalPlayer.GetComponent<PlayerHealth>().IncomingDamageMultiplier,originalDamage,"borrowed player damage multiplier restored");
        Require(world.GetComponent<CorridorLab>().enabled,"legacy lab controls restored on exit");
        experiment.Begin(); experiment.End(); // Cancel while attached build is still being reset.
        experiment.Begin();
        while (!experiment.Ready) yield return .02f;
        Require(experiment.ZoneCount == 5,"the same experiment can be reopened without reloading the scene");
        experiment.End();
    }
}
#endif
