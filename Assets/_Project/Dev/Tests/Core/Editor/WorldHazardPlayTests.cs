using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

public sealed class WorldHazardPlayTests
{
    private const string Fx = "Assets/_Project/prefabs/fx/";
    [SetUp] public void Setup() => CoreTestSupport.PreservePreferences();
    [UnityTearDown] public IEnumerator Cleanup() => CoreTestSupport.CleanupPlayMode();

    [UnitySetUp]
    public IEnumerator EnterGame()
    {
        EditorWindow.GetWindow(typeof(Editor).Assembly.GetType("UnityEditor.GameView")).Show();
        // Return the actual setup iterator: nested/manual enumeration cannot survive a domain reload.
        return TestContext.CurrentContext.Test.Name.StartsWith("ProductionSector")
            ? CoreTestSupport.BeginRun() : EnterIsolatedScene();
    }

    private IEnumerator EnterIsolatedScene()
    {
        if (TestContext.CurrentContext.Test.Name.StartsWith("BossKeeps"))
            EditorSceneManager.OpenScene("Assets/_Project/Dev/Labs/F1/BossPracticeLab.unity");
        else EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
        yield return new EnterPlayMode();
        Assert.That(Application.isPlaying, Is.True, "Tests must run in actual Play Mode");
        VisualTuningPresetStorage.Configure(AssetDatabase.LoadAssetAtPath<VisualTuningPreset>(
            VisualTuningPresetStorage.AssetPath));
        yield return null;
    }

    private static PooledGameObject[] ActiveFx(string name) => Object.FindObjectsByType<PooledGameObject>(
        FindObjectsSortMode.None).Where(x => x.name.StartsWith(name)).ToArray();

    private static void Set(object target, string name, object value) =>
        target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);

    [UnityTest]
    public IEnumerator SharedFlightWarnsDamagesOnceAndReusesAllThreeAssets()
    {
        var owner = new GameObject("Runner owner").AddComponent<WorldHazardDirector>();
        var player = new GameObject("Player") { tag = "Player" };
        var health = player.AddComponent<PlayerHealth>();
        health.maxHealth = health.currentHealth = 1000f;
        player.AddComponent<CircleCollider2D>();
        var child = new GameObject("Compound collider");
        child.transform.SetParent(player.transform);
        child.AddComponent<CircleCollider2D>();
        var rocket = AssetDatabase.LoadAssetAtPath<GameObject>(Fx + "p_fxRocket1.prefab");
        var marker = AssetDatabase.LoadAssetAtPath<GameObject>(Fx + "fx_BossTaget1.prefab");
        var explosion = AssetDatabase.LoadAssetAtPath<GameObject>(Fx + "fx_BomberExplosion.prefab").GetComponent<ParticleSystem>();
        using var runner = new RocketAttackRunner(owner, rocket, marker, explosion);
        int markerId = 0, rocketId = 0, impactId = 0;
        yield return null;
        for (int round = 0; round < 3; round++)
        {
            float hp = health.CurrentHealth;
            Assert.That(runner.Launch(Vector3.zero, Vector3.left * 8f, .8f, .4f, 2f), Is.True);
            var warning = ActiveFx(marker.name).Single();
            if (round == 0) markerId = warning.GetInstanceID();
            Assert.That(warning.GetInstanceID(), Is.EqualTo(markerId));
            Assert.That(warning.transform.localScale.x, Is.EqualTo(4f));
            var launch = ActiveFx(rocket.name).Single();
            if (round == 0) rocketId = launch.GetInstanceID();
            Assert.That(launch.GetInstanceID(), Is.EqualTo(rocketId));
            Assert.That(launch.PrimaryParticleSystem.particleCount, Is.EqualTo(1));
            runner.Tick(.79f, 14f, 2f, 25, () => true);
            Assert.That(health.CurrentHealth, Is.EqualTo(hp), "No damage during warning");
            Assert.That(warning.transform.position, Is.EqualTo(Vector3.zero));
            runner.Tick(.02f, 14f, 2f, 25, () => true);
            var falling = ActiveFx(rocket.name).Single();
            Assert.That(falling.GetInstanceID(), Is.EqualTo(rocketId), "Ascent instance reused for descent");
            Assert.That(falling.transform.position.y, Is.GreaterThan(0f));
            Assert.That(falling.PrimaryParticleSystem.main.startRotation.constant,
                Is.EqualTo(-Mathf.PI * .5f).Within(.001f));
            Assert.That(falling.GetComponentsInChildren<ParticleSystem>().All(ps => ps.isPlaying), Is.True);
            runner.Tick(.4f, 14f, 2f, 25, () => true);
            Assert.That(runner.PendingCount, Is.Zero);
            Assert.That(health.CurrentHealth, Is.EqualTo(hp - 25), "Compound colliders receive one hit");
            Assert.That(ActiveFx(marker.name), Is.Empty);
            Assert.That(ActiveFx(rocket.name), Is.Empty);
            var impact = ActiveFx(explosion.name).Single();
            if (round == 0) impactId = impact.GetInstanceID();
            Assert.That(impact.GetInstanceID(), Is.EqualTo(impactId));
            runner.Tick(10f, 14f, 2f, 25, () => true);
            Assert.That(ActiveFx(explosion.name), Is.Empty);
            float vulnerableAt = Time.time + .7f;
            yield return CoreTestSupport.Await(() => Time.time >= vulnerableAt);
        }
        runner.Launch(Vector3.zero, null, 1.5f, .5f, 2f);
        runner.Cancel();
        runner.Cancel();
        float before = health.CurrentHealth;
        runner.Tick(5f, 14f, 2f, 25, () => true);
        Assert.That(health.CurrentHealth, Is.EqualTo(before));
        Assert.That(Object.FindObjectsByType<PooledGameObject>(FindObjectsSortMode.None), Is.Empty);
        health.SetCurrentHealth(5);
        health.SetIncomingDamageMultiplier(3f);
        runner.Launch(Vector3.zero, null, 1.5f, .5f, 2f);
        runner.Tick(2.1f, 14f, 2f, 25, () => true, true);
        Assert.That(health.CurrentHealth, Is.EqualTo(1f).Within(.0001f));
        Assert.That(health.IsDead, Is.False);
        runner.Cancel();
    }

    [UnityTest]
    public IEnumerator ProductionSectorSchedulesSequentialStrikesAndCancelsOnStopAndRestart()
    {
        var flow = RunFlowController.Instance;
        var director = Object.FindFirstObjectByType<WorldHazardDirector>();
        var spawner = Object.FindFirstObjectByType<CharacterSpawner>();
        var health = spawner.SpawnedPlayer.GetComponent<PlayerHealth>();
        var area = GameplayAreaService.Instance;
        Assert.That(director, Is.Not.Null, "Director must be authored in the normal production scene");
        Assert.That(director.AttacksStarted, Is.Zero, "Initial grace period");
        Object.FindFirstObjectByType<EnemySpawner>().StopSpawning();
        var preset = Object.Instantiate(RunStateManager.Instance.CurrentSector.StageProfile.WorldHazards);
        Set(preset, "initialDelay", 5f);
        Set(preset, "interval", new Vector2(5f, 5f));
        director.Initialize(preset, flow, spawner, area);
        Time.timeScale = 3f;
        for (int count = 1; count <= 3; count++)
        {
            int expected = count;
            yield return CoreTestSupport.Await(() => director.AttacksStarted >= expected);
            Assert.That(director.AttacksStarted, Is.EqualTo(count));
            Assert.That(director.HasPendingAttack, Is.True);
            Vector3 target = director.LastTarget;
            Assert.That(Vector2.Distance(target, health.transform.position),
                Is.GreaterThan(preset.Attacks[0].DangerRadius + preset.PlayerClearance));
            Assert.That(area.IsInsidePlayableArea(target, preset.Attacks[0].DangerRadius + preset.EdgeClearance), Is.True);
            Assert.That(ActiveFx("fx_BossTaget1").Length, Is.EqualTo(1));
            float hp = health.CurrentHealth;
            yield return CoreTestSupport.Await(() => !director.HasPendingAttack);
            Assert.That(director.LastTarget, Is.EqualTo(target), "Target must not follow player");
            Assert.That(health.CurrentHealth, Is.EqualTo(hp), "Stationary player starts outside danger");
        }
        yield return CoreTestSupport.Await(() => director.HasPendingAttack);
        flow.StopRunGameplay();
        Assert.That(director.HasPendingAttack, Is.False, "Synchronous cancellation on stop");
        Assert.That(ActiveFx("fx_BossTaget1"), Is.Empty);
        int stoppedCount = director.AttacksStarted;
        float stoppedUntil = Time.time + 3f;
        yield return CoreTestSupport.Await(() => Time.time >= stoppedUntil);
        Assert.That(director.AttacksStarted, Is.EqualTo(stoppedCount));
        flow.InitializeSector(RunStateManager.Instance.CurrentSector.StageProfile);
        director.Initialize(preset, flow, spawner, area);
        Assert.That(director.AttacksStarted, Is.Zero);
        yield return CoreTestSupport.Await(() => director.HasPendingAttack);
        director.enabled = false;
        Assert.That(director.HasPendingAttack, Is.False);
        director.enabled = true;
        Assert.That(director.HasPendingAttack, Is.False);
        yield return CoreTestSupport.Await(() => director.HasPendingAttack);
        flow.StopRunGameplay();
        Object.Destroy(preset);
    }

    [UnityTest]
    public IEnumerator BossKeepsDualHandBurstsMovementRecoveryAndDisableCleanup()
    {
        yield return CoreTestSupport.Await(() => Object.FindFirstObjectByType<BossPracticeArena>()?.Boss != null);
        var arena = Object.FindFirstObjectByType<BossPracticeArena>();
        var boss = arena.Boss;
        var chase = boss.GetComponent<EnemyChaseMovement>();
        arena.Player.AddMaxHealth(100000f);
        Time.timeScale = 2f;
        foreach (int burstSize in new[] { 1, 3, 5 })
        {
            boss.CancelAttack();
            boss.transform.position = arena.Player.transform.position + Vector3.left * 20f;
            boss.AttackCooldown = .1f;
            boss.ShotsPerBurst = burstSize;
            boss.RocketFallDelayMin = boss.RocketFallDelayMax = 2f;
            boss.CancelAttack();
            yield return CoreTestSupport.Await(() => boss.IsBurstActive);
            boss.AttackCooldown = 60f;
            int cycles = 0;
            var previous = BossRocketAttack.AttackState.Chasing;
            float deadline = Time.realtimeSinceStartup + 30f;
            while (boss.State != BossRocketAttack.AttackState.Recovering && Time.realtimeSinceStartup < deadline)
            {
                if (boss.State == BossRocketAttack.AttackState.Firing && previous != boss.State)
                {
                    cycles++;
                    Assert.That(boss.PendingRocketCount, Is.GreaterThanOrEqualTo(2));
                    var ascending = ActiveFx("p_fxRocket1").Where(x =>
                        x.PrimaryParticleSystem.main.startRotation.constant > 0f).ToArray();
                    Assert.That(ascending.Length, Is.EqualTo(2), "Both authored hand launches");
                }
                if (boss.IsBurstActive) Assert.That(chase.IsAttackPaused, Is.True);
                previous = boss.State;
                yield return null;
            }
            Assert.That(boss.State, Is.EqualTo(BossRocketAttack.AttackState.Recovering));
            Assert.That(cycles, Is.EqualTo(burstSize));
            Assert.That(chase.IsAttackPaused, Is.False);
            Assert.That(boss.PendingRocketCount, Is.GreaterThan(0), "Movement resumes while rockets are in flight");
            yield return CoreTestSupport.Await(() => boss.PendingRocketCount == 0);
        }
        boss.AttackCooldown = .1f;
        boss.CancelAttack();
        yield return CoreTestSupport.Await(() => boss.PendingRocketCount > 0);
        boss.enabled = false;
        Assert.That(boss.PendingRocketCount, Is.Zero);
        Assert.That(chase.IsAttackPaused, Is.False);
        Assert.That(ActiveFx("fx_BossTaget1"), Is.Empty);
        boss.enabled = true;
        yield return CoreTestSupport.Await(() => boss.PendingRocketCount > 0);
        boss.CancelAttack();
    }
}
