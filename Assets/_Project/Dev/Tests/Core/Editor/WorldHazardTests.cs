using System.Collections;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

public sealed class WorldHazardTests
{
    [SetUp] public void Setup() => CoreTestSupport.PreservePreferences();
    [UnityTearDown] public IEnumerator Cleanup() => CoreTestSupport.CleanupPlayMode();

    [UnityTest, Timeout(90000)]
    public IEnumerator BeamDamageMatchesTelegraphStripAndRoundEnds()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
        yield return new EnterPlayMode();
        // Fresh health per case; no movement, physics, enemies, regeneration or other hazards.
        foreach (int positionCase in new[] { 0, 1, 2, 3 })
            yield return VerifyBeamPosition(positionCase);
    }

    private static IEnumerator VerifyBeamPosition(int positionCase)
    {
        var player = new GameObject("BEAM isolated health");
        var health = player.AddComponent<PlayerHealth>();
        // Remove only this isolated health fixture's cooldown so repeated Active damage
        // cannot hide behind invulnerability. Production assets and hazard cadence stay intact.
        var healthData = new SerializedObject(health);
        healthData.FindProperty("invulnerabilityTime").floatValue = 0f;
        healthData.ApplyModifiedPropertiesWithoutUndo();
        PlayerRuntimeReference.Bind(player);
        var owner = new GameObject("BEAM damage fixture");
        var hazard = CreateBeam(owner);
        var view = owner.GetComponentInChildren<AnomalyBeamView>();
        int hits = 0;
        health.DamageTaken += () => hits++;
        try
        {
            yield return null; // PlayerHealth.Start initializes HP.
            Assert.That(TutorialController.IsActive, Is.False, "No tutorial damage reduction in this fixture.");
            Assert.That(health.DebugInvulnerable, Is.False);
            float initial = health.CurrentHealth;
            Assert.That(initial, Is.GreaterThan(Config().BeamPlayerDamage));
            yield return CoreTestSupport.Await(() => view.State == AnomalyBeamView.BeamState.Telegraph);
            ReadFootprint(view, out var start, out var end, out float radius);
            Vector2 axis = (end - start).normalized;
            Vector2 normal = new Vector2(-axis.y, axis.x);
            Vector2 midpoint = (start + end) * .5f;
            Vector2 target = positionCase == 0 ? midpoint :
                positionCase == 1 ? midpoint + normal * (radius + .25f) :
                positionCase == 2 ? end + axis * (radius + .25f) :
                end + axis * (radius * .5f);
            bool shouldHit = positionCase == 0 || positionCase == 3;
            // Independent signed axial/lateral coordinates from the rendered footprint.
            float along = Vector2.Dot(target - midpoint, axis);
            float across = Vector2.Dot(target - midpoint, normal);
            float beyondEnd = Mathf.Max(0f, Mathf.Abs(along) - Vector2.Distance(start, end) * .5f);
            float distance = Mathf.Sqrt(across * across + beyondEnd * beyondEnd);
            Assert.That(distance < radius, Is.EqualTo(shouldHit), "Fixture must straddle the visible capsule.");
            player.transform.position = target;
            while (view.State == AnomalyBeamView.BeamState.Telegraph)
            {
                Assert.That(health.CurrentHealth, Is.EqualTo(initial), "Telegraph must never damage HP.");
                Assert.That(hits, Is.Zero);
                yield return null;
            }
            Assert.That(view.State, Is.EqualTo(AnomalyBeamView.BeamState.Active));
            Assert.That(health.CurrentHealth, Is.EqualTo(initial - (shouldHit ? Config().BeamPlayerDamage : 0f)).Within(.001f),
                "Active HP must match visible strip/caps; case=" + positionCase);
            Assert.That(hits, Is.EqualTo(shouldHit ? 1 : 0));
            // Further Active damage is observable with the fixture's zero cooldown.
            while (view.State == AnomalyBeamView.BeamState.Active) yield return null;
            Assert.That(health.CurrentHealth, Is.EqualTo(initial - (shouldHit ? Config().BeamPlayerDamage : 0f)).Within(.001f));
            Assert.That(hits, Is.EqualTo(shouldHit ? 1 : 0));
            if (!shouldHit)
            {
                // Same health and same hazard: a subsequent real hit proves negative controls
                // were not masked by invulnerability or a missing runtime player binding.
                yield return CoreTestSupport.Await(() => view.State == AnomalyBeamView.BeamState.Telegraph);
                ReadFootprint(view, out start, out end, out radius);
                player.transform.position = (start + end) * .5f;
                yield return CoreTestSupport.Await(() => view.State == AnomalyBeamView.BeamState.Active);
                Assert.That(hits, Is.EqualTo(1), "Negative case requires a working positive control.");
                Assert.That(health.CurrentHealth, Is.EqualTo(initial - Config().BeamPlayerDamage).Within(.001f));
            }
        }
        finally
        {
            if (hazard != null) hazard.GetType().GetMethod("StopHazard").Invoke(hazard, null);
            PlayerRuntimeReference.Clear();
            Object.Destroy(owner);
            Object.Destroy(player);
        }
        yield return null;
        Assert.That(owner == null && player == null && view == null, Is.True, "Owned fixture objects must be removed.");
    }

    [UnityTest, Timeout(30000)]
    public IEnumerator BeamStoppedDuringTelegraphRemovesVisualAndCannotDamage()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
        yield return new EnterPlayMode();
        yield return VerifyBeamStoppedDuringTelegraph();
    }

    private static IEnumerator VerifyBeamStoppedDuringTelegraph()
    {
        // Create lambda-captured locals after the EnterPlayMode domain reload.
        var player = new GameObject("BEAM stop health");
        GameObject owner = null;
        try
        {
            Assert.That(player != null, Is.True, "Stop fixture: player object must exist before adding health.");
            var health = player.AddComponent<PlayerHealth>();
            Assert.That(health != null, Is.True, "Stop fixture: PlayerHealth must survive AddComponent/Awake.");
            PlayerRuntimeReference.Bind(player);
            owner = new GameObject("BEAM stop fixture");
            var hazard = CreateBeam(owner);
            Assert.That(owner != null, Is.True, "Stop fixture: owner must survive hazard initialization.");
            Assert.That(hazard != null, Is.True, "Stop fixture: initialized hazard must exist.");
            Assert.That(hazard.isActiveAndEnabled, Is.True, "Stop fixture: hazard must be running before StopHazard.");
            var views = owner.GetComponentsInChildren<AnomalyBeamView>(true);
            Assert.That(views.Length, Is.EqualTo(1), "Stop fixture: Initialize must create exactly one owned view.");
            var view = views[0];
            Assert.That(view != null && view.isActiveAndEnabled, Is.True, "Stop fixture: owned view must be active and enabled.");
            int hits = 0;
            health.DamageTaken += () => hits++;
            yield return null; // PlayerHealth.Start must initialize HP before the cancellation control.
            Assert.That(health != null, Is.True, "Stop fixture: health must survive its first frame.");
            float initial = health.CurrentHealth;
            Assert.That(initial, Is.GreaterThan(Config().BeamPlayerDamage), "Stop fixture requires initialized, live HP.");
            yield return CoreTestSupport.Await(() =>
            {
                Assert.That(owner != null && hazard != null, Is.True, "Stop fixture: owner/hazard disappeared before Telegraph.");
                Assert.That(view != null && view.isActiveAndEnabled, Is.True, "Stop fixture: view disappeared or was disabled before Telegraph.");
                Assert.That(health != null, Is.True, "Stop fixture: health disappeared before Telegraph.");
                Assert.That(health.CurrentHealth, Is.EqualTo(initial), "Telegraph preparation must not damage HP.");
                Assert.That(hits, Is.Zero);
                return view.State == AnomalyBeamView.BeamState.Telegraph;
            });
            ReadFootprint(view, out var start, out var end, out _);
            player.transform.position = (start + end) * .5f;
            Assert.That(health.DebugInvulnerable, Is.False);
            Assert.That(TutorialController.IsActive, Is.False);
            var stop = hazard.GetType().GetMethod("StopHazard");
            Assert.That(stop, Is.Not.Null, "Stop fixture: production StopHazard must be callable.");
            stop.Invoke(hazard, null);
            Assert.That(view.gameObject.activeSelf, Is.False, "Stop hides the owned visual immediately.");
            yield return null;
            Assert.That(view == null && hazard == null, Is.True, "Stop destroys its component and visual.");
            // Beyond the original warning deadline and normal health invulnerability.
            yield return new WaitForSeconds(1f);
            Assert.That(hits, Is.Zero, "Stopped warning must not deliver a later hit.");
            Assert.That(health.CurrentHealth, Is.EqualTo(initial));
            Assert.That(health.TakeDamage(1f, Vector2.zero), Is.True, "Stop assertion must not be hidden by invulnerability.");
            Assert.That(health.CurrentHealth, Is.EqualTo(initial - 1f).Within(.001f), "Positive control must actually change HP.");
            Assert.That(hits, Is.EqualTo(1), "Only the explicit positive control may deliver a hit after Stop.");
            Assert.That(owner.GetComponentsInChildren<AnomalyBeamView>(true), Is.Empty);
        }
        finally
        {
            PlayerRuntimeReference.Clear();
            Object.Destroy(owner);
            Object.Destroy(player);
        }
    }

    private static ExplorationSectorConfig Config() => AssetDatabase.LoadAssetAtPath<ExplorationSectorConfig>(
        "Assets/_Project/Data/World/ExplorationSectorConfig.asset");

    [UnityTest, Timeout(60000)]
    public IEnumerator ElectricDamageFollowsCurrentPairInsteadOfPreviousLink()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
        yield return new EnterPlayMode();
        // Two consecutive pair transitions, each with its own live HP controls.
        yield return VerifyElectricPairChange(0);
        yield return VerifyElectricPairChange(1);
    }

    private static IEnumerator VerifyElectricPairChange(int skipPairs)
    {
        // All captured runtime locals are created after domain reload.
        var player = new GameObject("ELECTRIC isolated health");
        GameObject owner = null;
        MonoBehaviour hazard = null;
        AnomalyBeamView view = null;
        try
        {
            var health = player.AddComponent<PlayerHealth>();
            var healthData = new SerializedObject(health);
            healthData.FindProperty("invulnerabilityTime").floatValue = 0f;
            healthData.ApplyModifiedPropertiesWithoutUndo();
            Assert.That(health.RunUpgradeRegenerationPerSecond, Is.Zero);
            PlayerRuntimeReference.Bind(player);
            player.transform.position = new Vector2(1000f, 1000f);
            Assert.That(Object.FindObjectsByType<PlayerHealth>(FindObjectsSortMode.None).Length, Is.EqualTo(1));
            Assert.That(Object.FindObjectsByType<EnemyHealth>(FindObjectsSortMode.None), Is.Empty);
            var config = Config();
            Assert.That(config, Is.Not.Null);
            Assert.That(config.ElectricVisualPrefab, Is.Not.Null);
            Assert.That(config.ElectricPlayerDamage, Is.GreaterThan(0f));
            owner = new GameObject("ELECTRIC pair change fixture");
            var type = typeof(AnomalyBeamView).Assembly.GetType("ProductionElectricSiteHazard", true);
            hazard = (MonoBehaviour)owner.AddComponent(type);
            type.GetMethod("Initialize").Invoke(hazard, new object[]
                { Vector2.zero, new Vector2(24f, 20f), config.ElectricEnemyDamage,
                    config.ElectricPlayerDamage, config.ElectricVisualPrefab });
            var views = owner.GetComponentsInChildren<AnomalyBeamView>(true);
            Assert.That(views.Length, Is.EqualTo(1));
            view = views[0];
            int hits = 0;
            health.DamageTaken += () => hits++;
            yield return null;
            Assert.That(TutorialController.IsActive, Is.False);
            Assert.That(health.DebugInvulnerable, Is.False);
            float initial = health.CurrentHealth;
            Assert.That(initial, Is.GreaterThan(2f * config.ElectricPlayerDamage));
            for (int i = 0; i < skipPairs; i++)
            {
                yield return CoreTestSupport.Await(() => view.State == AnomalyBeamView.BeamState.Active);
                while (view.State == AnomalyBeamView.BeamState.Active) yield return null;
            }
            Assert.That(hits, Is.Zero, "Warmup outside the site must not damage HP.");
            Vector2 previousStart = default, previousEnd = default;
            float previousRadius = 0f;
            for (int discharge = 0; discharge < 3; discharge++)
            {
                yield return CoreTestSupport.Await(() => view.State == AnomalyBeamView.BeamState.Telegraph);
                ReadFootprint(view, out var start, out var end, out float radius);
                Vector2 target = (start + end) * .5f;
                bool positive = discharge != 1;
                if (discharge > 0)
                    Assert.That(Vector2.Distance(start, previousStart) + Vector2.Distance(end, previousEnd),
                        Is.GreaterThan(.1f), "Consecutive warnings must show different pairs.");
                if (!positive)
                {
                    // Search interior samples of the OBSERVED old link. No private node/pair data.
                    bool found = false;
                    for (int sample = 1; sample < 20; sample++)
                    {
                        var candidate = Vector2.Lerp(previousStart, previousEnd, sample / 20f);
                        if (VisibleCapsuleDistance(candidate, start, end) <= radius + .25f) continue;
                        target = candidate;
                        found = true;
                        break;
                    }
                    Assert.That(found, Is.True, "Geometry prerequisite: no old-link interior sample outside new footprint; transition=" + skipPairs);
                    Assert.That(VisibleCapsuleDistance(target, previousStart, previousEnd), Is.LessThan(previousRadius));
                    Assert.That(VisibleCapsuleDistance(target, start, end), Is.GreaterThan(radius + .25f));
                }
                else
                    Assert.That(VisibleCapsuleDistance(target, start, end), Is.LessThan(radius));
                player.transform.position = target;
                float before = health.CurrentHealth;
                int hitsBefore = hits;
                while (view.State == AnomalyBeamView.BeamState.Telegraph)
                {
                    Assert.That(health.CurrentHealth, Is.EqualTo(before), "ELECTRIC Telegraph must not damage HP.");
                    Assert.That(hits, Is.EqualTo(hitsBefore));
                    yield return null;
                }
                Assert.That(view.State, Is.EqualTo(AnomalyBeamView.BeamState.Active));
                float expected = before - (positive ? config.ElectricPlayerDamage : 0f);
                int expectedHits = hitsBefore + (positive ? 1 : 0);
                Assert.That(health.CurrentHealth, Is.EqualTo(expected).Within(.001f),
                    "ELECTRIC must damage only the current visible link; transition=" + skipPairs + ", discharge=" + discharge);
                Assert.That(hits, Is.EqualTo(expectedHits));
                while (view.State == AnomalyBeamView.BeamState.Active)
                {
                    Assert.That(health.CurrentHealth, Is.EqualTo(expected).Within(.001f), "No repeated Active damage with zero fixture cooldown.");
                    Assert.That(hits, Is.EqualTo(expectedHits));
                    yield return null;
                }
                Assert.That(health.CurrentHealth, Is.EqualTo(expected).Within(.001f));
                Assert.That(hits, Is.EqualTo(expectedHits));
                previousStart = start;
                previousEnd = end;
                previousRadius = radius;
            }
            Assert.That(hits, Is.EqualTo(2), "Old-link negative case requires a subsequent real current-link hit on the same health.");
            Assert.That(health.CurrentHealth, Is.EqualTo(initial - 2f * config.ElectricPlayerDamage).Within(.001f));
        }
        finally
        {
            if (hazard != null) hazard.GetType().GetMethod("StopHazard").Invoke(hazard, null);
            PlayerRuntimeReference.Clear();
            Object.Destroy(owner);
            Object.Destroy(player);
        }
        yield return null;
        Assert.That(owner == null && player == null && view == null && hazard == null, Is.True,
            "ELECTRIC fixture must leave no owned objects for the next transition.");
    }

    private static float VisibleCapsuleDistance(Vector2 point, Vector2 start, Vector2 end)
    {
        // Independent axial/lateral geometry of the rendered capsule, not utility ClosestPoint.
        Vector2 axis = (end - start).normalized;
        Vector2 relative = point - (start + end) * .5f;
        float across = Vector2.Dot(relative, new Vector2(-axis.y, axis.x));
        float beyond = Mathf.Max(0f, Mathf.Abs(Vector2.Dot(relative, axis)) - Vector2.Distance(start, end) * .5f);
        return Mathf.Sqrt(across * across + beyond * beyond);
    }

    private static MonoBehaviour CreateBeam(GameObject owner)
    {
        Assert.That(Object.FindObjectsByType<PlayerHealth>(FindObjectsSortMode.None).Length, Is.EqualTo(1));
        Assert.That(Object.FindObjectsByType<EnemyHealth>(FindObjectsSortMode.None), Is.Empty);
        var config = Config();
        Assert.That(config.BeamVisualPrefab, Is.Not.Null);
        Assert.That(config.BeamPlayerDamage, Is.GreaterThan(0f));
        var type = typeof(AnomalyBeamView).Assembly.GetType("ProductionBeamSiteHazard", true);
        var hazard = (MonoBehaviour)owner.AddComponent(type);
        type.GetMethod("Initialize").Invoke(hazard, new object[]
            { Vector2.zero, new Vector2(24f, 20f), config.BeamEnemyDamage, config.BeamPlayerDamage, config.BeamVisualPrefab });
        return hazard;
    }

    private static void ReadFootprint(AnomalyBeamView view, out Vector2 start, out Vector2 end, out float radius)
    {
        // Observe authored renderer transforms/sizes, not hazard private segment/distance helpers.
        var data = new SerializedObject(view);
        var first = (SpriteRenderer)data.FindProperty("startEmitter").objectReferenceValue;
        var last = (SpriteRenderer)data.FindProperty("endEmitter").objectReferenceValue;
        var footprint = (SpriteRenderer)data.FindProperty("footprint").objectReferenceValue;
        var caps = data.FindProperty("footprintCaps");
        start = first.transform.position;
        end = last.transform.position;
        radius = footprint.size.y * Mathf.Abs(footprint.transform.lossyScale.y) * .5f;
        Assert.That(Vector2.Distance(start, end), Is.GreaterThan(radius * 2f));
        Assert.That(radius, Is.EqualTo(view.DamageHalfWidth).Within(.001f));
        Assert.That(footprint.enabled, Is.True);
        Assert.That(caps.arraySize, Is.EqualTo(2));
        for (int i = 0; i < caps.arraySize; i++)
        {
            var cap = (SpriteRenderer)caps.GetArrayElementAtIndex(i).objectReferenceValue;
            Assert.That(cap.enabled, Is.True);
            Assert.That((Vector2)cap.transform.position, Is.EqualTo(i == 0 ? start : end));
            Assert.That(cap.size.x * Mathf.Abs(cap.transform.lossyScale.x), Is.EqualTo(radius * 2f).Within(.001f));
        }
    }

    [Test]
    public void RoutePresetsEscalateAndReuseBossAssets()
    {
        float previousDelay = float.MaxValue;
        var boss = AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/_Project/prefabs/Enemies/p_Boss1.prefab").GetComponent<BossRocketAttack>();
        var bossData = new SerializedObject(boss);
        for (int i = 1; i <= RunRoute.TotalSectors; i++)
        {
            var stage = AssetDatabase.LoadAssetAtPath<StageProfileData>(
                $"Assets/_Project/Data/Stages/StageProfiles/StageProfile_{i:00}.asset");
            var preset = stage.WorldHazards;
            Assert.That(preset, Is.Not.Null);
            Assert.That(preset.InitialDelay, Is.LessThan(previousDelay));
            previousDelay = preset.InitialDelay;
            Assert.That(preset.Attacks.Length, Is.EqualTo(1));
            Assert.That(preset.Attacks[0].IsConfigured, Is.True);
            var rocket = new SerializedObject(preset.Attacks[0]);
            foreach (string field in new[] { "rocketPrefab", "targetPrefab", "explosionPrefab" })
                Assert.That(rocket.FindProperty(field).objectReferenceValue,
                    Is.EqualTo(bossData.FindProperty(field).objectReferenceValue), field);
            Assert.That(rocket.FindProperty("warningDelay").floatValue, Is.GreaterThanOrEqualTo(1.5f));
        }
    }
}
