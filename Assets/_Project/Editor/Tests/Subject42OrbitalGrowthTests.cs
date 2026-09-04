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
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

public sealed class Subject42OrbitalGrowthTests
{
    private const string Output = "Artifacts/OrbitalGrowth/After";
    private static void Call(object target, string name, params object[] args) => target.GetType()
        .GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, args);

    [UnityTest]
    public IEnumerator ProductionScene_GrowthSwitchRestoreCombatAndScreenshots()
    {
        Directory.CreateDirectory(Output);
        File.WriteAllText(Output + "/measurements.txt", "Production MVP / unchanged gameplay camera\n");
        yield return new EnterPlayMode();
        EditorApplication.isPaused = false;
        Application.runInBackground = true;
        yield return Run();
        yield return new ExitPlayMode();
    }

    private static IEnumerator Run()
    {
        yield return SceneManager.LoadSceneAsync("MainMenu");
        float deadline = Time.realtimeSinceStartup + 25f;
        BunkerRunStarter starter;
        while ((starter = Object.FindFirstObjectByType<BunkerRunStarter>()) == null && Time.realtimeSinceStartup < deadline)
            yield return null;
        Assert.That(starter, Is.Not.Null);
        CharacterData character = AssetDatabase.FindAssets("t:CharacterData")
            .Select(id => AssetDatabase.LoadAssetAtPath<CharacterData>(AssetDatabase.GUIDToAssetPath(id)))
            .First(c => c != null && c.characterPrefab != null);
        RunSelectionManager.Instance.SelectCharacter(character);
        var rig = (Transform)typeof(BunkerRunStarter)
            .GetField("cameraRig", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(starter);
        starter.StartRun(rig);
        Assert.That(starter.IsTransitioning, Is.True);
        deadline = Time.realtimeSinceStartup + 35f;
        OrbitalStationRuntime station = null;
        while (Time.realtimeSinceStartup < deadline)
        {
            station = Object.FindFirstObjectByType<OrbitalStationRuntime>();
            if (SceneManager.GetActiveScene().name == "MVP" && station != null && station.IsInitialized) break;
            yield return null;
        }
        Assert.That(station != null && station.IsInitialized, Is.True);
        // Let startup messages expire naturally and the production camera settle.
        deadline = Time.realtimeSinceStartup + 10f;
        while (Time.realtimeSinceStartup < deadline) yield return null;
        Time.timeScale = 0f;
        station.enabled = false;
        var menu = Object.FindFirstObjectByType<Subject42DebugMenu>();
        Assert.That(menu, Is.Not.Null);
        var manager = RunStateManager.Instance;
        var sector = manager.CurrentSector;
        Camera camera = Camera.main;
        float originalSize = camera.orthographicSize;
        Vector3 originalPosition = camera.transform.position;
        int runId = station.State.RunId;
        var captured = new HashSet<OrbitalStationRuntime.GrowthPreset>();
        try
        {
            foreach (var preset in new[] { OrbitalStationRuntime.GrowthPreset.Beginning,
                OrbitalStationRuntime.GrowthPreset.Mid, OrbitalStationRuntime.GrowthPreset.Final,
                OrbitalStationRuntime.GrowthPreset.Beginning, OrbitalStationRuntime.GrowthPreset.Final })
            {
                bool capture = captured.Add(preset);
                Call(menu, "SetOpen", true);
                Call(menu, "LoadOrbitalGrowthPreset", station, preset);
                Assert.That(Subject42DebugMenu.IsDebugMenuOpen, Is.False);
                Assert.That(station.IsInitialized, Is.True);
                Assert.That(station.State.RunId, Is.GreaterThan(runId));
                runId = station.State.RunId;
                Assert.That(station.State, Is.SameAs(manager.OrbitalStationState));
                Assert.That(manager.CurrentSector, Is.SameAs(sector));
                Assert.That(UpgradeManager.Instance.IsRewardQueueIdle, Is.True);
                station.enabled = false;
                foreach (var ring in station.Rings) ring.Tick(0f);
                foreach (var link in station.Modules.Where(m => m.Kind == OrbitalModuleKind.LinkNode))
                    typeof(OrbitalModuleRuntime).GetField("Cooldown", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(link, 1f);
                Call(station, "UpdateLinkNodes", 0f);
                // Destroy is deferred: verify the settled hierarchy, including inactive objects.
                for (int i = 0; i < 4; i++) yield return null;
                Verify(station, preset);
                deadline = Time.realtimeSinceStartup + 2.5f;
                while (Time.realtimeSinceStartup < deadline) yield return null;
                if (preset == OrbitalStationRuntime.GrowthPreset.Beginning)
                    Assert.That(camera.orthographicSize, Is.EqualTo(originalSize).Within(.12f));
                else
                    Assert.That(camera.orthographicSize, Is.GreaterThan(originalSize));
                Assert.That(Vector3.Distance(camera.transform.position, originalPosition), Is.LessThan(.02f));
                if (capture)
                {
                    Measure(station, camera, preset);
                    File.Delete(Output + "/" + preset.ToString().ToUpperInvariant() + ".png");
                    ScreenCapture.CaptureScreenshot(Path.GetFullPath(Output + "/" + preset.ToString().ToUpperInvariant() + ".png"));
                    deadline = Time.realtimeSinceStartup + 8f;
                    while (!File.Exists(Output + "/" + preset.ToString().ToUpperInvariant() + ".png") && Time.realtimeSinceStartup < deadline)
                        yield return null;
                    Assert.That(File.Exists(Output + "/" + preset.ToString().ToUpperInvariant() + ".png"), Is.True);
                }
            }
            VerifyCombat(station);
            Assert.That(station.RebuildRuntimeFromState(), Is.True);
            station.enabled = false;
            Call(station, "UpdateLinkNodes", 0f);
            for (int i = 0; i < 4; i++) yield return null;
            Verify(station, OrbitalStationRuntime.GrowthPreset.Final);
            VerifyMountPresentation(station);
            var message = RunMessageService.Instance.View;
            message.ShowStartupHint("WASD — ДВИЖЕНИЕ\nИССЛЕДУЙТЕ ANOMALY SITES ИЛИ СРАЗУ ИДИТЕ К EXIT\nE — ВЗАИМОДЕЙСТВИЕ С EVENT", 6f);
            deadline = Time.realtimeSinceStartup + 2.5f;
            while (Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(message.IsPanelVisible, Is.True);
            Measure(station, camera, OrbitalStationRuntime.GrowthPreset.Final);
            ScreenCapture.CaptureScreenshot(Path.GetFullPath(Output + "/FINAL_HINT.png"));
            deadline = Time.realtimeSinceStartup + 4f;
            while (message.IsPanelVisible && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(message.IsPanelVisible, Is.False);

            station.ApplyPresetMid();
            // The fifth empty ring fits inside the existing outer Sword envelope.
            station.AddRing();
            station.enabled = false;
            deadline = Time.realtimeSinceStartup + 2.5f;
            while (Time.realtimeSinceStartup < deadline) yield return null;
            float startZoom = camera.orthographicSize;
            var added = station.AddRing();
            Assert.That(added, Is.Not.Null);
            Assert.That(camera.orthographicSize, Is.EqualTo(startZoom), "AddRing must not write the camera immediately");
            float prior = startZoom, maxStep = 0f;
            station.enabled = true;
            Time.timeScale = 1f;
            deadline = Time.realtimeSinceStartup + 2.5f;
            while (Time.realtimeSinceStartup < deadline)
            {
                yield return null;
                float zoom = camera.orthographicSize;
                maxStep = Mathf.Max(maxStep, Mathf.Abs(zoom - prior));
                Assert.That(zoom, Is.GreaterThanOrEqualTo(prior - .01f));
                prior = zoom;
            }
            Assert.That(camera.orthographicSize, Is.GreaterThan(startZoom + .1f));
            Assert.That(maxStep, Is.LessThan(.25f));
            File.AppendAllText(Output + "/measurements.txt", $"ADD RING gameplay: {startZoom:F3} -> {camera.orthographicSize:F3}; maximum frame step={maxStep:F4}; PASS\n");
            File.AppendAllText(Output + "/measurements.txt", "PASS: F1 switching B/M/F/B/F + restore; state ownership, mounts, authored views, Link pairs and all five damage kinds.\n");
        }
        finally { Time.timeScale = 1f; }
    }

    private static void Verify(OrbitalStationRuntime station, OrbitalStationRuntime.GrowthPreset preset)
    {
        int rings = preset == OrbitalStationRuntime.GrowthPreset.Beginning ? 1 : preset == OrbitalStationRuntime.GrowthPreset.Mid ? 4 : 8;
        int modules = rings == 1 ? 1 : rings * 2;
        Assert.That(station.ValidateState(out string error), Is.True, error);
        Assert.That(station.Rings.Count, Is.EqualTo(rings));
        Assert.That(station.Modules.Count, Is.EqualTo(modules));
        Assert.That(station.GetComponentsInChildren<OrbitalRingView>(true).Length, Is.EqualTo(rings));
        Assert.That(station.GetComponentsInChildren<OrbitalMountView>(true).Length, Is.EqualTo(rings * 3));
        Assert.That(station.GetComponentsInChildren<OrbitalModuleView>(true).Length, Is.EqualTo(modules));
        Assert.That(station.State.ResolveLinkPairs().Count(), Is.EqualTo(rings / 4));
        Assert.That(station.GetComponent<OrbitalStationView>().EffectsRoot.GetComponentsInChildren<LineRenderer>().Count(l => l.name == "Orbital Link"), Is.EqualTo(rings / 4));
        foreach (var state in station.State.Modules)
        {
            var module = station.Modules.Single(m => m.StableModuleId == state.StableModuleId);
            Assert.That(module.CurrentMount.Ring.RingId, Is.EqualTo(state.StableRingId));
            Assert.That(module.CurrentMount.MountIndex, Is.EqualTo(state.MountIndex));
            Assert.That(module.CurrentMount.Module, Is.SameAs(module));
        }
        foreach (var ring in station.Rings)
        {
            Assert.That(ring.Mounts.Count, Is.EqualTo(ring.State.MountCapacity));
            foreach (var mount in ring.Mounts)
                Assert.That(station.IsMountFree(mount), Is.EqualTo(mount.Module == null));
        }
        foreach (var view in station.GetComponentsInChildren<OrbitalModuleView>(true))
        {
            Assert.That(view.IsValid && view.gameObject.activeInHierarchy, Is.True);
            Assert.That(view.Sprites.Any(s => s.enabled && s.gameObject.activeInHierarchy), Is.True);
        }
    }

    private static void Measure(OrbitalStationRuntime station, Camera camera, OrbitalStationRuntime.GrowthPreset preset)
    {
        Vector2 min = new(float.PositiveInfinity, float.PositiveInfinity), max = new(float.NegativeInfinity, float.NegativeInfinity);
        void Point(Vector3 world)
        {
            Vector2 screen = camera.WorldToScreenPoint(world);
            min = Vector2.Min(min, screen); max = Vector2.Max(max, screen);
        }
        foreach (var renderer in station.GetComponent<OrbitalStationView>().RingsRoot.GetComponentsInChildren<Renderer>())
        {
            if (!renderer.enabled || !renderer.gameObject.activeInHierarchy) continue;
            Bounds b = renderer.bounds;
            Point(new Vector3(b.min.x, b.min.y, b.center.z));
            Point(new Vector3(b.max.x, b.max.y, b.center.z));
        }
        float diameter = station.Rings.Last().Radius * camera.pixelHeight / camera.orthographicSize;
        Rect safe = HUDManager.Instance.GetOrbitalSafePixelRect(camera);
        string outside = string.Join(",", station.Modules.Where(m => {
            Vector3 p = camera.WorldToViewportPoint(m.WorldPosition);
            return p.x < 0 || p.x > 1 || p.y < 0 || p.y > 1;
        }).Select(m => $"{m.StableModuleId}:{m.Kind}"));
        File.AppendAllText(Output + "/measurements.txt", $"{preset}: frame={Screen.width}x{Screen.height}; camera pixels={camera.pixelWidth}x{camera.pixelHeight}; ortho={camera.orthographicSize:F3}; pos={camera.transform.position}; ring diameter={diameter:F1}px; station bounds=({min.x:F1},{min.y:F1})..({max.x:F1},{max.y:F1}), size={max.x-min.x:F1}x{max.y-min.y:F1}px; outside modules=[{outside}]\n");
        float heightPercent = (max.y - min.y) / camera.pixelHeight * 100f;
        File.AppendAllText(Output + "/measurements.txt", $"viewport height={heightPercent:F2}%; safe pixels={safe}; presentation radius={station.PresentationRadius:F3}; HUD overlap={!safe.Contains(min) || !safe.Contains(max)}\n");
        Assert.That(safe.Contains(min) && safe.Contains(max), Is.True, "station must fit the HUD allowance");
        Assert.That(outside, Is.Empty);
        if (preset == OrbitalStationRuntime.GrowthPreset.Final)
            Assert.That(heightPercent, Is.InRange(55f, 65f));
    }

    private static void VerifyMountPresentation(OrbitalStationRuntime station)
    {
        var free = station.Rings.Last().Mounts.First(m => !m.Occupied);
        var view = free.Transform.GetComponent<OrbitalMountView>();
        free.SetVisualState(OrbitalMountRuntime.VisualState.Normal);
        float normalSize = view.Marker.transform.localScale.x;
        Assert.That(view.Marker.color.a, Is.LessThanOrEqualTo(.55f));
        Assert.That(view.Halo.enabled, Is.False);
        free.SetVisualState(OrbitalMountRuntime.VisualState.Hover);
        Assert.That(view.Marker.color.a, Is.GreaterThan(.9f));
        Assert.That(view.Marker.transform.localScale.x, Is.GreaterThan(normalSize));
        free.SetVisualState(OrbitalMountRuntime.VisualState.Valid);
        Assert.That(view.Marker.color.a, Is.GreaterThan(.9f));
        Assert.That(view.Halo.enabled, Is.True);
        free.SetVisualState(OrbitalMountRuntime.VisualState.Normal);
        var occupied = station.Rings.First().Mounts.First(m => m.Occupied);
        occupied.SetVisualState(OrbitalMountRuntime.VisualState.Occupied);
        Assert.That(occupied.Transform.GetComponent<OrbitalMountView>().Marker.enabled, Is.False);
    }

    private static void VerifyCombat(OrbitalStationRuntime station)
    {
        var targetObject = new GameObject("Growth QA damage receiver");
        var target = targetObject.AddComponent<EnemyHealth>();
        target.SetRuntimeMaxHealth(10000f);
        try
        {
            foreach (var kind in new[] { OrbitalModuleKind.Pistol, OrbitalModuleKind.LaserSword,
                OrbitalModuleKind.ImpulseGun, OrbitalModuleKind.ArcEmitter })
            {
                var module = station.Modules.First(m => m.Kind == kind);
                target.transform.position = module.WorldPosition + Vector2.right * .1f;
                float hp = target.CurrentHealth;
                module.Tick(10f);
                station.Combat.Tick(1f);
                Assert.That(target.CurrentHealth, Is.LessThan(hp), kind.ToString());
                File.AppendAllText(Output + "/measurements.txt", $"DAMAGE {kind}: {hp-target.CurrentHealth:F2}\n");
            }
            foreach (var pair in station.State.ResolveLinkPairs())
            {
                var a = station.Modules.Single(m => m.StableModuleId == pair.First);
                var b = station.Modules.Single(m => m.StableModuleId == pair.Second);
                a.Tick(10f);
                target.transform.position = (a.WorldPosition + b.WorldPosition) * .5f;
                float hp = target.CurrentHealth;
                Call(station, "UpdateLinkNodes", 1f);
                Assert.That(target.CurrentHealth, Is.LessThan(hp), $"Link {pair}");
                File.AppendAllText(Output + "/measurements.txt", $"DAMAGE Link {pair}: {hp-target.CurrentHealth:F2}\n");
            }
        }
        finally { Object.Destroy(targetObject); }
    }
}
#endif
