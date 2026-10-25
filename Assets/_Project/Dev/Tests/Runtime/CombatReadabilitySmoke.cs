using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Subject42.Combat.OrbitalStation;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

// Explicit command-line verification only. Uses production scenes and release APIs.
// The QA build has a separate product/save identity; ordinary launches do nothing here.
public sealed class CombatReadabilitySmoke : MonoBehaviour
{
    private string output;
    private readonly List<string> errors = new();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Launch()
    {
        string[] args = Environment.GetCommandLineArgs();
        int argument = Array.IndexOf(args, "-combat-readability-qa");
        if (argument < 0) argument = Array.IndexOf(args, "-orbital-hierarchy-qa");
        if (argument < 0) argument = Array.IndexOf(args, "-environment-pace-qa");
        if (argument < 0) argument = Array.IndexOf(args, "-anomaly-lifecycle-qa");
        if (argument < 0) argument = Array.IndexOf(args, "-enemy-feedback-qa");
        if (argument < 0) argument = Array.IndexOf(args, "-compute-buffer-qa");
        if (argument < 0 || argument + 1 >= args.Length) return;
        var runner = new GameObject("Combat readability verification").AddComponent<CombatReadabilitySmoke>();
        DontDestroyOnLoad(runner.gameObject);
        runner.output = Path.GetFullPath(args[argument + 1]);
        Directory.CreateDirectory(runner.output);
        Application.runInBackground = true;
        Application.SetStackTraceLogType(LogType.Error, StackTraceLogType.Full);
        Application.logMessageReceived += runner.ObserveLog;
        runner.StartCoroutine(runner.RunChecked());
    }

    private void ObserveLog(string message, string stack, LogType type)
    {
        if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
        {
            errors.Add(message);
            File.AppendAllText(output + "/errors.log", message + "\n" + stack + "\n");
            File.WriteAllText(output + "/status.txt", "FAIL\n" + string.Join("\n", errors));
        }
    }

    private IEnumerator RunChecked()
    {
        File.WriteAllText(output + "/status.txt", "RUNNING");
        var stack = new Stack<IEnumerator>();
        stack.Push(RunStandalone());
        Exception failure = null;
        while (stack.Count > 0)
        {
            object next = null;
            bool alive = false;
            try { alive = stack.Peek().MoveNext(); if (alive) next = stack.Peek().Current; }
            catch (Exception exception) { failure = exception; }
            if (failure != null) break;
            if (!alive) { stack.Pop(); continue; }
            if (next is IEnumerator nested) stack.Push(nested);
            else yield return next;
        }
        if (errors.Count > 0 && failure == null) failure = new InvalidOperationException(string.Join("\n", errors));
        File.WriteAllText(output + "/status.txt", failure == null ? "PASS" : "FAIL\n" + failure);
        // Keep observing through native application shutdown, not only the scenario.
        Application.Quit(failure == null ? 0 : 1);
    }

    private IEnumerator RunStandalone()
    {
        Require(!Application.isEditor && !Debug.isDebugBuild, "Expected an ordinary non-Development player.");
        Screen.SetResolution(1600, 900, false);
        PlayerPrefs.SetInt(BunkerIntroController.ViewedPreferenceKey, 1);
        PlayerPrefs.SetInt(TutorialController.CompletionKey, 1);
        yield return SceneManager.LoadSceneAsync("MainMenu");
        yield return WaitFor(() => Object.FindFirstObjectByType<BunkerPlayerLoadoutController>() is { IsReady: true } &&
            !SceneTransitionOverlay.IsTransitioning);
        var starter = Object.FindFirstObjectByType<BunkerRunStarter>();
        if (!GameplayRandom.IsActive) GameplayRandom.Begin(1709);
        starter.StartRun(starter.transform);
        yield return WaitFor(() => SceneManager.GetActiveScene().name == "MVP" &&
            Object.FindFirstObjectByType<OrbitalStationRuntime>() is { IsInitialized: true } && !SceneTransitionOverlay.IsTransitioning);
        if (Array.IndexOf(Environment.GetCommandLineArgs(), "-compute-buffer-qa") >= 0)
            yield return ComputeBufferLifecycleSmoke.VerifyCurrentRun(output);
        else if (Array.IndexOf(Environment.GetCommandLineArgs(), "-enemy-feedback-qa") >= 0)
        {
            yield return EnemyFlashTintSmoke.Verify(output);
            yield return EnemyFeedbackSmoke.VerifyCurrentRun(output);
            yield return AnomalyLifecycleSmoke.VerifyCurrentRun(output + "/Lifecycle");
        }
        else if (Array.IndexOf(Environment.GetCommandLineArgs(), "-anomaly-lifecycle-qa") >= 0)
            yield return AnomalyLifecycleSmoke.VerifyCurrentRun(output);
        else if (Array.IndexOf(Environment.GetCommandLineArgs(), "-environment-pace-qa") >= 0)
            yield return EnvironmentPaceSmoke.VerifyCurrentRun(output);
        else if (Array.IndexOf(Environment.GetCommandLineArgs(), "-orbital-hierarchy-qa") >= 0)
            yield return OrbitalHierarchySmoke.VerifyCurrentRun(output);
        else yield return VerifyCurrentRun(output);
    }

    public static IEnumerator VerifyCurrentRun(string directory)
    {
        Directory.CreateDirectory(directory);
        var trace = new StringBuilder();
        trace.AppendLine("Editor=" + Application.isEditor + " Development=" + Debug.isDebugBuild + " Scene=" + SceneManager.GetActiveScene().path);
        yield return WaitFor(() => EnemyHealth.ActiveInstances.Any(e => !e.IsBoss));
        var target = EnemyHealth.ActiveInstances.First(e => !e.IsBoss);
        Object.FindFirstObjectByType<RunFlowController>().StopRunGameplay();
        foreach (var enemy in EnemyHealth.ActiveInstances.ToArray()) if (enemy != target) Object.Destroy(enemy.gameObject);
        foreach (var behaviour in target.GetComponents<MonoBehaviour>())
            if (behaviour is not EnemyHealth && behaviour is not EnemyHealthPresentation && behaviour is not EnemyWhiteFlash)
                behaviour.enabled = false;
        var targetBody = target.GetComponent<Rigidbody2D>();
        if (targetBody != null) targetBody.constraints = RigidbodyConstraints2D.FreezeAll;

        var player = Object.FindFirstObjectByType<CharacterSpawner>().SpawnedPlayer;
        var health = player.GetComponent<PlayerHealth>();
        var playerBody = player.GetComponent<Rigidbody2D>();
        playerBody.constraints = RigidbodyConstraints2D.FreezeAll;
        playerBody.linearVelocity = Vector2.zero;
        target.transform.position = player.transform.position + Vector3.right * 3.4f;
        if (targetBody != null) targetBody.position = target.transform.position;
        target.SetRuntimeMaxHealth(1000f);
        Physics2D.SyncTransforms();
        var station = player.GetComponentInChildren<OrbitalStationRuntime>();
        station.InputOwner.enabled = false;
        var initial = station.Modules.OfType<OrbitalProjectileModule>().First();
        int ring = initial.CurrentMount.Ring.RingId, mount = initial.CurrentMount.MountIndex;
        foreach (var module in station.Modules.ToArray()) station.RemoveModule(module.StableModuleId);
        var adapter = (ProductionOrbitalCombatAdapter)station.Combat;
        adapter.DebugClearProjectiles();
        var follow = Object.FindFirstObjectByType<CameraFollow>();
        follow.IsPresentationSuspended = true;
        var camera = follow.ControlledCamera;
        Vector3 framing = new(player.transform.position.x + 1.1f, player.transform.position.y + 1f, camera.transform.position.z);
        // Move the follow rig, preserving the camera child's authored shake origin.
        follow.transform.position += framing - camera.transform.position;
        camera.orthographicSize = 6f;
        yield return null;
        yield return null;
        Require(Screen.width == 1600 && Screen.height == 900, "Expected Game View at 1600x900.");
        health.SetRuntimeHealth(100f, 100f);
        var hud = Object.FindFirstObjectByType<HUDManager>();
        var vignette = (CanvasGroup)typeof(HUDManager).GetField("lowHpVignette", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(hud);
        yield return Capture(directory, "01-before-damage");
        Require(health.TakeDamage(15f, Vector2.right), "First player damage rejected.");
        Require(health.CurrentHealth == 85f, "Player damage changed.");
        Require(vignette.gameObject.activeInHierarchy && vignette.alpha > .15f, "Damage vignette is not visible.");
        var flashed = player.GetComponentsInChildren<SpriteRenderer>().FirstOrDefault(s => s.enabled &&
            s.sharedMaterial != null && s.sharedMaterial.HasProperty("_FlashColor"));
        Require(flashed != null && flashed.sharedMaterial.GetColor("_FlashColor").r >
            flashed.sharedMaterial.GetColor("_FlashColor").g * 1.5f, "Player flash is not distinct from enemy white.");
        Require(!health.TakeDamage(15f, Vector2.right) && health.CurrentHealth == 85f, "Invulnerability changed.");
        trace.AppendLine("Accepted player hit: 100 -> 85; repeated hit rejected; warm flash and active HUD pulse PASS");
        yield return Capture(directory, "02-player-damage");
        yield return WaitSeconds(.4f);
        Require(vignette.alpha < .01f, "Damage pulse did not recover.");
        yield return Capture(directory, "03-player-recovered");
        health.SetRuntimeHealth(100f, 20f);
        Require(vignette.alpha > .15f, "Existing low-health baseline is not visible.");
        yield return Capture(directory, "04-low-health");
        health.SetRuntimeHealth(100f, 100f);
        trace.AppendLine("Pulse recovery and existing low-HP vignette PASS");

        foreach (var kind in new[] { OrbitalModuleKind.Pistol, OrbitalModuleKind.Shotgun, OrbitalModuleKind.Sniper })
        {
            target.SetRuntimeMaxHealth(1000f);
            Require(station.InstallModule(kind, ring, mount, out string error), "Install failed: " + error);
            var cannon = station.Modules.OfType<OrbitalProjectileModule>().Single(m => m.Kind == kind);
            string settings = JsonUtility.ToJson(cannon.Settings);
            bool hit = false;
            float impactAt = 0f;
            Vector2 point = default;
            Color impactColor = default;
            string shotPath = Path.Combine(directory, "05-shot-" + kind + ".png");
            string impactPath = Path.Combine(directory, "06-impact-" + kind + ".png");
            if (File.Exists(shotPath)) File.Delete(shotPath);
            if (File.Exists(impactPath)) File.Delete(impactPath);
            void OnImpact(Vector2 p, Vector2 direction, Color color)
            {
                if (hit) return;
                hit = true; point = p; impactColor = color; impactAt = Time.time;
                ScreenCapture.CaptureScreenshot(impactPath);
            }
            adapter.ProjectileImpact += OnImpact;
            yield return WaitFor(() => cannon.ShotsFired > 0);
            var view = cannon.CurrentMount.Transform.GetComponentInChildren<OrbitalModuleView>();
            Require(view.Halo.enabled && view.Halo.color.a > .3f, "Shot pulse missing for " + kind);
            Require(HaloDoesNotChangeContact(cannon, view.Halo), "Glow changed contact geometry for " + kind);
            ScreenCapture.CaptureScreenshot(shotPath);
            yield return WaitFor(() => hit);
            while (Time.time < impactAt + .09f) yield return null;
            var lines = station.GetComponentsInChildren<LineRenderer>().Where(l => l.enabled && l.positionCount == 2 &&
                Vector2.Distance(l.GetPosition(0), point) < .8f).ToArray();
            Require(lines.Length >= 2, "Impact too brief for " + kind + " age=" + (Time.time - impactAt) + " lines=" + lines.Length);
            Require(settings == JsonUtility.ToJson(cannon.Settings), "Feedback mutated gameplay settings for " + kind);
            trace.AppendLine(kind + ": real fire/impact, halo excluded from body contact, settings unchanged; color=" + impactColor + " lines=" + lines.Length + " PASS");
            yield return WaitFor(() => File.Exists(shotPath) && File.Exists(impactPath));
            adapter.ProjectileImpact -= OnImpact;
            station.RemoveModule(cannon.StableModuleId);
            adapter.DebugClearProjectiles();
            yield return WaitGameSeconds(.4f);
            Require(lines.All(l => !l.enabled), "Impact pool did not recover.");
        }

        target.SetRuntimeMaxHealth(1000f);
        bool controlHit = false;
        void ControlImpact(Vector2 _, Vector2 __, Color ___) => controlHit = true;
        adapter.ProjectileImpact += ControlImpact;
        adapter.SpawnWeaponProjectile(player.transform.position, target, Vector2.right,
            OrbitalPresentationConfig.Active.GetWeaponSettings(OrbitalModuleKind.Pistol), null, 8f);
        yield return WaitFor(() => controlHit);
        Require(Mathf.Approximately(target.CurrentHealth, 992f), "Control projectile must still deal exactly eight damage.");
        adapter.ProjectileImpact -= ControlImpact;
        trace.AppendLine("Control projectile: 1000 -> 992; pooling and unchanged damage PASS");
        trace.AppendLine("Resolution=" + Screen.width + "x" + Screen.height + "; normal production assets; physical input not simulated.");
        File.WriteAllText(directory + "/runtime-checks.txt", trace.ToString());
    }

    private static IEnumerator Capture(string directory, string name)
    {
        Canvas.ForceUpdateCanvases();
        string path = Path.Combine(directory, name + ".png");
        if (File.Exists(path)) File.Delete(path);
        ScreenCapture.CaptureScreenshot(path);
        yield return WaitFor(() => File.Exists(path));
    }

    public static bool HaloDoesNotChangeContact(OrbitalModuleRuntime cannon, SpriteRenderer halo)
    {
        // Some authored cannon bodies extend beyond the halo's bounding-box corner.
        // Compare the same physical silhouette with the glow on/off instead.
        bool visible = halo.enabled;
        var probes = new Vector2[16];
        var contacts = new bool[16];
        float radius = Mathf.Max(halo.bounds.extents.x, halo.bounds.extents.y);
        for (int i = 0; i < probes.Length; i++)
        {
            float angle = i * Mathf.PI * 2f / probes.Length;
            probes[i] = (Vector2)halo.bounds.center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
            contacts[i] = cannon.HasBodyContact(probes[i], .01f);
        }
        halo.enabled = false;
        bool same = true;
        for (int i = 0; i < probes.Length; i++) same &= contacts[i] == cannon.HasBodyContact(probes[i], .01f);
        halo.enabled = visible;
        return same;
    }

    private static IEnumerator WaitFor(Func<bool> condition)
    {
        float deadline = Time.realtimeSinceStartup + 30f;
        while (!condition() && Time.realtimeSinceStartup < deadline) yield return null;
        Require(condition(), "Production scenario timed out.");
    }
    private static IEnumerator WaitSeconds(float seconds)
    {
        float until = Time.realtimeSinceStartup + seconds;
        while (Time.realtimeSinceStartup < until) yield return null;
    }
    private static IEnumerator WaitGameSeconds(float seconds)
    {
        float until = Time.time + seconds;
        while (Time.time < until) yield return null;
    }
    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
