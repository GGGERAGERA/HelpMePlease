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
    public void BreachersPrepareIndependentlyAndContactHitEndsCharge()
    {
        var first = Create(4.5f);
        first.ConfigureCommittedRamExperiment(true);
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
            Assert.That(Read<object>(second, "ramPhase").ToString(), Is.EqualTo("Preparing"), "Each breacher commits independently.");
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
    public void FlankPoseBlendsOutAtMeleeDistance()
    {
        var chase = Create();
        chase.ConfigureFlankingExperiment(true, false);
        for (int i = 0; i < 40; i++) Call(chase, "MoveToPlayer");
        var graphic = Read<Transform>(chase, "flankingVisual");
        var before = graphic.localRotation;
        Assert.That(Quaternion.Angle(before, Quaternion.identity), Is.GreaterThan(5f), "Flank should have a visible lean.");
        actor.GetComponent<Rigidbody2D>().position = Vector2.left * 3.9f;
        Call(chase, "MoveToPlayer");
        Assert.That(Quaternion.Angle(before, graphic.localRotation), Is.LessThan(3f), "Ending the arc must blend its visual pose instead of snapping upright.");
    }

    private EnemyChaseMovement CreateBreacher()
    {
        actor = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Enemies + "p_Enemy_Breacher.prefab"));
        var chase = actor.GetComponent<EnemyChaseMovement>();
        if (Read<Rigidbody2D>(chase, "rb") == null) Call(chase, "Awake");
        target = new GameObject("Tactical test target");
        typeof(EnemyChaseMovement).GetField("player", Private).SetValue(chase, target.transform);
        actor.GetComponent<Rigidbody2D>().position = Vector2.left * 4.5f;
        chase.ConfigureLabTacticalMovement();
        chase.ConfigureCommittedRamExperiment(true);
        return chase;
    }

    [Test]
    public void BreacherTelegraphReachesItsApproachRange()
    {
        var chase = CreateBreacher(); Call(chase, "MoveToPlayer");
        var line = Read<LineRenderer>(chase, "ramTelegraph");
        Assert.That(line.enabled, Is.True);
        Assert.That(Vector2.Distance(line.GetPosition(0), line.GetPosition(1)), Is.GreaterThanOrEqualTo(4.49f),
            "A committed ram must threaten the approach distance shown to the player.");
    }

    [Test]
    public void BreacherDoesNotImmediatelyRepeatAgainstNearbyPlayer()
    {
        var chase = CreateBreacher(); Call(chase, "MoveToPlayer");
        for (int i = 0; i < 220 && Read<object>(chase, "ramPhase").ToString() != "Chasing"; i++) Call(chase, "MoveToPlayer");
        Assert.That(Read<object>(chase, "ramPhase").ToString(), Is.EqualTo("Chasing"));
        // Only this actor's separation and cooldown govern its next approach.
        Call(chase, "MoveToPlayer");
        Assert.That(Read<object>(chase, "ramPhase").ToString(), Is.EqualTo("Chasing"),
            "Recovery must lead to ordinary combat rather than another immediate ram at the same nearby player.");
    }

    [Test]
    public void ProductionProfilesAddBothVariantsAndKeepExistingEnemies()
    {
        for (int sector = 1; sector <= RunRoute.TotalSectors; sector++)
        {
            var profile = AssetDatabase.LoadAssetAtPath<EnemySpawnProfile>($"Assets/_Project/Data/Stages/EnemySpawnProfiles/ESP_Level_{sector:00}.asset");
            foreach (var phase in profile.Phases)
            {
                var flanker = phase.enemies.SingleOrDefault(e => e.enemyPrefab != null && e.enemyPrefab.name.StartsWith("p_Enemy_Flanker"));
                var breacher = phase.enemies.SingleOrDefault(e => e.enemyPrefab != null && e.enemyPrefab.name.StartsWith("p_Enemy_Breacher"));
                Assert.That(flanker != null, Is.EqualTo(sector >= 2), profile.name + " flanker availability");
                Assert.That(breacher != null, Is.EqualTo(sector >= 3), profile.name + " breacher availability");
                if (flanker != null) { Assert.That(flanker.weight, Is.EqualTo(6f)); Assert.That(flanker.maxAliveOfType, Is.Zero); Assert.That(flanker.useExactAliveLimit, Is.False); Assert.That(flanker.minimumRunLevel, Is.EqualTo(2)); }
                if (breacher != null) { Assert.That(breacher.weight, Is.EqualTo(4f)); Assert.That(breacher.maxAliveOfType, Is.Zero); Assert.That(breacher.useExactAliveLimit, Is.False); Assert.That(breacher.minimumRunLevel, Is.EqualTo(3)); }
                Assert.That(phase.enemies.Any(e => e.enemyPrefab != null && e.enemyPrefab.name.StartsWith("p_Enemy_classic")), Is.True);
                foreach (var entry in new[] { flanker, breacher }.Where(e => e != null))
                {
                    var parent = PrefabUtility.GetCorrespondingObjectFromSource(entry.enemyPrefab);
                    Assert.That(entry.enemyPrefab.GetComponent<EnemyHealth>().MaxHealth,
                        Is.EqualTo(entry == flanker ? 16f : 32f));
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

    [UnityTest, Timeout(60000)]
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
        Assert.That(RunStateManager.Instance.CurrentSector.SectorNumber, Is.EqualTo(1));
        Assert.That(phase.enemies.Any(e => e.enemyPrefab.GetComponent<EnemyChaseMovement>() is { IsProductionTacticalEnemy: true }), Is.False,
            "Sector one keeps ordinary easy enemies.");
        var ordinary = phase.enemies.First(e => e.enemyPrefab.name.StartsWith("p_Enemy_classic"));
        Assert.That(ordinary.enemyPrefab.GetComponent<EnemyHealth>().MaxHealth, Is.EqualTo(8f));
        var firstZombie = (GameObject)Call(spawner, "SpawnEnemyAt", ordinary.enemyPrefab, player.transform.position + Vector3.left * 4f, true);
        var firstHp = firstZombie.GetComponent<EnemyHealth>();
        Assert.That(firstHp.MaxHealth, Is.EqualTo(8f));
        var policy = firstZombie.AddComponent<LabEnemyDummy>(); policy.Initialize(); policy.Invulnerable = false;
        ((ProductionOrbitalCombatAdapter)station.Combat).ApplyDamage(firstHp, OrbitalPistolModule.BaseDamage, firstZombie.transform.position);
        Assert.That(firstHp.IsDead, Is.True, "One starter-pistol damage hit still kills the ordinary first-sector zombie.");
        // Short sector API transitions, without waiting through entire exploration timers.
        // Each reload exercises the actual MVP bootstrap/profile binding, not a test-only spawner.
        for (int sector = 2; sector <= 3; sector++)
        {
            var run = RunStateManager.Instance;
            var oldSector = run.CurrentSector;
            var profile = AssetDatabase.LoadAssetAtPath<StageProfileData>($"Assets/_Project/Data/Stages/StageProfiles/StageProfile_{sector:00}.asset");
            run.SetCurrentSector(new RunSector(sector, profile, oldSector.WorldRule, oldSector.LocalAnomaly));
            yield return UnityEngine.SceneManagement.SceneManager.LoadSceneAsync("MVP");
            yield return CoreTestSupport.Await(() => Object.FindFirstObjectByType<OrbitalStationRuntime>() is { IsInitialized: true } && !SceneTransitionOverlay.IsTransitioning);
            spawner = Object.FindFirstObjectByType<EnemySpawner>();
            player = Object.FindFirstObjectByType<CharacterSpawner>().SpawnedPlayer;
            station = Object.FindFirstObjectByType<OrbitalStationRuntime>();
            phase = Read<EnemySpawnPhase>(spawner, "activePhase");
            Assert.That(Read<EnemySpawnProfile>(spawner, "spawnProfile"), Is.SameAs(profile.SpawnProfile));
            Assert.That(RunStateManager.Instance.IsDevelopmentRun, Is.False);
            if (sector == 2)
            {
                spawner.enabled = false; station.enabled = false;
                foreach (var enemy in EnemyHealth.ActiveInstances.ToArray()) Object.DestroyImmediate(enemy.gameObject);
                Call(spawner, "RemoveDestroyedEnemies");
                Assert.That(phase.enemies.Any(e => e.enemyPrefab.name.StartsWith("p_Enemy_Breacher")), Is.False);
                var side = phase.enemies.Single(e => e.enemyPrefab.name == "p_Enemy_Flanker");
                var draws = new System.Collections.Generic.HashSet<GameObject>();
                GameplayRandom.Begin(42);
                for (int i = 0; i < 128; i++) draws.Add((GameObject)Call(spawner, "SelectWeightedEnemy", phase));
                GameplayRandom.End();
                Assert.That(draws, Does.Contain(side.enemyPrefab));
                for (int i = 0; i < 2; i++) Call(spawner, "SpawnEnemyAt", side.enemyPrefab, player.transform.position + new Vector3(-7, i * 2), true);
                Assert.That((bool)Call(spawner, "CanSpawn", side), Is.True, "Sector 2 has no flanker type cap.");
            }
            TestContext.WriteLine($"MVP sector {sector}: actual profile {profile.SpawnProfile.name}, Development={run.IsDevelopmentRun}.");
        }
        playerBody = player.GetComponent<Rigidbody2D>(); constraints = playerBody.constraints;
        stationEnabled = station.enabled; spawnerEnabled = spawner.enabled;
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
            Assert.That((bool)Call(spawner, "CanSpawn", flankerEntry), Is.True);
            Assert.That((bool)Call(spawner, "CanSpawn", breacherEntry), Is.True);
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
            Assert.That(Read<bool>(flanker, "spiralApproach"), Is.True, "Continuous production spiral never times out.");
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
        string path = System.IO.Path.GetFullPath(TacticalEnemyNightPreview.Output + "/mvp-" + phase + ".png");
        System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path));
        var started = DateTime.UtcNow;
        ScreenCapture.CaptureScreenshot(path);
        yield return CoreTestSupport.Await(() => System.IO.File.Exists(path) && System.IO.File.GetLastWriteTimeUtc(path) >= started);
    }
}
#endif
