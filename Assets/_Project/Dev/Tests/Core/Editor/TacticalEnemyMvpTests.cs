#if UNITY_EDITOR
using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Subject42.Combat.OrbitalStation;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

public sealed class TacticalEnemyMvpTests
{
    private const string Enemies = "Assets/_Project/prefabs/Enemies/";
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static T Read<T>(object value, string name) => (T)value.GetType().GetField(name, Private).GetValue(value);
    private static void Call(object value, string name, params object[] args) => value.GetType().GetMethod(name, Private).Invoke(value, args);
    private GameObject actor, target;

    private EnemyChaseMovement Create(float distance = 10f)
    {
        actor = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Enemies + "p_Enemy_classic_start.prefab"));
        var chase = actor.GetComponent<EnemyChaseMovement>();
        if (Read<Rigidbody2D>(chase, "rb") == null) Call(chase, "Awake");
        target = new GameObject("Tactical test target");
        typeof(EnemyChaseMovement).GetField("player", Private).SetValue(chase, target.transform);
        actor.GetComponent<Rigidbody2D>().position = Vector2.left * distance;
        return chase;
    }

    [TearDown] public void DestroyActors()
    {
        if (actor != null) Object.DestroyImmediate(actor);
        if (target != null) Object.DestroyImmediate(target);
    }

    [Test]
    public void FlankEndsOnceNearPlayerAndDoesNotRearmOnRetreat()
    {
        var chase = Create();
        chase.ConfigureFlankingExperiment(true, false);
        Call(chase, "MoveToPlayer");
        Assert.That(Read<Vector2>(chase, "chaseVelocity").y, Is.GreaterThan(.1f));
        actor.GetComponent<Rigidbody2D>().position = Vector2.left * 3f;
        Call(chase, "MoveToPlayer");
        actor.GetComponent<Rigidbody2D>().position = Vector2.left * 10f;
        Call(chase, "MoveToPlayer");
        Assert.That(Read<bool>(chase, "flankingApproach"), Is.False, "One arc must not restart when the player leaves.");
    }

    [Test]
    public void FlankHasBoundedDurationEvenWhenPlayerCannotBeReached()
    {
        var chase = Create();
        chase.ConfigureFlankingExperiment(true, true);
        for (int i = 0; i < 160; i++) Call(chase, "MoveToPlayer");
        Assert.That(Read<bool>(chase, "flankingApproach"), Is.False, "3.2 seconds outside melee range must finish the arc.");
        Assert.That(Read<Vector2>(chase, "chaseVelocity").x, Is.GreaterThan(0f));
        Assert.That(Mathf.Abs(Read<Vector2>(chase, "chaseVelocity").y), Is.LessThan(.01f));
    }

    [Test]
    public void BreachersCannotPrepareTogetherAndContactHitEndsCharge()
    {
        var first = Create(4.5f);
        first.ConfigureCommittedRamExperiment(true);
        // A previous test's release gap is permitted; advance only the static clock gate.
        var next = typeof(EnemyChaseMovement).GetField("ramNextAllowedAt", BindingFlags.Static | BindingFlags.NonPublic);
        next?.SetValue(null, 0f);
        Call(first, "MoveToPlayer");
        var other = Object.Instantiate(actor);
        try
        {
            var second = other.GetComponent<EnemyChaseMovement>();
            if (Read<Rigidbody2D>(second, "rb") == null) Call(second, "Awake");
            typeof(EnemyChaseMovement).GetField("player", Private).SetValue(second, target.transform);
            other.GetComponent<Rigidbody2D>().position = Vector2.right * 4.5f;
            second.ConfigureCommittedRamExperiment(true);
            Call(second, "MoveToPlayer");
            Assert.That(Read<object>(second, "ramPhase").ToString(), Is.EqualTo("Chasing"), "Only one global attack commitment is allowed.");
            var line = Read<Vector2>(first, "ramDirection");
            target.transform.position = Vector3.up * 8f;
            for (int i = 0; i < 34; i++) Call(first, "MoveToPlayer");
            Assert.That(Read<object>(first, "ramPhase").ToString(), Is.EqualTo("Charging"));
            Assert.That(Read<Vector2>(first, "ramDirection"), Is.EqualTo(line));
            first.StopAfterHit();
            Assert.That(Read<object>(first, "ramPhase").ToString(), Is.EqualTo("Recovering"), "A landed charge cannot pause and resume its attack.");
            var contact = actor.GetComponent<EnemyCollisionHandler>();
            Call(contact, "Awake");
            typeof(EnemyCollisionHandler).GetField("lastDamageTime", Private).SetValue(contact, -100f);
            Assert.That((bool)typeof(EnemyCollisionHandler).GetMethod("CanAttemptDamage", Private).Invoke(contact, null), Is.False, "Recovery is safe to approach.");
        }
        finally { Object.DestroyImmediate(other); }
    }

    [Test]
    public void TemporaryRecoveryModifierCanBeReplacedRemovedAndCleared()
    {
        var chase = Create(4.5f);
        var set = typeof(EnemyChaseMovement).GetMethod("SetAttackRecoveryMultiplier");
        var remove = typeof(EnemyChaseMovement).GetMethod("RemoveAttackRecoveryMultiplier");
        Assert.That(set, Is.Not.Null, "Future zones need an external recovery-duration modifier.");
        Assert.That(remove, Is.Not.Null);
        var rate = typeof(EnemyChaseMovement).GetProperty("AttackRecoveryMultiplier");
        set.Invoke(chase, new object[] { target, 2f });
        set.Invoke(chase, new object[] { target, 1.5f });
        Assert.That((float)rate.GetValue(chase), Is.EqualTo(1.5f));
        chase.ConfigureCommittedRamExperiment(true);
        Call(chase, "BeginRamRecovery");
        for (int i = 0; i < 50; i++) Call(chase, "MoveToPlayer");
        Assert.That(Read<object>(chase, "ramPhase").ToString(), Is.EqualTo("Recovering"));
        Assert.That(Read<float>(chase, "ramRemaining"), Is.EqualTo(1.2f - 1f / 1.5f).Within(.025f));
        remove.Invoke(chase, new object[] { target });
        Assert.That((float)rate.GetValue(chase), Is.EqualTo(1f));
        target.transform.position = Vector3.right * 20f;
        for (int i = 0; i < 28; i++) Call(chase, "MoveToPlayer");
        Assert.That(Read<object>(chase, "ramPhase").ToString(), Is.EqualTo("Chasing"));
        set.Invoke(chase, new object[] { target, 2f });
        Call(chase, "OnDisable");
        Assert.That((float)rate.GetValue(chase), Is.EqualTo(1f));
    }

    [Test]
    public void ProductionProfilesAddBothVariantsAndKeepExistingEnemies()
    {
        foreach (string guid in AssetDatabase.FindAssets("t:EnemySpawnProfile", new[] { "Assets/_Project/Data/Stages/EnemySpawnProfiles" }))
        {
            var profile = AssetDatabase.LoadAssetAtPath<EnemySpawnProfile>(AssetDatabase.GUIDToAssetPath(guid));
            foreach (var phase in profile.Phases)
            {
                var flanker = phase.enemies.SingleOrDefault(e => e.enemyPrefab != null && e.enemyPrefab.name.StartsWith("p_Enemy_Flanker"));
                var breacher = phase.enemies.SingleOrDefault(e => e.enemyPrefab != null && e.enemyPrefab.name.StartsWith("p_Enemy_Breacher"));
                Assert.That(flanker, Is.Not.Null, profile.name);
                Assert.That(breacher, Is.Not.Null, profile.name);
                Assert.That(flanker.weight, Is.EqualTo(8f));
                Assert.That(breacher.weight, Is.EqualTo(5f));
                Assert.That(flanker.maxAliveOfType, Is.EqualTo(2));
                Assert.That(breacher.maxAliveOfType, Is.EqualTo(1));
                Assert.That(flanker.useExactAliveLimit && breacher.useExactAliveLimit, Is.True);
                Assert.That(phase.enemies.Any(e => e.enemyPrefab != null && e.enemyPrefab.name.StartsWith("p_Enemy_classic")), Is.True);
                foreach (var entry in new[] { flanker, breacher })
                {
                    var parent = PrefabUtility.GetCorrespondingObjectFromSource(entry.enemyPrefab);
                    Assert.That(entry.enemyPrefab.GetComponent<EnemyHealth>().MaxHealth,
                        Is.EqualTo(parent.GetComponent<EnemyHealth>().MaxHealth));
                    Assert.That(entry.enemyPrefab.GetComponent<EnemyCollisionHandler>().damage,
                        Is.EqualTo(parent.GetComponent<EnemyCollisionHandler>().damage));
                    Assert.That(EnemySpawner.IsFlankingChaserEligible(entry.enemyPrefab), Is.False, "F1 must not reassign production archetypes.");
                    Assert.That(EnemySpawner.IsCommittedRamEligible(entry.enemyPrefab), Is.False);
                }
            }
        }
    }
}

public sealed class TacticalEnemyMvpSmokeTests
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static T Read<T>(object value, string name) => (T)value.GetType().GetField(name, Private).GetValue(value);
    private static object Call(object value, string name, params object[] args) => value.GetType().GetMethod(name, Private).Invoke(value, args);
    [SetUp] public void Preserve() => CoreTestSupport.PreservePreferences();
    [UnitySetUp] public IEnumerator Begin() => CoreTestSupport.BeginRun();
    [UnityTearDown] public IEnumerator Cleanup() => CoreTestSupport.CleanupPlayMode();

    [UnityTest, Timeout(90000)]
    public IEnumerator OrdinaryMvpVariantsFlankCommitRecoverAndKeepTheirCaps() => Body();

    private static IEnumerator Body()
    {
        var spawner = Object.FindFirstObjectByType<EnemySpawner>();
        var player = Object.FindFirstObjectByType<CharacterSpawner>().SpawnedPlayer;
        var station = Object.FindFirstObjectByType<OrbitalStationRuntime>();
        var playerBody = player.GetComponent<Rigidbody2D>();
        var constraints = playerBody.constraints;
        bool stationEnabled = station.enabled;
        bool spawnerEnabled = spawner.enabled;
        var phase = Read<EnemySpawnPhase>(spawner, "activePhase");
        Assert.That(phase, Is.Not.Null, "MVP must bind an authored ordinary profile.");
        var flankerEntry = phase.enemies.Single(e => e.enemyPrefab.name.StartsWith("p_Enemy_Flanker"));
        var breacherEntry = phase.enemies.Single(e => e.enemyPrefab.name.StartsWith("p_Enemy_Breacher"));
        bool development = RunStateManager.Instance.IsDevelopmentRun;
        Assert.That(development, Is.False, "Test starts through the ordinary bunker → MVP route.");
        spawner.enabled = false;
        station.enabled = false; // Isolate the short motion observation from weapon knockback.
        playerBody.constraints = RigidbodyConstraints2D.FreezeAll;
        var actors = new System.Collections.Generic.List<GameObject>();
        GameObject Spawn(EnemySpawnEntry entry, Vector2 offset)
        {
            var result = (GameObject)Call(spawner, "SpawnEnemyAt", entry.enemyPrefab, player.transform.position + (Vector3)offset, true);
            actors.Add(result);
            return result;
        }
        try
        {
            GameplayRandom.Begin(42);
            var existing = EnemyHealth.ActiveInstances.ToArray();
            spawner.SpawnAdditionalWave(player.transform.position, 80, 8f, 12f, 6f);
            var eventWave = EnemyHealth.ActiveInstances.Except(existing).ToArray();
            Assert.That(eventWave.Length, Is.GreaterThan(0));
            Assert.That(eventWave.Any(e => e.GetComponent<EnemyChaseMovement>() is { IsProductionTacticalEnemy: true }),
                Is.False, "Existing event waves must keep their original roster.");
            foreach (var enemy in eventWave) Object.DestroyImmediate(enemy.gameObject);
            Call(spawner, "RemoveDestroyedEnemies");
            var selected = new System.Collections.Generic.HashSet<GameObject>();
            for (int i = 0; i < 256; i++) selected.Add((GameObject)Call(spawner, "SelectWeightedEnemy", phase));
            Assert.That(selected, Does.Contain(flankerEntry.enemyPrefab));
            Assert.That(selected, Does.Contain(breacherEntry.enemyPrefab));
            var flanker = Spawn(flankerEntry, Vector2.left * 7f).GetComponent<EnemyChaseMovement>();
            Spawn(flankerEntry, new Vector2(-8f, -3f));
            var breacher = Spawn(breacherEntry, Vector2.right * 4.5f).GetComponent<EnemyChaseMovement>();
            string Phase() => Read<object>(breacher, "ramPhase").ToString();
            Assert.That(RunStateManager.Instance.IsDevelopmentRun, Is.EqualTo(development), "Production variants never mark the run Development.");
            Assert.That((bool)Call(spawner, "CanSpawn", flankerEntry), Is.False);
            Assert.That((bool)Call(spawner, "CanSpawn", breacherEntry), Is.False);
            Assert.That(phase.enemies.Any(e => !e.enemyPrefab.GetComponent<EnemyChaseMovement>()?.IsProductionTacticalEnemy ?? true), Is.True);
            yield return CoreTestSupport.Await(() => Phase() == "Preparing");
            var cue = Read<LineRenderer>(breacher, "ramTelegraph");
            Assert.That(cue.enabled, Is.True);
            var direction = Read<Vector2>(breacher, "ramDirection");
            Assert.That(Vector2.Dot((cue.GetPosition(1) - cue.GetPosition(0)).normalized, direction), Is.GreaterThan(.999f));
            float timer = Read<float>(breacher, "ramRemaining");
            Time.timeScale = 0f;
            yield return new WaitForSecondsRealtime(.08f);
            Assert.That(Read<float>(breacher, "ramRemaining"), Is.EqualTo(timer));
            yield return Capture("preparing"); // Capture the telegraph before the diagnostic player teleport.
            Time.timeScale = 1f;
            playerBody.position += Vector2.up * 2.5f;
            Physics2D.SyncTransforms();
            yield return CoreTestSupport.Await(() => Phase() == "Charging");
            Assert.That(Read<Vector2>(breacher, "ramDirection"), Is.EqualTo(direction));
            var chargePosition = breacher.GetComponent<Rigidbody2D>().position;
            yield return CoreTestSupport.Await(() => Phase() == "Recovering");
            Assert.That(Vector2.Dot(breacher.GetComponent<Rigidbody2D>().position - chargePosition, direction), Is.GreaterThan(.5f));
            Assert.That(breacher.CanDealContactDamage, Is.False);
            Assert.That(cue.enabled, Is.False);
            var hp = breacher.GetComponent<EnemyHealth>();
            float before = hp.CurrentHealth;
            hp.TakeDamage(1f, player.transform.position);
            Assert.That(hp.CurrentHealth, Is.EqualTo(before - 1f).Within(.001f), "Recovery accepts ordinary weapon damage.");
            var recoveryPosition = breacher.GetComponent<Rigidbody2D>().position;
            yield return new WaitForSeconds(.2f);
            Assert.That(Vector2.Distance(recoveryPosition, breacher.GetComponent<Rigidbody2D>().position), Is.LessThan(.03f));
            yield return Capture("recovering");
            yield return CoreTestSupport.Await(() => !Read<bool>(flanker, "flankingApproach"));
            Assert.That(flanker.IsFlankingExperiment, Is.True, "The archetype persists after its single arc.");
            station.enabled = stationEnabled;
            yield return new WaitForSeconds(.3f); // A short live combat tail with the existing weapon system.
        }
        finally
        {
            Time.timeScale = 1f;
            GameplayRandom.End();
            foreach (var actor in actors) if (actor != null) Object.DestroyImmediate(actor);
            playerBody.constraints = constraints;
            station.enabled = stationEnabled;
            spawner.enabled = spawnerEnabled;
        }
    }

    private static IEnumerator Capture(string phase)
    {
        string path = System.IO.Path.GetFullPath("Artifacts/GeneratedQA/TacticalEnemies/" + phase + ".png");
        System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path));
        var started = DateTime.UtcNow;
        ScreenCapture.CaptureScreenshot(path);
        yield return CoreTestSupport.Await(() => System.IO.File.Exists(path) && System.IO.File.GetLastWriteTimeUtc(path) >= started);
    }
}
#endif
