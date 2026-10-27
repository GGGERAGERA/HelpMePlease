#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Subject42.Combat.OrbitalStation;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

public sealed class OrbitalCombatV2PlacementTests
{
    private const BindingFlags Fields = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private EnemyOrbitalLabController lab;
    private OrbitalStationRuntime station;
    private OrbitalCombatV2LabPlacement Placement => lab.Placement;
    private OrbitalRingRuntime Ring => station.Rings[0];
    private OrbitalModuleRuntime Gun => Ring.Mounts[0].Module;

    [SetUp] public void Preserve() => CoreTestSupport.PreservePreferences();
    [UnitySetUp]
    public IEnumerator OpenLab()
    {
        EditorSceneManager.OpenScene(EnemyOrbitalLabController.ScenePath);
        yield return new EnterPlayMode();
        lab = Object.FindFirstObjectByType<EnemyOrbitalLabController>();
        yield return CoreTestSupport.Await(() => lab.Ready && lab.SandboxReady);
        lab.SelectShootingBuild(EnemyOrbitalLabController.ShootingBuild.Pistol);
        station = lab.Station;
        lab.ClearEnemies(); lab.SetPlayerInvincible(true); lab.SetFrozen(true);
        lab.SetPaused(true);
        TestContext.WriteLine($"Footprint={Gun.PresentationReach:F3}m; ring radius={Ring.Radius:F3}m");
    }
    [UnityTearDown] public IEnumerator Cleanup() => CoreTestSupport.CleanupPlayMode();

    [UnityTest]
    public IEnumerator FreeAngleUsesPausedPreviewAndDoesNotMoveUntilConfirmed()
    {
        Assert.That(Select(Gun), Is.True, "Installed weapons must be selectable on lab pause.");
        float previous = Gun.CurrentMount.LocalPhase;
        Preview(Gun, 23.7f);
        Assert.That(Placement.PreviewValid, Is.True);
        Assert.That(Placement.PreviewAngle, Is.EqualTo(23.7f).Within(.01f));
        Assert.That(Gun.CurrentMount.LocalPhase, Is.EqualTo(previous), "Preview must not move or detach the live weapon.");
        Assert.That(Confirm(), Is.True);
        Assert.That(Gun.CurrentMount.LocalPhase, Is.EqualTo(23.7f).Within(.01f));
        Vector2 expected = new(Mathf.Cos((Ring.Phase + 23.7f) * Mathf.Deg2Rad), Mathf.Sin((Ring.Phase + 23.7f) * Mathf.Deg2Rad));
        var body = Gun.CurrentMount.Transform.GetComponentInChildren<OrbitalModuleView>();
        Assert.That(Vector2.Angle(body.transform.right, expected), Is.LessThan(.1f));
        lab.SetPaused(false);
        Assert.That(Select(Gun), Is.False, "Combat must reject editing.");
        Assert.That(Gun.CurrentMount.LocalPhase, Is.EqualTo(23.7f).Within(.01f));
        yield return null;
    }

    [UnityTest]
    public IEnumerator WrapGapAndMountChangesPreserveCommittedAngles()
    {
        Assert.That(Move(Gun, 359f), Is.True);
        var second = Ring.Mounts[1].Module;
        float originalSecond = second.CurrentMount.LocalPhase;
        Assert.That(Move(second, 0f), Is.False, "359 and 0 degrees must be treated as neighbouring positions.");
        Assert.That(second.CurrentMount.LocalPhase, Is.EqualTo(originalSecond));
        Assert.That(Move(second, 100.3f), Is.True);
        int addedIndex = Ring.MountCount;
        Assert.That(station.UpgradeRingCapacity(Ring.RingId), Is.True);
        Assert.That(station.AddMount(Ring.RingId, out _), Is.True);
        Assert.That(Gun.CurrentMount.LocalPhase, Is.EqualTo(359f).Within(.01f), "AddMount must not rebalance user positions.");
        Assert.That(second.CurrentMount.LocalPhase, Is.EqualTo(100.3f).Within(.01f));
        Assert.That(station.InstallModule(OrbitalModuleKind.Pistol, Ring.RingId, addedIndex, out _), Is.True);
        Assert.That(Gun.CurrentMount.LocalPhase, Is.EqualTo(359f).Within(.01f));
        Assert.That(second.CurrentMount.LocalPhase, Is.EqualTo(100.3f).Within(.01f));
        Assert.That(Move(Gun, 345.2f), Is.True, "The same installed weapon can be repositioned again.");
        Assert.That(Gun.CurrentMount.MountIndex, Is.EqualTo(0));
        lab.ResetLab();
        yield return CoreTestSupport.Await(() => lab.Ready && lab.Station != station && lab.Station.Rings[0].MountCount == addedIndex + 1);
        station = lab.Station;
        lab.SetPaused(true);
        Assert.That(Gun.CurrentMount.LocalPhase, Is.EqualTo(345.2f).Within(.01f));
        Assert.That(Ring.Mounts[1].LocalPhase, Is.EqualTo(100.3f).Within(.01f));
    }

    [UnityTest]
    public IEnumerator PresetsReuseFourIdenticalGunsWithoutResettingClocksAndCustomSurvivesReset()
    {
        Preset("EVEN");
        Assert.That(station.Modules.Count, Is.EqualTo(4), "Comparison presets must contain exactly four identical guns.");
        var guns = station.Modules.Cast<OrbitalProjectileModule>().ToArray();
        Assert.That(guns.Cast<EnemyOrbitalLabWeapon>().All(g => ReferenceEquals(g.SourceProfile, lab.Pistol)), Is.True);
        Assert.That(guns.Select(g => g.Settings).Distinct().Count(), Is.EqualTo(4), "Each instance owns its own parameter copy.");
        var ids = guns.Select(g => g.StableModuleId).ToArray();
        var cooldown = typeof(OrbitalModuleRuntime).GetProperty("RuntimeCooldown", Fields);
        var timers = guns.Select(g => (float)cooldown.GetValue(g)).ToArray();
        float phase = Ring.Phase, speed = Ring.RotationSpeed;
        CollectionAssert.AreEqual(new[] { 0f, 90f, 180f, 270f }, Ring.Mounts.Take(4).Select(m => m.LocalPhase).ToArray());
        Preset("GROUPED");
        CollectionAssert.AreEqual(ids, station.Modules.Select(m => m.StableModuleId).ToArray());
        CollectionAssert.AreEqual(timers, guns.Select(g => (float)cooldown.GetValue(g)).ToArray());
        Assert.That(Ring.Phase, Is.EqualTo(phase));
        Assert.That(Ring.RotationSpeed, Is.EqualTo(speed));
        var placed = Ring.Mounts.Where(m => m.Occupied).ToArray();
        for (int a = 0; a < placed.Length; a++)
            for (int b = a + 1; b < placed.Length; b++)
                Assert.That(Vector2.Distance(placed[a].Transform.position, placed[b].Transform.position),
                    Is.GreaterThanOrEqualTo(placed[a].Module.PresentationReach + placed[b].Module.PresentationReach + .079f));
        Preset("EVEN");
        Assert.That(Move(Gun, 7.3f), Is.True);
        Preset("GROUPED");
        Preset("CUSTOM");
        Assert.That(Gun.CurrentMount.LocalPhase, Is.EqualTo(7.3f).Within(.01f));
        Assert.That(lab.BreakableShieldBearerPrefab, Is.Not.Null, "Reset requires a valid authored lab enemy reference.");
        lab.ResetLab();
        yield return CoreTestSupport.Await(() => lab.Ready && lab.Station != station && lab.Station.Modules.Count == 4);
        station = lab.Station;
        lab.SetPaused(true);
        Assert.That(Gun.CurrentMount.LocalPhase, Is.EqualTo(7.3f).Within(.01f), "Lab reset preserves the chosen custom layout.");
    }

    [UnityTest]
    public IEnumerator RotationAndShiftKeepLocalOffsetsAndUnboundRingsKeepProductionRebalance()
    {
        Assert.That(Move(Gun, 23.7f), Is.True);
        float other = Ring.Mounts[1].LocalPhase;
        lab.SetPaused(false);
        lab.CombatV2.RouteShift(.01f, false, true);
        lab.CombatV2.RouteShift(.1f, true, true);
        Ring.State.BaseRotationSpeed = 90f; Ring.State.CurrentPhase = 0f;
        Ring.Tick(1f);
        Assert.That(Ring.Phase, Is.EqualTo(36f).Within(.01f));
        Assert.That(Gun.CurrentMount.LocalPhase, Is.EqualTo(23.7f).Within(.01f));
        Assert.That(Ring.Mounts[1].LocalPhase, Is.EqualTo(other));
        lab.CombatV2.RouteShift(.1f, false, true);
        Ring.Tick(1f);
        Assert.That(Ring.Phase, Is.EqualTo(126f).Within(.01f));
        Placement.Unbind();
        Assert.That(station.UpgradeRingCapacity(Ring.RingId), Is.True);
        Assert.That(station.AddMount(Ring.RingId, out _), Is.True);
        Assert.That(Ring.Mounts[0].LocalPhase, Is.EqualTo(0f), "Without a lab owner, existing production rebalancing remains unchanged.");
        Assert.That(Ring.Mounts[1].LocalPhase, Is.EqualTo(90f));
        yield return null;
    }

    private bool Select(OrbitalModuleRuntime module) => Placement.TrySelect(module.StableModuleId);
    private void Preview(OrbitalModuleRuntime module, float localAngle)
    {
        float radians = (module.CurrentMount.Ring.Phase + localAngle) * Mathf.Deg2Rad;
        Vector2 world = (Vector2)station.transform.position + new Vector2(Mathf.Cos(radians), Mathf.Sin(radians)) * module.CurrentMount.Ring.Radius;
        Placement.PreviewWorld(world);
    }
    private bool Confirm() => Placement.Confirm();
    private bool Move(OrbitalModuleRuntime module, float angle) { if (!Select(module)) return false; Preview(module, angle); return Confirm(); }
    private void Preset(string name) => lab.SelectPlacementPreset(name);
}
#endif

