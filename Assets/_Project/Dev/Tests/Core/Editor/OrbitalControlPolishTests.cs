#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Subject42.Combat.OrbitalStation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

public sealed class OrbitalControlPolishTests
{
    private const string Output = "Artifacts/GeneratedQA/OrbitalReach-20261026";
    private const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
    [SetUp] public void Preserve() => CoreTestSupport.PreservePreferences();
    [UnityTearDown] public IEnumerator Cleanup() => CoreTestSupport.CleanupPlayMode();

    [Test]
    public void PistolLeadIsBoundedAndOtherGunsKeepTheirAim()
    {
        var evaluate = typeof(OrbitalProjectileModule).GetMethod("EvaluateAimDirection", BindingFlags.Public | BindingFlags.Static);
        Assert.That(evaluate, Is.Not.Null, "Pistol needs a bounded initial aim correction; bullets must retain straight flight.");
        Vector2 Evaluate(Vector2 velocity, float lead = .35f, float degrees = 12f) =>
            (Vector2)evaluate.Invoke(null, new object[] { Vector2.zero, Vector2.right * 5f, velocity, 13f, lead, degrees });
        Assert.That(Evaluate(Vector2.zero), Is.EqualTo(Vector2.right));
        var corrected = Evaluate(Vector2.up * 4f);
        Assert.That(corrected.y, Is.GreaterThan(.05f));
        Assert.That(Vector2.Angle(Vector2.right, corrected), Is.LessThanOrEqualTo(12.001f));
        Assert.That(Vector2.Angle(Vector2.right, Evaluate(Vector2.up * 100f)), Is.LessThanOrEqualTo(12.001f));
        Assert.That(Evaluate(Vector2.up * 4f, 0f), Is.EqualTo(Vector2.right));
        Assert.That(Evaluate(Vector2.up * 4f, .35f, 0f), Is.EqualTo(Vector2.right));
        Assert.That(OrbitalWeaponSettings.Shotgun().AimLeadSeconds, Is.Zero);
        Assert.That(OrbitalWeaponSettings.Sniper().AimLeadSeconds, Is.Zero);
    }

    [UnityTest]
    public IEnumerator ProductionPointerRangeReturnsAndMatchesVisibleCircle()
    {
        typeof(BunkerStationPresentationTests).GetMethod("SetResolution", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, null);
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
        yield return new EnterPlayMode();
        yield return StartProductionRun();
        yield return CheckPointer();
    }

    [UnityTest]
    public IEnumerator ProductionPistolLeadAndDashFeedback()
    {
        typeof(BunkerStationPresentationTests).GetMethod("SetResolution", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, null);
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
        yield return new EnterPlayMode();
        yield return StartProductionRun();
        yield return CheckPistol();
        yield return CaptureDash();
    }

    private static IEnumerator StartProductionRun()
    {
        yield return CoreTestSupport.LoadBunker();
        RunSelectionManager.Instance.SelectCharacter(AssetDatabase.LoadAssetAtPath<CharacterData>("Assets/_Project/Data/Characters/01_Gera.asset"));
        var starter = Object.FindFirstObjectByType<BunkerRunStarter>();
        if (!GameplayRandom.IsActive) GameplayRandom.Begin(1709);
        starter.StartRun(starter.transform);
        yield return CoreTestSupport.Await(() => SceneManager.GetActiveScene().name == "MVP" &&
            Object.FindFirstObjectByType<OrbitalStationRuntime>() is { IsInitialized: true } && !SceneTransitionOverlay.IsTransitioning);
        PlayerRuntimeReference.CachedPlayer.GetComponent<PlayerHealth>().AddMaxHealth(1000000f);
        Directory.CreateDirectory(Output);
    }

    private static IEnumerator CheckPistol()
    {
        var player = PlayerRuntimeReference.CachedPlayer;
        var station = player.GetComponentInChildren<OrbitalStationRuntime>();
        var spawner = Object.FindFirstObjectByType<EnemySpawner>();
        station.enabled = false; spawner.enabled = false;
        foreach (var enemy in EnemyHealth.ActiveInstances.ToArray()) Object.Destroy(enemy.gameObject);
        yield return null;
        var module = (OrbitalProjectileModule)station.Modules.First(m => m.Kind == OrbitalModuleKind.Pistol);
        var adapter = (ProductionOrbitalCombatAdapter)station.Combat;
        var enemyObject = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/prefabs/Enemies/p_Enemy_classic.prefab"));
        foreach (var ai in enemyObject.GetComponents<EnemyMovement>()) ai.enabled = false;
        var body = enemyObject.GetComponent<Rigidbody2D>();
        body.bodyType = RigidbodyType2D.Kinematic;
        body.linearVelocity = Vector2.zero;
        var health = enemyObject.GetComponent<EnemyHealth>();
        yield return null;
        float authoredLead = module.Settings.AimLeadSeconds;
        var audio = AudioService.Instance;
        Assert.That(audio, Is.Not.Null);
        var pistolClips = new[]
        {
            AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/_Project/AudioEffects/SFX/Game/Pistol_shot_SFX.wav"),
            AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/_Project/AudioEffects/SFX/Game/Pistol_shot_SFX_Alt.wav")
        };
        Assert.That(pistolClips.All(c => c != null), Is.True, "Both accepted pistol sounds must import.");
        int audioShots = 0;
        void OnCue(AudioCueId cue) { if (cue == AudioCueId.PistolShot) audioShots++; }
        audio.DebugCuePlayed += OnCue;
        float[] damage = new float[3];
        var report = new System.Collections.Generic.List<string> { "Controlled production module/adapter: transverse normal enemy2m/s and fast enemy4m/s at4m, .02s simulation steps; one shot; actual enemy prefab." };
        try
        {
            for (int mode = 0; mode < 3; mode++)
            {
                adapter.DebugClearProjectiles(); module.ResetFiring();
                module.Settings.AimLeadSeconds = mode == 0 ? 0f : authoredLead;
                var cooldown = typeof(OrbitalModuleRuntime).GetField("Cooldown", Hidden);
                cooldown.SetValue(module, 10f);
                Vector2 origin = module.WorldPosition;
                float targetStep = mode == 2 ? .08f : .04f;
                for (int frame = 0; frame <= 20; frame++)
                {
                    enemyObject.transform.position = origin + Vector2.right * 4f + Vector2.up * ((frame - 20) * targetStep);
                    Physics2D.SyncTransforms(); module.Tick(.02f);
                    yield return null;
                }
                health.SetRuntimeMaxHealth(1000f);
                cooldown.SetValue(module, 0f);
                int shots = module.ShotsFired;
                int heardShots = audioShots;
                enemyObject.transform.position = origin + Vector2.right * 4f + Vector2.up * targetStep;
                Physics2D.SyncTransforms();
                module.Tick(.02f);
                Assert.That(module.ShotsFired, Is.EqualTo(shots + 1));
                Assert.That(audioShots, Is.EqualTo(heardShots + 1), "The real pistol firing path must play its authored audio cue.");
                var source = audio.GetComponentsInChildren<AudioSource>()
                    .FirstOrDefault(s => pistolClips.Contains(s.clip) && s.isPlaying);
                Assert.That(source, Is.Not.Null, "Pistol audio must reach a playing pooled AudioSource.");
                report.Add($"audio={source.clip.name}, length={source.clip.length:F3}, pitch={source.pitch:F3}, volume={source.volume:F3}");
                report.Add("estimated velocity=" + typeof(OrbitalProjectileModule).GetField("estimatedTargetVelocity", Hidden).GetValue(module));
                for (int frame = 1; frame <= 35; frame++)
                {
                    enemyObject.transform.position = origin + Vector2.right * 4f + Vector2.up * ((frame + 1) * targetStep);
                    Physics2D.SyncTransforms(); adapter.Tick(.02f);
                    if (frame == 10) ScreenCapture.CaptureScreenshot(Output + (mode == 0 ? "/pistol-before.png" : mode == 1 ? "/pistol-after.png" : "/pistol-fast-target.png"));
                    yield return null;
                }
                damage[mode] = 1000f - health.CurrentHealth;
                report.Add($"lead={module.Settings.AimLeadSeconds:F2}, shots=1, damage={damage[mode]:F3}");
            }
            Assert.That(damage[0], Is.Zero, "Baseline transverse-miss reproduction changed.");
            Assert.That(damage[1], Is.GreaterThan(0f), "Bounded initial lead must recover the demonstrated transverse miss.");
            Assert.That(damage[2], Is.Zero, "Fast lateral escape must still defeat bounded lead; this is not guaranteed auto-aim.");
        }
        finally
        {
            audio.DebugCuePlayed -= OnCue;
            module.Settings.AimLeadSeconds = authoredLead;
            adapter.DebugClearProjectiles();
            Object.Destroy(enemyObject);
            station.enabled = true; spawner.enabled = true;
            File.WriteAllLines(Output + "/pistol.txt", report);
        }
    }

    private static IEnumerator CaptureDash()
    {
        var movement = PlayerRuntimeReference.CachedPlayer.GetComponent<CharacterMovement2D>();
        movement.MovementIntent = () => Vector2.right;
        float until = Time.time + .25f;
        while (Time.time < until) yield return null;
        var effect = AssetDatabase.LoadAssetAtPath<ParticleSystem>("Assets/_Project/prefabs/fx/PlayerDashBurst.prefab");
        Assert.That(effect, Is.Not.Null);
        typeof(CharacterMovement2D).GetMethod("TryStartDash", Hidden).Invoke(movement, null);
        yield return null;
        Assert.That(Object.FindObjectsByType<ParticleSystem>(FindObjectsSortMode.None)
            .Any(p => p.name == effect.name + "(Clone)" && p.particleCount > 0), Is.True, "Accepted dash must emit visible particles in the production scene.");
        ScreenCapture.CaptureScreenshot(Output + "/dash-start.png");
        until = Time.time + .06f;
        while (Time.time < until) yield return null;
        ScreenCapture.CaptureScreenshot(Output + "/dash-mid.png");
        until = Time.time + .15f;
        while (Time.time < until) yield return null;
        movement.MovementIntent = null;
    }

    private static IEnumerator CheckPointer()
    {
        Directory.CreateDirectory(Output);
        var player = PlayerRuntimeReference.CachedPlayer;
        var movement = player.GetComponent<CharacterMovement2D>();
        var station = player.GetComponentInChildren<OrbitalStationRuntime>();
        var shift = station.GetComponentInChildren<OrbitalCenterShift>();
        var range = station.GetComponentInChildren<OrbitalCenterRangeView>();
        var line = (LineRenderer)new SerializedObject(range).FindProperty("line").objectReferenceValue;
        var follow = Object.FindFirstObjectByType<CameraFollow>();
        var advance = typeof(OrbitalCenterShift).GetMethod("AdvancePointer", Hidden);
        var data = new SerializedObject(shift);
        Assert.That(shift.MaximumOffset, Is.EqualTo(4f));
        Assert.That(data.FindProperty("moveSpeed").floatValue, Is.EqualTo(6f));
        Assert.That(data.FindProperty("returnSpeed").floatValue, Is.EqualTo(8f));
        Assert.That(Application.isFocused, Is.True, "The actual pointer owner requires focused Unity; do not bypass its production guard.");
        player.GetComponent<PlayerHealth>().AddMaxHealth(1000000f);
        var spawner = Object.FindFirstObjectByType<EnemySpawner>();
        bool stationEnabled = station.enabled, spawnerEnabled = spawner.enabled;
        var filter = movement.DirectionalInputFilter;
        station.enabled = false; spawner.enabled = false;
        // Exercise the real pointer method once per frame without OS polling releasing the synthetic hold.
        movement.DirectionalInputFilter = null;
        var log = new System.Collections.Generic.List<string>
        {
            "Project=" + Path.GetFullPath("."), "Scene=" + SceneManager.GetActiveScene().path,
            "Unity=" + Application.unityVersion + " PID=" + System.Diagnostics.Process.GetCurrentProcess().Id,
            "Programmatic pointer calls; physical RMB not claimed."
        };
        try
        {
            foreach (float radius in new[] { 2f, 4f })
            {
                shift.ResetOffset(); shift.Configure(radius, 6f, 8f);
                Vector2 target = (Vector2)shift.RangeCenter + Vector2.right * 10f;
                bool down = true;
                float deadline = Time.realtimeSinceStartup + 1.2f;
                while (Time.realtimeSinceStartup < deadline)
                {
                    Assert.That(shift.CanControl, Is.True,
                        $"Pointer scenario interrupted: focused={Application.isFocused}, owner={station.Owner.CanControl}, " +
                        $"idle={station.InputOwner.IsIdle}, blocked={station.InputOwner.IsGameplayInputBlocked}, " +
                        $"rewardsIdle={station.InputOwner.IsRewardQueueIdle}, timeScale={Time.timeScale}");
                    advance.Invoke(shift, new object[] { down, true, false, target, Time.deltaTime });
                    down = false;
                    yield return null;
                }
                Assert.That(shift.Offset.magnitude, Is.EqualTo(radius).Within(.001f));
                Assert.That(line.enabled, Is.True);
                Assert.That(Vector2.Distance(line.GetPosition(0), shift.RangeCenter), Is.EqualTo(radius).Within(.001f));
                Assert.That(movement.WalkingSpeed, Is.EqualTo(movement.speed * .7f).Within(.001f));
                Vector3 viewport = follow.ControlledCamera.WorldToViewportPoint(station.transform.position);
                Assert.That(viewport.x, Is.InRange(0f, 1f));
                Assert.That(viewport.y, Is.InRange(0f, 1f));
                ScreenCapture.CaptureScreenshot(Output + "/range-" + radius + ".png");
                log.Add($"range={radius} actual={shift.Offset.magnitude:F3} indicator={Vector2.Distance(line.GetPosition(0), shift.RangeCenter):F3} speed={movement.WalkingSpeed:F3} viewport={viewport}");
                yield return null;
                deadline = Time.realtimeSinceStartup + .8f;
                while (Time.realtimeSinceStartup < deadline)
                {
                    advance.Invoke(shift, new object[] { false, false, false, target, Time.deltaTime });
                    yield return null;
                }
                Assert.That(shift.Offset, Is.EqualTo(Vector2.zero));
                Assert.That(movement.WalkingSpeed, Is.EqualTo(movement.speed));
            }
            advance.Invoke(shift, new object[] { true, true, false, (Vector2)shift.RangeCenter + Vector2.one * 10f, 1f });
            Assert.That(shift.Offset.magnitude, Is.EqualTo(4f).Within(.001f));
            Assert.That(shift.Offset.x, Is.EqualTo(shift.Offset.y).Within(.001f));
            Time.timeScale = 0f;
            advance.Invoke(shift, new object[] { false, true, false, Vector2.zero, 0f });
            Assert.That(shift.IsPointerSteering, Is.False);
            Assert.That(movement.WalkingSpeed, Is.EqualTo(movement.speed));
            Time.timeScale = 1f;
            shift.enabled = false;
            Assert.That(shift.Offset, Is.EqualTo(Vector2.zero));
            shift.enabled = true;
        }
        finally
        {
            Time.timeScale = 1f;
            shift.Configure(4f, 6f, 8f); shift.ResetOffset();
            movement.DirectionalInputFilter = filter;
            station.enabled = stationEnabled; spawner.enabled = spawnerEnabled;
            File.WriteAllLines(Output + "/pointer.txt", log);
        }
    }
}
#endif
