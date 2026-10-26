#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
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

public sealed class CombatAudioFeedbackTests
{
    private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
    private const string Fx = "Assets/_Project/prefabs/fx/";
    [SetUp] public void Setup() => CoreTestSupport.PreservePreferences();
    [UnityTearDown] public IEnumerator Cleanup() => CoreTestSupport.CleanupPlayMode();

    [UnityTest]
    public IEnumerator AcceptedDashSoundsOnceAndCooldownAndWalkingStaySilent()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
        yield return new EnterPlayMode();
        yield return BeginProductionRun();
        yield return VerifyDash();
    }

    private static IEnumerator VerifyDash()
    {
        var player = PlayerRuntimeReference.CachedPlayer;
        var movement = player.GetComponent<CharacterMovement2D>();
        var presentation = player.GetComponent<PlayerFeedbackPresentation>();
        var played = new List<AudioCueId>();
        var service = AudioService.Instance;
        service.DebugCuePlayed += played.Add;
        try
        {
            movement.MovementIntent = () => Vector2.right;
            yield return WaitRealtime(.15f);
            Assert.That(Count(played, AudioCueId.PlayerDash), Is.Zero, "Walking is not a dash.");
            Invoke(movement, "TryStartDash");
            Assert.That(movement.IsDashReady, Is.False, "Use the real accepted dash path.");
            Assert.That(Count(played, AudioCueId.PlayerDash), Is.EqualTo(1));
            AssertPlaying(AudioCueId.PlayerDash);
            yield return null;
            yield return null;
            string screenshot = System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath,
                "../Artifacts/GeneratedQA/AudioPack-20261026/dash-gameview.png"));
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(screenshot));
            if (System.IO.File.Exists(screenshot)) System.IO.File.Delete(screenshot);
            ScreenCapture.CaptureScreenshot(screenshot);
            yield return CoreTestSupport.Await(() => System.IO.File.Exists(screenshot));
            yield return WaitRealtime(.25f);
            Invoke(movement, "TryStartDash");
            Assert.That(Count(played, AudioCueId.PlayerDash), Is.EqualTo(1), "Rejected cooldown input must be silent.");
            presentation.enabled = false;
            movement.enabled = false;
            movement.enabled = true;
            presentation.enabled = true;
            movement.MovementIntent = () => Vector2.left;
            yield return null;
            Invoke(movement, "TryStartDash");
            Assert.That(Count(played, AudioCueId.PlayerDash), Is.EqualTo(2), "Re-enable must restore exactly one subscription.");
        }
        finally { service.DebugCuePlayed -= played.Add; }
    }

    [UnityTest]
    public IEnumerator FatalPlayerDamagePlaysDeathOnceWithoutHurtAndStartsAudioSource()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
        yield return new EnterPlayMode();
        yield return BeginProductionRun();
        yield return VerifyPlayerDeath();
    }

    private static IEnumerator VerifyPlayerDeath()
    {
        var health = PlayerRuntimeReference.CachedPlayer.GetComponent<PlayerHealth>();
        var service = AudioService.Instance;
        var catalog = (AudioCatalog)new SerializedObject(service).FindProperty("catalog").objectReferenceValue;
        Assert.That(catalog.TryGet(AudioCueId.PlayerDeath, out var death), Is.True);
        Assert.That(death.TryGetRandomClip(out var deathClip), Is.True, "The authored death cue needs a real clip.");
        // A prior spawn hit must finish the existing invulnerability interval.
        yield return WaitRealtime(.7f);
        health.SetRuntimeHealth(100f, 1f);
        health.SetIncomingDamageMultiplier(1f);
        var played = new List<AudioCueId>();
        service.DebugCuePlayed += played.Add;
        try
        {
            Assert.That(health.TakeDamage(100f, Vector2.right), Is.True, "Apply fatal damage through the real health owner.");
            Assert.That(health.IsDead, Is.True);
            Assert.That(Count(played, AudioCueId.PlayerDeath), Is.EqualTo(1));
            Assert.That(Count(played, AudioCueId.PlayerHurt), Is.Zero, "A fatal hit must not layer the hurt sound over death.");
            Assert.That(service.GetComponentsInChildren<AudioSource>().Any(source => source.clip == deathClip &&
                source.isPlaying && source.isActiveAndEnabled && source.volume > 0f), Is.True,
                "The accepted death cue must start a live AudioSource, including while the death UI pauses gameplay.");
            Assert.That(health.TakeDamage(100f, Vector2.left), Is.False, "Dead players reject subsequent damage.");
            yield return null;
            Assert.That(Count(played, AudioCueId.PlayerDeath), Is.EqualTo(1));
            Assert.That(Count(played, AudioCueId.PlayerHurt), Is.Zero);
        }
        finally { service.DebugCuePlayed -= played.Add; }
    }

    [UnityTest]
    public IEnumerator BulletTimeSoundsOnlyOnAcceptedEdgesAndNotAdministrativeRelease()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
        yield return new EnterPlayMode();
        yield return BeginProductionRun();
        yield return VerifyBulletTime();
    }

    private static IEnumerator VerifyBulletTime()
    {
        var station = PlayerRuntimeReference.CachedPlayer.GetComponentInChildren<OrbitalStationRuntime>();
        station.enabled = false; // Drive the existing production input step deterministically.
        station.BulletTime.Reset();
        var played = new List<AudioCueId>();
        var service = AudioService.Instance;
        service.DebugCuePlayed += played.Add;
        try
        {
            TickBulletTime(station, .12f, true);
            Assert.That(station.BulletTime.IsActive, Is.True);
            Assert.That(Count(played, AudioCueId.BulletTimeEnter), Is.EqualTo(1));
            AssertPlaying(AudioCueId.BulletTimeEnter);
            for (int i = 0; i < 4; i++)
            {
                yield return WaitRealtime(.12f);
                TickBulletTime(station, .12f, true);
            }
            Assert.That(Count(played, AudioCueId.BulletTimeEnter), Is.EqualTo(1), "Held frames must not replay enter.");
            TickBulletTime(station, .2f, false);
            Assert.That(station.BulletTime.IsActive, Is.False);
            Assert.That(Count(played, AudioCueId.BulletTimeExit), Is.EqualTo(1));
            AssertPlaying(AudioCueId.BulletTimeExit);
            TickBulletTime(station, .2f, false);
            Assert.That(Count(played, AudioCueId.BulletTimeExit), Is.EqualTo(1), "Idle frames must not replay exit.");
            yield return WaitRealtime(.25f);
            station.BulletTime.Reset();
            TickBulletTime(station, .12f, true);
            float previousTick = Time.realtimeSinceStartup;
            while (station.BulletTime.IsActive)
            {
                yield return null;
                float now = Time.realtimeSinceStartup;
                TickBulletTime(station, now - previousTick, true);
                previousTick = now;
            }
            Assert.That(station.BulletTime.Energy, Is.Zero);
            Assert.That(Count(played, AudioCueId.BulletTimeExit), Is.EqualTo(2), "Natural energy depletion is a real exit.");
            AssertPlaying(AudioCueId.BulletTimeExit);
            TickBulletTime(station, .2f, false); // Finish the normal exit blend.
            yield return WaitRealtime(.12f);
            station.BulletTime.Reset();
            TickBulletTime(station, .12f, true);
            yield return WaitRealtime(.12f);
            TickBulletTime(station, .12f, false);
            Assert.That(Count(played, AudioCueId.BulletTimeExit), Is.EqualTo(3),
                "A valid release must sound while the prior exit's short tail is still playing.");
            yield return WaitRealtime(.12f);
            TickBulletTime(station, .12f, true);
            Assert.That(Count(played, AudioCueId.BulletTimeEnter), Is.EqualTo(4),
                "A valid re-entry must sound while the prior enter's short tail is still playing.");
            AssertPlaying(AudioCueId.BulletTimeEnter);
            int exits = Count(played, AudioCueId.BulletTimeExit);
            station.InputOwner.PrepareForExternalPause();
            Time.timeScale = 0f;
            TickBulletTime(station, .5f, false);
            Assert.That(Count(played, AudioCueId.BulletTimeExit), Is.EqualTo(exits), "Pause cleanup must not synthesize a gameplay exit.");
            int enters = Count(played, AudioCueId.BulletTimeEnter);
            TickBulletTime(station, .5f, true);
            Assert.That(Count(played, AudioCueId.BulletTimeEnter), Is.EqualTo(enters), "Paused input cannot enter.");
            Time.timeScale = 1f;
            station.InputOwner.ReleaseBulletTime();
            station.BulletTime.Reset();
            station.enabled = true;
            station.enabled = false;
            Assert.That(Count(played, AudioCueId.BulletTimeExit), Is.EqualTo(exits), "Disable/reset is administrative cleanup.");
        }
        finally { service.DebugCuePlayed -= played.Add; Time.timeScale = 1f; }
    }

    [UnityTest]
    public IEnumerator RocketAndBomberUseDistinctExplosionsAndCanceledRocketNeverImpacts()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
        yield return new EnterPlayMode();
        yield return BeginProductionRun();
        yield return VerifyExplosions();
    }

    private static IEnumerator VerifyExplosions()
    {
        var player = PlayerRuntimeReference.CachedPlayer;
        Vector3 target = player.transform.position + Vector3.one * 40f;
        var played = new List<AudioCueId>();
        var service = AudioService.Instance;
        service.DebugCuePlayed += played.Add;
        var owner = new GameObject("Combat audio rocket fixture").AddComponent<WorldHazardDirector>();
        var explosion = AssetDatabase.LoadAssetAtPath<GameObject>(Fx + "fx_BomberExplosion.prefab").GetComponent<ParticleSystem>();
        using var runner = new RocketAttackRunner(owner,
            AssetDatabase.LoadAssetAtPath<GameObject>(Fx + "p_fxRocket1.prefab"),
            AssetDatabase.LoadAssetAtPath<GameObject>(Fx + "fx_BossTaget1.prefab"), explosion);
        try
        {
            Assert.That(runner.Launch(target, null, .8f, .4f, 2f), Is.True);
            Assert.That(Count(played, AudioCueId.RocketWarning), Is.EqualTo(1), "Warning belongs to accepted marker creation.");
            AssertPlaying(AudioCueId.RocketWarning);
            runner.Tick(.5f, 14f, 2f, 25, () => true);
            Assert.That(Count(played, AudioCueId.RocketExplosion), Is.Zero);
            runner.Tick(.8f, 14f, 2f, 25, () => true);
            Assert.That(Count(played, AudioCueId.RocketExplosion), Is.EqualTo(1));
            AssertPlaying(AudioCueId.RocketExplosion);
            Assert.That(Count(played, AudioCueId.Explosion), Is.Zero, "Rocket impact must not double-emit the world explosion.");
            yield return WaitRealtime(.3f);
            Assert.That(runner.Launch(target, null, 1f, .4f, 2f), Is.True);
            runner.Cancel();
            runner.Cancel();
            runner.Tick(5f, 14f, 2f, 25, () => true);
            Assert.That(Count(played, AudioCueId.RocketExplosion), Is.EqualTo(1), "Canceled flight must stay silent.");
            runner.Dispose();
            int warnings = Count(played, AudioCueId.RocketWarning);
            Assert.That(runner.Launch(target, null, 1f, .4f, 2f), Is.False);
            Assert.That(Count(played, AudioCueId.RocketWarning), Is.EqualTo(warnings), "Rejected launch must stay silent.");

            var sequence = typeof(EnemyBomberMovement).Assembly.GetType("BomberExplosionSequence");
            sequence.GetMethod("Create", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null,
                new object[] { (Vector2)target, 2f, 0f, 0, null, null, null, null });
            Assert.That(Count(played, AudioCueId.BomberExplosion), Is.EqualTo(1));
            AssertPlaying(AudioCueId.BomberExplosion);
            Assert.That(Count(played, AudioCueId.Explosion), Is.Zero, "Bomber impact must not double-emit the world explosion.");

            // Exercise the existing world route: physics enter -> registered enemy death -> authored delay -> detonation.
            var data = AssetDatabase.LoadAssetAtPath<LocalAnomalyData>("Assets/_Project/Data/Anomalies/LocalAnomaly_ExplosiveZone.asset");
            var zone = (ExplosiveZone)Object.Instantiate(data.ZonePrefab, target, Quaternion.identity);
            Assert.That(LevelAnomalyController.Instance, Is.Not.Null);
            zone.Initialize(data, LevelAnomalyController.Instance, data.ZoneSize);
            var victim = new GameObject("World explosion audio victim");
            victim.transform.position = target;
            victim.AddComponent<EnemyIdentity>();
            var body = victim.AddComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Kinematic;
            body.useFullKinematicContacts = true;
            body.gravityScale = 0f;
            victim.AddComponent<CircleCollider2D>();
            var health = victim.AddComponent<EnemyHealth>();
            try
            {
                var trackedEnemies = (System.Collections.IDictionary)typeof(ExplosiveZone)
                    .GetField("enemyColliderCounts", PrivateInstance).GetValue(zone);
                Physics2D.SyncTransforms();
                yield return CoreTestSupport.Await(() => trackedEnemies.Contains(health));
                health.TakeDamage(1000f, target);
                Assert.That(health.IsDead, Is.True);
                Assert.That(Count(played, AudioCueId.Explosion), Is.Zero, "The world explosion must honor its authored warning delay.");
                yield return CoreTestSupport.Await(() => Count(played, AudioCueId.Explosion) > 0);
                Assert.That(Count(played, AudioCueId.Explosion), Is.EqualTo(1));
                AssertPlaying(AudioCueId.Explosion);
                Assert.That(Count(played, AudioCueId.BomberExplosion), Is.EqualTo(1), "World deaths must not emit the bomber cue.");
                Assert.That(Count(played, AudioCueId.RocketExplosion), Is.EqualTo(1), "World deaths must not emit the rocket cue.");
                zone.Despawn();
                yield return WaitRealtime(.25f);
                Assert.That(Count(played, AudioCueId.Explosion), Is.EqualTo(1), "World zone cleanup must not replay detonation.");
            }
            finally
            {
                if (victim != null) Object.Destroy(victim);
                if (zone != null) Object.Destroy(zone.gameObject);
            }
        }
        finally { service.DebugCuePlayed -= played.Add; Object.Destroy(owner.gameObject); }
    }

    [UnityTest]
    public IEnumerator ElectricAndBeamWarnOnceAtTelegraphAndStaySilentOnStop()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
        yield return new EnterPlayMode();
        yield return BeginProductionRun();
        yield return VerifySiteWarnings();
    }

    private static IEnumerator VerifySiteWarnings()
    {
        // Existing production sites keep their own cadence. Retire their owners before counting this fixture's cues.
        foreach (var site in Object.FindObjectsByType<ProductionAnomalySite>(FindObjectsSortMode.None))
            if (site.GetComponents<MonoBehaviour>().Any(component => component.GetType().Name is
                "ProductionElectricSiteHazard" or "ProductionBeamSiteHazard"))
                site.DisposeSite();
        // Their already accepted one-shots must finish before the isolated warning competes for a voice.
        yield return WaitRealtime(.5f);
        var service = AudioService.Instance;
        var played = new List<AudioCueId>();
        service.DebugCuePlayed += played.Add;
        try
        {
            foreach (bool electric in new[] { true, false })
            {
                var owner = new GameObject("Combat audio telegraph fixture");
                string name = electric ? "ProductionElectricSiteHazard" : "ProductionBeamSiteHazard";
                var hazard = (MonoBehaviour)owner.AddComponent(typeof(AnomalyBeamView).Assembly.GetType(name));
                var prefab = AssetDatabase.LoadAssetAtPath<AnomalyBeamView>(
                    "Assets/_Project/prefabs/Environment/SpecialAnomalies/PF_AnomalyBeam_" + (electric ? "Electric" : "Danger") + ".prefab");
                hazard.GetType().GetMethod("Initialize").Invoke(hazard, new object[]
                    { (Vector2)PlayerRuntimeReference.CachedPlayer.transform.position + Vector2.one * 40f, Vector2.one * 12f, 0f, 0f, prefab });
                var view = owner.GetComponentInChildren<AnomalyBeamView>();
                var cue = electric ? AudioCueId.ElectricWarning : AudioCueId.BeamWarning;
                Assert.That(view.State, Is.EqualTo(AnomalyBeamView.BeamState.Inactive));
                Assert.That(Count(played, cue), Is.Zero, "Creating an idle hazard is not a warning.");
                yield return CoreTestSupport.Await(() => view.State == AnomalyBeamView.BeamState.Telegraph);
                Assert.That(Count(played, cue), Is.EqualTo(1));
                AssertPlaying(cue);
                yield return WaitRealtime(.25f);
                Assert.That(Count(played, cue), Is.EqualTo(1), "Telegraph frames must not replay warning.");
                hazard.GetType().GetMethod("StopHazard").Invoke(hazard, null);
                yield return null;
                Assert.That(Count(played, cue), Is.EqualTo(1), "Stopping an owned hazard must not emit another cue.");
                Object.Destroy(owner);
            }
        }
        finally { service.DebugCuePlayed -= played.Add; }
    }

    private static IEnumerator BeginProductionRun()
    {
        yield return CoreTestSupport.LoadBunker();
        var character = AssetDatabase.LoadAssetAtPath<CharacterData>("Assets/_Project/Data/Characters/01_Gera.asset");
        RunSelectionManager.Instance.SelectCharacter(character);
        var starter = Object.FindFirstObjectByType<BunkerRunStarter>();
        starter.StartRun(starter.transform);
        yield return CoreTestSupport.Await(() => SceneManager.GetActiveScene().name == "MVP" &&
            Object.FindFirstObjectByType<OrbitalStationRuntime>() is { IsInitialized: true } && !SceneTransitionOverlay.IsTransitioning);
        PlayerRuntimeReference.CachedPlayer.GetComponent<PlayerHealth>().AddMaxHealth(1000000f);
        Object.FindFirstObjectByType<EnemySpawner>().StopSpawning();
        Assert.That(AudioService.Instance, Is.Not.Null, "Exercise the authored production audio composition.");
        yield return null;
    }

    private static int Count(List<AudioCueId> played, AudioCueId cue) => played.FindAll(value => value == cue).Count;
    private static void AssertPlaying(AudioCueId cue)
    {
        var service = AudioService.Instance;
        var catalog = (AudioCatalog)new SerializedObject(service).FindProperty("catalog").objectReferenceValue;
        var definitions = new SerializedObject(catalog).FindProperty("cues");
        var clips = new List<AudioClip>();
        for (int i = 0; i < definitions.arraySize; i++)
        {
            var definition = definitions.GetArrayElementAtIndex(i);
            if (definition.FindPropertyRelative("id").intValue != (int)cue) continue;
            var variants = definition.FindPropertyRelative("clips");
            for (int v = 0; v < variants.arraySize; v++)
                clips.Add((AudioClip)variants.GetArrayElementAtIndex(v).objectReferenceValue);
        }
        Assert.That(service.GetComponentsInChildren<AudioSource>().Any(source => source.clip != null &&
            clips.Contains(source.clip) && source.isPlaying && source.isActiveAndEnabled && source.volume > 0f), Is.True,
            cue + " must start an active AudioSource with an authored clip.");
    }
    private static void Invoke(object target, string method) => target.GetType().GetMethod(method, PrivateInstance).Invoke(target, null);
    private static void TickBulletTime(OrbitalStationRuntime station, float dt, bool held) => typeof(OrbitalStationRuntime)
        .GetMethod("TickRightMouse", PrivateInstance).Invoke(station, new object[] { dt, held, false });
    private static IEnumerator WaitRealtime(float duration)
    {
        float until = Time.realtimeSinceStartup + duration;
        while (Time.realtimeSinceStartup < until) yield return null;
    }
}
#endif
