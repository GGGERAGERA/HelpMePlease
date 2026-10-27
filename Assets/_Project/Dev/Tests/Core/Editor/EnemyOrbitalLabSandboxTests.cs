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
        yield return null;
    }

    [UnityTest] public IEnumerator DenseScenarioIsBoundedRepeatableAndCanStopAndClear()
    {
        Call("StartSandboxEncounter", 4);
        Assert.That(lab.AliveEnemies, Is.EqualTo(48));
        var first = lab.GetComponentsInChildren<EnemyHealth>().Select(e => (Vector2)e.transform.position).ToArray();
        Call("RepeatSandboxEncounter");
        var second = lab.GetComponentsInChildren<EnemyHealth>().Where(e => e.gameObject.activeSelf).Select(e => (Vector2)e.transform.position).ToArray();
        CollectionAssert.AreEqual(first, second);
        Call("SetSandboxSpawning", false);
        lab.SetPaused(false); lab.SetPlayerInvincible(true);
        yield return null; yield return null;
        Assert.That(lab.AliveEnemies, Is.LessThanOrEqualTo(64));
        Call("ClearSandboxArena");
        Assert.That(lab.AliveEnemies, Is.Zero);
        Call("RepeatSandboxEncounter");
        Assert.That(lab.AliveEnemies, Is.EqualTo(48));
    }

    [UnityTest] public IEnumerator CaptureNormalAndDenseShiftAt1600x900()
    {
        lab.SetPaused(true); lab.SelectWeaponModulePreset("C");
        Call("SelectSandboxWeapon", lab.Station.Modules[0].StableModuleId);
        lab.WeaponModules.UnlockSecondSlot(lab.Station.Modules[0].StableModuleId);
        lab.WeaponModules.SetModule(lab.Station.Modules[0].StableModuleId, 1, "PIERCE");
        Call("SetSandboxTab", 3);
        yield return Capture("normal.png");
        Call("SetSandboxTab", 0); Call("StartSandboxEncounter", 4); lab.SetPlayerInvincible(true); lab.SetPaused(false);
        // Batch Editor has no physical focus: exercise the same Shift owner explicitly.
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
        yield return Capture("dense-shift.png");
        lab.CombatV2.Suspend(); lab.Station.enabled = true;
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
        var outer = lab.Station.Rings.Last(); float phase = outer.Phase;
        Call("SelectSandboxRing", 1); Call("ChangeSandboxRings", false); Call("ChangeSandboxRings", true);
        var radii = lab.Station.Rings.Select(r => r.Radius).ToArray();
        Assert.That(radii.Distinct().Count(), Is.EqualTo(3));
        Assert.That(outer.Phase, Is.EqualTo(phase));
        yield return null;
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
        if (File.Exists(path)) File.Delete(path);
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


