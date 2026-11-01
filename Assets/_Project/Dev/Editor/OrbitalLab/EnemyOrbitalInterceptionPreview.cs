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
            { report.Clear(); errors = 0; deadline = EditorApplication.timeSinceStartup + (SessionState.GetBool(Key + ".Motion", false) ? 35 : 65);
                routine = SessionState.GetBool(Key + ".Motion", false) ? CheckMotion() : Check(); }
            if (state == PlayModeStateChange.EnteredEditMode) { SessionState.EraseBool(Key); SessionState.EraseBool(Key + ".Motion"); }
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
    [MenuItem("Tools/Subject42/Dev/OrbitalLab/Quick check Interception Motion")]
    public static void OpenMotion()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        SessionState.SetBool(Key + ".Motion", true); Open();
    }
    static void Poll()
    {
        string request = Output + "/editor.request";
        if (File.Exists(request) && !EditorApplication.isCompiling && !EditorApplication.isUpdating)
        {
            string action = File.ReadAllText(request).Trim(); File.Delete(request);
            if (action == "smoke" && !EditorApplication.isPlayingOrWillChangePlaymode) Open();
            else if (action == "motion" && !EditorApplication.isPlayingOrWillChangePlaymode) OpenMotion();
            else if (action == "stop") EditorApplication.ExitPlaymode();
            else if (action == "capture" && EditorApplication.isPlaying)
                ScreenCapture.CaptureScreenshot(Output + "/game-view.png");
        }
        if (routine == null || EditorApplication.timeSinceStartup < next) return;
        try
        {
            if (EditorApplication.timeSinceStartup > deadline) throw new Exception("Bounded preview deadline reached");
            if (!routine.MoveNext())
            { routine = null; report.Add(errors == 0 ? "PASS: short Play Mode preview; zero runtime errors. Left open for manual play." : "FAIL: runtime errors"); WriteReport(); return; }
            next = EditorApplication.timeSinceStartup + (routine.Current is float delay ? delay : .01);
        }
        catch (Exception e)
        { report.Add("FAIL: " + e); WriteReport(); routine = null; }
    }
    static void WriteReport() => File.WriteAllLines(Output + (SessionState.GetBool(Key + ".Motion", false) ? "/motion-result.txt" : "/result.txt"), report);
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
        yield return EnemyOrbitalLabLaserTurret.EnterSeconds + .08f;
        position = target.transform.position;
        var gun = AimOrbitalShot(lab, target);
        report.Add($"COLLISION FIXTURE: turret {position}, gun {gun.WorldPosition}, distance {Vector2.Distance(position, gun.WorldPosition):0.00}, weapon range {gun.SourceProfile.Range:0.00}");
        gun.FireLabExtraShot(); lab.Station.GetComponent<OrbitalCenterShift>().ResetOffset(); yield return .12f;
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
            var turret = lab.LaserTurrets[0]; direction = turret.Direction;
            int shots = lab.LaserShots;
            yield return EnemyOrbitalLabLaserTurret.EnterSeconds + .03f;
            position = turret.transform.position;
            yield return .3f;
            Require(turret != null && (Vector2)turret.transform.position == position && turret.Direction == direction && !turret.Firing, "turret and telegraph direction remain fixed during charge");
            if (variant == EnemyOrbitalLabController.InterceptionVariant.Single)
            {
                lab.SetPlayerInvincible(false); lab.Player.Heal(1000);
                var player = lab.Player.transform.position;
                player.y = turret.Origin.y + turret.Direction.y / turret.Direction.x * (player.x - turret.Origin.x);
                lab.Player.transform.position = player;
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
        yield return EnemyOrbitalLabLaserTurret.EnterSeconds + 2.03f;
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

    static void MovePlayer(EnemyOrbitalLabController lab, Vector2 position)
    {
        var body = lab.Player.GetComponent<Rigidbody2D>();
        if (body != null) { body.linearVelocity = Vector2.zero; body.position = position; }
        lab.Player.transform.position = position;
    }
    static EnemyOrbitalLabWeapon AimOrbitalShot(EnemyOrbitalLabController lab, EnemyOrbitalLabLaserTurret turret)
    {
        // With X following, approaching horizontally no longer closes the gap.
        // Exercise the existing center-shift path (RMB), without extending weapon range.
        var center = lab.Station.GetComponent<OrbitalCenterShift>();
        Vector2 toward = ((Vector2)turret.transform.position - (Vector2)lab.Player.transform.position).normalized;
        typeof(OrbitalCenterShift).GetMethod("Advance", BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(center, new object[] { toward, true, .5f });
        var gun = lab.Station.Modules.OfType<EnemyOrbitalLabWeapon>().OrderByDescending(g => g.SourceProfile.Range).First();
        Vector2 radial = gun.WorldPosition - (Vector2)lab.Station.transform.position;
        Vector2 aim = (Vector2)turret.transform.position - (Vector2)lab.Station.transform.position;
        float delta = Mathf.DeltaAngle(Mathf.Atan2(radial.y, radial.x) * Mathf.Rad2Deg, Mathf.Atan2(aim.y, aim.x) * Mathf.Rad2Deg);
        foreach (var ring in lab.Station.Rings) { ring.State.CurrentPhase += delta; ring.Tick(0); }
        return gun;
    }
    static void CheckGeometry(EnemyOrbitalLabLaserTurret turret)
    {
        foreach (string name in new[] { "Red warning", "Laser edge", "Laser core" })
        {
            var line = turret.transform.Find(name).GetComponent<LineRenderer>();
            Require(Vector2.Distance(line.GetPosition(0), turret.Origin) < .002f && Vector2.Distance(line.GetPosition(1), turret.End) < .002f,
                name + " uses the current damage segment");
        }
        Require(Mathf.Abs(turret.transform.Find("Red warning").GetComponent<LineRenderer>().widthMultiplier - EnemyOrbitalLabLaserTurret.BeamWidth) < .001f,
            "warning shows the full future damage width");
    }
    static IEnumerator CheckMotion()
    {
        EnemyOrbitalLabController lab;
        while ((lab = Object.FindFirstObjectByType<EnemyOrbitalLabController>()) == null || !lab.SandboxReady || !lab.Ready) yield return .02f;
        lab.AutomaticInterceptions = false; lab.ClearDensityEnemies(); lab.SetSegmentedOrbit(true);
        while (!lab.Ready || !lab.SegmentedOrbit || !lab.Segmented.Bound) yield return .02f;
        lab.Segmented.LoadPreset("Multi-Layer"); lab.Segmented.SetMotion(SegmentedOrbitConfiguration.Motion.Manual);
        lab.SetPaused(false); lab.SetPlayerInvincible(true); lab.Station.enabled = false;
        var movement = lab.Player.GetComponent<CharacterMovement2D>(); movement.enabled = false;
        MovePlayer(lab, Vector2.zero); yield return .1f;
        Require(lab.LaunchInterception(EnemyOrbitalLabController.InterceptionVariant.Single), "motion fixture starts in existing EnemyOrbitalLab");
        var turret = lab.LaserTurrets[0]; float fixedY = turret.FixedY, offset = turret.HorizontalOffset;
        Vector3 viewport = lab.CameraRig.ControlledCamera.WorldToViewportPoint(turret.transform.position);
        Require(viewport.x < 0 || viewport.x > 1, "entering starts behind the visible side edge");
        Require(turret.transform.parent == lab.transform && !turret.transform.IsChildOf(lab.Player.transform), "turret belongs to lab, not player");
        Require(!turret.Firing && turret.Phase == EnemyOrbitalLabLaserTurret.AttackPhase.Entering, "entry is a separate safe phase");
        yield return .15f;
        float entrySlide = Mathf.Abs(turret.transform.position.x - lab.Player.transform.position.x - offset);
        MovePlayer(lab, new Vector2(2, 3)); yield return .12f;
        Require(turret != null && Mathf.Abs(turret.transform.position.y - fixedY) < .001f, "entry follows X while Y stays fixed");
        Require(Mathf.Abs(turret.transform.position.x - lab.Player.transform.position.x - offset) < entrySlide, "entry slides smoothly inward");
        while (turret.Phase == EnemyOrbitalLabLaserTurret.AttackPhase.Entering) yield return .01f;
        Require(Mathf.Abs(turret.transform.position.x - lab.Player.transform.position.x - offset) < .002f, "settled turret has constant horizontal player offset");
        float beforeX = turret.transform.position.x;
        MovePlayer(lab, (Vector2)lab.Player.transform.position + new Vector2(3, -2)); yield return .08f;
        Require(Mathf.Abs(turret.transform.position.x - beforeX - 3) < .002f && Mathf.Abs(turret.transform.position.y - fixedY) < .001f,
            "charging follows a 3 m X move without following Y");
        CheckGeometry(turret);
        float nearY = lab.Player.transform.position.y;
        MovePlayer(lab, (Vector2)lab.Player.transform.position + Vector2.up * 35); yield return .12f;
        Require(turret != null && !turret.Health.IsDead && Mathf.Abs(turret.transform.position.y - fixedY) < .001f, "large Y move never deletes or teleports active turret");
        viewport = lab.CameraRig.ControlledCamera.WorldToViewportPoint(turret.transform.position);
        Require(viewport.y < 0 || viewport.y > 1, "turret naturally leaves camera vertically");
        MovePlayer(lab, new Vector2(lab.Player.transform.position.x, nearY)); yield return .05f;
        while (!turret.Firing) yield return .01f;
        MovePlayer(lab, (Vector2)lab.Player.transform.position + Vector2.right * 2); yield return .03f;
        Require(Mathf.Abs(turret.transform.position.x - lab.Player.transform.position.x - offset) < .002f && Mathf.Abs(turret.transform.position.y - fixedY) < .001f,
            "firing follows X with fixed Y");
        CheckGeometry(turret);
        float beamY = turret.Origin.y + turret.Direction.y / turret.Direction.x * (lab.Player.transform.position.x - turret.Origin.x);
        lab.SetPlayerInvincible(false); lab.Player.Heal(1000); float hp = lab.Player.CurrentHealth;
        MovePlayer(lab, new Vector2(lab.Player.transform.position.x, beamY)); yield return .05f;
        Require(lab.Player.CurrentHealth < hp, "moved rendered beam damages player on that same segment");
        lab.SetPlayerInvincible(true); lab.Player.Heal(1000);
        ScreenCapture.CaptureScreenshot(Output + "/motion-beam.png"); yield return .04f;
        while (turret.Phase != EnemyOrbitalLabLaserTurret.AttackPhase.Retracting) yield return .01f;
        Require(!turret.Firing && !turret.transform.Find("Laser edge").GetComponent<LineRenderer>().enabled, "attack ends with a harmless animated retreat");
        float retreatSlide = Mathf.Abs(turret.transform.position.x - lab.Player.transform.position.x - offset);
        MovePlayer(lab, (Vector2)lab.Player.transform.position + new Vector2(2, 4)); yield return .15f;
        Require(turret != null && Mathf.Abs(turret.transform.position.y - fixedY) < .001f &&
            Mathf.Abs(turret.transform.position.x - lab.Player.transform.position.x - offset) > retreatSlide, "retreat slides outward while still syncing only X");
        while (lab.InterceptionActive) yield return .01f;
        Require(turret == null, "turret retires only after completing retreat");

        MovePlayer(lab, Vector2.zero);
        // Reset the camera with this fixture teleport; otherwise its normal follow lag
        // leaves a deliberately huge offset when the next turret samples the visible edge.
        lab.CameraRig.transform.position = lab.Player.transform.position + lab.CameraRig.offset;
        yield return .15f;
        Require(lab.LaunchInterception(EnemyOrbitalLabController.InterceptionVariant.Dual), "dual motion fixture launches");
        while (lab.LaserTurrets.Any(t => t.Phase == EnemyOrbitalLabLaserTurret.AttackPhase.Entering)) yield return .01f;
        Require(lab.LaserTurrets[0].LeftSide != lab.LaserTurrets[1].LeftSide, "dual turrets remain on opposite sides");
        MovePlayer(lab, new Vector2(-3, 2)); yield return .05f;
        foreach (var actor in lab.LaserTurrets)
            Require(Mathf.Abs(actor.transform.position.x - lab.Player.transform.position.x - actor.HorizontalOffset) < .002f && Mathf.Abs(actor.transform.position.y - actor.FixedY) < .001f,
                "both dual turrets independently preserve X offset and fixed Y");
        turret = lab.LaserTurrets[0];
        var gun = AimOrbitalShot(lab, turret);
        Require(Vector2.Distance(gun.WorldPosition, turret.transform.position) < gun.SourceProfile.Range + .5f,
            $"existing orbital weapon reaches following turret with center shift: distance={Vector2.Distance(gun.WorldPosition, turret.transform.position):0.00}, range={gun.SourceProfile.Range:0.00}, ring={gun.CurrentMount.Ring.Radius:0.00}, offset={turret.HorizontalOffset:0.00}");
        int destroyed = lab.LaserTurretsDestroyed, shots = lab.LaserShots;
        gun.FireLabExtraShot();
        ((ProductionOrbitalCombatAdapter)lab.Station.Combat).Tick(.12f);
        lab.Station.GetComponent<OrbitalCenterShift>().ResetOffset(); yield return .04f;
        Require(turret == null && lab.LaserTurretsDestroyed > destroyed, "real orbital projectile destroys moving turret");
        Require(Object.FindObjectsByType<EnemyOrbitalLabTurretDestruction>(FindObjectsSortMode.None).Length > 0, "destruction leaves independent pixel debris effect");
        ScreenCapture.CaptureScreenshot(Output + "/motion-destruction.png");
        Require(lab.LaserShots == shots, "destroyed charging turret never fires");
        lab.CancelInterception(); yield return .5f;
        Require(Object.FindObjectsByType<EnemyOrbitalLabTurretDestruction>(FindObjectsSortMode.None).Length == 0, "short destruction effect cleans itself up");
        movement.enabled = true; MovePlayer(lab, Vector2.zero); lab.Station.enabled = false; yield return .1f;
        lab.LaunchInterception(EnemyOrbitalLabController.InterceptionVariant.Single);
        yield return EnemyOrbitalLabLaserTurret.EnterSeconds + .3f;
        var bodySprite = lab.LaserTurrets[0].transform.Find("Pixel turret").GetComponent<SpriteRenderer>();
        report.Add($"VISUAL: turret sprite={bodySprite.sprite.rect.width}×{bodySprite.sprite.rect.height}, material={bodySprite.sharedMaterial.shader.name}, center pixel={bodySprite.sprite.texture.GetPixel(12, 12)}");
        ScreenCapture.CaptureScreenshot(Output + "/motion-warning.png"); yield return .05f;
        lab.Station.enabled = true; lab.SetPaused(true); lab.AutomaticInterceptions = true;
        Require(lab.Station.ValidateState(out _), "construction remains valid; Play Mode left on review pause");
    }
}
#endif
