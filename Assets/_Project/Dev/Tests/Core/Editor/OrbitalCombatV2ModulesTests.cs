#if UNITY_EDITOR
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Subject42.Combat.OrbitalStation;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

public sealed class OrbitalCombatV2ModulesTests
{
    private const BindingFlags Fields = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private EnemyOrbitalLabController lab;
    private OrbitalStationRuntime station;
    private ProductionOrbitalCombatAdapter adapter;
    private OrbitalCombatV2WeaponModules Modules => lab.WeaponModules;
    private EnemyOrbitalLabWeapon Gun => station.Modules.OfType<EnemyOrbitalLabWeapon>().First();
    [SetUp] public void Preserve() => CoreTestSupport.PreservePreferences();
    [UnitySetUp]
    public IEnumerator OpenLab()
    {
        EditorSceneManager.OpenScene(EnemyOrbitalLabController.ScenePath);
        yield return new EnterPlayMode();
        lab = Object.FindFirstObjectByType<EnemyOrbitalLabController>();
        yield return CoreTestSupport.Await(() => lab.Ready && lab.SandboxReady);
        lab.SelectPlacementPreset("EVEN"); station = lab.Station;
        adapter = (ProductionOrbitalCombatAdapter)station.Combat;
        lab.ClearEnemies(); lab.SetPlayerInvincible(true); lab.SetFrozen(true);
    }
    [UnityTearDown] public IEnumerator Cleanup() => CoreTestSupport.CleanupPlayMode();

    [UnityTest]
    public IEnumerator RapidAndReplacementOnlyChangeSelectedRuntimeCopyAndKeepOrbitSpeed()
    {
        var gun = Gun;
        var other = station.Modules.OfType<EnemyOrbitalLabWeapon>().Skip(1).First();
        string source = JsonUtility.ToJson(lab.Pistol);
        float speed = station.Rings[0].RotationSpeed;
        Assert.That(Set(gun, 0, "RAPID"), Is.True);
        Assert.That(gun.Settings.FireInterval, Is.EqualTo(.55f / 1.5f).Within(.00001f));
        Assert.That(other.Settings.FireInterval, Is.EqualTo(.55f));
        Assert.That(ReferenceEquals(gun.Settings, other.Settings), Is.False);
        Assert.That(ReferenceEquals(gun.Settings, lab.Pistol), Is.False);
        Assert.That(JsonUtility.ToJson(lab.Pistol), Is.EqualTo(source));
        Assert.That(station.Rings[0].RotationSpeed, Is.EqualTo(speed));
        Assert.That(Set(gun, 0, "PIERCE"), Is.True);
        Assert.That(gun.Settings.FireInterval, Is.EqualTo(.55f));
        Assert.That(gun.Settings.BodyPierces, Is.EqualTo(3));
        Assert.That(Set(gun, 0, "EMPTY"), Is.True);
        Assert.That(gun.Settings.BodyPierces, Is.EqualTo(1));
        Assert.That(other.Settings.BodyPierces, Is.EqualTo(1));
        lab.SetPaused(false);
        Assert.That(Set(gun, 0, "RAPID"), Is.False, "Modules may only be edited on the lab pause.");
        yield return null;
    }

    [UnityTest]
    public IEnumerator PierceHitsThreeRealTargetsAndRemovalRestoresOneTarget()
    {
        var gun = Gun;
        Assert.That(Set(gun, 0, "PIERCE"), Is.True);
        var ring = gun.CurrentMount.Ring; ring.State.CurrentPhase = 0f; ring.Tick(0f);
        var targets = new EnemyHealth[4];
        for (int i = 0; i < targets.Length; i++)
        {
            var body = new GameObject("Pierce target " + i); body.transform.SetParent(lab.transform);
            body.transform.position = gun.WorldPosition + Vector2.right * (i + 1f);
            targets[i] = body.AddComponent<EnemyHealth>(); targets[i].SetRuntimeMaxHealth(100f);
            body.AddComponent<CircleCollider2D>().radius = .12f;
        }
        Physics2D.SyncTransforms();
        Ready(gun); gun.Tick(.001f); adapter.Tick(.4f);
        CollectionAssert.AreEqual(new[] { 92f, 92f, 92f, 100f }, targets.Select(t => t.CurrentHealth).ToArray());
        Assert.That(Set(gun, 0, "EMPTY"), Is.True);
        foreach (var target in targets) target.SetRuntimeMaxHealth(100f);
        adapter.DebugClearProjectiles(); Ready(gun); gun.Tick(.001f); adapter.Tick(.4f);
        CollectionAssert.AreEqual(new[] { 92f, 100f, 100f, 100f }, targets.Select(t => t.CurrentHealth).ToArray());
        yield return null;
    }

    [UnityTest]
    public IEnumerator MultishotAddsOneFixedRayAndKeepsChargeBurstAndSpread()
    {
        var gun = Gun;
        Assert.That(Set(gun, 0, "MULTISHOT"), Is.True);
        lab.SetFixedDirection(false);
        gun.CurrentMount.Ring.State.CurrentPhase = 0f; gun.CurrentMount.Ring.Tick(0f);
        adapter.DebugClearProjectiles(); Ready(gun); gun.Tick(.001f);
        CollectionAssert.AreEqual(new[] { 0f, 6f }, ShotAngles().Select(a => Mathf.Round(a * 100f) / 100f).ToArray());
        Assert.That(gun.Settings.Pellets, Is.EqualTo(1), "An extra ray must not replace the original pellet profile.");
        gun.ResetFiring(); Ready(gun); adapter.DebugClearProjectiles();
        gun.Settings.ChargeSeconds = .28f; gun.Settings.BurstShots = 3; gun.Settings.BurstInterval = .09f;
        gun.Settings.Pellets = 3; gun.Settings.SpreadDegrees = 20f;
        int before = gun.ShotsFired;
        gun.Tick(.001f);
        Assert.That(gun.ShotsFired, Is.EqualTo(before));
        gun.CurrentMount.Ring.State.CurrentPhase = 90f; gun.CurrentMount.Ring.Tick(0f);
        gun.Tick(.281f);
        CollectionAssert.AreEqual(new[] { 80f, 90f, 96f, 100f }, ShotAngles().Select(a => Mathf.Round(a * 100f) / 100f).ToArray());
        Assert.That(gun.ShotsFired, Is.EqualTo(before + 1));
        gun.Tick(.09f); gun.Tick(.09f);
        Assert.That(gun.ShotsFired, Is.EqualTo(before + 3));
        Assert.That(adapter.LabActiveProjectileCount, Is.EqualTo(12));
        yield return null;
    }

    [UnityTest]
    public IEnumerator SlotsSpokesAndMovingTheSameWeaponPreserveItsModules()
    {
        var gun = Gun; int id = gun.StableModuleId;
        Assert.That(Set(gun, 0, "RAPID"), Is.True);
        Assert.That(Set(gun, 1, "PIERCE"), Is.False);
        Assert.That(Unlock(gun), Is.True);
        Assert.That(Set(gun, 1, "RAPID"), Is.False, "Duplicate types do not stack.");
        Assert.That(Set(gun, 1, "PIERCE"), Is.True);
        Assert.That(lab.Placement.TrySelect(id), Is.True);
        float radians = (gun.CurrentMount.Ring.Phase + 23.7f) * Mathf.Deg2Rad;
        lab.Placement.PreviewWorld((Vector2)station.transform.position + new Vector2(Mathf.Cos(radians), Mathf.Sin(radians)) * gun.CurrentMount.Ring.Radius);
        Assert.That(lab.Placement.Confirm(), Is.True);
        Assert.That(gun.StableModuleId, Is.EqualTo(id));
        Assert.That(Get(gun, 0), Is.EqualTo("RAPID")); Assert.That(Get(gun, 1), Is.EqualTo("PIERCE"));
        Modules.UpdateSpokes();
        var spoke = lab.GetComponentsInChildren<LineRenderer>().Single(l => l.name == "Weapon spoke " + id);
        Assert.That(Vector2.Distance(spoke.GetPosition(0), station.transform.position), Is.LessThan(.001f));
        Assert.That(Vector2.Distance(spoke.GetPosition(1), gun.CurrentMount.Transform.position), Is.LessThan(.001f));
        gun.CurrentMount.Ring.Tick(.5f);
        Modules.UpdateSpokes();
        Assert.That(Vector2.Distance(spoke.GetPosition(1), gun.CurrentMount.Transform.position), Is.LessThan(.001f));
        var other = station.Modules.OfType<EnemyOrbitalLabWeapon>().First(g => g != gun);
        Assert.That(Set(other, 0, "MULTISHOT"), Is.True);
        Modules.Select(id); Modules.UpdateSpokes(); CaptureSpokes();
        lab.SelectShootingBuild(EnemyOrbitalLabController.ShootingBuild.Saw);
        int saw = station.Modules.First().StableModuleId;
        Assert.That(Modules.SetModule(saw, 0, "RAPID"), Is.False);
        yield return null;
    }

    [UnityTest]
    public IEnumerator ExperimentalPresetsKeepRingAndEnemiesAndRestoreSelectedBuildOnReset()
    {
        Preset("A");
        Assert.That(station.Modules.Count, Is.EqualTo(4));
        float phase = station.Rings[0].Phase, speed = station.Rings[0].RotationSpeed;
        lab.Spawn(1); int enemies = lab.AliveEnemies;
        var enemy = lab.GetComponentsInChildren<EnemyHealth>().Single();
        float hp = enemy.CurrentHealth; Vector3 position = enemy.transform.position;
        Preset("B");
        Assert.That(station.Modules.Count, Is.EqualTo(1));
        Assert.That(Get(Gun, 0), Is.EqualTo("RAPID")); Assert.That(Get(Gun, 1), Is.EqualTo("PIERCE"));
        Preset("C");
        Assert.That(station.Modules.Count, Is.EqualTo(2));
        var second = station.Modules.OfType<EnemyOrbitalLabWeapon>().Skip(1).Single();
        Assert.That(Get(Gun, 0), Is.EqualTo("RAPID")); Assert.That(Get(second, 0), Is.EqualTo("MULTISHOT"));
        Assert.That(station.Rings[0].Phase, Is.EqualTo(phase)); Assert.That(station.Rings[0].RotationSpeed, Is.EqualTo(speed));
        Assert.That(lab.AliveEnemies, Is.EqualTo(enemies)); Assert.That(enemy.CurrentHealth, Is.EqualTo(hp));
        Assert.That(enemy.transform.position, Is.EqualTo(position));
        Assert.That(Unlock(Gun), Is.True); Assert.That(Set(Gun, 1, "PIERCE"), Is.True);
        lab.RestartScenario();
        yield return CoreTestSupport.Await(() => lab.Ready && lab.Station != station && lab.Station.Modules.Count == 2);
        station = lab.Station; lab.SetPaused(true);
        Assert.That(Get(Gun, 0), Is.EqualTo("RAPID")); Assert.That(Get(Gun, 1), Is.EqualTo("PIERCE"));
        Assert.That(Get(station.Modules.OfType<EnemyOrbitalLabWeapon>().Skip(1).Single(), 0), Is.EqualTo("MULTISHOT"));
    }

    [UnityTest]
    public IEnumerator NewlyInstalledProjectileReceivesAnIndependentDefaultSlot()
    {
        var ring = station.Rings[0];
        Assert.That(station.UpgradeRingCapacity(ring.RingId), Is.True);
        Assert.That(station.AddMount(ring.RingId, out _), Is.True);
        Assert.That(station.InstallModule(OrbitalModuleKind.Pistol, ring.RingId, 4, out _), Is.True);
        var installed = ring.Mounts[4].Module as EnemyOrbitalLabWeapon;
        Assert.That(installed, Is.Not.Null, "The lab factory must register an ordinary newly installed pistol too.");
        Assert.That(Set(installed, 0, "RAPID"), Is.True);
        Assert.That(Gun.Settings.FireInterval, Is.EqualTo(.55f));
        Assert.That(installed.Settings.FireInterval, Is.EqualTo(.55f / 1.5f).Within(.00001f));
        yield return null;
    }

    [UnityTest]
    public IEnumerator RestoredWeaponDoesNotDonateModulesToANewStableId()
    {
        Preset("B");
        lab.RestartScenario();
        yield return CoreTestSupport.Await(() => lab.Ready && lab.Station != station && lab.Station.Modules.Count == 1);
        station = lab.Station; lab.SetPaused(true);
        var restored = Gun; var ring = restored.CurrentMount.Ring;
        Assert.That(Get(restored, 0), Is.EqualTo("RAPID"));
        Assert.That(station.AddMount(ring.RingId, out _), Is.True);
        Assert.That(station.MoveModule(restored.StableModuleId, ring.RingId, 1, out _), Is.True);
        Assert.That(station.InstallModule(OrbitalModuleKind.Pistol, ring.RingId, 0, out _), Is.True);
        var fresh = ring.Mounts[0].Module as EnemyOrbitalLabWeapon;
        Assert.That(fresh, Is.Not.Null);
        Assert.That(fresh.StableModuleId, Is.Not.EqualTo(restored.StableModuleId));
        Assert.That(Modules.SlotCount(fresh.StableModuleId), Is.EqualTo(1), "Reset snapshots may only restore the original build once.");
        Assert.That(Get(fresh, 0), Is.EqualTo("EMPTY"));
        Assert.That(fresh.Settings.FireInterval, Is.EqualTo(.55f));
        Assert.That(Get(restored, 0), Is.EqualTo("RAPID")); Assert.That(Get(restored, 1), Is.EqualTo("PIERCE"));
    }

    [UnityTest]
    public IEnumerator LeavingPlayDoesNotWriteAuthoredProfilesOrPrefabs()
    {
        string[] paths = { "Assets/_Project/Data/Orbital/OrbitalPresentationConfig.asset", "Assets/_Project/prefabs/Orbital/Core/Station.prefab",
            EnemyOrbitalLabController.ScenePath };
        var bytes = paths.Select(File.ReadAllBytes).ToArray();
        string source = JsonUtility.ToJson(lab.Pistol);
        Assert.That(Set(Gun, 0, "RAPID"), Is.True); Assert.That(Unlock(Gun), Is.True);
        Assert.That(Set(Gun, 1, "MULTISHOT"), Is.True);
        Assert.That(JsonUtility.ToJson(lab.Pistol), Is.EqualTo(source));
        yield return new ExitPlayMode();
        for (int i = 0; i < paths.Length; i++) CollectionAssert.AreEqual(bytes[i], File.ReadAllBytes(paths[i]), paths[i]);
    }

    private bool Set(EnemyOrbitalLabWeapon gun, int slot, string kind) => Modules.SetModule(gun.StableModuleId, slot, kind);
    private bool Unlock(EnemyOrbitalLabWeapon gun) => Modules.UnlockSecondSlot(gun.StableModuleId);
    private string Get(EnemyOrbitalLabWeapon gun, int slot) => Modules.GetModule(gun.StableModuleId, slot);
    private void Preset(string name) => lab.SelectWeaponModulePreset(name);
    private void CaptureSpokes()
    {
        var camera = lab.CameraRig.ControlledCamera;
        var target = new RenderTexture(1280, 800, 24);
        var old = camera.targetTexture; var active = RenderTexture.active;
        var pixels = new Texture2D(1280, 800, TextureFormat.RGB24, false);
        try
        {
            camera.targetTexture = target; camera.Render(); RenderTexture.active = target;
            pixels.ReadPixels(new Rect(0, 0, 1280, 800), 0, 0); pixels.Apply();
            string output = Path.GetFullPath(Path.Combine(Application.dataPath, "../Artifacts/GeneratedQA/OrbitalModules/spokes.png"));
            Directory.CreateDirectory(Path.GetDirectoryName(output));
            File.WriteAllBytes(output, pixels.EncodeToPNG());
            TestContext.WriteLine("Spokes capture: " + output);
            Assert.That(File.Exists(output), Is.True);
        }
        finally { camera.targetTexture = old; RenderTexture.active = active; Object.DestroyImmediate(pixels); Object.DestroyImmediate(target); }
    }
    private static void Ready(OrbitalProjectileModule gun) => typeof(OrbitalModuleRuntime).GetProperty("RuntimeCooldown", Fields).SetValue(gun, 0f);
    private float[] ShotAngles() => ((IEnumerable)adapter.GetType().GetField("projectiles", Fields).GetValue(adapter)).Cast<object>()
        .Where(p => (bool)p.GetType().GetField("Active", Fields).GetValue(p)).Select(p => (Vector2)p.GetType().GetField("Direction", Fields).GetValue(p))
        .Select(v => Mathf.Repeat(Mathf.Atan2(v.y, v.x) * Mathf.Rad2Deg, 360f)).OrderBy(v => v).ToArray();
}
#endif

