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

// Explicit QA scenario only, entered by the existing isolated-save player runner.
public static class OrbitalHierarchySmoke
{
    private static readonly BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;

    public static IEnumerator VerifyCurrentRun(string directory)
    {
        Directory.CreateDirectory(directory);
        var failures = new List<string>();
        var trace = new StringBuilder();
        trace.AppendLine($"Editor={Application.isEditor} Development={Debug.isDebugBuild} Scene={SceneManager.GetActiveScene().path} Resolution={Screen.width}x{Screen.height}");
        float until = Time.realtimeSinceStartup + 15f;
        while (EnemyHealth.ActiveInstances.Count < 6 && Time.realtimeSinceStartup < until) yield return null;
        Object.FindFirstObjectByType<RunFlowController>().StopRunGameplay();
        var player = Object.FindFirstObjectByType<CharacterSpawner>().SpawnedPlayer;
        var body = player.GetComponent<Rigidbody2D>();
        body.constraints = RigidbodyConstraints2D.FreezeAll;
        body.linearVelocity = Vector2.zero;
        player.GetComponent<PlayerHealth>().SetRuntimeHealth(10000f, 10000f);
        var station = player.GetComponentInChildren<OrbitalStationRuntime>();
        station.InputOwner.enabled = false;
        var site = ProductionAnomalySite.ActiveSites.Where(s => !s.IsSpecial && s.EnvironmentAlive)
            .OrderBy(s => Vector2.Distance(s.transform.position, player.transform.position)).First();
        Rect bounds = site.TerritoryBounds;
        Vector2 position = new(bounds.xMin + 3f, bounds.center.y);
        body.position = position;
        player.transform.position = position;
        int count = 0;
        foreach (var enemy in EnemyHealth.ActiveInstances.ToArray())
        {
            if (count >= 8) { Object.Destroy(enemy.gameObject); continue; }
            foreach (var behaviour in enemy.GetComponents<MonoBehaviour>())
                if (behaviour is not EnemyHealth && behaviour is not EnemyHealthPresentation && behaviour is not EnemyWhiteFlash)
                    behaviour.enabled = false;
            Vector2 point = position + new Vector2(3.5f + count % 3 * .6f, (count / 3 - 1) * .65f);
            enemy.transform.position = point;
            var enemyBody = enemy.GetComponent<Rigidbody2D>();
            if (enemyBody != null) { enemyBody.constraints = RigidbodyConstraints2D.FreezeAll; enemyBody.position = point; }
            enemy.SetRuntimeMaxHealth(10000f);
            count++;
        }
        Physics2D.SyncTransforms();
        var follow = Object.FindFirstObjectByType<CameraFollow>();
        follow.IsPresentationSuspended = true;
        var camera = follow.ControlledCamera;
        follow.transform.position += new Vector3(position.x, position.y + 1f, camera.transform.position.z) - camera.transform.position;
        camera.orthographicSize = 7f;
        foreach (var module in station.Modules.ToArray()) station.RemoveModule(module.StableModuleId);
        var ring = station.Rings.First();
        while (ring.Mounts.Count < 3)
            if (!station.AddMount(ring.RingId, out string error)) throw new InvalidOperationException(error);
        int index = 0;
        foreach (var kind in new[] { OrbitalModuleKind.Pistol, OrbitalModuleKind.Shotgun, OrbitalModuleKind.Sniper })
            if (!station.InstallModule(kind, ring.RingId, index++, out string error)) throw new InvalidOperationException(error);
        float settle = Time.time + 1.4f;
        while (Time.time < settle) yield return null;
        var anomalies = Object.FindFirstObjectByType<LevelAnomalyController>();
        var focusedZone = (LocalAnomalyZone)typeof(LevelAnomalyController).GetField("focusedZone", Private).GetValue(anomalies);
        if (focusedZone == null || !anomalies.IsAnomalyFocusActive)
            throw new InvalidOperationException("Expected an active production anomaly focus.");
        Bounds focusBounds = focusedZone.FocusArea.bounds;
        Vector3 outside = new(focusBounds.min.x - 1f, player.transform.position.y + 2.5f, 0f);
        var outsideEnemy = EnemyHealth.ActiveInstances.First();
        outsideEnemy.transform.position = outside;
        var outsideBody = outsideEnemy.GetComponent<Rigidbody2D>();
        if (outsideBody != null) outsideBody.position = outside;
        Physics2D.SyncTransforms();
        yield return null;
        Vector3 viewport = camera.WorldToViewportPoint(outsideEnemy.transform.position);
        Check(!focusedZone.FocusArea.OverlapPoint(outsideEnemy.transform.position) && viewport.x > .05f && viewport.x < .95f &&
            viewport.y > .05f && viewport.y < .95f, "The outside threat must be outside the focus area and visible in Game View.");
        var overlay = (SpriteRenderer)typeof(LevelAnomalyController).GetField("focusOverlay", Private).GetValue(anomalies);
        var properties = new MaterialPropertyBlock();
        overlay.GetPropertyBlock(properties);
        float appliedDarkness = properties.GetFloat("_OutsideDarkness");
        float appliedFocus = properties.GetFloat("_FocusAmount");
        Check(overlay.enabled && appliedFocus > .9f && appliedDarkness <= .4f,
            "Verify the active rendered focus mask, not only its stored configuration.");
        trace.AppendLine($"Outside threat={outsideEnemy.name} world={outsideEnemy.transform.position} viewport={viewport} focusBounds={focusBounds} renderedFocus={appliedFocus:F3} renderedDarkness={appliedDarkness:F3}");
        yield return Capture(directory, "01-territory-and-three-cannons");
        var fill = (Mesh)typeof(ProductionAnomalySite).GetField("territoryMesh", Private).GetValue(site);
        float fillAlpha = fill.colors[0].a;
        var boundaries = site.GetComponentsInChildren<LineRenderer>().Where(l => l.name == "Site Boundary" && l.enabled).ToArray();
        trace.AppendLine($"Territory={site.name} Bounds={bounds} tint={fillAlpha:F3} boundaries={boundaries.Length} alphaMax={boundaries.Max(l => l.startColor.a):F3} enemies={count}");
        Check(fillAlpha <= .08f, "Passive territory tint must not dominate the floor.");
        Check(boundaries.Length > 0 && boundaries.All(l => l.startColor.a <= .55f && l.startColor.a > .05f), "Territory borders must remain visible below combat accents.");
        Check(count >= 3, "Expected an actual production enemy group.");
        Check(Screen.width == 1600 && Screen.height == 900, "Expected 1600x900 Game View.");
        Check(anomalies.OutsideDarkness <= .4f && anomalies.OutsideDesaturation <= .4f,
            "Anomaly focus must not conceal threats outside the active zone.");
        trace.AppendLine($"Focus darkness={anomalies.OutsideDarkness:F3} desaturation={anomalies.OutsideDesaturation:F3}");

        // Freeze the existing production presentation clock to capture exact states.
        // No new sprite, collider or effect is created by these visual probes.
        station.enabled = false;
        Time.timeScale = 0f;
        var presentations = station.Modules.OfType<OrbitalProjectileModule>().Select(m => new
        {
            Module = m,
            Presentation = typeof(OrbitalModuleRuntime).GetField("presentation", Private).GetValue(m),
            View = ((GameObject)typeof(OrbitalModuleRuntime).GetField("Visual", Private).GetValue(m)).GetComponent<OrbitalModuleView>()
        }).ToArray();
        foreach (var item in presentations)
        {
            Invoke(item.Presentation, "ResetWeaponFeedback");
            item.Module.SetHighlighted(true);
            Invoke(item.Presentation, "Tick");
            var halo = item.View.Halo;
            Check(halo.enabled && halo.color.a > .1f && halo.color.a < .4f, item.Module.Kind + " selection halo must remain below shot brightness.");
            Check(CombatReadabilitySmoke.HaloDoesNotChangeContact(item.Module, halo), "Halo changed body contact.");
            trace.AppendLine($"{item.Module.Kind} selection halo enabled={halo.enabled} color={halo.color}");
        }
        yield return Capture(directory, "02-selection");
        foreach (var item in presentations)
        {
            Invoke(item.Presentation, "WeaponRecoil", item.Module.Settings.Recoil);
            Invoke(item.Presentation, "Tick");
            Check(item.View.Halo.enabled && item.View.Halo.color.a > .4f, item.Module.Kind + " shot must remain stronger than selection.");
            foreach (var sprite in item.View.Sprites.Where(s => s != item.View.Halo && s.enabled))
            {
                // At least one of the two weaker channels must remain distinctly below white.
                Check(Mathf.Min(sprite.color.r, Mathf.Min(sprite.color.g, sprite.color.b)) < .7f,
                    item.Module.Kind + " firing washed the authored cannon to white.");
            }
            trace.AppendLine($"{item.Module.Kind} shot halo={item.View.Halo.color}; settings={JsonUtility.ToJson(item.Module.Settings)}");
        }
        yield return Capture(directory, "03-colored-shot");
        foreach (var item in presentations)
        {
            Invoke(item.Presentation, "ResetWeaponFeedback");
            item.Module.SetHighlighted(true);
            Invoke(item.Presentation, "Tick");
            Check(item.View.Halo.enabled, "Selection must survive shot reset.");
            item.Module.SetHighlighted(false);
            Check(!item.View.Halo.enabled, "Idle cannon must not keep a charge or shot halo.");
        }
        yield return Capture(directory, "04-idle-recovered");
        Time.timeScale = 1f;
        station.enabled = true;
        trace.AppendLine(failures.Count == 0 ? "PASS" : string.Join("\n", failures));
        File.WriteAllText(directory + "/runtime-checks.txt", trace.ToString());
        if (failures.Count > 0) throw new InvalidOperationException(string.Join("\n", failures));
        void Check(bool valid, string reason) { if (!valid) failures.Add(reason); }
    }

    private static void Invoke(object presentation, string method, params object[] args) =>
        presentation.GetType().GetMethod(method).Invoke(presentation, args);

    private static IEnumerator Capture(string directory, string name)
    {
        string path = Path.GetFullPath(Path.Combine(directory, name + ".png"));
        if (File.Exists(path)) File.Delete(path);
        ScreenCapture.CaptureScreenshot(path);
        float until = Time.realtimeSinceStartup + 5f;
        while (!File.Exists(path) && Time.realtimeSinceStartup < until) yield return null;
        if (!File.Exists(path)) throw new IOException("Game View capture missing: " + path);
    }
}
