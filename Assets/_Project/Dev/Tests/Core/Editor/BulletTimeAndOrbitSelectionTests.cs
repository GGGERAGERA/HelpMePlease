#if UNITY_EDITOR
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using Subject42.Combat.OrbitalStation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

public sealed class BulletTimeAndOrbitSelectionTests
{
    [SetUp] public void Setup() => CoreTestSupport.PreservePreferences();
    [UnityTearDown] public IEnumerator Cleanup() => CoreTestSupport.CleanupPlayMode();

    [Test]
    public void EnergyUsesThreeRealSecondsAndSixSecondRecharge()
    {
        var ability = new BulletTimeAbility();
        ability.Tick(true, 1f);
        Assert.That(ability.Energy, Is.EqualTo(2f / 3f).Within(.0001f));
        ability.Tick(true, 2.01f);
        Assert.That(ability.Energy, Is.Zero);
        Assert.That(ability.IsActive, Is.False);
        ability.Tick(true, 1f);
        Assert.That(ability.Energy, Is.Zero, "Exhaustion must not pulse while held");
        ability.Tick(false, 3f);
        Assert.That(ability.Energy, Is.EqualTo(.5f));
        ability.Tick(false, 0f);
        Assert.That(ability.Energy, Is.EqualTo(.5f), "Pause freezes energy");
        ability.Tick(false, 3f);
        Assert.That(ability.Energy, Is.EqualTo(1f));
    }

    [UnityTest]
    public IEnumerator GameplayTimeAndOrbitTargetFlow()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
        ProductionSceneCompositionAuthoring.EnsureScene(SceneManager.GetActiveScene());
        yield return new EnterPlayMode();
        yield return VerifyInPlayMode();
    }

    private static IEnumerator VerifyInPlayMode()
    {
        var character = AssetDatabase.LoadAssetAtPath<CharacterData>("Assets/_Project/Data/Characters/01_Gera.asset");
        var stage = AssetDatabase.LoadAssetAtPath<StageProfileData>("Assets/_Project/Data/Stages/StageProfiles/StageProfile_01.asset");
        var rule = AssetDatabase.LoadAssetAtPath<WorldRuleData>("Assets/_Project/Data/World/Rules/WorldRule_None.asset");
        var anomaly = AssetDatabase.LoadAssetAtPath<LocalAnomalyData>("Assets/_Project/Data/Anomalies/LocalAnomaly_Gravity.asset");
        RunStateManager.EnsureExists().BeginNewRun(character, null, stage, rule, anomaly, true);
        yield return SceneManager.LoadSceneAsync("MVP");
        yield return CoreTestSupport.Await(() => PlayerRuntimeReference.CachedPlayer != null && HUDManager.Instance.IsInformationVisible);
        var player = PlayerRuntimeReference.CachedPlayer;
        player.GetComponent<PlayerHealth>().AddMaxHealth(100000f);
        Object.FindFirstObjectByType<EnemySpawner>().StopSpawning();
        var station = player.GetComponentInChildren<OrbitalStationRuntime>();
        var tick = typeof(OrbitalStationRuntime).GetMethod("TickRightMouse", BindingFlags.Instance | BindingFlags.NonPublic);
        float fixedStep = Time.fixedDeltaTime;
        tick.Invoke(station, new object[] { .3f, true, true });
        Assert.That(Time.timeScale, Is.InRange(.35f, .45f), "RMB must slow the world, not individual enemies");
        Assert.That(station.SlowField.Energy, Is.EqualTo(.9f).Within(.001f));
        tick.Invoke(station, new object[] { .05f, false, false });
        Assert.That(Time.timeScale, Is.InRange(.41f, .99f), "Release must blend smoothly");
        tick.Invoke(station, new object[] { .3f, false, false });
        Assert.That(Time.timeScale, Is.EqualTo(1f));
        Assert.That(Time.fixedDeltaTime, Is.EqualTo(fixedStep).Within(.000001f));
        tick.Invoke(station, new object[] { .3f, true, true });
        var pause = Object.FindFirstObjectByType<PauseMenuUI>();
        pause.Pause();
        float energy = station.SlowField.Energy;
        tick.Invoke(station, new object[] { 1f, false, false });
        Assert.That(Time.timeScale, Is.Zero);
        Assert.That(station.SlowField.Energy, Is.EqualTo(energy));
        pause.Resume();
        Assert.That(Time.timeScale, Is.EqualTo(1f));
        tick.Invoke(station, new object[] { .3f, true, true });
        station.enabled = false;
        Assert.That(Time.timeScale, Is.EqualTo(1f));
        Assert.That(Time.fixedDeltaTime, Is.EqualTo(fixedStep).Within(.000001f));
        station.enabled = true;

        // Exercise real Update/FixedUpdate clocks, with no per-enemy speed layer.
        // Drive the same production steps from a coroutine so this smoke also works
        // when Editor/GameView loses OS focus during automated execution.
        station.enabled = false;
        station.BulletTime.Reset();
        var hold = station.StartCoroutine(HoldRightMouse(station, tick));
        var movement = player.GetComponent<CharacterMovement2D>();
        movement.MovementIntent = () => Vector2.right;
        yield return new WaitForSecondsRealtime(.3f);
        Assert.That(Time.timeScale, Is.EqualTo(.4f).Within(.001f));
        station.BulletTime.Reset();
        typeof(CharacterMovement2D).GetField("currentVelocity", BindingFlags.Instance | BindingFlags.NonPublic)
            .SetValue(movement, Vector2.right * movement.speed);
        Assert.That(movement.BulletTimeControlMultiplier, Is.EqualTo(2.5f).Within(.001f));
        var projectile = new GameObject("Bullet time projectile", typeof(EnemyProjectile));
        projectile.transform.position = player.transform.position + Vector3.up * 20f;
        projectile.GetComponent<EnemyProjectile>().Initialize(Vector2.right);
        var physics = new GameObject("Bullet time physics", typeof(Rigidbody2D));
        var body = physics.GetComponent<Rigidbody2D>();
        body.gravityScale = 0f;
        body.linearVelocity = Vector2.right * 2f;
        float startScaled = Time.time, startReal = Time.unscaledTime;
        float energyBefore = station.BulletTime.Energy;
        float projectileX = projectile.transform.position.x, playerX = player.transform.position.x;
        float phase = station.Rings[0].Phase;
        while (Time.unscaledTime - startReal < .5f)
        {
            yield return null;
            // Editor pauses/import stalls clamp the gameplay clock. Compare steady frames,
            // and measure each world's displacement against the clock it actually received.
            if (Time.unscaledDeltaTime > 0f && Time.unscaledDeltaTime < .1f)
            {
                Assert.That(Time.deltaTime / Time.unscaledDeltaTime, Is.EqualTo(.4f).Within(.02f));
            }
        }
        float scaled = Time.time - startScaled, real = Time.unscaledTime - startReal;
        Assert.That(scaled, Is.GreaterThan(0f));
        Assert.That(scaled, Is.LessThan(real * .6f));
        Assert.That(projectile.transform.position.x - projectileX, Is.EqualTo(7f * scaled).Within(.12f));
        Assert.That(body.position.x, Is.EqualTo(2f * scaled).Within(.08f));
        Assert.That(Mathf.Abs(Mathf.DeltaAngle(phase, station.Rings[0].Phase)),
            Is.EqualTo(station.Rings[0].RotationSpeed * scaled).Within(2f));
        Assert.That(player.transform.position.x - playerX, Is.EqualTo(movement.speed * scaled / .4f).Within(.35f));
        Assert.That(energyBefore - station.BulletTime.Energy, Is.EqualTo(real / 3f).Within(.02f));
        Assert.That(station.Interaction.ScreenEffectVisible, Is.True);
        var screen = station.Interaction.GetComponentInChildren<UnityEngine.UI.Image>(true);
        Assert.That(screen.material.shader.isSupported, Is.True);
        station.StopCoroutine(hold);
        station.enabled = true;
        movement.MovementIntent = () => Vector2.zero;
        yield return new WaitForSecondsRealtime(.25f);
        Assert.That(Time.timeScale, Is.EqualTo(1f));
        Assert.That(station.Interaction.ScreenEffectVisible, Is.False);
        Object.Destroy(projectile);
        Object.Destroy(physics);

        var reward = ScriptableObject.CreateInstance<OrbitalRewardData>();
        reward.RewardKind = OrbitalRewardKind.RingSpeed; reward.RequiresArenaSelection = true;
        int speed = station.Rings[0].State.SpeedUpgradeLevel;
        bool completed = false;
        Assert.That(station.RewardFlow.Begin(reward, () => completed = true, () => {}), Is.True);
        Assert.That(completed, Is.True, "Single eligible orbit must commit synchronously");
        Assert.That(station.Rings[0].State.SpeedUpgradeLevel, Is.EqualTo(speed + 1));
        Assert.That(station.RewardFlow.State, Is.EqualTo(OrbitalRewardFlowState.Completed));
        Assert.That(station.Interaction.RingSelectionFocus, Is.False);
        var second = station.AddRing();
        Assert.That(second, Is.Not.Null);
        // A new empty ring is still a valid target and requires an explicit choice.
        completed = false;
        Assert.That(station.RewardFlow.Begin(reward, () => completed = true, () => {}), Is.True);
        Assert.That(completed, Is.False);
        Assert.That(station.RewardFlow.State, Is.EqualTo(OrbitalRewardFlowState.RingSelection));
        Assert.That(station.Interaction.RingSelectionFocus, Is.True);
        Assert.That(station.Interaction.ScreenEffectVisible, Is.True);
        yield return new WaitForSecondsRealtime(.25f);
        Assert.That(station.RewardFlow.DebugChooseRing(station.Rings[1].RingId), Is.True);
        Assert.That(completed, Is.True);
        Assert.That(station.Interaction.RingSelectionFocus, Is.False);
        Assert.That(station.Interaction.ScreenEffectVisible, Is.False);
        Assert.That(station.RewardFlow.Begin(reward, () => {}, () => {}), Is.True);
        station.RewardFlow.CancelForSceneTransition();
        Assert.That(station.Interaction.RingSelectionFocus, Is.False);
        Assert.That(station.Interaction.ScreenEffectVisible, Is.False);
        Object.Destroy(reward);
        yield return null;
    }

    private static IEnumerator HoldRightMouse(OrbitalStationRuntime station, MethodInfo tick)
    {
        var tickStation = typeof(OrbitalStationRuntime).GetMethod("TickStation", BindingFlags.Instance | BindingFlags.NonPublic);
        while (true)
        {
            tick.Invoke(station, new object[] { Time.unscaledDeltaTime, true, false });
            tickStation.Invoke(station, new object[] { Time.deltaTime });
            yield return null;
        }
    }
}
#endif
