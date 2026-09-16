#if UNITY_EDITOR
using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using Subject42.Combat.OrbitalStation;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

public sealed partial class Subject42OrbitalOperationsTests
{

    [Category("Extended")]
    [Test]
    public void CommandApi_OwnsAllOrbitalStateMutations()
    {
        var state = OrbitalRunState.CreateDefault(42, customPaths: true);
        var pendingRing = state.NextPendingRing;
        Assert.That(state.IsPending(pendingRing), Is.True);
        Assert.That(state.TryInstallModule(OrbitalModuleKind.LaserSword,
            pendingRing.StableRingId, 0, out _, out string error), Is.False,
            "default pistol already occupies mount 0");

        Assert.That(CustomOrbitPath.TryRestore(new[]
        {
            new Vector2(0f, 1f), new Vector2(1f, 0f),
            new Vector2(0f, -1f), new Vector2(-1f, 0f)
        }, out var firstPath), Is.True);
        Assert.That(state.TrySetCustomPath(pendingRing.StableRingId,
            firstPath, 3f, out error), Is.True, error);
        Assert.That(state.FindRing(pendingRing.StableRingId).CustomPath,
            Is.EqualTo(firstPath.CapturePoints()));

        Assert.That(state.TryAddMount(1, out error), Is.True, error);
        Assert.That(state.TryInstallModule(OrbitalModuleKind.ImpulseGun,
            1, 1, out var impulse, out error), Is.True, error);
        Assert.That(state.TryUpgradeRingSpeed(1, out error), Is.True, error);
        Assert.That(state.TryUpgradeRingDamage(1, out error), Is.True, error);
        Assert.That(state.TryUpgradeModuleDamage(impulse.StableModuleId, out error), Is.True, error);
        Assert.That(state.TryUpgradeCore(out error), Is.True, error);

        string beforeInvalid = Snapshot(state);
        Assert.That(state.TryInstallModule(OrbitalModuleKind.Pistol, 999, 0,
            out _, out error), Is.False);
        Assert.That(error, Is.Not.Null.And.Not.Empty);
        Assert.That(Snapshot(state), Is.EqualTo(beforeInvalid));
    }

    [Category("Extended")]
    [UnityTest]
    public IEnumerator ImpulseFiveAttacksDamageAndMoveBasicEnemyWithoutVisualAnimator()
    {
        yield return new EnterPlayMode();
        using (var f = new Fixture())
        {
            bool frozen = EnemyDebugAiFreeze.IsFrozen;
            SimulationMode2D simulation = Physics2D.simulationMode;
            GameObject enemyObject = null;
            try
            {
                // Keep the fixture outside any enemies in the currently open lab.
                f.Player.transform.position = new Vector3(10000f, 10000f);
                foreach (var existing in f.Station.Modules.ToArray())
                    Assert.That(f.Station.RemoveModule(existing.StableModuleId), Is.True);
                Assert.That(f.Station.InstallModule(OrbitalModuleKind.ImpulseGun, 1, 0, out _), Is.True);
                var module = f.Station.Modules.Single();
                var view = ((GameObject)Get(module, "Visual")).GetComponent<OrbitalModuleView>();
                Vector3 position = view.Body.localPosition, scale = view.Body.localScale;
                Quaternion rotation = view.Body.localRotation;
                foreach (var animator in view.GetComponentsInChildren<Animator>(true))
                    Assert.That(animator.enabled, Is.False, "Gameplay must not require PistolShoot1");

                var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                    "Assets/_Project/prefabs/Enemies/p_Enemy_default.prefab");
                enemyObject = Object.Instantiate(prefab,
                    f.Player.transform.position + Vector3.right * 3f, Quaternion.identity);
                var enemy = enemyObject.GetComponent<EnemyHealth>();
                var movement = enemyObject.GetComponent<EnemyChaseMovement>();
                var body = enemyObject.GetComponent<Rigidbody2D>();
                movement.enabled = false; // Invoke the real FixedUpdate deterministically.
                yield return null; // EnemyHealth.Start must finish before measuring HP.
                Set(movement, "player", f.Player.transform);
                EnemyDebugAiFreeze.SetFrozen(true); // Freeze chase, not the knockback motion.
                Physics2D.simulationMode = SimulationMode2D.Script;
                float damage = OrbitalImpulseGunModule.BaseDamage * module.CurrentMount.Ring.PowerMultiplier *
                    f.Station.GetModuleMetaDamageMultiplier(module.Kind) *
                    f.Station.GetModuleDamageMultiplier(module.StableModuleId);
                enemy.SetRuntimeMaxHealth(damage * 6f); // Fixture survives five hits at any saved meta level.
                float initialHp = enemy.CurrentHealth;
                int hits = 0, shots = 0, knockbacks = 0;
                f.Station.Combat.Hit += (target, amount) => { if (target == enemy) hits++; };
                Vector2 initialPosition = body.position;
                for (int i = 0; i < 5; i++)
                {
                    Assert.That(f.Station.Combat.FindNearest(module.WorldPosition, 5f), Is.SameAs(enemy));
                    module.Tick(1.25f);
                    if ((float)Get(module, "Cooldown") == 1.25f) shots++;
                    Vector2 knockback = (Vector2)Get(movement, "knockbackVelocity");
                    Assert.That(knockback.magnitude,
                        Is.EqualTo(4.5f * module.CurrentMount.Ring.PowerMultiplier).Within(.0001f));
                    Assert.That(Vector2.Dot(knockback, body.position - (Vector2)f.Player.transform.position), Is.GreaterThan(0));
                    knockbacks++;
                    Vector2 before = body.position;
                    Call(movement, "FixedUpdate");
                    Physics2D.Simulate(Time.fixedDeltaTime);
                    Assert.That(Vector2.Dot(body.position - before,
                        before - (Vector2)f.Player.transform.position), Is.GreaterThan(0),
                        "A hit must move the real Rigidbody away, not just call AddForce");
                    module.Tick(0f);
                    Assert.That(hits, Is.EqualTo(i + 1), "No extra fire before cooldown");
                }
                Assert.That(shots, Is.EqualTo(5));
                Assert.That(hits, Is.EqualTo(5));
                Assert.That(knockbacks, Is.EqualTo(5));
                Assert.That(enemy.CurrentHealth, Is.EqualTo(initialHp - 5f * damage).Within(.001f));
                var projectiles = (IList)Get(f.Station.Combat, "projectiles");
                Assert.That(projectiles.Cast<object>().Any(p => (bool)Get(p, "Active")), Is.False,
                    "Impulse is a direct hit, not a projectile attack");
                yield return new WaitForSecondsRealtime(.25f);
                module.Tick(0f);
                Assert.That(view.Body.localPosition, Is.EqualTo(position));
                Assert.That(view.Body.localRotation, Is.EqualTo(rotation));
                Assert.That(view.Body.localScale, Is.EqualTo(scale));
                TestContext.WriteLine($"Impulse: shots={shots}, hits={hits}, HP={initialHp}->{enemy.CurrentHealth}, " +
                    $"knockbacks={knockbacks}, outward displacement={Vector2.Distance(initialPosition, body.position)}");
            }
            finally
            {
                Physics2D.simulationMode = simulation;
                EnemyDebugAiFreeze.SetFrozen(frozen);
                if (enemyObject != null) Object.Destroy(enemyObject);
            }
        }
        yield return null;
        yield return new ExitPlayMode();
    }

    [Category("Extended")]
    [UnityTest]
    public IEnumerator CapacityExpansionPreservesBuiltMountsAndCombat()
    {
        yield return new EnterPlayMode();
        using (var f = new Fixture())
        {
            var ring = f.Station.Rings.Single();
            var firstMount = ring.Mounts[0];
            var module = f.Station.Modules.Single();
            var target = new GameObject("Capacity must not fire a bonus wave");
            target.transform.position = new Vector3(4, 0, 0);
            target.AddComponent<EnemyHealth>();
            Set(module, "Cooldown", .37f);
            var projectiles = (IList)Get(f.Station.Combat, "projectiles");
            for (int cap = 4; cap <= 6; cap++)
            {
                Assert.That(f.Station.UpgradeRingCapacity(1), Is.True);
                Assert.That(ring.MountCapacity, Is.EqualTo(cap));
                Assert.That(ring.Mounts.Count, Is.EqualTo(cap - 1));
                Assert.That(ring.Mounts[0], Is.SameAs(firstMount));
                Assert.That(Get(module, "Cooldown"), Is.EqualTo(.37f));
                Assert.That(projectiles.Cast<object>().Any(p => (bool)Get(p, "Active")), Is.False);
                Assert.That(f.Station.UpgradeRingCapacity(1), Is.False);
                Assert.That(f.Station.AddMount(1, out _), Is.True);
                Assert.That(ring.Mounts.Count, Is.EqualTo(cap));
            }
            Assert.That(f.Station.AddMount(1, out _), Is.False);
            Assert.That(f.Station.UpgradeRingCapacity(1), Is.False);
            Assert.That(f.Station.SimulateSectorRestore(), Is.True);
            Assert.That(f.Station.Rings[0].Mounts.Count, Is.EqualTo(6));
            Assert.That(f.Station.Rings[0].MountCapacity, Is.EqualTo(6));
            Object.Destroy(target);
        }
        yield return new ExitPlayMode();
    }

    [Category("Extended")]
    [UnityTest]
    public IEnumerator GoldenFlow_TransientCombatContinuity()
    {
        yield return new EnterPlayMode();
        yield return Golden();
        yield return new ExitPlayMode();
    }

    [Category("Extended")]
    [UnityTest]
    public IEnumerator ModuleFactory_CoversCurrentProductionModules()
    {
        yield return new EnterPlayMode();
        using (var f = new Fixture())
        {
            foreach (OrbitalModuleKind kind in Enum.GetValues(typeof(OrbitalModuleKind)))
            {
                var moduleState = new OrbitalModuleState
                {
                    StableModuleId = 100 + (int)kind,
                    ModuleType = kind,
                    StableRingId = 1,
                    MountIndex = 0
                };
                Assert.That(OrbitalModuleRuntimeFactory.TryCreate(f.Station,
                    moduleState, out var module, out string error), Is.True, error);
                Assert.That(module.Kind, Is.EqualTo(kind));
                module.Teardown();
            }
        }
        yield return new ExitPlayMode();
    }

    [Category("Extended")]
    [UnityTest]
    public IEnumerator CommittedState_SurvivesVisualFailures()
    {
        yield return new EnterPlayMode();
        yield return Failures();
        yield return new ExitPlayMode();
    }

    [Category("Extended")]
    [UnityTest]
    public IEnumerator RewardsAndDrag_StillCompleteAndCancel()
    {
        yield return new EnterPlayMode();
        yield return Rewards();
        yield return new ExitPlayMode();
    }

    [Category("Extended")]
    [UnityTest]
    public IEnumerator LinkInstallAndCancel_PreserveExistingSemantics()
    {
        yield return new EnterPlayMode();
        yield return Links();
        yield return new ExitPlayMode();
    }

    [Category("Extended")]
    [UnityTest]
    public IEnumerator Pass3_TransactionsAndArbitration()
    {
        yield return new EnterPlayMode();
        yield return Transactions();
        yield return new ExitPlayMode();
    }
}
#endif
