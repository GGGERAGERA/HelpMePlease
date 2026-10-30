#if UNITY_EDITOR
using System;
using System.Collections;
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

public sealed class EnemyOrbitalLabSandboxTests
{
    const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    EnemyOrbitalLabController lab;
    [SetUp] public void Preserve() => CoreTestSupport.PreservePreferences();
    [UnitySetUp] public IEnumerator Open()
    {
        SetGameView();
        EditorSceneManager.OpenScene(EnemyOrbitalLabController.ScenePath);
        yield return new EnterPlayMode();
        lab = Object.FindFirstObjectByType<EnemyOrbitalLabController>();
        yield return CoreTestSupport.Await(() => lab.Ready && lab.Station.Modules.Count >= 4);
        yield return null; yield return null;
    }
    [UnityTearDown] public IEnumerator Cleanup() => CoreTestSupport.CleanupPlayMode();
    object Call(string name, params object[] args)
    {
        var method = lab.GetType().GetMethod(name, Flags);
        Assert.That(method, Is.Not.Null, "Missing sandbox action: " + name);
        return method.Invoke(lab, args);
    }

    [UnityTest] public IEnumerator PanelReservesCombatAreaAndStartsReadyToAssemble()
    {
        Assert.That(lab.Paused, Is.True, "Sandbox must start ready for placement.");
        Assert.That(lab.AliveEnemies, Is.Zero);
        Assert.That(lab.Station.Modules.Count, Is.EqualTo(4));
        Assert.That(lab.FixedDirection, Is.True);
        Assert.That(lab.CombatV2.UsesBulletTime, Is.False);
        Assert.That(Screen.width, Is.EqualTo(1600)); Assert.That(Screen.height, Is.EqualTo(900));
        var panel = (Rect)lab.GetType().GetProperty("PanelRect", Flags).GetValue(lab);
        Assert.That(panel.width, Is.InRange(350f, 420f));
        Assert.That(lab.CameraRig.ControlledCamera.pixelRect.xMin, Is.GreaterThanOrEqualTo(panel.xMax));
        yield return null;
    }

    [UnityTest] public IEnumerator LayoutAndEditingKeepInstancesAndModulesAcrossRingChanges()
    {
        lab.SetPaused(true); lab.SelectWeaponModulePreset("C");
        var guns = lab.Station.Modules.ToArray();
        var ids = guns.Select(g => g.StableModuleId).ToArray();
        Call("ApplySandboxLayout", "GROUPED");
        CollectionAssert.AreEqual(ids, lab.Station.Modules.Select(g => g.StableModuleId));
        Assert.That(lab.WeaponModules.HasModule(ids[0], "RAPID"), Is.True);
        Assert.That(lab.Placement.TrySelect(ids[1]), Is.True);
        var ring = guns[0].CurrentMount.Ring;
        float radians = (ring.Phase + 359f) * Mathf.Deg2Rad;
        lab.Placement.PreviewWorld((Vector2)lab.Station.transform.position + new Vector2(Mathf.Cos(radians), Mathf.Sin(radians)) * ring.Radius);
        // The first grouped gun at 0° makes 359° invalid; free placement must still enforce its gap.
        Assert.That(lab.Placement.Confirm(), Is.False);
        Call("SelectSandboxWeapon", ids[1]);
        Assert.That((bool)Call("RemoveSandboxWeapon"), Is.True);
        Assert.That((bool)Call("InstallSandboxWeapon", 1), Is.True);
        Assert.That(lab.Station.Modules.Any(g => g.Kind == OrbitalModuleKind.Shotgun), Is.True);
        Call("SetSandboxRingSpeed", 73f);
        Call("SetSandboxRingDirection", -1);
        Assert.That(ring.RotationSpeed, Is.EqualTo(73f).Within(.001f));
        Assert.That(ring.State.Direction, Is.EqualTo(-1));
        while (lab.Station.Rings.Count > 1)
        { Call("SelectSandboxRing", lab.Station.Rings.Count - 1); Call("ChangeSandboxRings", false); }
        Assert.That((bool)Call("ChangeSandboxRings", false), Is.False);
        while (lab.Station.Rings.Count < 3) Assert.That((bool)Call("ChangeSandboxRings", true), Is.True);
        Assert.That((bool)Call("ChangeSandboxRings", true), Is.False);
        Assert.That(lab.WeaponModules.HasModule(ids[0], "RAPID"), Is.True);
        Assert.That(lab.Station.Modules.Any(g => g.StableModuleId == ids[0]), Is.True);

        // Restart creates a different Station. Explicit choices must survive, rewards must not.
        for (int i = 0; i < 3; i++)
        {
            lab.SelectSandboxRing(i);
            lab.SetSandboxRingSpeed(53f + 20f * i);
            lab.SetSandboxRingDirection(i == 1 ? 1 : -1);
        }
        var speeds = lab.Station.Rings.Select(r => r.State.BaseRotationSpeed).ToArray();
        var directions = lab.Station.Rings.Select(r => r.State.Direction).ToArray();
        Assert.That(lab.ApplyLabUpgrade(EnemyOrbitalLabController.LabUpgrade.OrbitSpeed), Is.True);
        // Compare choices by placement, not by IDs belonging to a particular Station.
        (int Ring, int Mount, OrbitalModuleKind Kind, float Phase, string[] Modules)[] WeaponComposition()
            => lab.Station.Modules.OrderBy(g => g.CurrentMount.Ring.State.Order)
                .ThenBy(g => g.CurrentMount.MountIndex)
                .Select(g => (g.CurrentMount.Ring.State.Order, g.CurrentMount.MountIndex, g.Kind,
                    g.CurrentMount.LocalPhase, Enumerable.Range(0, lab.WeaponModules.SlotCount(g.StableModuleId))
                        .Select(slot => lab.WeaponModules.GetModule(g.StableModuleId, slot)).ToArray())).ToArray();
        var expectedWeapons = WeaponComposition();
        var expectedOccupancy = lab.Station.Rings.OrderBy(r => r.State.Order)
            .Select(r => r.Mounts.Count(m => m.Occupied)).ToArray();
        var oldStation = lab.Station;
        lab.RestartScenario();
        yield return CoreTestSupport.Await(() => lab.Ready && lab.Station != oldStation);
        lab.SetPaused(true);
        var actualWeapons = WeaponComposition();
        var actualOccupancy = lab.Station.Rings.OrderBy(r => r.State.Order)
            .Select(r => r.Mounts.Count(m => m.Occupied)).ToArray();
        TestContext.WriteLine("Expected weapons: " + string.Join("; ", expectedWeapons.Select(w =>
            FormattableString.Invariant($"R{w.Ring}:M{w.Mount} {w.Kind} phase={w.Phase:R} modules=[{string.Join(",", w.Modules)}]"))));
        TestContext.WriteLine("Actual weapons: " + string.Join("; ", actualWeapons.Select(w =>
            FormattableString.Invariant($"R{w.Ring}:M{w.Mount} {w.Kind} phase={w.Phase:R} modules=[{string.Join(",", w.Modules)}]"))));
        TestContext.WriteLine("Expected/actual occupied mounts by ring: [" + string.Join(",", expectedOccupancy)
            + "] / [" + string.Join(",", actualOccupancy) + "]");
        CollectionAssert.AreEqual(speeds, lab.Station.Rings.Select(r => r.State.BaseRotationSpeed));
        CollectionAssert.AreEqual(directions, lab.Station.Rings.Select(r => r.State.Direction));
        Assert.That(lab.Station.Rings.All(r => r.State.SpeedUpgradeLevel == 0), Is.True);
        CollectionAssert.AreEqual(expectedOccupancy, actualOccupancy, "Restart must preserve intentionally empty rings.");
        Assert.That(actualWeapons.Length, Is.EqualTo(expectedWeapons.Length), "Restart weapon count.");
        for (int i = 0; i < expectedWeapons.Length; i++)
        {
            var expected = expectedWeapons[i]; var actual = actualWeapons[i];
            Assert.That(actual.Ring, Is.EqualTo(expected.Ring), "Weapon ring at index " + i);
            Assert.That(actual.Mount, Is.EqualTo(expected.Mount), "Weapon mount at index " + i);
            Assert.That(actual.Kind, Is.EqualTo(expected.Kind), "Weapon kind at index " + i);
            Assert.That(actual.Phase, Is.EqualTo(expected.Phase).Within(.001f), "Weapon local phase at index " + i);
            CollectionAssert.AreEqual(expected.Modules, actual.Modules, "Individual module slots at index " + i);
        }

        // Remove the inner ring: both survivors change Order. The replacement has no choices.
        var survivors = lab.Station.Rings.Skip(1).ToArray();
        lab.SelectSandboxRing(0);
        Assert.That(lab.ChangeSandboxRings(false), Is.True);
        Assert.That(lab.ChangeSandboxRings(true), Is.True);
        CollectionAssert.AreEqual(survivors, lab.Station.Rings.Take(2));
        var replacement = lab.Station.Rings.Last();
        float replacementSpeed = replacement.State.BaseRotationSpeed;
        int replacementDirection = replacement.State.Direction;
        // Editing after a reward must remember the compensated base, not bake in the reward.
        Assert.That(lab.ApplyLabUpgrade(EnemyOrbitalLabController.LabUpgrade.OrbitSpeed), Is.True);
        lab.SelectSandboxRing(0); lab.SetSandboxRingSpeed(80f);
        float compensatedSpeed = lab.Station.Rings[0].State.BaseRotationSpeed;
        Assert.That(lab.Station.Rings[0].RotationSpeed, Is.EqualTo(80f).Within(.001f));
        Assert.That(lab.Station.Modules.Count, Is.Zero, "Removing the armed inner ring leaves an empty build.");
        oldStation = lab.Station;
        lab.RestartScenario();
        yield return CoreTestSupport.Await(() => lab.Ready && lab.Station != oldStation);
        lab.SetPaused(true);
        CollectionAssert.AreEqual(new[] { compensatedSpeed, speeds[2], replacementSpeed },
            lab.Station.Rings.Select(r => r.State.BaseRotationSpeed));
        CollectionAssert.AreEqual(new[] { directions[1], directions[2], replacementDirection },
            lab.Station.Rings.Select(r => r.State.Direction));
        Assert.That(lab.Station.Rings.All(r => r.State.SpeedUpgradeLevel == 0), Is.True);
        Assert.That(lab.Station.Rings[0].RotationSpeed, Is.EqualTo(compensatedSpeed));
        Assert.That(lab.Station.Modules.Count, Is.Zero, "Repeated Restart must preserve an intentionally empty build.");
        CollectionAssert.AreEqual(new[] { 0, 0, 0 }, lab.Station.Rings.OrderBy(r => r.State.Order)
            .Select(r => r.Mounts.Count(m => m.Occupied)), "No empty ring may be filled by the previous general build.");
    }

    [UnityTest] public IEnumerator DenseScenarioIsBoundedRepeatableAndCanStopAndClear()
    {
        lab.ApplyTacticalPreset("EVEN");
        Call("StartSandboxEncounter", 4);
        Assert.That(lab.AliveEnemies, Is.EqualTo(48));
        Assert.That(lab.SandboxSpawning, Is.True);
        Assert.That(lab.TacticalPreset, Is.EqualTo("EVEN"));
        var initial = lab.Telemetry.Snapshot();
        Assert.That(initial.Build, Is.EqualTo("EVEN"));
        Assert.That(initial.ConfigurationChanged, Is.False);
        Call("SetSandboxSpawning", true);
        lab.Telemetry.Tick(.02f);
        Assert.That(lab.Telemetry.Snapshot().ConfigurationChanged, Is.False,
            "Assigning the current spawning policy must not mix conditions.");
        Call("SetSandboxSpawning", false);
        Assert.That(lab.Telemetry.Snapshot().ConfigurationChanged, Is.True,
            "Snapshot must detect a policy change without Tick.");
        Call("SetSandboxSpawning", true);
        Assert.That(lab.Telemetry.Snapshot().ConfigurationChanged, Is.True);
        Assert.That(lab.Telemetry.Snapshot().Build, Is.EqualTo("EVEN"));
        Assert.That(lab.TacticalPreset, Is.EqualTo("EVEN"));
        var first = lab.GetComponentsInChildren<EnemyHealth>().Select(e => (Vector2)e.transform.position).ToArray();
        Call("RepeatSandboxEncounter");
        var second = lab.GetComponentsInChildren<EnemyHealth>().Where(e => e.gameObject.activeSelf).Select(e => (Vector2)e.transform.position).ToArray();
        CollectionAssert.AreEqual(first, second);
        var tickReport = lab.Telemetry.Snapshot();
        Assert.That(tickReport.ConfigurationChanged, Is.False, "Repeat starts a clean recording.");
        Assert.That(tickReport.Build, Is.EqualTo("EVEN"));
        Call("SetSandboxSpawning", false);
        lab.Telemetry.Tick(.02f);
        // Inspect the existing report before Snapshot can detect the change itself.
        Assert.That(tickReport.ConfigurationChanged, Is.True, "Tick independently detects the change.");
        Assert.That(lab.TacticalPreset, Is.EqualTo("EVEN"));
        Call("SetSandboxSpawning", true);
        lab.Telemetry.Tick(.02f);
        Assert.That(tickReport.ConfigurationChanged, Is.True, "Restoring policy preserves history.");
        Assert.That(tickReport.Build, Is.EqualTo("EVEN"));

        Call("RepeatSandboxEncounter");
        Assert.That(lab.Telemetry.Snapshot().ConfigurationChanged, Is.False);
        lab.StopCombatTelemetry();
        Assert.That(lab.Telemetry.Recording, Is.False);
        string completed = lab.CombatTelemetryJson();
        Call("SetSandboxSpawning", false);
        lab.Telemetry.Tick(.02f);
        Assert.That(lab.CombatTelemetryJson(), Is.EqualTo(completed), "Stopped reports ignore policy changes.");
        Call("SetSandboxSpawning", true);
        Assert.That(lab.CombatTelemetryJson(), Is.EqualTo(completed));
        Call("SetSandboxSpawning", false);
        lab.SetPaused(false); lab.SetPlayerInvincible(true);
        yield return null; yield return null;
        Assert.That(lab.AliveEnemies, Is.LessThanOrEqualTo(64));
        Call("ClearSandboxArena");
        Assert.That(lab.AliveEnemies, Is.Zero);
        Call("RepeatSandboxEncounter");
        Assert.That(lab.AliveEnemies, Is.EqualTo(48));
        Assert.That(lab.Telemetry.Recording, Is.True);
        Assert.That(lab.Telemetry.Snapshot().ConfigurationChanged, Is.False);
        Assert.That(lab.Telemetry.Snapshot().Build, Is.EqualTo("EVEN"));
        Assert.That(lab.TacticalPreset, Is.EqualTo("EVEN"));
    }

    [UnityTest] public IEnumerator CaptureNormalAndDenseShiftAt1600x900()
    {
        string run = "capture-" + DateTime.UtcNow.ToString("yyyyMMddTHHmmssfff") + "-" + Guid.NewGuid().ToString("N");
        string directory = Path.GetFullPath(Path.Combine(Application.dataPath, "../Artifacts/GeneratedQA/OrbitalSandboxUI", run));
        Directory.CreateDirectory(directory);
        lab.SetPaused(true); lab.SelectWeaponModulePreset("A");
        var guns = lab.Station.Modules.ToArray();
        Assert.That(guns.Length, Is.EqualTo(4), "Capture requires four pistols from preset A.");
        Assert.That(guns.All(g => g.Kind == OrbitalModuleKind.Pistol), Is.True);
        var ids = guns.Select(g => g.StableModuleId).ToArray();
        Assert.That(ids.Distinct().Count(), Is.EqualTo(4));
        Assert.That(lab.WeaponModules.SetModule(ids[0], 0, "RAPID"), Is.True);
        Assert.That(lab.WeaponModules.SetModule(ids[1], 0, "MULTISHOT"), Is.True);
        Assert.That(lab.WeaponModules.SetModule(ids[2], 0, "RAPID"), Is.True);
        Assert.That(lab.WeaponModules.UnlockSecondSlot(ids[2]), Is.True);
        Assert.That(lab.WeaponModules.SetModule(ids[2], 1, "PIERCE"), Is.True);
        var expectedModules = new[] { "RAPID", "MULTISHOT", "RAPID + PIERCE", "EMPTY" };
        for (int i = 0; i < ids.Length; i++)
        {
            Assert.That(lab.WeaponModules.SlotCount(ids[i]), Is.EqualTo(i == 2 ? 2 : 1));
            string modules = string.Join(" + ", Enumerable.Range(0, lab.WeaponModules.SlotCount(ids[i]))
                .Select(slot => lab.WeaponModules.GetModule(ids[i], slot)));
            Assert.That(modules, Is.EqualTo(expectedModules[i]), "Prepared modules for #" + ids[i]);
        }
        Call("SelectSandboxWeapon", ids[2]);
        Call("SetSandboxTab", 3);
        yield return Capture(Path.Combine(run, "normal.png"));
        Call("SetSandboxTab", 0); Call("StartSandboxEncounter", 4); lab.SetPlayerInvincible(true); lab.SetPaused(false);
        // Batch Editor has no physical focus: exercise the same Shift owner explicitly.
        bool stationEnabled = lab.Station.enabled;
        try
        {
            lab.Station.enabled = false;
            lab.CombatV2.RouteShift(.016f, false, true);
            var tick = typeof(OrbitalStationRuntime).GetMethod("TickStation", Flags);
            for (int i = 0; i < 16; i++)
            {
                lab.CombatV2.RouteShift(.016f, true, true);
                tick.Invoke(lab.Station, new object[] { .016f });
                yield return null;
            }
            Assert.That(lab.CombatV2.IsSlowingOrbits, Is.True);
            Assert.That(Time.timeScale, Is.EqualTo(1f));
            yield return Capture(Path.Combine(run, "dense-shift.png"));
            lab.SetPaused(true);
            var report = lab.Telemetry.Snapshot();
            Assert.That(report.Weapons.Length, Is.EqualTo(4));
            CollectionAssert.AreEquivalent(ids, report.Weapons.Select(row => row.WeaponId));
            for (int i = 0; i < ids.Length; i++)
                Assert.That(report.Weapons.Single(row => row.WeaponId == ids[i]).Modules,
                    Is.EqualTo(expectedModules[i]), "Recorded modules for #" + ids[i]);
            var view = lab.GetType().GetField("sandboxView", Flags).GetValue(lab);
            // Existing scroll view: show the result and all four weapon rows together.
            typeof(EnemyOrbitalLabSandboxView).GetField("scroll", Flags).SetValue(view, new Vector2(0, 10000));
            yield return Capture(Path.Combine(run, "telemetry.png"));
            Assert.That(lab.Telemetry.Recording, Is.True, "Paused recording has not been stopped.");
            lab.StopCombatTelemetry();
            Assert.That(lab.Telemetry.Recording, Is.False);
            string stoppedReport = JsonUtility.ToJson(lab.Telemetry.Snapshot());
            lab.Telemetry.Tick(1f);
            Assert.That(JsonUtility.ToJson(lab.Telemetry.Snapshot()), Is.EqualTo(stoppedReport),
                "Stopped telemetry must remain fixed after a positive Tick.");
            yield return Capture(Path.Combine(run, "telemetry-stopped.png"));

            // Exercise the existing serializer only; physical clipboard interaction stays manual.
            bool pausedBeforeExport = lab.Paused;
            string exportedJson = lab.CombatTelemetryJson();
            var exportedReport = JsonUtility.FromJson<EnemyOrbitalLabTelemetry.Report>(exportedJson);
            Assert.That(exportedReport, Is.Not.Null);
            Assert.That(exportedReport.Weapons.Length, Is.EqualTo(4));
            CollectionAssert.AreEquivalent(ids, exportedReport.Weapons.Select(row => row.WeaponId));
            for (int i = 0; i < ids.Length; i++)
                Assert.That(exportedReport.Weapons.Single(row => row.WeaponId == ids[i]).Modules,
                    Is.EqualTo(expectedModules[i]), "Exported modules for #" + ids[i]);
            var fixedReport = JsonUtility.FromJson<EnemyOrbitalLabTelemetry.Report>(stoppedReport);
            foreach (string field in new[] { "DistanceTravelled", "MaximumCenterOffset", "OrbitSlowSeconds" })
                StringAssert.Contains("\"" + field + "\"", exportedJson);
            Assert.That(exportedReport.DistanceTravelled, Is.EqualTo(fixedReport.DistanceTravelled));
            Assert.That(exportedReport.MaximumCenterOffset, Is.EqualTo(fixedReport.MaximumCenterOffset));
            Assert.That(exportedReport.OrbitSlowSeconds, Is.EqualTo(fixedReport.OrbitSlowSeconds));
            Assert.That(lab.Telemetry.Recording, Is.False, "Export must not restart recording.");
            Assert.That(lab.Paused, Is.EqualTo(pausedBeforeExport), "Export must not change pause.");
            Assert.That(JsonUtility.ToJson(lab.Telemetry.Snapshot()), Is.EqualTo(stoppedReport),
                "Export must not change the fixed report.");
            // Place the export action in view instead of pinning the scroll to the last row.
            typeof(EnemyOrbitalLabSandboxView).GetField("scroll", Flags).SetValue(view, new Vector2(0, 320));
            yield return Capture(Path.Combine(run, "telemetry-json-export.png"));

            // A fresh encounter registers the original modules; editing must not relabel its history.
            lab.StartSandboxEncounter(4);
            lab.SetPaused(true);
            var initialReport = lab.Telemetry.Snapshot();
            Assert.That(lab.Telemetry.Recording, Is.True);
            Assert.That(initialReport.ConfigurationChanged, Is.False);
            Assert.That(initialReport.Weapons.Length, Is.EqualTo(4));
            string registeredModules = initialReport.Weapons.Single(row => row.WeaponId == ids[0]).Modules;
            Assert.That(registeredModules, Is.EqualTo("RAPID"));
            lab.SelectSandboxWeapon(ids[0]);
            lab.SetSandboxModule(0, "PIERCE");
            Assert.That(lab.Paused, Is.True);
            Assert.That(lab.WeaponModules.GetModule(ids[0], 0), Is.EqualTo("PIERCE"));
            var mixedReport = lab.Telemetry.Snapshot();
            Assert.That(lab.Telemetry.Recording, Is.True, "Editing must not stop or restart the measurement.");
            Assert.That(mixedReport.ConfigurationChanged, Is.True);
            Assert.That(mixedReport.Weapons.Length, Is.EqualTo(4));
            string historicalModules = mixedReport.Weapons.Single(row => row.WeaponId == ids[0]).Modules;
            Assert.That(historicalModules, Is.EqualTo(registeredModules));
            Assert.That(historicalModules, Is.Not.EqualTo(lab.WeaponModules.GetModule(ids[0], 0)));
            typeof(EnemyOrbitalLabSandboxView).GetField("scroll", Flags).SetValue(view, new Vector2(0, 10000));
            yield return Capture(Path.Combine(run, "telemetry-mixed-configuration.png"));
        }
        finally
        {
            lab.CombatV2.Suspend();
            lab.Station.enabled = stationEnabled;
        }
    }
    [UnityTest] public IEnumerator PausedRingsHaveFullVisibleRadiusWithoutAdvancingGunClocks()
    {
        Assert.That(lab.Paused, Is.True);
        foreach (var ring in lab.Station.Rings)
            Assert.That(Vector2.Distance(ring.Mounts[0].Transform.position, lab.Station.transform.position), Is.EqualTo(ring.Radius).Within(.001f));
        Call("SelectSandboxRing", 2); Call("ChangeSandboxRings", false);
        var first = lab.Station.Rings[0]; float phase = first.Phase;
        var cooldown = typeof(OrbitalModuleRuntime).GetProperty("RuntimeCooldown", Flags).GetValue(lab.Station.Modules[0]);
        Call("ChangeSandboxRings", true);
        var added = lab.Station.Rings.Last();
        Assert.That(Vector2.Distance(added.Mounts[0].Transform.position, lab.Station.transform.position), Is.EqualTo(added.Radius).Within(.001f));
        Assert.That(first.Phase, Is.EqualTo(phase));
        Assert.That(typeof(OrbitalModuleRuntime).GetProperty("RuntimeCooldown", Flags).GetValue(lab.Station.Modules[0]), Is.EqualTo(cooldown));
        yield return null;
    }
    [UnityTest] public IEnumerator RemoveInnerThenAddRingKeepsDistinctRadiiAndSurvivorPhase()
    {
        // Middle first reproduces the nonstandard radius order; inner also reindexes both survivors.
        foreach (int removedIndex in new[] { 1, 0 })
        {
            lab.SetPaused(true);
            var outer = lab.Station.Rings.Last(); float phase = outer.Phase;
            Call("SelectSandboxRing", removedIndex);
            Assert.That((bool)Call("ChangeSandboxRings", false), Is.True);
            Assert.That((bool)Call("ChangeSandboxRings", true), Is.True);
            var radii = lab.Station.Rings.Select(r => r.State.Radius).ToArray();
            Assert.That(radii.Distinct().Count(), Is.EqualTo(3));
            Assert.That(outer.Phase, Is.EqualTo(phase));

            var oldStation = lab.Station;
            lab.RestartScenario();
            yield return CoreTestSupport.Await(() => lab.Ready && lab.Station != oldStation);
            lab.SetPaused(true);
            var restoredRadii = lab.Station.Rings.Select(r => r.State.Radius).ToArray();
            TestContext.WriteLine("Removed ring index " + removedIndex + ": radii before restart=[" +
                string.Join(", ", radii) + "], after=[" + string.Join(", ", restoredRadii) + "]");
            CollectionAssert.AreEqual(radii, restoredRadii,
                "Restart must preserve the exact ordered physical radii after removing ring index " + removedIndex);
        }
    }
    [UnityTest] public IEnumerator RemovingUnrelatedRingPreservesRememberedCustomLayout()
    {
        var ring = lab.Station.Rings[0]; var gun = ring.Mounts[0].Module;
        Assert.That(lab.Placement.TrySelect(gun.StableModuleId), Is.True);
        float radians = (ring.Phase + 7.3f) * Mathf.Deg2Rad;
        lab.Placement.PreviewWorld((Vector2)lab.Station.transform.position + new Vector2(Mathf.Cos(radians), Mathf.Sin(radians)) * ring.Radius);
        Assert.That(lab.Placement.Confirm(), Is.True);
        Call("SelectSandboxRing", 0); Call("ApplySandboxLayout", "EVEN");
        Call("SelectSandboxRing", 2); Call("ChangeSandboxRings", false);
        Call("SelectSandboxRing", 0); Call("ApplySandboxLayout", "CUSTOM");
        Assert.That(gun.CurrentMount.LocalPhase, Is.EqualTo(7.3f).Within(.001f));
        yield return null;
    }
    IEnumerator Capture(string name)
    {
        string path = Path.GetFullPath(Path.Combine(Application.dataPath, "../Artifacts/GeneratedQA/OrbitalSandboxUI", name));
        Assert.That(File.Exists(path), Is.False, "Capture must preserve previous evidence.");
        yield return null; yield return null;
        ScreenCapture.CaptureScreenshot(path);
        yield return CoreTestSupport.Await(() => File.Exists(path));
        var image = new Texture2D(2, 2); image.LoadImage(File.ReadAllBytes(path));
        Assert.That(image.width, Is.EqualTo(1600)); Assert.That(image.height, Is.EqualTo(900));
        Object.Destroy(image);
    }
    public static void OpenForManualTesting()
    {
        EditorSceneManager.OpenScene(EnemyOrbitalLabController.ScenePath); SetGameView();
        EditorApplication.delayCall += () => { if (!EditorApplication.isPlaying) EditorApplication.EnterPlaymode(); };
    }
    static void SetGameView()
    {
        var assembly = typeof(Editor).Assembly;
        var type = assembly.GetType("UnityEditor.GameViewSizes");
        var sizes = typeof(ScriptableSingleton<>).MakeGenericType(type).GetProperty("instance").GetValue(null);
        var group = type.GetMethod("GetGroup").Invoke(sizes, new object[] { 0 });
        var gt = group.GetType(); int count = (int)gt.GetMethod("GetTotalCount").Invoke(group, null), index = -1;
        for (int i = 0; i < count; i++)
        {
            var size = gt.GetMethod("GetGameViewSize").Invoke(group, new object[] { i }); var st = size.GetType();
            if ((int)st.GetProperty("width").GetValue(size) == 1600 && (int)st.GetProperty("height").GetValue(size) == 900) { index = i; break; }
        }
        if (index < 0)
        {
            var mode = Enum.ToObject(assembly.GetType("UnityEditor.GameViewSizeType"), 1);
            var size = Activator.CreateInstance(assembly.GetType("UnityEditor.GameViewSize"), new object[] { mode, 1600, 900, "Orbital sandbox 1600×900" });
            gt.GetMethod("AddCustomSize").Invoke(group, new[] { size }); index = count;
        }
        var view = EditorWindow.GetWindow(assembly.GetType("UnityEditor.GameView"));
        view.GetType().GetProperty("selectedSizeIndex").SetValue(view, index); view.Show();
    }
    string segmentedOutput;
    [UnityTest]
    public IEnumerator NeighborAndSkipLinksRespectTheEmptySlot()
    {
        yield return PrepareSegmentedBuild();
        var b = lab.Segmented;
        b.SetMotion(SegmentedOrbitConfiguration.Motion.Manual);
        Assert.That(b.LoadPreset("Шашечка"),Is.True);
        var skip = b.FindConnection(11,1);
        Assert.That(skip,Is.Not.Null,"12 → 1 → 2 must work across the seam.");
        Assert.That(skip.Middle,Is.Zero);
        Assert.That(skip.Type,Is.EqualTo(SegmentedOrbitConfiguration.Geometry.Skip));
        Assert.That(b.Active(skip.Id),Is.True);
        Assert.That(b.Strength(skip),Is.EqualTo(.65f));
        Assert.That(b.FindConnection(0,12),Is.Null,"The current experiment contains no cross-layer pairs.");
        var pistol = (EnemyOrbitalLabWeapon)b.Gun(1);
        Assert.That(b.DamageFactor(pistol.StableModuleId),Is.EqualTo(1.13f).Within(.0001f));
        Assert.That(b.RateFactor(pistol.StableModuleId),Is.EqualTo(1.13f).Within(.0001f));
        lab.Station.enabled = false;
        var adapter = (ProductionOrbitalCombatAdapter)lab.Station.Combat;
        adapter.DebugClearProjectiles(); SetCooldown(pistol,0); lab.SetPaused(false); pistol.Tick(.001f); lab.SetPaused(true);
        Assert.That(Cooldown(pistol),Is.EqualTo(pistol.Settings.FireInterval/1.13f).Within(.001f));
        var shot = ((IEnumerable)typeof(ProductionOrbitalCombatAdapter).GetField("projectiles",Flags).GetValue(adapter)).Cast<object>()
            .First(p => (bool)p.GetType().GetField("Active").GetValue(p));
        Assert.That((float)shot.GetType().GetField("Damage").GetValue(shot),
            Is.EqualTo(pistol.Settings.Damage * lab.Station.GetModuleMetaDamageMultiplier(pistol.Kind) * 1.13f).Within(.001f));
        b.Select(skip.Id,true);
        yield return CaptureSegmented("skip-65.png");
        // A bare segment already occupies the middle slot: it must disable a retained Skip module.
        b.RemoveSegment(22); Assert.That(b.AddSegment(0),Is.True);
        Assert.That(b.Active(skip.Id),Is.False);
        Assert.That(skip.Module,Is.EqualTo(SegmentedOrbitConfiguration.Link.Damage));
        Assert.That(b.Status(skip.Id),Does.Contain("промежуточный"));
        yield return CaptureSegmented("skip-blocked.png");
        b.Select(9,false); yield return CaptureSegmented("skip-inactive.png");
        b.RemoveSegment(0); Assert.That(b.Active(skip.Id),Is.True);
        // Occupy the middle with a real gun, then compare a full Near effect.
        b.RemoveWeapon(7); b.AddSegment(0); Assert.That(b.InstallWeapon(0,0),Is.True);
        Assert.That(b.Active(skip.Id),Is.False);
        Assert.That(b.Connect(11,0,SegmentedOrbitConfiguration.Link.Damage),Is.True);
        var near = b.FindConnection(11,0);
        Assert.That(near.Type,Is.EqualTo(SegmentedOrbitConfiguration.Geometry.Near));
        Assert.That(b.Strength(near),Is.EqualTo(1));
        Assert.That(b.DamageFactor(b.Gun(11).StableModuleId),Is.EqualTo(1.2f).Within(.0001f));
        Assert.That(b.Connect(0,11,SegmentedOrbitConfiguration.Link.ExtraShot),Is.False,"One module per unordered pair.");
        Assert.That(b.Connect(11,1,SegmentedOrbitConfiguration.Link.FireRate),Is.False,"A gun in the middle blocks Skip.");
        b.Select(near.Id,true); yield return CaptureSegmented("near-100.png");
        Assert.That(Enumerable.Range(0,36).All(i => b.ActiveLinkCount(i) <= 2),Is.True);
        b.SetLink(skip.Id,SegmentedOrbitConfiguration.Link.None);
        Assert.That(b.Connect(9,11,SegmentedOrbitConfiguration.Link.Damage),Is.True);
        Assert.That(b.DamageFactor(b.Gun(11).StableModuleId),Is.EqualTo(1.33f).Within(.0001f),"Near 20% + Skip 13% remain additive.");
        b.LoadPreset("Шашечка");
        var extra = b.FindConnection(3,5);
        Assert.That(b.SetLink(b.FindConnection(5,7).Id,SegmentedOrbitConfiguration.Link.ExtraShot),Is.True);
        var a = (EnemyOrbitalLabWeapon)b.Gun(3); var other = (EnemyOrbitalLabWeapon)b.Gun(5); var third = (EnemyOrbitalLabWeapon)b.Gun(7);
        int beforeOther = other.ShotsFired, beforeThird = third.ShotsFired;
        SetCooldown(a,0); SetCooldown(other,.31f); lab.SetPaused(false); a.Tick(.001f);
        Assert.That(other.ShotsFired,Is.EqualTo(beforeOther+1));
        Assert.That(third.ShotsFired,Is.EqualTo(beforeThird),"Extra shots must not propagate along another Skip link.");
        Assert.That(Cooldown(other),Is.EqualTo(.31f));
        float until = (float)extra.GetType().GetField("NextExtraShot",Flags).GetValue(extra);
        Assert.That(until-Time.time,Is.EqualTo(.5f/.65f).Within(.001f),"Skip uses the actual longer cooldown.");
        SetCooldown(a,0); a.Tick(.001f); Assert.That(other.ShotsFired,Is.EqualTo(beforeOther+1));
        lab.SetPaused(true);
        lab.SegmentedSkipStrength = .5f;
        Assert.That(b.DamageFactor(b.Gun(1).StableModuleId),Is.EqualTo(1.1f).Within(.0001f));
        Assert.That(b.RateFactor(b.Gun(1).StableModuleId),Is.EqualTo(1.1f).Within(.0001f));
        Assert.That(b.ExtraShotInterval(extra),Is.EqualTo(1f));
        lab.SegmentedSkipStrength = .65f;
        b.LoadPreset("Шашечка");
        Assert.That(b.Ricochets(b.Gun(5).StableModuleId),Is.Zero,"Shotgun does not support Skip ricochet.");
        Assert.That(b.Ricochets(b.Gun(7).StableModuleId),Is.EqualTo(1),"Pistol retains Skip ricochet.");
        skip = b.FindConnection(11,1); b.SetLink(skip.Id,SegmentedOrbitConfiguration.Link.None);
        b.Select(11,false); b.BeginConnect(SegmentedOrbitConfiguration.Link.Damage);
        Assert.That(b.IsConnectionTarget(1),Is.True);
        Assert.That(b.IsConnectionTarget(9),Is.True);
        yield return CaptureSegmented("skip-targets.png");
        var view = lab.GetType().GetField("segmentedView",Flags).GetValue(lab);
        Assert.That((bool)view.GetType().GetMethod("SelectWeaponAt",Flags).Invoke(view,new object[] { b.Gun(1).WorldPosition }),Is.True);
        Assert.That(b.SelectedLink && b.Selected == skip.Id && b.Active(skip.Id),Is.True);
        yield return CaptureSegmented("skip-created.png");
        lab.Station.enabled = true; lab.StartSandboxEncounter(4); lab.StartSegmentedCombat();
        yield return new WaitForSecondsRealtime(.45f); yield return CaptureSegmented("skip-combat-a.png");
        yield return new WaitForSecondsRealtime(.25f); yield return CaptureSegmented("skip-combat-b.png");
        Assert.That(lab.Station.Modules.OfType<EnemyOrbitalLabWeapon>().Sum(g => g.ShotsFired),Is.GreaterThan(0));
        lab.SetPaused(true); b.RemoveSegment(22); b.AddSegment(0); b.Select(9,false); lab.SetPaused(false);
        yield return CaptureSegmented("skip-inactive-combat.png");
        lab.SetPaused(true);
    }
    IEnumerator PrepareSegmentedBuild()
    {
        var previous = lab.Station;
        lab.SetSegmentedOrbit(true);
        float deadline = Time.realtimeSinceStartup + 8f;
        while ((!lab.Ready || lab.Station == previous || !lab.Segmented.Bound) && Time.realtimeSinceStartup < deadline) yield return null;
        Assert.That(lab.Ready && lab.Segmented.Bound, Is.True, "Segmented build must initialize.");
        lab.SetPaused(true);
        segmentedOutput = Path.GetFullPath("Artifacts/GeneratedQA/SegmentedOrbit/run-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmssfff"));
        Directory.CreateDirectory(segmentedOutput);
        Debug.Log($"[Segmented QA] project={Path.GetFullPath(".")} scene={EnemyOrbitalLabController.ScenePath} editor={Application.unityVersion} PID={System.Diagnostics.Process.GetCurrentProcess().Id}");
    }
    [Test]
    public void TwelveMountOptInIsLocalAndNotSerialized()
    {
        var state = OrbitalRunState.CreateDefault(1);
        state.LabMountLimit = 12;
        var ring = state.Rings[0];
        while (ring.MountCount < 12)
        {
            if (ring.MountCount == ring.MountCapacity) Assert.That(state.TryIncreaseCapacity(ring.StableRingId,out _),Is.True);
            Assert.That(state.TryAddMount(ring.StableRingId,out _),Is.True);
        }
        Assert.That(state.Validate(out _),Is.True);
        Assert.That(JsonUtility.ToJson(state),Does.Not.Contain("LabMountLimit"));
        var normal = OrbitalRunState.CreateDefault(2);
        Assert.That(normal.LabMountLimit,Is.Zero);
        Assert.That(OrbitalProgressionConfig.Default.MaxMountsPerRing,Is.EqualTo(6));
    }
    [Test]
    public void LabExposesAnExplicitSegmentedModeWithoutReplacingFreePlacement()
    {
        Assert.That(typeof(EnemyOrbitalLabController).GetMethod("SetSegmentedOrbit"), Is.Not.Null,
            "EnemyOrbitalLab must expose a separate Segmented Orbit mode.");
        Assert.That(typeof(EnemyOrbitalLabController).GetMethod("MoveSandboxWeapon"), Is.Not.Null);
    }
    [UnityTest]
    public IEnumerator GeometryLinksPresetsAndModeRoundTrip()
    {
        yield return PrepareSegmentedBuild();
        var b = lab.Segmented;
        Assert.That(lab.Station.Rings.Count,Is.EqualTo(3));
        Assert.That(lab.Station.Rings.All(r => r.MountCount == 12),Is.True);
        Assert.That(lab.Station.ValidateState(out string error),Is.True,error);
        foreach (string preset in SegmentedOrbitConfiguration.Presets)
        {
            Assert.That(b.LoadPreset(preset),Is.True);
            Assert.That(b.SegmentCount,Is.EqualTo(12));
            Assert.That(b.Cells.Count(c => c.Weapon == 0),Is.EqualTo(4));
            Assert.That(b.Cells.Count(c => c.Weapon == 1),Is.EqualTo(1));
            Assert.That(b.Cells.Count(c => c.Weapon == 2),Is.EqualTo(1));
            Assert.That(b.Links.Where(c => c.Module != SegmentedOrbitConfiguration.Link.None)
                .All(c => b.Active(c.Id)),Is.True);
        }
        b.LoadPreset("Full Circle");
        Assert.That(SegmentedOrbitConfiguration.Next(11),Is.Zero);
        Assert.That(SegmentedOrbitConfiguration.Next(23),Is.EqualTo(12));
        Assert.That(SegmentedOrbitConfiguration.Previous(24),Is.EqualTo(35));
        Assert.That(b.Active(11),Is.True);
        Assert.That(b.SetLink(11,SegmentedOrbitConfiguration.Link.Damage),Is.True);
        Assert.That(b.SetLink(0,SegmentedOrbitConfiguration.Link.Damage),Is.True);
        var gun = (EnemyOrbitalLabWeapon)b.Gun(0);
        Assert.That(b.DamageFactor(gun.StableModuleId),Is.EqualTo(1.4f).Within(.0001f));
        float baseDamage = gun.SourceProfile.Damage, baseInterval = gun.SourceProfile.FireInterval;
        lab.Station.enabled = false;
        var adapter = (ProductionOrbitalCombatAdapter)lab.Station.Combat;
        adapter.DebugClearProjectiles(); SetCooldown(gun,0); lab.SetPaused(false); gun.Tick(.001f); lab.SetPaused(true);
        var shot = ((IEnumerable)typeof(ProductionOrbitalCombatAdapter).GetField("projectiles",Flags).GetValue(adapter)).Cast<object>()
            .First(p => (bool)p.GetType().GetField("Active").GetValue(p));
        float damage = (float)shot.GetType().GetField("Damage").GetValue(shot);
        Assert.That(damage,Is.EqualTo(baseDamage * lab.Station.GetModuleMetaDamageMultiplier(gun.Kind) * 1.4f).Within(.001f),"The emitted projectile must receive additive link damage.");
        b.SetLink(4,SegmentedOrbitConfiguration.Link.None);
        Assert.That(b.SetLink(11,SegmentedOrbitConfiguration.Link.FireRate),Is.True);
        Assert.That(b.SetLink(0,SegmentedOrbitConfiguration.Link.FireRate),Is.True);
        Assert.That(b.RateFactor(gun.StableModuleId),Is.EqualTo(1.4f).Within(.0001f));
        SetCooldown(gun,0); lab.SetPaused(false); gun.Tick(.001f); lab.SetPaused(true);
        Assert.That(Cooldown(gun),Is.EqualTo(baseInterval / 1.4f).Within(.001f),"The real firing clock uses the combined rate bonus.");
        adapter.DebugClearProjectiles(); lab.Station.enabled = true;
        Assert.That(b.DamageFactor(gun.StableModuleId),Is.EqualTo(1));
        Assert.That(gun.SourceProfile.Damage,Is.EqualTo(baseDamage));
        Assert.That(gun.SourceProfile.FireInterval,Is.EqualTo(baseInterval));
        Assert.That(b.LinkRemaining(SegmentedOrbitConfiguration.Link.Damage),Is.EqualTo(2));
        b.RemoveWeapon(11);
        Assert.That(b.Active(11),Is.False);
        Assert.That(b.Links[11].Module,Is.EqualTo(SegmentedOrbitConfiguration.Link.FireRate));
        Assert.That(b.Status(11),Does.Contain("нужны две"));
        Assert.That(b.InstallWeapon(11,2),Is.True);
        Assert.That(b.Active(11),Is.True);
        // Independent levels, empty-position break and bounded inventory.
        foreach (int index in new[] { 2,14,26 })
        {
            if (b.Cells[index].Segment) b.RemoveSegment(index);
            else b.RemoveSegment(2);
            Assert.That(b.AddSegment(index),Is.True);
            Assert.That(b.RemoveSegment(index),Is.True);
        }
        b.LoadPreset("Full Circle");
        gun = (EnemyOrbitalLabWeapon)b.Gun(0);
        Assert.That(lab.WeaponModules.SetModule(gun.StableModuleId,0,"RAPID"),Is.True);
        b.RemoveSegment(2); b.AddSegment(24);
        float cooldown = Cooldown(gun);
        b.Select(0,false); b.BeginMove();
        Assert.That(b.MoveWeapon(24),Is.True);
        Assert.That(b.Gun(24),Is.SameAs(gun));
        Assert.That(Cooldown(gun),Is.EqualTo(cooldown));
        Assert.That(lab.WeaponModules.GetModule(gun.StableModuleId,0),Is.EqualTo("RAPID"));
        Assert.That(b.Links[0].Module,Is.EqualTo(SegmentedOrbitConfiguration.Link.Damage));
        Assert.That(b.Active(0),Is.False);
        Assert.That(b.SetLink(1,SegmentedOrbitConfiguration.Link.Damage),Is.False,"No gun at position 3.");
        lab.Station.enabled = false;
        b.SetMotion(SegmentedOrbitConfiguration.Motion.Fixed);
        var phases = lab.Station.Rings.Select(r => r.Phase).ToArray();
        lab.CombatV2.RouteShift(.1f,false,true);
        foreach (var ring in lab.Station.Rings) ring.Tick(.1f);
        CollectionAssert.AreEqual(phases,lab.Station.Rings.Select(r => r.Phase));
        b.SetMotion(SegmentedOrbitConfiguration.Motion.Rotating);
        foreach (var ring in lab.Station.Rings) ring.Tick(.1f);
        Assert.That(lab.Station.Rings[0].Phase,Is.Not.EqualTo(phases[0]));
        yield return CaptureSegmented("constructor-moved.png");
        lab.Station.enabled = true;
        lab.WeaponModules.UnlockSecondSlot(b.Gun(1).StableModuleId);
        var old = lab.Station; lab.SetSegmentedOrbit(false);
        yield return CoreTestSupport.Await(() => lab.Ready && lab.Station != old);
        Assert.That(lab.Station.Modules.Count,Is.EqualTo(4));
        Assert.That(lab.Station.State.LabMountLimit,Is.Zero);
        Assert.That(lab.CombatV2.UsesBulletTime,Is.False);
        Assert.That(lab.Station.Modules.All(g => lab.WeaponModules.SlotCount(g.StableModuleId) == 1),Is.True,"Segmented slots must not leak into the free build.");
        old = lab.Station; lab.SetSegmentedOrbit(true);
        yield return CoreTestSupport.Await(() => lab.Ready && lab.Station != old && b.Bound);
        Assert.That(b.Gun(24),Is.TypeOf<EnemyOrbitalLabWeapon>());
        Assert.That(lab.WeaponModules.GetModule(b.Gun(24).StableModuleId,0),Is.EqualTo("RAPID"));
        Assert.That(b.Cells[0].Weapon,Is.EqualTo(-1));
        Assert.That(lab.Station.ValidateState(out error),Is.True,error);
    }
    [UnityTest]
    public IEnumerator ExtraShotDoesNotRecurseRicochetHitsAndDenseCombatRenders()
    {
        yield return PrepareSegmentedBuild();
        var b = lab.Segmented;
        lab.Station.enabled = false;
        b.SetLink(10,SegmentedOrbitConfiguration.Link.None);
        Assert.That(b.SetLink(0,SegmentedOrbitConfiguration.Link.ExtraShot),Is.True);
        Assert.That(b.SetLink(11,SegmentedOrbitConfiguration.Link.ExtraShot),Is.True);
        var a = (EnemyOrbitalLabWeapon)b.Gun(0); var neighbour = (EnemyOrbitalLabWeapon)b.Gun(1); var wrap = (EnemyOrbitalLabWeapon)b.Gun(11);
        int n = neighbour.ShotsFired, w = wrap.ShotsFired;
        SetCooldown(neighbour,.37f); SetCooldown(wrap,.43f); SetCooldown(a,0f);
        lab.SetPaused(false); a.Tick(.001f);
        Assert.That(a.ShotsFired,Is.EqualTo(1));
        Assert.That(neighbour.ShotsFired,Is.EqualTo(n+1)); Assert.That(wrap.ShotsFired,Is.EqualTo(w+1));
        Assert.That(Cooldown(neighbour),Is.EqualTo(.37f)); Assert.That(Cooldown(wrap),Is.EqualTo(.43f));
        SetCooldown(a,0f); a.Tick(.001f);
        Assert.That(neighbour.ShotsFired,Is.EqualTo(n+1),"Each edge has an independent 0.5 s cooldown.");
        Assert.That(wrap.ShotsFired,Is.EqualTo(w+1));
        var adapter = (ProductionOrbitalCombatAdapter)lab.Station.Combat;
        adapter.DebugClearProjectiles();
        var one = Target(new Vector2(13,10)); var two = Target(new Vector2(13,12));
        Physics2D.SyncTransforms();
        adapter.SpawnLabDirectionalProjectile(new Vector2(11,10),Vector2.right,lab.Pistol,null,2,0,1);
        for (int i = 0; i < 50; i++) adapter.Tick(.02f);
        Assert.That(one.CurrentHealth,Is.EqualTo(98)); Assert.That(two.CurrentHealth,Is.EqualTo(98));
        Assert.That(adapter.LabActiveProjectileCount,Is.Zero);
        UnityEngine.Object.Destroy(one.gameObject); UnityEngine.Object.Destroy(two.gameObject);
        lab.SetPaused(true); b.LoadPreset("Multi-Layer"); b.Select(11,true);
        yield return CaptureSegmented("constructor.png");
        lab.Station.enabled = true; lab.StartSandboxEncounter(4);
        Assert.That(lab.AliveEnemies,Is.EqualTo(48));
        Assert.That(lab.GetComponentsInChildren<EnemyDirectionalShield>().Length,Is.EqualTo(18),"Dense composition must contain both existing shield types.");
        lab.StartSegmentedCombat();
        yield return new WaitForSecondsRealtime(.65f);
        yield return CaptureSegmented("combat-a.png");
        yield return new WaitForSecondsRealtime(.35f);
        yield return CaptureSegmented("combat-b.png");
        Assert.That(lab.Station.Modules.OfType<EnemyOrbitalLabWeapon>().Sum(g => g.ShotsFired),Is.GreaterThan(0));
        // Exercise the real Shift owner and time owner; physical held-key input remains a manual check.
        lab.Station.enabled = false; lab.CombatV2.RouteShift(.02f,false,true);
        Assert.That(lab.CombatV2.RouteShift(.02f,true,true),Is.True);
        for (int i = 0; i < 12; i++)
        {
            typeof(OrbitalStationRuntime).GetMethod("TickRightMouse",Flags).Invoke(lab.Station,new object[] { .02f,true,false });
            TickStation(.02f); yield return null;
        }
        Assert.That(lab.Station.BulletTime.IsActive,Is.True);
        Assert.That(Time.timeScale,Is.LessThan(1));
        yield return CaptureSegmented("bullet-time.png");
        lab.CombatV2.Suspend(); lab.Station.enabled = true; lab.SetPaused(true);
        Assert.That(Time.timeScale,Is.Zero); Assert.That(lab.Station.BulletTime.IsActive,Is.False);
        lab.StartSegmentedCombat(); yield return new WaitForSecondsRealtime(.2f);
        Assert.That(lab.Paused,Is.False); Assert.That(lab.AliveEnemies,Is.LessThanOrEqualTo(64));
        lab.SetPaused(true);
    }
    static EnemyHealth Target(Vector2 position)
    {
        var go = new GameObject("Ricochet fixture"); go.transform.position = position;
        go.AddComponent<CircleCollider2D>().radius = .25f;
        var hp = go.AddComponent<EnemyHealth>(); hp.SetRuntimeMaxHealth(100);
        var dummy = go.AddComponent<LabEnemyDummy>(); dummy.Initialize(); dummy.Invulnerable = false;
        return hp;
    }
    [UnityTest]
    public IEnumerator ManualRotationStopsAtZeroAndKeepsItsFiringClocks()
    {
        yield return PrepareSegmentedBuild();
        var b = lab.Segmented; b.SetMotion(SegmentedOrbitConfiguration.Motion.Manual); b.LoadPreset("Шашечка");
        lab.Station.enabled = false;
        var guns = lab.Station.Modules.OfType<EnemyOrbitalLabWeapon>().ToArray(); var clocks = guns.Select(Cooldown).ToArray();
        float phase = lab.Station.Rings[0].Phase;
        lab.SetPaused(false); b.TickManualRotation(1,true,false,true);
        Assert.That(b.RotationEnergy,Is.EqualTo(75).Within(.001f));
        Assert.That(lab.Station.Rings.All(r => Mathf.Abs(Mathf.DeltaAngle(phase+90,r.Phase)) < .001f),Is.True);
        CollectionAssert.AreEqual(clocks,guns.Select(Cooldown));
        b.TickManualRotation(10,true,false,true); Assert.That(b.RotationEnergy,Is.Zero);
        phase = lab.Station.Rings[0].Phase; b.TickManualRotation(1,true,false,true);
        Assert.That(lab.Station.Rings.All(r => Mathf.Approximately(r.Phase,phase)),Is.True);
        TickStation(.6f); Assert.That(guns.Sum(g => g.ShotsFired),Is.GreaterThan(0));
        b.TickManualRotation(.5f,false,false,true); Assert.That(b.RotationEnergy,Is.EqualTo(6).Within(.001f));
        lab.Station.enabled = true; lab.SetPaused(true);
        yield return null;
    }
    static float Cooldown(OrbitalModuleRuntime gun) => (float)typeof(OrbitalModuleRuntime).GetProperty("RuntimeCooldown",Flags).GetValue(gun);
    static void SetCooldown(OrbitalModuleRuntime gun,float value) => typeof(OrbitalModuleRuntime).GetProperty("RuntimeCooldown",Flags).SetValue(gun,value);
    void TickStation(float delta) => typeof(OrbitalStationRuntime).GetMethod("TickStation",Flags).Invoke(lab.Station,new object[] { delta });
    IEnumerator CaptureSegmented(string name)
    {
        yield return null; yield return null;
        string path = Path.Combine(segmentedOutput,name);
        ScreenCapture.CaptureScreenshot(path);
        yield return CoreTestSupport.Await(() => File.Exists(path));
        TestContext.WriteLine($"Game View: {path} · screen={Screen.width}x{Screen.height} · motion={lab.Segmented.Movement} · energy={lab.Segmented.RotationEnergy}");
    }
}
#endif
