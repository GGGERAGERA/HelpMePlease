using System.Collections;
using System.Linq;
using NUnit.Framework;
using Subject42.Combat.OrbitalStation;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.TestTools;

public sealed class Subject42OrbitalOperationsTests
{
    [SetUp] public void PreservePreferences() => CoreTestSupport.PreservePreferences();
    [UnityTearDown] public IEnumerator Cleanup() => CoreTestSupport.CleanupPlayMode();

    [Category("Core"), UnityTest]
    public IEnumerator ProductionSniperBypassesFrontalPlateButStopsAtFirstBody()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
        yield return new EnterPlayMode();
        yield return VerifySniperShieldAndBodyLimit();
    }

    // Create all runtime locals after domain reload. No station, player, spawner or hazard
    // exists in this scene; only the explicitly ticked adapter can deal damage.
    private static IEnumerator VerifySniperShieldAndBodyLimit()
    {
        GameObject root = null, first = null, second = null;
        ProductionOrbitalCombatAdapter adapter = null;
        try
        {
            Assert.That(EnemyHealth.ActiveInstances.Count, Is.Zero, "Fixture must start without enemies.");
            Assert.That(ProductionOrbitalCombatAdapter.LabInterceptor, Is.Null);
            root = new GameObject("Sniper shield contract");
            first = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/_Project/prefabs/Enemies/ShieldBearer.prefab"));
            second = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/_Project/prefabs/Enemies/p_Enemy_classic.prefab"));
            foreach (var enemy in new[] { first, second })
            {
                foreach (var movement in enemy.GetComponentsInChildren<EnemyMovement>()) movement.enabled = false;
                foreach (var body in enemy.GetComponentsInChildren<Rigidbody2D>())
                {
                    body.bodyType = RigidbodyType2D.Kinematic;
                    body.linearVelocity = Vector2.zero;
                    body.angularVelocity = 0f;
                }
            }
            yield return null; // EnemyHealth.Start must finish before setting baseline HP.
            var shield = first.GetComponent<EnemyDirectionalShield>();
            var firstHealth = first.GetComponent<EnemyHealth>();
            var secondHealth = second.GetComponent<EnemyHealth>();
            root.transform.position = shield.Center + Vector2.right * 3f;
            shield.Bind(root.transform);
            Vector2 direction = -shield.Forward;
            Vector2 origin = shield.Center + shield.Forward * 3f;
            var secondBody = second.GetComponent<Collider2D>();
            Assert.That(secondBody, Is.Not.Null);
            second.transform.position += (Vector3)(shield.Center + direction * 2f - (Vector2)secondBody.bounds.center);
            Physics2D.SyncTransforms();

            var sniper = OrbitalPresentationConfig.Active.GetWeaponSettings(OrbitalModuleKind.Sniper);
            var pistol = OrbitalPresentationConfig.Active.GetWeaponSettings(OrbitalModuleKind.Pistol);
            Assert.That(sniper.BypassShields, Is.True);
            Assert.That(sniper.BodyPierces, Is.EqualTo(1));
            Assert.That(pistol.BypassShields, Is.False);
            Assert.That(shield.CanBlockProjectile(direction), Is.True);
            // Observe actual colliders independently of the adapter's swept-shape query.
            var hits = Physics2D.RaycastAll(origin, direction, Mathf.Min(sniper.Range, pistol.Range))
                .OrderBy(hit => hit.distance).ToArray();
            var plate = hits.Where(hit => hit.collider == shield.ShieldCollider).ToArray();
            var firstBodies = hits.Where(hit => hit.collider != shield.ShieldCollider &&
                hit.collider.GetComponentInParent<EnemyHealth>() == firstHealth).ToArray();
            var secondBodies = hits.Where(hit => hit.collider.GetComponentInParent<EnemyHealth>() == secondHealth).ToArray();
            Assert.That(plate.Length, Is.GreaterThan(0), "Ray must cross frontal plate.");
            Assert.That(firstBodies.Length, Is.GreaterThan(0), "Ray must cross first body.");
            Assert.That(secondBodies.Length, Is.GreaterThan(0), "Second body must be reachable by both weapons.");
            Assert.That(plate[0].distance, Is.LessThan(firstBodies[0].distance));
            Assert.That(firstBodies.Max(hit => hit.distance), Is.LessThan(secondBodies[0].distance));
            Assert.That(secondBodies.Max(hit => hit.distance), Is.LessThan(Mathf.Min(sniper.Range, pistol.Range) - .5f));

            adapter = new ProductionOrbitalCombatAdapter(root.transform, null);
            int firstHits = 0, secondHits = 0;
            firstHealth.HitFeedback += (_, _, _) => firstHits++;
            secondHealth.HitFeedback += (_, _, _) => secondHits++;
            foreach (float step in new[] { 1f / 30f, 1f / 120f })
            foreach (var settings in new[] { sniper, pistol })
            {
                adapter.DebugClearProjectiles();
                firstHealth.SetRuntimeMaxHealth(1000f);
                secondHealth.SetRuntimeMaxHealth(1000f);
                firstHits = secondHits = 0;
                int blocks = shield.Blocks;
                adapter.SpawnWeaponProjectile(origin, firstHealth, direction, settings, null, settings.Damage);
                Assert.That(adapter.LabActiveProjectileCount, Is.EqualTo(1));
                int frames = Mathf.CeilToInt(settings.Range / settings.ProjectileSpeed / step) + 2;
                for (int frame = 0; frame < frames; frame++) adapter.Tick(step);
                string context = $"{settings.Kind}, dt={step}";
                Assert.That(adapter.LabActiveProjectileCount, Is.Zero, context);
                bool bypass = settings == sniper;
                Assert.That(firstHealth.CurrentHealth, Is.EqualTo(1000f - (bypass ? settings.Damage : 0f)).Within(.001f), context);
                Assert.That(secondHealth.CurrentHealth, Is.EqualTo(1000f), context);
                Assert.That(firstHits, Is.EqualTo(bypass ? 1 : 0), context);
                Assert.That(secondHits, Is.Zero, context);
                Assert.That(shield.Blocks, Is.EqualTo(blocks + (bypass ? 0 : 1)), context);
            }
        }
        finally
        {
            adapter?.DebugClearProjectiles();
            adapter?.Teardown();
            if (first != null) Object.DestroyImmediate(first);
            if (second != null) Object.DestroyImmediate(second);
            if (root != null) Object.DestroyImmediate(root);
        }
    }

    [Category("Core"), Test]
    public void RingMountAndModuleTransactionsCommitOnceAndRejectOccupiedTargets()
    {
        var state = OrbitalRunState.CreateDefault(1);
        int revision = state.Revision;
        Assert.That(state.TryAddRing(out var ring, out _), Is.True);
        Assert.That(state.AddMount(ring.StableRingId, out _), Is.True);
        Assert.That(ring.MountCount, Is.EqualTo(2));
        Assert.That(state.InstallModule(OrbitalModuleKind.ArcEmitter,
            ring.StableRingId, 0, out var module), Is.True);
        Assert.That(state.Revision, Is.EqualTo(revision + 3));
        int moduleId = module.StableModuleId;
        string before = JsonUtility.ToJson(state);
        Assert.That(state.MoveModule(moduleId, 1, 0, out _), Is.False);
        Assert.That(state.InstallModule(OrbitalModuleKind.Pistol,
            ring.StableRingId, 0, out _), Is.False);
        Assert.That(JsonUtility.ToJson(state), Is.EqualTo(before));
        Assert.That(state.MoveModule(moduleId, ring.StableRingId, 1, out _), Is.True);
        Assert.That(state.FindModule(moduleId), Is.SameAs(module));
        Assert.That(state.IsMountFree(ring.StableRingId, 0), Is.True);
        Assert.That(module.MountIndex, Is.EqualTo(1));
        Assert.That(state.RemoveModule(moduleId), Is.True);
        Assert.That(state.IsMountFree(ring.StableRingId, 1), Is.True);
        Assert.That(state.NextStableModuleId, Is.EqualTo(moduleId + 1));
        Assert.That(state.Revision, Is.EqualTo(revision + 5));
        Assert.That(state.Validate(out string error), Is.True, error);
    }

    [Category("Core"), Test]
    public void LinkPairCommitsAtomicallyPreservesPartnerOnMoveAndUpgradesCore()
    {
        var state = OrbitalRunState.CreateDefault(2);
        Assert.That(state.AddMount(1, out _), Is.True);
        Assert.That(state.AddMount(1, out _), Is.True);
        var ring = state.AddRing();
        Assert.That(ring, Is.Not.Null);
        string before = JsonUtility.ToJson(state);
        Assert.That(state.InstallLinkPair(1, 1, 1, 1, out _, out _, out _), Is.False);
        Assert.That(JsonUtility.ToJson(state), Is.EqualTo(before));
        int revision = state.Revision, nextId = state.NextStableModuleId;
        Assert.That(state.InstallLinkPair(1, 1, 1, 2, out var a, out var b, out _), Is.True);
        Assert.That(state.Revision, Is.EqualTo(revision + 1));
        Assert.That(state.NextStableModuleId, Is.EqualTo(nextId + 2));
        Assert.That(state.FindLinkPartner(a.StableModuleId), Is.EqualTo(b.StableModuleId));
        Assert.That(state.MoveModule(a.StableModuleId, ring.StableRingId, 0, out _), Is.True);
        Assert.That(state.FindLinkPartner(a.StableModuleId), Is.EqualTo(b.StableModuleId));
        Assert.That(state.UpgradeCore(), Is.True);
        Assert.That(state.UpgradeLinkMatrix(), Is.True);
        Assert.That(state.CoreState.Level, Is.EqualTo(1));
        Assert.That(state.CoreState.LinkMatrixUpgradeLevel, Is.EqualTo(1));
        Assert.That(state.RemoveModule(a.StableModuleId), Is.True);
        Assert.That(state.FindLinkPartner(b.StableModuleId), Is.Zero);
        Assert.That(state.Validate(out string error), Is.True, error);
    }

    [Category("Core"), Test]
    public void VikaCustomPathsFinalizeMultipleRingsInCreationOrder()
    {
        var state = OrbitalRunState.CreateDefault(3, customPaths: true);
        var first = state.NextPendingRing;
        Assert.That(state.TryAddRing(out var second, out _), Is.True);
        Assert.That(CustomOrbitPath.TryRestore(new[] {
            new Vector2(0f, .8f), new Vector2(.8f, 0f),
            new Vector2(0f, -.8f), new Vector2(-.8f, 0f)
        }, out var firstPath), Is.True);
        Assert.That(CustomOrbitPath.TryRestore(new[] {
            new Vector2(0f, 1.4f), new Vector2(1.2f, .2f),
            new Vector2(.1f, -1.1f), new Vector2(-1.3f, -.1f)
        }, out var secondPath), Is.True);
        Assert.That(state.PendingRingCount, Is.EqualTo(2));
        string before = JsonUtility.ToJson(state);
        Assert.That(state.TrySetCustomPath(second.StableRingId, secondPath, 2f, out _), Is.False);
        Assert.That(JsonUtility.ToJson(state), Is.EqualTo(before));
        Assert.That(state.TrySetCustomPath(first.StableRingId, firstPath, 2f, out _), Is.True);
        Assert.That(state.NextPendingRing, Is.SameAs(second));
        Assert.That(state.TrySetCustomPath(second.StableRingId, secondPath, 2f, out _), Is.True);
        Assert.That(first.CustomPath, Is.EqualTo(firstPath.CapturePoints()));
        Assert.That(second.CustomPath, Is.EqualTo(secondPath.CapturePoints()));
        Assert.That(state.PendingRingCount, Is.Zero);
        Assert.That(state.NextPendingRing, Is.Null);
        Assert.That(state.Validate(out string error), Is.True, error);
    }

    [Category("Core"), UnityTest]
    public IEnumerator SectorRestorePreservesCompleteOrbitalStateAndRebuildsOccupancy()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
        ProductionSceneCompositionAuthoring.EnsureScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        yield return new EnterPlayMode();
        var manager = RunStateManager.EnsureExists();
        var stage = ScriptableObject.CreateInstance<StageProfileData>();
        var rule = ScriptableObject.CreateInstance<WorldRuleData>();
        var anomaly = ScriptableObject.CreateInstance<LocalAnomalyData>();
        var player = new GameObject("Orbital restore test player");
        try
        {
            manager.BeginNewRun(null, null, stage, rule, anomaly);
            var state = manager.OrbitalStationState;
            var second = state.AddRing();
            Assert.That(second, Is.Not.Null);
            Assert.That(state.AddMount(1, out _), Is.True);
            Assert.That(state.InstallModule(OrbitalModuleKind.ArcEmitter,
                second.StableRingId, 0, out var arc), Is.True);
            Assert.That(state.MoveModule(arc.StableModuleId, 1, 1, out _), Is.True);
            Assert.That(state.UpgradeModuleDamage(arc.StableModuleId), Is.True);
            Assert.That(state.UpgradeRingPower(1), Is.True);
            Assert.That(state.UpgradeRingSpeed(second.StableRingId), Is.True);
            Assert.That(state.UpgradeCore(), Is.True);
            Assert.That(state.AddMount(second.StableRingId, out _), Is.True);
            Assert.That(state.InstallLinkPair(second.StableRingId, 0, second.StableRingId, 1, out _, out _, out _), Is.True);
            Assert.That(state.UpgradeLinkMatrix(), Is.True);
            state.SetPhase(1, 17.5f);
            state.SetPhase(second.StableRingId, 81.25f);
            string before = JsonUtility.ToJson(state);
            var station = OrbitalStationRuntime.CreateFromState(player, state);
            Assert.That(station, Is.Not.Null);
            Assert.That(station.IsInitialized, Is.True);
            Assert.That(station.SimulateSectorRestore(), Is.True);
            Assert.That(station.CaptureState(), Is.SameAs(state));
            Assert.That(manager.OrbitalStationState, Is.SameAs(state));
            Assert.That(JsonUtility.ToJson(state), Is.EqualTo(before));
            Assert.That(station.Rings.Count, Is.EqualTo(state.Rings.Count));
            Assert.That(station.Modules.Count, Is.EqualTo(state.Modules.Count));
            foreach (var module in state.Modules)
            {
                var restored = station.Modules.Single(m => m.StableModuleId == module.StableModuleId);
                Assert.That(restored.Kind, Is.EqualTo(module.ModuleType));
                Assert.That(restored.CurrentMount.Ring.RingId, Is.EqualTo(module.StableRingId));
                Assert.That(restored.CurrentMount.MountIndex, Is.EqualTo(module.MountIndex));
            }
        }
        finally
        {
            Object.DestroyImmediate(player);
            Object.DestroyImmediate(manager.gameObject);
            Object.DestroyImmediate(stage);
            Object.DestroyImmediate(rule);
            Object.DestroyImmediate(anomaly);
        }

    }
}
