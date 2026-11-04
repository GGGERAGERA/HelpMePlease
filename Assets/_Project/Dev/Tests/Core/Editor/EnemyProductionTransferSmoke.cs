#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Subject42.Combat.OrbitalStation;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

public sealed class EnemyProductionTransferSmoke
{
    const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
    static T Field<T>(object target, string name) => (T)target.GetType().GetField(name, Hidden).GetValue(target);
    static object Call(object target, string name, params object[] args) => target.GetType().GetMethod(name, Hidden).Invoke(target, args);
    static string Phase(EnemyChaseMovement actor) => Field<object>(actor, "ramPhase").ToString();
    static string Kind(EnemyHealth actor) => actor != null ? actor.name.Replace("(Clone)", "") : "";
    [SetUp] public void Preserve() => CoreTestSupport.PreservePreferences();
    [UnitySetUp] public IEnumerator Begin() => CoreTestSupport.BeginRun();
    [UnityTearDown] public IEnumerator Cleanup() => CoreTestSupport.CleanupPlayMode();

    [UnityTest, Timeout(90000)]
    public IEnumerator BunkerSpawnerMovementShieldsSpidersAndWeapons()
    {
        EnemyProductionTransferPreview.CheckAssets();
        Assert.That(RunStateManager.Instance.IsDevelopmentRun, Is.False);
        Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("MVP"));
        // Normal bunker entry is above; skip only the long sector timers for this bounded scenario.
        var run = RunStateManager.Instance;
        var old = run.CurrentSector;
        var stage = AssetDatabase.LoadAssetAtPath<StageProfileData>("Assets/_Project/Data/Stages/StageProfiles/StageProfile_03.asset");
        run.SetCurrentSector(new RunSector(3, stage, old.WorldRule, old.LocalAnomaly));
        yield return SceneManager.LoadSceneAsync("MVP");
        yield return CoreTestSupport.Await(() => Object.FindFirstObjectByType<OrbitalStationRuntime>() is { IsInitialized: true } && !SceneTransitionOverlay.IsTransitioning);
        var spawner = Object.FindFirstObjectByType<EnemySpawner>();
        var station = Object.FindFirstObjectByType<OrbitalStationRuntime>();
        var adapter = (ProductionOrbitalCombatAdapter)station.Combat;
        var player = Object.FindFirstObjectByType<CharacterSpawner>().SpawnedPlayer;
        player.GetComponent<PlayerHealth>().AddMaxHealth(1000000f);
        var playerBody = player.GetComponent<Rigidbody2D>(); playerBody.constraints = RigidbodyConstraints2D.FreezeAll;
        Assert.That(Field<EnemySpawnProfile>(spawner, "spawnProfile"), Is.SameAs(stage.SpawnProfile));
        spawner.enabled = false; station.enabled = false;
        Clear();
        var seen = new HashSet<string>();
        var kept = new List<EnemyHealth>();
        var configuredHealth = new Dictionary<EnemyHealth, float>();
        string[] required = { "p_Enemy_classic", "p_Enemy_SpiderDark", "p_Enemy_SpiderBigGait", "p_Enemy_SpiderBlue", "p_Enemy_Flanker", "p_Enemy_Breacher" };
        GameplayRandom.Begin(42);
        // Exercise the actual weighted cycle, position finder and spawn initialization.
        // Cadence alone is shortened; no roster/weights/data are substituted.
        for (int cycle = 0; cycle < 240; cycle++)
        {
            Call(spawner, "SpawnCycle", 1);
            var added = EnemyHealth.ActiveInstances.Where(e => !kept.Contains(e)).ToArray();
            foreach (var actor in added)
            {
                configuredHealth[actor] = actor.MaxHealth;
                actor.GetComponent<EnemyMovement>().enabled = false;
                actor.GetComponent<EnemyCollisionHandler>().enabled = false;
            }
            yield return null; // Let the ordinary creation-time shield roll happen.
            foreach (var actor in added)
            {
                string kind = Kind(actor); seen.Add(kind);
                bool wanted = required.Contains(kind) && !kept.Any(k => Kind(k) == kind);
                wanted |= kind == "p_Enemy_Breacher" && kept.Count(k => Kind(k) == kind) < 2;
                wanted |= actor.GetComponent<EnemyTacticalTimedShield>() is { ActivationDelay: > 0 } && kept.Count(k => k.GetComponent<EnemyTacticalTimedShield>() is { ActivationDelay: > 0 }) < 2;
                if (wanted) kept.Add(actor); else Object.DestroyImmediate(actor.gameObject);
            }
            Call(spawner, "RemoveDestroyedEnemies");
            if (required.All(seen.Contains) && kept.Count(k => Kind(k) == "p_Enemy_Breacher") >= 2 && kept.Count(k => k.GetComponent<EnemyTacticalTimedShield>() is { ActivationDelay: > 0 }) >= 2) break;
        }
        Assert.That(required.All(seen.Contains), Is.True, string.Join(", ", seen));
        Assert.That(seen, Does.Not.Contain("p_Enemy_SpiderWhite"));
        TestContext.WriteLine("Ordinary SpawnCycle produced: " + string.Join(", ", seen));
        var shieldActors = kept.Where(k => k.GetComponent<EnemyTacticalTimedShield>() is { ActivationDelay: > 0 }).Take(2).ToArray();
        Assert.That(shieldActors.Length, Is.EqualTo(2));
        foreach (var actor in kept) Move(actor, new Vector2(35, kept.IndexOf(actor) * 4));
        foreach (var actor in shieldActors)
        {
            var timer = actor.GetComponent<EnemyTacticalTimedShield>();
            Assert.That(timer.ActivationDelay, Is.InRange(3f, 6f));
            Assert.That(actor.MaxHealth, Is.EqualTo(configuredHealth[actor]), "shield timer preserves the ordinary spawner's existing sector/phase scaling");
        }
        var warned = new HashSet<EnemyTacticalTimedShield>();
        float deadline = Time.time + 6.5f;
        while (shieldActors.Any(a => !a.GetComponent<EnemyTacticalTimedShield>().Activated) && Time.time < deadline)
        {
            foreach (var actor in shieldActors)
            {
                var timer = actor.GetComponent<EnemyTacticalTimedShield>();
                if (!timer.Warning) continue;
                warned.Add(timer);
                Assert.That(timer.Shield.ShieldCollider.enabled, Is.False);
                Assert.That(timer.Shield.Covers(Vector2.right), Is.False);
            }
            yield return null;
        }
        Assert.That(warned.Count, Is.GreaterThan(0));
        for (int i = 0; i < 2; i++)
        {
            var actor = shieldActors[i]; Move(actor, new Vector2(7, i * 6));
            var shield = actor.GetComponent<EnemyDirectionalShield>(); shield.Bind(player.transform);
            Assert.That(shield.enabled && shield.RemainingBlockedHits == 2, Is.True);
        }
        Physics2D.SyncTransforms();
        var bearer = shieldActors[0]; var plate = bearer.GetComponent<EnemyDirectionalShield>(); float hp = bearer.CurrentHealth;
        Shot(bearer, plate.Forward, OrbitalWeaponSettings.Pistol(), 1);
        Assert.That(bearer.CurrentHealth, Is.EqualTo(hp - 1));
        hp = bearer.CurrentHealth;
        Shot(bearer, -plate.Forward, OrbitalWeaponSettings.Pistol(), 8);
        Assert.That(plate.RemainingBlockedHits, Is.EqualTo(1)); Assert.That(bearer.CurrentHealth, Is.EqualTo(hp));
        Shot(bearer, -plate.Forward, OrbitalWeaponSettings.Pistol(), 8);
        Assert.That(plate.IsBroken && !plate.ShieldCollider.enabled && plate.BreakShards.particleCount > 0, Is.True);
        Shot(bearer, -plate.Forward, OrbitalWeaponSettings.Pistol(), 1);
        Assert.That(bearer.CurrentHealth, Is.EqualTo(hp - 1));
        var sniper = shieldActors[1]; var sniperPlate = sniper.GetComponent<EnemyDirectionalShield>(); hp = sniper.CurrentHealth;
        Shot(sniper, -sniperPlate.Forward, OrbitalWeaponSettings.Sniper(), 1);
        Assert.That(sniper.CurrentHealth, Is.EqualTo(hp - 1)); Assert.That(sniperPlate.RemainingBlockedHits, Is.EqualTo(2));
        TestContext.WriteLine("Natural shield rolls: 3–6 s, visible nonblocking warning, two blocks/break FX, open side, real sniper bypass; no HP reset.");
        foreach (var actor in kept) Move(actor, new Vector2(35, kept.IndexOf(actor) * 4));

        var flank = kept.First(a => Kind(a) == "p_Enemy_Flanker");
        // Existing threat controller continually writes its movement modifier; isolate nominal tuning.
        Object.FindFirstObjectByType<RunThreatController>().enabled = false;
        var flankMove = flank.GetComponent<EnemyChaseMovement>(); Move(flank, Vector2.left * 24);
        foreach (var collider in flank.GetComponentsInChildren<Collider2D>()) collider.enabled = false;
        flankMove.enabled = true; flankMove.SetSpeedMultiplier(1); flankMove.SetWorldRuleSpeedMultiplier(1); flankMove.SetAnomalySpeedMultiplier(1); flankMove.SetThreatMovement(1, 0);
        Vector2 start = flank.transform.position;
        yield return Observe(3.3f);
        Assert.That(Mathf.Abs(flank.transform.position.y - start.y), Is.GreaterThan(3));
        Assert.That(Field<bool>(flankMove, "flankingApproach"), Is.True, "spiral lasts beyond three seconds");
        Assert.That(Field<float>(flankMove, "normalSpeed"), Is.EqualTo(7));
        float speed = Field<Vector2>(flankMove, "chaseVelocity").magnitude;
        float modifiers = Field<float>(flankMove, "speedMultiplier") * Field<float>(flankMove, "anomalySpeedMultiplier") * Field<float>(flankMove, "worldRuleSpeedMultiplier") * Field<float>(flankMove, "threatSpeedMultiplier");
        Assert.That(speed, Is.EqualTo(7 * modifiers).Within(.05));
        flankMove.enabled = false;
        TestContext.WriteLine($"Flanker: actual spiral continues after 3.3 s; nominal 7 m/s, actual {speed:0.00}, existing modifiers x{modifiers:0.00}.");

        var rams = kept.Where(a => Kind(a) == "p_Enemy_Breacher").Take(2).ToArray();
        Assert.That(rams.Length, Is.EqualTo(2));
        for (int i = 0; i < rams.Length; i++)
        {
            var actor = rams[i]; Move(actor, new Vector2(i == 0 ? -16 : 16, i * 3));
            var move = actor.GetComponent<EnemyChaseMovement>(); move.enabled = true;
            foreach (var collider in actor.GetComponentsInChildren<Collider2D>()) collider.enabled = false;
            move.SetSpeedMultiplier(1); move.SetWorldRuleSpeedMultiplier(1); move.SetAnomalySpeedMultiplier(1); move.SetThreatMovement(1, 0);
        }
        yield return CoreTestSupport.Await(() => rams.All(a => Phase(a.GetComponent<EnemyChaseMovement>()) == "Preparing"));
        var ram = rams[0].GetComponent<EnemyChaseMovement>(); var direction = Field<Vector2>(ram, "ramDirection");
        float ramModifiers = Field<float>(ram, "speedMultiplier") * Field<float>(ram, "anomalySpeedMultiplier") * Field<float>(ram, "worldRuleSpeedMultiplier") * Field<float>(ram, "threatSpeedMultiplier");
        Assert.That(Vector2.Distance(Field<LineRenderer>(ram, "ramTelegraph").GetPosition(0), Field<LineRenderer>(ram, "ramTelegraph").GetPosition(1)), Is.GreaterThanOrEqualTo(28.8f * ramModifiers - .1f));
        playerBody.position += Vector2.up * 6; player.transform.position = playerBody.position; Physics2D.SyncTransforms();
        yield return CoreTestSupport.Await(() => rams.All(a => Phase(a.GetComponent<EnemyChaseMovement>()) == "Charging"));
        ScreenCapture.CaptureScreenshot(EnemyProductionTransferPreview.Output + "/simultaneous-rams.png");
        var chargeStart = ram.GetComponent<Rigidbody2D>().position; float chargeAt = Time.fixedTime;
        rams[1].GetComponent<EnemyChaseMovement>().enabled = false; Move(rams[1], new Vector2(35, 30));
        yield return CoreTestSupport.Await(() => Phase(ram) == "Recovering");
        float distance = Vector2.Distance(chargeStart, ram.GetComponent<Rigidbody2D>().position);
        Assert.That(distance, Is.EqualTo(28.8f * ramModifiers).Within(.6f));
        Assert.That(Time.fixedTime - chargeAt, Is.EqualTo(1.2f).Within(.05f));
        Assert.That(Field<Vector2>(ram, "ramDirection"), Is.EqualTo(direction));
        Assert.That(ram.CanDealContactDamage, Is.False);
        Vector2 rest = ram.GetComponent<Rigidbody2D>().position;
        hp = rams[0].CurrentHealth; adapter.ApplyDamage(rams[0], 1, rest);
        Assert.That(rams[0].CurrentHealth, Is.EqualTo(hp - 1));
        yield return Observe(.3f);
        Assert.That(Vector2.Distance(rest, ram.GetComponent<Rigidbody2D>().position), Is.LessThan(.03));
        TestContext.WriteLine($"Two simultaneous independent charges; observed {distance:0.00} m; locked direction, stationary vulnerable recovery.");
        ram.enabled = false;

        foreach (string kind in required.Where(n => n.Contains("Spider")))
        {
            var actor = kept.First(a => Kind(a) == kind); Move(actor, new Vector2(6, 0));
            var animator = actor.GetComponentInChildren<Animator>();
            Assert.That(animator, Is.Not.Null, kind);
            float animationAt = animator.GetCurrentAnimatorStateInfo(0).normalizedTime;
            yield return Observe(.15f);
            Assert.That(animator.GetCurrentAnimatorStateInfo(0).normalizedTime, Is.GreaterThan(animationAt));
            var flash = actor.GetComponentInChildren<EnemyWhiteFlash>(); Assert.That(flash.TargetRenderer.enabled, Is.True);
            hp = actor.CurrentHealth; Shot(actor, Vector2.left, OrbitalWeaponSettings.Pistol(), 1);
            Assert.That(actor.CurrentHealth, Is.EqualTo(hp - 1));
            Shot(actor, Vector2.left, OrbitalWeaponSettings.Sniper(), actor.CurrentHealth + 1);
            Assert.That(actor.IsDead, Is.True);
            yield return null;
            Assert.That(actor == null, Is.True, kind + " ordinary death cleanup");
        }
        Assert.That(station.ValidateState(out _), Is.True);
        Assert.That(Object.FindFirstObjectByType<EnemyOrbitalLabController>(), Is.Null);
        Assert.That(Object.FindFirstObjectByType<EnemyOrbitalLabLaserTurret>(), Is.Null);
        Assert.That(run.IsDevelopmentRun, Is.False);
        TestContext.WriteLine("All three combat spider variants animate, take projectile hits and die; production orbital adapter intact; no lab controller/Interception.");
        GameplayRandom.End();

        void Clear() { foreach (var actor in EnemyHealth.ActiveInstances.ToArray()) Object.DestroyImmediate(actor.gameObject); Call(spawner, "RemoveDestroyedEnemies"); }
        void Move(EnemyHealth actor, Vector2 offset) { var point = (Vector2)player.transform.position + offset; actor.GetComponent<Rigidbody2D>().position = point; actor.transform.position = point; }
        void Shot(EnemyHealth actor, Vector2 direction, OrbitalWeaponSettings settings, float damage)
        {
            Vector2 center = actor.GetComponent<CapsuleCollider2D>().bounds.center;
            adapter.SpawnWeaponProjectile(center - direction * 3, actor, direction, settings, null, damage);
            for (int i = 0; i < 30; i++) adapter.Tick(.025f);
            adapter.DebugClearProjectiles();
        }
    }
    [UnityTest, Timeout(30000)]
    public IEnumerator ProductionSpiderSpritesAnimateThroughTheirAuthoredControllers()
    {
        // UnitySetUp enters Play Mode; create scenario locals after its domain reload.
        yield return ObserveProductionSpiderSprites();
    }

    private static IEnumerator ObserveProductionSpiderSprites()
    {
        BunkerDebugTools.SetGameView1600();
        var spawner = Object.FindFirstObjectByType<EnemySpawner>();
        var station = Object.FindFirstObjectByType<OrbitalStationRuntime>();
        var player = PlayerRuntimeReference.CachedPlayer;
        spawner.StopSpawning();
        foreach (var enemy in EnemyHealth.ActiveInstances.ToArray()) Object.Destroy(enemy.gameObject);
        yield return null;
        bool stationEnabled = station.enabled;
        bool frozen = EnemyDebugAiFreeze.IsFrozen;
        station.enabled = false;
        EnemyDebugAiFreeze.SetFrozen(false);
        player.GetComponent<Rigidbody2D>().constraints = RigidbodyConstraints2D.FreezeAll;
        try
        {
            string[] variants = { "Dark", "Blue", "BigGait" };
            var animators = new Animator[variants.Length];
            var renderers = new SpriteRenderer[variants.Length];
            var observed = new HashSet<Sprite>[variants.Length];
            for (int index = 0; index < variants.Length; index++)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                    "Assets/_Project/prefabs/Enemies/p_Enemy_Spider" + variants[index] + ".prefab");
                var enemy = spawner.SpawnDebugEnemyAt(prefab,
                    player.transform.position + new Vector3((index - 1) * 2f, 3f, 0f));
                animators[index] = enemy.GetComponentInChildren<Animator>();
                renderers[index] = enemy.GetComponentInChildren<EnemyWhiteFlash>().TargetRenderer;
                observed[index] = new HashSet<Sprite>();
            }
            yield return null;
            var initialTimes = animators.Select(animator => animator.GetCurrentAnimatorStateInfo(0).normalizedTime).ToArray();
            for (int index = 0; index < variants.Length; index++)
            {
                Assert.That(renderers[index].enabled && renderers[index].gameObject.activeInHierarchy &&
                    renderers[index].sprite != null, Is.True, variants[index]);
                Vector3 viewport = Camera.main.WorldToViewportPoint(renderers[index].bounds.center);
                Assert.That(viewport.x, Is.InRange(0.05f, 0.95f));
                Assert.That(viewport.y, Is.InRange(0.05f, 0.95f));
                Assert.That(viewport.z, Is.GreaterThan(0f));
                observed[index].Add(renderers[index].sprite);
            }
            const string output = "Artifacts/GeneratedQA/Boundary94Repair";
            System.IO.Directory.CreateDirectory(output);
            ScreenCapture.CaptureScreenshot(output + "/spiders-first.png");
            float deadline = Time.realtimeSinceStartup + 2f;
            while (Time.realtimeSinceStartup < deadline && observed.Any(frames => frames.Count < 2))
            {
                yield return null;
                for (int index = 0; index < variants.Length; index++) observed[index].Add(renderers[index].sprite);
            }
            for (int index = 0; index < variants.Length; index++)
            {
                Assert.That(observed[index].Count, Is.GreaterThanOrEqualTo(2), variants[index] + " sprite frames");
                Assert.That(observed[index].All(sprite => sprite != null), Is.True, variants[index] + " valid sprite frames");
                Assert.That(animators[index].GetCurrentAnimatorStateInfo(0).normalizedTime,
                    Is.GreaterThan(initialTimes[index]), variants[index] + " animation time");
            }
            ScreenCapture.CaptureScreenshot(output + "/spiders-second.png");
            yield return null;
        }
        finally
        {
            if (station != null) station.enabled = stationEnabled;
            EnemyDebugAiFreeze.SetFrozen(frozen);
        }
    }

    static IEnumerator Observe(float seconds)
    { float until = Time.time + seconds; while (Time.time < until) yield return null; }
}
#endif
