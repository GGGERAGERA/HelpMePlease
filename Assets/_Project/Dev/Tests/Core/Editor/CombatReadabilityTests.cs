#if UNITY_EDITOR
using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Subject42.Combat.OrbitalStation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

public sealed class CombatReadabilityTests
{
    private static T Read<T>(object target, string field) => (T)target.GetType()
        .GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);
    private static void Set(object target, Type type, string field, object value) => type
        .GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);

    [SetUp] public void Preserve() => CoreTestSupport.PreservePreferences();
    [UnityTearDown] public IEnumerator Cleanup()
    {
        var cleanup = CoreTestSupport.CleanupPlayMode();
        while (cleanup.MoveNext()) yield return cleanup.Current;
    }

    [UnityTest, Timeout(120000)]
    public IEnumerator WoundedRetreatChaserOneShotWorldLineAndPause()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
        yield return new EnterPlayMode();
        yield return BeginProductionRun();
        var productionSpawner = Object.FindFirstObjectByType<CharacterSpawner>();
        Assert.That(productionSpawner, Is.Not.Null, "Production CharacterSpawner must exist before preparation.");
        Assert.That(productionSpawner.SpawnedPlayer, Is.Not.Null, "Production player must be spawned.");
        var player = productionSpawner.SpawnedPlayer;
        var playerBody = player.GetComponent<Rigidbody2D>();
        Assert.That(playerBody, Is.Not.Null, "Production player needs Rigidbody2D.");
        var constraints = playerBody.constraints;
        var originalPosition = playerBody.position;
        var station = Object.FindFirstObjectByType<OrbitalStationRuntime>();
        var spawner = Object.FindFirstObjectByType<EnemySpawner>();
        Assert.That(station, Is.Not.Null, "Production ORBITAL must exist.");
        Assert.That(spawner, Is.Not.Null, "Production EnemySpawner must exist.");
        bool stationEnabled = station.enabled, spawnerEnabled = spawner.enabled;
        bool frozen = EnemyDebugAiFreeze.IsFrozen;
        float scale = Time.timeScale;
        player = PrepareProduction(); // Authoritative spawned player, as in the neighbouring flanking contract.
        Assert.That(player, Is.SameAs(productionSpawner.SpawnedPlayer));
        Assert.That(player.GetComponent<Rigidbody2D>(), Is.SameAs(playerBody));
        var playerHealth = player.GetComponent<PlayerHealth>();
        Assert.That(playerHealth, Is.Not.Null, "Prepared production player needs PlayerHealth; do not skip the scenario.");
        bool invulnerable = playerHealth.DebugInvulnerable;
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/_Project/prefabs/Enemies/p_Enemy_classic_start.prefab");
        Assert.That(prefab, Is.Not.Null, "Use the authored production Basic prefab.");
        var actor = Object.Instantiate(prefab, player.transform.position + Vector3.left * 7f, Quaternion.identity);
        Assert.That(actor, Is.Not.Null, "Production Basic must instantiate.");
        var chase = actor.GetComponent<EnemyChaseMovement>();
        var hp = actor.GetComponent<EnemyHealth>();
        var body = actor.GetComponent<Rigidbody2D>();
        float Remaining() => Read<float>(chase, "woundedRetreatRemaining");
        try
        {
            Assert.That(chase, Is.Not.Null, "Production Basic needs EnemyChaseMovement.");
            Assert.That(hp, Is.Not.Null, "Production Basic needs EnemyHealth.");
            Assert.That(body, Is.Not.Null, "Production Basic needs Rigidbody2D.");
            station.enabled = false;
            playerHealth.DebugInvulnerable = true;
            EnemyDebugAiFreeze.SetFrozen(true);
            Time.timeScale = 1f;
            yield return null;
            chase.ConfigureWoundedRetreatExperiment(true);
            var visual = Read<Transform>(chase, "flankingVisual");
            Assert.That(visual, Is.Not.Null, "Production Basic needs a safe existing graphic parent.");
            var authored = visual.localRotation;
            hp.TakeDamage(hp.MaxHealth * .49f, body.position);
            EnemyDebugAiFreeze.SetFrozen(false);
            yield return new WaitForFixedUpdate();
            yield return null;
            Assert.That(Remaining(), Is.Zero, "Above half health keeps pursuit.");
            hp.TakeDamage(hp.CurrentHealth - hp.MaxHealth * .5f, body.position);
            chase.StopAfterHit(); // The ordinary contact pipeline requests this separately from health damage.
            yield return CoreTestSupport.Await(() => Remaining() > 0f);
            Assert.That(hp.CurrentHealth, Is.EqualTo(hp.MaxHealth * .5f).Within(.0001f));
            var line = Read<Vector2>(chase, "woundedRetreatDirection");
            Assert.That(Vector2.Dot(line, (body.position - playerBody.position).normalized), Is.GreaterThan(.99f));
            if (Read<float>(chase, "stopTimer") > .15f)
            {
                float heldBudget = Remaining();
                yield return WaitGameTime(.1f);
                Assert.That(Remaining(), Is.EqualTo(heldBudget), "Hit stop cannot spend retreat locomotion.");
            }
            yield return CoreTestSupport.Await(() => Read<float>(chase, "stopTimer") <= 0f);
            yield return new WaitForFixedUpdate();
            yield return null;
            Assert.That(Quaternion.Angle(authored, visual.localRotation), Is.EqualTo(12f).Within(.02f));
            float remaining = Remaining();
            Assert.That(remaining, Is.InRange(.8f, .9f));
            var start = body.position;
            var tangent = new Vector2(-line.y, line.x);
            chase.SetWorldRuleExternalVelocity(tangent * 2f);
            Time.timeScale = 0f;
            yield return new WaitForSecondsRealtime(.12f);
            Assert.That(Remaining(), Is.EqualTo(remaining));
            Assert.That(Vector2.Distance(start, body.position), Is.LessThan(.001f));
            Time.timeScale = 1f;
            playerBody.position = body.position + tangent * 7f;
            Physics2D.SyncTransforms();
            yield return WaitGameTime(.12f);
            Assert.That(Read<Vector2>(chase, "woundedRetreatDirection"), Is.EqualTo(line));
            Assert.That(Vector2.Dot(body.position - start, line), Is.GreaterThan(.03f));
            Assert.That(Vector2.Dot(body.position - start, tangent), Is.GreaterThan(.1f), "External force survives retreat and pause.");
            chase.SetWorldRuleExternalVelocity(Vector2.zero);
            hp.TakeDamage(hp.MaxHealth * .05f, body.position);
            Assert.That(Remaining(), Is.LessThan(remaining), "Second wound cannot extend retreat.");
            hp.SetRuntimeMaxHealth(hp.MaxHealth); // Heal through existing API, preserving authored maximum.
            float beforeStop = Remaining();
            if (Read<float>(chase, "stopTimer") > .15f)
            {
                float heldBudget = Remaining();
                yield return WaitGameTime(.1f);
                Assert.That(Remaining(), Is.EqualTo(heldBudget), "Hit stop cannot spend retreat locomotion.");
            }
            yield return CoreTestSupport.Await(() => Read<float>(chase, "stopTimer") <= 0f);
            float started = Time.time;
            yield return CoreTestSupport.Await(() => Remaining() <= 0f);
            Assert.That(Time.time - started, Is.EqualTo(beforeStop).Within(.1f), "Only locomotion consumes the 0.9 s budget.");
            Assert.That(Quaternion.Angle(authored, visual.localRotation), Is.LessThan(.02f));
            yield return AwaitWoundedRetreatPursuit(chase, body, playerBody);
            chase.ConfigureWoundedRetreatExperiment(false);
            chase.ConfigureWoundedRetreatExperiment(true);
            hp.TakeDamage(hp.MaxHealth * .5f, body.position);
            yield return new WaitForFixedUpdate();
            yield return null;
            Assert.That(Remaining(), Is.Zero, "Healing and OFF/ON cannot rearm a life.");
            actor.SetActive(false);
            actor.SetActive(true);
            chase.ConfigureWoundedRetreatExperiment(true);
            hp.TakeDamage(hp.MaxHealth * .5f, body.position);
            yield return CoreTestSupport.Await(() => Remaining() > 0f && Quaternion.Angle(authored, visual.localRotation) > 1f);
            chase.ConfigureWoundedRetreatExperiment(false);
            Assert.That(Remaining(), Is.Zero);
            Assert.That(Quaternion.Angle(authored, visual.localRotation), Is.LessThan(.02f));
        }
        finally
        {
            if (chase != null) chase.SetWorldRuleExternalVelocity(Vector2.zero);
            Object.DestroyImmediate(actor);
            playerBody.position = originalPosition;
            playerBody.constraints = constraints;
            playerBody.linearVelocity = Vector2.zero;
            station.enabled = stationEnabled;
            spawner.enabled = spawnerEnabled;
            spawner.StopSpawning();
            playerHealth.DebugInvulnerable = invulnerable;
            EnemyDebugAiFreeze.SetFrozen(frozen);
            Time.timeScale = scale;
        }
    }

    [UnityTest, Timeout(120000)]
    public IEnumerator WoundedRetreatChaserSpawnerLifecycleAndCadence()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
        yield return new EnterPlayMode();
        yield return WoundedRetreatLifecycleBody();
    }

    private static IEnumerator WoundedRetreatLifecycleBody()
    {
        yield return BeginProductionRun();
        var originalConstraints = PlayerRuntimeReference.CachedPlayer.GetComponent<Rigidbody2D>().constraints;
        var player = PrepareProduction();
        var station = Object.FindFirstObjectByType<OrbitalStationRuntime>();
        var spawner = Object.FindFirstObjectByType<EnemySpawner>();
        bool stationEnabled = station.enabled;
        bool spawnerEnabled = spawner.enabled;
        bool frozen = EnemyDebugAiFreeze.IsFrozen;
        float scale = Time.timeScale;
        var actors = new System.Collections.Generic.List<GameObject>();
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/prefabs/Enemies/p_Enemy_classic_start.prefab");
        var spawn = typeof(EnemySpawner).GetMethod("SpawnEnemyAt", BindingFlags.Instance | BindingFlags.NonPublic);
        EnemyChaseMovement Spawn(bool ordinary)
        {
            var actor = (GameObject)spawn.Invoke(spawner, new object[] {
                prefab, player.transform.position + Vector3.left * (12f + actors.Count * 2f), ordinary });
            Assert.That(actor, Is.Not.Null);
            actors.Add(actor);
            return actor.GetComponent<EnemyChaseMovement>();
        }
        EnemyChaseMovement Manual()
        {
            var actor = spawner.DebugSpawnWoundedRetreatAround(prefab, player.transform.position, out string status);
            Assert.That(actor, Is.Not.Null, status);
            actors.Add(actor);
            Assert.That(actor.GetComponent<EnemyChaseMovement>().IsWoundedRetreatExperiment, Is.True);
            return actor.GetComponent<EnemyChaseMovement>();
        }
        try
        {
            station.enabled = false;
            EnemyDebugAiFreeze.SetFrozen(true);
            Time.timeScale = 1f;
            Assert.That(spawner.WoundedRetreatExperiment, Is.False);
            Assert.That(EnemySpawner.IsWoundedRetreatEligible(prefab), Is.True);
            var ready = Spawn(true);
            Assert.That(ready.IsWoundedRetreatExperiment, Is.False);
            yield return null;
            ready.ConfigureFlankingExperiment(true, true);
            Assert.That(EnemySpawner.IsWoundedRetreatEligible(ready.gameObject), Is.False);
            ready.ConfigureFlankingExperiment(false, false);
            ready.ConfigureCommittedRamExperiment(true);
            Assert.That(EnemySpawner.IsWoundedRetreatEligible(ready.gameObject), Is.False);
            ready.ConfigureCommittedRamExperiment(false);
            ready.SetAssaultDestination(player.transform.position);
            Assert.That(EnemySpawner.IsWoundedRetreatEligible(ready.gameObject), Is.False);
            ready.SetAssaultDestination(null);
            foreach (var special in new[] { "p_Enemy_Shooter", "ShieldBearer", "ShieldShooter", "p_Boss1", "p_EnemyTurret1" })
            {
                var specialPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/prefabs/Enemies/" + special + ".prefab");
                Assert.That(specialPrefab, Is.Not.Null, special);
                Assert.That(EnemySpawner.IsWoundedRetreatEligible(specialPrefab), Is.False, special);
            }
            spawner.DebugSetWoundedRetreatExperiment(true);
            Assert.That(RunStateManager.Instance.IsDevelopmentRun, Is.True, "Development before assignment.");
            Assert.That(Spawn(true).IsWoundedRetreatExperiment, Is.False);
            Assert.That(Spawn(false).IsWoundedRetreatExperiment, Is.False, "Special spawn does not join cadence.");
            var manual = Manual();
            Assert.That(Read<int>(spawner, "retreatEligibleSpawns"), Is.EqualTo(1));
            Assert.That(Read<System.Collections.Generic.List<EnemyChaseMovement>>(spawner, "retreatChasers"), Does.Contain(manual));
            Assert.That(Spawn(true).IsWoundedRetreatExperiment, Is.False);
            Assert.That(Spawn(true).IsWoundedRetreatExperiment, Is.False);
            var fourth = Spawn(true);
            Assert.That(fourth.IsWoundedRetreatExperiment, Is.True, "Fourth eligible ordinary Basic chaser.");
            spawner.DebugSetWoundedRetreatExperiment(false);
            Assert.That(manual.IsWoundedRetreatExperiment || fourth.IsWoundedRetreatExperiment, Is.False);
            Assert.That(Read<int>(spawner, "retreatEligibleSpawns"), Is.Zero);
            Assert.That(Read<System.Collections.Generic.List<EnemyChaseMovement>>(spawner, "retreatChasers"), Is.Empty);
            spawner.DebugSetWoundedRetreatExperiment(true);
            for (int i = 0; i < 3; i++) Assert.That(Spawn(true).IsWoundedRetreatExperiment, Is.False);
            fourth = Spawn(true);
            Assert.That(fourth.IsWoundedRetreatExperiment, Is.True, "OFF reset the cadence.");
            manual = Manual();
            spawner.SetSpawnProfile(RunStateManager.Instance.CurrentSector.SpawnProfile, 1);
            spawner.StopSpawning();
            Assert.That(manual.IsWoundedRetreatExperiment || fourth.IsWoundedRetreatExperiment, Is.False);
            Assert.That(spawner.WoundedRetreatExperiment, Is.False);
            spawner.DebugSetWoundedRetreatExperiment(true);
            manual = Manual();
            spawner.enabled = false;
            Assert.That(manual.gameObject.activeInHierarchy && fourth.gameObject.activeInHierarchy, Is.True);
            Assert.That(manual.IsWoundedRetreatExperiment || fourth.IsWoundedRetreatExperiment, Is.False);
            Assert.That(spawner.WoundedRetreatExperiment, Is.False);
            Assert.That(Read<int>(spawner, "retreatEligibleSpawns"), Is.Zero);
            Assert.That(Read<System.Collections.Generic.List<EnemyChaseMovement>>(spawner, "retreatChasers"), Is.Empty);
            spawner.enabled = true;
            spawner.StopSpawning();
            Assert.That(spawner.WoundedRetreatExperiment, Is.False, "Re-enable never restores opt-in.");
            Assert.That(RunStateManager.Instance.IsDevelopmentRun, Is.True);
        }
        finally
        {
            spawner.DebugSetWoundedRetreatExperiment(false);
            foreach (var actor in actors) if (actor != null) Object.DestroyImmediate(actor);
            spawner.enabled = spawnerEnabled;
            spawner.StopSpawning();
            station.enabled = stationEnabled;
            player.GetComponent<Rigidbody2D>().constraints = originalConstraints;
            EnemyDebugAiFreeze.SetFrozen(frozen);
            Time.timeScale = scale;
        }
    }

    [UnityTest]
    public IEnumerator RecoilingShooterRetreatRecoveryAndExternalMotion()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
        yield return new EnterPlayMode();
        yield return RecoilingShooterMotionBody();
    }

    private static IEnumerator RecoilingShooterMotionBody()
    {
        yield return BeginProductionRun();
        var playerBody = PlayerRuntimeReference.CachedPlayer.GetComponent<Rigidbody2D>();
        var originalConstraints = playerBody.constraints;
        var originalPosition = playerBody.position;
        var player = PrepareProduction();
        var station = Object.FindFirstObjectByType<OrbitalStationRuntime>();
        var spawner = Object.FindFirstObjectByType<EnemySpawner>();
        bool stationEnabled = station.enabled, spawnerEnabled = spawner.enabled;
        bool frozen = EnemyDebugAiFreeze.IsFrozen;
        float scale = Time.timeScale;
        var health = player.GetComponent<PlayerHealth>();
        bool invulnerable = health.DebugInvulnerable;
        var existingProjectiles = Object.FindObjectsByType<EnemyProjectile>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        var actor = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/_Project/prefabs/Enemies/p_Enemy_Shooter.prefab"), player.transform.position + Vector3.left * 6f, Quaternion.identity);
        var shooter = actor.GetComponent<EnemyShooterMovement>();
        var body = actor.GetComponent<Rigidbody2D>();
        var visual = Read<Transform>(shooter, "visualRoot");
        Quaternion authored = visual.localRotation;
        float rootRotation = body.rotation;
        string Phase() => Read<object>(shooter, "recoilPhase").ToString();
        IEnumerator AwaitPhase(string expected)
        {
            float deadline = Time.realtimeSinceStartup + 3f;
            while (Phase() != expected && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(Phase(), Is.EqualTo(expected), "Natural Update must advance recoil phase.");
        }
        try
        {
            station.enabled = false;
            health.DebugInvulnerable = true;
            Time.timeScale = 1f;
            EnemyDebugAiFreeze.SetFrozen(true);
            yield return null; // Start binds the real player, without invoking lifecycle manually.
            Assert.That(Read<Transform>(shooter, "player"), Is.EqualTo(player.transform));
            Assert.That(shooter.RecoilingExperiment, Is.False, "Default OFF after Start.");
            Assert.That(shooter.CanUseRecoiling && shooter.HasRecoilingVisual, Is.True);
            Assert.That(visual, Is.Not.EqualTo(actor.transform));
            float preferred = Read<float>(shooter, "preferredDistance");
            float tolerance = Read<float>(shooter, "distanceTolerance");
            var area = GameplayAreaService.Instance;
            Vector2 center = playerBody.position;
            Vector2? radial = null;
            bool Free(Vector2 point) => area.IsInsidePlayableArea(point, .8f) && area.IsInsideSpawnArea(point, .8f) &&
                Physics2D.OverlapCircleAll(point, .8f).All(c => c.transform.IsChildOf(actor.transform) ||
                    c == area.PlayableArea || c == area.SpawnArea ||
                    (c.isTrigger && c.GetComponentInParent<EnemyHealth>() == null));
            for (int i = 0; i < 24; i++)
            {
                var direction = new Vector2(Mathf.Cos(i * Mathf.PI / 12f), Mathf.Sin(i * Mathf.PI / 12f));
                if (Free(center + direction * preferred) && Free(center + direction * (preferred + 2f)))
                { radial = direction; break; }
            }
            Assert.That(radial.HasValue, Is.True, "Need a free authored distance band, preserving obstacles and steering.");
            body.position = center + radial.Value * preferred;
            body.linearVelocity = Vector2.zero;
            Physics2D.SyncTransforms();
            Assert.That(Vector2.Distance(body.position, playerBody.position), Is.LessThanOrEqualTo(preferred + tolerance));
            shooter.ConfigureRecoilingExperiment(true);
            Set(shooter, typeof(EnemyShooterMovement), "fireTimer", .04f);
            Vector2 shotPlayerPosition = playerBody.position;
            EnemyDebugAiFreeze.SetFrozen(false);
            yield return AwaitPhase("Retreating");
            float retreatStarted = Time.time;
            var projectiles = Object.FindObjectsByType<EnemyProjectile>(FindObjectsSortMode.None)
                .Where(p => !existingProjectiles.Contains(p)).ToArray();
            Assert.That(projectiles.Length, Is.EqualTo(1), "A real ordinary shot starts retreat.");
            Vector2 directionAtShot = Read<Vector2>(shooter, "recoilDirection");
            Assert.That(Vector2.Dot(directionAtShot, (body.position - shotPlayerPosition).normalized), Is.GreaterThan(.99f));
            Assert.That(Vector2.Dot(Read<Vector2>(projectiles[0], "direction"), directionAtShot), Is.LessThan(-.99f));
            float timerBeforeRejected = Read<float>(shooter, "fireTimer");
            float phaseTimerBeforeRejected = Read<float>(shooter, "recoilTimer");
            shooter.ConfigureStrafingExperiment(true, true);
            shooter.ConfigureCommittedVolleyExperiment(true);
            Assert.That(shooter.StrafingExperiment || shooter.CommittedVolleyExperiment, Is.False);
            Assert.That(shooter.RecoilingExperiment, Is.True);
            Assert.That(Phase(), Is.EqualTo("Retreating"));
            Assert.That(Read<float>(shooter, "fireTimer"), Is.EqualTo(timerBeforeRejected));
            Assert.That(Read<float>(shooter, "recoilTimer"), Is.EqualTo(phaseTimerBeforeRejected));
            var tilted = visual.localRotation;
            Assert.That(Quaternion.Angle(authored, tilted), Is.EqualTo(15f).Within(.01f));
            var retreatStart = body.position;
            // Change the actual player position after commitment; crowd steering may bend the path.
            playerBody.position = body.position + new Vector2(-directionAtShot.y, directionAtShot.x) * preferred;
            Physics2D.SyncTransforms();
            yield return WaitGameTime(.16f);
            Assert.That(Read<Vector2>(shooter, "recoilDirection"), Is.EqualTo(directionAtShot), "Player relocation cannot retarget committed retreat.");
            Assert.That(Vector2.Dot(body.position - retreatStart, directionAtShot), Is.GreaterThan(.03f));
            Assert.That(Quaternion.Angle(tilted, visual.localRotation), Is.LessThan(.01f), "Tilt does not accumulate.");
            Assert.That(Mathf.DeltaAngle(rootRotation, body.rotation), Is.EqualTo(0f).Within(.01f));
            yield return AwaitPhase("Recovering");
            Assert.That(Time.time - retreatStarted, Is.EqualTo(.65f).Within(.12f));
            Assert.That(Quaternion.Angle(authored, visual.localRotation), Is.LessThan(.01f));
            float recoveryStarted = Time.time;
            yield return new WaitForFixedUpdate();
            var recoveryStart = body.position;
            yield return WaitGameTime(.1f);
            Assert.That(Vector2.Distance(body.position, recoveryStart), Is.LessThan(.03f), "Recovery has no own locomotion after crowd steering.");
            float timerBeforeFreeze = Read<float>(shooter, "recoilTimer");
            EnemyDebugAiFreeze.SetFrozen(true);
            float freezeStarted = Time.time;
            yield return WaitGameTime(.1f);
            Assert.That(Read<float>(shooter, "recoilTimer"), Is.EqualTo(timerBeforeFreeze).Within(.001f));
            Assert.That(Phase(), Is.EqualTo("Recovering"));
            float frozenDuration = Time.time - freezeStarted;
            EnemyDebugAiFreeze.SetFrozen(false);
            var external = new Vector2(-directionAtShot.y, directionAtShot.x) * 2f;
            shooter.SetWorldRuleExternalVelocity(external);
            recoveryStart = body.position;
            yield return WaitGameTime(.12f);
            Assert.That(Phase(), Is.EqualTo("Recovering"));
            Assert.That(Vector2.Dot(body.position - recoveryStart, external.normalized), Is.GreaterThan(.1f), "External world velocity still moves a recovering actor.");
            shooter.SetWorldRuleExternalVelocity(Vector2.zero);
            yield return AwaitPhase("Ready");
            Assert.That(Time.time - recoveryStarted - frozenDuration, Is.EqualTo(.65f).Within(.12f));
            Assert.That(Read<float>(shooter, "recoilTimer"), Is.Zero);
            // Outside the band after recovery, ordinary distance keeping resumes.
            playerBody.position = body.position - directionAtShot * (preferred + tolerance + 3f);
            Physics2D.SyncTransforms();
            var ordinaryStart = body.position;
            yield return WaitGameTime(.12f);
            Assert.That(Vector2.Distance(body.position, ordinaryStart), Is.GreaterThan(.03f));
            shooter.ConfigureRecoilingExperiment(false);
            Assert.That(Quaternion.Angle(authored, visual.localRotation), Is.LessThan(.01f));
            // A second real shot proves pose restoration from an active retreat, both OFF and disable.
            for (int boundary = 0; boundary < 2; boundary++)
            {
                EnemyDebugAiFreeze.SetFrozen(true);
                playerBody.position = body.position - directionAtShot * preferred;
                Physics2D.SyncTransforms();
                shooter.ConfigureRecoilingExperiment(true);
                Set(shooter, typeof(EnemyShooterMovement), "fireTimer", .04f);
                EnemyDebugAiFreeze.SetFrozen(false);
                yield return AwaitPhase("Retreating");
                Assert.That(Quaternion.Angle(authored, visual.localRotation), Is.EqualTo(15f).Within(.01f));
                if (boundary == 0) shooter.ConfigureRecoilingExperiment(false);
                else shooter.enabled = false;
                Assert.That(shooter.RecoilingExperiment, Is.False);
                Assert.That(Quaternion.Angle(authored, visual.localRotation), Is.LessThan(.01f));
            }
        }
        finally
        {
            shooter.SetWorldRuleExternalVelocity(Vector2.zero);
            if (actor != null) Object.DestroyImmediate(actor);
            foreach (var projectile in Object.FindObjectsByType<EnemyProjectile>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (!existingProjectiles.Contains(projectile)) Object.DestroyImmediate(projectile.gameObject);
            playerBody.position = originalPosition;
            playerBody.linearVelocity = Vector2.zero;
            playerBody.constraints = originalConstraints;
            station.enabled = stationEnabled;
            spawner.enabled = spawnerEnabled;
            spawner.StopSpawning();
            health.DebugInvulnerable = invulnerable;
            EnemyDebugAiFreeze.SetFrozen(frozen);
            Time.timeScale = scale;
        }
    }

    [UnityTest, Timeout(120000)]
    public IEnumerator RecoilingShooterOptInCadenceAndLifecycle()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
        yield return new EnterPlayMode();
        yield return RecoilingShooterLifecycleBody();
    }

    private static IEnumerator RecoilingShooterLifecycleBody()
    {
        yield return BeginProductionRun();
        var originalConstraints = PlayerRuntimeReference.CachedPlayer.GetComponent<Rigidbody2D>().constraints;
        var player = PrepareProduction();
        var station = Object.FindFirstObjectByType<OrbitalStationRuntime>();
        var spawner = Object.FindFirstObjectByType<EnemySpawner>();
        bool stationEnabled = station.enabled;
        bool spawnerEnabled = spawner.enabled;
        bool frozen = EnemyDebugAiFreeze.IsFrozen;
        float scale = Time.timeScale;
        var actors = new System.Collections.Generic.List<GameObject>();
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/prefabs/Enemies/p_Enemy_Shooter.prefab");
        var spawn = typeof(EnemySpawner).GetMethod("SpawnEnemyAt", BindingFlags.Instance | BindingFlags.NonPublic);
        EnemyShooterMovement Spawn(bool ordinary)
        {
            var actor = (GameObject)spawn.Invoke(spawner, new object[] {
                prefab, player.transform.position + Vector3.left * (12f + actors.Count * 2f), ordinary });
            Assert.That(actor, Is.Not.Null);
            actors.Add(actor);
            return actor.GetComponent<EnemyShooterMovement>();
        }
        EnemyShooterMovement Manual()
        {
            var actor = spawner.DebugSpawnRecoilingShooterAround(prefab, player.transform.position, out string status);
            Assert.That(actor, Is.Not.Null, status);
            actors.Add(actor);
            Assert.That(actor.GetComponent<EnemyShooterMovement>().RecoilingExperiment, Is.True);
            return actor.GetComponent<EnemyShooterMovement>();
        }
        try
        {
            station.enabled = false;
            EnemyDebugAiFreeze.SetFrozen(true);
            Time.timeScale = 1f;
            Assert.That(spawner.RecoilingShootersExperiment, Is.False);
            Assert.That(EnemySpawner.IsRecoilingShooterPrefabEligible(prefab), Is.True, "Serialized prefab eligibility needs no Awake.");
            Assert.That(EnemySpawner.IsRecoilingShooterEligible(prefab), Is.False, "Prefab is not a ready runtime actor.");
            var ready = Spawn(true);
            Assert.That(ready.RecoilingExperiment, Is.False);
            Assert.That(EnemySpawner.IsRecoilingShooterEligible(ready.gameObject), Is.True);
            Assert.That(Read<int>(spawner, "recoilingEligibleSpawns"), Is.Zero);
            yield return null; // Real Start readiness, with AI frozen and spawning stopped.
            Assert.That(Read<Transform>(ready, "player"), Is.EqualTo(player.transform));
            float fireBefore = Read<float>(ready, "fireTimer");
            ready.ConfigureStrafingExperiment(true, true);
            ready.ConfigureRecoilingExperiment(true);
            ready.ConfigureCommittedVolleyExperiment(true);
            Assert.That(ready.StrafingExperiment, Is.True);
            Assert.That(ready.RecoilingExperiment || ready.CommittedVolleyExperiment, Is.False);
            Assert.That(Read<float>(ready, "fireTimer"), Is.EqualTo(fireBefore));
            ready.ConfigureStrafingExperiment(false, false);
            ready.ConfigureCommittedVolleyExperiment(true);
            var volleyPhaseBefore = Read<object>(ready, "volleyPhase");
            float volleyTimerBefore = Read<float>(ready, "volleyTimer");
            fireBefore = Read<float>(ready, "fireTimer");
            ready.ConfigureRecoilingExperiment(true);
            ready.ConfigureStrafingExperiment(true, false);
            Assert.That(ready.CommittedVolleyExperiment, Is.True);
            Assert.That(ready.RecoilingExperiment || ready.StrafingExperiment, Is.False);
            Assert.That(Read<object>(ready, "volleyPhase"), Is.EqualTo(volleyPhaseBefore));
            Assert.That(Read<float>(ready, "volleyTimer"), Is.EqualTo(volleyTimerBefore));
            Assert.That(Read<float>(ready, "fireTimer"), Is.EqualTo(fireBefore));
            ready.ConfigureCommittedVolleyExperiment(false);
            spawner.DebugSetRecoilingShootersExperiment(true);
            Assert.That(RunStateManager.Instance.IsDevelopmentRun, Is.True, "Development before assignment.");
            Assert.That(Spawn(true).RecoilingExperiment, Is.False);
            Assert.That(Spawn(false).RecoilingExperiment, Is.False, "Special spawn does not join cadence.");
            var manual = Manual();
            Assert.That(Read<int>(spawner, "recoilingEligibleSpawns"), Is.EqualTo(1));
            Assert.That(Read<System.Collections.Generic.List<EnemyShooterMovement>>(spawner, "recoilingShooters"), Does.Contain(manual));
            Assert.That(Spawn(true).RecoilingExperiment, Is.False);
            Assert.That(Spawn(true).RecoilingExperiment, Is.False);
            var fourth = Spawn(true);
            Assert.That(fourth.RecoilingExperiment, Is.True, "Fourth eligible ordinary shooter.");
            spawner.DebugSetRecoilingShootersExperiment(false);
            Assert.That(manual.RecoilingExperiment || fourth.RecoilingExperiment, Is.False);
            Assert.That(Read<int>(spawner, "recoilingEligibleSpawns"), Is.Zero);
            Assert.That(Read<System.Collections.Generic.List<EnemyShooterMovement>>(spawner, "recoilingShooters"), Is.Empty);
            spawner.DebugSetRecoilingShootersExperiment(true);
            for (int i = 0; i < 3; i++) Assert.That(Spawn(true).RecoilingExperiment, Is.False);
            fourth = Spawn(true);
            Assert.That(fourth.RecoilingExperiment, Is.True, "OFF reset the cadence.");
            manual = Manual();
            spawner.enabled = false;
            Assert.That(manual.gameObject.activeInHierarchy && fourth.gameObject.activeInHierarchy, Is.True);
            Assert.That(manual.RecoilingExperiment || fourth.RecoilingExperiment, Is.False);
            Assert.That(spawner.RecoilingShootersExperiment, Is.False);
            Assert.That(Read<int>(spawner, "recoilingEligibleSpawns"), Is.Zero);
            Assert.That(Read<System.Collections.Generic.List<EnemyShooterMovement>>(spawner, "recoilingShooters"), Is.Empty);
            spawner.enabled = true;
            spawner.StopSpawning();
            Assert.That(spawner.RecoilingShootersExperiment, Is.False, "Re-enable never restores opt-in.");
            Assert.That(RunStateManager.Instance.IsDevelopmentRun, Is.True);
        }
        finally
        {
            spawner.DebugSetRecoilingShootersExperiment(false);
            foreach (var actor in actors) if (actor != null) Object.DestroyImmediate(actor);
            spawner.enabled = spawnerEnabled;
            spawner.StopSpawning();
            station.enabled = stationEnabled;
            player.GetComponent<Rigidbody2D>().constraints = originalConstraints;
            EnemyDebugAiFreeze.SetFrozen(frozen);
            Time.timeScale = scale;
        }
    }

    [UnityTest, Timeout(120000)]
    public IEnumerator WoundedRetreatChaserProductionMvpF1GameViewCapture()
    {
        typeof(BunkerStationPresentationTests).GetMethod("SetResolution", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, null);
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
        yield return new EnterPlayMode();
        yield return WoundedRetreatProductionCaptureBody();
    }

    private static IEnumerator WoundedRetreatProductionCaptureBody()
    {
        yield return BeginProductionRun(); // Ordinary station, spawning, AI and physics remain live.
        yield return CoreTestSupport.Await(() => Object.FindFirstObjectByType<Subject42DebugMenu>() != null);
        var player = PlayerRuntimeReference.CachedPlayer;
        var station = Object.FindFirstObjectByType<OrbitalStationRuntime>();
        var spawner = Object.FindFirstObjectByType<EnemySpawner>();
        var menu = Object.FindFirstObjectByType<Subject42DebugMenu>();
        Assert.That(player, Is.Not.Null);
        Assert.That(station, Is.Not.Null);
        Assert.That(spawner, Is.Not.Null);
        var health = player.GetComponent<PlayerHealth>();
        var controller = player.GetComponent<CharacterMovement2D>();
        var playerBody = player.GetComponent<Rigidbody2D>();
        var intent = controller.MovementIntent;
        var constraints = playerBody.constraints;
        var originalPosition = playerBody.position;
        bool invulnerable = health.DebugInvulnerable;
        bool frozen = EnemyDebugAiFreeze.IsFrozen;
        float scale = Time.timeScale;
        bool experiment = spawner.WoundedRetreatExperiment;
        GameObject actor = null;
        EnemyChaseMovement shooter = null;
        Transform visual = null;
        Quaternion authored = Quaternion.identity;
        Quaternion rootRotation = Quaternion.identity;
        bool ownsActor = false;
        bool Visible(Vector3 position)
        {
            var camera = Camera.main;
            if (camera == null || !camera.isActiveAndEnabled) return false;
            var v = camera.WorldToViewportPoint(position);
            return v.z > 0f && v.x > .08f && v.x < .92f && v.y > .08f && v.y < .92f;
        }
        void AssertLive()
        {
            Assert.That(actor != null && actor.activeInHierarchy, Is.True, "F1-owned wounded retreat chaser must survive this short observation.");
            Assert.That(actor.GetComponent<EnemyHealth>().IsDead, Is.False);
            Assert.That(shooter.isActiveAndEnabled && shooter.IsWoundedRetreatExperiment, Is.True);
            var owned = Read<System.Collections.Generic.List<GameObject>>(menu, "debugEnemies");
            Assert.That(owned.Count, Is.EqualTo(1));
            Assert.That(owned[0], Is.SameAs(actor));
            Assert.That(station.isActiveAndEnabled && station.IsInitialized, Is.True);
            Assert.That(spawner.isActiveAndEnabled && spawner.IsSpawningEnabled, Is.True);
            Assert.That(playerBody.simulated && actor.GetComponent<Rigidbody2D>().simulated, Is.True);
            Assert.That(controller.isActiveAndEnabled && !health.IsDead, Is.True);
            Assert.That(EnemyDebugAiFreeze.IsFrozen || Subject42DebugMenu.IsDebugMenuOpen, Is.False);
            Assert.That(Read<bool>(menu, "isOpen"), Is.False);
            Assert.That(Time.timeScale, Is.GreaterThan(0f));
            Assert.That(RunStateManager.Instance.IsDevelopmentRun, Is.True);
            Assert.That(Visible(actor.transform.position), Is.True, "Need a living visible wounded retreat chaser in the real Game View.");
            Assert.That(Quaternion.Angle(rootRotation, actor.transform.rotation), Is.LessThan(.001f));
        }
        try
        {
            health.DebugInvulnerable = true;
            EnemyDebugAiFreeze.SetFrozen(false);
            controller.MovementIntent = () => Vector2.zero;
            OpenEscortF1(menu);
            Assert.That(spawner.WoundedRetreatExperiment, Is.False, "Fresh MVP default OFF.");
            var tracked = Read<System.Collections.Generic.List<GameObject>>(menu, "debugEnemies");
            Assert.That(tracked, Is.Empty, "Do not take ownership of another diagnostic actor.");
            var profile = RunStateManager.Instance.CurrentSector.SpawnProfile;
            var prefab = profile.Phases.Where(p => p != null && p.enemies != null).SelectMany(p => p.enemies)
                .Where(e => e?.enemyPrefab != null).Select(e => e.enemyPrefab)
                .FirstOrDefault(EnemySpawner.IsWoundedRetreatEligible);
            Assert.That(prefab, Is.Not.Null, "Current sector needs an authored eligible Basic chaser; no replacement prefab.");
            var toggle = EscortF1Button(menu, "ПЕРЕКЛЮЧИТЬ", "Эксперимент: отступающий преследователь");
            Assert.That(toggle.interactable, Is.True);
            toggle.onClick.Invoke();
            Assert.That(spawner.WoundedRetreatExperiment, Is.True);
            Assert.That(RunStateManager.Instance.IsDevelopmentRun, Is.True, "Development must precede manual assignment.");
            var spawn = EscortF1Button(menu, "+1 ОТСТУПАЮЩИЙ");
            Assert.That(spawn.interactable, Is.True);
            spawn.onClick.Invoke();
            ownsActor = tracked.Count == 1;
            Assert.That(tracked.Count, Is.EqualTo(1), Read<string>(menu, "enemyDebugStatus"));
            actor = tracked.Single();
            Assert.That(actor.name, Is.EqualTo(prefab.name + "(Clone)"), "F1 uses the eligible prefab from this sector.");
            shooter = actor.GetComponent<EnemyChaseMovement>();
            visual = Read<Transform>(shooter, "flankingVisual");
            Assert.That(visual != actor.transform && visual.IsChildOf(actor.transform), Is.True);
            authored = visual.localRotation;
            rootRotation = actor.transform.rotation;
            Assert.That(Read<bool>(menu, "isOpen"), Is.False, "Successful real F1 callback closes the menu.");
            yield return null; // Natural Start binds the player before diagnostic placement.
            Assert.That(Read<Transform>(shooter, "player"), Is.EqualTo(player.transform));
            Assert.That(shooter.CanUseWoundedRetreat, Is.True);
            var body = actor.GetComponent<Rigidbody2D>();
            var area = GameplayAreaService.Instance;
            Assert.That(area, Is.Not.Null);
            // Move only the observer, never the enemy or its authored combat parameters.
            Vector2? destination = null;
            for (int i = 0; i < 24; i++)
            {
                float angle = i * Mathf.PI / 12f;
                Vector2 candidate = body.position + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * 3.5f;
                if (!area.IsInsidePlayableArea(candidate, .8f) || !area.IsInsideSpawnArea(candidate, .8f)) continue;
                if (Physics2D.OverlapCircleAll(candidate, .8f).All(c => c.transform.IsChildOf(player.transform) ||
                    c == area.PlayableArea || c == area.SpawnArea ||
                    (c.isTrigger && c.GetComponentInParent<EnemyHealth>() == null)))
                { destination = candidate; break; }
            }
            Assert.That(destination.HasValue, Is.True, "Need a free observation point, preserving enemy spawn.");
            MoveEscortPlayer(playerBody, destination.Value);
            playerBody.constraints = RigidbodyConstraints2D.FreezeAll;
            var enemyHealth = actor.GetComponent<EnemyHealth>();
            float authoredMax = enemyHealth.MaxHealth;
            // Reproducible scenario trigger through the ordinary damage API, NOT evidence
            // that the starting pistol can wound this authored Basic without killing it.
            enemyHealth.TakeDamage(enemyHealth.CurrentHealth - authoredMax * .5f, body.position);
            float Remaining() => Read<float>(shooter, "woundedRetreatRemaining");
            yield return CoreTestSupport.Await(() => actor != null && Remaining() > 0f &&
                Quaternion.Angle(authored, visual.localRotation) > 1f);
            AssertLive();
            Assert.That(enemyHealth.MaxHealth, Is.EqualTo(authoredMax));
            Assert.That(enemyHealth.CurrentHealth, Is.EqualTo(authoredMax * .5f).Within(.0001f));
            var line = Read<Vector2>(shooter, "woundedRetreatDirection");
            Vector2 retreatStart = body.position;
            yield return WaitGameTime(.12f);
            AssertLive();
            Assert.That(Remaining(), Is.GreaterThan(0f), "Capture must observe actual active retreat.");
            Assert.That(Vector2.Dot(body.position - retreatStart, line), Is.GreaterThan(.02f));
            Assert.That(Read<Vector2>(shooter, "woundedRetreatDirection"), Is.EqualTo(line));
            Assert.That(Quaternion.Angle(authored, visual.localRotation), Is.EqualTo(12f).Within(.02f));
            var active = CaptureFlankingGameView("wounded-retreat-active", "WoundedRetreatChaser");
            while (active.MoveNext())
            {
                AssertLive();
                Assert.That(Remaining(), Is.GreaterThan(0f), "Active capture must finish before retreat ends.");
                yield return active.Current;
            }
            AssertLive();
            Assert.That(Remaining(), Is.GreaterThan(0f), "Active Game View completed inside retreat, before recovery wait.");
            Assert.That(Quaternion.Angle(authored, visual.localRotation), Is.EqualTo(12f).Within(.02f));
            yield return CoreTestSupport.Await(() => actor != null && Remaining() <= 0f);
            yield return new WaitForFixedUpdate();
            yield return null;
            AssertLive();
            Assert.That(Read<bool>(shooter, "woundedRetreatUsed"), Is.True);
            Assert.That(Remaining(), Is.Zero, "Recovered phase, not just an existing PNG.");
            Assert.That(Quaternion.Angle(authored, visual.localRotation), Is.LessThan(.02f));
            yield return AwaitWoundedRetreatPursuit(shooter, body, playerBody);
            AssertLive();
            Assert.That(Remaining(), Is.Zero);
            var recovered = CaptureFlankingGameView("wounded-retreat-recovered", "WoundedRetreatChaser");
            while (recovered.MoveNext())
            {
                AssertLive();
                Assert.That(Remaining(), Is.Zero, "Recovered capture follows confirmed natural pursuit.");
                yield return recovered.Current;
            }
            AssertLive();
            Assert.That(Remaining(), Is.Zero);
            Assert.That(Quaternion.Angle(authored, visual.localRotation), Is.LessThan(.02f));
        }
        finally
        {
            EscortF1Call(menu, "CloseMenu");
            if (ownsActor) EscortF1Call(menu, "ClearDebugEnemies");
            spawner.DebugSetWoundedRetreatExperiment(experiment);
            controller.MovementIntent = intent;
            MoveEscortPlayer(playerBody, originalPosition);
            playerBody.linearVelocity = Vector2.zero;
            playerBody.constraints = constraints;
            health.DebugInvulnerable = invulnerable;
            EnemyDebugAiFreeze.SetFrozen(frozen);
            Time.timeScale = scale;
        }
    }

    private static IEnumerator AwaitWoundedRetreatPursuit(EnemyChaseMovement chase, Rigidbody2D body, Rigidbody2D playerBody)
    {
        // Observe the authored threat response and live physics. Retreat leaves an outward
        // chaseVelocity, so a fixed 0.1 s sample can still be moving away during recovery.
        float deadline = Time.realtimeSinceStartup + 5f;
        float TowardSpeed() => Vector2.Dot(Read<Vector2>(chase, "chaseVelocity"),
            (playerBody.position - body.position).normalized);
        string Diagnostic() => $"remaining={Read<float>(chase, "woundedRetreatRemaining")}, " +
            $"used={Read<bool>(chase, "woundedRetreatUsed")}, stop={Read<float>(chase, "stopTimer")}, " +
            $"chase={Read<Vector2>(chase, "chaseVelocity")}, towardSpeed={TowardSpeed()}, " +
            $"bodyVelocity={body.linearVelocity}, knockback={Read<Vector2>(chase, "knockbackVelocity")}, " +
            $"worldExternal={Read<Vector2>(chase, "worldRuleExternalVelocity")}, " +
            $"anomalyExternal={typeof(EnemyMovement).GetProperty("AnomalyExternalVelocity", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(chase)}, " +
            $"response={Read<float>(chase, "threatResponseSeconds")}, distance={Vector2.Distance(body.position, playerBody.position)}, " +
            $"frozen={EnemyDebugAiFreeze.IsFrozen}, scale={Time.timeScale}";
        while (TowardSpeed() <= 0f && Time.realtimeSinceStartup < deadline)
            yield return null;
        Assert.That(TowardSpeed(), Is.GreaterThan(0f), "Natural chase turn timed out: " + Diagnostic());
        Vector2 start = body.position;
        Vector2 towardPlayer = (playerBody.position - start).normalized;
        float startDistance = Vector2.Distance(start, playerBody.position);
        bool Approached() => Vector2.Dot(body.position - start, towardPlayer) > .02f &&
            startDistance - Vector2.Distance(body.position, playerBody.position) > .02f;
        while (!Approached() && Time.realtimeSinceStartup < deadline)
            yield return null;
        Assert.That(Approached(), Is.True, "Natural pursuit did not close distance after turning: " + Diagnostic());
        Assert.That(Read<float>(chase, "woundedRetreatRemaining"), Is.Zero, "Pursuit is measured after retreat.");
    }

    [UnityTest, Timeout(120000)]
    public IEnumerator RecoilingShooterProductionMvpF1GameViewCapture()
    {
        typeof(BunkerStationPresentationTests).GetMethod("SetResolution", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, null);
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
        yield return new EnterPlayMode();
        yield return RecoilingShooterProductionCaptureBody();
    }

    private static IEnumerator RecoilingShooterProductionCaptureBody()
    {
        yield return BeginProductionRun(); // Ordinary station, spawning, AI and physics remain live.
        yield return CoreTestSupport.Await(() => Object.FindFirstObjectByType<Subject42DebugMenu>() != null);
        var player = PlayerRuntimeReference.CachedPlayer;
        var station = Object.FindFirstObjectByType<OrbitalStationRuntime>();
        var spawner = Object.FindFirstObjectByType<EnemySpawner>();
        var menu = Object.FindFirstObjectByType<Subject42DebugMenu>();
        Assert.That(player, Is.Not.Null);
        Assert.That(station, Is.Not.Null);
        Assert.That(spawner, Is.Not.Null);
        var health = player.GetComponent<PlayerHealth>();
        var controller = player.GetComponent<CharacterMovement2D>();
        var playerBody = player.GetComponent<Rigidbody2D>();
        var intent = controller.MovementIntent;
        var constraints = playerBody.constraints;
        var originalPosition = playerBody.position;
        bool invulnerable = health.DebugInvulnerable;
        bool frozen = EnemyDebugAiFreeze.IsFrozen;
        float scale = Time.timeScale;
        bool experiment = spawner.RecoilingShootersExperiment;
        GameObject actor = null;
        EnemyShooterMovement shooter = null;
        Transform visual = null;
        Quaternion authored = Quaternion.identity;
        Quaternion rootRotation = Quaternion.identity;
        bool ownsActor = false;
        bool Visible(Vector3 position)
        {
            var camera = Camera.main;
            if (camera == null || !camera.isActiveAndEnabled) return false;
            var v = camera.WorldToViewportPoint(position);
            return v.z > 0f && v.x > .08f && v.x < .92f && v.y > .08f && v.y < .92f;
        }
        void AssertLive()
        {
            Assert.That(actor != null && actor.activeInHierarchy, Is.True, "F1-owned recoiling shooter must survive this short observation.");
            Assert.That(actor.GetComponent<EnemyHealth>().IsDead, Is.False);
            Assert.That(shooter.isActiveAndEnabled && shooter.RecoilingExperiment, Is.True);
            var owned = Read<System.Collections.Generic.List<GameObject>>(menu, "debugEnemies");
            Assert.That(owned.Count, Is.EqualTo(1));
            Assert.That(owned[0], Is.SameAs(actor));
            Assert.That(station.isActiveAndEnabled && station.IsInitialized, Is.True);
            Assert.That(spawner.isActiveAndEnabled && spawner.IsSpawningEnabled, Is.True);
            Assert.That(playerBody.simulated && actor.GetComponent<Rigidbody2D>().simulated, Is.True);
            Assert.That(controller.isActiveAndEnabled && !health.IsDead, Is.True);
            Assert.That(EnemyDebugAiFreeze.IsFrozen || Subject42DebugMenu.IsDebugMenuOpen, Is.False);
            Assert.That(Read<bool>(menu, "isOpen"), Is.False);
            Assert.That(Time.timeScale, Is.GreaterThan(0f));
            Assert.That(RunStateManager.Instance.IsDevelopmentRun, Is.True);
            Assert.That(Visible(actor.transform.position), Is.True, "Need a living visible recoiling shooter in the real Game View.");
            Assert.That(Quaternion.Angle(rootRotation, actor.transform.rotation), Is.LessThan(.001f));
        }
        try
        {
            health.DebugInvulnerable = true;
            EnemyDebugAiFreeze.SetFrozen(false);
            controller.MovementIntent = () => Vector2.zero;
            OpenEscortF1(menu);
            Assert.That(spawner.RecoilingShootersExperiment, Is.False, "Fresh MVP default OFF.");
            var tracked = Read<System.Collections.Generic.List<GameObject>>(menu, "debugEnemies");
            Assert.That(tracked, Is.Empty, "Do not take ownership of another diagnostic actor.");
            var profile = RunStateManager.Instance.CurrentSector.SpawnProfile;
            var prefab = profile.Phases.Where(p => p != null && p.enemies != null).SelectMany(p => p.enemies)
                .Where(e => e?.enemyPrefab != null).Select(e => e.enemyPrefab)
                .FirstOrDefault(EnemySpawner.IsRecoilingShooterPrefabEligible);
            Assert.That(prefab, Is.Not.Null, "Current sector needs an authored eligible shooter; no replacement prefab.");
            var toggle = EscortF1Button(menu, "ПЕРЕКЛЮЧИТЬ", "Эксперимент: стрелок с отходом");
            Assert.That(toggle.interactable, Is.True);
            toggle.onClick.Invoke();
            Assert.That(spawner.RecoilingShootersExperiment, Is.True);
            Assert.That(RunStateManager.Instance.IsDevelopmentRun, Is.True, "Development must precede manual assignment.");
            var spawn = EscortF1Button(menu, "+1 С ОТХОДОМ");
            Assert.That(spawn.interactable, Is.True);
            spawn.onClick.Invoke();
            ownsActor = tracked.Count == 1;
            Assert.That(tracked.Count, Is.EqualTo(1), Read<string>(menu, "enemyDebugStatus"));
            actor = tracked.Single();
            Assert.That(actor.name, Is.EqualTo(prefab.name + "(Clone)"), "F1 uses the eligible prefab from this sector.");
            shooter = actor.GetComponent<EnemyShooterMovement>();
            visual = Read<Transform>(shooter, "visualRoot");
            Assert.That(visual != actor.transform && visual.IsChildOf(actor.transform), Is.True);
            authored = visual.localRotation;
            rootRotation = actor.transform.rotation;
            Assert.That(Read<bool>(menu, "isOpen"), Is.False, "Successful real F1 callback closes the menu.");
            yield return null; // Natural Start binds the player before diagnostic placement.
            Assert.That(Read<Transform>(shooter, "player"), Is.EqualTo(player.transform));
            Assert.That(shooter.CanUseRecoiling && shooter.HasRecoilingVisual, Is.True);
            var body = actor.GetComponent<Rigidbody2D>();
            var area = GameplayAreaService.Instance;
            Assert.That(area, Is.Not.Null);
            float preferred = Read<float>(shooter, "preferredDistance");
            Vector2? destination = null;
            // Never move the enemy or change its combat parameters. Find a free player point.
            for (int i = 0; i < 24; i++)
            {
                float angle = i * Mathf.PI / 12f;
                Vector2 candidate = body.position + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * preferred;
                if (!area.IsInsidePlayableArea(candidate, .8f) || !area.IsInsideSpawnArea(candidate, .8f)) continue;
                bool clear = Physics2D.OverlapCircleAll(candidate, .8f).All(c => c.transform.IsChildOf(player.transform) ||
                    c == area.PlayableArea || c == area.SpawnArea ||
                    (c.isTrigger && c.GetComponentInParent<EnemyHealth>() == null));
                if (clear) { destination = candidate; break; }
            }
            Assert.That(destination.HasValue, Is.True, "No free authored-distance observation point; preserve enemy placement.");
            MoveEscortPlayer(playerBody, destination.Value);
            playerBody.constraints = RigidbodyConstraints2D.FreezeAll;
            string Phase() => Read<object>(shooter, "recoilPhase").ToString();
            float deadline = Time.realtimeSinceStartup + 8f;
            while (actor != null && Phase() != "Retreating" && Time.realtimeSinceStartup < deadline)
                yield return null; // Ordinary Update shoots; no fireTimer or phase writes.
            AssertLive();
            Assert.That(Phase(), Is.EqualTo("Retreating"), "A successful natural ordinary shot must start retreat.");
            Vector2 direction = Read<Vector2>(shooter, "recoilDirection");
            Vector2 retreatStart = body.position;
            yield return WaitGameTime(.12f);
            AssertLive();
            Assert.That(Phase(), Is.EqualTo("Retreating"));
            Assert.That(Vector2.Dot(body.position - retreatStart, direction), Is.GreaterThan(.02f),
                "Natural Rigidbody retreat progresses along the commitment, with ordinary crowd steering.");
            Assert.That(Read<Vector2>(shooter, "recoilDirection"), Is.EqualTo(direction));
            Assert.That(Quaternion.Angle(authored, visual.localRotation), Is.EqualTo(15f).Within(.02f));
            var retreat = CaptureFlankingGameView("recoil-retreat", "RecoilingShooter");
            Assert.That(retreat.MoveNext(), Is.True); // Request now, poll disk after both short phases.

            deadline = Time.realtimeSinceStartup + 2f;
            while (actor != null && Phase() == "Retreating" && Time.realtimeSinceStartup < deadline)
                yield return null;
            AssertLive();
            Assert.That(Phase(), Is.EqualTo("Recovering"));
            Assert.That(Quaternion.Angle(authored, visual.localRotation), Is.LessThan(.02f));
            yield return new WaitForFixedUpdate();
            Vector2 recoveryStart = body.position;
            yield return WaitGameTime(.1f);
            AssertLive();
            Assert.That(Phase(), Is.EqualTo("Recovering"));
            Assert.That(Vector2.Distance(body.position, recoveryStart), Is.LessThan(.03f),
                "Short natural recovery has no own locomotion; no diagnostic external forces are injected.");
            Assert.That(Quaternion.Angle(authored, visual.localRotation), Is.LessThan(.02f));
            var recovery = CaptureFlankingGameView("recoil-recovery", "RecoilingShooter");
            Assert.That(recovery.MoveNext(), Is.True);
            yield return recovery.Current;
            while (recovery.MoveNext()) yield return recovery.Current;
            while (retreat.MoveNext()) yield return retreat.Current;
        }
        finally
        {
            EscortF1Call(menu, "CloseMenu");
            if (ownsActor) EscortF1Call(menu, "ClearDebugEnemies");
            spawner.DebugSetRecoilingShootersExperiment(experiment);
            controller.MovementIntent = intent;
            MoveEscortPlayer(playerBody, originalPosition);
            playerBody.linearVelocity = Vector2.zero;
            playerBody.constraints = constraints;
            health.DebugInvulnerable = invulnerable;
            EnemyDebugAiFreeze.SetFrozen(frozen);
            Time.timeScale = scale;
        }
    }

    [UnityTest, Timeout(120000)]
    public IEnumerator InterceptingBomberProductionMvpF1GameViewCapture()
    {
        typeof(BunkerStationPresentationTests).GetMethod("SetResolution", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, null);
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
        yield return new EnterPlayMode();
        yield return InterceptingBomberProductionCaptureBody();
    }

    private static IEnumerator InterceptingBomberProductionCaptureBody()
    {
        yield return BeginProductionRun(); // Ordinary station, spawning, AI and physics remain live.
        yield return CoreTestSupport.Await(() => Object.FindFirstObjectByType<Subject42DebugMenu>() != null);
        var player = PlayerRuntimeReference.CachedPlayer;
        var station = Object.FindFirstObjectByType<OrbitalStationRuntime>();
        var spawner = Object.FindFirstObjectByType<EnemySpawner>();
        var menu = Object.FindFirstObjectByType<Subject42DebugMenu>();
        Assert.That(player, Is.Not.Null);
        Assert.That(station, Is.Not.Null);
        Assert.That(spawner, Is.Not.Null);
        var health = player.GetComponent<PlayerHealth>();
        var controller = player.GetComponent<CharacterMovement2D>();
        var playerBody = player.GetComponent<Rigidbody2D>();
        var intent = controller.MovementIntent;
        var constraints = playerBody.constraints;
        bool invulnerable = health.DebugInvulnerable;
        bool frozen = EnemyDebugAiFreeze.IsFrozen;
        float scale = Time.timeScale;
        bool experiment = spawner.InterceptingBombersExperiment;
        GameObject actor = null;
        EnemyBomberMovement bomber = null;
        Transform visual = null;
        Quaternion authored = Quaternion.identity;
        Quaternion rootRotation = Quaternion.identity;
        bool ownsActor = false;
        bool Visible(Vector3 position)
        {
            var camera = Camera.main;
            if (camera == null || !camera.isActiveAndEnabled) return false;
            var v = camera.WorldToViewportPoint(position);
            return v.z > 0f && v.x > .08f && v.x < .92f && v.y > .08f && v.y < .92f;
        }
        void AssertLive()
        {
            Assert.That(actor != null && actor.activeInHierarchy, Is.True, "F1-owned interceptor must survive this short observation.");
            Assert.That(actor.GetComponent<EnemyHealth>().IsDead, Is.False);
            Assert.That(bomber.isActiveAndEnabled && bomber.InterceptingExperiment, Is.True);
            Assert.That(Read<bool>(bomber, "isExploding"), Is.False, "Observation stays outside the authored trigger.");
            var owned = Read<System.Collections.Generic.List<GameObject>>(menu, "debugEnemies");
            Assert.That(owned.Count, Is.EqualTo(1));
            Assert.That(owned[0], Is.SameAs(actor));
            Assert.That(station.isActiveAndEnabled && station.IsInitialized, Is.True);
            Assert.That(spawner.isActiveAndEnabled && spawner.IsSpawningEnabled, Is.True);
            Assert.That(playerBody.simulated && actor.GetComponent<Rigidbody2D>().simulated, Is.True);
            Assert.That(controller.isActiveAndEnabled && !health.IsDead, Is.True);
            Assert.That(EnemyDebugAiFreeze.IsFrozen || Subject42DebugMenu.IsDebugMenuOpen, Is.False);
            Assert.That(Read<bool>(menu, "isOpen"), Is.False);
            Assert.That(Time.timeScale, Is.GreaterThan(0f));
            Assert.That(RunStateManager.Instance.IsDevelopmentRun, Is.True);
            Assert.That(Visible(actor.transform.position), Is.True, "Need a living visible interceptor in the real Game View.");
            Assert.That(Quaternion.Angle(rootRotation, actor.transform.rotation), Is.LessThan(.001f));
            float tilt = Quaternion.Angle(authored, visual.localRotation);
            Assert.That(tilt, Is.GreaterThan(.01f).And.LessThanOrEqualTo(20.001f));
        }
        try
        {
            health.DebugInvulnerable = true;
            EnemyDebugAiFreeze.SetFrozen(false);
            controller.MovementIntent = () => Vector2.zero;
            OpenEscortF1(menu);
            Assert.That(spawner.InterceptingBombersExperiment, Is.False, "Fresh MVP default OFF.");
            var tracked = Read<System.Collections.Generic.List<GameObject>>(menu, "debugEnemies");
            Assert.That(tracked, Is.Empty, "Do not take ownership of another diagnostic actor.");
            var profile = RunStateManager.Instance.CurrentSector.SpawnProfile;
            var prefab = profile.Phases.Where(p => p != null && p.enemies != null).SelectMany(p => p.enemies)
                .Where(e => e?.enemyPrefab != null).Select(e => e.enemyPrefab)
                .FirstOrDefault(EnemySpawner.IsInterceptingBomberPrefabEligible);
            Assert.That(prefab, Is.Not.Null, "Current sector needs an authored eligible bomber; no replacement prefab.");
            var toggle = EscortF1Button(menu, "ПЕРЕКЛЮЧИТЬ", "Эксперимент: подрывник-перехватчик");
            Assert.That(toggle.interactable, Is.True);
            toggle.onClick.Invoke();
            Assert.That(spawner.InterceptingBombersExperiment, Is.True);
            Assert.That(RunStateManager.Instance.IsDevelopmentRun, Is.True, "Development must precede manual assignment.");
            var spawn = EscortF1Button(menu, "+1 ПЕРЕХВАТЧИК");
            Assert.That(spawn.interactable, Is.True);
            spawn.onClick.Invoke();
            ownsActor = tracked.Count == 1;
            Assert.That(tracked.Count, Is.EqualTo(1), Read<string>(menu, "enemyDebugStatus"));
            actor = tracked.Single();
            Assert.That(actor.name, Is.EqualTo(prefab.name + "(Clone)"), "F1 uses the eligible prefab from this sector.");
            bomber = actor.GetComponent<EnemyBomberMovement>();
            visual = Read<Transform>(bomber, "visualRoot");
            Assert.That(visual != actor.transform && visual.IsChildOf(actor.transform), Is.True);
            authored = Read<Quaternion>(bomber, "interceptingAuthoredRotation");
            rootRotation = actor.transform.rotation;
            Assert.That(Read<bool>(menu, "isOpen"), Is.False, "Successful real F1 callback closes the menu.");
            var body = actor.GetComponent<Rigidbody2D>();
            var area = GameplayAreaService.Instance;
            Assert.That(area, Is.Not.Null);
            Vector2? destination = null;
            // Only the player is relocated, before observation; preserve enemy placement and all combat parameters.
            for (int i = 0; i < 24; i++)
            {
                float angle = i * Mathf.PI / 12f;
                Vector2 candidate = body.position + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * 6f;
                if (!Visible(candidate) || !area.IsInsidePlayableArea(candidate, .8f) || !area.IsInsideSpawnArea(candidate, .8f)) continue;
                bool clear = Physics2D.OverlapCircleAll(candidate, .8f).All(c => c.transform.IsChildOf(player.transform) ||
                    c == area.PlayableArea || c == area.SpawnArea ||
                    (c.isTrigger && c.GetComponentInParent<EnemyHealth>() == null));
                if (clear) { destination = candidate; break; }
            }
            Assert.That(destination.HasValue, Is.True, "No safe visible player observation point; keep production placement intact.");
            MoveEscortPlayer(playerBody, destination.Value);
            playerBody.constraints = RigidbodyConstraints2D.FreezeRotation;
            Vector2 radial = (playerBody.position - body.position).normalized;
            Vector2 tangent = new Vector2(-radial.y, radial.x);
            controller.MovementIntent = () => tangent;
            Vector2 playerStart = playerBody.position;
            Vector2 enemyStart = body.position;
            yield return WaitGameTime(.2f);
            yield return new WaitForEndOfFrame();
            AssertLive();
            Assert.That(Read<Transform>(bomber, "player"), Is.EqualTo(player.transform));
            Assert.That(Vector2.Dot(playerBody.position - playerStart, tangent), Is.GreaterThan(.05f));
            Assert.That(Vector2.Distance(enemyStart, body.position), Is.GreaterThan(.05f));
            Vector2 approachHeading = (body.position - enemyStart).normalized;
            float firstTilt = Mathf.DeltaAngle(0f, (Quaternion.Inverse(authored) * visual.localRotation).eulerAngles.z);
            var first = CaptureFlankingGameView("intercept-approach", "InterceptingBomber");
            Assert.That(first.MoveNext(), Is.True);
            yield return first.Current; // Request now; defer disk polling until both short live observations finish.

            controller.MovementIntent = () => -tangent;
            Vector2 beforeTurn = body.position;
            yield return WaitGameTime(.3f); // Real controller accelerates through zero; no manual FixedUpdate/calculation.
            Vector2 reverseSample = playerBody.position;
            yield return WaitGameTime(.1f);
            yield return new WaitForEndOfFrame();
            AssertLive();
            Assert.That(Vector2.Dot(playerBody.position - reverseSample, tangent), Is.LessThan(-.01f),
                "Completed player displacement must reverse; MovePosition velocity may be cleared after physics.");
            Assert.That(Vector2.Distance(beforeTurn, body.position), Is.GreaterThan(.05f));
            Assert.That(Vector2.Angle(approachHeading, body.position - beforeTurn), Is.GreaterThan(.1f),
                "Observe a natural change in the interceptor's travel heading as well as its visual pose.");
            float secondTilt = Mathf.DeltaAngle(0f, (Quaternion.Inverse(authored) * visual.localRotation).eulerAngles.z);
            Assert.That(firstTilt * secondTilt, Is.LessThan(0f), "Natural lead/visual response must change side after player reversal.");
            var second = CaptureFlankingGameView("intercept-direction-change", "InterceptingBomber");
            Assert.That(second.MoveNext(), Is.True);
            yield return second.Current;
            while (second.MoveNext()) yield return second.Current;
            while (first.MoveNext()) yield return first.Current;
        }
        finally
        {
            EscortF1Call(menu, "CloseMenu");
            // A death-triggered sequence can outlive its Unity owner. Match the managed owner reference,
            // including a destroyed wrapper, so unrelated ordinary bomber warnings are never cancelled.
            var sequenceType = typeof(EnemyBomberMovement).Assembly.GetType("BomberExplosionSequence");
            if (!ReferenceEquals(actor, null))
                foreach (Component sequence in Resources.FindObjectsOfTypeAll(sequenceType))
                    if (ReferenceEquals(Read<GameObject>(sequence, "owner"), actor))
                        sequenceType.GetMethod("Cancel", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(sequence, null);
            if (ownsActor) EscortF1Call(menu, "ClearDebugEnemies");
            spawner.DebugSetInterceptingBombersExperiment(experiment);
            controller.MovementIntent = intent;
            playerBody.linearVelocity = Vector2.zero;
            playerBody.constraints = constraints;
            health.DebugInvulnerable = invulnerable;
            EnemyDebugAiFreeze.SetFrozen(frozen);
            Time.timeScale = scale;
        }
    }

    [UnityTest, Timeout(120000)]
    public IEnumerator InterceptingBomberPredictionAndExplosionBoundary()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
        yield return new EnterPlayMode();
        yield return InterceptingBomberPredictionBody();
    }

    private static IEnumerator InterceptingBomberPredictionBody()
    {
        yield return BeginProductionRun();
        var originalConstraints = PlayerRuntimeReference.CachedPlayer.GetComponent<Rigidbody2D>().constraints;
        var player = PrepareProduction();
        var station = Object.FindFirstObjectByType<OrbitalStationRuntime>();
        bool stationEnabled = station.enabled;
        bool frozen = EnemyDebugAiFreeze.IsFrozen;
        float scale = Time.timeScale;
        var health = player.GetComponent<PlayerHealth>();
        bool invulnerable = health.DebugInvulnerable;
        var controller = player.GetComponent<CharacterMovement2D>();
        var intent = controller.MovementIntent;
        var playerBody = player.GetComponent<Rigidbody2D>();
        var actor = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/_Project/prefabs/Enemies/p_Enemy_Bomber.prefab"), player.transform.position + Vector3.left * 7f, Quaternion.identity);
        var bomber = actor.GetComponent<EnemyBomberMovement>();
        var body = actor.GetComponent<Rigidbody2D>();
        var visual = Read<Transform>(bomber, "visualRoot");
        Quaternion authored = visual.localRotation;
        Quaternion rootRotation = actor.transform.rotation;
        Component sequence = null;
        var calculate = typeof(EnemyBomberMovement).GetMethod("GetInterceptingTarget", BindingFlags.Instance | BindingFlags.NonPublic);
        Vector2 Predict(Vector2 target, Vector2 offset, Vector2 velocity) =>
            (Vector2)calculate.Invoke(bomber, new object[] { target, offset, velocity });
        var applyPose = typeof(EnemyBomberMovement).GetMethod("ApplyInterceptingPose", BindingFlags.Instance | BindingFlags.NonPublic);
        void Pose(Vector2 offset, Vector2 lead) => applyPose.Invoke(bomber, new object[] { offset, lead });
        try
        {
            station.enabled = false;
            health.DebugInvulnerable = true;
            EnemyDebugAiFreeze.SetFrozen(true);
            Time.timeScale = 1f;
            controller.MovementIntent = () => Vector2.zero;
            yield return null; // Start resolves the production player; never invoke lifecycle by reflection.
            Assert.That(Read<Transform>(bomber, "player"), Is.EqualTo(player.transform));
            Assert.That(visual != actor.transform && visual.IsChildOf(actor.transform), Is.True);
            Assert.That(bomber.InterceptingExperiment, Is.False, "Public default OFF.");
            Vector2 target = playerBody.position;
            Vector2 testVelocity = Vector2.up * 2f;
            Assert.That(Predict(target, Vector2.right * 8f, testVelocity), Is.EqualTo(target));
            bomber.ConfigureInterceptingExperiment(true);
            Assert.That(bomber.InterceptingExperiment, Is.True);
            Assert.That(RunStateManager.Instance.IsDevelopmentRun, Is.True);
            // Pure numerical boundaries use explicit velocity, never transient MovePosition velocity.
            testVelocity = Vector2.zero;
            Assert.That(Predict(target, Vector2.right * 8f, testVelocity), Is.EqualTo(target));
            testVelocity = Vector2.up * 2f;
            Assert.That(Vector2.Distance(Predict(target, Vector2.right * 8f, testVelocity), target + Vector2.up * 1.1f), Is.LessThan(.0001f));
            testVelocity = Vector2.up * 100f;
            var lead = Predict(target, Vector2.right * 8f, testVelocity) - target;
            Assert.That(lead.magnitude, Is.EqualTo(1.75f).Within(.0001f));
            float radius = Read<float>(bomber, "triggerRadius");
            Assert.That(Predict(target, Vector2.right * (radius + .5f), testVelocity), Is.EqualTo(target));
            float middle = (Predict(target, Vector2.right * (radius + 1.25f), testVelocity) - target).magnitude;
            Assert.That(middle, Is.EqualTo(.875f).Within(.0001f), "Halfway SmoothStep weight.");
            Pose(Vector2.right * (radius + 1.25f), Vector2.up * middle);
            var pose = visual.localRotation;
            for (int i = 0; i < 12; i++) Pose(Vector2.right * (radius + 1.25f), Vector2.up * middle);
            Assert.That(Quaternion.Angle(pose, visual.localRotation), Is.LessThan(.0001f), "No accumulated tilt.");
            Assert.That(Quaternion.Angle(authored, pose), Is.LessThanOrEqualTo(20.001f));
            // Exercise the actual 20 degree clamp as well.
            Pose(Vector2.right * (radius + 2f), Predict(target, Vector2.right * (radius + 2f), testVelocity) - target);
            Assert.That(Quaternion.Angle(authored, visual.localRotation), Is.EqualTo(20f).Within(.001f), "Authored trigger band reaches the tilt clamp.");
            bomber.ConfigureInterceptingExperiment(false);
            Assert.That(visual.localRotation, Is.EqualTo(authored));
            bomber.ConfigureInterceptingExperiment(true);
            Pose(Vector2.right * 8f, Predict(target, Vector2.right * 8f, testVelocity) - target);
            bomber.enabled = false;
            Assert.That(visual.localRotation, Is.EqualTo(authored));
            Assert.That(bomber.InterceptingExperiment, Is.False);
            bomber.enabled = true;
            Assert.That(bomber.InterceptingExperiment, Is.False);

            // Sustained intent runs the real controller, physics and enemy FixedUpdate.
            playerBody.linearVelocity = Vector2.zero;
            playerBody.constraints = RigidbodyConstraints2D.FreezeRotation;
            controller.MovementIntent = () => Vector2.up;
            bomber.ConfigureInterceptingExperiment(true);
            EnemyDebugAiFreeze.SetFrozen(false);
            Vector2 playerStart = playerBody.position;
            Vector2 enemyStart = body.position;
            yield return WaitGameTime(.2f);
            Assert.That(Vector2.Distance(playerStart, playerBody.position), Is.GreaterThan(.05f), "Real controller must move the player.");
            Assert.That(Quaternion.Angle(authored, visual.localRotation), Is.GreaterThan(.01f), "Sustained real movement must produce lead during enemy FixedUpdate; post-simulation MovePosition velocity may already be cleared.");
            Assert.That(Read<Vector2>(bomber, "interceptingPlayerVelocity").y, Is.GreaterThan(.1f),
                "Completed physical displacement supplies real upward velocity.");
            Assert.That(Vector2.Distance(enemyStart, body.position), Is.GreaterThan(.05f), "Enemy moves through natural FixedUpdate.");
            Assert.That(Quaternion.Angle(authored, visual.localRotation), Is.LessThanOrEqualTo(20.001f));
            Assert.That(Quaternion.Angle(rootRotation, actor.transform.rotation), Is.LessThan(.001f), "Physics root never tilts.");

            bomber.ConfigureInterceptingExperiment(false);
            Assert.That(Read<Vector2>(bomber, "interceptingPlayerVelocity"), Is.EqualTo(Vector2.zero));
            Assert.That(visual.localRotation, Is.EqualTo(authored));
            bomber.ConfigureInterceptingExperiment(true);
            Assert.That(Read<Vector2>(bomber, "interceptingPlayerVelocity"), Is.EqualTo(Vector2.zero));
            EnemyDebugAiFreeze.SetFrozen(true);
            controller.MovementIntent = () => Vector2.zero;
            yield return WaitGameTime(.3f); // Let the controller decelerate before positioning the boundary.
            playerBody.constraints = RigidbodyConstraints2D.FreezeAll;
            playerBody.linearVelocity = Vector2.zero;
            target = playerBody.position;
            body.position = target + Vector2.left * (radius + 1.75f);
            body.linearVelocity = Vector2.zero;
            Physics2D.SyncTransforms();
            // A predicted point inside the trigger while the player is outside is unreachable
            // with this cap/fade. Check the closest reachable region without changing the pipeline.
            testVelocity = Vector2.left * 100f;
            Vector2 predicted = Predict(target, target - body.position, testVelocity);
            Assert.That(Vector2.Distance(predicted, body.position), Is.GreaterThan(radius));
            Assert.That(Vector2.Distance(predicted, body.position), Is.LessThan(Vector2.Distance(target, body.position)));
            playerBody.linearVelocity = Vector2.zero;
            EnemyDebugAiFreeze.SetFrozen(false);
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();
            Assert.That(Read<bool>(bomber, "isExploding"), Is.False, "Real player remains outside trigger.");
            body.position = playerBody.position + Vector2.left * (radius * .5f);
            body.linearVelocity = Vector2.zero;
            Physics2D.SyncTransforms();
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();
            Assert.That(Read<bool>(bomber, "isExploding"), Is.True, "Actual proximity starts the authored explosion.");
            sequence = Read<Component>(bomber, "activeExplosionSequence");
            Assert.That(sequence, Is.Not.Null);
            bomber.ConfigureInterceptingExperiment(false);
            Assert.That(Read<Component>(bomber, "activeExplosionSequence"), Is.SameAs(sequence));
            Assert.That(sequence.gameObject.activeInHierarchy, Is.True, "OFF preserves the existing warning/sequence.");
            bomber.ConfigureInterceptingExperiment(true);
            Assert.That(bomber.InterceptingExperiment, Is.False, "An exploding actor rejects assignment.");
            Assert.That(visual.localRotation, Is.EqualTo(authored));
        }
        finally
        {
            if (actor != null && sequence == null) sequence = Read<Component>(bomber, "activeExplosionSequence");
            if (sequence != null) sequence.GetType().GetMethod("Cancel", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(sequence, null);
            if (actor != null) Object.DestroyImmediate(actor);
            controller.MovementIntent = intent;
            playerBody.linearVelocity = Vector2.zero;
            playerBody.constraints = originalConstraints;
            health.DebugInvulnerable = invulnerable;
            station.enabled = stationEnabled;
            EnemyDebugAiFreeze.SetFrozen(frozen);
            Time.timeScale = scale;
        }
    }

    [UnityTest, Timeout(120000)]
    public IEnumerator InterceptingBomberOptInCadenceAndLifecycle()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
        yield return new EnterPlayMode();
        yield return InterceptingBomberLifecycleBody();
    }

    private static IEnumerator InterceptingBomberLifecycleBody()
    {
        yield return BeginProductionRun();
        var originalConstraints = PlayerRuntimeReference.CachedPlayer.GetComponent<Rigidbody2D>().constraints;
        var player = PrepareProduction();
        var station = Object.FindFirstObjectByType<OrbitalStationRuntime>();
        var spawner = Object.FindFirstObjectByType<EnemySpawner>();
        bool stationEnabled = station.enabled;
        bool spawnerEnabled = spawner.enabled;
        bool frozen = EnemyDebugAiFreeze.IsFrozen;
        float scale = Time.timeScale;
        var actors = new System.Collections.Generic.List<GameObject>();
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/prefabs/Enemies/p_Enemy_Bomber.prefab");
        var spawn = typeof(EnemySpawner).GetMethod("SpawnEnemyAt", BindingFlags.Instance | BindingFlags.NonPublic);
        EnemyBomberMovement Spawn(bool ordinary)
        {
            var actor = (GameObject)spawn.Invoke(spawner, new object[] {
                prefab, player.transform.position + Vector3.left * (12f + actors.Count * 2f), ordinary });
            Assert.That(actor, Is.Not.Null);
            actors.Add(actor);
            return actor.GetComponent<EnemyBomberMovement>();
        }
        EnemyBomberMovement Manual()
        {
            var actor = spawner.DebugSpawnInterceptingBomberAround(prefab, player.transform.position, out string status);
            Assert.That(actor, Is.Not.Null, status);
            actors.Add(actor);
            Assert.That(actor.GetComponent<EnemyBomberMovement>().InterceptingExperiment, Is.True);
            return actor.GetComponent<EnemyBomberMovement>();
        }
        try
        {
            station.enabled = false;
            EnemyDebugAiFreeze.SetFrozen(true);
            Time.timeScale = 1f;
            Assert.That(spawner.InterceptingBombersExperiment, Is.False);
            Assert.That(EnemySpawner.IsInterceptingBomberPrefabEligible(prefab), Is.True, "Serialized prefab eligibility needs no Awake.");
            Assert.That(EnemySpawner.IsInterceptingBomberEligible(prefab), Is.False, "Prefab is not a ready runtime actor.");
            var ready = Spawn(true);
            Assert.That(ready.InterceptingExperiment, Is.False);
            Assert.That(EnemySpawner.IsInterceptingBomberEligible(ready.gameObject), Is.True);
            Assert.That(Read<int>(spawner, "interceptingBomberEligibleSpawns"), Is.Zero);
            spawner.DebugSetInterceptingBombersExperiment(true);
            Assert.That(RunStateManager.Instance.IsDevelopmentRun, Is.True, "Development before assignment.");
            Assert.That(Spawn(true).InterceptingExperiment, Is.False);
            Assert.That(Spawn(false).InterceptingExperiment, Is.False, "Special spawn does not join cadence.");
            var manual = Manual();
            Assert.That(Read<int>(spawner, "interceptingBomberEligibleSpawns"), Is.EqualTo(1));
            Assert.That(Read<System.Collections.Generic.List<EnemyBomberMovement>>(spawner, "interceptingBombers"), Does.Contain(manual));
            Assert.That(Spawn(true).InterceptingExperiment, Is.False);
            Assert.That(Spawn(true).InterceptingExperiment, Is.False);
            var fourth = Spawn(true);
            Assert.That(fourth.InterceptingExperiment, Is.True, "Fourth eligible ordinary bomber.");
            spawner.DebugSetInterceptingBombersExperiment(false);
            Assert.That(manual.InterceptingExperiment || fourth.InterceptingExperiment, Is.False);
            Assert.That(Read<int>(spawner, "interceptingBomberEligibleSpawns"), Is.Zero);
            Assert.That(Read<System.Collections.Generic.List<EnemyBomberMovement>>(spawner, "interceptingBombers"), Is.Empty);
            spawner.DebugSetInterceptingBombersExperiment(true);
            for (int i = 0; i < 3; i++) Assert.That(Spawn(true).InterceptingExperiment, Is.False);
            fourth = Spawn(true);
            Assert.That(fourth.InterceptingExperiment, Is.True, "OFF reset the cadence.");
            manual = Manual();
            spawner.enabled = false;
            Assert.That(manual.gameObject.activeInHierarchy && fourth.gameObject.activeInHierarchy, Is.True);
            Assert.That(manual.InterceptingExperiment || fourth.InterceptingExperiment, Is.False);
            Assert.That(spawner.InterceptingBombersExperiment, Is.False);
            Assert.That(Read<int>(spawner, "interceptingBomberEligibleSpawns"), Is.Zero);
            Assert.That(Read<System.Collections.Generic.List<EnemyBomberMovement>>(spawner, "interceptingBombers"), Is.Empty);
            spawner.enabled = true;
            spawner.StopSpawning();
            Assert.That(spawner.InterceptingBombersExperiment, Is.False, "Re-enable never restores opt-in.");
            Assert.That(RunStateManager.Instance.IsDevelopmentRun, Is.True);
        }
        finally
        {
            spawner.DebugSetInterceptingBombersExperiment(false);
            foreach (var actor in actors) if (actor != null) Object.DestroyImmediate(actor);
            spawner.enabled = spawnerEnabled;
            spawner.StopSpawning();
            station.enabled = stationEnabled;
            player.GetComponent<Rigidbody2D>().constraints = originalConstraints;
            EnemyDebugAiFreeze.SetFrozen(frozen);
            Time.timeScale = scale;
        }
    }

    [UnityTest, Timeout(120000)]
    public IEnumerator FireteamClearanceProductionMvpF1GameViewCapture()
    {
        typeof(BunkerStationPresentationTests).GetMethod("SetResolution", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, null);
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
        yield return new EnterPlayMode();
        yield return FireteamClearanceProductionCaptureBody();
    }

    private static IEnumerator FireteamClearanceProductionCaptureBody()
    {
        yield return BeginProductionRun(); // Keep station, ordinary spawning, AI, camera and physics alive.
        yield return AwaitEscortProductionReady();
        var player = PlayerRuntimeReference.CachedPlayer;
        var health = player.GetComponent<PlayerHealth>();
        var body = player.GetComponent<Rigidbody2D>();
        var station = Object.FindFirstObjectByType<OrbitalStationRuntime>();
        var enemies = Object.FindFirstObjectByType<EnemySpawner>();
        var events = Object.FindFirstObjectByType<WorldEventSpawner>();
        var menu = Object.FindFirstObjectByType<Subject42DebugMenu>();
        Assert.That(menu, Is.Not.Null);
        Assert.That(enemies, Is.Not.Null);
        Assert.That(station, Is.Not.Null);
        bool protection = health.DebugInvulnerable, frozen = EnemyDebugAiFreeze.IsFrozen;
        bool experiment = events.FireteamClearanceExperimentEnabled;
        float scale = Time.timeScale;
        Vector3 originalPosition = player.transform.position;
        FireteamClearanceEvent clearance = null;
        EnemyHealth[] targets = null;
        bool Visible(EnemyHealth target)
        {
            if (target == null || target.IsDead || !target.isActiveAndEnabled || Camera.main == null) return false;
            var v = Camera.main.WorldToViewportPoint(target.transform.position);
            return v.z > 0f && v.x > .08f && v.x < .92f && v.y > .08f && v.y < .92f;
        }
        void AssertLive(int kills)
        {
            Assert.That(station.isActiveAndEnabled && station.IsInitialized, Is.True);
            Assert.That(enemies.isActiveAndEnabled && enemies.IsSpawningEnabled, Is.True, "Ordinary battle must remain running.");
            Assert.That(body.simulated && !health.IsDead, Is.True);
            Assert.That(Camera.main != null && Camera.main.isActiveAndEnabled, Is.True);
            Assert.That(EnemyDebugAiFreeze.IsFrozen || Subject42DebugMenu.IsDebugMenuOpen, Is.False);
            Assert.That(Read<bool>(menu, "isOpen"), Is.False);
            Assert.That(Time.timeScale, Is.GreaterThan(0f));
            Assert.That(RunStateManager.Instance.IsDevelopmentRun, Is.True);
            Assert.That(clearance != null && clearance.IsStarted && !clearance.IsCompleted, Is.True);
            Assert.That(events.ActiveEvent, Is.SameAs(clearance));
            Assert.That(events.FireteamClearanceExperimentInstance, Is.SameAs(clearance));
            Assert.That(clearance.ConfirmedKills, Is.EqualTo(kills));
            var living = targets.Where(t => t != null && !t.IsDead && t.isActiveAndEnabled).ToArray();
            Assert.That(living.Length, Is.EqualTo(3 - kills));
            foreach (var target in living)
            {
                Assert.That(target.GetComponent<EnemyShooterMovement>().isActiveAndEnabled, Is.True);
                Assert.That(target.GetComponent<Rigidbody2D>().simulated, Is.True);
            }
            Assert.That(living.Any(Visible), Is.True, "No visible owned target in the live production camera; do not relocate targets.");
            var guidance = clearance.GetObjectiveGuidance();
            Assert.That(guidance.Completed, Is.EqualTo(kills));
            Assert.That(guidance.Total, Is.EqualTo(3));
            Assert.That(guidance.DescriptionKey, Does.Contain(kills + "/3"));
            Assert.That(guidance.DescriptionKey, Does.Contain(Mathf.CeilToInt(clearance.RemainingSeconds) + " с"));
            Assert.That(living.Any(t => Vector2.Distance(t.transform.position, guidance.Target) < .01f), Is.True);
            var markers = new System.Collections.Generic.List<TacticalMapMarkerDescriptor>();
            clearance.CollectTacticalMapMarkers(markers);
            Assert.That(markers.Count, Is.EqualTo(3 - kills));
            foreach (var marker in markers)
            {
                Assert.That(marker.Kind, Is.EqualTo(TacticalMapMarkerKind.Target));
                Assert.That(living.Any(t => Vector2.Distance(t.transform.position, marker.Position) < .01f), Is.True);
            }
            Assert.That(Read<WorldEventMarker[]>(clearance, "indicators").Count(m => m != null), Is.EqualTo(3 - kills));
        }
        try
        {
            health.DebugInvulnerable = true;
            EnemyDebugAiFreeze.SetFrozen(false);
            Time.timeScale = 1f;
            Assert.That(experiment, Is.False, "Fresh production attempts default OFF.");
            OpenEscortF1(menu);
            Assert.That(Read<bool>(menu, "isOpen"), Is.True);
            var toggle = EscortF1Button(menu, "ПЕРЕКЛЮЧИТЬ", "Зачистка огневой группы");
            Assert.That(toggle.interactable, Is.True);
            toggle.onClick.Invoke();
            Assert.That(events.FireteamClearanceExperimentEnabled, Is.True, Read<string>(menu, "enemyDebugStatus"));
            Assert.That(RunStateManager.Instance.IsDevelopmentRun, Is.True, "Opt-in marks Development before creation.");
            Assert.That(events.FireteamClearanceExperimentInstance, Is.Null);
            var spawn = EscortF1Button(menu, "СОЗДАТЬ ЗАЧИСТКУ");
            Assert.That(spawn.interactable, Is.True);
            spawn.onClick.Invoke();
            clearance = events.FireteamClearanceExperimentInstance;
            Assert.That(clearance, Is.Not.Null, Read<string>(menu, "enemyDebugStatus"));
            Assert.That(clearance.SourcePrefab, Is.SameAs(Read<FireteamClearanceEvent>(events, "fireteamClearanceEventPrefab")));
            Assert.That(clearance.TryValidateConfiguration(out var status), Is.True, status);
            Assert.That(Read<bool>(menu, "isOpen"), Is.False, "Successful real F1 callback closes the menu.");
            Assert.That(clearance.IsStarted, Is.False, "Creation must not start the event automatically.");
            Assert.That(Read<EnemyHealth[]>(clearance, "targets").All(t => t == null), Is.True);
            MoveEscortPlayer(body, clearance.transform.position); // Only the real player moves diagnostically.
            yield return new WaitForFixedUpdate();
            Assert.That(clearance.CanInteract, Is.True, "Player must enter the authored trigger.");
            Assert.That(clearance.IsStarted, Is.False);
            clearance.Interact();
            targets = Read<EnemyHealth[]>(clearance, "targets").ToArray();
            Assert.That(targets.Length, Is.EqualTo(3));
            Assert.That(targets.Distinct().Count(), Is.EqualTo(3));
            Assert.That(targets.All(t => t != null && t.isActiveAndEnabled && !t.IsDead &&
                t.GetComponent<EnemyShooterMovement>() != null), Is.True);
            yield return FocusBlockLockedShieldGameView();
            yield return new WaitForEndOfFrame();
            AssertLive(0);
            var active = CaptureFlankingGameView("fireteam-active", "FireteamClearance");
            Assert.That(active.MoveNext(), Is.True); // Request now, wait for files after the short live observation.
            yield return active.Current;
            var origins = targets.Select(t => t.GetComponent<Rigidbody2D>().position).ToArray();
            float remaining = clearance.RemainingSeconds;
            bool moved = false;
            float deadline = Time.realtimeSinceStartup + 2f;
            while (Time.realtimeSinceStartup < deadline && (!moved || remaining - clearance.RemainingSeconds < .25f))
            {
                yield return new WaitForFixedUpdate();
                AssertLive(0);
                moved = targets.Where((t, i) => Vector2.Distance(t.GetComponent<Rigidbody2D>().position, origins[i]) > .05f).Any();
            }
            Assert.That(moved, Is.True, "Owned production shooters must move naturally; no manual Update/Move/Shoot.");
            Assert.That(remaining - clearance.RemainingSeconds, Is.GreaterThan(.2f), "Event clock must advance in the live battle.");
            targets[0].TakeDamage(float.MaxValue, targets[0].transform.position);
            AssertLive(1);
            yield return new WaitForEndOfFrame();
            AssertLive(1);
            var cleared = CaptureFlankingGameView("fireteam-one-cleared", "FireteamClearance");
            Assert.That(cleared.MoveNext(), Is.True);
            yield return cleared.Current;
            while (active.MoveNext()) yield return active.Current;
            while (cleared.MoveNext()) yield return cleared.Current;
        }
        finally
        {
            if (menu != null) EscortF1Call(menu, "CloseMenu");
            if (events != null)
            {
                events.DebugClearFireteamClearance(out _);
                events.DebugSetFireteamClearanceExperiment(experiment, out _);
            }
            if (health != null) health.DebugInvulnerable = protection;
            if (body != null) MoveEscortPlayer(body, originalPosition);
            EnemyDebugAiFreeze.SetFrozen(frozen);
            Time.timeScale = scale;
        }
    }
    [UnityTest, Timeout(120000)]
    public IEnumerator FireteamClearanceConfirmedKillsAndReward()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
        yield return new EnterPlayMode();
        yield return FireteamClearanceConfirmedKillsAndRewardBody();
    }

    private static IEnumerator FireteamClearanceConfirmedKillsAndRewardBody()
    {
        yield return BeginProductionRun();
        yield return AwaitEscortProductionReady();
        var events = Object.FindFirstObjectByType<WorldEventSpawner>();
        var enemies = Object.FindFirstObjectByType<EnemySpawner>();
        var player = PlayerRuntimeReference.CachedPlayer;
        var health = player.GetComponent<PlayerHealth>();
        var body = player.GetComponent<Rigidbody2D>();
        bool protection = health.DebugInvulnerable, frozen = EnemyDebugAiFreeze.IsFrozen;
        bool strafe = enemies.StrafingShootersExperiment;
        Vector3 originalPosition = player.transform.position;
        float scale = Time.timeScale;
        var rewards = Read<System.Collections.Generic.List<WorldBreakable>>(events, "ownedRewardContainers");
        var previousRewards = rewards.ToArray();
        WorldEvent owned = null;
        EnemyHealth outsider = null, foreignVictim = null;
        int completed = 0, failed = 0, credited = 0;
        WorldBreakable[] awarded = null;
        void OnComplete(WorldEvent e)
        {
            if (e != owned) return;
            completed++;
            awarded = rewards.Except(previousRewards).ToArray();
        }
        void OnFail(WorldEvent e) { if (e == owned) failed++; }
        void OnCredit(RunConfig config, int id, WorldEvent source, string tag) { credited++; }
        events.EventCompleted += OnComplete;
        events.EventFailed += OnFail;
        RunStateManager.ContentEventCompleted += OnCredit;
        try
        {
            health.DebugInvulnerable = true;
            EnemyDebugAiFreeze.SetFrozen(true);
            Time.timeScale = 1f;
            enemies.DebugSetStrafingShootersExperiment(true);
            Assert.That(events.DebugSetFireteamClearanceExperiment(true, out var status), Is.True, status);
            Assert.That(events.DebugSpawnFireteamClearance(out status), Is.True, status);
            var clearance = events.FireteamClearanceExperimentInstance;
            owned = clearance;
            Assert.That(clearance, Is.Not.Null);
            Assert.That(clearance.TryValidateConfiguration(out status), Is.True, status);
            Assert.That(clearance.SourcePrefab, Is.SameAs(Read<FireteamClearanceEvent>(events, "fireteamClearanceEventPrefab")));
            var footprints = new System.Collections.Generic.List<Rect>();
            clearance.CollectReservedFootprints(footprints);
            Assert.That(footprints.Count, Is.EqualTo(4));
            var area = GameplayAreaService.Instance;
            Assert.That(area, Is.Not.Null);
            for (int i = 0; i < footprints.Count; i++)
            {
                Rect f = footprints[i];
                foreach (Vector2 corner in new[] { f.min, f.max,
                    new Vector2(f.xMin, f.yMax), new Vector2(f.xMax, f.yMin) })
                    Assert.That(area.IsInsidePlayableArea(corner), Is.True, "Reserved footprint must fit the playable area.");
                for (int j = 0; j < i; j++) Assert.That(f.Overlaps(footprints[j]), Is.False);
            }
            MoveEscortPlayer(body, clearance.transform.position);
            yield return new WaitForFixedUpdate();
            Assert.That(clearance.CanInteract, Is.True);
            int cadence = Read<int>(enemies, "strafingEligibleSpawns");
            int volleyCadence = Read<int>(enemies, "volleyEligibleSpawns");
            int tracked = enemies.DebugTrackedEnemyCount;
            clearance.Interact();
            Assert.That(clearance.IsStarted && !clearance.IsCompleted, Is.True);
            Assert.That(events.ActiveEvent, Is.SameAs(clearance));
            var targets = Read<EnemyHealth[]>(clearance, "targets").ToArray();
            Assert.That(targets.Length, Is.EqualTo(3));
            Assert.That(targets.Distinct().Count(), Is.EqualTo(3));
            foreach (var target in targets)
            {
                Assert.That(target != null && target.isActiveAndEnabled && !target.IsDead, Is.True);
                Assert.That(target.GetComponent<EnemyShooterMovement>(), Is.Not.Null);
            }
            Assert.That(enemies.DebugTrackedEnemyCount, Is.EqualTo(tracked + 3), "All three event actors use production tracking.");
            Assert.That(Read<int>(enemies, "strafingEligibleSpawns"), Is.EqualTo(cadence));
            Assert.That(Read<int>(enemies, "volleyEligibleSpawns"), Is.EqualTo(volleyCadence));
            // Separate production actors: one foreign death and one survivor test both ownership boundaries.
            var prefab = Read<GameObject>(clearance, "shooterPrefab");
            for (int i = 0; i < 24 && (outsider == null || foreignVictim == null); i++)
            {
                float angle = i * Mathf.PI / 12f;
                var point = clearance.transform.position + new Vector3(Mathf.Cos(angle), Mathf.Sin(angle)) * 4f;
                var actor = enemies.SpawnEventEnemyAt(prefab, point, .6f, 3f);
                if (actor == null) continue;
                if (outsider == null) outsider = actor; else foreignVictim = actor;
            }
            Assert.That(outsider, Is.Not.Null, "Requires a safely admitted unrelated actor.");
            Assert.That(foreignVictim, Is.Not.Null);
            foreignVictim.TakeDamage(float.MaxValue, foreignVictim.transform.position);
            Assert.That(clearance.ConfirmedKills, Is.Zero, "Foreign deaths cannot advance the objective.");
            for (int i = 0; i < 2; i++)
            {
                targets[i].TakeDamage(float.MaxValue, targets[i].transform.position);
                Assert.That(clearance.ConfirmedKills, Is.EqualTo(i + 1));
                Assert.That(clearance.IsCompleted, Is.False);
                Assert.That(completed + failed, Is.Zero);
                Assert.That(rewards.Except(previousRewards).Count(), Is.Zero);
            }
            Vector3 rewardPosition = clearance.RewardPosition;
            Assert.That(clearance.CompletionReward.HasValue, Is.False, "Authored event uses standard completion container dispatch.");
            targets[2].TakeDamage(float.MaxValue, targets[2].transform.position);
            Assert.That(clearance.ConfirmedKills, Is.EqualTo(3));
            Assert.That(clearance.IsCompleted && !clearance.IsFailed, Is.True);
            Assert.That(completed, Is.EqualTo(1));
            Assert.That(failed, Is.Zero);
            Assert.That(awarded, Has.Length.EqualTo(1));
            Assert.That(awarded[0], Is.Not.Null);
            Assert.That(Vector3.Distance(awarded[0].transform.position, rewardPosition), Is.LessThan(.01f));
            Assert.That(events.FireteamClearanceExperimentInstance, Is.Null);
            Assert.That(events.ActiveEvent, Is.Null);
            Assert.That(events.SpawnedEvents.Contains(owned), Is.False);
            Assert.That(Read<System.Collections.Generic.HashSet<WorldEvent>>(events, "debugContentEvents").Contains(owned), Is.False);
            Assert.That(outsider.isActiveAndEnabled && !outsider.IsDead, Is.True);
            Assert.That(RunStateManager.Instance.IsDevelopmentRun, Is.True);
            Assert.That(credited, Is.Zero);
            events.NotifyEventCompleted(owned); // Owner notification idempotency; never force event completion.
            Assert.That(completed, Is.EqualTo(1));
            Assert.That(rewards.Except(previousRewards).Count(), Is.EqualTo(1));
        }
        finally
        {
            events.EventCompleted -= OnComplete;
            events.EventFailed -= OnFail;
            RunStateManager.ContentEventCompleted -= OnCredit;
            events.DebugClearFireteamClearance(out _);
            events.DebugSetFireteamClearanceExperiment(false, out _);
            enemies.DebugSetStrafingShootersExperiment(strafe);
            foreach (var actor in new[] { outsider, foreignVictim })
                if (actor != null) { actor.gameObject.SetActive(false); Object.Destroy(actor.gameObject); }
            if (health != null) health.DebugInvulnerable = protection;
            if (body != null) MoveEscortPlayer(body, originalPosition);
            EnemyDebugAiFreeze.SetFrozen(frozen);
            Time.timeScale = scale;
        }
    }

    [UnityTest, Timeout(120000)]
    public IEnumerator FireteamClearanceOptInAndCancellation()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
        yield return new EnterPlayMode();
        yield return FireteamClearanceOptInAndCancellationBody();
    }

    private static IEnumerator FireteamClearanceOptInAndCancellationBody()
    {
        yield return BeginProductionRun();
        yield return AwaitEscortProductionReady();
        var events = Object.FindFirstObjectByType<WorldEventSpawner>();
        var player = PlayerRuntimeReference.CachedPlayer;
        var health = player.GetComponent<PlayerHealth>();
        var body = player.GetComponent<Rigidbody2D>();
        bool protection = health.DebugInvulnerable, frozen = EnemyDebugAiFreeze.IsFrozen;
        float scale = Time.timeScale;
        Vector3 originalPosition = player.transform.position;
        var pool = events.EventPrefabs.ToArray();
        var rewards = Read<System.Collections.Generic.List<WorldBreakable>>(events, "ownedRewardContainers");
        var previousRewards = rewards.ToArray();
        WorldEvent owned = null;
        int completed = 0, failed = 0;
        EnemyHealth outsider = null;
        void OnComplete(WorldEvent e) { if (e == owned) completed++; }
        void OnFail(WorldEvent e) { if (e == owned) failed++; }
        events.EventCompleted += OnComplete;
        events.EventFailed += OnFail;
        try
        {
            health.DebugInvulnerable = true;
            EnemyDebugAiFreeze.SetFrozen(true);
            Time.timeScale = 1f;
            Assert.That(events.FireteamClearanceExperimentEnabled, Is.False);
            Assert.That(events.DebugSpawnFireteamClearance(out var status), Is.False, status);
            Assert.That(events.FireteamClearanceExperimentInstance, Is.Null);
            var prefab = Read<FireteamClearanceEvent>(events, "fireteamClearanceEventPrefab");
            Assert.That(prefab, Is.Not.Null);
            Assert.That(prefab.AvailableInProduction || prefab.AllowedInSite, Is.False);
            Assert.That(pool.Contains(prefab), Is.False);
            Assert.That(events.DebugSetFireteamClearanceExperiment(true, out status), Is.True, status);
            Assert.That(RunStateManager.Instance.IsDevelopmentRun, Is.True, "Development before event creation.");
            Assert.That(events.FireteamClearanceExperimentInstance, Is.Null);
            int ordinaryCapacity = Read<int>(events, "maxActiveEvents");
            var ordinaryEvents = events.SpawnedEvents.ToArray();
            Assert.That(ordinaryEvents.Length, Is.EqualTo(ordinaryCapacity), "Production MVP must retain its filled ordinary event capacity.");
            Assert.That(Read<int>(events, "spawnedEventCount"), Is.EqualTo(ordinaryCapacity));
            Assert.That(events.DebugSpawnFireteamClearance(out status), Is.True, status);
            var clearance = events.FireteamClearanceExperimentInstance;
            owned = clearance;
            Assert.That(events.SpawnedEvents.Count, Is.EqualTo(ordinaryCapacity + 1));
            Assert.That(Read<int>(events, "spawnedEventCount"), Is.EqualTo(ordinaryCapacity + 1));
            CollectionAssert.AreEquivalent(ordinaryEvents, events.SpawnedEvents.Where(e => e != clearance).ToArray());
            var withClearance = events.SpawnedEvents.ToArray();
            Assert.That(events.DebugSpawnFireteamClearance(out status), Is.False, status);
            Assert.That(events.FireteamClearanceExperimentInstance, Is.SameAs(clearance));
            CollectionAssert.AreEqual(withClearance, events.SpawnedEvents);
            Assert.That(Read<int>(events, "spawnedEventCount"), Is.EqualTo(ordinaryCapacity + 1));
            Assert.That(Read<int>(events, "maxActiveEvents"), Is.EqualTo(ordinaryCapacity));
            MoveEscortPlayer(body, clearance.transform.position);
            yield return new WaitForFixedUpdate();
            Assert.That(clearance.CanInteract, Is.True);
            clearance.Interact();
            Assert.That(events.ActiveEvent, Is.SameAs(clearance));
            var targets = Read<EnemyHealth[]>(clearance, "targets").ToArray();
            Assert.That(targets.All(t => t != null && t.isActiveAndEnabled && !t.IsDead), Is.True);
            targets[0].gameObject.SetActive(false);
            yield return null;
            Assert.That(failed, Is.EqualTo(1));
            Assert.That(completed, Is.Zero);
            Assert.That(rewards.Except(previousRewards).Count(), Is.Zero);
            Assert.That(targets.All(t => t == null || !t.gameObject.activeInHierarchy), Is.True);
            Assert.That(events.FireteamClearanceExperimentInstance, Is.Null);
            Assert.That(events.ActiveEvent, Is.Null);
            Assert.That(events.SpawnedEvents.Contains(owned), Is.False);
            yield return null; // Release the failed root before a separate placement.
            CollectionAssert.AreEquivalent(ordinaryEvents, events.SpawnedEvents);
            Assert.That(ordinaryEvents.All(e => e != null && e.gameObject.activeInHierarchy), Is.True);
            Assert.That(Read<int>(events, "spawnedEventCount"), Is.EqualTo(ordinaryCapacity));
            Assert.That(failed, Is.EqualTo(1));
            Assert.That(events.DebugSpawnFireteamClearance(out status), Is.True, status);
            clearance = events.FireteamClearanceExperimentInstance;
            owned = clearance;
            MoveEscortPlayer(body, clearance.transform.position);
            yield return new WaitForFixedUpdate();
            Assert.That(clearance.CanInteract, Is.True);
            clearance.Interact();
            Assert.That(events.ActiveEvent, Is.SameAs(clearance));
            targets = Read<EnemyHealth[]>(clearance, "targets").ToArray();
            Assert.That(targets.All(t => t != null && t.isActiveAndEnabled && !t.IsDead), Is.True);
            var enemies = Object.FindFirstObjectByType<EnemySpawner>();
            var shooterPrefab = Read<GameObject>(clearance, "shooterPrefab");
            for (int i = 0; i < 24 && outsider == null; i++)
            {
                float angle = i * Mathf.PI / 12f;
                var point = clearance.transform.position + new Vector3(Mathf.Cos(angle), Mathf.Sin(angle)) * 4f;
                outsider = enemies.SpawnEventEnemyAt(shooterPrefab, point, .6f, 3f);
            }
            Assert.That(outsider, Is.Not.Null, "Requires a safely admitted actor outside event ownership.");
            Assert.That(events.DebugSetFireteamClearanceExperiment(false, out status), Is.True, status);
            Assert.That(events.FireteamClearanceExperimentEnabled, Is.False);
            Assert.That(events.FireteamClearanceExperimentInstance, Is.Null);
            Assert.That(events.ActiveEvent, Is.Null);
            Assert.That(events.SpawnedEvents.Contains(owned), Is.False);
            Assert.That(Read<System.Collections.Generic.HashSet<WorldEvent>>(events, "debugContentEvents").Contains(owned), Is.False);
            Assert.That(((System.Collections.IDictionary)Read<object>(events, "eventRootOwners")).Contains(owned), Is.False);
            Assert.That(targets.All(t => t == null || !t.gameObject.activeInHierarchy), Is.True);
            Assert.That(outsider.isActiveAndEnabled && !outsider.IsDead, Is.True, "OFF must preserve unrelated actors.");
            yield return null;
            Assert.That(completed, Is.Zero);
            Assert.That(failed, Is.EqualTo(1), "OFF is administrative, with no additional outcome.");
            CollectionAssert.AreEquivalent(ordinaryEvents, events.SpawnedEvents);
            Assert.That(ordinaryEvents.All(e => e != null && e.gameObject.activeInHierarchy), Is.True);
            Assert.That(Read<int>(events, "spawnedEventCount"), Is.EqualTo(ordinaryCapacity));
            Assert.That(Read<int>(events, "maxActiveEvents"), Is.EqualTo(ordinaryCapacity));
            Assert.That(rewards.Except(previousRewards).Count(), Is.Zero);
            CollectionAssert.AreEqual(pool, events.EventPrefabs);
        }
        finally
        {
            events.EventCompleted -= OnComplete;
            events.EventFailed -= OnFail;
            events.DebugClearFireteamClearance(out _);
            events.DebugSetFireteamClearanceExperiment(false, out _);
            if (outsider != null) { outsider.gameObject.SetActive(false); Object.Destroy(outsider.gameObject); }
            if (health != null) health.DebugInvulnerable = protection;
            if (body != null) MoveEscortPlayer(body, originalPosition);
            EnemyDebugAiFreeze.SetFrozen(frozen);
            Time.timeScale = scale;
        }
    }

    [UnityTest]
    public IEnumerator StrafingShooterTangentialMotionAndVolleyExclusion()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
        yield return new EnterPlayMode();
        yield return StrafingShooterMotionBody();
    }

    private static IEnumerator StrafingShooterMotionBody()
    {
        yield return BeginProductionRun();
        var player = PrepareProduction();
        var station = Object.FindFirstObjectByType<OrbitalStationRuntime>();
        bool stationEnabled = station.enabled;
        bool frozen = EnemyDebugAiFreeze.IsFrozen;
        float scale = Time.timeScale;
        var health = player.GetComponent<PlayerHealth>();
        bool invulnerable = health.DebugInvulnerable;
        var actor = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/_Project/prefabs/Enemies/p_Enemy_Shooter.prefab"), player.transform.position + Vector3.left * 6f, Quaternion.identity);
        var shooter = actor.GetComponent<EnemyShooterMovement>();
        var body = actor.GetComponent<Rigidbody2D>();
        try
        {
            station.enabled = false;
            health.DebugInvulnerable = true;
            EnemyDebugAiFreeze.SetFrozen(false);
            Time.timeScale = 1f;
            yield return null; // Resolve the real player in Start before observing FixedUpdate.
            Assert.That(Read<Transform>(shooter, "player"), Is.EqualTo(player.transform));
            float preferred = Read<float>(shooter, "preferredDistance");
            float tolerance = Read<float>(shooter, "distanceTolerance");
            Assert.That(preferred - tolerance, Is.GreaterThan(1f));
            var area = GameplayAreaService.Instance;
            Assert.That(area, Is.Not.Null);
            bool Free(Vector2 point) => area.IsInsidePlayableArea(point, .8f) && area.IsInsideSpawnArea(point, .8f) &&
                Physics2D.OverlapCircleAll(point, .8f).All(c => c.transform.IsChildOf(actor.transform) ||
                    c == area.PlayableArea || c == area.SpawnArea ||
                    (c.isTrigger && c.GetComponentInParent<EnemyHealth>() == null));
            Vector2 center = player.transform.position;
            Vector2? radial = null;
            float near = preferred - tolerance - .7f;
            float far = preferred + tolerance + .7f;
            for (int i = 0; i < 24; i++)
            {
                float angle = i * Mathf.PI / 12f;
                var direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                if (Free(center + direction * near) && Free(center + direction * preferred) && Free(center + direction * far))
                { radial = direction; break; }
            }
            Assert.That(radial.HasValue, Is.True, "No free diagnostic distance band; preserve production placement and steering.");
            Vector2 tangent = new Vector2(radial.Value.y, -radial.Value.x);
            void Place(float distance)
            {
                body.position = center + radial.Value * distance;
                body.linearVelocity = Vector2.zero;
                Physics2D.SyncTransforms();
            }
            // A natural OFF fixed step also settles the existing steering state between observations.
            for (int side = 0; side < 2; side++)
            {
                shooter.ConfigureStrafingExperiment(false, false);
                Place(preferred);
                yield return new WaitForFixedUpdate();
                shooter.ConfigureStrafingExperiment(true, side == 0);
                Vector2 start = body.position;
                yield return WaitGameTime(.16f);
                float lateral = Vector2.Dot(body.position - start, tangent);
                Assert.That(lateral * (side == 0 ? 1f : -1f), Is.GreaterThan(.05f),
                    "Opposite assignments must produce opposite real tangential displacement with production steering.");
            }
            shooter.ConfigureStrafingExperiment(false, false);
            Place(preferred);
            yield return new WaitForFixedUpdate();
            var idle = body.position;
            yield return WaitGameTime(.16f);
            Assert.That(Vector2.Distance(body.position, idle), Is.LessThan(.03f), "OFF removes intentional lateral movement.");
            foreach (float distance in new[] { near, far })
            {
                shooter.ConfigureStrafingExperiment(false, false);
                Place(preferred);
                // Settle steering naturally; never invoke Move/FixedUpdate or alter authored speeds.
                yield return new WaitForFixedUpdate();
                Place(distance);
                shooter.ConfigureStrafingExperiment(true, true);
                float before = Vector2.Distance(body.position, center);
                yield return WaitGameTime(.16f);
                float change = Vector2.Distance(body.position, center) - before;
                Assert.That(change * (distance == near ? 1f : -1f), Is.GreaterThan(.03f),
                    "Outside the authored band the shooter still retreats/approaches.");
            }
            Place(preferred);
            EnemyDebugAiFreeze.SetFrozen(true);
            var stopped = body.position;
            yield return WaitGameTime(.12f);
            Assert.That(Vector2.Distance(body.position, stopped), Is.LessThan(.03f));
            shooter.SetWorldRuleExternalVelocity(tangent * 2f);
            yield return WaitGameTime(.12f);
            Assert.That(Vector2.Dot(body.position - stopped, tangent), Is.GreaterThan(.1f), "Freeze preserves external motion.");
            shooter.SetWorldRuleExternalVelocity(Vector2.zero);
            float fireTimerBeforeRejectedVolley = Read<float>(shooter, "fireTimer");
            shooter.ConfigureCommittedVolleyExperiment(true);
            Assert.That(shooter.CommittedVolleyExperiment, Is.False);
            Assert.That(shooter.StrafingExperiment, Is.True, "Rejected volley cannot cancel existing strafe.");
            Assert.That(Read<float>(shooter, "fireTimer"), Is.EqualTo(fireTimerBeforeRejectedVolley),
                "Rejected volley cannot delay the existing single-shot cadence.");
            shooter.ConfigureStrafingExperiment(false, false);
            shooter.ConfigureCommittedVolleyExperiment(true);
            Assert.That(shooter.CommittedVolleyExperiment, Is.True);
            shooter.ConfigureStrafingExperiment(true, false);
            Assert.That(shooter.StrafingExperiment, Is.False);
            Assert.That(shooter.CommittedVolleyExperiment, Is.True, "Rejected strafe cannot cancel existing volley.");
        }
        finally
        {
            if (actor != null) Object.DestroyImmediate(actor);
            station.enabled = stationEnabled;
            health.DebugInvulnerable = invulnerable;
            EnemyDebugAiFreeze.SetFrozen(frozen);
            Time.timeScale = scale;
        }
    }

    [UnityTest]
    public IEnumerator StrafingShooterOptInCadenceAndLifecycle()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
        yield return new EnterPlayMode();
        yield return StrafingShooterLifecycleBody();
    }

    private static IEnumerator StrafingShooterLifecycleBody()
    {
        yield return BeginProductionRun();
        var player = PrepareProduction();
        var station = Object.FindFirstObjectByType<OrbitalStationRuntime>();
        var spawner = Object.FindFirstObjectByType<EnemySpawner>();
        bool stationEnabled = station.enabled;
        bool spawnerEnabled = spawner.enabled;
        bool frozen = EnemyDebugAiFreeze.IsFrozen;
        var actors = new System.Collections.Generic.List<GameObject>();
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/prefabs/Enemies/p_Enemy_Shooter.prefab");
        var spawn = typeof(EnemySpawner).GetMethod("SpawnEnemyAt", BindingFlags.Instance | BindingFlags.NonPublic);
        EnemyShooterMovement Spawn(bool ordinary)
        {
            var actor = (GameObject)spawn.Invoke(spawner, new object[] {
                prefab, player.transform.position + Vector3.left * (12f + actors.Count * 2f), ordinary });
            Assert.That(actor, Is.Not.Null);
            actors.Add(actor);
            return actor.GetComponent<EnemyShooterMovement>();
        }
        EnemyShooterMovement Manual()
        {
            var actor = spawner.DebugSpawnStrafingShooterAround(prefab, player.transform.position, out string status);
            Assert.That(actor, Is.Not.Null, status);
            actors.Add(actor);
            var result = actor.GetComponent<EnemyShooterMovement>();
            Assert.That(result.StrafingExperiment, Is.True);
            return result;
        }
        EnemyShooterMovement Fourth()
        {
            for (int i = 0; i < 3; i++) Assert.That(Spawn(true).StrafingExperiment, Is.False);
            var result = Spawn(true);
            Assert.That(result.StrafingExperiment, Is.True, "Every fourth eligible ordinary assignment.");
            return result;
        }
        try
        {
            station.enabled = false;
            EnemyDebugAiFreeze.SetFrozen(true);
            Assert.That(spawner.StrafingShootersExperiment, Is.False, "Public default OFF.");
            Assert.That(EnemySpawner.IsStrafingShooterEligible(prefab), Is.True);
            Assert.That(Spawn(true).StrafingExperiment, Is.False);
            spawner.DebugSetStrafingShootersExperiment(true);
            Assert.That(RunStateManager.Instance.IsDevelopmentRun, Is.True, "Development before first assignment.");
            Assert.That(Spawn(true).StrafingExperiment, Is.False);
            Assert.That(Spawn(false).StrafingExperiment, Is.False);
            var manual = Manual();
            float firstDirection = Read<float>(manual, "strafingDirection");
            Assert.That(Mathf.Abs(firstDirection), Is.EqualTo(1f));
            Assert.That(Read<int>(spawner, "strafingEligibleSpawns"), Is.EqualTo(1), "Manual/special preserve ordinary cadence.");
            Assert.That(Spawn(true).StrafingExperiment, Is.False);
            Assert.That(Spawn(true).StrafingExperiment, Is.False);
            var ordinary = Spawn(true);
            Assert.That(ordinary.StrafingExperiment, Is.True);
            Assert.That(Read<float>(ordinary, "strafingDirection"), Is.EqualTo(-firstDirection));
            var next = Fourth();
            Assert.That(Read<float>(next, "strafingDirection"), Is.EqualTo(firstDirection), "Assignment sides alternate across manual and ordinary.");
            spawner.DebugSetStrafingShootersExperiment(false);
            Assert.That(manual.StrafingExperiment || ordinary.StrafingExperiment || next.StrafingExperiment, Is.False);
            Assert.That(Read<int>(spawner, "strafingEligibleSpawns"), Is.Zero);
            Assert.That(Read<int>(spawner, "strafingAssignedSpawns"), Is.Zero);
            spawner.DebugSetStrafingShootersExperiment(true);
            ordinary = Fourth();
            manual = Manual();
            spawner.enabled = false; // Disable only the owner, leaving both assigned enemies alive.
            Assert.That(ordinary.gameObject.activeInHierarchy && manual.gameObject.activeInHierarchy, Is.True);
            Assert.That(ordinary.StrafingExperiment || manual.StrafingExperiment, Is.False);
            Assert.That(spawner.StrafingShootersExperiment, Is.False);
            Assert.That(Read<int>(spawner, "strafingEligibleSpawns"), Is.Zero);
            Assert.That(Read<int>(spawner, "strafingAssignedSpawns"), Is.Zero);
            spawner.enabled = true;
            spawner.StopSpawning();
            Assert.That(spawner.StrafingShootersExperiment, Is.False, "Re-enable cannot restore opt-in.");
            ordinary.ConfigureStrafingExperiment(true, true);
            Assert.That(ordinary.StrafingExperiment, Is.True);
            ordinary.enabled = false;
            Assert.That(ordinary.StrafingExperiment, Is.False);
            Assert.That(Read<float>(ordinary, "strafingDirection"), Is.Zero);
            ordinary.enabled = true;
            Assert.That(ordinary.StrafingExperiment, Is.False);
            Assert.That(RunStateManager.Instance.IsDevelopmentRun, Is.True, "Cleanup cannot restore production credit.");
        }
        finally
        {
            spawner.DebugSetStrafingShootersExperiment(false);
            foreach (var actor in actors) if (actor != null) Object.DestroyImmediate(actor);
            spawner.enabled = spawnerEnabled;
            spawner.StopSpawning();
            station.enabled = stationEnabled;
            EnemyDebugAiFreeze.SetFrozen(frozen);
        }
    }

    private static IEnumerator FocusBlockLockedShieldGameView()
    {
        // Same real Game View focus path as the existing bunker production scenarios.
        var view = EditorWindow.GetWindow(typeof(Editor).Assembly.GetType("UnityEditor.GameView"));
        view.Show();
        view.Focus();
        float deadline = Time.realtimeSinceStartup + 2f;
        while (!Application.isFocused && Time.realtimeSinceStartup < deadline) yield return null;
        Debug.Log($"BlockLockedShield Game View: applicationFocused={Application.isFocused}, " +
            $"focusedWindow={EditorWindow.focusedWindow?.GetType().Name}, aiFrozen={EnemyDebugAiFreeze.IsFrozen}, timeScale={Time.timeScale}");
        Assert.That(Application.isFocused, Is.True,
            "Real Game View focus unavailable; production shield Update guard is preserved.");
    }

    private static void AssertBlockLockedShieldUpdateReady(EnemyDirectionalShield shield, Transform player)
    {
        var boundPlayer = Read<Transform>(shield, "playerBody");
        string state = $"focus={Application.isFocused}, aiFrozen={EnemyDebugAiFreeze.IsFrozen}, " +
            $"timeScale={Time.timeScale}, boundPlayer={boundPlayer?.name}, expectedPlayer={player?.name}, " +
            $"dead={shield.GetComponent<EnemyHealth>().IsDead}, active={shield.isActiveAndEnabled}";
        Assert.That(Application.isFocused, Is.True, state);
        Assert.That(EnemyDebugAiFreeze.IsFrozen, Is.False, state);
        Assert.That(Time.timeScale, Is.GreaterThan(0f), state);
        Assert.That(boundPlayer, Is.EqualTo(player), state);
        Assert.That(shield.GetComponent<EnemyHealth>().IsDead, Is.False, state);
        Assert.That(shield.isActiveAndEnabled, Is.True, state);
    }

    [UnityTest]
    public IEnumerator BlockLockedShieldConfirmedBlockAndRecovery()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
        yield return new EnterPlayMode();
        yield return BlockLockedShieldRecoveryBody();
    }

    private static IEnumerator BlockLockedShieldRecoveryBody()
    {
        yield return BeginProductionRun();
        var player = PrepareProduction();
        var playerBody = player.GetComponent<Rigidbody2D>();
        var originalPosition = playerBody.position;
        var station = Object.FindFirstObjectByType<OrbitalStationRuntime>();
        bool stationEnabled = station.enabled;
        bool frozen = EnemyDebugAiFreeze.IsFrozen;
        float scale = Time.timeScale;
        var actor = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/_Project/prefabs/Enemies/ShieldBearer.prefab"),
            player.transform.position + Vector3.left * 5f, Quaternion.identity);
        var shield = actor.GetComponent<EnemyDirectionalShield>();
        try
        {
            station.enabled = false;
            foreach (var movement in actor.GetComponents<EnemyMovement>()) movement.enabled = false;
            actor.GetComponent<Rigidbody2D>().constraints = RigidbodyConstraints2D.FreezeAll;
            EnemyDebugAiFreeze.SetFrozen(false);
            Time.timeScale = 1f;
            shield.Bind(player.transform);
            shield.ConfigureBlockLockedExperiment(true);
            Physics2D.SyncTransforms();
            yield return FocusBlockLockedShieldGameView();
            AssertBlockLockedShieldUpdateReady(shield, player.transform);
            var forward = shield.Forward;
            Vector2 Plate() => shield.ShieldCollider.ClosestPoint(shield.Center + shield.Forward * (shield.ShieldDistance + 1f));
            Assert.That(shield.BlockProjectile(-forward, shield.Center + forward * 20f), Is.False);
            Assert.That(shield.BlockProjectile(forward, Plate()), Is.False, "Unprotected incoming side cannot start a window.");
            Assert.That(shield.BlockLockRemaining, Is.Zero);
            Assert.That(shield.BlockProjectile(-forward, Plate()), Is.True);
            Assert.That(shield.BlockLockRemaining, Is.EqualTo(.9f).Within(.0001f));
            yield return WaitGameTime(.12f);
            AssertBlockLockedShieldUpdateReady(shield, player.transform);
            float remaining = shield.BlockLockRemaining;
            Assert.That(remaining, Is.InRange(.01f, .89f));
            Assert.That(shield.BlockProjectile(-forward, Plate()), Is.True);
            Assert.That(shield.BlockLockRemaining, Is.EqualTo(remaining), "A second confirmed block cannot refresh the window.");
            int durability = shield.RemainingBlockedHits;
            EnemyDebugAiFreeze.SetFrozen(true);
            playerBody.position = shield.Center + new Vector2(-forward.y, forward.x) * 5f;
            Physics2D.SyncTransforms();
            yield return WaitRealtime(.12f);
            Assert.That(shield.BlockLockRemaining, Is.EqualTo(remaining));
            Assert.That(Vector2.Angle(shield.Forward, forward), Is.LessThan(.01f));
            EnemyDebugAiFreeze.SetFrozen(false);
            AssertBlockLockedShieldUpdateReady(shield, player.transform);
            float deadline = Time.realtimeSinceStartup + 4f;
            while (shield.BlockLockRemaining > 0f && Time.realtimeSinceStartup < deadline)
            {
                AssertBlockLockedShieldUpdateReady(shield, player.transform);
                Assert.That(Vector2.Angle(shield.Forward, forward), Is.LessThan(.01f));
                yield return null;
            }
            Assert.That(shield.BlockLockRemaining, Is.Zero);
            // Sample natural Update, including an allowed boundary frame before resuming tracking.
            float startTime = Time.time;
            var startForward = shield.Forward;
            yield return WaitGameTime(.18f);
            AssertBlockLockedShieldUpdateReady(shield, player.transform);
            float turn = Vector2.Angle(startForward, shield.Forward);
            Assert.That(turn, Is.GreaterThan(1f), "Tracking resumes after the lock expires.");
            Assert.That(turn, Is.LessThanOrEqualTo(shield.TurnSpeed * (Time.time - startTime) + .5f), "Authored angular speed limits recovery.");
            Assert.That(shield.BlockProjectile(-shield.Forward, Plate(), false), Is.True);
            var beforeOff = shield.Forward;
            shield.ConfigureBlockLockedExperiment(false);
            Assert.That(shield.IsBlockLockedExperiment, Is.False);
            Assert.That(shield.BlockLockRemaining, Is.Zero);
            Assert.That(shield.Forward, Is.EqualTo(beforeOff), "OFF cannot snap the plate toward the player.");
            Assert.That(shield.RemainingBlockedHits, Is.EqualTo(durability), "OFF cannot repair consumed durability.");
        }
        finally
        {
            if (actor != null) Object.DestroyImmediate(actor);
            playerBody.position = originalPosition;
            station.enabled = stationEnabled;
            EnemyDebugAiFreeze.SetFrozen(frozen);
            Time.timeScale = scale;
        }
    }

    [UnityTest]
    public IEnumerator BlockLockedShieldOptInCadenceAndLifecycle()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
        yield return new EnterPlayMode();
        yield return BlockLockedShieldLifecycleBody();
    }

    private static IEnumerator BlockLockedShieldLifecycleBody()
    {
        yield return BeginProductionRun();
        yield return FocusBlockLockedShieldGameView();
        var player = PrepareProduction();
        var station = Object.FindFirstObjectByType<OrbitalStationRuntime>();
        var spawner = Object.FindFirstObjectByType<EnemySpawner>();
        bool stationEnabled = station.enabled;
        bool spawnerEnabled = spawner.enabled;
        bool frozen = EnemyDebugAiFreeze.IsFrozen;
        var actors = new System.Collections.Generic.List<GameObject>();
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/prefabs/Enemies/ShieldBearer.prefab");
        var spawn = typeof(EnemySpawner).GetMethod("SpawnEnemyAt", BindingFlags.Instance | BindingFlags.NonPublic);
        EnemyDirectionalShield Spawn(bool ordinary)
        {
            var actor = (GameObject)spawn.Invoke(spawner, new object[] {
                prefab, player.transform.position + Vector3.left * (12f + actors.Count * 2f), ordinary });
            Assert.That(actor, Is.Not.Null);
            actors.Add(actor);
            return actor.GetComponent<EnemyDirectionalShield>();
        }
        EnemyDirectionalShield Manual()
        {
            var actor = spawner.DebugSpawnBlockLockedShieldAround(prefab, player.transform.position, out string status);
            Assert.That(actor, Is.Not.Null, status);
            actors.Add(actor);
            var result = actor.GetComponent<EnemyDirectionalShield>();
            Assert.That(result.IsBlockLockedExperiment, Is.True);
            return result;
        }
        EnemyDirectionalShield Fourth()
        {
            for (int i = 0; i < 3; i++) Assert.That(Spawn(true).IsBlockLockedExperiment, Is.False);
            var result = Spawn(true);
            Assert.That(result.IsBlockLockedExperiment, Is.True);
            return result;
        }
        try
        {
            station.enabled = false;
            EnemyDebugAiFreeze.SetFrozen(true);
            Assert.That(spawner.BlockLockedShieldsExperiment, Is.False, "Public default OFF.");
            Assert.That(EnemySpawner.IsBlockLockedShieldPrefabEligible(prefab), Is.True);
            var baseline = Spawn(true);
            Assert.That(baseline.IsBlockLockedExperiment, Is.False);
            Assert.That(EnemySpawner.IsBlockLockedShieldEligible(baseline.gameObject), Is.True);
            Physics2D.SyncTransforms();
            Assert.That(baseline.Kind, Is.EqualTo(EnemyDirectionalShield.ShieldKind.Breakable));
            int hitsToBreak = baseline.RemainingBlockedHits;
            for (int i = 0; i < hitsToBreak; i++)
            {
                var plate = baseline.ShieldCollider.ClosestPoint(baseline.Center + baseline.Forward * 3f);
                Assert.That(baseline.BlockProjectile(-baseline.Forward, plate), Is.True);
            }
            Assert.That(baseline.IsBroken, Is.True);
            Assert.That(EnemySpawner.IsBlockLockedShieldEligible(baseline.gameObject), Is.False,
                "A broken live plate remains ineligible even though its authored prefab is eligible.");
            Assert.That(EnemySpawner.IsBlockLockedShieldPrefabEligible(prefab), Is.True);
            spawner.DebugSetBlockLockedShieldsExperiment(true);
            Assert.That(RunStateManager.Instance.IsDevelopmentRun, Is.True, "Development before first assignment.");
            Assert.That(Spawn(true).IsBlockLockedExperiment, Is.False);
            Assert.That(Spawn(false).IsBlockLockedExperiment, Is.False);
            var manual = Manual();
            Assert.That(Read<int>(spawner, "blockLockedShieldEligibleSpawns"), Is.EqualTo(1), "Manual and special spawn preserve cadence.");
            Assert.That(Spawn(true).IsBlockLockedExperiment, Is.False);
            Assert.That(Spawn(true).IsBlockLockedExperiment, Is.False);
            var ordinary = Spawn(true);
            Assert.That(ordinary.IsBlockLockedExperiment, Is.True, "Every fourth eligible ordinary spawn.");
            spawner.DebugSetBlockLockedShieldsExperiment(false);
            Assert.That(ordinary.IsBlockLockedExperiment, Is.False);
            Assert.That(manual.IsBlockLockedExperiment, Is.False);
            Assert.That(Read<int>(spawner, "blockLockedShieldEligibleSpawns"), Is.Zero);
            spawner.DebugSetBlockLockedShieldsExperiment(true);
            ordinary = Fourth();
            manual = Manual();
            spawner.enabled = false; // Keep both enemies alive: test the owner's cleanup.
            Assert.That(ordinary.gameObject.activeInHierarchy && manual.gameObject.activeInHierarchy, Is.True);
            Assert.That(ordinary.IsBlockLockedExperiment || manual.IsBlockLockedExperiment, Is.False);
            Assert.That(spawner.BlockLockedShieldsExperiment, Is.False);
            Assert.That(Read<int>(spawner, "blockLockedShieldEligibleSpawns"), Is.Zero);
            spawner.enabled = true;
            spawner.StopSpawning();
            Assert.That(spawner.BlockLockedShieldsExperiment, Is.False, "Re-enable cannot restore opt-in.");
            ordinary.ConfigureBlockLockedExperiment(true);
            Physics2D.SyncTransforms();
            var point = ordinary.ShieldCollider.ClosestPoint(ordinary.Center + ordinary.Forward * 3f);
            Assert.That(ordinary.BlockProjectile(-ordinary.Forward, point, false), Is.True);
            ordinary.ResetForSpawn();
            Assert.That(ordinary.IsBlockLockedExperiment, Is.False);
            Assert.That(ordinary.BlockLockRemaining, Is.Zero);
            ordinary.ConfigureBlockLockedExperiment(true);
            Assert.That(ordinary.BlockProjectile(-ordinary.Forward, point, false), Is.True);
            ordinary.enabled = false;
            Assert.That(ordinary.IsBlockLockedExperiment, Is.False);
            Assert.That(ordinary.BlockLockRemaining, Is.Zero);
            ordinary.enabled = true;
            Assert.That(ordinary.IsBlockLockedExperiment, Is.False);
            Assert.That(RunStateManager.Instance.IsDevelopmentRun, Is.True, "Cleanup cannot restore production credit.");
        }
        finally
        {
            spawner.DebugSetBlockLockedShieldsExperiment(false);
            foreach (var actor in actors) if (actor != null) Object.DestroyImmediate(actor);
            spawner.enabled = spawnerEnabled;
            spawner.StopSpawning();
            station.enabled = stationEnabled;
            EnemyDebugAiFreeze.SetFrozen(frozen);
        }
    }

    [UnityTest]
    public IEnumerator CommittedRamLocksDirectionAndPreservesExternalMotion()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
        yield return new EnterPlayMode();
        yield return CommittedRamMotionBody();
    }

    private static IEnumerator CommittedRamMotionBody()
    {
        yield return BeginProductionRun();
        var player = PrepareProduction();
        var station = Object.FindFirstObjectByType<OrbitalStationRuntime>();
        bool stationEnabled = station.enabled;
        var health = player.GetComponent<PlayerHealth>();
        var playerBody = player.GetComponent<Rigidbody2D>();
        var originalPosition = playerBody.position;
        var constraints = playerBody.constraints;
        bool invulnerable = health.DebugInvulnerable;
        bool frozen = EnemyDebugAiFreeze.IsFrozen;
        float timeScale = Time.timeScale;
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/_Project/prefabs/Enemies/p_Enemy_classic_start.prefab");
        var actor = Object.Instantiate(prefab, player.transform.position + Vector3.left * 4.5f, Quaternion.identity);
        var chase = actor.GetComponent<EnemyChaseMovement>();
        var body = actor.GetComponent<Rigidbody2D>();
        var visual = Read<Transform>(chase, "flankingVisual");
        var authored = visual.localRotation;
        float rootRotation = body.rotation;
        string Phase() => Read<object>(chase, "ramPhase").ToString();
        try
        {
            station.enabled = false; // Isolate locomotion from weapon knockback; live MVP is a separate contract.
            health.DebugInvulnerable = true;
            EnemyDebugAiFreeze.SetFrozen(false);
            Time.timeScale = 1f;
            playerBody.constraints = RigidbodyConstraints2D.FreezeAll;
            actor.GetComponent<EnemyHealth>().SetRuntimeMaxHealth(10000f);
            Assert.That(chase.CanUseCommittedRam, Is.True, "Use the production safe graphic parent.");
            chase.ConfigureCommittedRamExperiment(true); // Before the first natural FixedUpdate.
            yield return CoreTestSupport.Await(() => Phase() == "Preparing");
            Assert.That(Vector2.Distance(body.position, playerBody.position), Is.LessThanOrEqualTo(5f));
            var direction = Read<Vector2>(chase, "ramDirection");
            var leaned = visual.localRotation;
            Assert.That(Quaternion.Angle(leaned, authored), Is.EqualTo(20f).Within(.05f));
            var stopped = body.position;
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();
            Assert.That(Vector2.Distance(body.position, stopped), Is.LessThan(.03f), "Preparation has no own locomotion.");
            chase.SetWorldRuleExternalVelocity(Vector2.up * 2f);
            var externalStart = body.position;
            yield return WaitGameTime(.12f);
            Assert.That(body.position.y - externalStart.y, Is.GreaterThan(.1f));
            Assert.That(Mathf.Abs(body.position.x - externalStart.x), Is.LessThan(.04f));
            chase.SetWorldRuleExternalVelocity(Vector2.zero);
            playerBody.position = originalPosition + Vector2.up * 7f;
            Physics2D.SyncTransforms();
            yield return CoreTestSupport.Await(() => Phase() == "Charging");
            var chargeStart = body.position;
            float deadline = Time.realtimeSinceStartup + 3f;
            while (Phase() == "Charging" && Time.realtimeSinceStartup < deadline)
            {
                Assert.That(Read<Vector2>(chase, "ramDirection"), Is.EqualTo(direction));
                Assert.That(Quaternion.Angle(visual.localRotation, leaned), Is.LessThan(.01f));
                Assert.That(body.rotation, Is.EqualTo(rootRotation).Within(.01f));
                yield return new WaitForFixedUpdate();
            }
            Assert.That(Phase(), Is.EqualTo("Recovering"));
            var travelled = body.position - chargeStart;
            Assert.That(travelled.magnitude, Is.GreaterThan(1f), "Observe actual MovePosition travel, not just desired velocity.");
            Assert.That(Vector2.Dot(travelled.normalized, direction), Is.GreaterThan(.995f), "Crossing player cannot steer the committed ram.");
            Assert.That(Quaternion.Angle(visual.localRotation, authored), Is.LessThan(.01f));
            var recoveryStart = body.position;
            yield return WaitGameTime(.2f);
            Assert.That(Phase(), Is.EqualTo("Recovering"));
            Assert.That(Vector2.Distance(body.position, recoveryStart), Is.LessThan(.03f));
            yield return CoreTestSupport.Await(() => Phase() == "Chasing");
            var pursuitStart = body.position;
            float distance = Vector2.Distance(pursuitStart, playerBody.position);
            yield return WaitGameTime(.2f);
            Assert.That(Phase(), Is.EqualTo("Chasing"), "Player stays outside the next preparation radius.");
            Assert.That(Vector2.Distance(body.position, playerBody.position), Is.LessThan(distance - .1f));
            Assert.That(body.rotation, Is.EqualTo(rootRotation).Within(.01f));
            Assert.That(Quaternion.Angle(visual.localRotation, authored), Is.LessThan(.01f));
        }
        finally
        {
            if (chase != null) { chase.SetWorldRuleExternalVelocity(Vector2.zero); chase.ConfigureCommittedRamExperiment(false); }
            if (actor != null) Object.DestroyImmediate(actor);
            playerBody.position = originalPosition;
            playerBody.constraints = constraints;
            health.DebugInvulnerable = invulnerable;
            station.enabled = stationEnabled;
            EnemyDebugAiFreeze.SetFrozen(frozen);
            Time.timeScale = timeScale;
        }
    }

    [UnityTest]
    public IEnumerator CommittedRamOptInCadenceAndLifecycle()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
        yield return new EnterPlayMode();
        yield return CommittedRamLifecycleBody();
    }

    private static IEnumerator CommittedRamLifecycleBody()
    {
        yield return BeginProductionRun();
        var player = PrepareProduction();
        var station = Object.FindFirstObjectByType<OrbitalStationRuntime>();
        bool stationEnabled = station.enabled;
        var spawner = Object.FindFirstObjectByType<EnemySpawner>();
        var health = player.GetComponent<PlayerHealth>();
        bool invulnerable = health.DebugInvulnerable;
        bool frozen = EnemyDebugAiFreeze.IsFrozen;
        float timeScale = Time.timeScale;
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/_Project/prefabs/Enemies/p_Enemy_classic_start.prefab");
        var actors = new System.Collections.Generic.List<GameObject>();
        var spawn = typeof(EnemySpawner).GetMethod("SpawnEnemyAt", BindingFlags.Instance | BindingFlags.NonPublic);
        EnemyChaseMovement Spawn(bool ordinary)
        {
            var actor = (GameObject)spawn.Invoke(spawner,
                new object[] { prefab, player.transform.position + Vector3.left * (12f + actors.Count * 2f), ordinary });
            Assert.That(actor, Is.Not.Null);
            actors.Add(actor);
            return actor.GetComponent<EnemyChaseMovement>();
        }
        try
        {
            station.enabled = false; // Keep ordinary weapon knockback outside this cadence/lifecycle contract.
            health.DebugInvulnerable = true;
            EnemyDebugAiFreeze.SetFrozen(false);
            Time.timeScale = 1f;
            Assert.That(spawner.CommittedRamExperiment, Is.False, "Public default is OFF.");
            Assert.That(EnemySpawner.IsCommittedRamEligible(prefab), Is.True);
            Assert.That(Spawn(true).IsCommittedRamExperiment, Is.False);
            spawner.DebugSetCommittedRamExperiment(true);
            Assert.That(RunStateManager.Instance.IsDevelopmentRun, Is.True, "Development precedes any assignment.");
            Assert.That(Spawn(true).IsCommittedRamExperiment, Is.False);
            Assert.That(Spawn(false).IsCommittedRamExperiment, Is.False);
            Assert.That(Read<int>(spawner, "ramEligibleSpawns"), Is.EqualTo(1));
            var manual = spawner.DebugSpawnCommittedRamAround(prefab, player.transform.position, out string status);
            Assert.That(manual, Is.Not.Null, status);
            actors.Add(manual);
            Assert.That(manual.GetComponent<EnemyChaseMovement>().IsCommittedRamExperiment, Is.True);
            Assert.That(Read<int>(spawner, "ramEligibleSpawns"), Is.EqualTo(1), "Manual/special spawn does not spend cadence.");
            Assert.That(Spawn(true).IsCommittedRamExperiment, Is.False);
            Assert.That(Spawn(true).IsCommittedRamExperiment, Is.False);
            var selected = Spawn(true);
            Assert.That(selected.IsCommittedRamExperiment, Is.True, "Fourth eligible ordinary spawn.");
            Assert.That(RunStateManager.Instance.IsDevelopmentRun, Is.True);
            foreach (var actor in actors) if (actor != selected.gameObject) actor.SetActive(false);
            selected.ConfigureFlankingExperiment(true, false);
            Assert.That(selected.IsCommittedRamExperiment, Is.False);
            Assert.That(EnemySpawner.IsCommittedRamEligible(selected.gameObject), Is.False);
            selected.ConfigureCommittedRamExperiment(true);
            Assert.That(selected.IsCommittedRamExperiment, Is.False, "Flanking rejects ram assignment.");
            selected.ConfigureFlankingExperiment(false, false);
            selected.ConfigureCommittedRamExperiment(true);
            var body = selected.GetComponent<Rigidbody2D>();
            var visual = Read<Transform>(selected, "flankingVisual");
            var authored = visual.localRotation;
            string Phase() => Read<object>(selected, "ramPhase").ToString();
            body.position = (Vector2)player.transform.position + Vector2.left * 4.5f;
            Physics2D.SyncTransforms();
            yield return CoreTestSupport.Await(() => Phase() == "Preparing");
            float remaining = Read<float>(selected, "ramRemaining");
            selected.PauseForAttack(.4f);
            yield return WaitGameTime(.12f);
            Assert.That(selected.IsAttackPaused, Is.True);
            Assert.That(Read<float>(selected, "ramRemaining"), Is.EqualTo(remaining).Within(.0001f));
            selected.ResumeAfterAttack();
            EnemyDebugAiFreeze.SetFrozen(true);
            yield return WaitRealtime(.12f);
            Assert.That(Read<float>(selected, "ramRemaining"), Is.EqualTo(remaining).Within(.0001f));
            EnemyDebugAiFreeze.SetFrozen(false);
            spawner.DebugSetCommittedRamExperiment(false);
            Assert.That(selected.IsCommittedRamExperiment, Is.False);
            Assert.That(Phase(), Is.EqualTo("Chasing"));
            Assert.That(Read<Vector2>(selected, "ramDirection"), Is.EqualTo(Vector2.zero));
            Assert.That(Quaternion.Angle(visual.localRotation, authored), Is.LessThan(.01f));
            yield return WaitGameTime(.1f);
            Assert.That(Phase(), Is.EqualTo("Chasing"), "OFF cannot release the pending ram.");
            selected.ConfigureCommittedRamExperiment(true);
            yield return CoreTestSupport.Await(() => Phase() == "Preparing");
            var assaultTarget = body.position + Vector2.up * 8f;
            selected.SetAssaultDestination(assaultTarget);
            Assert.That(Phase(), Is.EqualTo("Chasing"));
            Assert.That(Read<float>(selected, "ramRemaining"), Is.Zero);
            Assert.That(Quaternion.Angle(visual.localRotation, authored), Is.LessThan(.01f));
            float assaultDistance = Vector2.Distance(body.position, assaultTarget);
            yield return WaitGameTime(.15f);
            Assert.That(selected.HasAssaultDestination, Is.True);
            Assert.That(Vector2.Distance(body.position, assaultTarget), Is.LessThan(assaultDistance - .1f));
            Assert.That(Phase(), Is.EqualTo("Chasing"), "Assault retains production control.");
            selected.SetAssaultDestination(null);
            yield return CoreTestSupport.Await(() => Phase() == "Preparing");
            selected.gameObject.SetActive(false);
            Assert.That(selected.IsCommittedRamExperiment, Is.False);
            Assert.That(Read<float>(selected, "ramRemaining"), Is.Zero);
            Assert.That(Read<Vector2>(selected, "ramDirection"), Is.EqualTo(Vector2.zero));
            Assert.That(Quaternion.Angle(visual.localRotation, authored), Is.LessThan(.01f));
            selected.gameObject.SetActive(true);
            yield return WaitGameTime(.1f);
            Assert.That(selected.IsCommittedRamExperiment, Is.False, "Reuse clears opt-in and pose.");
            Assert.That(Phase(), Is.EqualTo("Chasing"));
            Assert.That(Quaternion.Angle(visual.localRotation, authored), Is.LessThan(.01f));
            Assert.That(RunStateManager.Instance.IsDevelopmentRun, Is.True, "OFF never restores production credit.");
        }
        finally
        {
            spawner.DebugSetCommittedRamExperiment(false);
            foreach (var actor in actors) if (actor != null) Object.DestroyImmediate(actor);
            health.DebugInvulnerable = invulnerable;
            station.enabled = stationEnabled;
            EnemyDebugAiFreeze.SetFrozen(frozen);
            Time.timeScale = timeScale;
        }
    }

    [UnityTest]
    public IEnumerator BlockLockedShieldProductionMvpF1GameViewCapture()
    {
        typeof(BunkerStationPresentationTests).GetMethod("SetResolution", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, null);
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
        yield return new EnterPlayMode();
        yield return BlockLockedShieldProductionCaptureBody();
    }

    private static IEnumerator BlockLockedShieldProductionCaptureBody()
    {
        yield return BeginProductionRun(); // Keep the ordinary run alive: no PrepareProduction/StopRunGameplay.
        float readyDeadline = Time.realtimeSinceStartup + 10f;
        while (Object.FindFirstObjectByType<Subject42DebugMenu>() == null &&
               Time.realtimeSinceStartup < readyDeadline) yield return null;
        var player = Object.FindFirstObjectByType<CharacterSpawner>()?.SpawnedPlayer;
        var station = Object.FindFirstObjectByType<OrbitalStationRuntime>();
        var spawner = Object.FindFirstObjectByType<EnemySpawner>();
        var menu = Object.FindFirstObjectByType<Subject42DebugMenu>();
        Assert.That(player, Is.Not.Null, "Production player readiness timed out.");
        Assert.That(station, Is.Not.Null, "Production station readiness timed out.");
        Assert.That(station.IsInitialized && station.isActiveAndEnabled, Is.True);
        Assert.That(spawner, Is.Not.Null, "Production EnemySpawner readiness timed out.");
        Assert.That(spawner.isActiveAndEnabled && spawner.IsSpawningEnabled, Is.True, "Ordinary spawning must remain active.");
        Assert.That(menu, Is.Not.Null, "Real production F1 readiness timed out.");
        Assert.That(Camera.main != null && Camera.main.isActiveAndEnabled, Is.True);
        Assert.That(RunStateManager.Instance, Is.Not.Null);
        var health = player.GetComponent<PlayerHealth>();
        Assert.That(health, Is.Not.Null);
        bool invulnerable = health.DebugInvulnerable;
        bool frozen = EnemyDebugAiFreeze.IsFrozen;
        float timeScale = Time.timeScale;
        bool experiment = spawner.BlockLockedShieldsExperiment;
        GameObject actor = null;
        EnemyDirectionalShield shield = null;
        var playerBody = player.GetComponent<Rigidbody2D>();
        var originalPosition = playerBody.position;
        bool Visible(Vector3 position)
        {
            var v = Camera.main.WorldToViewportPoint(position);
            return v.z > 0f && v.x > .08f && v.x < .92f && v.y > .08f && v.y < .92f;
        }
        void AssertLive()
        {
            Assert.That(actor != null && actor.activeInHierarchy, Is.True, "F1-owned shield bearer must survive the observation.");
            Assert.That(actor.GetComponent<EnemyHealth>().IsDead, Is.False);
            Assert.That(shield.isActiveAndEnabled && shield.IsBlockLockedExperiment, Is.True);
            Assert.That(actor.GetComponent<Rigidbody2D>().simulated, Is.True);
            Assert.That(station.isActiveAndEnabled && station.IsInitialized, Is.True);
            Assert.That(spawner.isActiveAndEnabled && spawner.IsSpawningEnabled, Is.True);
            Assert.That(Camera.main != null && Camera.main.isActiveAndEnabled, Is.True);
            Assert.That(EnemyDebugAiFreeze.IsFrozen || Subject42DebugMenu.IsDebugMenuOpen, Is.False);
            Assert.That(Read<bool>(menu, "isOpen"), Is.False);
            Assert.That(Time.timeScale, Is.GreaterThan(0f));
            AssertBlockLockedShieldUpdateReady(shield, player.transform);
            Assert.That(RunStateManager.Instance.IsDevelopmentRun, Is.True);
        }
        try
        {
            health.DebugInvulnerable = true;
            EnemyDebugAiFreeze.SetFrozen(false);
            OpenEscortF1(menu);
            Assert.That(Read<bool>(menu, "isOpen"), Is.True);
            Assert.That(spawner.BlockLockedShieldsExperiment, Is.False, "Fresh production attempts default OFF.");
            var tracked = Read<System.Collections.Generic.List<GameObject>>(menu, "debugEnemies");
            Assert.That(tracked, Is.Empty, "Only this scenario's manual actor may be cleaned up.");
            var toggle = EscortF1Button(menu, "ПЕРЕКЛЮЧИТЬ", "Эксперимент: заклинивающий щит");
            Assert.That(toggle.interactable, Is.True);
            toggle.onClick.Invoke();
            Assert.That(spawner.BlockLockedShieldsExperiment, Is.True);
            Assert.That(RunStateManager.Instance.IsDevelopmentRun, Is.True);
            var spawn = EscortF1Button(menu, "+1 ЗАКЛИНИВАЮЩИЙ");
            Assert.That(spawn.interactable, Is.True, "Current sector must offer an eligible shield bearer; no substitute prefab.");
            spawn.onClick.Invoke();
            Assert.That(tracked.Count, Is.EqualTo(1), Read<string>(menu, "enemyDebugStatus"));
            actor = tracked.Single();
            Assert.That(EnemySpawner.IsBlockLockedShieldEligible(actor), Is.True);
            var profile = RunStateManager.Instance.CurrentSector.SpawnProfile;
            Assert.That(profile.Phases.Where(p => p != null && p.enemies != null).SelectMany(p => p.enemies)
                .Any(e => e?.enemyPrefab != null && EnemySpawner.IsBlockLockedShieldPrefabEligible(e.enemyPrefab)), Is.True,
                "Eligibility must come from the current production profile, without a fallback prefab.");
            shield = actor.GetComponent<EnemyDirectionalShield>();
            Assert.That(shield, Is.Not.Null);
            actor.GetComponent<EnemyHealth>().SetRuntimeMaxHealth(10000f); // Diagnostic survivability, removed with owner cleanup.
            yield return FocusBlockLockedShieldGameView();
            AssertLive();
            Assert.That(GameplayAreaService.Instance, Is.Not.Null);
            bool Free(Vector2 point, float radius)
            {
                if (!Visible(point) || !GameplayAreaService.Instance.IsInsidePlayableArea(point, radius) ||
                    !GameplayAreaService.Instance.IsInsideSpawnArea(point, radius)) return false;
                return Physics2D.OverlapCircleAll(point, radius).All(c =>
                    c.transform.IsChildOf(actor.transform) || c.transform.IsChildOf(player.transform) ||
                    c == GameplayAreaService.Instance.PlayableArea || c == GameplayAreaService.Instance.SpawnArea ||
                    (c.isTrigger && c.GetComponentInParent<EnemyHealth>() == null));
            }
            var gun = station.Modules.OfType<OrbitalProjectileModule>().FirstOrDefault(m => m.CurrentMount != null && m.Kind == OrbitalModuleKind.Pistol);
            Assert.That(gun, Is.Not.Null, "Observe an existing mounted production weapon.");
            var radial = (gun.WorldPosition - (Vector2)station.transform.position).normalized;
            Vector2? destination = null;
            // Align the plate centre with the existing fixed firing lane; never edit facing.
            for (float distance = 3f; distance <= 4.5f; distance += .5f)
            {
                var candidate = gun.WorldPosition + radial * distance -
                    (Vector2)actor.transform.TransformVector(shield.LocalShieldCenter);
                bool laneClear = Physics2D.LinecastAll(gun.WorldPosition, candidate).All(hit =>
                    hit.collider.transform.IsChildOf(actor.transform) || hit.collider.transform.IsChildOf(player.transform) ||
                    hit.collider.transform.IsChildOf(station.transform) || hit.collider.isTrigger);
                if (Free(candidate, .7f) && laneClear) { destination = candidate; break; }
            }
            Assert.That(destination.HasValue, Is.True, "No free visible firing-lane point; no placement bypass.");
            actor.GetComponent<Rigidbody2D>().position = destination.Value;
            Physics2D.SyncTransforms();
            int blocks = shield.Blocks;
            // A projectile may already be in flight at placement; observe the weapon's actual shot counter.
            Debug.Log("BlockLockedShield capture: one enemy relocation before observation; ordinary weapon/AI/physics active.");
            float blockDeadline = Time.realtimeSinceStartup + 10f;
            while ((shield.Blocks <= blocks || shield.BlockLockRemaining <= 0f) && Time.realtimeSinceStartup < blockDeadline)
            {
                AssertLive();
                Assert.That(shield.IsBroken, Is.False, "Plate broke before a natural observed lock.");
                yield return null;
            }
            Assert.That(shield.Blocks, Is.GreaterThan(blocks), "Production projectile collision must deliver the block.");
            Assert.That(gun.ShotsFired, Is.GreaterThan(0), "The existing station weapon must actually fire.");
            Assert.That(shield.BlockLockRemaining, Is.GreaterThan(0f), "Natural block window was not observed.");
            var lockedForward = shield.Forward;
            var side = new Vector2(-lockedForward.y, lockedForward.x);
            Vector2? crossing = null;
            foreach (float sign in new[] { 1f, -1f })
            {
                var candidate = playerBody.position + side * sign * 3f;
                if (Free(candidate, .6f)) { crossing = candidate; break; }
            }
            Assert.That(crossing.HasValue, Is.True, "No free diagnostic cross-lane player point.");
            playerBody.position = crossing.Value;
            Physics2D.SyncTransforms();
            Debug.Log("BlockLockedShield capture: diagnostic player relocation across locked direction; integration evidence, not human dodging.");
            yield return new WaitForEndOfFrame();
            AssertLive();
            Assert.That(Visible(actor.transform.position), Is.True);
            Assert.That(shield.BlockLockRemaining, Is.GreaterThan(0f));
            Assert.That(Vector2.Angle(shield.Forward, lockedForward), Is.LessThan(.01f));
            var locked = CaptureFlankingGameView("shield-locked", "BlockLockedShield");
            Assert.That(locked.MoveNext(), Is.True); // Request now; defer IO beyond the short window.
            float deadline = Time.realtimeSinceStartup + 5f;
            while (shield.BlockLockRemaining > 0f && Time.realtimeSinceStartup < deadline)
            {
                AssertLive();
                Assert.That(Vector2.Angle(shield.Forward, lockedForward), Is.LessThan(.01f));
                yield return null;
            }
            Assert.That(shield.BlockLockRemaining, Is.Zero);
            yield return CoreTestSupport.Await(() => Vector2.Angle(shield.Forward, lockedForward) > 4f);
            yield return new WaitForEndOfFrame();
            AssertLive();
            Assert.That(Visible(actor.transform.position), Is.True);
            Assert.That(Vector2.Angle(shield.Forward, lockedForward), Is.GreaterThan(4f), "Natural tracking resumes after lock.");
            Assert.That(shield.BlockLockRemaining, Is.Zero, "Tracking capture must be outside a lock window.");
            var tracking = CaptureFlankingGameView("shield-tracking", "BlockLockedShield");
            Assert.That(tracking.MoveNext(), Is.True);
            yield return locked.Current;
            while (locked.MoveNext()) yield return locked.Current;
            yield return tracking.Current;
            while (tracking.MoveNext()) yield return tracking.Current;
            AssertLive();
        }
        finally
        {
            EscortF1Call(menu, "CloseMenu");
            EscortF1Call(menu, "ClearDebugEnemies"); // Existing owner also removes diagnostic extra HP.
            spawner.DebugSetBlockLockedShieldsExperiment(experiment);
            playerBody.position = originalPosition;
            Physics2D.SyncTransforms();
            health.DebugInvulnerable = invulnerable;
            EnemyDebugAiFreeze.SetFrozen(frozen);
            Time.timeScale = timeScale;
        }
    }

    [UnityTest]
    public IEnumerator CommittedRamProductionMvpF1GameViewCapture()
    {
        typeof(BunkerStationPresentationTests).GetMethod("SetResolution", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, null);
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
        yield return new EnterPlayMode();
        yield return CommittedRamProductionCaptureBody();
    }

    private static IEnumerator CommittedRamProductionCaptureBody()
    {
        yield return BeginProductionRun(); // Keep the ordinary run alive: no PrepareProduction/StopRunGameplay.
        float readyDeadline = Time.realtimeSinceStartup + 10f;
        while (Object.FindFirstObjectByType<Subject42DebugMenu>() == null &&
               Time.realtimeSinceStartup < readyDeadline) yield return null;
        var player = Object.FindFirstObjectByType<CharacterSpawner>()?.SpawnedPlayer;
        var station = Object.FindFirstObjectByType<OrbitalStationRuntime>();
        var spawner = Object.FindFirstObjectByType<EnemySpawner>();
        var menu = Object.FindFirstObjectByType<Subject42DebugMenu>();
        Assert.That(player, Is.Not.Null, "Production player readiness timed out.");
        Assert.That(station, Is.Not.Null, "Production station readiness timed out.");
        Assert.That(station.IsInitialized && station.isActiveAndEnabled, Is.True);
        Assert.That(spawner, Is.Not.Null, "Production EnemySpawner readiness timed out.");
        Assert.That(spawner.isActiveAndEnabled && spawner.IsSpawningEnabled, Is.True, "Ordinary spawning must remain active.");
        Assert.That(menu, Is.Not.Null, "Real production F1 readiness timed out.");
        Assert.That(Camera.main != null && Camera.main.isActiveAndEnabled, Is.True);
        Assert.That(RunStateManager.Instance, Is.Not.Null);
        var health = player.GetComponent<PlayerHealth>();
        Assert.That(health, Is.Not.Null);
        bool invulnerable = health.DebugInvulnerable;
        bool frozen = EnemyDebugAiFreeze.IsFrozen;
        float timeScale = Time.timeScale;
        bool experiment = spawner.CommittedRamExperiment;
        GameObject actor = null;
        EnemyChaseMovement chase = null;
        string Phase() => chase == null ? "Missing" : Read<object>(chase, "ramPhase").ToString();
        bool Visible(Vector3 position)
        {
            var v = Camera.main.WorldToViewportPoint(position);
            return v.z > 0f && v.x > .08f && v.x < .92f && v.y > .08f && v.y < .92f;
        }
        void AssertLive()
        {
            Assert.That(actor != null && actor.activeInHierarchy, Is.True, "F1-owned chase must survive the observation.");
            Assert.That(actor.GetComponent<EnemyHealth>().IsDead, Is.False);
            Assert.That(chase.isActiveAndEnabled && chase.IsCommittedRamExperiment, Is.True);
            Assert.That(actor.GetComponent<Rigidbody2D>().simulated, Is.True);
            Assert.That(station.isActiveAndEnabled && station.IsInitialized, Is.True);
            Assert.That(spawner.isActiveAndEnabled && spawner.IsSpawningEnabled, Is.True);
            Assert.That(Camera.main != null && Camera.main.isActiveAndEnabled, Is.True);
            Assert.That(EnemyDebugAiFreeze.IsFrozen || Subject42DebugMenu.IsDebugMenuOpen, Is.False);
            Assert.That(Read<bool>(menu, "isOpen"), Is.False);
            Assert.That(Time.timeScale, Is.GreaterThan(0f));
            Assert.That(RunStateManager.Instance.IsDevelopmentRun, Is.True);
        }
        try
        {
            health.DebugInvulnerable = true;
            EnemyDebugAiFreeze.SetFrozen(false);
            OpenEscortF1(menu);
            Assert.That(Read<bool>(menu, "isOpen"), Is.True);
            Assert.That(spawner.CommittedRamExperiment, Is.False, "Fresh production attempts default OFF.");
            var tracked = Read<System.Collections.Generic.List<GameObject>>(menu, "debugEnemies");
            Assert.That(tracked, Is.Empty, "Only this scenario's manual actor may be cleaned up.");
            var toggle = EscortF1Button(menu, "ПЕРЕКЛЮЧИТЬ", "Эксперимент: таранный преследователь");
            Assert.That(toggle.interactable, Is.True);
            toggle.onClick.Invoke();
            Assert.That(spawner.CommittedRamExperiment, Is.True);
            Assert.That(RunStateManager.Instance.IsDevelopmentRun, Is.True);
            var spawn = EscortF1Button(menu, "+1 ТАРАННЫЙ");
            Assert.That(spawn.interactable, Is.True, "Current sector must offer an eligible chase; no substitute prefab.");
            spawn.onClick.Invoke();
            Assert.That(tracked.Count, Is.EqualTo(1), Read<string>(menu, "enemyDebugStatus"));
            actor = tracked.Single();
            Assert.That(EnemySpawner.IsCommittedRamEligible(actor), Is.True);
            chase = actor.GetComponent<EnemyChaseMovement>();
            actor.GetComponent<EnemyHealth>().SetRuntimeMaxHealth(10000f); // Diagnostic survivability, removed with owner cleanup.
            AssertLive();
            Assert.That(GameplayAreaService.Instance, Is.Not.Null);
            // At most one relocation, before observing any cycle; retain normal movement afterwards.
            if (!Visible(actor.transform.position) || Vector2.Distance(actor.transform.position, player.transform.position) < 3.5f ||
                Vector2.Distance(actor.transform.position, player.transform.position) > 4.5f)
            {
                Vector3? destination = null;
                for (int i = 0; i < 24; i++)
                {
                    float angle = i * Mathf.PI * 2f / 24f;
                    var candidate = player.transform.position + new Vector3(Mathf.Cos(angle), Mathf.Sin(angle)) * 4f;
                    if (!Visible(candidate) || !GameplayAreaService.Instance.IsInsidePlayableArea(candidate, .6f) ||
                        !GameplayAreaService.Instance.IsInsideSpawnArea(candidate, .6f)) continue;
                    bool clear = Physics2D.OverlapCircleAll(candidate, .6f).All(c =>
                        c.transform.IsChildOf(actor.transform) || c == GameplayAreaService.Instance.PlayableArea ||
                        c == GameplayAreaService.Instance.SpawnArea ||
                        (c.isTrigger && c.GetComponentInParent<EnemyHealth>() == null));
                    if (clear) { destination = candidate; break; }
                }
                Assert.That(destination.HasValue, Is.True, "No visible free world point: do not bypass placement.");
                actor.GetComponent<Rigidbody2D>().position = destination.Value;
                Physics2D.SyncTransforms();
                Debug.Log("CommittedRam capture: one diagnostic relocation before natural observation: " + destination.Value);
            }
            yield return CoreTestSupport.Await(() => Phase() == "Preparing" && Visible(actor.transform.position));
            yield return new WaitForEndOfFrame();
            AssertLive();
            Assert.That(Phase(), Is.EqualTo("Preparing"));
            Assert.That(Visible(actor.transform.position), Is.True);
            var preparing = CaptureFlankingGameView("ram-preparing", "CommittedRam");
            // Execute the helper's screenshot request immediately after phase/visibility assertions.
            Assert.That(preparing.MoveNext(), Is.True);
            // Defer file-write waiting until after the cycle, so capture IO cannot hide Charging.
            // MoveNext above already requested the actual screenshot in this preparation frame.
            var visual = Read<Transform>(chase, "flankingVisual");
            var authored = Read<Quaternion>(chase, "flankingAuthoredRotation");
            Assert.That(visual, Is.Not.Null);
            Assert.That(Quaternion.Angle(visual.localRotation, authored), Is.EqualTo(20f).Within(.05f));
            yield return CoreTestSupport.Await(() => Phase() == "Charging");
            AssertLive();
            var body = actor.GetComponent<Rigidbody2D>();
            var chargeStart = body.position;
            float maxTravel = 0f;
            float deadline = Time.realtimeSinceStartup + 6f;
            while (Phase() == "Charging" && Time.realtimeSinceStartup < deadline)
            {
                AssertLive();
                maxTravel = Mathf.Max(maxTravel, Vector2.Distance(chargeStart, body.position));
                yield return new WaitForFixedUpdate();
            }
            maxTravel = Mathf.Max(maxTravel, Vector2.Distance(chargeStart, body.position));
            Assert.That(Phase(), Is.EqualTo("Recovering"), "Natural charge must finish without timer edits.");
            Assert.That(maxTravel, Is.GreaterThan(.1f), "Observe physical travel during Charging in the live battle.");
            Assert.That(Quaternion.Angle(visual.localRotation, authored), Is.LessThan(.01f), "Recovery restores authored pose.");
            yield return new WaitForEndOfFrame();
            AssertLive();
            Assert.That(Phase(), Is.EqualTo("Recovering"));
            Assert.That(Visible(actor.transform.position), Is.True, "Recovery capture requires a visible enemy.");
            Assert.That(Quaternion.Angle(visual.localRotation, authored), Is.LessThan(.01f));
            var recovery = CaptureFlankingGameView("ram-recovery", "CommittedRam");
            Assert.That(recovery.MoveNext(), Is.True);
            yield return preparing.Current;
            while (preparing.MoveNext()) yield return preparing.Current;
            yield return recovery.Current;
            while (recovery.MoveNext()) yield return recovery.Current;
            AssertLive();
        }
        finally
        {
            EscortF1Call(menu, "CloseMenu");
            EscortF1Call(menu, "ClearDebugEnemies"); // Existing owner also removes diagnostic extra HP.
            spawner.DebugSetCommittedRamExperiment(experiment);
            health.DebugInvulnerable = invulnerable;
            EnemyDebugAiFreeze.SetFrozen(frozen);
            Time.timeScale = timeScale;
        }
    }

    [UnityTest]
    public IEnumerator StrafingShooterProductionMvpF1GameViewCapture()
    {
        typeof(BunkerStationPresentationTests).GetMethod("SetResolution", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, null);
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
        yield return new EnterPlayMode();
        yield return StrafingShooterProductionCaptureBody();
    }

    private static IEnumerator StrafingShooterProductionCaptureBody()
    {
        yield return BeginProductionRun(); // Keep the ordinary run alive: no PrepareProduction/StopRunGameplay.
        float readyDeadline = Time.realtimeSinceStartup + 10f;
        while (Object.FindFirstObjectByType<Subject42DebugMenu>() == null &&
               Time.realtimeSinceStartup < readyDeadline) yield return null;
        var player = Object.FindFirstObjectByType<CharacterSpawner>()?.SpawnedPlayer;
        var station = Object.FindFirstObjectByType<OrbitalStationRuntime>();
        var spawner = Object.FindFirstObjectByType<EnemySpawner>();
        var menu = Object.FindFirstObjectByType<Subject42DebugMenu>();
        Assert.That(player, Is.Not.Null, "Production player readiness timed out.");
        Assert.That(station, Is.Not.Null, "Production station readiness timed out.");
        Assert.That(station.IsInitialized && station.isActiveAndEnabled, Is.True);
        Assert.That(spawner, Is.Not.Null, "Production EnemySpawner readiness timed out.");
        Assert.That(spawner.isActiveAndEnabled && spawner.IsSpawningEnabled, Is.True, "Ordinary spawning must remain active.");
        Assert.That(menu, Is.Not.Null, "Real production F1 readiness timed out.");
        Assert.That(Camera.main != null && Camera.main.isActiveAndEnabled, Is.True);
        Assert.That(RunStateManager.Instance, Is.Not.Null);
        var health = player.GetComponent<PlayerHealth>();
        Assert.That(health, Is.Not.Null);
        bool invulnerable = health.DebugInvulnerable;
        bool frozen = EnemyDebugAiFreeze.IsFrozen;
        float timeScale = Time.timeScale;
        bool experiment = spawner.StrafingShootersExperiment;
        GameObject actor = null;
        EnemyShooterMovement shooter = null;
        bool Visible(Vector3 position)
        {
            var v = Camera.main.WorldToViewportPoint(position);
            return v.z > 0f && v.x > .08f && v.x < .92f && v.y > .08f && v.y < .92f;
        }
        void AssertLive()
        {
            Assert.That(actor != null && actor.activeInHierarchy, Is.True, "F1-owned shooter must survive the observation.");
            Assert.That(actor.GetComponent<EnemyHealth>().IsDead, Is.False);
            Assert.That(shooter.isActiveAndEnabled && shooter.StrafingExperiment, Is.True);
            var owned = Read<System.Collections.Generic.List<GameObject>>(menu, "debugEnemies");
            Assert.That(owned.Count, Is.EqualTo(1), "Existing debugEnemies remains the sole manual-spawn owner.");
            Assert.That(owned[0], Is.SameAs(actor));
            Assert.That(actor.GetComponent<Rigidbody2D>().simulated, Is.True);
            Assert.That(station.isActiveAndEnabled && station.IsInitialized, Is.True);
            Assert.That(spawner.isActiveAndEnabled && spawner.IsSpawningEnabled, Is.True);
            Assert.That(Camera.main != null && Camera.main.isActiveAndEnabled, Is.True);
            Assert.That(EnemyDebugAiFreeze.IsFrozen || Subject42DebugMenu.IsDebugMenuOpen, Is.False);
            Assert.That(Read<bool>(menu, "isOpen"), Is.False);
            Assert.That(Time.timeScale, Is.GreaterThan(0f));
            Assert.That(RunStateManager.Instance.IsDevelopmentRun, Is.True);
        }
        try
        {
            health.DebugInvulnerable = true;
            EnemyDebugAiFreeze.SetFrozen(false);
            OpenEscortF1(menu);
            Assert.That(Read<bool>(menu, "isOpen"), Is.True);
            Assert.That(spawner.StrafingShootersExperiment, Is.False, "Fresh production attempts default OFF.");
            var tracked = Read<System.Collections.Generic.List<GameObject>>(menu, "debugEnemies");
            Assert.That(tracked, Is.Empty, "Only this scenario's manual actor may be cleaned up.");
            var toggle = EscortF1Button(menu, "ПЕРЕКЛЮЧИТЬ", "Эксперимент: стрелок на траверсе");
            Assert.That(toggle.interactable, Is.True);
            toggle.onClick.Invoke();
            Assert.That(spawner.StrafingShootersExperiment, Is.True);
            Assert.That(RunStateManager.Instance.IsDevelopmentRun, Is.True);
            var spawn = EscortF1Button(menu, "+1 НА ТРАВЕРСЕ");
            Assert.That(spawn.interactable, Is.True, "Current sector must offer an eligible shooter; no substitute prefab.");
            spawn.onClick.Invoke();
            Assert.That(tracked.Count, Is.EqualTo(1), Read<string>(menu, "enemyDebugStatus"));
            actor = tracked.Single();
            var profile = RunStateManager.Instance.CurrentSector.SpawnProfile;
            Assert.That(profile.Phases.Where(p => p != null && p.enemies != null).SelectMany(p => p.enemies)
                .Where(e => e?.enemyPrefab != null).Select(e => e.enemyPrefab)
                .Any(EnemySpawner.IsStrafingShooterEligible), Is.True, "Eligibility must come from the current authored profile.");
            Assert.That(EnemySpawner.IsStrafingShooterEligible(actor), Is.False, "Assigned actor cannot receive a second strafe assignment.");
            shooter = actor.GetComponent<EnemyShooterMovement>();
            actor.GetComponent<EnemyHealth>().SetRuntimeMaxHealth(10000f); // Diagnostic survivability, removed with owner cleanup.
            AssertLive();
            yield return null; // Let Start bind the real player before observing movement/shooting.
            Assert.That(Read<Transform>(shooter, "player"), Is.EqualTo(player.transform));
            Assert.That(shooter.CommittedVolleyExperiment, Is.False);
            var area = GameplayAreaService.Instance;
            Assert.That(area, Is.Not.Null);
            var body = actor.GetComponent<Rigidbody2D>();
            float preferred = Read<float>(shooter, "preferredDistance");
            float tolerance = Read<float>(shooter, "distanceTolerance");
            Vector2? destination = null;
            for (int i = 0; i < 24; i++)
            {
                float angle = i * Mathf.PI / 12f;
                Vector2 candidate = (Vector2)player.transform.position + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * preferred;
                if (!Visible(candidate) || !area.IsInsidePlayableArea(candidate, .8f) || !area.IsInsideSpawnArea(candidate, .8f)) continue;
                bool clear = Physics2D.OverlapCircleAll(candidate, .8f).All(c => c.transform.IsChildOf(actor.transform) ||
                    c == area.PlayableArea || c == area.SpawnArea ||
                    (c.isTrigger && c.GetComponentInParent<EnemyHealth>() == null));
                if (clear) { destination = candidate; break; }
            }
            Assert.That(destination.HasValue, Is.True, "No free visible authored-distance point; preserve production placement.");
            body.position = destination.Value; // Only relocation; no changes to velocity, steering or authored parameters.
            Physics2D.SyncTransforms();
            Debug.Log("StrafingShooter capture: one diagnostic relocation before observation: " + destination.Value);
            yield return new WaitForEndOfFrame();
            AssertLive();
            Assert.That(Visible(actor.transform.position), Is.True);
            var first = CaptureFlankingGameView("strafe-start", "StrafingShooter");
            Assert.That(first.MoveNext(), Is.True); // Request now; defer file wait until after the live observation.
            yield return first.Current;

            Vector2 origin = body.position;
            Vector2 radial = (origin - (Vector2)player.transform.position).normalized;
            Vector2 tangent = new Vector2(radial.y, -radial.x);
            var firePoint = Read<Transform>(shooter, "firePoint");
            float previousTimer = Read<float>(shooter, "fireTimer");
            var previousProjectiles = Object.FindObjectsByType<EnemyProjectile>(FindObjectsSortMode.None)
                .Where(p => p.isActiveAndEnabled).Select(p => p.GetInstanceID()).ToHashSet();
            EnemyProjectile observed = null;
            Vector2 projectileOrigin = Vector2.zero;
            bool projectileMoved = false;
            float maxLateral = 0f;
            float deadline = Time.realtimeSinceStartup + 6f;
            while (Time.realtimeSinceStartup < deadline && (!projectileMoved || maxLateral < .35f))
            {
                AssertLive();
                float distance = Vector2.Distance(body.position, player.transform.position);
                Assert.That(distance, Is.InRange(preferred - tolerance, preferred + tolerance),
                    "Natural observation must stay inside the authored strafe band.");
                maxLateral = Mathf.Max(maxLateral, Mathf.Abs(Vector2.Dot(body.position - origin, tangent)));
                var projectiles = Object.FindObjectsByType<EnemyProjectile>(FindObjectsSortMode.None).Where(p => p.isActiveAndEnabled).ToArray();
                float timer = Read<float>(shooter, "fireTimer");
                if (timer > previousTimer + .1f && observed == null)
                {
                    Vector2 expected = ((Vector2)player.transform.position - (Vector2)firePoint.position).normalized;
                    var candidates = projectiles.Where(p => !previousProjectiles.Contains(p.GetInstanceID()) &&
                        Vector2.Distance(p.transform.position, firePoint.position) < .8f &&
                        Vector2.Dot(Read<Vector2>(p, "direction"), expected) > .995f &&
                        p.DebugTravelVelocity.sqrMagnitude > 0f).ToArray();
                    // Reject ambiguous nearby ordinary-wave emitters instead of attributing their shot to this actor.
                    bool isolatedMuzzle = Object.FindObjectsByType<EnemyShooterMovement>(FindObjectsSortMode.None)
                        .Where(s => s != shooter && s.isActiveAndEnabled).All(s =>
                            Vector2.Distance((Read<Transform>(s, "firePoint") ?? s.transform).position, firePoint.position) > 2f);
                    if (isolatedMuzzle && candidates.Length == 1)
                    {
                        observed = candidates[0];
                        projectileOrigin = observed.transform.position;
                        Debug.Log("StrafingShooter natural fireTimer reset + isolated muzzle projectile id=" + observed.GetInstanceID());
                    }
                }
                if (observed != null && observed.isActiveAndEnabled &&
                    Vector2.Distance(observed.transform.position, projectileOrigin) > .08f &&
                    observed.DebugTravelVelocity.sqrMagnitude > 0f) projectileMoved = true;
                previousTimer = timer;
                previousProjectiles = projectiles.Select(p => p.GetInstanceID()).ToHashSet();
                yield return null;
            }
            Assert.That(maxLateral, Is.GreaterThanOrEqualTo(.35f), "Natural Rigidbody2D lateral displacement is required.");
            Assert.That(projectileMoved, Is.True,
                "Require this shooter's natural timer reset, unique fresh muzzle projectile and subsequent real travel; ordinary-wave shots are insufficient.");
            yield return new WaitForEndOfFrame();
            AssertLive();
            Assert.That(Visible(actor.transform.position), Is.True, "Moving capture requires a visible living shooter.");
            Assert.That(Mathf.Abs(Vector2.Dot(body.position - origin, tangent)), Is.GreaterThan(.35f));
            var second = CaptureFlankingGameView("strafe-moving", "StrafingShooter");
            Assert.That(second.MoveNext(), Is.True);
            yield return second.Current;
            while (second.MoveNext()) yield return second.Current;
            while (first.MoveNext()) yield return first.Current;
            AssertLive();
        }
        finally
        {
            EscortF1Call(menu, "CloseMenu");
            EscortF1Call(menu, "ClearDebugEnemies"); // Existing owner also removes diagnostic extra HP.
            spawner.DebugSetStrafingShootersExperiment(experiment);
            health.DebugInvulnerable = invulnerable;
            EnemyDebugAiFreeze.SetFrozen(frozen);
            Time.timeScale = timeScale;
        }
    }

    [UnityTest]
    public IEnumerator CommittedVolleyProductionMvpF1GameViewCapture()
    {
        typeof(BunkerStationPresentationTests).GetMethod("SetResolution", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, null);
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
        yield return new EnterPlayMode();
        yield return CommittedVolleyProductionCaptureBody();
    }

    private static IEnumerator CommittedVolleyProductionCaptureBody()
    {
        yield return BeginProductionRun(); // Keep the ordinary run alive: no PrepareProduction/StopRunGameplay.
        float readyDeadline = Time.realtimeSinceStartup + 10f;
        while (Object.FindFirstObjectByType<Subject42DebugMenu>() == null &&
               Time.realtimeSinceStartup < readyDeadline) yield return null;
        var player = Object.FindFirstObjectByType<CharacterSpawner>()?.SpawnedPlayer;
        var station = Object.FindFirstObjectByType<OrbitalStationRuntime>();
        var spawner = Object.FindFirstObjectByType<EnemySpawner>();
        var menu = Object.FindFirstObjectByType<Subject42DebugMenu>();
        Assert.That(player, Is.Not.Null, "Production player readiness timed out.");
        Assert.That(station, Is.Not.Null, "Production station readiness timed out.");
        Assert.That(station.IsInitialized && station.isActiveAndEnabled, Is.True);
        Assert.That(spawner, Is.Not.Null, "Production EnemySpawner readiness timed out.");
        Assert.That(spawner.isActiveAndEnabled && spawner.IsSpawningEnabled, Is.True, "Ordinary spawning must remain active.");
        Assert.That(menu, Is.Not.Null, "Real production F1 readiness timed out.");
        Assert.That(Camera.main != null && Camera.main.isActiveAndEnabled, Is.True);
        Assert.That(RunStateManager.Instance, Is.Not.Null);
        var health = player.GetComponent<PlayerHealth>();
        Assert.That(health, Is.Not.Null);
        bool invulnerable = health.DebugInvulnerable;
        bool frozen = EnemyDebugAiFreeze.IsFrozen;
        float timeScale = Time.timeScale;
        bool experiment = spawner.CommittedVolleyExperiment;
        GameObject actor = null;
        EnemyShooterMovement shooter = null;
        string Phase() => shooter == null ? "Missing" : Read<object>(shooter, "volleyPhase").ToString();
        bool Visible(Vector3 position)
        {
            var v = Camera.main.WorldToViewportPoint(position);
            return v.z > 0f && v.x > .08f && v.x < .92f && v.y > .08f && v.y < .92f;
        }
        void AssertLive()
        {
            Assert.That(actor != null && actor.activeInHierarchy, Is.True, "F1-owned shooter must survive the observation.");
            Assert.That(actor.GetComponent<EnemyHealth>().IsDead, Is.False);
            Assert.That(shooter.isActiveAndEnabled && shooter.CommittedVolleyExperiment, Is.True);
            Assert.That(actor.GetComponent<Rigidbody2D>().simulated, Is.True);
            Assert.That(station.isActiveAndEnabled && station.IsInitialized, Is.True);
            Assert.That(spawner.isActiveAndEnabled && spawner.IsSpawningEnabled, Is.True);
            Assert.That(Camera.main != null && Camera.main.isActiveAndEnabled, Is.True);
            Assert.That(EnemyDebugAiFreeze.IsFrozen || Subject42DebugMenu.IsDebugMenuOpen, Is.False);
            Assert.That(Read<bool>(menu, "isOpen"), Is.False);
            Assert.That(Time.timeScale, Is.GreaterThan(0f));
            Assert.That(RunStateManager.Instance.IsDevelopmentRun, Is.True);
        }
        try
        {
            health.DebugInvulnerable = true;
            EnemyDebugAiFreeze.SetFrozen(false);
            OpenEscortF1(menu);
            Assert.That(Read<bool>(menu, "isOpen"), Is.True);
            Assert.That(spawner.CommittedVolleyExperiment, Is.False, "Fresh production attempts default OFF.");
            var tracked = Read<System.Collections.Generic.List<GameObject>>(menu, "debugEnemies");
            Assert.That(tracked, Is.Empty, "Only this scenario's manual actor may be cleaned up.");
            var toggle = EscortF1Button(menu, "ПЕРЕКЛЮЧИТЬ", "Эксперимент: залповый стрелок");
            Assert.That(toggle.interactable, Is.True);
            toggle.onClick.Invoke();
            Assert.That(spawner.CommittedVolleyExperiment, Is.True);
            Assert.That(RunStateManager.Instance.IsDevelopmentRun, Is.True);
            var spawn = EscortF1Button(menu, "+1 ЗАЛПОВЫЙ");
            Assert.That(spawn.interactable, Is.True, "Current sector must offer an eligible shooter; no substitute prefab.");
            spawn.onClick.Invoke();
            Assert.That(tracked.Count, Is.EqualTo(1), Read<string>(menu, "enemyDebugStatus"));
            actor = tracked.Single();
            Assert.That(EnemySpawner.IsCommittedVolleyEligible(actor), Is.True);
            shooter = actor.GetComponent<EnemyShooterMovement>();
            actor.GetComponent<EnemyHealth>().SetRuntimeMaxHealth(10000f); // Diagnostic survivability, removed with owner cleanup.
            AssertLive();
            Assert.That(GameplayAreaService.Instance, Is.Not.Null);
            // At most one relocation, before observing any cycle; retain normal movement afterwards.
            if (!Visible(actor.transform.position))
            {
                Vector3? destination = null;
                for (int i = 0; i < 24; i++)
                {
                    float angle = i * Mathf.PI * 2f / 24f;
                    var candidate = player.transform.position + new Vector3(Mathf.Cos(angle), Mathf.Sin(angle)) * 5.5f;
                    if (!Visible(candidate) || !GameplayAreaService.Instance.IsInsidePlayableArea(candidate, .6f) ||
                        !GameplayAreaService.Instance.IsInsideSpawnArea(candidate, .6f)) continue;
                    bool clear = Physics2D.OverlapCircleAll(candidate, .6f).All(c =>
                        c.transform.IsChildOf(actor.transform) || c == GameplayAreaService.Instance.PlayableArea ||
                        c == GameplayAreaService.Instance.SpawnArea ||
                        (c.isTrigger && c.GetComponentInParent<EnemyHealth>() == null));
                    if (clear) { destination = candidate; break; }
                }
                Assert.That(destination.HasValue, Is.True, "No visible free world point: do not bypass placement.");
                actor.GetComponent<Rigidbody2D>().position = destination.Value;
                Physics2D.SyncTransforms();
                Debug.Log("CommittedVolley capture: one diagnostic relocation before natural observation: " + destination.Value);
            }
            yield return CoreTestSupport.Await(() => Phase() == "Preparing" && Visible(actor.transform.position));
            yield return new WaitForEndOfFrame();
            AssertLive();
            Assert.That(Phase(), Is.EqualTo("Preparing"));
            Assert.That(Visible(actor.transform.position), Is.True);
            var preparing = CaptureFlankingGameView("volley-preparing", "CommittedVolley");
            // Execute the helper's screenshot request immediately after phase/visibility assertions.
            Assert.That(preparing.MoveNext(), Is.True);
            yield return preparing.Current;
            while (preparing.MoveNext()) yield return preparing.Current;

            // Observe a fresh full preparation, avoiding any file-write wait consuming its shots.
            yield return CoreTestSupport.Await(() => Phase() != "Preparing");
            yield return CoreTestSupport.Await(() => Phase() == "Preparing");
            var shots = new System.Collections.Generic.HashSet<int>();
            void ObserveActualShots()
            {
                var direction = Read<Vector2>(shooter, "volleyDirection");
                foreach (var projectile in Object.FindObjectsByType<EnemyProjectile>(FindObjectsSortMode.None))
                    if (Vector2.Distance(projectile.transform.position, actor.transform.position) < 2f &&
                        Vector2.Dot(Read<Vector2>(projectile, "direction"), direction) > .999f &&
                        projectile.DebugTravelVelocity.sqrMagnitude > 0f)
                        shots.Add(projectile.GetInstanceID());
            }
            int maxVolleyShots = 0;
            float deadline = Time.realtimeSinceStartup + 6f;
            while (Phase() != "Recovering" && Time.realtimeSinceStartup < deadline)
            {
                AssertLive();
                maxVolleyShots = Mathf.Max(maxVolleyShots, Read<int>(shooter, "volleyShots"));
                ObserveActualShots();
                yield return null;
            }
            Assert.That(Phase(), Is.EqualTo("Recovering"), "Natural volley must finish without timer edits.");
            ObserveActualShots(); // The third projectile is emitted on the transition into recovery.
            Assert.That(Read<int>(shooter, "volleyShots"), Is.EqualTo(3));
            Assert.That(maxVolleyShots, Is.GreaterThanOrEqualTo(2), "Observe firing between preparation and recovery.");
            Assert.That(shots.Count, Is.GreaterThanOrEqualTo(3), "Observe moving production projectiles alongside the live ordinary battle.");
            yield return new WaitForEndOfFrame();
            AssertLive();
            Assert.That(Phase(), Is.EqualTo("Recovering"));
            Assert.That(Visible(actor.transform.position), Is.True, "Recovery capture requires a visible enemy.");
            var recovery = CaptureFlankingGameView("volley-recovery", "CommittedVolley");
            Assert.That(recovery.MoveNext(), Is.True);
            yield return recovery.Current;
            while (recovery.MoveNext()) yield return recovery.Current;
            AssertLive();
        }
        finally
        {
            EscortF1Call(menu, "CloseMenu");
            EscortF1Call(menu, "ClearDebugEnemies"); // Existing owner also removes diagnostic extra HP.
            spawner.DebugSetCommittedVolleyExperiment(experiment);
            health.DebugInvulnerable = invulnerable;
            EnemyDebugAiFreeze.SetFrozen(frozen);
            Time.timeScale = timeScale;
        }
    }

    [UnityTest]
    public IEnumerator CommittedVolleyLocksDirectionAndPreservesExternalMotion()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
        yield return new EnterPlayMode();
        yield return CommittedVolleyMotionBody();
    }

    private static IEnumerator CommittedVolleyMotionBody()
    {
        yield return BeginProductionRun();
        var player = PrepareProduction();
        var health = player.GetComponent<PlayerHealth>();
        bool invulnerable = health.DebugInvulnerable;
        bool frozen = EnemyDebugAiFreeze.IsFrozen;
        float timeScale = Time.timeScale;
        var playerBody = player.GetComponent<Rigidbody2D>();
        var originalPosition = playerBody.position;
        var originalConstraints = playerBody.constraints;
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/_Project/prefabs/Enemies/p_Enemy_Shooter.prefab");
        var actor = Object.Instantiate(prefab, player.transform.position + Vector3.left * 12f, Quaternion.identity);
        var shooter = actor.GetComponent<EnemyShooterMovement>();
        var body = actor.GetComponent<Rigidbody2D>();
        var seen = new System.Collections.Generic.HashSet<int>();
        var positions = new System.Collections.Generic.Dictionary<int, Vector3>();
        var travelled = new System.Collections.Generic.HashSet<int>();
        var baseline = Object.FindObjectsByType<EnemyProjectile>(FindObjectsSortMode.None)
            .Select(projectile => projectile.GetInstanceID()).ToArray();
        string Phase() => Read<object>(shooter, "volleyPhase").ToString();
        void ObserveShots(Vector2 direction)
        {
            foreach (var projectile in Object.FindObjectsByType<EnemyProjectile>(FindObjectsSortMode.None))
            {
                if (baseline.Contains(projectile.GetInstanceID())) continue;
                Assert.That(Vector2.Dot(Read<Vector2>(projectile, "direction"), direction),
                    Is.GreaterThan(.999f), "Actual production projectile must keep the committed direction.");
                Assert.That(projectile.DebugTravelVelocity.sqrMagnitude, Is.GreaterThan(0f));
                int id = projectile.GetInstanceID();
                if (positions.TryGetValue(id, out var previous) &&
                    Vector3.Distance(projectile.transform.position, previous) > .001f)
                {
                    Assert.That(Vector2.Dot(((Vector2)(projectile.transform.position - previous)).normalized,
                        projectile.DebugTravelVelocity.normalized), Is.GreaterThan(.99f),
                        "Production projectile Update advances the actual object along its travel velocity.");
                    travelled.Add(id);
                }
                positions[id] = projectile.transform.position;
                seen.Add(id);
            }
        }
        try
        {
            health.DebugInvulnerable = true;
            EnemyDebugAiFreeze.SetFrozen(false);
            Time.timeScale = 1f;
            actor.GetComponent<EnemyHealth>().SetRuntimeMaxHealth(10000f);
            Assert.That(shooter.CanUseCommittedVolley, Is.True, "Production shooter needs a safe visualRoot.");
            // Opt in before the first Update can fire an ordinary shot into the observation set.
            shooter.ConfigureCommittedVolleyExperiment(true);
            yield return null; // Start still resolves the production player/firePoint itself.
            var visual = Read<Transform>(shooter, "visualRoot");
            var authoredScale = visual.localScale;
            var authoredRotation = visual.localRotation;
            float rootRotation = body.rotation;
            Set(shooter, typeof(EnemyShooterMovement), "fireTimer", 0f);
            yield return CoreTestSupport.Await(() => Phase() == "Preparing");
            var direction = Read<Vector2>(shooter, "volleyDirection");
            var lockedVisual = visual.rotation;
            Assert.That(Vector2.Dot(-(Vector2)visual.right, direction), Is.GreaterThan(.999f),
                "Existing left-facing artwork shows the committed line.");
            var stoppedAt = body.position;
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();
            Assert.That(Vector2.Distance(body.position, stoppedAt), Is.LessThan(.03f),
                "Preparing suppresses own approach even outside preferred distance.");
            shooter.SetWorldRuleExternalVelocity(Vector2.up * 2f);
            var externalStart = body.position;
            yield return WaitGameTime(.12f);
            Assert.That(body.position.y - externalStart.y, Is.GreaterThan(.1f), "External velocity still moves the body.");
            Assert.That(Mathf.Abs(body.position.x - externalStart.x), Is.LessThan(.04f));
            shooter.SetWorldRuleExternalVelocity(Vector2.zero);
            yield return new WaitForFixedUpdate();
            var committedPosition = body.position;
            playerBody.position = originalPosition + Vector2.up * 5f;
            Physics2D.SyncTransforms();
            float deadline = Time.realtimeSinceStartup + 5f;
            while (Phase() != "Recovering" && Time.realtimeSinceStartup < deadline)
            {
                Assert.That(Read<Vector2>(shooter, "volleyDirection"), Is.EqualTo(direction));
                Assert.That(Vector2.Distance(body.position, committedPosition), Is.LessThan(.06f),
                    "Preparing and firing suppress own movement after the player crosses the line.");
                Assert.That(Quaternion.Angle(visual.rotation, lockedVisual), Is.LessThan(.01f),
                    "Crossing the line cannot visually retarget the volley.");
                ObserveShots(direction);
                yield return null;
            }
            ObserveShots(direction);
            Assert.That(Phase(), Is.EqualTo("Recovering"));
            Assert.That(seen.Count, Is.EqualTo(3), "Observe three real instantiated production projectiles, not a shot counter.");
            Assert.That(Quaternion.Angle(visual.localRotation, authoredRotation), Is.LessThan(.01f));
            Assert.That(visual.localScale, Is.EqualTo(authoredScale));
            var recoveryStart = body.position;
            yield return WaitGameTime(.25f);
            ObserveShots(direction);
            Assert.That(Phase(), Is.EqualTo("Recovering"));
            Assert.That(seen.Count, Is.EqualTo(3), "Recovery cannot fire another shot.");
            Assert.That(travelled.Count, Is.EqualTo(3), "All three real projectile objects travelled during natural Update.");
            Assert.That(Vector2.Distance(body.position, recoveryStart), Is.GreaterThan(.1f),
                "Recovery resumes ordinary distance keeping.");
            Assert.That(body.rotation, Is.EqualTo(rootRotation).Within(.01f), "Physics root never aims the visual.");
        }
        finally
        {
            if (shooter != null) { shooter.SetWorldRuleExternalVelocity(Vector2.zero); shooter.ConfigureCommittedVolleyExperiment(false); }
            if (actor != null) Object.DestroyImmediate(actor);
            foreach (var projectile in Object.FindObjectsByType<EnemyProjectile>(FindObjectsSortMode.None))
                if (!baseline.Contains(projectile.GetInstanceID())) projectile.TryIntercept();
            playerBody.position = originalPosition;
            playerBody.constraints = originalConstraints;
            health.DebugInvulnerable = invulnerable;
            EnemyDebugAiFreeze.SetFrozen(frozen);
            Time.timeScale = timeScale;
        }
    }

    [UnityTest]
    public IEnumerator CommittedVolleyOptInCadenceAndLifecycle()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
        yield return new EnterPlayMode();
        yield return CommittedVolleyLifecycleBody();
    }

    private static IEnumerator CommittedVolleyLifecycleBody()
    {
        yield return BeginProductionRun();
        var player = PrepareProduction();
        var spawner = Object.FindFirstObjectByType<EnemySpawner>();
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/_Project/prefabs/Enemies/p_Enemy_Shooter.prefab");
        var actors = new System.Collections.Generic.List<GameObject>();
        var baseline = Object.FindObjectsByType<EnemyProjectile>(FindObjectsSortMode.None)
            .Select(projectile => projectile.GetInstanceID()).ToArray();
        bool frozen = EnemyDebugAiFreeze.IsFrozen;
        float timeScale = Time.timeScale;
        var spawn = typeof(EnemySpawner).GetMethod("SpawnEnemyAt", BindingFlags.Instance | BindingFlags.NonPublic);
        EnemyShooterMovement Spawn(bool ordinary)
        {
            var actor = (GameObject)spawn.Invoke(spawner,
                new object[] { prefab, player.transform.position + Vector3.left * (12f + actors.Count * 2f), ordinary });
            Assert.That(actor, Is.Not.Null, "Production SpawnEnemyAt must create the shooter.");
            actors.Add(actor);
            return actor.GetComponent<EnemyShooterMovement>();
        }
        int NewProjectiles() => Object.FindObjectsByType<EnemyProjectile>(FindObjectsSortMode.None)
            .Count(projectile => !baseline.Contains(projectile.GetInstanceID()));
        try
        {
            EnemyDebugAiFreeze.SetFrozen(false);
            Time.timeScale = 1f;
            Assert.That(spawner.CommittedVolleyExperiment, Is.False, "Public default is OFF.");
            Assert.That(EnemySpawner.IsCommittedVolleyEligible(prefab), Is.True);
            Assert.That(Spawn(true).CommittedVolleyExperiment, Is.False);
            spawner.DebugSetCommittedVolleyExperiment(true);
            Assert.That(RunStateManager.Instance.IsDevelopmentRun, Is.True);
            Assert.That(Spawn(true).CommittedVolleyExperiment, Is.False);
            Assert.That(Spawn(false).CommittedVolleyExperiment, Is.False, "Special/untracked spawn is never automatically assigned.");
            Assert.That(Read<int>(spawner, "volleyEligibleSpawns"), Is.EqualTo(1));
            var manual = spawner.DebugSpawnCommittedVolleyAround(prefab, player.transform.position, out string status);
            Assert.That(manual, Is.Not.Null, status);
            actors.Add(manual);
            Assert.That(manual.GetComponent<EnemyShooterMovement>().CommittedVolleyExperiment, Is.True);
            Assert.That(Read<int>(spawner, "volleyEligibleSpawns"), Is.EqualTo(1), "Manual API does not consume ordinary cadence.");
            Assert.That(Spawn(true).CommittedVolleyExperiment, Is.False);
            var selected = Spawn(true);
            Assert.That(selected.CommittedVolleyExperiment, Is.True, "Exactly the third eligible ordinary spawn.");
            // Isolate the selected actor without allowing the other genuine spawns to shoot.
            foreach (var actor in actors) if (actor != selected.gameObject) actor.SetActive(false);
            yield return null;
            var visual = Read<Transform>(selected, "visualRoot");
            var rotation = visual.localRotation;
            var scale = visual.localScale;
            Set(selected, typeof(EnemyShooterMovement), "fireTimer", 0f);
            yield return CoreTestSupport.Await(() => Read<object>(selected, "volleyPhase").ToString() == "Preparing");
            float remaining = Read<float>(selected, "volleyTimer");
            EnemyDebugAiFreeze.SetFrozen(true);
            yield return WaitRealtime(.2f);
            Assert.That(Read<float>(selected, "volleyTimer"), Is.EqualTo(remaining).Within(.0001f));
            Assert.That(NewProjectiles(), Is.Zero, "AI freeze cannot release a pending volley.");
            EnemyDebugAiFreeze.SetFrozen(false);
            spawner.DebugSetCommittedVolleyExperiment(false);
            Assert.That(selected.CommittedVolleyExperiment, Is.False);
            Assert.That(Read<Vector2>(selected, "volleyDirection"), Is.EqualTo(Vector2.zero));
            Assert.That(Quaternion.Angle(visual.localRotation, rotation), Is.LessThan(.01f));
            Assert.That(visual.localScale, Is.EqualTo(scale));
            yield return WaitGameTime(.8f); // Beyond the cancelled preparation, below the normal fire interval.
            Assert.That(NewProjectiles(), Is.Zero, "OFF must cancel the deferred three-shot attack.");
            Assert.That(RunStateManager.Instance.IsDevelopmentRun, Is.True, "OFF never restores production credit.");
            selected.ConfigureCommittedVolleyExperiment(true);
            Set(selected, typeof(EnemyShooterMovement), "fireTimer", 0f);
            yield return CoreTestSupport.Await(() => Read<object>(selected, "volleyPhase").ToString() == "Preparing");
            selected.gameObject.SetActive(false);
            Assert.That(selected.CommittedVolleyExperiment, Is.False);
            Assert.That(Read<float>(selected, "volleyTimer"), Is.Zero);
            Assert.That(Read<Vector2>(selected, "volleyDirection"), Is.EqualTo(Vector2.zero));
            Assert.That(Quaternion.Angle(visual.localRotation, rotation), Is.LessThan(.01f));
            Assert.That(visual.localScale, Is.EqualTo(scale));
            selected.gameObject.SetActive(true);
            yield return WaitGameTime(.7f);
            Assert.That(selected.CommittedVolleyExperiment, Is.False, "Reuse does not retain experiment assignment.");
            Assert.That(NewProjectiles(), Is.Zero, "Reuse cannot release the previous pending volley.");
        }
        finally
        {
            spawner.DebugSetCommittedVolleyExperiment(false);
            foreach (var actor in actors) if (actor != null) Object.DestroyImmediate(actor);
            foreach (var projectile in Object.FindObjectsByType<EnemyProjectile>(FindObjectsSortMode.None))
                if (!baseline.Contains(projectile.GetInstanceID())) projectile.TryIntercept();
            EnemyDebugAiFreeze.SetFrozen(frozen);
            Time.timeScale = timeScale;
        }
    }

    [UnityTest]
    public IEnumerator FlankingChaserMovementHysteresisAssaultAndPoseRestore()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
        yield return new EnterPlayMode();
        yield return BeginProductionRun();
        var player = PrepareProduction();
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/_Project/prefabs/Enemies/p_Enemy_classic_start.prefab");
        var actor = Object.Instantiate(prefab);
        var chase = actor.GetComponent<EnemyChaseMovement>();
        var body = actor.GetComponent<Rigidbody2D>();
        Set(chase, typeof(EnemyChaseMovement), "player", player.transform);
        var visual = Read<Transform>(chase, "flankingVisual");
        Assert.That(visual, Is.Not.Null, "Production enemy needs a safe existing visual parent.");
        var authored = visual.localRotation;
        var rootRotation = body.rotation;
        // Validate the selected parent against actual production animation bindings.
        var animation = Read<Animator>(chase, "animator");
        foreach (var clip in animation.runtimeAnimatorController.animationClips)
            Assert.That(AnimationUtility.GetCurveBindings(clip).Any(binding =>
                binding.type == typeof(Transform) && binding.path == "" &&
                (binding.propertyName.Contains("Rotation") || binding.propertyName.Contains("Euler"))),
                Is.False, "The pose parent must not be rotation-animated: " + clip.name);
        var move = typeof(EnemyChaseMovement).GetMethod("MoveToPlayer", BindingFlags.Instance | BindingFlags.NonPublic);
        void Step(float distance)
        {
            body.position = (Vector2)player.transform.position + Vector2.left * distance;
            move.Invoke(chase, null);
        }
        // Each invocation advances the production filter by one fixed timestep.
        // Six response times leave less than 0.25% of the previous velocity.
        int responseSteps = Mathf.CeilToInt(6f * Mathf.Max(
            Read<float>(chase, "threatResponseSeconds"), Time.fixedDeltaTime) / Time.fixedDeltaTime);
        Assert.That(responseSteps, Is.InRange(1, 500), "Bounded production response window.");
        void Settle(float distance)
        {
            for (int i = 0; i < responseSteps; i++) Step(distance);
        }
        Step(10f);
        Assert.That(Quaternion.Angle(visual.localRotation, authored), Is.LessThan(.01f), "Default enemy pose.");
        chase.ConfigureFlankingExperiment(true, false);
        Settle(10f);
        Assert.That(Read<Vector2>(chase, "chaseVelocity").y, Is.GreaterThan(.1f), "Diagonal approach.");
        var diagonal = Read<Vector2>(chase, "chaseVelocity").normalized;
        Assert.That(diagonal.x, Is.GreaterThan(.4f), "Approach still closes on the player.");
        Assert.That(diagonal.y, Is.GreaterThan(.4f), "Approach has a substantial lateral direction.");
        var leaned = visual.localRotation;
        Assert.That(Quaternion.Angle(leaned, authored), Is.InRange(1f, 12.01f));
        Step(10f);
        Assert.That(Quaternion.Angle(visual.localRotation, authored), Is.InRange(1f, 12.01f), "No accumulated rotation as crowd steering updates.");
        Step(3.9f);
        Assert.That(Read<bool>(chase, "flankingApproach"), Is.False);
        Assert.That(Quaternion.Angle(visual.localRotation, authored), Is.LessThan(.01f));
        Step(5f);
        Assert.That(Read<bool>(chase, "flankingApproach"), Is.False, "4–6 m hysteresis.");
        Step(6.1f);
        Assert.That(Read<bool>(chase, "flankingApproach"), Is.True);
        chase.SetAssaultDestination((Vector2)player.transform.position + Vector2.left * 10f + Vector2.up * 8f);
        Assert.That(Quaternion.Angle(visual.localRotation, authored), Is.LessThan(.01f), "Assault resets pose immediately.");
        float previousLateralSpeed = Mathf.Abs(Read<Vector2>(chase, "chaseVelocity").x);
        Assert.That(previousLateralSpeed, Is.GreaterThan(.1f), "Assault starts with residual chase velocity.");
        Settle(10f);
        var assaultVelocity = Read<Vector2>(chase, "chaseVelocity");
        Assert.That(Mathf.Abs(assaultVelocity.x), Is.LessThan(previousLateralSpeed * .01f),
            "Assault suppresses the previous lateral chase over the response window.");
        Assert.That(Vector2.Dot(assaultVelocity.normalized, Vector2.up), Is.GreaterThan(.999f),
            "Assault overrides flanking and crowd steering, converging toward its destination.");
        chase.SetAssaultDestination(null);
        Step(10f);
        chase.ConfigureFlankingExperiment(false, false);
        Assert.That(Quaternion.Angle(visual.localRotation, authored), Is.LessThan(.01f));
        chase.ConfigureFlankingExperiment(true, false);
        Step(10f);
        chase.enabled = false;
        Assert.That(chase.IsFlankingExperiment, Is.False);
        Assert.That(Quaternion.Angle(visual.localRotation, authored), Is.LessThan(.01f));
        Assert.That(body.rotation, Is.EqualTo(rootRotation), "Physics root rotation is untouched.");
        Object.Destroy(actor);
    }

    [UnityTest]
    public IEnumerator FlankingChaserProductionSpawnerDefaultDevelopmentAndReset()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
        yield return new EnterPlayMode();
        yield return FlankingChaserSpawnerLifecycleBody();
    }

    private static IEnumerator FlankingChaserSpawnerLifecycleBody()
    {
        yield return BeginProductionRun();
        var spawner = Object.FindFirstObjectByType<EnemySpawner>();
        Assert.That(spawner, Is.Not.Null);
        var player = Object.FindFirstObjectByType<CharacterSpawner>().SpawnedPlayer;
        var playerBody = player.GetComponent<Rigidbody2D>();
        var constraints = playerBody.constraints;
        var playerVelocity = playerBody.linearVelocity;
        var health = player.GetComponent<PlayerHealth>();
        bool invulnerable = health.DebugInvulnerable;
        bool frozen = EnemyDebugAiFreeze.IsFrozen;
        float timeScale = Time.timeScale;
        var station = Object.FindFirstObjectByType<OrbitalStationRuntime>();
        bool stationEnabled = station.enabled;
        var created = new System.Collections.Generic.List<GameObject>();
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/_Project/prefabs/Enemies/p_Enemy_classic_start.prefab");
        Assert.That(prefab.GetComponent<EnemyHealth>().HasExperienceLoot, Is.True);
        var spawn = typeof(EnemySpawner).GetMethod("SpawnEnemyAt", BindingFlags.Instance | BindingFlags.NonPublic);
        void Own(GameObject actor)
        {
            created.Add(actor);
            actor.GetComponent<EnemyHealth>().SetRuntimeMaxHealth(10000f);
        }
        EnemyChaseMovement Spawn(bool tracked = true)
        {
            var actor = (GameObject)spawn.Invoke(spawner,
                new object[] { prefab, player.transform.position + Vector3.right * 20f, tracked });
            Assert.That(actor, Is.Not.Null);
            Own(actor);
            return actor.GetComponent<EnemyChaseMovement>();
        }
        try
        {
            PrepareProduction();
            station.enabled = false; // Keep weapon knockback outside this lifecycle contract.
            health.DebugInvulnerable = true;
            EnemyDebugAiFreeze.SetFrozen(false);
            Time.timeScale = 1f;
            spawner.ResetForNewLevel();
            spawner.StopSpawning(); // Reset re-enables spawning; keep unrelated actors outside the observation.
            Assert.That(spawner.FlankingChasersExperiment, Is.False);
            for (int i = 0; i < 4; i++) Assert.That(Spawn().IsFlankingExperiment, Is.False);
            spawner.DebugSetFlankingChasersExperiment(true);
            Assert.That(RunStateManager.Instance.IsDevelopmentRun, Is.True);
            for (int i = 0; i < 3; i++) Assert.That(Spawn().IsFlankingExperiment, Is.False);
            Assert.That(Spawn(false).IsFlankingExperiment, Is.False, "Debug/untracked spawn does not count.");
            int cadence = Read<int>(spawner, "flankingEligibleSpawns");
            int assigned = Read<int>(spawner, "flankingAssignedSpawns");
            var manualActor = spawner.DebugSpawnFlankingChaserAround(prefab, player.transform.position, out var status);
            Assert.That(manualActor, Is.Not.Null, status);
            Own(manualActor);
            var manual = manualActor.GetComponent<EnemyChaseMovement>();
            Assert.That(manual.IsFlankingExperiment, Is.True);
            Assert.That(Read<int>(spawner, "flankingEligibleSpawns"), Is.EqualTo(cadence), "Manual spawn preserves cadence.");
            Assert.That(Read<int>(spawner, "flankingAssignedSpawns"), Is.EqualTo(assigned), "Manual spawn preserves side cadence.");
            var flank = Spawn();
            Assert.That(flank.IsFlankingExperiment, Is.True, "Fourth tracked eligible production spawn.");
            var visual = Read<Transform>(flank, "flankingVisual");
            var manualVisual = Read<Transform>(manual, "flankingVisual");
            Assert.That(visual, Is.Not.Null, "Safe production visual parent.");
            Assert.That(manualVisual, Is.Not.Null);
            var authored = visual.localRotation;
            var manualAuthored = manualVisual.localRotation;
            var body = flank.GetComponent<Rigidbody2D>();
            var manualBody = manual.GetComponent<Rigidbody2D>();
            // Diagnostic placement only: leave authored speeds and natural FixedUpdate intact.
            body.position = (Vector2)player.transform.position + Vector2.left * 10f;
            manualBody.position = (Vector2)player.transform.position + Vector2.left * 10f + Vector2.up * 3f;
            Physics2D.SyncTransforms();
            var start = body.position;
            var manualStart = manualBody.position;
            yield return CoreTestSupport.Await(() => flank != null && manual != null &&
                Vector2.Distance(body.position, start) > .01f &&
                Vector2.Distance(manualBody.position, manualStart) > .01f &&
                Quaternion.Angle(visual.localRotation, authored) > 1f &&
                Quaternion.Angle(manualVisual.localRotation, manualAuthored) > 1f);
            Assert.That(Quaternion.Angle(visual.localRotation, authored), Is.GreaterThan(1f), "Natural ordinary flank visibly leans before OFF.");
            Assert.That(Quaternion.Angle(manualVisual.localRotation, manualAuthored), Is.GreaterThan(1f), "Natural manual flank visibly leans before OFF.");
            spawner.DebugSetFlankingChasersExperiment(false);
            Assert.That(flank.IsFlankingExperiment, Is.False, "OFF cancels a live ordinary assignment immediately.");
            Assert.That(manual.IsFlankingExperiment, Is.False, "OFF also cancels a live manual assignment.");
            Assert.That(Quaternion.Angle(visual.localRotation, authored), Is.LessThan(.01f));
            Assert.That(Quaternion.Angle(manualVisual.localRotation, manualAuthored), Is.LessThan(.01f));
            for (int i = 0; i < 4; i++) Assert.That(Spawn().IsFlankingExperiment, Is.False);
            Assert.That(RunStateManager.Instance.IsDevelopmentRun, Is.True, "Disabling cannot restore production progress.");
            spawner.DebugSetFlankingChasersExperiment(true);
            for (int i = 0; i < 3; i++) Assert.That(Spawn().IsFlankingExperiment, Is.False, "OFF clears cadence before re-enable.");
            var resetFlank = Spawn();
            Assert.That(resetFlank.IsFlankingExperiment, Is.True);
            spawner.ResetForNewLevel();
            Assert.That(spawner.FlankingChasersExperiment, Is.False);
            Assert.That(resetFlank.IsFlankingExperiment, Is.False, "Sector reset cancels live assignments.");
            spawner.DebugSetFlankingChasersExperiment(true);
            for (int i = 0; i < 3; i++) Assert.That(Spawn().IsFlankingExperiment, Is.False, "Sector reset clears cadence.");
            Assert.That(Spawn().IsFlankingExperiment, Is.True);
            spawner.ResetSpawner();
            Assert.That(spawner.FlankingChasersExperiment, Is.False);
            Assert.That(RunStateManager.Instance.IsDevelopmentRun, Is.True);
        }
        finally
        {
            if (spawner != null) spawner.DebugSetFlankingChasersExperiment(false);
            foreach (var actor in created) if (actor != null) Object.DestroyImmediate(actor);
            station.enabled = stationEnabled;
            health.DebugInvulnerable = invulnerable;
            playerBody.constraints = constraints;
            playerBody.linearVelocity = playerVelocity;
            EnemyDebugAiFreeze.SetFrozen(frozen);
            Time.timeScale = timeScale;
        }
    }

    [UnityTest]
    public IEnumerator FlankingChaserSpawnerDisableCancelsLiveAssignments()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
        yield return new EnterPlayMode();
        yield return FlankingChaserSpawnerDisableBody();
    }

    private static IEnumerator FlankingChaserSpawnerDisableBody()
    {
        yield return BeginProductionRun();
        var spawner = Object.FindFirstObjectByType<EnemySpawner>();
        Assert.That(spawner, Is.Not.Null);
        bool spawnerEnabled = spawner.enabled;
        var player = Object.FindFirstObjectByType<CharacterSpawner>().SpawnedPlayer;
        var playerBody = player.GetComponent<Rigidbody2D>();
        var constraints = playerBody.constraints;
        var playerVelocity = playerBody.linearVelocity;
        var health = player.GetComponent<PlayerHealth>();
        bool invulnerable = health.DebugInvulnerable;
        bool frozen = EnemyDebugAiFreeze.IsFrozen;
        float timeScale = Time.timeScale;
        var station = Object.FindFirstObjectByType<OrbitalStationRuntime>();
        bool stationEnabled = station.enabled;
        var created = new System.Collections.Generic.List<GameObject>();
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/_Project/prefabs/Enemies/p_Enemy_classic_start.prefab");
        Assert.That(prefab.GetComponent<EnemyHealth>().HasExperienceLoot, Is.True);
        var spawn = typeof(EnemySpawner).GetMethod("SpawnEnemyAt", BindingFlags.Instance | BindingFlags.NonPublic);
        void Own(GameObject actor)
        {
            created.Add(actor);
            actor.GetComponent<EnemyHealth>().SetRuntimeMaxHealth(10000f);
        }
        EnemyChaseMovement Spawn(bool tracked = true)
        {
            var actor = (GameObject)spawn.Invoke(spawner,
                new object[] { prefab, player.transform.position + Vector3.right * 20f, tracked });
            Assert.That(actor, Is.Not.Null);
            Own(actor);
            return actor.GetComponent<EnemyChaseMovement>();
        }
        try
        {
            PrepareProduction();
            station.enabled = false; // Keep weapon knockback outside this lifecycle contract.
            health.DebugInvulnerable = true;
            EnemyDebugAiFreeze.SetFrozen(false);
            Time.timeScale = 1f;
            spawner.ResetForNewLevel();
            spawner.StopSpawning(); // Reset re-enables spawning; keep unrelated actors outside the observation.
            Assert.That(spawner.FlankingChasersExperiment, Is.False);
            for (int i = 0; i < 4; i++) Assert.That(Spawn().IsFlankingExperiment, Is.False);
            spawner.DebugSetFlankingChasersExperiment(true);
            Assert.That(RunStateManager.Instance.IsDevelopmentRun, Is.True);
            for (int i = 0; i < 3; i++) Assert.That(Spawn().IsFlankingExperiment, Is.False);
            Assert.That(Spawn(false).IsFlankingExperiment, Is.False, "Debug/untracked spawn does not count.");
            int cadence = Read<int>(spawner, "flankingEligibleSpawns");
            int assigned = Read<int>(spawner, "flankingAssignedSpawns");
            var manualActor = spawner.DebugSpawnFlankingChaserAround(prefab, player.transform.position, out var status);
            Assert.That(manualActor, Is.Not.Null, status);
            Own(manualActor);
            var manual = manualActor.GetComponent<EnemyChaseMovement>();
            Assert.That(manual.IsFlankingExperiment, Is.True);
            Assert.That(Read<int>(spawner, "flankingEligibleSpawns"), Is.EqualTo(cadence), "Manual spawn preserves cadence.");
            Assert.That(Read<int>(spawner, "flankingAssignedSpawns"), Is.EqualTo(assigned), "Manual spawn preserves side cadence.");
            var flank = Spawn();
            Assert.That(flank.IsFlankingExperiment, Is.True, "Fourth tracked eligible production spawn.");
            var visual = Read<Transform>(flank, "flankingVisual");
            var manualVisual = Read<Transform>(manual, "flankingVisual");
            Assert.That(visual, Is.Not.Null, "Safe production visual parent.");
            Assert.That(manualVisual, Is.Not.Null);
            var authored = visual.localRotation;
            var manualAuthored = manualVisual.localRotation;
            var body = flank.GetComponent<Rigidbody2D>();
            var manualBody = manual.GetComponent<Rigidbody2D>();
            // Diagnostic placement only: leave authored speeds and natural FixedUpdate intact.
            body.position = (Vector2)player.transform.position + Vector2.left * 10f;
            manualBody.position = (Vector2)player.transform.position + Vector2.left * 10f + Vector2.up * 3f;
            Physics2D.SyncTransforms();
            var start = body.position;
            var manualStart = manualBody.position;
            yield return CoreTestSupport.Await(() => flank != null && manual != null &&
                Vector2.Distance(body.position, start) > .01f &&
                Vector2.Distance(manualBody.position, manualStart) > .01f &&
                Quaternion.Angle(visual.localRotation, authored) > 1f &&
                Quaternion.Angle(manualVisual.localRotation, manualAuthored) > 1f);
            Assert.That(Quaternion.Angle(visual.localRotation, authored), Is.GreaterThan(1f), "Natural ordinary flank visibly leans before OFF.");
            Assert.That(Quaternion.Angle(manualVisual.localRotation, manualAuthored), Is.GreaterThan(1f), "Natural manual flank visibly leans before OFF.");
            spawner.enabled = false; // Only the owner is disabled; enemies remain active.
            Assert.That(spawner.FlankingChasersExperiment, Is.False);
            Assert.That(flank.isActiveAndEnabled, Is.True);
            Assert.That(manual.isActiveAndEnabled, Is.True);
            Assert.That(Read<int>(spawner, "flankingEligibleSpawns"), Is.Zero);
            Assert.That(Read<int>(spawner, "flankingAssignedSpawns"), Is.Zero);
            Assert.That(Read<System.Collections.Generic.List<EnemyChaseMovement>>(spawner, "flankingChasers"), Is.Empty);
            Assert.That(flank.IsFlankingExperiment, Is.False, "OFF cancels a live ordinary assignment immediately.");
            Assert.That(manual.IsFlankingExperiment, Is.False, "OFF also cancels a live manual assignment.");
            Assert.That(Quaternion.Angle(visual.localRotation, authored), Is.LessThan(.01f));
            Assert.That(Quaternion.Angle(manualVisual.localRotation, manualAuthored), Is.LessThan(.01f));
            spawner.enabled = true;
            spawner.StopSpawning();
            Assert.That(spawner.FlankingChasersExperiment, Is.False, "Re-enable must not restore opt-in.");
            yield return null;
            Assert.That(spawner.FlankingChasersExperiment, Is.False);
            Assert.That(flank.IsFlankingExperiment, Is.False);
            Assert.That(manual.IsFlankingExperiment, Is.False);
            Assert.That(Read<int>(spawner, "flankingEligibleSpawns"), Is.Zero);
            Assert.That(Read<int>(spawner, "flankingAssignedSpawns"), Is.Zero);
            Assert.That(RunStateManager.Instance.IsDevelopmentRun, Is.True, "Re-enable cannot restore production progress.");
        }
        finally
        {
            if (spawner != null)
            {
                spawner.DebugSetFlankingChasersExperiment(false);
                spawner.enabled = spawnerEnabled;
            }
            foreach (var actor in created) if (actor != null) Object.DestroyImmediate(actor);
            station.enabled = stationEnabled;
            health.DebugInvulnerable = invulnerable;
            playerBody.constraints = constraints;
            playerBody.linearVelocity = playerVelocity;
            EnemyDebugAiFreeze.SetFrozen(frozen);
            Time.timeScale = timeScale;
        }
    }

    [UnityTest, Timeout(120000)]
    public IEnumerator FlankingChaserProductionMvpGameViewCapture()
    {
        typeof(BunkerStationPresentationTests).GetMethod("SetResolution", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, null);
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
        yield return new EnterPlayMode();
        yield return BeginProductionRun();
        var player = PrepareProduction();
        var spawner = Object.FindFirstObjectByType<EnemySpawner>();
        var camera = Camera.main;
        var health = player.GetComponent<PlayerHealth>();
        bool wasInvulnerable = health.DebugInvulnerable;
        bool wasFrozen = EnemyDebugAiFreeze.IsFrozen;
        float previousTimeScale = Time.timeScale;
        try
        {
            Time.timeScale = 1f;
            EnemyDebugAiFreeze.SetFrozen(false);
            health.DebugInvulnerable = true;
            spawner.ResetForNewLevel();
            spawner.StopSpawning();
            spawner.DebugSetFlankingChasersExperiment(true);
            Assert.That(RunStateManager.Instance.IsDevelopmentRun, Is.True);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/_Project/prefabs/Enemies/p_Enemy_classic_start.prefab");
            var spawn = typeof(EnemySpawner).GetMethod("SpawnEnemyAt", BindingFlags.Instance | BindingFlags.NonPublic);
            GameObject Spawn(Vector3 offset) => (GameObject)spawn.Invoke(spawner,
                new object[] { prefab, player.transform.position + offset, true });
            // These are tracked production spawns, including the cadence leading
            // to the fourth enemy. Keep the other three away from this approach.
            for (int i = 0; i < 3; i++)
                Assert.That(Spawn(new Vector3(20f, i * 2f)).GetComponent<EnemyChaseMovement>().IsFlankingExperiment, Is.False);
            var actor = Spawn(Vector3.left * 8f);
            var chase = actor.GetComponent<EnemyChaseMovement>();
            var body = actor.GetComponent<Rigidbody2D>();
            Assert.That(chase.IsFlankingExperiment, Is.True);
            Assert.That(chase.enabled && body.simulated, Is.True);
            yield return null; // Production Start resolves the player and animator.
            actor.GetComponent<EnemyHealth>().SetRuntimeMaxHealth(1000f);
            var start = body.position;
            yield return WaitGameTime(.25f);
            Assert.That(Vector2.Distance(body.position, player.transform.position), Is.GreaterThan(6f));
            Assert.That(Mathf.Abs(body.position.y - start.y), Is.GreaterThan(.05f), "Actual physics must show a diagonal approach.");
            Assert.That(Read<bool>(chase, "flankingApproach"), Is.True);
            var approachVisual = Read<Transform>(chase, "flankingVisual");
            Assert.That(approachVisual, Is.Not.Null);
            Assert.That(Quaternion.Angle(approachVisual.localRotation, Read<Quaternion>(chase, "flankingAuthoredRotation")),
                Is.InRange(1f, 12.01f), "Live approach must use the production flanking pose.");
            Vector3 viewport = camera.WorldToViewportPoint(actor.transform.position);
            Assert.That(viewport.z, Is.GreaterThan(0f));
            Assert.That(viewport.x, Is.InRange(0f, 1f));
            Assert.That(viewport.y, Is.InRange(0f, 1f));
            yield return CaptureFlankingGameView("flanking-approach");

            float deadline = Time.realtimeSinceStartup + 12f;
            while (actor != null && Vector2.Distance(body.position, player.transform.position) > 3.8f &&
                   Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(actor, Is.Not.Null, "Production enemy must survive until the close approach.");
            Assert.That(Vector2.Distance(body.position, player.transform.position), Is.LessThan(4f), "Live AI must reach the direct-chase zone.");
            // Allow the next production FixedUpdate to select direct pursuit.
            yield return WaitGameTime(.06f);
            Assert.That(Read<bool>(chase, "flankingApproach"), Is.False);
            var visual = Read<Transform>(chase, "flankingVisual");
            Assert.That(visual, Is.Not.Null);
            Assert.That(Quaternion.Angle(visual.localRotation, Read<Quaternion>(chase, "flankingAuthoredRotation")), Is.LessThan(.01f));
            yield return CaptureFlankingGameView("flanking-close");
        }
        finally
        {
            spawner.DebugSetFlankingChasersExperiment(false);
            health.DebugInvulnerable = wasInvulnerable;
            EnemyDebugAiFreeze.SetFrozen(wasFrozen);
            Time.timeScale = previousTimeScale;
        }
    }

    [UnityTest, Timeout(120000)]
    public IEnumerator FlankingChaserF1ImmediateSpawnPreservesCadenceAndClosesMenu()
    {
        typeof(BunkerStationPresentationTests).GetMethod("SetResolution", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, null);
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
        yield return new EnterPlayMode();
        yield return BeginProductionRun();
        var player = PrepareProduction();
        var spawner = Object.FindFirstObjectByType<EnemySpawner>();
        var menu = Object.FindFirstObjectByType<Subject42DebugMenu>();
        Assert.That(menu, Is.Not.Null, "Use the existing production F1 instance.");
        var health = player.GetComponent<PlayerHealth>();
        bool wasInvulnerable = health.DebugInvulnerable;
        bool wasFrozen = EnemyDebugAiFreeze.IsFrozen;
        float previousTimeScale = Time.timeScale;
        object Call(string method, params object[] args) => typeof(Subject42DebugMenu)
            .GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(menu, args);
        var tabType = typeof(Subject42DebugMenu).GetNestedType("DebugTab", BindingFlags.NonPublic);
        void OpenEnemies()
        {
            Call("OpenMenu");
            Call("SelectTab", Enum.ToObject(tabType, 2), true);
        }
        UnityEngine.UI.Button Button(string label, string row = null) => Read<GameObject>(menu, "menuRoot")
            .GetComponentsInChildren<UnityEngine.UI.Button>(true).Single(b => b.gameObject.activeInHierarchy &&
                b.GetComponentInChildren<TMPro.TMP_Text>().text == label && (row == null || b.transform.parent.name == row));
        try
        {
            Time.timeScale = 1f;
            health.DebugInvulnerable = true;
            EnemyDebugAiFreeze.SetFrozen(false);
            spawner.ResetForNewLevel();
            spawner.StopSpawning();
            OpenEnemies();
            Assert.That(Read<bool>(menu, "isOpen"), Is.True);
            Assert.That(Button("+1 ФЛАНГОВЫЙ").interactable, Is.False, "Manual option requires opt-in.");
            var prefab = RunStateManager.Instance.CurrentSector.SpawnProfile.Phases
                .Where(p => p != null && p.enemies != null).SelectMany(p => p.enemies)
                .Where(e => e?.enemyPrefab != null).Select(e => e.enemyPrefab)
                .FirstOrDefault(EnemySpawner.IsFlankingChaserEligible);
            Assert.That(prefab, Is.Not.Null, "The actual sector must offer an eligible enemy.");
            Assert.That(spawner.DebugSpawnFlankingChaserAround(prefab, player.transform.position, out _), Is.Null,
                "The API also rejects a disabled experiment.");
            var tracked = Read<System.Collections.Generic.List<GameObject>>(menu, "debugEnemies");
            Assert.That(tracked, Is.Empty);
            Button("ПЕРЕКЛЮЧИТЬ", "Эксперимент: фланговый преследователь").onClick.Invoke();
            Assert.That(spawner.FlankingChasersExperiment, Is.True);
            Assert.That(RunStateManager.Instance.IsDevelopmentRun, Is.True);
            var spawn = typeof(EnemySpawner).GetMethod("SpawnEnemyAt", BindingFlags.Instance | BindingFlags.NonPublic);
            GameObject SpawnTracked() => (GameObject)spawn.Invoke(spawner,
                new object[] { prefab, player.transform.position + Vector3.right * 25f, true });
            for (int i = 0; i < 3; i++)
                Assert.That(SpawnTracked().GetComponent<EnemyChaseMovement>().IsFlankingExperiment, Is.False);
            Assert.That(Read<int>(spawner, "flankingEligibleSpawns"), Is.EqualTo(3));
            var button = Button("+1 ФЛАНГОВЫЙ");
            Assert.That(button.interactable, Is.True);
            button.onClick.Invoke(); // Actual listener built by BuildMvpEnemies, not a direct spawn substitute.
            Assert.That(tracked.Count, Is.EqualTo(1), Read<string>(menu, "enemyDebugStatus"));
            var actor = tracked.Single();
            Assert.That(EnemySpawner.IsFlankingChaserEligible(actor), Is.True);
            var chase = actor.GetComponent<EnemyChaseMovement>();
            var body = actor.GetComponent<Rigidbody2D>();
            Assert.That(chase.IsFlankingExperiment, Is.True);
            Assert.That(Read<bool>(menu, "isOpen"), Is.False, "Successful callback closes F1.");
            Assert.That(Subject42DebugMenu.IsDebugMenuOpen, Is.False);
            Assert.That(Time.timeScale, Is.GreaterThan(0f));
            Assert.That(Read<int>(spawner, "flankingEligibleSpawns"), Is.EqualTo(3));
            Assert.That(Read<int>(spawner, "flankingAssignedSpawns"), Is.Zero);
            Assert.That(SpawnTracked().GetComponent<EnemyChaseMovement>().IsFlankingExperiment, Is.True,
                "Manual spawn must leave the fourth automatic assignment due.");
            actor.GetComponent<EnemyHealth>().SetRuntimeMaxHealth(1000f);
            yield return null;
            Assert.That(chase.enabled && body.simulated && !EnemyDebugAiFreeze.IsFrozen, Is.True);
            var start = body.position;
            yield return WaitGameTime(.25f);
            Assert.That(Vector2.Distance(start, body.position), Is.GreaterThan(.01f), "Live AI and physics advance after F1 closes.");
            yield return CoreTestSupport.Await(() => actor != null &&
                Camera.main.WorldToViewportPoint(actor.transform.position) is var v &&
                v.z > 0f && v.x > .02f && v.x < .98f && v.y > .02f && v.y < .98f);
            yield return CaptureFlankingGameView("flanking-f1-immediate");
            OpenEnemies();
            var clear = Button("УБРАТЬ");
            Assert.That(clear.interactable, Is.True);
            clear.onClick.Invoke();
            Assert.That(tracked, Is.Empty, "Existing debugEnemies cleanup owns the manual instance.");
            Call("CloseMenu");
            yield return null;
            Assert.That(actor == null, Is.True, "Manual instance is destroyed by the existing cleanup.");
        }
        finally
        {
            Call("CloseMenu");
            Call("ClearDebugEnemies");
            spawner.DebugSetFlankingChasersExperiment(false);
            health.DebugInvulnerable = wasInvulnerable;
            EnemyDebugAiFreeze.SetFrozen(wasFrozen);
            Time.timeScale = previousTimeScale;
        }
    }

    [UnityTest, Timeout(120000)]
    public IEnumerator EscortCargoProductionMvpEscortPauseAndRewardCapture()
    {
        typeof(BunkerStationPresentationTests).GetMethod("SetResolution", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, null);
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
        yield return new EnterPlayMode();
        yield return EscortCargoProductionMvpEscortPauseAndRewardCaptureBody();
    }

    // Create the iterator and its event-handler closure only after the domain reload.
    private static IEnumerator EscortCargoProductionMvpEscortPauseAndRewardCaptureBody()
    {
        yield return BeginProductionRun();
        yield return AwaitEscortProductionReady();
        var player = PlayerRuntimeReference.CachedPlayer;
        var health = player.GetComponent<PlayerHealth>();
        var body = player.GetComponent<Rigidbody2D>();
        var station = player.GetComponentInChildren<OrbitalStationRuntime>();
        var events = Object.FindFirstObjectByType<WorldEventSpawner>();
        var menu = Object.FindFirstObjectByType<Subject42DebugMenu>();
        bool protection = health.DebugInvulnerable;
        bool frozen = EnemyDebugAiFreeze.IsFrozen;
        float scale = Time.timeScale;
        int completed = 0, failed = 0, credited = 0;
        WorldEvent cargoEvent = null;
        Vector3 destination = default;
        var rewards = Read<System.Collections.Generic.List<WorldBreakable>>(events, "ownedRewardContainers");
        WorldBreakable[] previousRewards = rewards.ToArray();
        WorldBreakable[] awarded = null;
        void OnComplete(WorldEvent e)
        {
            if (e != cargoEvent) return;
            completed++;
            awarded = rewards.Except(previousRewards).ToArray();
        }
        void OnFail(WorldEvent e) { if (e == cargoEvent) failed++; }
        void OnCredit(RunConfig config, int id, WorldEvent source, string tag) { credited++; }
        events.EventCompleted += OnComplete;
        events.EventFailed += OnFail;
        RunStateManager.ContentEventCompleted += OnCredit;
        try
        {
            health.DebugInvulnerable = true;
            EnemyDebugAiFreeze.SetFrozen(false);
            Time.timeScale = 1f;
            OpenEscortF1(menu);
            EscortF1Button(menu, "ПЕРЕКЛЮЧИТЬ", "Сопровождение груза").onClick.Invoke();
            Assert.That(events.EscortCargoExperimentEnabled, Is.True, Read<string>(menu, "enemyDebugStatus"));
            EscortF1Button(menu, "СОЗДАТЬ ГРУЗ").onClick.Invoke();
            var escort = events.EscortCargoExperimentInstance;
            Assert.That(escort, Is.Not.Null, Read<string>(menu, "enemyDebugStatus"));
            cargoEvent = escort;
            destination = escort.RewardPosition;
            Assert.That(Read<bool>(menu, "isOpen"), Is.False, "Successful actual F1 callback closes the menu.");
            Assert.That(escort.IsStarted, Is.False, "Creation must not accept the event.");
            var cargo = Read<Transform>(escort, "cargo");
            MoveEscortPlayer(body, cargo.position);
            yield return new WaitForFixedUpdate();
            Assert.That(escort.CanInteract, Is.True, "Actual player must be in the authored acceptance trigger.");
            escort.Interact();
            Assert.That(escort.IsStarted && escort.IsEscorted, Is.True);
            Vector3 start = cargo.position;
            yield return WaitGameTime(.4f);
            Assert.That(Vector3.Distance(start, cargo.position), Is.GreaterThan(.1f));
            Assert.That(station.isActiveAndEnabled && body.simulated && !EnemyDebugAiFreeze.IsFrozen, Is.True);
            yield return CaptureFlankingGameView("escort-moving", "EscortCargo");

            Vector3 retreat = cargo.position + (cargo.position - destination).normalized *
                (Read<float>(escort, "escortRadius") + 1.5f);
            MoveEscortPlayer(body, retreat);
            yield return new WaitForFixedUpdate();
            Assert.That(escort.IsEscorted, Is.False);
            Vector3 paused = cargo.position;
            float remaining = escort.RemainingSeconds;
            yield return WaitGameTime(.4f);
            Assert.That(Vector3.Distance(paused, cargo.position), Is.LessThan(.001f), "Cargo waits when the player retreats.");
            Assert.That(escort.RemainingSeconds, Is.LessThan(remaining - .2f), "The deadline continues while cargo waits.");
            yield return CaptureFlankingGameView("escort-paused", "EscortCargo");

            float deadline = Time.realtimeSinceStartup + Read<float>(escort, "timeLimit") + 3f;
            while (escort != null && !escort.IsCompleted && Time.realtimeSinceStartup < deadline)
            {
                MoveEscortPlayer(body, cargo.position);
                yield return new WaitForFixedUpdate();
            }
            Assert.That(completed, Is.EqualTo(1), "Authored route must complete through live Update, not manual ticking.");
            Assert.That(failed, Is.Zero);
            Assert.That(awarded, Is.Not.Null);
            Assert.That(awarded.Length, Is.EqualTo(1), "One standard owned reward container.");
            Assert.That(awarded[0], Is.Not.Null);
            Assert.That(Vector3.Distance(awarded[0].transform.position, destination), Is.LessThan(.01f));
            Assert.That(events.EscortCargoExperimentInstance, Is.Null);
            Assert.That(events.SpawnedEvents.Contains(cargoEvent), Is.False);
            Assert.That(RunStateManager.Instance.IsDevelopmentRun, Is.True);
            Assert.That(credited, Is.Zero, "Development completion cannot report production credit.");
            events.NotifyEventCompleted(cargoEvent);
            Assert.That(completed, Is.EqualTo(1), "Duplicate completion cannot dispatch again.");
            Assert.That(rewards.Except(previousRewards).Count(), Is.EqualTo(1));
        }
        finally
        {
            events.EventCompleted -= OnComplete;
            events.EventFailed -= OnFail;
            RunStateManager.ContentEventCompleted -= OnCredit;
            EscortF1Call(menu, "CloseMenu");
            events.DebugClearEscortCargo(out _);
            events.DebugSetEscortCargoExperiment(false, out _);
            if (health != null) health.DebugInvulnerable = protection;
            EnemyDebugAiFreeze.SetFrozen(frozen);
            Time.timeScale = scale;
        }
    }

    [UnityTest, Timeout(120000)]
    public IEnumerator EscortCargoOptInAndOwnerResetPreservePublicPool()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
        yield return new EnterPlayMode();
        yield return EscortCargoOptInAndOwnerResetPreservePublicPoolBody();
    }

    private static IEnumerator EscortCargoOptInAndOwnerResetPreservePublicPoolBody()
    {
        yield return BeginProductionRun();
        yield return AwaitEscortProductionReady();
        var events = Object.FindFirstObjectByType<WorldEventSpawner>();
        var player = PlayerRuntimeReference.CachedPlayer;
        var health = player.GetComponent<PlayerHealth>();
        var body = player.GetComponent<Rigidbody2D>();
        var pool = events.EventPrefabs.ToArray();
        var rewards = Read<System.Collections.Generic.List<WorldBreakable>>(events, "ownedRewardContainers");
        int rewardCount = rewards.Count;
        int outcomes = 0;
        WorldEvent owned = null;
        void OnOutcome(WorldEvent e) { if (e == owned) outcomes++; }
        events.EventCompleted += OnOutcome;
        events.EventFailed += OnOutcome;
        bool protection = health.DebugInvulnerable;
        try
        {
            health.DebugInvulnerable = true;
            Assert.That(events.EscortCargoExperimentEnabled, Is.False);
            Assert.That(Read<bool>(events, "debugManualOnly"), Is.False);
            Assert.That(events.DebugSpawnEscortCargo(out var status), Is.False, status);
            Assert.That(events.EscortCargoExperimentInstance, Is.Null);
            Assert.That(events.DebugSetEscortCargoExperiment(true, out status), Is.True, status);
            Assert.That(RunStateManager.Instance.IsDevelopmentRun, Is.True);
            Assert.That(events.DebugSpawnEscortCargo(out status), Is.True, status);
            var escort = events.EscortCargoExperimentInstance;
            owned = escort;
            Assert.That(escort, Is.Not.Null);
            Assert.That(escort.SourcePrefab.AvailableInProduction, Is.False);
            Assert.That(pool.Contains(escort.SourcePrefab), Is.False);
            MoveEscortPlayer(body, Read<Transform>(escort, "cargo").position);
            yield return new WaitForFixedUpdate();
            Assert.That(escort.CanInteract, Is.True);
            escort.Interact();
            Assert.That(events.ActiveEvent, Is.SameAs(escort));
            Assert.That(events.DebugSetEscortCargoExperiment(false, out status), Is.True, status);
            Assert.That(events.EscortCargoExperimentInstance, Is.SameAs(escort), "Opt-out alone preserves the owned event.");
            Assert.That(events.DebugClearEscortCargo(out status), Is.True, status);
            Assert.That(events.EscortCargoExperimentInstance, Is.Null);
            Assert.That(events.ActiveEvent, Is.Null);
            Assert.That(events.SpawnedEvents.Contains(owned), Is.False);
            Assert.That(Read<System.Collections.Generic.HashSet<WorldEvent>>(events, "debugContentEvents").Contains(owned), Is.False);
            Assert.That(((System.Collections.IDictionary)Read<object>(events, "eventRootOwners")).Contains(owned), Is.False);
            yield return null;
            Assert.That(escort == null, Is.True, "Owner-reset releases the actual event root.");
            Assert.That(outcomes, Is.Zero, "Administrative cleanup emits neither success nor failure.");
            Assert.That(rewards.Count, Is.EqualTo(rewardCount));
            CollectionAssert.AreEqual(pool, events.EventPrefabs);
            Assert.That(Read<bool>(events, "debugManualOnly"), Is.False);
            Assert.That(RunStateManager.Instance.IsDevelopmentRun, Is.True);
        }
        finally
        {
            events.EventCompleted -= OnOutcome;
            events.EventFailed -= OnOutcome;
            events.DebugClearEscortCargo(out _);
            events.DebugSetEscortCargoExperiment(false, out _);
            if (health != null) health.DebugInvulnerable = protection;
        }
    }

    private static IEnumerator AwaitEscortProductionReady()
    {
        float deadline = Time.realtimeSinceStartup + 10f;
        while ((RunStateManager.Instance == null || UpgradeManager.Instance == null ||
                PlayerRuntimeReference.CachedPlayer == null ||
                Object.FindFirstObjectByType<OrbitalStationRuntime>() is not { IsInitialized: true } ||
                Object.FindFirstObjectByType<WorldEventSpawner>() == null) &&
               Time.realtimeSinceStartup < deadline) yield return null;
        Assert.That(RunStateManager.Instance, Is.Not.Null, "Production RunStateManager readiness timed out.");
        Assert.That(UpgradeManager.Instance, Is.Not.Null, "Production UpgradeManager readiness timed out.");
        var player = PlayerRuntimeReference.CachedPlayer;
        Assert.That(player, Is.Not.Null, "Production player readiness timed out.");
        Assert.That(player.GetComponent<PlayerHealth>(), Is.Not.Null);
        Assert.That(player.GetComponent<Rigidbody2D>(), Is.Not.Null);
        var station = player.GetComponentInChildren<OrbitalStationRuntime>();
        Assert.That(station, Is.Not.Null, "Production station missing.");
        Assert.That(station.IsInitialized && station.isActiveAndEnabled, Is.True);
        Assert.That(station.InputOwner, Is.Not.Null);
        Assert.That(Object.FindFirstObjectByType<WorldEventSpawner>(), Is.Not.Null, "Production event owner missing.");
        Assert.That(Object.FindFirstObjectByType<Subject42DebugMenu>(), Is.Not.Null, "Production F1 missing.");
        Assert.That(Camera.main, Is.Not.Null);
    }

    private static void MoveEscortPlayer(Rigidbody2D body, Vector3 position)
    {
        body.position = position;
        body.linearVelocity = Vector2.zero;
        Physics2D.SyncTransforms();
    }

    private static object EscortF1Call(Subject42DebugMenu menu, string method, params object[] args) =>
        typeof(Subject42DebugMenu).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(menu, args);

    private static void OpenEscortF1(Subject42DebugMenu menu)
    {
        EscortF1Call(menu, "OpenMenu");
        var tab = typeof(Subject42DebugMenu).GetNestedType("DebugTab", BindingFlags.NonPublic);
        EscortF1Call(menu, "SelectTab", Enum.ToObject(tab, 2), true);
    }

    private static UnityEngine.UI.Button EscortF1Button(Subject42DebugMenu menu, string label, string row = null) =>
        Read<GameObject>(menu, "menuRoot").GetComponentsInChildren<UnityEngine.UI.Button>(true).Single(b =>
            b.gameObject.activeInHierarchy && b.GetComponentInChildren<TMPro.TMP_Text>().text == label &&
            (row == null || b.transform.parent.name == row));

    private static IEnumerator CaptureFlankingGameView(string phase, string content = "FlankingChaser")
    {
        string path = System.IO.Path.GetFullPath("Artifacts/GeneratedQA/NightShift/" + content + "/" + phase + ".png");
        System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path));
        var requested = DateTime.UtcNow;
        ScreenCapture.CaptureScreenshot(path);
        yield return CoreTestSupport.Await(() => System.IO.File.Exists(path) &&
            System.IO.File.GetLastWriteTimeUtc(path) >= requested && new System.IO.FileInfo(path).Length > 0);
        Debug.Log(content + " production Game View: " + path);
    }

    [UnityTest]
    public IEnumerator ProductionComputeBufferSurvivesScenesAndReleasesOnPlayModeExit()
    {
        typeof(BunkerStationPresentationTests).GetMethod("SetResolution", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, null);
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
        yield return new EnterPlayMode();
        yield return BeginProductionRun();
        yield return ComputeBufferLifecycleSmoke.VerifyCurrentRun("Artifacts/GeneratedQA/ComputeBuffer/PlayMode");
        yield return new ExitPlayMode();
        VerifyBufferExitAudit();
    }

    private static void VerifyBufferExitAudit()
    {
        string audit = System.IO.File.ReadAllText("Artifacts/GeneratedQA/ComputeBuffer/PlayMode/shutdown-audit.txt");
        Assert.That(audit, Does.Contain("released=True"), audit);
    }

    [UnityTest]
    public IEnumerator EnemyFlashPreservesRenderedSpriteMaterialAndRendererTint()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
        yield return new EnterPlayMode();
        yield return EnemyFlashTintSmoke.Verify("Artifacts/GeneratedQA/EnemyFeedback/ShaderPlayMode");
    }

    [UnityTest]
    public IEnumerator ProductionEnemyHitsDeathsAndReuseKeepCrowdReadable()
    {
        typeof(BunkerStationPresentationTests).GetMethod("SetResolution", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, null);
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
        yield return new EnterPlayMode();
        yield return BeginProductionRun();
        yield return EnemyFeedbackSmoke.VerifyCurrentRun("Artifacts/GeneratedQA/EnemyFeedback/PlayMode");
    }

    [UnityTest]
    public IEnumerator ProductionAnomalyRetryStopsAtApplicationShutdown()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
        yield return new EnterPlayMode();
        yield return BeginProductionRun();
        yield return AnomalyLifecycleSmoke.VerifyCurrentRun("Artifacts/GeneratedQA/EnemyFeedback/LifecyclePlayMode");
    }

    [UnityTest]
    public IEnumerator ProductionEnvironmentAndEventPresentationStayQuietBetweenBeats()
    {
        typeof(BunkerStationPresentationTests).GetMethod("SetResolution", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, null);
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
        yield return new EnterPlayMode();
        yield return BeginProductionRun();
        yield return EnvironmentPaceSmoke.VerifyCurrentRun("Artifacts/GeneratedQA/EnvironmentPace/PlayMode");
    }

    [UnityTest]
    public IEnumerator ProductionOrbitalHierarchyKeepsColoredCannonsAboveTerritories()
    {
        typeof(BunkerStationPresentationTests).GetMethod("SetResolution", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, null);
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
        yield return new EnterPlayMode();
        yield return BeginProductionRun();
        yield return OrbitalHierarchySmoke.VerifyCurrentRun("Artifacts/GeneratedQA/OrbitalHierarchy/PlayMode");
    }

    [UnityTest]
    public IEnumerator PlayerFlashIsWarmAndRestoresWhilePausedOrDisabled()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
        yield return new EnterPlayMode();
        yield return CheckFlash();
    }

    private static IEnumerator CheckFlash()
    {
        var actor = new GameObject("Flash lifecycle check");
        actor.SetActive(false);
        var sprite = actor.AddComponent<SpriteRenderer>();
        var original = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/prefabs/Bunker/mat/BunkerElements2D.mat");
        var authoredFlash = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Shaders/M_WhiteFlash.mat");
        sprite.sharedMaterial = original;
        var flash = actor.AddComponent<PlayerWhiteFlash>();
        Set(flash, typeof(PlayerWhiteFlash), "flashMaterial", authoredFlash);
        actor.SetActive(true);
        flash.Flash();
        var tint = sprite.sharedMaterial.GetColor("_FlashColor");
        Assert.That(tint.r, Is.GreaterThan(tint.g * 1.5f), "Player damage must be distinguishable from the enemy white flash.");
        Assert.That(authoredFlash.GetColor("_FlashColor"), Is.EqualTo(Color.white), "Never recolor the shared enemy flash material.");
        Time.timeScale = 0f;
        yield return WaitRealtime(.3f);
        Assert.That(sprite.sharedMaterial, Is.SameAs(original), "A paused UI must not leave a permanent flash.");
        Time.timeScale = 1f;
        flash.Flash();
        actor.SetActive(false);
        Assert.That(sprite.sharedMaterial, Is.SameAs(original), "Disable must restore the material before reuse.");
        actor.SetActive(true);
        flash.Flash();
        yield return WaitRealtime(.3f);
        Assert.That(sprite.sharedMaterial, Is.SameAs(original));
        Object.Destroy(actor);
    }

    [UnityTest]
    public IEnumerator ProductionGameViewShowsPlayerDamageAndThreeCannonColors()
    {
        typeof(BunkerStationPresentationTests).GetMethod("SetResolution", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, null);
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
        yield return new EnterPlayMode();
        yield return BeginProductionRun();
        yield return CombatReadabilitySmoke.VerifyCurrentRun("Artifacts/GeneratedQA/CombatReadability/PlayMode");
    }

    [UnityTest]
    public IEnumerator AcceptedPlayerDamagePulsesExistingHudWithoutChangingHealthRules()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
        yield return new EnterPlayMode();
        yield return BeginProductionRun();
        yield return CheckPlayerDamage();
    }

    private static IEnumerator CheckPlayerDamage()
    {
        var player = PrepareProduction();
        var health = player.GetComponent<PlayerHealth>();
        var hud = Object.FindFirstObjectByType<HUDManager>();
        var vignette = Read<CanvasGroup>(hud, "lowHpVignette");
        health.SetRuntimeHealth(100f, 100f);
        Assert.That(health.TakeDamage(15f, Vector2.right), Is.True);
        Assert.That(health.CurrentHealth, Is.EqualTo(85f));
        Assert.That(vignette.gameObject.activeInHierarchy, Is.True, "An alpha change on an inactive vignette cannot be seen.");
        Assert.That(vignette.alpha, Is.GreaterThan(.15f), "The first accepted hit must be visible even above the low-health threshold.");
        Assert.That(health.TakeDamage(15f, Vector2.right), Is.False, "Keep the existing invulnerability window.");
        Assert.That(health.CurrentHealth, Is.EqualTo(85f));
        yield return WaitRealtime(.4f);
        Assert.That(vignette.alpha, Is.LessThan(.01f), "The pulse must end instead of becoming a constant overlay.");
        health.Heal(5f);
        yield return null;
        Assert.That(vignette.alpha, Is.LessThan(.01f), "Healing must not be presented as damage.");
        yield return WaitGameTime(.7f);
        health.SetRuntimeHealth(100f, 20f);
        Assert.That(health.TakeDamage(5f, Vector2.left), Is.True);
        yield return WaitRealtime(.4f);
        Assert.That(health.CurrentHealth, Is.EqualTo(15f));
        float baseline = Mathf.Lerp(.15f, .55f, 1f - .15f / Read<float>(hud, "lowHpThreshold"));
        Assert.That(vignette.alpha, Is.EqualTo(baseline).Within(.02f), "Recover the authored low-health baseline after the hit pulse.");
        hud.HideLowHpVignette();
        yield return null;
        Assert.That(vignette.alpha, Is.Zero);
    }

    [UnityTest]
    public IEnumerator RoundCannonShotUsesExistingHaloWithoutAddingAContactHitbox()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
        yield return new EnterPlayMode();
        yield return BeginProductionRun();
        yield return CheckCannonShot();
    }

    private static IEnumerator CheckCannonShot()
    {
        var player = PrepareProduction();
        var station = player.GetComponentInChildren<OrbitalStationRuntime>();
        var module = station.Modules.OfType<OrbitalProjectileModule>().First();
        var enemy = Target(player.transform.position + Vector3.right * 5f);
        yield return null;
        var visual = (GameObject)typeof(OrbitalModuleRuntime).GetField("Visual", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(module);
        var view = visual.GetComponent<OrbitalModuleView>();
        module.SetHighlighted(false);
        Set(module, typeof(OrbitalModuleRuntime), "Cooldown", 0f);
        int shots = module.ShotsFired;
        module.ActivateCombat();
        module.Tick(0f);
        Assert.That(module.ShotsFired, Is.EqualTo(shots + 1));
        Assert.That(view.Halo.enabled, Is.True, "The existing non-colliding halo must emphasize the cannon firing.");
        Assert.That(view.Halo.color.a, Is.GreaterThan(.3f));
        Assert.That(CombatReadabilitySmoke.HaloDoesNotChangeContact(module, view.Halo), Is.True,
            "The shot glow must not enlarge the contact weapon.");
        yield return null;
        Assert.That(view.Halo.color.a, Is.GreaterThan(.3f), "The ordinary sprite tint pass must not erase the shot pulse.");
        module.SetHighlighted(true);
        yield return WaitGameTime(.3f);
        Assert.That(view.Halo.enabled, Is.True, "Shot recovery must keep an active selection halo.");
        module.SetHighlighted(false);
        Assert.That(view.Halo.enabled, Is.False);
        var presentation = typeof(OrbitalModuleRuntime).GetField("presentation", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(module);
        presentation.GetType().GetMethod("SetPreviewState").Invoke(presentation, new object[] { true });
        module.Tick(0f);
        Assert.That(view.Halo.enabled, Is.True, "A valid reward preview must retain its existing halo across ticks.");
        presentation.GetType().GetMethod("SetPreviewState").Invoke(presentation, new object[] { false });
        module.Tick(0f);
        Assert.That(view.Halo.enabled, Is.False, "An invalid reward preview must retain its existing hidden halo.");
        Object.Destroy(enemy.gameObject);
    }

    [UnityTest]
    public IEnumerator OrbitalProjectileImpactRemainsReadableAndDamageStaysEight()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
        yield return new EnterPlayMode();
        yield return BeginProductionRun();
        yield return CheckImpact();
        yield return CompareCrowdWeapons();
    }

    private static IEnumerator CompareCrowdWeapons()
    {
        const string output = "Artifacts/GeneratedQA/WeaponFeel";
        const string phase = "after";
        System.IO.Directory.CreateDirectory(output);
        var report = new System.Collections.Generic.List<string>();
        var player = Object.FindFirstObjectByType<CharacterSpawner>().SpawnedPlayer;
        player.GetComponent<PlayerHealth>().DebugInvulnerable = true;
        var station = player.GetComponentInChildren<OrbitalStationRuntime>();
        station.enabled = false;
        foreach (var module in station.Modules.ToArray()) station.RemoveModule(module.StableModuleId);
        var adapter = (ProductionOrbitalCombatAdapter)station.Combat;
        var ring = station.Rings[0];
        var mount = ring.Mounts[0];
        var enemies = new System.Collections.Generic.List<EnemyHealth>();
        float shotgunRetreat = 0f;
        var tickStation = typeof(OrbitalStationRuntime).GetMethod("TickStation", BindingFlags.Instance | BindingFlags.NonPublic);
        report.Add($"Project={System.IO.Path.GetFullPath(".")} Scene={UnityEngine.SceneManagement.SceneManager.GetActiveScene().path} Unity={Application.unityVersion} PID={System.Diagnostics.Process.GetCurrentProcess().Id}");
        report.Add("16 production chasers, active AI; one real mounted volley; player stationary/invulnerable. Physical input not covered.");
        foreach (var path in new[] { "scripts/Combat/Enemies/EnemyChaseMovement.cs", "scripts/Combat/OrbitalStation/OrbitalStationAdapters.cs", "scripts/Combat/OrbitalStation/OrbitalWeaponSettings.cs" })
        {
            using var sha = System.Security.Cryptography.SHA256.Create();
            report.Add(path + " SHA256=" + System.BitConverter.ToString(sha.ComputeHash(System.IO.File.ReadAllBytes("Assets/_Project/" + path))).Replace("-", ""));
        }
        foreach (var kind in new[] { OrbitalModuleKind.Pistol, OrbitalModuleKind.Shotgun, OrbitalModuleKind.Sniper })
        {
            adapter.DebugClearProjectiles();
            foreach (var enemy in EnemyHealth.ActiveInstances.ToArray()) Object.DestroyImmediate(enemy.gameObject);
            enemies.Clear();
            Assert.That(station.InstallModule(kind, ring.RingId, mount.MountIndex, out string error), Is.True, error);
            var cannon = station.Modules.OfType<OrbitalProjectileModule>().Single();
            mount.Transform.position = player.transform.position + Vector3.right * .5f;
            for (int i = 0; i < 16; i++)
            {
                var actor = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/prefabs/Enemies/p_Enemy_classic.prefab"),
                    player.transform.position + new Vector3(2.6f + i % 4 * .8f, (i / 4 - 1.5f) * .8f), Quaternion.identity);
                enemies.Add(actor.GetComponent<EnemyHealth>());
            }
            yield return null;
            foreach (var enemy in enemies) enemy.SetRuntimeMaxHealth(1000f);
            var target = adapter.FindNearest(cannon.WorldPosition, cannon.Settings.Range);
            Vector2 start = target.transform.position;
            Vector2 away = (start - (Vector2)player.transform.position).normalized;
            var targetBody = target.GetComponent<Rigidbody2D>();
            bool impacted = false;
            Vector2 impactPosition = start;
            float maxOutward = 0f;
            typeof(OrbitalModuleRuntime).GetField("Cooldown", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(cannon, 0f);
            cannon.ActivateCombat();
            ScreenCapture.CaptureScreenshot(output + "/crowd-" + phase + "-" + kind + "-start.png");
            float began = Time.time;
            bool captured = false;
            while (Time.time - began < .4f)
            {
                tickStation.Invoke(station, new object[] { Time.deltaTime });
                if (!impacted && target.CurrentHealth < 1000f)
                { impacted = true; impactPosition = targetBody.position; }
                if (impacted)
                    maxOutward = Mathf.Max(maxOutward, Vector2.Dot(targetBody.position - impactPosition, away));
                if (!captured && Time.time - began >= .25f)
                {
                    ScreenCapture.CaptureScreenshot(output + "/crowd-" + phase + "-" + kind + "-mid.png");
                    captured = true;
                }
                yield return null;
            }
            float damage = enemies.Sum(e => 1000f - e.CurrentHealth);
            int hitCount = enemies.Count(e => e.CurrentHealth < 1000f);
            Assert.That(cannon.ShotsFired, Is.EqualTo(1), "Compare one real volley per weapon.");
            Assert.That(hitCount, Is.GreaterThan(0));
            if (kind == OrbitalModuleKind.Shotgun) shotgunRetreat = maxOutward;
            report.Add($"{kind}: shots={cannon.ShotsFired} damaged={hitCount} damage={damage:F3} maxOutward={maxOutward:F4} finalOutward={Vector2.Dot((Vector2)target.transform.position - start, away):F4}");
            ScreenCapture.CaptureScreenshot(output + "/crowd-" + phase + "-" + kind + "-end.png");
            yield return null;
            typeof(OrbitalModuleRuntime).GetField("Cooldown", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(cannon, 10f);
            Vector2 recoveryStart = targetBody.position;
            float recoverAt = Time.time;
            while (Time.time - recoverAt < .2f)
            { tickStation.Invoke(station, new object[] { Time.deltaTime }); yield return null; }
            float recoveredApproach = Vector2.Dot(recoveryStart - targetBody.position, away);
            report.Add($"{kind}: approach after impulse={recoveredApproach:F4}");
            Assert.That(recoveredApproach, Is.GreaterThan(.15f), "Chase pressure must resume after the short impulse.");
            station.RemoveModule(cannon.StableModuleId);
        }
        foreach (var enemy in enemies) Object.Destroy(enemy.gameObject);
        System.IO.File.WriteAllLines(output + "/crowd-" + phase + ".txt", report);
        Assert.That(shotgunRetreat, Is.GreaterThan(.15f),
            "A close shotgun hit must visibly displace a chasing enemy, rather than only reduce its approach speed.");
    }

    private static IEnumerator CheckImpact()
    {
        var player = PrepareProduction();
        var station = player.GetComponentInChildren<OrbitalStationRuntime>();
        foreach (var module in station.Modules.ToArray()) station.RemoveModule(module.StableModuleId);
        var adapter = (ProductionOrbitalCombatAdapter)station.Combat;
        adapter.DebugClearProjectiles();
        var enemy = Target(player.transform.position + Vector3.right * 4f);
        yield return null;
        enemy.SetRuntimeMaxHealth(1000f);
        bool hit = false;
        Vector2 point = default;
        adapter.ProjectileImpact += (p, _, _) => { hit = true; point = p; };
        adapter.SpawnWeaponProjectile(player.transform.position, enemy, Vector2.right,
            OrbitalPresentationConfig.Active.GetWeaponSettings(OrbitalModuleKind.Pistol), null, 8f);
        yield return CoreTestSupport.Await(() => hit);
        Assert.That(enemy.CurrentHealth, Is.EqualTo(992f), "Presentation must not add damage or alter collision resolution.");
        yield return WaitGameTime(.09f);
        var lines = station.GetComponentsInChildren<LineRenderer>()
            .Where(l => l.enabled && l.positionCount == 2 && Vector2.Distance(l.GetPosition(0), point) < .8f).ToArray();
        Assert.That(lines.Length, Is.GreaterThanOrEqualTo(3), "The impact must remain readable after the first few frames.");
        yield return WaitGameTime(.3f);
        Assert.That(lines.All(l => !l.enabled), Is.True, "Impact lines must return to the existing pool.");
        Object.Destroy(enemy.gameObject);
    }

    private static IEnumerator BeginProductionRun()
    {
        yield return CoreTestSupport.LoadBunker();
        var character = AssetDatabase.LoadAssetAtPath<CharacterData>("Assets/_Project/Data/Characters/01_Gera.asset");
        RunSelectionManager.Instance.SelectCharacter(character);
        var starter = Object.FindFirstObjectByType<BunkerRunStarter>();
        if (!GameplayRandom.IsActive) GameplayRandom.Begin(1709);
        starter.StartRun(starter.transform);
        yield return CoreTestSupport.Await(() => UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == "MVP" &&
            ProductionSceneComposition.Active is { IsReady: true } &&
            PlayerRuntimeReference.CachedPlayer != null &&
            Object.FindFirstObjectByType<CharacterSpawner>()?.SpawnedPlayer != null &&
            Object.FindFirstObjectByType<RunFlowController>() != null &&
            Object.FindFirstObjectByType<EnemySpawner>() != null &&
            Camera.main != null && Camera.main.isActiveAndEnabled &&
            Object.FindFirstObjectByType<OrbitalStationRuntime>() is { IsInitialized: true } && !SceneTransitionOverlay.IsTransitioning);
    }

    private static GameObject PrepareProduction()
    {
        Object.FindFirstObjectByType<RunFlowController>().StopRunGameplay();
        foreach (var enemy in EnemyHealth.ActiveInstances.ToArray()) Object.DestroyImmediate(enemy.gameObject);
        var player = Object.FindFirstObjectByType<CharacterSpawner>().SpawnedPlayer;
        var body = player.GetComponent<Rigidbody2D>();
        body.linearVelocity = Vector2.zero;
        body.constraints = RigidbodyConstraints2D.FreezeAll;
        return player;
    }

    private static EnemyHealth Target(Vector3 position)
    {
        var target = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/_Project/prefabs/Enemies/p_Enemy_Shooter.prefab"), position, Quaternion.identity);
        foreach (var movement in target.GetComponents<EnemyMovement>()) movement.enabled = false;
        var body = target.GetComponent<Rigidbody2D>();
        if (body != null) body.constraints = RigidbodyConstraints2D.FreezeAll;
        target.GetComponent<EnemyHealth>().SetRuntimeMaxHealth(1000f);
        return target.GetComponent<EnemyHealth>();
    }

    private static IEnumerator WaitRealtime(float seconds)
    {
        float until = Time.realtimeSinceStartup + seconds;
        while (Time.realtimeSinceStartup < until) yield return null;
    }
    private static IEnumerator WaitGameTime(float seconds)
    {
        float until = Time.time + seconds;
        while (Time.time < until) yield return null;
    }
}
#endif
