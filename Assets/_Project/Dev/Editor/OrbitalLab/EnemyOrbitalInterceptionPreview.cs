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

// Opt-in, bounded Editor preview. Never builds, compiles player scripts, or writes scenes.
[InitializeOnLoad]
public static class EnemyOrbitalInterceptionPreview
{
    const string Output = "Artifacts/GeneratedQA/EnemyOrbitalInterception", Key = "EnemyOrbital.InterceptionPreview";
    static IEnumerator routine;
    static readonly List<string> report = new();
    static double next, deadline;
    static int errors;
    static EnemyOrbitalInterceptionPreview()
    {
        EditorApplication.update += Poll;
        EditorApplication.playModeStateChanged += state =>
        {
            if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(Key, false))
            { report.Clear(); errors = 0; deadline = EditorApplication.timeSinceStartup + 65; routine = Check(); }
            if (state == PlayModeStateChange.EnteredEditMode) SessionState.EraseBool(Key);
        };
        Application.logMessageReceived += (message, stack, type) =>
        { if (routine != null && (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)) { errors++; report.Add("UNITY ERROR: " + message); } };
    }
    [MenuItem("Tools/Subject42/Dev/OrbitalLab/Quick check Protocol Interception")]
    public static void Open()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        Directory.CreateDirectory(Output);
        SetGameView();
        EditorSceneManager.OpenScene(EnemyOrbitalLabController.ScenePath);
        SessionState.SetBool(Key, true); EditorApplication.EnterPlaymode();
    }
    static void Poll()
    {
        string request = Output + "/editor.request";
        if (File.Exists(request) && !EditorApplication.isCompiling && !EditorApplication.isUpdating)
        {
            string action = File.ReadAllText(request).Trim(); File.Delete(request);
            if (action == "smoke" && !EditorApplication.isPlayingOrWillChangePlaymode) Open();
            else if (action == "stop") EditorApplication.ExitPlaymode();
            else if (action == "capture" && EditorApplication.isPlaying)
                ScreenCapture.CaptureScreenshot(Output + "/game-view.png");
        }
        if (routine == null || EditorApplication.timeSinceStartup < next) return;
        try
        {
            if (EditorApplication.timeSinceStartup > deadline) throw new Exception("65-second preview deadline reached");
            if (!routine.MoveNext())
            { routine = null; report.Add(errors == 0 ? "PASS: short Play Mode preview; zero runtime errors. Left open for manual play." : "FAIL: runtime errors"); File.WriteAllLines(Output + "/result.txt", report); return; }
            next = EditorApplication.timeSinceStartup + (routine.Current is float delay ? delay : .01);
        }
        catch (Exception e)
        { report.Add("FAIL: " + e); File.WriteAllLines(Output + "/result.txt", report); routine = null; }
    }
    static void Require(bool value, string message)
    { if (!value) throw new Exception(message); report.Add("PASS: " + message); }
    static void SetGameView()
    {
        var assembly = typeof(Editor).Assembly; var type = assembly.GetType("UnityEditor.GameViewSizes");
        var sizes = typeof(ScriptableSingleton<>).MakeGenericType(type).GetProperty("instance").GetValue(null);
        var group = type.GetMethod("GetGroup").Invoke(sizes, new object[] { 0 }); var gt = group.GetType();
        int count = (int)gt.GetMethod("GetTotalCount").Invoke(group, null), index = -1;
        for (int i = 0; i < count; i++)
        {
            var size = gt.GetMethod("GetGameViewSize").Invoke(group, new object[] { i });
            if ((int)size.GetType().GetProperty("width").GetValue(size) == 1600 && (int)size.GetType().GetProperty("height").GetValue(size) == 900) { index = i; break; }
        }
        if (index < 0)
        {
            var mode = Enum.ToObject(assembly.GetType("UnityEditor.GameViewSizeType"), 1);
            var size = Activator.CreateInstance(assembly.GetType("UnityEditor.GameViewSize"), new object[] { mode, 1600, 900, "Interception 1600×900" });
            gt.GetMethod("AddCustomSize").Invoke(group, new[] { size }); index = count;
        }
        var view = EditorWindow.GetWindow(assembly.GetType("UnityEditor.GameView"));
        view.GetType().GetProperty("selectedSizeIndex").SetValue(view, index); view.Show(); view.Focus();
    }
    static IEnumerator Check()
    {
        EnemyOrbitalLabController lab;
        while ((lab = Object.FindFirstObjectByType<EnemyOrbitalLabController>()) == null || !lab.SandboxReady || !lab.Ready) yield return .02f;
        Require(lab.HasInterceptionExperiment, "existing EnemyOrbitalLab; experiment has exact scene guard");
        lab.AutomaticInterceptions = false;
        lab.SetSegmentedOrbit(true);
        while (!lab.Ready || !lab.SegmentedOrbit || !lab.Segmented.Bound) yield return .02f;
        lab.Segmented.SetMotion(SegmentedOrbitConfiguration.Motion.Manual); lab.Segmented.LoadPreset("Шашечка");
        lab.SetPaused(false); yield return .1f;
        var ids = lab.Station.Modules.Select(g => g.StableModuleId).ToArray();
        int links = lab.Segmented.Links.Count(e => e.IsActive);
        float energy = lab.Segmented.RotationEnergy, phase = lab.Station.Rings[0].Phase;
        lab.Segmented.TickManualRotation(.2f, true, false, true);
        Require(lab.Segmented.RotationEnergy < energy && Mathf.Abs(Mathf.DeltaAngle(phase, lab.Station.Rings[0].Phase)) > 1, "Q/E path rotates construction and spends energy");
        Require(ids.SequenceEqual(lab.Station.Modules.Select(g => g.StableModuleId)) && links == lab.Segmented.Links.Count(e => e.IsActive), "weapon instances and active links preserved");
        var tickRight = typeof(OrbitalStationRuntime).GetMethod("TickRightMouse", BindingFlags.Instance | BindingFlags.NonPublic);
        tickRight.Invoke(lab.Station, new object[] { .25f, true, false });
        Require(lab.Station.BulletTime.IsActive, "existing Shift/Bullet Time path remains active");
        lab.CombatV2.Suspend();

        lab.SetEnemiesInvincible(true); lab.DensitySpawnRate = 60;
        foreach (int preset in new[] { 25, 50, 100, 150 })
        {
            lab.SetTargetEnemyDensity(preset); int previous = lab.AliveEnemies;
            while (lab.AliveEnemies < preset)
            { yield return .01f; Require(lab.AliveEnemies - previous <= 20, "spawn paced over frames"); previous = lab.AliveEnemies; }
            Require(lab.AliveEnemies == preset && lab.TargetEnemyDensity == preset, $"density preset {preset}: actual alive equals target");
        }
        var types = lab.LabEnemies.Where(e => e != null).Select(e => e.name).Distinct().ToArray();
        Require(types.Length >= 8, "mixed existing types: " + string.Join(", ", types));
        // Normal, unfrozen AI and real weapon hits at exactly 150. Invulnerability only keeps the sample at 150.
        var frames = new List<float>(); int lastFrame = -1; double until = EditorApplication.timeSinceStartup + 3;
        while (EditorApplication.timeSinceStartup < until)
        {
            if (Time.frameCount != lastFrame) { lastFrame = Time.frameCount; frames.Add(Time.unscaledDeltaTime * 1000f); }
            yield return .001f;
        }
        frames.Sort();
        report.Add($"PERFORMANCE: {lab.AliveEnemies}/150, active AI, {frames.Count} sampled frames, mean {frames.Average():0.0} ms / {1000f / frames.Average():0.0} FPS, p95 {frames[Mathf.Min(frames.Count - 1, (int)(frames.Count * .95f))]:0.0} ms, max {frames.Last():0.0} ms. Editor, not a standalone build.");
        var victim = lab.LabEnemies.First(e => e != null); victim.GetComponent<LabEnemyDummy>().Invulnerable = false;
        victim.TakeDamage(victim.MaxHealth + 1, victim.transform.position); yield return .2f;
        Require(lab.AliveEnemies == 150, "death replenished to 150");
        lab.SetTargetEnemyDensity(25); while (lab.AliveEnemies > 25) yield return .01f;
        Require(lab.AliveEnemies == 25, "lowering target retires excess actors");
        lab.ClearDensityEnemies(); yield return .1f;
        Require(lab.AliveEnemies == 0 && lab.TargetEnemyDensity == 0, "clear stops replenishment and removes enemies");
        lab.SetEnemiesInvincible(false);

        // Real shared projectile collision, without a direct damage call on this turret.
        lab.LaunchInterception(EnemyOrbitalLabController.InterceptionVariant.Single);
        var target = lab.LaserTurrets[0]; Vector2 position = target.transform.position, direction = target.Direction;
        // Screen edges can be beyond a pistol's 8 m range: approach the fixed world threat.
        lab.Player.transform.position += (Vector3)(position - (Vector2)lab.Player.transform.position).normalized * 5f;
        yield return .08f;
        var gun = lab.Station.Modules.OfType<EnemyOrbitalLabWeapon>().First();
        float current = Mathf.Atan2(gun.WorldPosition.y - lab.Station.transform.position.y, gun.WorldPosition.x - lab.Station.transform.position.x) * Mathf.Rad2Deg;
        float aim = Mathf.Atan2(position.y - lab.Station.transform.position.y, position.x - lab.Station.transform.position.x) * Mathf.Rad2Deg;
        foreach (var ring in lab.Station.Rings) ring.State.CurrentPhase += Mathf.DeltaAngle(current, aim);
        yield return .05f;
        report.Add($"COLLISION FIXTURE: turret {position}, gun {gun.WorldPosition}, distance {Vector2.Distance(position, gun.WorldPosition):0.00}, pistol range {gun.SourceProfile.Range:0.00}");
        for (int shot = 0; shot < 4 && target != null; shot++) { gun.FireLabExtraShot(); yield return .12f; }
        yield return .45f;
        Require(target == null && lab.LaserTurretsDestroyed > 0, "turret destroyed by real orbital projectiles before charge completes");
        int shotsBefore = lab.LaserShots; yield return 1.2f;
        Require(lab.LaserShots == shotsBefore, "destroyed turret never fires");
        lab.CancelInterception();
        // Suspend weapons during beam timing/contact checks, leaving the incident unmodified.
        lab.Station.enabled = false;
        foreach (var variant in new[] { EnemyOrbitalLabController.InterceptionVariant.Single, EnemyOrbitalLabController.InterceptionVariant.Sequential, EnemyOrbitalLabController.InterceptionVariant.Dual })
        {
            Require(lab.LaunchInterception(variant), "manual launch with automation OFF: " + variant);
            if (variant == EnemyOrbitalLabController.InterceptionVariant.Dual)
                Require(Mathf.Abs(Vector2.Dot(lab.LaserTurrets[0].Direction, lab.LaserTurrets[1].Direction)) <= Mathf.Cos(12f * Mathf.Deg2Rad), "dual lanes cross at a readable angle");
            Require(!lab.LaunchInterception(), "overlapping manual incident rejected");
            var turret = lab.LaserTurrets[0]; position = turret.transform.position; direction = turret.Direction;
            int shots = lab.LaserShots;
            yield return .3f;
            Require(turret != null && (Vector2)turret.transform.position == position && turret.Direction == direction && !turret.Firing, "turret and telegraph direction remain fixed during charge");
            if (variant == EnemyOrbitalLabController.InterceptionVariant.Single)
            {
                lab.SetPlayerInvincible(false); lab.Player.Heal(1000);
                lab.Player.transform.position = turret.Origin + turret.Direction * 4;
                int contacts = lab.LaserDamageContacts; float hp = lab.Player.CurrentHealth;
                yield return 1.85f;
                Require(lab.Player.CurrentHealth < hp && lab.LaserDamageContacts == contacts + 1, "beam contact applies damage once");
                yield return .3f; Require(lab.LaserDamageContacts == contacts + 1, "beam does not apply damage each frame");
                lab.SetPlayerInvincible(true); lab.Player.Heal(1000);
            }
            while (lab.InterceptionActive) yield return .02f;
            Require(lab.LaserShots - shots == (variant == EnemyOrbitalLabController.InterceptionVariant.Single ? 1 : 2), "expected shot count and incident completion: " + variant);
            lab.Player.transform.position = Vector3.zero; yield return .1f;
        }
        lab.SetEnemiesInvincible(true); lab.SetTargetEnemyDensity(25); lab.AutomaticInterceptions = true;
        lab.SetInterceptionIntervals(4, 4); yield return 3.7f;
        Require(!lab.InterceptionActive, "ordinary combat remains between incidents");
        yield return .6f; Require(lab.InterceptionActive, "automatic incident launches after configured interval");
        lab.CancelInterception(); lab.AutomaticInterceptions = false; lab.SetInterceptionIntervals(9, 16);
        lab.Station.enabled = true;
        lab.SetTargetEnemyDensity(150); lab.DensitySpawnRate = 40; lab.SetEnemiesInvincible(true);
        while (lab.AliveEnemies < 150) yield return .02f;
        lab.Station.enabled = false;
        lab.LaunchInterception(EnemyOrbitalLabController.InterceptionVariant.Dual);
        yield return 2.03f;
        Require(Screen.width == 1600 && Screen.height == 900, "Game View is 1600×900");
        ScreenCapture.CaptureScreenshot(Output + "/game-view.png"); yield return .1f;
        lab.Station.enabled = true; lab.SetEnemiesInvincible(false);
        lab.SetPaused(true); // Stable active-beam review frame. P/Tab resumes the personal Play Mode check.
        lab.AutomaticInterceptions = true;
        // Show the new controls on the review pause, preserving the active beam in the world.
        var view = typeof(EnemyOrbitalLabController).GetField("segmentedView", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(lab);
        view.GetType().GetField("tab", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(view, 1);
        yield return .1f; ScreenCapture.CaptureScreenshot(Output + "/controls.png"); yield return .1f;
        Require(lab.Station.ValidateState(out _), "orbital construction validates after all experiments");
    }
}
#endif
