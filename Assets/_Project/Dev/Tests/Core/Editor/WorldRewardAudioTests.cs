#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

public sealed class WorldRewardAudioTests
{
    private const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
    [SetUp] public void Preserve() => CoreTestSupport.PreservePreferences();
    [UnityTearDown] public IEnumerator Cleanup() => CoreTestSupport.CleanupPlayMode();

    [UnityTest, Timeout(90000)]
    public IEnumerator AcceptedPickupsPlayTheirOwnCueWithoutXPOrRejectedCollectionAudio()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
        yield return new EnterPlayMode();
        yield return CheckPickups();
    }

    private static IEnumerator CheckPickups()
    {
        yield return BeginProductionRun();
        var player = PlayerRuntimeReference.CachedPlayer;
        player.GetComponent<CharacterMovement2D>().enabled = false;
        var health = player.GetComponent<PlayerHealth>();
        health.DebugInvulnerable = true;
        health.SetRuntimeHealth(100, 50);
        var audio = AudioService.Instance;
        Assert.That(audio, Is.Not.Null);
        var cues = new List<AudioCueId>();
        audio.DebugCuePlayed += cues.Add;
        try
        {
            var kit = SpawnRare("p_medKit1", player.transform.position);
            yield return CoreTestSupport.Await(() => kit == null);
            Assert.That(health.CurrentHealth, Is.EqualTo(75));
            Assert.That(cues.Count(c => c == AudioCueId.MedkitPickup), Is.EqualTo(1));
            Assert.That(cues, Has.No.Member(AudioCueId.XPPickup), "Rare pickups must not call the XP implementation.");

            cues.Clear(); health.SetCurrentHealth(100);
            kit = SpawnRare("p_medKit1", player.transform.position);
            float until = Time.time + .2f;
            while (Time.time < until) yield return null;
            Assert.That(kit != null && kit.isActiveAndEnabled, Is.True);
            Assert.That(cues.Count(c => c == AudioCueId.MedkitPickup), Is.Zero, "Full health rejects collection silently.");
            kit.Despawn();

            cues.Clear();
            var magnet = SpawnRare("p_magnet1", player.transform.position);
            yield return CoreTestSupport.Await(() => magnet == null);
            Assert.That(ExperienceManager.Instance.IsGlobalMagnetActive, Is.True);
            Assert.That(cues.Count(c => c == AudioCueId.MagnetPickup), Is.EqualTo(1));
            Assert.That(cues, Has.No.Member(AudioCueId.XPPickup));

            cues.Clear();
            var coin = SpawnCoin(player.transform, player.transform.position, 10);
            yield return CoreTestSupport.Await(() => coin == null);
            Assert.That(cues.Count(c => c == AudioCueId.GoldPickup), Is.EqualTo(1));
            cues.Clear();
            SpawnCoin(player.transform, player.transform.position + Vector3.right * 100, .1f);
            until = Time.time + .3f;
            while (Time.time < until) yield return null;
            Assert.That(cues.Count(c => c == AudioCueId.GoldPickup), Is.Zero, "Expiry must not sound like a collection.");
            SpawnCoin(player.transform, player.transform.position + Vector3.right * 100, 10);
            GoldenCoinPickup.ClearAll(); yield return null;
            Assert.That(cues.Count(c => c == AudioCueId.GoldPickup), Is.Zero, "Rule clear must not sound like a collection.");

            cues.Clear(); ExperienceManager.Instance.ClearGlobalMagnet();
            var xp = Object.Instantiate(LoadPickup("p_Hex1"), player.transform.position, Quaternion.identity);
            yield return CoreTestSupport.Await(() => xp == null);
            Assert.That(cues.Count(c => c == AudioCueId.XPPickup), Is.EqualTo(1));
            Assert.That(cues.Count(c => c == AudioCueId.MedkitPickup || c == AudioCueId.MagnetPickup || c == AudioCueId.GoldPickup), Is.Zero);
        }
        finally { audio.DebugCuePlayed -= cues.Add; }
    }

    [UnityTest, Timeout(90000)]
    public IEnumerator RelayContactPhasesCompletionAndQualifiedCancelPlayOnce()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
        yield return new EnterPlayMode();
        yield return CheckRelay(false);
    }

    [UnityTest, Timeout(90000)]
    public IEnumerator RelayFailureIsAudibleButAdministrativeResetAndUnloadAreSilent()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
        yield return new EnterPlayMode();
        yield return CheckRelay(true);
    }

    private static IEnumerator CheckRelay(bool exitCases)
    {
        yield return BeginProductionRun();
        Application.runInBackground = true;
        var events = Object.FindFirstObjectByType<WorldEventSpawner>();
        var player = PlayerRuntimeReference.CachedPlayer.transform;
        var prefab = events.EventPrefabs.OfType<OrbitalRelayEvent>().Single();
        events.ClearAllDebugEvents();
        var audio = AudioService.Instance;
        Assert.That(audio, Is.Not.Null);
        var cues = new List<AudioCueId>();
        audio.DebugCuePlayed += cues.Add;
        var config = ScriptableObject.CreateInstance<OrbitalRelayConfig>();
        config.requiredActivations = 1; config.stabilizationContactTime = .08f;
        config.stabilizationDuration = exitCases ? .2f : 10f;
        config.transitionDuration = .3f; config.bonusDuration = .4f;
        try
        {
            var relay = SpawnRelay(events, player, prefab, config);
            var movement = player.GetComponent<CharacterMovement2D>();
            movement.MovementIntent = () => Vector2.zero;
            if (exitCases)
            {
                player.position += Vector3.right * 30;
                yield return CoreTestSupport.Await(() => relay.IsCompleted);
                Assert.That(relay.IsFailed, Is.True);
                Assert.That(cues.Count(c => c == AudioCueId.RelayFailed), Is.EqualTo(1));
                relay.Cancel(); yield return null;
                Assert.That(cues.Count(c => c == AudioCueId.RelayFailed), Is.EqualTo(1));
            }
            else
            {
                yield return ContactUntilTransition(player, relay);
                Assert.That(cues.Count(c => c == AudioCueId.RelayNodeActivated), Is.EqualTo(1));
                Assert.That(cues.Count(c => c == AudioCueId.RelayPhaseChanged), Is.EqualTo(1), "Only the transition earns a phase cue before Bonus.");
                yield return CoreTestSupport.Await(() => relay.Snapshot.Phase == OrbitalRelayPhase.Bonus);
                Assert.That(cues.Count(c => c == AudioCueId.RelayPhaseChanged), Is.EqualTo(2));
                player.position += Vector3.right * 30;
                yield return CoreTestSupport.Await(() => relay.IsCompleted);
                Assert.That(cues.Count(c => c == AudioCueId.RelayCompleted), Is.EqualTo(1));
                Assert.That(cues.Count(c => c == AudioCueId.RelayPhaseChanged), Is.EqualTo(2), "Result owns terminal feedback without a second phase chime.");
                relay.Cancel(); yield return null;
                Assert.That(cues.Count(c => c == AudioCueId.RelayCompleted), Is.EqualTo(1));
            }

            events.ClearAllDebugEvents(); yield return null;
            config.stabilizationDuration = 10;
            cues.Clear(); relay = SpawnRelay(events, player, prefab, config);
            yield return ContactUntilTransition(player, relay);
            if (!exitCases)
            {
                relay.Cancel(); relay.Cancel();
                Assert.That(cues.Count(c => c == AudioCueId.RelayCompleted), Is.EqualTo(1), "Gameplay cancel after qualification is a real success.");
                Assert.That(cues.Count(c => c == AudioCueId.RelayFailed), Is.Zero);
            }
            else
            {
                cues.Clear(); events.ClearAllDebugEvents(); yield return null;
                Assert.That(cues.Count(c => c == AudioCueId.RelayCompleted || c == AudioCueId.RelayFailed), Is.Zero);
                relay = SpawnRelay(events, player, prefab, config);
                yield return ContactUntilTransition(player, relay);
                cues.Clear();
                var scene = player.gameObject.scene;
                SceneManager.SetActiveScene(SceneManager.CreateScene("WorldAudioUnload"));
                yield return SceneManager.UnloadSceneAsync(scene);
                yield return null;
                Assert.That(cues.Count(c => c == AudioCueId.RelayCompleted || c == AudioCueId.RelayFailed), Is.Zero,
                    "Unload after qualification must not announce a gameplay result.");
            }
        }
        finally
        {
            audio.DebugCuePlayed -= cues.Add;
            if (events != null) events.ClearAllDebugEvents();
            Object.Destroy(config);
        }
    }

    private static IEnumerator ContactUntilTransition(Transform player, OrbitalRelayEvent relay)
    {
        var station = player.GetComponentInChildren<Subject42.Combat.OrbitalStation.OrbitalStationRuntime>();
        float deadline = Time.time + 8f;
        while (relay.Snapshot.Phase == OrbitalRelayPhase.Stabilization && Time.time < deadline)
        {
            var module = station.Modules.First();
            Assert.That(relay.ActiveNode.TryGetContactCircle(out var center, out _), Is.True);
            player.position += (Vector3)(center - module.WorldPosition);
            Physics2D.SyncTransforms();
            yield return null;
        }
        Assert.That(relay.Snapshot.Phase, Is.EqualTo(OrbitalRelayPhase.Transition));
    }
    private static OrbitalRelayEvent SpawnRelay(WorldEventSpawner events, Transform player, OrbitalRelayEvent prefab, OrbitalRelayConfig config)
    {
        var field = typeof(OrbitalRelayEvent).GetField("config", Hidden);
        var original = field.GetValue(prefab);
        try
        {
            field.SetValue(prefab, config);
            Assert.That(events.SpawnDebugEventAt(prefab, player.position, true, out var instance), Is.True);
            instance.Interact();
            Assert.That(instance.IsStarted, Is.True, "Production player must start the authored Relay through its normal interaction.");
            return (OrbitalRelayEvent)instance;
        }
        finally { field.SetValue(prefab, original); }
    }
    private static GameObject LoadPickup(string name) => AssetDatabase.LoadAssetAtPath<GameObject>(
        "Assets/_Project/prefabs/Pickups/" + name + ".prefab");
    private static RarePickup SpawnRare(string name, Vector3 position) => Object.Instantiate(
        LoadPickup(name), position, Quaternion.identity).GetComponent<RarePickup>();
    private static GoldenCoinPickup SpawnCoin(Transform player, Vector3 position, float lifetime)
    {
        var coin = Object.Instantiate(LoadPickup("p_coin1"), position, Quaternion.identity).GetComponent<GoldenCoinPickup>();
        coin.Initialize(player, 1, lifetime, 3, 10, 0, .1f);
        return coin;
    }
    private static IEnumerator BeginProductionRun()
    {
        yield return CoreTestSupport.LoadBunker();
        RunSelectionManager.Instance.SelectCharacter(AssetDatabase.LoadAssetAtPath<CharacterData>(
            "Assets/_Project/Data/Characters/01_Gera.asset"));
        var starter = Object.FindFirstObjectByType<BunkerRunStarter>();
        starter.StartRun(starter.transform);
        yield return CoreTestSupport.Await(() => SceneManager.GetActiveScene().name == "MVP" &&
            Object.FindFirstObjectByType<Subject42.Combat.OrbitalStation.OrbitalStationRuntime>() is { IsInitialized: true } &&
            PlayerRuntimeReference.CachedPlayer != null && !SceneTransitionOverlay.IsTransitioning);
        Object.FindFirstObjectByType<EnemySpawner>().StopSpawning();
        foreach (var enemy in EnemyHealth.ActiveInstances.ToArray()) Object.Destroy(enemy.gameObject);
        PlayerRuntimeReference.CachedPlayer.GetComponent<PlayerHealth>().DebugInvulnerable = true;
    }
}
#endif
