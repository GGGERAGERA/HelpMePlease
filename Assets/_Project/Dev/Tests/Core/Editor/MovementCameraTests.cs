#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using NUnit.Framework;
using Subject42.Combat.OrbitalStation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

public sealed class MovementCameraTests
{
    private const string Output = "Artifacts/GeneratedQA/MovementCamera";
    private const string ScenePath = "Assets/_Project/Scenes/MainBuild/MVP.unity";

    [SetUp] public void Preserve() => CoreTestSupport.PreservePreferences();
    [UnityTearDown] public IEnumerator Cleanup() => CoreTestSupport.CleanupPlayMode();

    [Test]
    public void ProductionRunCameraHasNoAdditionalFollowDelay()
    {
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
        try
        {
            CameraFollow follow = null;
            foreach (var root in scene.GetRootGameObjects())
                if (root.GetComponentInChildren<CameraFollow>(true) is { } candidate) follow = candidate;
            Assert.That(follow, Is.Not.Null);
            Assert.That(follow.smoothSpeed, Is.EqualTo(1f),
                "The run camera must follow the interpolated player without another damping stage.");
        }
        finally { EditorSceneManager.CloseScene(scene, true); }
    }

    [UnityTest]
    public IEnumerator ProductionMovementAuditAt30_60_120Frames()
    {
        typeof(BunkerStationPresentationTests).GetMethod("SetResolution", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static).Invoke(null, null);
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
        yield return new EnterPlayMode();
        yield return CoreTestSupport.LoadBunker();
        var character = AssetDatabase.LoadAssetAtPath<CharacterData>("Assets/_Project/Data/Characters/01_Gera.asset");
        RunSelectionManager.Instance.SelectCharacter(character);
        var starter = Object.FindFirstObjectByType<BunkerRunStarter>();
        if (!GameplayRandom.IsActive) GameplayRandom.Begin(1709);
        starter.StartRun(starter.transform);
        yield return CoreTestSupport.Await(() => SceneManager.GetActiveScene().name == "MVP" &&
            Object.FindFirstObjectByType<OrbitalStationRuntime>() is { IsInitialized: true } &&
            !SceneTransitionOverlay.IsTransitioning);
        PlayerRuntimeReference.CachedPlayer.GetComponent<PlayerHealth>().AddMaxHealth(1000000f);
        yield return AuditRun();
    }

    private static IEnumerator AuditRun()
    {
        Directory.CreateDirectory(Output);
        var player = PlayerRuntimeReference.CachedPlayer;
        var movement = player.GetComponent<CharacterMovement2D>();
        var body = player.GetComponent<Rigidbody2D>();
        var animator = player.GetComponentInChildren<Animator>();
        var station = player.GetComponentInChildren<OrbitalStationRuntime>();
        var steering = station.GetComponentInChildren<OrbitalCenterShift>();
        var follow = Object.FindFirstObjectByType<CameraFollow>();
        var camera = follow.ControlledCamera;
        var spawner = Object.FindFirstObjectByType<EnemySpawner>();
        var start = body.position;
        var report = new List<string>();
        string phase = follow.smoothSpeed < 1f ? "before" : "after";
        string folder = Output + "/" + phase;
        Directory.CreateDirectory(folder);
        report.Add("Project=" + Path.GetFullPath("."));
        report.Add("Scene=" + SceneManager.GetActiveScene().path);
        report.Add("Unity=" + Application.unityVersion + " PID=" + System.Diagnostics.Process.GetCurrentProcess().Id);
        report.Add("Input=programmatic MovementIntent; physical WASD/Shift/RMB not covered");
        report.Add($"Speed={movement.speed} Acceleration={movement.DebugAcceleration} Deceleration={movement.DebugDeceleration}");
        report.Add($"Interpolation={body.interpolation} FixedStep={Time.fixedDeltaTime} Follow={follow.smoothSpeed}");
        foreach (var file in new[] { "scripts/Combat/Player/CharacterMovement2D.cs", "scripts/UI/HUD/CameraFollow.cs", "Scenes/MainBuild/MVP.unity" })
        {
            using var sha = System.Security.Cryptography.SHA256.Create();
            report.Add(file + " SHA256=" + BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes("Assets/_Project/" + file))).Replace("-", ""));
        }
        File.WriteAllLines(folder + "/identity.txt", report);
        var trace = new List<string> { "scenario,target_fps,frame,unscaled_dt,time_scale,physics_x,physics_y,render_x,render_y,rb_velocity_x,rb_velocity_y,camera_x,camera_y,camera_error,animation_speed" };
        string currentScenario = "startup";
        int currentFps = 60;
        float maxFollowError = 0f;
        float walkingAnimation = 1f;
        RenderPipelineManager.endCameraRendering += RecordFrame;
        var intent = Vector2.zero;
        movement.MovementIntent = () => intent;
        int savedFps = Application.targetFrameRate, savedVsync = QualitySettings.vSyncCount;
        float fixedStep = Time.fixedDeltaTime;
        bool savedStation = station.enabled, savedSpawner = spawner.enabled, savedSteering = steering.enabled;
        // Controlled comparisons retain the production player, physics, scene and camera.
        // Stop new enemies and station input sampling; explicitly drive its existing time owner.
        spawner.enabled = false;
        station.enabled = false;
        steering.enabled = false;
        QualitySettings.vSyncCount = 0;
        GameObject wall = null;
        try
        {
            foreach (int fps in new[] { 30, 60, 120 })
            {
                Application.targetFrameRate = fps;
                intent = Vector2.zero;
                yield return Sample("settle", .5f, fps);
                body.position = start;
                body.linearVelocity = Vector2.zero;
                Physics2D.SyncTransforms();
                yield return Sample("settle", .4f, fps);
                Vector2 origin = body.position;
                intent = Vector2.right;
                yield return Sample("start-right", .5f, fps);
                yield return CheckVelocity(Vector2.right * movement.WalkingSpeed, "steady-right", fps);
                walkingAnimation = Mathf.Min(walkingAnimation, animator.GetFloat("Speed"));
                report.Add($"{fps}: walking animation Speed={animator.GetFloat("Speed"):F4}, Rigidbody velocity={body.linearVelocity}");
                report.Add($"{fps}: right displacement={(body.position - origin).magnitude:F4}");
                ScreenCapture.CaptureScreenshot(folder + "/" + fps + "-right.png");
                yield return Sample("steady-right", .11f, fps);
                ScreenCapture.CaptureScreenshot(folder + "/" + fps + "-right-next.png");
                intent = Vector2.left;
                yield return Sample("reverse-left", .35f, fps);
                yield return CheckVelocity(Vector2.left * movement.WalkingSpeed, "steady-left", fps);
                ScreenCapture.CaptureScreenshot(folder + "/" + fps + "-left.png");
                for (int i = 0; i < 8; i++)
                {
                    intent = i % 2 == 0 ? Vector2.right : Vector2.left;
                    yield return Sample("alternate", .075f, fps);
                }
                intent = Vector2.one.normalized;
                yield return Sample("diagonal", .4f, fps);
                yield return CheckVelocity(Vector2.one.normalized * movement.WalkingSpeed, "steady-diagonal", fps);
                intent = Vector2.zero;
                yield return Sample("release", .4f, fps);
                Vector2 stopped = body.position;
                yield return Sample("idle", .25f, fps);
                Assert.That(Vector2.Distance(stopped, body.position), Is.LessThan(.005f), "Released movement drifted after the authored deceleration.");
                Assert.That(animator.GetFloat("Speed"), Is.LessThan(.01f), "Stopping must restore idle animation.");
                report.Add($"{fps}: stopped drift={Vector2.Distance(stopped, body.position):F6}");
            }

            Application.targetFrameRate = 60;
            intent = Vector2.zero;
            yield return Sample("settle", .3f, 60);
            body.position = start;
            body.linearVelocity = Vector2.zero;
            wall = new GameObject("Movement regression wall", typeof(BoxCollider2D));
            wall.transform.position = start + Vector2.right * 1.2f + Vector2.up * 2f;
            wall.GetComponent<BoxCollider2D>().size = new Vector2(.3f, 16f);
            Physics2D.SyncTransforms();
            intent = Vector2.right;
            yield return Sample("wall-contact", .5f, 60);
            Assert.That(animator.GetFloat("Speed"), Is.LessThan(.01f), "Holding a wall must not animate free walking.");
            float contactX = body.position.x;
            intent = Vector2.one.normalized;
            Vector2 slideStart = body.position;
            yield return Sample("wall-slide", .5f, 60);
            report.Add($"Wall slide dx={body.position.x - contactX:F5} dy={body.position.y - slideStart.y:F5}");
            Assert.That(body.position.x - contactX, Is.LessThan(.04f), "Passed through the regression wall.");
            Assert.That(body.position.y - slideStart.y, Is.GreaterThan(movement.WalkingSpeed * .1f), "Stuck instead of sliding along the wall.");
            intent = Vector2.left;
            yield return Sample("wall-leave", .4f, 60);
            Assert.That(body.position.x, Is.LessThan(contactX - .25f), "Could not leave the wall.");
            Object.Destroy(wall); wall = null;

            intent = Vector2.right;
            station.BulletTime.Reset();
            yield return Sample("bullet-time", .8f, 60, true);
            Assert.That(Time.timeScale, Is.EqualTo(station.BulletTime.WorldTimeScale).Within(.01f));
            Assert.That(movement.BulletTimeControlMultiplier * Time.timeScale,
                Is.EqualTo(station.BulletTime.PlayerEffectiveTimeScale).Within(.01f));
            ScreenCapture.CaptureScreenshot(folder + "/bullet-time.png");
            station.InputOwner.ReleaseBulletTime();
            Assert.That(Time.fixedDeltaTime, Is.EqualTo(fixedStep).Within(.000001f));
            // Exercise the existing source-scoped .7 steering penalty, without claiming physical RMB input.
            Assert.That(steering, Is.Not.Null);
            movement.SetAnomalySpeedMultiplier(steering, .7f);
            yield return Sample("steering-speed", .4f, 60);
            yield return CheckVelocity(Vector2.right * movement.speed * .7f, "steady-steering", 60);
            movement.RemoveAnomalySpeedMultiplier(steering);
            yield return Sample("steering-release", .3f, 60);
            yield return CheckVelocity(Vector2.right * movement.speed, "steady-steering-release", 60);
            intent = Vector2.zero;
            yield return Sample("final-stop", .4f, 60);
            report.Add("Assertions completed; actual cadence and follow errors are in frames.csv.");
            Assert.That(trace.Count, Is.GreaterThan(10), "The production camera must actually render the measured frames.");
            Assert.That(maxFollowError < .001f && walkingAnimation > .8f, Is.True,
                $"Camera must follow the interpolated player (max error={maxFollowError:F5}); walking must drive Animator (Speed={walkingAnimation:F4}).");
        }
        finally
        {
            RenderPipelineManager.endCameraRendering -= RecordFrame;
            movement.MovementIntent = null;
            movement.RemoveAnomalySpeedMultiplier(steering);
            station.InputOwner.ReleaseBulletTime();
            station.enabled = savedStation;
            steering.enabled = savedSteering;
            spawner.enabled = savedSpawner;
            if (wall != null) Object.Destroy(wall);
            Application.targetFrameRate = savedFps;
            QualitySettings.vSyncCount = savedVsync;
            File.WriteAllLines(folder + "/report.txt", report);
            File.WriteAllLines(folder + "/frames.csv", trace);
        }

        IEnumerator Sample(string scenario, float seconds, int fps, bool bullet = false)
        {
            currentScenario = scenario;
            currentFps = fps;
            float until = Time.realtimeSinceStartup + seconds;
            while (Time.realtimeSinceStartup < until)
            {
                if (bullet) station.InputOwner.TickBulletTime(true, station.BulletTime, Time.unscaledDeltaTime);
                yield return null;
            }
        }

        IEnumerator CheckVelocity(Vector2 expected, string scenario, int fps)
        {
            Vector2 origin = body.position;
            float physicsTime = Time.fixedTime;
            yield return Sample(scenario, .25f, fps);
            Vector2 actual = (body.position - origin) / (Time.fixedTime - physicsTime);
            report.Add($"{fps}: {scenario} actual={actual} expected={expected}");
            Assert.That(Vector2.Distance(actual, expected), Is.LessThan(.15f), "Completed physics displacement has the wrong speed or direction.");
        }

        void RecordFrame(ScriptableRenderContext context, Camera rendered)
        {
            if (rendered != camera) return;
            var desired = player.transform.position + follow.offset;
            if (currentScenario != "settle" && currentScenario != "startup")
                maxFollowError = Mathf.Max(maxFollowError, Vector2.Distance(follow.transform.position, desired));
            trace.Add(string.Format(CultureInfo.InvariantCulture,
                    "{0},{1},{2},{3:F6},{4:F4},{5:F6},{6:F6},{7:F6},{8:F6},{9:F6},{10:F6},{11:F6},{12:F6},{13:F6},{14:F4}",
                    currentScenario, currentFps, Time.frameCount, Time.unscaledDeltaTime, Time.timeScale,
                    body.position.x, body.position.y, player.transform.position.x, player.transform.position.y,
                    body.linearVelocity.x, body.linearVelocity.y, follow.transform.position.x, follow.transform.position.y,
                    Vector2.Distance(follow.transform.position, desired), animator.GetFloat("Speed")));
        }
    }
}
#endif
