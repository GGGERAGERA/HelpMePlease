#if UNITY_EDITOR
using System;
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

// One bounded situation test. Uses existing lab spawns, actors, weapons and movement.
public sealed class TacticalEnemyNightLabTests
{
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    EnemyOrbitalLabController lab;
    static T Read<T>(object value, string field) => (T)value.GetType().GetField(field, Private).GetValue(value);
    static object Call(object value, string method, params object[] args) => value.GetType().GetMethod(method, Private).Invoke(value, args);
    [SetUp] public void Preserve() => CoreTestSupport.PreservePreferences();
    [UnitySetUp] public IEnumerator Open()
    {
        typeof(EnemyOrbitalInterceptionPreview).GetMethod("SetGameView", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, null);
        EditorSceneManager.OpenScene(EnemyOrbitalLabController.ScenePath);
        yield return new EnterPlayMode();
        lab = Object.FindFirstObjectByType<EnemyOrbitalLabController>();
        yield return CoreTestSupport.Await(() => lab.Ready && lab.SandboxReady);
        lab.AutomaticInterceptions = false; lab.CancelInterception();
        lab.SetSegmentedOrbit(true);
        yield return CoreTestSupport.Await(() => lab.Ready && lab.Segmented.Bound && lab.SegmentedOrbit);
        Assert.That(lab.Segmented.LoadPreset("Front Arc"), Is.True);
        lab.Segmented.SetMotion(SegmentedOrbitConfiguration.Motion.Manual);
        lab.SetPlayerInvincible(true); lab.SetPaused(false);
    }
    [UnityTearDown] public IEnumerator Cleanup() => CoreTestSupport.CleanupPlayMode();
    EnemyChaseMovement Spawn(string name, Vector2 position)
    {
        int index = Array.FindIndex(lab.EnemyPrefabs, p => p != null && p.name == name);
        Assert.That(index, Is.GreaterThanOrEqualTo(0), "Existing lab must expose production variant " + name);
        Call(lab, "SpawnAt", index, position);
        return lab.LabEnemies.Last().GetComponent<EnemyChaseMovement>();
    }
    static string Phase(EnemyChaseMovement movement) => Read<object>(movement, "ramPhase").ToString();
    static IEnumerator Observe(float seconds, bool realTime = false)
    {
        // These tests enter Play Mode from the Edit Mode runner. Observe the actual
        // Unity clock instead of assuming it interprets a WaitForSeconds instruction.
        float until = (realTime ? Time.realtimeSinceStartup : Time.time) + seconds;
        while ((realTime ? Time.realtimeSinceStartup : Time.time) < until) yield return null;
    }
    void ClearFixtures()
    {
        lab.ClearDensityEnemies();
        // Manual isolated actors are not a target-density encounter. Transfer spawn
        // ownership back to the existing scenario mode, then keep spawning stopped.
        lab.SetSandboxSpawning(true); lab.SetSandboxSpawning(false);
    }
    static IEnumerator WaitRam(EnemyChaseMovement enemy, string phase)
    {
        TestContext.WriteLine("Wait ram phase: " + phase);
        float deadline = Time.realtimeSinceStartup + 5;
        while (enemy != null && Phase(enemy) != phase && Time.realtimeSinceStartup < deadline) yield return null;
        Assert.That(enemy != null && Phase(enemy) == phase, Is.True,
            enemy == null ? "Ram actor disappeared before " + phase : $"Expected {phase}, actual={Phase(enemy)}, enabled={enemy.IsCommittedRamExperiment}, distance={Vector2.Distance(enemy.transform.position, enemy.Target.position):0.00}, frozen={EnemyDebugAiFreeze.IsFrozen}, timescale={Time.timeScale}, timer={Read<float>(enemy, "ramRemaining"):0.00}");
    }
    void MovePlayer(Vector2 position)
    {
        var body = lab.Player.GetComponent<Rigidbody2D>(); body.linearVelocity = Vector2.zero; body.position = position;
        lab.Player.transform.position = position;
    }
    void Aim(OrbitalModuleRuntime gun, Vector2 position)
    {
        Vector2 from = gun.WorldPosition - (Vector2)lab.Station.transform.position;
        Vector2 to = position - (Vector2)lab.Station.transform.position;
        float delta = Mathf.DeltaAngle(Mathf.Atan2(from.y, from.x) * Mathf.Rad2Deg, Mathf.Atan2(to.y, to.x) * Mathf.Rad2Deg);
        foreach (var ring in lab.Station.Rings) { ring.State.CurrentPhase += delta; ring.Tick(0); }
    }
    static IEnumerator Capture(string name)
    {
        string path = TacticalEnemyNightPreview.Output + "/" + name + ".png";
        ScreenCapture.CaptureScreenshot(path);
        yield return CoreTestSupport.Await(() => File.Exists(path));
        TestContext.WriteLine("Game View 1600×900: " + Path.GetFullPath(path));
    }
    [UnityTest, Timeout(65000)]
    public IEnumerator FlankRotationRamDodgeAndDenseCombat()
    {
        lab.Station.enabled = false;
        var playerBody = lab.Player.GetComponent<Rigidbody2D>();
        var constraints = playerBody.constraints; playerBody.constraints = RigidbodyConstraints2D.FreezeAll;
        var flanker = Spawn("p_Enemy_Flanker", new Vector2(-8, 0));
        Vector2 initial = flanker.GetComponent<Rigidbody2D>().position, previous = initial;
        float largestStep = 0;
        float observedAt = Time.fixedTime;
        while (Time.fixedTime < observedAt + 1.5f)
        {
            yield return new WaitForFixedUpdate();
            Vector2 point = flanker.GetComponent<Rigidbody2D>().position;
            largestStep = Mathf.Max(largestStep, Vector2.Distance(point, previous)); previous = point;
        }
        TestContext.WriteLine($"Flanker observation: fixed elapsed={Time.fixedTime - observedAt:0.000}, position={previous}, speed={Read<float>(flanker, "normalSpeed")}, speed multiplier={Read<float>(flanker, "speedMultiplier")}, threat={Read<float>(flanker, "threatSpeedMultiplier")}, response={Read<float>(flanker, "threatResponseSeconds")}.");
        Assert.That(Mathf.Abs(previous.y - initial.y), Is.GreaterThan(.6f), "Flanker must arrive on a different firing bearing.");
        Assert.That(largestStep, Is.LessThan(.3f), "No movement jumps at authored speed.");
        TestContext.WriteLine($"Flanker: lateral shift={Mathf.Abs(previous.y - initial.y):0.00} m, largest fixed step={largestStep:0.000} m.");
        lab.SetFrozen(true);
        var health = flanker.GetComponent<EnemyHealth>(); float hp = health.CurrentHealth;
        float bearing = Mathf.Atan2(previous.y, previous.x) * Mathf.Rad2Deg;
        // Front Arc's 0…150° set faces away; the actor sits in its unprotected rear.
        foreach (var ring in lab.Station.Rings) { ring.State.CurrentPhase = bearing - 255; ring.Tick(0); }
        lab.Station.enabled = true; yield return Observe(.3f);
        Assert.That(health.CurrentHealth, Is.EqualTo(hp), "A directed loadout does not magically shoot its unprotected rear.");
        var gun = lab.Station.Modules.OfType<EnemyOrbitalLabWeapon>().First();
        Vector2 radial = gun.WorldPosition - (Vector2)lab.Station.transform.position;
        float delta = Mathf.DeltaAngle(Mathf.Atan2(radial.y, radial.x) * Mathf.Rad2Deg, bearing);
        float energy = lab.Segmented.RotationEnergy;
        int steps = Mathf.CeilToInt(Mathf.Abs(delta) / (lab.ManualRotationSpeed * Time.fixedDeltaTime));
        for (int i = 0; i < steps && health != null && !health.IsDead; i++)
        { lab.Segmented.TickManualRotation(Time.fixedDeltaTime, delta > 0, delta < 0, true); yield return new WaitForFixedUpdate(); }
        float until = Time.time + 2f;
        while (health != null && !health.IsDead && health.CurrentHealth >= hp && Time.time < until) yield return null;
        Assert.That(health == null || health.IsDead || health.CurrentHealth < hp, Is.True, "Actual orbital fire hits after Q/E changes the bearing.");
        Assert.That(lab.Segmented.RotationEnergy, Is.LessThan(energy), "Changing the protected side costs rotation energy.");
        TestContext.WriteLine("Front Arc: no rear hit before turn; actual orbital hit after Q/E path, energy spent.");

        ClearFixtures(); lab.SetFrozen(false); lab.Station.enabled = false;
        MovePlayer(Vector2.zero); lab.SetPlayerInvincible(false); lab.Player.Heal(1000);
        var breacher = Spawn("p_Enemy_Breacher", new Vector2(4.4f, 0));
        breacher.GetComponent<LabEnemyDummy>().Invulnerable = true;
        yield return WaitRam(breacher, "Preparing");
        var cue = Read<LineRenderer>(breacher, "ramTelegraph"); var direction = Read<Vector2>(breacher, "ramDirection");
        Assert.That(cue.enabled && !breacher.CanDealContactDamage, Is.True);
        float warnedDistance = Vector2.Distance(cue.GetPosition(0), cue.GetPosition(1));
        Assert.That(warnedDistance, Is.GreaterThan(4.4f));
        float timer = Read<float>(breacher, "ramRemaining"); lab.SetPaused(true); yield return Observe(.08f, true);
        Assert.That(Read<float>(breacher, "ramRemaining"), Is.EqualTo(timer)); lab.SetPaused(false);
        float playerHp = lab.Player.CurrentHealth;
        MovePlayer(Vector2.up * 2.3f); Physics2D.SyncTransforms();
        yield return WaitRam(breacher, "Charging");
        Vector2 chargeStart = breacher.GetComponent<Rigidbody2D>().position;
        yield return WaitRam(breacher, "Recovering");
        Vector2 travel = breacher.GetComponent<Rigidbody2D>().position - chargeStart;
        Assert.That(Read<Vector2>(breacher, "ramDirection"), Is.EqualTo(direction));
        Assert.That(Mathf.Abs(travel.x * direction.y - travel.y * direction.x), Is.LessThan(.15f));
        Assert.That(lab.Player.CurrentHealth, Is.EqualTo(playerHp), "Moving across the warning avoids the charge.");
        Assert.That(breacher.CanDealContactDamage, Is.False);
        var targetHealth = breacher.GetComponent<EnemyHealth>(); targetHealth.GetComponent<LabEnemyDummy>().Invulnerable = false;
        float beforeDamage = targetHealth.CurrentHealth;
        ((ProductionOrbitalCombatAdapter)lab.Station.Combat).ApplyDamage(targetHealth, 1, targetHealth.transform.position);
        Assert.That(targetHealth.CurrentHealth, Is.EqualTo(beforeDamage - 1), "Recovery accepts ordinary orbital damage.");
        Vector2 rest = breacher.GetComponent<Rigidbody2D>().position;
        yield return Observe(.2f);
        Assert.That(Vector2.Distance(rest, breacher.GetComponent<Rigidbody2D>().position), Is.LessThan(.03f));
        breacher.GetComponent<EnemyCollisionHandler>().enabled = false; // Observe cadence without melee hit-stop.
        yield return WaitRam(breacher, "Chasing");
        yield return Observe(.75f);
        Assert.That(Phase(breacher), Is.EqualTo("Chasing"), "Nearby player does not cause another immediate charge.");
        MovePlayer(breacher.transform.position + Vector3.up * 12);
        yield return Observe(1.4f);
        MovePlayer(breacher.transform.position + Vector3.up * 4.4f);
        yield return WaitRam(breacher, "Preparing");
        TestContext.WriteLine($"Breacher: warned {warnedDistance:0.00} m, observed dash {travel.magnitude:0.00} m; sideways dodge safe, recovery stationary and vulnerable, another approach required.");
        ClearFixtures(); MovePlayer(Vector2.zero); lab.SetPlayerInvincible(true);

        // A real weapon projectile must cancel a preparing actor, not just reduce a mock HP value.
        breacher = Spawn("p_Enemy_Breacher", new Vector2(4.4f, 0));
        yield return WaitRam(breacher, "Preparing");
        var sniper = lab.Station.Modules.OfType<EnemyOrbitalLabWeapon>().First(g => g.Kind == OrbitalModuleKind.Sniper);
        Aim(sniper, breacher.transform.position); Assert.That(sniper.FireLabExtraShot(), Is.True);
        ((ProductionOrbitalCombatAdapter)lab.Station.Combat).Tick(.08f); yield return null; yield return null;
        Assert.That(breacher == null || breacher.GetComponent<EnemyHealth>().IsDead, Is.True, "Existing orbital sniper destroys the actor before it can dash.");

        ClearFixtures(); lab.SetEnemiesInvincible(true); lab.SetTargetEnemyDensity(50); lab.DensitySpawnRate = 40;
        lab.Station.enabled = true; playerBody.constraints = constraints;
        yield return CoreTestSupport.Await(() => lab.AliveEnemies == 50);
        Assert.That(lab.LabEnemies.Any(e => e != null && e.GetComponent<EnemyChaseMovement>() is { IsProductionTacticalEnemy: true }), Is.True);
        yield return CoreTestSupport.Await(() => lab.LabEnemies.Any(e => e != null && e.GetComponent<EnemyChaseMovement>() is { IsProductionTacticalEnemy: true } m && Phase(m) == "Preparing"));
        lab.SetPaused(true); yield return Capture("lab-flank-and-ram");
        Assert.That(lab.InterceptionActive, Is.False, "Laser incident is disabled throughout this independent enemy check.");
        Assert.That(lab.Station.ValidateState(out _), Is.True);
        TestContext.WriteLine("Dense lab: 50 live mixed existing enemies, tactical variants and real manual orbital construction; no laser incident.");
    }
}
#endif
