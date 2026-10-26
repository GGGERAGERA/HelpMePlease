#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
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

public sealed class PistolPrecisionTests
{
    private const string Output = "Artifacts/GeneratedQA/RocketAndPistol-20261026";
    private const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
    private static readonly FieldInfo Cooldown = typeof(OrbitalModuleRuntime).GetField("Cooldown", Hidden);
    [SetUp] public void Preserve() => CoreTestSupport.PreservePreferences();
    [UnityTearDown] public IEnumerator Cleanup() => CoreTestSupport.CleanupPlayMode();

    [UnityTest, Timeout(120000)]
    public IEnumerator ProductionPistolImprovesTransverseHitsWithoutSteeringOrMoreAimCorrection()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
        yield return new EnterPlayMode();
        yield return CheckProductionPistol();
    }

    private static IEnumerator CheckProductionPistol()
    {
        yield return CoreTestSupport.LoadBunker();
        RunSelectionManager.Instance.SelectCharacter(AssetDatabase.LoadAssetAtPath<CharacterData>(
            "Assets/_Project/Data/Characters/01_Gera.asset"));
        GameplayRandom.Begin(1709);
        var starter = Object.FindFirstObjectByType<BunkerRunStarter>();
        starter.StartRun(starter.transform);
        yield return CoreTestSupport.Await(() => ProductionSceneComposition.Active is { IsReady: true } &&
            PlayerRuntimeReference.CachedPlayer != null && !SceneTransitionOverlay.IsTransitioning);
        Application.runInBackground = true;
        var player = PlayerRuntimeReference.CachedPlayer;
        player.GetComponent<PlayerHealth>().AddMaxHealth(1000000f);
        var station = player.GetComponentInChildren<OrbitalStationRuntime>();
        var spawner = Object.FindFirstObjectByType<EnemySpawner>();
        spawner.StopSpawning(); station.enabled = false;
        foreach (var enemy in EnemyHealth.ActiveInstances.ToArray()) Object.Destroy(enemy.gameObject);
        yield return null;
        var module = (OrbitalProjectileModule)station.Modules.First(m => m.Kind == OrbitalModuleKind.Pistol);
        var adapter = (ProductionOrbitalCombatAdapter)station.Combat;
        var target = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/_Project/prefabs/Enemies/p_Enemy_classic.prefab"));
        foreach (var movement in target.GetComponents<EnemyMovement>()) movement.enabled = false;
        target.GetComponent<Rigidbody2D>().bodyType = RigidbodyType2D.Kinematic;
        target.GetComponent<Rigidbody2D>().linearVelocity = Vector2.zero;
        var health = target.GetComponent<EnemyHealth>();
        yield return null;
        float authoredSpeed = module.Settings.ProjectileSpeed;
        var report = new List<string> { "profile,fps,distance,targetSpeed,hit,damage" };
        var totals = new Dictionary<string, int>();
        Directory.CreateDirectory(Output);
        try
        {
            Assert.That(module.Settings.AimLeadSeconds, Is.EqualTo(.35f));
            Assert.That(module.Settings.MaxAimCorrectionDegrees, Is.EqualTo(12f));
            foreach (var profile in new[] { (name: "before13", speed: 13f), (name: "candidate16", speed: 16f),
                         (name: "candidate18", speed: 18f), (name: "production", speed: authoredSpeed) })
            {
                int hits = 0;
                module.Settings.ProjectileSpeed = profile.speed;
                foreach (int fps in new[] { 30, 60, 120 })
                foreach (float distance in new[] { 3f, 5f, 7f })
                foreach (float velocity in new[] { -4f, -3f, -2f, 0f, 2f, 3f, 4f })
                {
                    PrepareShot(module, adapter, target, health, distance, velocity, 1f / fps);
                    Vector2 origin = module.WorldPosition;
                    for (int frame = 1; frame <= Mathf.CeilToInt(module.Settings.Range / profile.speed * fps) + 1; frame++)
                    {
                        target.transform.position = origin + new Vector2(distance, velocity * frame / fps);
                        Physics2D.SyncTransforms(); adapter.Tick(1f / fps);
                    }
                    float damage = 1000f - health.CurrentHealth;
                    if (damage > 0) hits++;
                    report.Add(FormattableString.Invariant($"{profile.name},{fps},{distance},{velocity},{(damage > 0 ? 1 : 0)},{damage}"));
                }
                totals[profile.name] = hits;
                // Render the same live authored projectile/target, using the real game camera.
                if (profile.name is "before13" or "production")
                {
                    adapter.DebugClearProjectiles();
                    // Let pooled hit sparks and damage numbers from the synchronous matrix expire.
                    yield return new WaitForSeconds(1.2f);
                    const int fps = 60;
                    PrepareShot(module, adapter, target, health, 5f, 3f, 1f / fps);
                    Vector2 origin = module.WorldPosition;
                    for (int frame = 1; frame <= 25; frame++)
                    {
                        target.transform.position = origin + new Vector2(5f, 3f * frame / fps);
                        Physics2D.SyncTransforms(); adapter.Tick(1f / fps);
                        yield return null;
                        if (frame is 5 or 12)
                        {
                            string shot = Path.Combine(Output, $"pistol-{profile.name}-{frame}.png");
                            if (File.Exists(shot)) File.Delete(shot);
                            ScreenCapture.CaptureScreenshot(shot);
                            yield return CoreTestSupport.Await(() => File.Exists(shot));
                        }
                    }
                }
            }
            File.WriteAllLines(Path.Combine(Output, "pistol-speed-comparison.csv"), report);
            File.WriteAllLines(Path.Combine(Output, "pistol-summary.txt"), totals.Select(pair => $"{pair.Key}: {pair.Value}/63"));

            module.Settings.ProjectileSpeed = authoredSpeed;
            PrepareShot(module, adapter, target, health, 5f, 8f, 1f / 60f);
            Vector2 fastOrigin = module.WorldPosition;
            for (int frame = 1; frame <= 60; frame++)
            {
                target.transform.position = fastOrigin + new Vector2(5f, 8f * frame / 60f);
                Physics2D.SyncTransforms(); adapter.Tick(1f / 60f);
            }
            Assert.That(health.CurrentHealth, Is.EqualTo(1000f), "Fast lateral escape must still beat the bounded launch correction.");
            PrepareShot(module, adapter, target, health, 5f, 3f, 1f / 60f);
            fastOrigin = module.WorldPosition;
            for (int frame = 1; frame <= 60; frame++)
            {
                target.transform.position = fastOrigin + new Vector2(5f, -8f * frame / 60f);
                Physics2D.SyncTransforms(); adapter.Tick(1f / 60f);
            }
            Assert.That(health.CurrentHealth, Is.EqualTo(1000f), "A post-launch dodge cannot be followed by the projectile.");
            var shieldTarget = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/_Project/prefabs/Enemies/ShieldBearer.prefab"));
            try
            {
                foreach (var ai in shieldTarget.GetComponents<EnemyMovement>()) ai.enabled = false;
                shieldTarget.GetComponent<Rigidbody2D>().bodyType = RigidbodyType2D.Kinematic;
                shieldTarget.transform.position = module.WorldPosition + Vector2.right * 4f;
                var shield = shieldTarget.GetComponent<EnemyDirectionalShield>();
                var shieldHealth = shieldTarget.GetComponent<EnemyHealth>();
                shield.Bind(player.transform);
                foreach (float speed in new[] { 13f, authoredSpeed })
                {
                    module.Settings.ProjectileSpeed = speed;
                    adapter.DebugClearProjectiles(); shieldHealth.SetRuntimeMaxHealth(1000f);
                    int blocks = shield.Blocks;
                    Physics2D.SyncTransforms();
                    adapter.SpawnWeaponProjectile(shield.Center + shield.Forward * 4f, shieldHealth,
                        -shield.Forward, module.Settings, null, 8f);
                    for (int frame = 0; frame < 45; frame++) adapter.Tick(1f / 60f);
                    Assert.That(shield.Blocks, Is.EqualTo(blocks + 1), "The faster swept shot must still hit the frontal plate.");
                    Assert.That(shieldHealth.CurrentHealth, Is.EqualTo(1000f));
                    adapter.SpawnWeaponProjectile(shield.Center - shield.Forward * 4f, shieldHealth,
                        shield.Forward, module.Settings, null, 8f);
                    for (int frame = 0; frame < 45; frame++) adapter.Tick(1f / 60f);
                    Assert.That(shieldHealth.CurrentHealth, Is.EqualTo(992f), "Rear positioning must retain the same body damage.");
                    Assert.That(shield.Blocks, Is.EqualTo(blocks + 1));
                }
            }
            finally { Object.Destroy(shieldTarget); }
            Assert.That(totals["production"], Is.GreaterThan(totals["before13"]),
                "The production setting must recover demonstrated misses on the same targets without increasing aim correction.");
        }
        finally
        {
            module.Settings.ProjectileSpeed = authoredSpeed;
            adapter.DebugClearProjectiles();
            Object.Destroy(target); station.enabled = true;
        }
    }

    private static void PrepareShot(OrbitalProjectileModule module, ProductionOrbitalCombatAdapter adapter,
        GameObject target, EnemyHealth health, float distance, float velocity, float deltaTime)
    {
        adapter.DebugClearProjectiles(); module.ResetFiring();
        Cooldown.SetValue(module, 10f);
        health.SetRuntimeMaxHealth(1000f);
        Vector2 origin = module.WorldPosition;
        for (int frame = -Mathf.CeilToInt(.3f / deltaTime); frame < 0; frame++)
        {
            target.transform.position = origin + new Vector2(distance, velocity * frame * deltaTime);
            Physics2D.SyncTransforms(); module.Tick(deltaTime);
        }
        target.transform.position = origin + Vector2.right * distance;
        Physics2D.SyncTransforms(); Cooldown.SetValue(module, 0f);
        int shots = module.ShotsFired;
        module.Tick(deltaTime);
        Assert.That(module.ShotsFired, Is.EqualTo(shots + 1));
    }
}
#endif
