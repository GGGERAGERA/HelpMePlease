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
        var enemy = new GameObject("Bullet time chase clock", typeof(Rigidbody2D), typeof(EnemyChaseMovement));
        var enemyBody = enemy.GetComponent<Rigidbody2D>();
        enemyBody.bodyType = RigidbodyType2D.Kinematic;
        enemyBody.gravityScale = 0f;
        enemyBody.position = player.transform.position + Vector3.up * 20f;
        enemy.GetComponent<EnemyChaseMovement>().SetAssaultDestination(enemyBody.position + Vector2.right * 100f);
        station.StopCoroutine(hold);
        station.InputOwner.ReleaseBulletTime();
        station.BulletTime.Reset();
        hold = station.StartCoroutine(HoldRightMouse(station, tick, false));
        typeof(CharacterMovement2D).GetField("currentVelocity", BindingFlags.Instance | BindingFlags.NonPublic)
            .SetValue(movement, Vector2.right * movement.speed);
        yield return new WaitForSecondsRealtime(.15f);
        float normalPlayerX = player.transform.position.x, normalEnemyX = enemyBody.position.x;
        float normalPhase = station.Rings[0].Phase;
        float normalTime = Time.time, normalReal = Time.unscaledTime;
        yield return new WaitForSecondsRealtime(.6f);
        float normalElapsed = Time.time - normalTime;
        float normalRealElapsed = Time.unscaledTime - normalReal;
        float normalPlayerDistance = player.transform.position.x - normalPlayerX;
        float normalEnemyDistance = enemyBody.position.x - normalEnemyX;
        float normalRotation = Mathf.Abs(Mathf.DeltaAngle(normalPhase, station.Rings[0].Phase));
        Assert.That(normalPlayerDistance / normalElapsed, Is.EqualTo(movement.speed).Within(.3f));
        Assert.That(normalEnemyDistance / normalElapsed, Is.EqualTo(2f).Within(.2f));
        Assert.That(normalRotation / normalElapsed, Is.EqualTo(station.Rings[0].RotationSpeed).Within(2f));
        station.StopCoroutine(hold);
        hold = station.StartCoroutine(HoldRightMouse(station, tick));
        yield return new WaitForSecondsRealtime(.3f);
        Assert.That(Time.timeScale, Is.EqualTo(.4f).Within(.001f));
        station.BulletTime.Reset();
        typeof(CharacterMovement2D).GetField("currentVelocity", BindingFlags.Instance | BindingFlags.NonPublic)
            .SetValue(movement, Vector2.right * movement.speed);
        Assert.That(movement.BulletTimeControlMultiplier, Is.EqualTo(1.875f).Within(.001f));
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
        float enemyX = enemyBody.position.x;
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
        Assert.That(player.transform.position.x - playerX, Is.EqualTo(movement.speed * scaled * 1.875f).Within(.25f));
        Assert.That(enemyBody.position.x - enemyX, Is.EqualTo(2f * scaled).Within(.08f));
        Assert.That((player.transform.position.x - playerX) / real / (normalPlayerDistance / normalRealElapsed),
            Is.EqualTo(.75f).Within(.07f), "Measure the player's effective speed against ordinary movement.");
        System.IO.Directory.CreateDirectory("Artifacts/GeneratedQA/BulletTimeCleanup");
        System.IO.File.WriteAllText("Artifacts/GeneratedQA/BulletTimeCleanup/movement.txt",
            $"Normal player={normalPlayerDistance / normalRealElapsed:F3}; enemy={normalEnemyDistance / normalRealElapsed:F3} units/real second\n" +
            $"Bullet Time player={(player.transform.position.x - playerX) / real:F3}; enemy={(enemyBody.position.x - enemyX) / real:F3} units/real second\n" +
            $"World clock={scaled / real:F3}; ring normal={normalRotation / normalRealElapsed:F3}; Bullet Time={(Mathf.Abs(Mathf.DeltaAngle(phase, station.Rings[0].Phase)) / real):F3} degrees/real second\n");
        Assert.That(energyBefore - station.BulletTime.Energy, Is.EqualTo(real / 3f).Within(.02f));
        Assert.That(station.Interaction.ScreenEffectVisible, Is.True);
        var screen = station.Interaction.GetComponentInChildren<UnityEngine.UI.Image>(true);
        Assert.That(screen.material.shader.isSupported, Is.True);
        var dash = typeof(CharacterMovement2D).GetMethod("TryStartDash", BindingFlags.Instance | BindingFlags.NonPublic);
        var dashing = typeof(CharacterMovement2D).GetField("isDashing", BindingFlags.Instance | BindingFlags.NonPublic);
        float dashX = player.transform.position.x, dashReal = Time.unscaledTime;
        dash.Invoke(movement, null);
        Assert.That((bool)dashing.GetValue(movement), Is.True);
        while ((bool)dashing.GetValue(movement) && Time.unscaledTime - dashReal < 1f) yield return null;
        float dashRealDuration = Time.unscaledTime - dashReal;
        yield return new WaitForFixedUpdate();
        Assert.That(dashRealDuration, Is.InRange(.17f, .25f), "A .15 player-second dash lasts about .20 real seconds at 75%.");
        Assert.That(player.transform.position.x - dashX, Is.EqualTo(3f).Within(.16f));
        typeof(CharacterMovement2D).GetField("dashCooldownRemaining", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(movement, 0f);
        var wall = new GameObject("Bullet Time dash wall", typeof(BoxCollider2D));
        wall.transform.position = player.transform.position + Vector3.right * 1.2f;
        wall.GetComponent<BoxCollider2D>().size = new Vector2(.1f, 4f);
        Physics2D.SyncTransforms();
        float wallLeft = wall.GetComponent<BoxCollider2D>().bounds.min.x;
        dash.Invoke(movement, null);
        yield return new WaitForSecondsRealtime(.3f);
        Assert.That(player.transform.position.x, Is.LessThan(wallLeft), "Slowed dash must retain its collision casts.");
        Object.Destroy(wall);
        System.IO.File.AppendAllText("Artifacts/GeneratedQA/BulletTimeCleanup/movement.txt",
            $"Bullet Time dash={dashRealDuration:F3} real seconds; distance=3; wall collision=PASS\n");
        station.StopCoroutine(hold);
        station.enabled = true;
        movement.MovementIntent = () => Vector2.zero;
        yield return new WaitForSecondsRealtime(.25f);
        Assert.That(Time.timeScale, Is.EqualTo(1f));
        Assert.That(station.Interaction.ScreenEffectVisible, Is.False);
        Object.Destroy(projectile);
        Object.Destroy(physics);
        Object.Destroy(enemy);

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

    private static IEnumerator HoldRightMouse(OrbitalStationRuntime station, MethodInfo tick, bool held = true)
    {
        var tickStation = typeof(OrbitalStationRuntime).GetMethod("TickStation", BindingFlags.Instance | BindingFlags.NonPublic);
        while (true)
        {
            tick.Invoke(station, new object[] { Time.unscaledDeltaTime, held, false });
            tickStation.Invoke(station, new object[] { Time.deltaTime });
            yield return null;
        }
    }

    [UnityTest]
    public IEnumerator PlayerTimeRespectsPauseForeignOwnersDeathAndSceneCleanup()
    {
        EditorSceneManager.OpenScene("Assets/_Project/Scenes/MainBuild/MVP.unity");
        yield return new EnterPlayMode();
        yield return VerifyTimeOwnershipInPlayMode();
    }

    [UnityTest]
    public IEnumerator DashRealAndScaledDurationsWithAndWithoutBulletTime()
    {
        EditorSceneManager.OpenScene("Assets/_Project/Scenes/MainBuild/MVP.unity");
        yield return new EnterPlayMode();
        yield return MeasureDashClocksInPlayMode();
    }

    private static IEnumerator MeasureDashClocksInPlayMode()
    {
        const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        yield return CoreTestSupport.Await(() => ProductionSceneComposition.Active is { IsReady: true });
        Object.FindFirstObjectByType<EnemySpawner>().StopSpawning();
        var player = PlayerRuntimeReference.CachedPlayer;
        player.GetComponent<PlayerHealth>().AddMaxHealth(100000f);
        var body = player.GetComponent<Rigidbody2D>();
        var movement = player.GetComponent<CharacterMovement2D>();
        var station = player.GetComponentInChildren<OrbitalStationRuntime>();
        station.enabled = false;
        var tick = typeof(OrbitalStationRuntime).GetMethod("TickRightMouse", Private);
        var dash = typeof(CharacterMovement2D).GetMethod("TryStartDash", Private);
        var active = typeof(CharacterMovement2D).GetField("isDashing", Private);
        var cooldown = typeof(CharacterMovement2D).GetField("dashCooldownRemaining", Private);
        var input = typeof(CharacterMovement2D).GetField("moveInput", Private);
        var velocity = typeof(CharacterMovement2D).GetField("currentVelocity", Private);
        Vector2 origin = body.position;
        var realDash = new float[2];
        var scaledDash = new float[2];
        var walkSpeed = new float[2];
        for (int mode = 0; mode < 2; mode++)
        {
            tick.Invoke(station, new object[] { .3f, mode == 1, false });
            movement.MovementIntent = () => Vector2.right;
            velocity.SetValue(movement, Vector2.right * movement.speed);
            yield return new WaitForSecondsRealtime(.15f);
            float walkX = body.position.x, walkReal = Time.realtimeSinceStartup;
            yield return new WaitForSecondsRealtime(.4f);
            walkSpeed[mode] = (body.position.x - walkX) / (Time.realtimeSinceStartup - walkReal);
            movement.MovementIntent = () => Vector2.zero;
            for (int sample = 0; sample < 3; sample++)
            {
                body.position = origin;
                Physics2D.SyncTransforms();
                cooldown.SetValue(movement, 0f);
                input.SetValue(movement, Vector2.right);
                float realStart = Time.realtimeSinceStartup, scaledStart = Time.time;
                dash.Invoke(movement, null);
                Assert.That((bool)active.GetValue(movement), Is.True);
                while ((bool)active.GetValue(movement) && Time.realtimeSinceStartup - realStart < 1f)
                    yield return new WaitForFixedUpdate();
                realDash[mode] += Time.realtimeSinceStartup - realStart;
                scaledDash[mode] += Time.time - scaledStart;
                Assert.That((bool)active.GetValue(movement), Is.False);
                Assert.That(body.position.x - origin.x, Is.EqualTo(3f).Within(.04f));
                yield return null;
            }
            realDash[mode] /= 3f;
            scaledDash[mode] /= 3f;
        }
        Assert.That(walkSpeed[1] / walkSpeed[0], Is.EqualTo(.75f).Within(.07f));
        Assert.That(realDash[1], Is.GreaterThan(realDash[0] * 1.15f), "Bullet Time must lengthen the dash in real time.");
        Assert.That(realDash[0], Is.EqualTo(.15f).Within(.035f));
        Assert.That(realDash[1], Is.EqualTo(.20f).Within(.035f));
        var wall = new GameObject("Dash clock collision wall", typeof(BoxCollider2D));
        body.position = origin;
        wall.transform.position = (Vector3)origin + Vector3.right * 1.2f;
        wall.GetComponent<BoxCollider2D>().size = new Vector2(.1f, 4f);
        Physics2D.SyncTransforms();
        cooldown.SetValue(movement, 0f);
        input.SetValue(movement, Vector2.right);
        dash.Invoke(movement, null);
        yield return new WaitForSecondsRealtime(.3f);
        Assert.That(body.position.x, Is.LessThan(wall.GetComponent<BoxCollider2D>().bounds.min.x));
        Object.Destroy(wall);
        System.IO.Directory.CreateDirectory("Artifacts/GeneratedQA/DashClockCheck");
        System.IO.File.WriteAllText("Artifacts/GeneratedQA/DashClockCheck/measurements.txt",
            $"Three dashes per mode; distance=3; authored dashDuration=0.15 player seconds\n" +
            $"Normal: dash real={realDash[0]:F4}s; scaled={scaledDash[0]:F4}s; walking={walkSpeed[0]:F3} units/real second\n" +
            $"Bullet Time: dash real={realDash[1]:F4}s; scaled={scaledDash[1]:F4}s; walking={walkSpeed[1]:F3} units/real second\n" +
            $"World=0.4; player=0.75; modifier=1.875; dash wall collision=PASS\n");
        station.InputOwner.ReleaseBulletTime();
    }

    private static IEnumerator VerifyTimeOwnershipInPlayMode()
    {
        const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        yield return CoreTestSupport.Await(() => ProductionSceneComposition.Active is { IsReady: true });
        Object.FindFirstObjectByType<EnemySpawner>().StopSpawning();
        var station = PlayerRuntimeReference.CachedPlayer.GetComponentInChildren<OrbitalStationRuntime>();
        station.enabled = false;
        var movement = station.Owner.Transform.GetComponent<CharacterMovement2D>();
        var animator = movement.GetComponentInChildren<Animator>();
        float animatorSpeed = animator.speed, fixedStep = Time.fixedDeltaTime;
        var tick = typeof(OrbitalStationRuntime).GetMethod("TickRightMouse", Private);
        tick.Invoke(station, new object[] { .06f, true, true });
        Assert.That(Time.timeScale, Is.EqualTo(.7f).Within(.001f));
        tick.Invoke(station, new object[] { .06f, true, false });
        Assert.That(Time.timeScale, Is.EqualTo(.4f).Within(.001f));
        Assert.That(movement.BulletTimeControlMultiplier, Is.EqualTo(1.875f).Within(.001f));
        Assert.That(animator.speed, Is.EqualTo(animatorSpeed * 1.875f).Within(.001f));
        var pause = Object.FindFirstObjectByType<PauseMenuUI>();
        pause.Pause();
        float energy = station.BulletTime.Energy;
        tick.Invoke(station, new object[] { .5f, true, false });
        Assert.That(Time.timeScale, Is.Zero);
        Assert.That(station.BulletTime.Energy, Is.EqualTo(energy));
        Assert.That(movement.BulletTimeControlMultiplier, Is.EqualTo(1f));
        pause.Resume();
        tick.Invoke(station, new object[] { .12f, true, false });
        Assert.That(Time.timeScale, Is.EqualTo(.4f).Within(.001f));
        pause.Pause();
        tick.Invoke(station, new object[] { .5f, false, false });
        Assert.That(Time.timeScale, Is.Zero, "Releasing RMB must not release Pause.");
        pause.Resume();
        tick.Invoke(station, new object[] { .18f, false, false });
        Assert.That(Time.timeScale, Is.EqualTo(1f));
        tick.Invoke(station, new object[] { .12f, true, false });
        Assert.That(station.InputOwner.BeginReward(), Is.True);
        Time.timeScale = 0f; // The reward queue's independent pause.
        tick.Invoke(station, new object[] { .5f, true, false });
        Assert.That(Time.timeScale, Is.Zero);
        station.InputOwner.EndReward();
        Time.timeScale = 1f;
        tick.Invoke(station, new object[] { .12f, true, false });
        Time.timeScale = .05f; // A foreign hitstop/slowdown owner.
        Time.fixedDeltaTime = fixedStep * .3f;
        station.InputOwner.ReleaseBulletTime();
        Assert.That(Time.timeScale, Is.EqualTo(.05f));
        Assert.That(Time.fixedDeltaTime, Is.EqualTo(fixedStep * .3f).Within(.000001f));
        Assert.That(animator.speed, Is.EqualTo(animatorSpeed).Within(.001f));
        Time.timeScale = 1f;
        Time.fixedDeltaTime = fixedStep;
        tick.Invoke(station, new object[] { .12f, true, false });
        RunEndService.Instance.RestartRun(RunEndReason.ReturnedToBunker);
        yield return CoreTestSupport.Await(() => !SceneTransitionOverlay.IsTransitioning &&
            ProductionSceneComposition.Active is { IsReady: true });
        Assert.That(Time.timeScale, Is.EqualTo(1f));
        Assert.That(Time.fixedDeltaTime, Is.EqualTo(fixedStep).Within(.000001f));
        station = PlayerRuntimeReference.CachedPlayer.GetComponentInChildren<OrbitalStationRuntime>();
        station.enabled = false;
        tick.Invoke(station, new object[] { .12f, true, false });
        var health = station.Owner.Transform.GetComponent<PlayerHealth>();
        Assert.That(health.TakeDamage(health.MaxHealth * 10f, Vector2.zero), Is.True);
        Assert.That(health.IsDead, Is.True);
        tick.Invoke(station, new object[] { .2f, false, false });
        Assert.That(Time.timeScale, Is.Zero, "Death owns its pause.");
        SceneTransitionOverlay.Instance.RecoverToBunker();
        yield return CoreTestSupport.Await(() => SceneManager.GetActiveScene().name == "MainMenu" &&
            ProductionSceneComposition.Active is { IsReady: true } && !SceneTransitionOverlay.IsTransitioning);
        Assert.That(Time.timeScale, Is.EqualTo(1f));
        Assert.That(Time.fixedDeltaTime, Is.EqualTo(fixedStep).Within(.000001f));
    }
}
#endif
