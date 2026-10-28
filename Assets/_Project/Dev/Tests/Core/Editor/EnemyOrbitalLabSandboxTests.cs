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
}
#endif


