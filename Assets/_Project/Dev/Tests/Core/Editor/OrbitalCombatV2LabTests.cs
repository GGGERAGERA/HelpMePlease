#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Subject42.Combat.OrbitalStation;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

public sealed class OrbitalCombatV2LabTests
{
    private const BindingFlags Fields = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private EnemyOrbitalLabController lab;
    private OrbitalStationRuntime station;
    private ProductionOrbitalCombatAdapter adapter;
    private EnemyOrbitalLabWeapon weapon;

    [SetUp] public void Preserve() => CoreTestSupport.PreservePreferences();
    [UnitySetUp]
    public IEnumerator OpenLab()
    {
        EditorSceneManager.OpenScene(EnemyOrbitalLabController.ScenePath);
        yield return new EnterPlayMode();
        lab = Object.FindFirstObjectByType<EnemyOrbitalLabController>();
        yield return CoreTestSupport.Await(() => lab.Ready && lab.SandboxReady);
        lab.SetPlayerInvincible(true);
        lab.SetFrozen(true);
        lab.ClearEnemies();
        lab.SelectShootingBuild(EnemyOrbitalLabController.ShootingBuild.Pistol);
        lab.SetPaused(false); // This clock/input fixture exercises active combat, unlike sandbox startup.
        station = lab.Station;
        station.enabled = false;
        adapter = (ProductionOrbitalCombatAdapter)station.Combat;
        weapon = station.Modules.OfType<EnemyOrbitalLabWeapon>().First();
        RouteShift(.01f, false, true);
        yield return null;
    }
    [UnityTearDown] public IEnumerator Cleanup() => CoreTestSupport.CleanupPlayMode();

    [UnityTest]
    public IEnumerator FixedShotsUseRadialMuzzleWithoutTargetsAndKeepTheProfileCadence()
    {
        SetFixed(true);
        var ring = weapon.CurrentMount.Ring;
        station.transform.localPosition = new Vector3(2f, -1f, 0f);
        foreach (float angle in new[] { 0f, 90f, 359f })
        {
            ring.State.CurrentPhase = angle;
            ring.Tick(0f);
            adapter.DebugClearProjectiles();
            SetCooldown(0f);
            int before = weapon.ShotsFired;
            weapon.Tick(.001f);
            Assert.That(weapon.ShotsFired, Is.EqualTo(before + 1), "A fixed gun must fire into an empty arena.");
            Assert.That(adapter.LabActiveProjectileCount, Is.EqualTo(1), "A real pooled shot must exist, not only muzzle feedback.");
            var shot = ActiveShot();
            Vector2 direction = (Vector2)shot.GetType().GetField("Direction", Fields).GetValue(shot);
            Assert.That(Mathf.Abs(Mathf.DeltaAngle(angle, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg)), Is.LessThan(.1f));
            var visual = weapon.CurrentMount.Transform.GetComponentInChildren<OrbitalModuleView>();
            Assert.That(Vector2.Angle(visual.transform.right, direction), Is.LessThan(.1f));
            Vector2 start = (Vector2)shot.GetType().GetField("Position", Fields).GetValue(shot);
            adapter.Tick(.1f);
            Vector2 end = (Vector2)shot.GetType().GetField("Position", Fields).GetValue(shot);
            Assert.That(Vector2.Distance(start, end), Is.EqualTo(1.3f).Within(.001f), "Keep this scene's authored 13 m/s pistol.");
            before = weapon.ShotsFired;
            weapon.Tick(.1f);
            Assert.That(weapon.ShotsFired, Is.EqualTo(before), "No immediate extra shot on mode/angle changes.");
            weapon.Tick(.451f);
            Assert.That(weapon.ShotsFired, Is.EqualTo(before + 1));
        }
        var target = new GameObject("Off-axis target").AddComponent<EnemyHealth>();
        target.transform.position = weapon.WorldPosition + Vector2.up * 3f;
        ring.State.CurrentPhase = 0f;
        ring.Tick(0f);
        adapter.DebugClearProjectiles();
        SetCooldown(0f);
        weapon.Tick(.001f);
        Assert.That(Vector2.Angle((Vector2)ActiveShot().GetType().GetField("Direction", Fields).GetValue(ActiveShot()), Vector2.right), Is.LessThan(.1f));
        SetFixed(false);
        SetCooldown(0f);
        adapter.DebugClearProjectiles();
        weapon.Tick(.001f);
        Assert.That(adapter.LabActiveProjectileCount, Is.EqualTo(1));
        Assert.That(Vector2.Angle((Vector2)ActiveShot().GetType().GetField("Direction", Fields).GetValue(ActiveShot()), Vector2.up), Is.LessThan(.1f), "Legacy aiming remains available.");
        Object.Destroy(target.gameObject);
        yield return null;
        adapter.DebugClearProjectiles();
        SetCooldown(0f);
        int legacyCount = weapon.ShotsFired;
        weapon.Tick(1f);
        Assert.That(weapon.ShotsFired, Is.EqualTo(legacyCount), "Legacy mode must still wait for a target.");
    }

    [UnityTest]
    public IEnumerator OrbitMultiplierPreservesSignedPhaseAndWeaponClockAtThirtySixtyAndOneTwentyHz()
    {
        SetFixed(true);
        var ring = weapon.CurrentMount.Ring;
        weapon.Settings.FireInterval = .5f;
        foreach (int hz in new[] { 30, 60, 120 })
        {
            int[] shots = new int[2];
            var firingFrames = new[] { new List<int>(), new List<int>() };
            for (int variant = 0; variant < 2; variant++)
            {
                ring.State.BaseRotationSpeed = 90f;
                ring.State.Direction = 1;
                ring.State.CurrentPhase = 0f;
                SetRingMultiplier(ring, variant == 0 ? 1f : .4f);
                weapon.ResetFiring();
                SetCooldown(.5f);
                int before = weapon.ShotsFired;
                for (int frame = 0; frame < hz * 2; frame++)
                {
                    int shotCount = weapon.ShotsFired;
                    ring.Tick(1f / hz); weapon.Tick(1f / hz);
                    if (weapon.ShotsFired != shotCount) firingFrames[variant].Add(frame);
                }
                shots[variant] = weapon.ShotsFired - before;
                Assert.That(ring.Phase, Is.EqualTo(variant == 0 ? 180f : 72f).Within(.01f));
            }
            Assert.That(shots[1], Is.EqualTo(shots[0]).And.GreaterThan(0), "Angular slowing must not change the gun's clock.");
            CollectionAssert.AreEqual(firingFrames[0], firingFrames[1], "Every shot must occur on the same frame, not only the same total count.");
            SetRingMultiplier(ring, 1f);
            for (int frame = 0; frame < hz; frame++) ring.Tick(1f / hz);
            Assert.That(ring.Phase, Is.EqualTo(162f).Within(.01f), "Continue from the attained phase without catching up.");
        }
        float[] speed = { 90f, 60f, 120f }, want = { 71f, 311f, 95f };
        for (int i = 0; i < 3; i++)
        {
            var other = station.Rings[i];
            other.State.BaseRotationSpeed = speed[i];
            other.State.Direction = i == 1 ? -1 : 1;
            other.State.CurrentPhase = 359f;
            SetRingMultiplier(other, .4f);
            other.Tick(2f);
            Assert.That(other.Phase, Is.EqualTo(want[i]).Within(.01f));
        }
        yield return null;
    }

    [UnityTest]
    public IEnumerator ShiftRoutesExclusivelyAndLeavesWorldClocksAndCenterControlAlone()
    {
        float fixedStep = Time.fixedDeltaTime;
        float energy = station.BulletTime.Energy;
        var movement = lab.Player.GetComponent<CharacterMovement2D>();
        float movementScale = movement.BulletTimeControlMultiplier;
        var center = station.GetComponent<OrbitalCenterShift>();
        bool centerEligibleBefore = center.CanControl;
        var tick = typeof(OrbitalStationRuntime).GetMethod("TickRightMouse", Fields);
        tick.Invoke(station, new object[] { .3f, RouteShift(.3f, true, true), false });
        Assert.That(Time.timeScale, Is.EqualTo(1f), "Orbit Shift must never enter global Bullet Time.");
        Assert.That(Time.fixedDeltaTime, Is.EqualTo(fixedStep));
        Assert.That(station.BulletTime.Energy, Is.EqualTo(energy));
        Assert.That(movement.BulletTimeControlMultiplier, Is.EqualTo(movementScale));
        var ring = station.Rings[0];
        ring.State.BaseRotationSpeed = 90f;
        ring.State.CurrentPhase = 0f;
        ring.Tick(.5f);
        Assert.That(ring.Phase, Is.EqualTo(18f).Within(.01f));
        Assert.That(center.IsPointerControlConfigured, Is.True);
        Assert.That(center.CanControl, Is.EqualTo(centerEligibleBefore), "Orbit Shift must not change RMB eligibility.");
        if (Application.isFocused)
            typeof(OrbitalCenterShift).GetMethod("AdvancePointer", Fields).Invoke(center,
                new object[] { true, true, false, (Vector2)lab.Player.transform.position + Vector2.right * 3f, .1f });
        else
        {
            Assert.That(center.CanControl, Is.False, "Retain the production OS-focus guard in batch mode.");
            TestContext.WriteLine("Headless: center motion exercised programmatically; physical RMB remains a manual check.");
            typeof(OrbitalCenterShift).GetMethod("Advance", Fields).Invoke(center, new object[] { Vector2.right, true, .1f });
        }
        Assert.That(center.Offset.x, Is.GreaterThan(0f), "Orbit Shift must not block RMB center steering.");
        Assert.That(Time.timeScale, Is.EqualTo(1f));
        RouteShift(.1f, false, true);
        ring.Tick(1f);
        Assert.That(ring.Phase, Is.EqualTo(108f).Within(.01f));
        RouteShift(.1f, true, true);
        RouteShift(.1f, true, false);
        ring.State.CurrentPhase = 0f;
        RouteShift(.1f, true, true);
        ring.Tick(1f);
        Assert.That(ring.Phase, Is.EqualTo(90f).Within(.01f), "Blocked input must require release before reactivation.");
        RouteShift(.1f, false, true);
        SetBulletTime(true);
        RouteShift(.1f, false, true);
        tick.Invoke(station, new object[] { .3f, RouteShift(.3f, true, true), false });
        Assert.That(Time.timeScale, Is.EqualTo(.4f).Within(.001f));
        ring.State.CurrentPhase = 0f;
        ring.Tick(.5f);
        Assert.That(ring.Phase, Is.EqualTo(45f).Within(.01f), "BT must not also apply a local angular multiplier.");
        station.InputOwner.ReleaseBulletTime();
        Assert.That(Time.timeScale, Is.EqualTo(1f));
        Assert.That(Time.fixedDeltaTime, Is.EqualTo(fixedStep).Within(.000001f));
        yield return null;
    }

    [UnityTest]
    public IEnumerator ReplacementOwnerRequiresReleaseBeforeAcceptingHeldShift()
    {
        lab.CombatV2.Dispose();
        var ring = station.Rings[0];
        ring.State.BaseRotationSpeed = 90f;
        ring.State.CurrentPhase = 0f;
        using (var replacement = new OrbitalCombatV2LabControl(station))
        {
            replacement.RouteShift(.1f, true, true);
            ring.Tick(1f);
            Assert.That(ring.Phase, Is.EqualTo(90f).Within(.01f), "Reset/re-enable must not consume a Shift held through old ownership.");
            replacement.RouteShift(.1f, false, true);
            replacement.RouteShift(.1f, true, true);
            ring.Tick(.5f);
            Assert.That(ring.Phase, Is.EqualTo(108f).Within(.01f));
        }
        ring.Tick(1f);
        Assert.That(ring.Phase, Is.EqualTo(198f).Within(.01f), "Disposal restores angular speed without rewinding phase.");
        Assert.That(station.LabShiftInput, Is.Null);
        var mount = weapon.CurrentMount;
        mount.Detach();
        var ordinary = new OrbitalProjectileModule(station, 100007, weapon.Settings);
        Assert.That(mount.Attach(ordinary), Is.True);
        ordinary.Tick(1f);
        Assert.That(ordinary.ShotsFired, Is.Zero, "An ordinary production module must still wait for a target even in this lab.");
        ordinary.Teardown();
        yield return null;
    }

    [UnityTest]
    public IEnumerator TargetlessChargeAndBurstReuseTheExistingProfileAndReadMuzzleAtFireTime()
    {
        SetFixed(true);
        var ring = weapon.CurrentMount.Ring;
        ring.State.CurrentPhase = 0f; ring.Tick(0f);
        weapon.Settings.ChargeSeconds = .28f;
        weapon.Settings.BurstShots = 3;
        weapon.Settings.BurstInterval = .09f;
        SetCooldown(0f);
        int before = weapon.ShotsFired;
        weapon.Tick(.001f);
        Assert.That(weapon.ShotsFired, Is.EqualTo(before), "Starting charge must not skip its timer.");
        ring.State.CurrentPhase = 90f; ring.Tick(0f);
        adapter.DebugClearProjectiles();
        weapon.Tick(.281f);
        Assert.That(weapon.ShotsFired, Is.EqualTo(before + 1));
        Assert.That(Vector2.Angle((Vector2)ActiveShot().GetType().GetField("Direction", Fields).GetValue(ActiveShot()), Vector2.up), Is.LessThan(.1f));
        weapon.Tick(.09f);
        Assert.That(weapon.ShotsFired, Is.EqualTo(before + 2));
        weapon.Tick(.09f);
        Assert.That(weapon.ShotsFired, Is.EqualTo(before + 3));
        weapon.Tick(.1f);
        Assert.That(weapon.ShotsFired, Is.EqualTo(before + 3), "Burst completion retains the ordinary fire interval.");
        yield return null;
    }

    private void SetFixed(bool value) => lab.SetFixedDirection(value);
    private bool RouteShift(float dt, bool held, bool allowed) => lab.CombatV2.RouteShift(dt, held, allowed);
    private void SetBulletTime(bool value) => lab.CombatV2.SetBulletTime(value);
    private static void SetRingMultiplier(OrbitalRingRuntime ring, float value) => ring.LabRotationMultiplier = value;
    private void SetCooldown(float value) => typeof(OrbitalModuleRuntime).GetProperty("RuntimeCooldown", Fields).SetValue(weapon, value);
    private object ActiveShot() => ((IEnumerable)adapter.GetType().GetField("projectiles", Fields).GetValue(adapter))
        .Cast<object>().Single(p => (bool)p.GetType().GetField("Active", Fields).GetValue(p));

    public static void OpenLabForManualTesting() => EditorSceneManager.OpenScene(EnemyOrbitalLabController.ScenePath);
}
#endif

