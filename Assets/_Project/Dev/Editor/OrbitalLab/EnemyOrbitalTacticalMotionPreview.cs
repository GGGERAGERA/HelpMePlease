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

// Explicit, bounded observation of the existing AI in the lab; no production authoring.
[InitializeOnLoad]
public static class EnemyOrbitalTacticalMotionPreview
{
    const string Output = "Artifacts/GeneratedQA/TacticalMotionLab", Key = "EnemyOrbital.TacticalMotionPreview";
    const string CrowdOutput = "Artifacts/GeneratedQA/TacticalCrowdLab";
    const string IndependentOutput = "Artifacts/GeneratedQA/IndependentBreacherLab";
    static bool crowd, independent;
    static IEnumerator routine;
    static readonly List<string> report = new();
    static double next, deadline;
    static int failures;
    static EnemyOrbitalTacticalMotionPreview()
    {
        EditorApplication.update += Poll;
        EditorApplication.playModeStateChanged += state =>
        {
            if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(Key, false))
            { crowd = SessionState.GetBool(Key + ".Crowd", false); independent = SessionState.GetBool(Key + ".Independent", false); routine = independent ? CheckIndependent() : crowd ? CheckCrowd() : Check(); report.Clear(); failures = 0; next = 0; deadline = EditorApplication.timeSinceStartup + (crowd ? 40 : 30); }
            if (state == PlayModeStateChange.EnteredEditMode) { routine = null; SessionState.EraseBool(Key); }
        };
        Application.logMessageReceived += (message, stack, type) =>
        { if (routine != null && (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)) { failures++; report.Add("UNITY ERROR: " + message); } };
    }
    [MenuItem("Tools/Subject42/Dev/OrbitalLab/Check Tactical Motion Experiment")]
    public static void Open()
        => OpenRun(false);
    [MenuItem("Tools/Subject42/Dev/OrbitalLab/Check Tactical Crowd and Shields")]
    public static void OpenCrowd() => OpenRun(true);
    [MenuItem("Tools/Subject42/Dev/OrbitalLab/Check Independent Breachers")]
    public static void OpenIndependent() => OpenRun(false, true);
    static void OpenRun(bool withCrowd, bool withIndependent = false)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        Directory.CreateDirectory(Output);
        Directory.CreateDirectory(CrowdOutput);
        Directory.CreateDirectory(IndependentOutput);
        typeof(EnemyOrbitalInterceptionPreview).GetMethod("SetGameView", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, null);
        EditorSceneManager.OpenScene(EnemyOrbitalLabController.ScenePath);
        SessionState.SetBool(Key, true); SessionState.SetBool(Key + ".Crowd", withCrowd); SessionState.SetBool(Key + ".Independent", withIndependent); EditorApplication.EnterPlaymode();
    }
    static void Poll()
    {
        string path = Output + "/editor.request";
        if (File.Exists(path) && !EditorApplication.isCompiling && !EditorApplication.isUpdating)
        {
            string command;
            try { command = File.ReadAllText(path).Trim(); } catch (IOException) { return; }
            if (command == "check" && !EditorApplication.isPlayingOrWillChangePlaymode) { File.Delete(path); Open(); }
            else if (command == "crowd" && !EditorApplication.isPlayingOrWillChangePlaymode) { File.Delete(path); OpenCrowd(); }
            else if (command == "independent" && !EditorApplication.isPlayingOrWillChangePlaymode) { File.Delete(path); OpenIndependent(); }
            else if (command == "stop") { File.Delete(path); EditorApplication.ExitPlaymode(); }
        }
        if (routine == null || EditorApplication.timeSinceStartup < next) return;
        try
        {
            if (EditorApplication.timeSinceStartup > deadline) throw new Exception("30-second tactical motion check deadline");
            if (!routine.MoveNext()) { Finish(); return; }
            next = EditorApplication.timeSinceStartup + (routine.Current is float delay ? delay : .01);
        }
        catch (Exception error) { failures++; report.Add("FAIL: " + error); Finish(); }
    }
    static void Finish() { routine = null; report.Add(failures == 0 ? "PASS: bounded lab check, no runtime errors" : "FAILURES: " + failures); File.WriteAllLines((independent ? IndependentOutput : crowd ? CrowdOutput : Output) + "/result.txt", report); }
    static void Expect(bool value, string label) { if (!value) failures++; report.Add((value ? "PASS: " : "FAIL: ") + label); }
    static string Phase(EnemyChaseMovement actor) => typeof(EnemyChaseMovement).GetField("ramPhase", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(actor).ToString();
    static T Field<T>(EnemyChaseMovement actor, string field) => (T)typeof(EnemyChaseMovement).GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(actor);
    static EnemyHealth Spawn(EnemyOrbitalLabController lab, string name, Vector2 position)
    {
        int type = Array.FindIndex(lab.EnemyPrefabs, p => p != null && p.name == name);
        if (lab.SpawnManualEnemies(type, 1) != 1) throw new Exception("Cannot manually spawn " + name);
        var enemy = lab.ManualEnemies.Last(e => e != null && e.name.StartsWith(name));
        enemy.GetComponent<Rigidbody2D>().position = position; enemy.transform.position = position;
        Physics2D.SyncTransforms(); return enemy;
    }
    static IEnumerator Check()
    {
        EnemyOrbitalLabController lab;
        while ((lab = Object.FindFirstObjectByType<EnemyOrbitalLabController>()) == null || !lab.Ready || !lab.SandboxReady) yield return .02f;
        lab.AutomaticInterceptions = false; lab.CancelInterception(); lab.SetSegmentedOrbit(true);
        while (!lab.Ready || !lab.Segmented.Bound || !lab.SegmentedOrbit) yield return .02f;
        lab.Segmented.SetMotion(SegmentedOrbitConfiguration.Motion.Manual);
        lab.ClearDensityEnemies(); lab.SetAutomaticEnemySpawning(false); lab.SetFrozen(false); lab.SetPlayerInvincible(true); lab.SetEnemiesInvincible(false);
        lab.Station.enabled = false;
        var playerBody = lab.Player.GetComponent<Rigidbody2D>(); var constraints = playerBody.constraints;
        playerBody.position = Vector2.zero; lab.Player.transform.position = Vector2.zero;
        playerBody.constraints = RigidbodyConstraints2D.FreezeAll; lab.SetPaused(false);
        var flank = Spawn(lab, "p_Enemy_Flanker", new Vector2(12, 0));
        var move = flank.GetComponent<EnemyChaseMovement>();
        float start = Time.fixedTime, angle = 0, distance = 0;
        Vector2 previous = flank.transform.position;
        while (Time.fixedTime < start + 2.5f)
        {
            yield return .01f;
            Vector2 position = flank.transform.position;
            angle += Vector2.SignedAngle(previous, position); distance += Vector2.Distance(previous, position); previous = position;
        }
        float speed = distance / (Time.fixedTime - start);
        report.Add($"MEASURE: flanker speed={speed:0.00} m/s, angular travel={angle:0.0}°, radius={previous.magnitude:0.00} m after {Time.fixedTime-start:0.00} s");
        Expect(speed > 6f && Mathf.Abs(angle) > 55f && previous.magnitude < 8f, "fast spiral changes bearing while closing distance");
        Expect(flank.MaxHealth == 16, "lab flanker has 16 HP");
        lab.SetFrozen(true); float hp = flank.CurrentHealth;
        ((ProductionOrbitalCombatAdapter)lab.Station.Combat).ApplyDamage(flank, 8, flank.transform.position);
        Expect(!flank.IsDead && Mathf.Approximately(flank.CurrentHealth, hp - 8), "ordinary orbital damage works without instant death");
        lab.ClearDensityEnemies(); lab.SetFrozen(false);

        var breacher = Spawn(lab, "p_Enemy_Breacher", new Vector2(10, 0)); move = breacher.GetComponent<EnemyChaseMovement>();
        float until = Time.time + 5;
        while (Phase(move) != "Preparing" && Time.time < until) yield return .01f;
        Expect(Phase(move) == "Preparing", "breacher prepares from a distant approach");
        var cue = Field<LineRenderer>(move, "ramTelegraph");
        float warned = Vector3.Distance(cue.GetPosition(0), cue.GetPosition(1));
        Vector2 warningStart = cue.GetPosition(0), warningEnd = cue.GetPosition(1);
        float warningWidth = cue.startWidth;
        var breacherBody = breacher.GetComponent<Rigidbody2D>();
        Vector2 direction = Field<Vector2>(move, "ramDirection"), chargeStart = breacherBody.position;
        Expect(cue.enabled && warned > 27f && !move.CanDealContactDamage, "visible half-arena warning; preparation contact is safe");
        Expect(breacher.MaxHealth == 32, "lab breacher has 32 HP");
        lab.SetPaused(true); yield return .1f;
        ScreenCapture.CaptureScreenshot(Output + "/warning-1600x900.png"); yield return .15f;
        playerBody.position = Vector2.up * 3; lab.Player.transform.position = Vector2.up * 3; Physics2D.SyncTransforms();
        float playerHp = lab.Player.CurrentHealth; lab.SetPlayerInvincible(false); lab.SetPaused(false);
        until = Time.time + 4;
        while (Phase(move) != "Recovering" && Time.time < until) yield return .01f;
        Vector2 travel = breacherBody.position - chargeStart;
        report.Add($"MEASURE: warned corridor with body footprint={warned:0.00} m, actual root travel={travel.magnitude:0.00} m");
        Expect(Phase(move) == "Recovering" && Mathf.Abs(travel.magnitude - 28.8f) < .1f, "actual charge is 28.8 m within 0.1 m");
        var damageBounds = breacher.GetComponent<CapsuleCollider2D>().bounds;
        Expect(warningWidth >= damageBounds.size.y - .001f && warningStart.y - warningWidth * .5f <= damageBounds.min.y + .001f &&
            warningEnd.y + warningWidth * .5f >= damageBounds.max.y - .001f && damageBounds.min.x >= Mathf.Min(warningStart.x, warningEnd.x) - .001f,
            "warning corridor covers the real offset damage collider including charge endpoint");
        Expect(Mathf.Abs(travel.x * direction.y - travel.y * direction.x) < .15f && Field<Vector2>(move, "ramDirection") == direction, "charge stays on locked world line");
        Expect(lab.Player.CurrentHealth == playerHp, "sideways movement avoids the charge");
        Vector2 rest = breacher.transform.position; yield return .2f;
        Expect(!move.CanDealContactDamage && Vector2.Distance(rest, breacher.transform.position) < .03f, "stationary vulnerable recovery without contact damage");
        lab.SetPlayerInvincible(true);
        var rawPrefab = lab.EnemyPrefabs.First(p => p.name == "p_Enemy_Breacher");
        var raw = Object.Instantiate(rawPrefab, new Vector3(24, 24), Quaternion.identity);
        var rawMove = raw.GetComponent<EnemyChaseMovement>();
        Expect(rawMove.BaseChaseSpeed == 2f && raw.GetComponent<EnemyHealth>().MaxHealth != 32, "prefab outside lab retains production speed and HP");
        Expect(Field<float>(rawMove, "ramApproachRange") == 5f && Field<float>(rawMove, "ramPrepareSeconds") == .65f &&
            Field<float>(rawMove, "ramChargeSeconds") == .45f && Field<float>(rawMove, "ramSpeedMultiplier") == 2.5f &&
            !Field<bool>(rawMove, "exactRamDuration"), "production ram defaults are unchanged outside the lab spawn path");
        Object.Destroy(raw); lab.ClearDensityEnemies(); lab.SetPaused(true);
        playerBody.constraints = constraints; playerBody.position = Vector2.zero; lab.Player.transform.position = Vector2.zero;
        Spawn(lab, "p_Enemy_Flanker", new Vector2(-7, 3));
        Spawn(lab, "p_Enemy_Breacher", new Vector2(10, -3));
        lab.SetPaused(false); yield return .08f; lab.SetPaused(true); lab.Station.enabled = true;
        var view = typeof(EnemyOrbitalLabController).GetField("segmentedView", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(lab);
        view.GetType().GetField("tab", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(view, 1);
        yield return .1f; ScreenCapture.CaptureScreenshot(Output + "/review-1600x900.png"); yield return .15f;
        Expect(lab.Station.ValidateState(out _), "manual orbital construction remains valid; lab left paused");
    }

    static void Shot(EnemyOrbitalLabController lab, EnemyHealth enemy, Vector2 direction, OrbitalWeaponSettings settings, float damage)
    {
        var adapter = (ProductionOrbitalCombatAdapter)lab.Station.Combat;
        Vector2 center = enemy.GetComponent<CapsuleCollider2D>().bounds.center;
        adapter.SpawnWeaponProjectile(center - direction * 3f, enemy, direction, settings, null, damage);
        for (int i = 0; i < 30; i++) adapter.Tick(.025f);
        adapter.DebugClearProjectiles();
    }
    static IEnumerator CheckIndependent()
    {
        EnemyOrbitalLabController lab;
        while ((lab = Object.FindFirstObjectByType<EnemyOrbitalLabController>()) == null || !lab.Ready || !lab.SandboxReady) yield return .02f;
        lab.AutomaticInterceptions = false; lab.CancelInterception(); lab.SetSegmentedOrbit(true);
        while (!lab.Ready || !lab.Segmented.Bound || !lab.SegmentedOrbit) yield return .02f;
        lab.Segmented.SetMotion(SegmentedOrbitConfiguration.Motion.Manual);
        lab.SetAutomaticEnemySpawning(false); lab.SetPlayerInvincible(true); lab.SetEnemiesInvincible(false); lab.SetFrozen(false);
        lab.Station.enabled = false;
        var playerBody = lab.Player.GetComponent<Rigidbody2D>(); var constraints = playerBody.constraints;
        playerBody.position = Vector2.zero; lab.Player.transform.position = Vector2.zero;
        playerBody.constraints = RigidbodyConstraints2D.FreezeAll;
        int type = Array.FindIndex(lab.EnemyPrefabs, p => p != null && p.name == "p_Enemy_Breacher");
        foreach (int count in new[] { 5, 10, 20 })
        {
            lab.SetPaused(true); lab.ClearDensityEnemies();
            Expect(lab.SpawnManualEnemies(type, count) == count, count + " living breachers spawn without alive caps");
            var actors = lab.ManualEnemies.Where(e => e != null && !e.IsDead).ToArray();
            for (int i = 0; i < actors.Length; i++)
            {
                float angle = i * Mathf.PI * 2f / actors.Length;
                Vector2 position = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * 12f;
                actors[i].GetComponent<Rigidbody2D>().position = position; actors[i].transform.position = position;
            }
            Physics2D.SyncTransforms(); lab.SetPaused(false);
            float until = Time.fixedTime + 1.15f;
            int preparing = 0, charging = 0, cues = 0;
            while (Time.fixedTime < until)
            {
                var moves = actors.Select(e => e.GetComponent<EnemyChaseMovement>()).ToArray();
                preparing = Mathf.Max(preparing, moves.Count(m => Phase(m) == "Preparing"));
                charging = Mathf.Max(charging, moves.Count(m => Phase(m) == "Charging"));
                cues = Mathf.Max(cues, moves.Count(m => Field<LineRenderer>(m, "ramTelegraph").enabled));
                yield return .01f;
            }
            report.Add($"MEASURE: live={count}, preparing={preparing}, charging={charging}, individual warnings={cues}");
            Expect(preparing == count && charging == count && cues == count, "all " + count + " independently prepare, warn and charge concurrently");
        }
        lab.SetPaused(true); playerBody.constraints = constraints; lab.Station.enabled = true;
        var view = typeof(EnemyOrbitalLabController).GetField("segmentedView", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(lab);
        view.GetType().GetField("tab", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(view, 1);
        yield return .1f; ScreenCapture.CaptureScreenshot(IndependentOutput + "/twenty-breachers-1600x900.png"); yield return .15f;
        Expect(lab.Station.ValidateState(out _), "orbital construction valid; Play Mode left open");
    }
    static IEnumerator CheckCrowd()
    {
        // Reuse the short trajectory check rather than another test suite.
        var baseline = Check(); while (baseline.MoveNext()) yield return baseline.Current;
        var lab = Object.FindFirstObjectByType<EnemyOrbitalLabController>();
        lab.ClearDensityEnemies(); lab.Station.enabled = false; lab.SetFrozen(true); lab.SetPaused(false);
        foreach (string name in new[] { "p_Enemy_Flanker", "p_Enemy_Breacher" })
        {
            var target = Spawn(lab, name, new Vector2(7, 0)); yield return .04f;
            int shots = name.Contains("Flanker") ? 2 : 4;
            for (int i = 0; i < shots; i++)
            {
                Shot(lab, target, Vector2.right, lab.Pistol, 8);
                Expect(target.IsDead == (i == shots - 1), name + " normal pistol hit " + (i + 1) + "/" + shots);
            }
            lab.ClearDensityEnemies();
        }
        lab.SetPaused(true); lab.SpawnTacticalGroup(true); lab.SpawnTacticalGroup(false);
        var actors = lab.LabEnemies.Where(e => e != null && !e.IsDead).ToArray();
        Expect(actors.Count(e => e.name.Contains("Flanker")) == 20 && actors.Count(e => e.name.Contains("Breacher")) == 10,
            "20 flankers plus 10 breachers coexist without production caps");
        var variants = actors.Select(e => e.GetComponent<LabTacticalTimedShield>()).Where(t => t != null).ToArray();
        report.Add($"SAMPLE: shield eligibility {variants.Count(t => t.name.Contains("Flanker"))}/20 flankers, {variants.Count(t => t.name.Contains("Breacher"))}/10 breachers (independent 20%/25% draws)");
        Expect(variants.Length >= 2 && variants.All(t => t.ActivationDelay >= 3 && t.ActivationDelay <= 6 &&
            !t.Shield.enabled && t.Shield.IsBroken && !t.Shield.ShieldCollider.enabled && !t.Shield.BlockDiskContact(t.Shield.Center, 5)),
            "rare variants wait 3–6 s without hidden projectile/beam/saw protection");
        lab.SetFrozen(false); lab.SetPaused(false);
        float until = Time.time + 6.4f; int maxCharging = 0, maxCommitted = 0, warnings = 0;
        var observedWarnings = new HashSet<LabTacticalTimedShield>();
        while (Time.time < until)
        {
            var moves = actors.Where(e => e != null && !e.IsDead).Select(e => e.GetComponent<EnemyChaseMovement>()).ToArray();
            maxCharging = Mathf.Max(maxCharging, moves.Count(m => Phase(m) == "Charging"));
            maxCommitted = Mathf.Max(maxCommitted, moves.Count(m => Phase(m) == "Charging" || Phase(m) == "Preparing"));
            foreach (var timer in variants)
                if (timer != null && timer.Warning && observedWarnings.Add(timer))
                {
                    warnings++;
                    Expect(!timer.Shield.ShieldCollider.enabled && timer.Shield.ShieldArc.enabled && !timer.Shield.Covers(Vector2.right),
                        "0.4-second visible cue does not block early");
                }
            yield return .01f;
        }
        report.Add($"MEASURE: peak charging={maxCharging}, preparing+charging={maxCommitted}; observed cues={warnings}");
        Expect(maxCharging == 10 && maxCommitted == 10, "all ten living breachers can prepare and charge concurrently without admission limits");
        Expect(variants.All(t => t.Activated && t.Shield.MaximumBlockedHits == 2) && warnings > 0,
            "all eligible actors activate exactly once with two blocks");
        Expect(actors.All(e => e.GetComponent<EnemyChaseMovement>().IsFlankingExperiment || e.GetComponent<EnemyChaseMovement>().IsCommittedRamExperiment),
            "activation preserves the spiral/ram AI modes");
        lab.SetPaused(true); lab.SetFrozen(true);
        var keep = variants.Take(2).Select(t => t.GetComponent<EnemyHealth>()).ToArray();
        foreach (var actor in actors) if (!keep.Contains(actor)) { actor.gameObject.SetActive(false); Object.Destroy(actor.gameObject); }
        for (int i = 0; i < keep.Length; i++)
        {
            Vector2 position = new(7, i * 6);
            keep[i].GetComponent<Rigidbody2D>().position = position; keep[i].transform.position = position;
            keep[i].GetComponent<EnemyDirectionalShield>().Bind(lab.Player.transform);
        }
        Physics2D.SyncTransforms();
        var bearer = keep[0]; var shield = bearer.GetComponent<EnemyDirectionalShield>();
        float hp = bearer.CurrentHealth;
        Shot(lab, bearer, shield.Forward, lab.Pistol, 1);
        Expect(bearer.CurrentHealth == hp - 1 && shield.RemainingBlockedHits == 2, "unprotected side takes ordinary projectile damage");
        hp = bearer.CurrentHealth;
        Shot(lab, bearer, -shield.Forward, lab.Pistol, 8);
        Expect(bearer.CurrentHealth == hp && shield.RemainingBlockedHits == 1, "first frontal pistol shot consumes one block only");
        Shot(lab, bearer, -shield.Forward, lab.Pistol, 8);
        Expect(bearer.CurrentHealth == hp && shield.IsBroken && !shield.ShieldCollider.enabled && !shield.ShieldArc.enabled && shield.BreakShards.particleCount > 0,
            "second frontal shot breaks the existing shield with its destruction effect");
        Shot(lab, bearer, -shield.Forward, lab.Pistol, 8);
        Expect(bearer.CurrentHealth == hp - 8, "body is vulnerable after the shield breaks");
        var sniperTarget = keep[1]; var sniperShield = sniperTarget.GetComponent<EnemyDirectionalShield>();
        Shot(lab, sniperTarget, -sniperShield.Forward, lab.Sniper, lab.Sniper.Damage);
        Expect(sniperTarget.IsDead && sniperShield.RemainingBlockedHits == 2, "existing sniper bypass kills through an intact directional shield");
        lab.SetPaused(false); yield return .5f;
        Expect(shield.IsBroken && !bearer.GetComponent<LabTacticalTimedShield>().enabled, "broken shield never rearms");
        lab.ClearDensityEnemies(); lab.SetFrozen(true); lab.RandomEnemyTypes = true;
        lab.Player.GetComponent<Rigidbody2D>().position = Vector2.zero; lab.Player.transform.position = Vector2.zero;
        lab.DensitySpawnRate = 60; lab.SetAutomaticEnemySpawning(true); lab.SetTargetEnemyDensity(50);
        until = Time.time + 3; while (lab.AliveEnemies < 50 && Time.time < until) yield return .02f;
        Expect(lab.AliveEnemies == 50 && lab.LabEnemies.Any(e => e.name.Contains("Flanker")) && lab.LabEnemies.Any(e => e.name.Contains("Breacher")),
            "ordinary random mixture reaches 50 with both tactical types");
        lab.SetTargetEnemyDensity(100);
        until = Time.time + 3; while (lab.AliveEnemies < 100 && Time.time < until) yield return .02f;
        int flankers = lab.LabEnemies.Count(e => e != null && !e.IsDead && e.name.Contains("Flanker"));
        int breachers = lab.LabEnemies.Count(e => e != null && !e.IsDead && e.name.Contains("Breacher"));
        report.Add($"MIXTURE: {lab.AliveEnemies} alive, flankers={flankers}, breachers={breachers}");
        Expect(lab.AliveEnemies == 100 && flankers == 10 && breachers == 5, "100-spawn shuffled mixture uses 10%/5% tactical shares");
        lab.SetFrozen(false); lab.Station.enabled = true; lab.SetEnemiesInvincible(false); lab.SetPlayerInvincible(true);
        yield return 1.5f;
        lab.SetPaused(true); yield return .1f;
        ScreenCapture.CaptureScreenshot(CrowdOutput + "/crowd-100-1600x900.png"); yield return .15f;
        Expect(lab.Station.ValidateState(out _), "dense real-AI/orbital combat stays valid; Play Mode left open for review");
    }
}
#endif
