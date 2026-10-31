#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Reflection;
using System.Linq;
using System.Collections.Generic;
using NUnit.Framework;
using Subject42.Combat.OrbitalStation;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.EventSystems;
using Object = UnityEngine.Object;

public sealed class AudioMonitorTests
{
    [SetUp] public void Setup() => CoreTestSupport.PreservePreferences();
    [UnityTearDown] public IEnumerator Cleanup() => CoreTestSupport.CleanupPlayMode();

    [UnityTest, Timeout(120000)]
    public IEnumerator MainRunHistoryShowsRealCallerAndClipWithoutInterruptingGameplay()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
        yield return new EnterPlayMode();
        yield return VerifyMainRun();
    }

    private static IEnumerator VerifyMainRun()
    {
        yield return CoreTestSupport.LoadBunker();
        var character = UnityEditor.AssetDatabase.LoadAssetAtPath<CharacterData>(
            "Assets/_Project/Data/Characters/01_Gera.asset");
        RunSelectionManager.Instance.SelectCharacter(character);
        var starter = Object.FindFirstObjectByType<BunkerRunStarter>();
        // Match the existing movement/readability QA layout; this test checks diagnostics, not random site admission.
        if (!GameplayRandom.IsActive) GameplayRandom.Begin(1709);
        starter.StartRun(starter.transform);
        yield return CoreTestSupport.Await(() => ProductionSceneComposition.Active is { IsReady: true } &&
            PlayerRuntimeReference.CachedPlayer != null && !SceneTransitionOverlay.IsTransitioning);
        Application.runInBackground = true;
        Object.FindFirstObjectByType<EnemySpawner>().StopSpawning();
        var player = PlayerRuntimeReference.CachedPlayer;
        player.GetComponent<PlayerHealth>().AddMaxHealth(1000000f);
        var audio = AudioService.Instance;
        var toggle = typeof(AudioService).GetMethod("ToggleDebugAudioPanel");
        Assert.That(toggle, Is.Not.Null, "The existing AudioService must expose the non-pausing sound history panel.");
        var clear = typeof(AudioService).GetMethod("ClearDebugAudioHistory");
        var read = typeof(AudioService).GetMethod("GetDebugAudioPlayback");
        var count = typeof(AudioService).GetProperty("DebugAudioHistoryCount");
        var visible = typeof(AudioService).GetProperty("DebugAudioPanelVisible");
        Assert.That(clear, Is.Not.Null); Assert.That(read, Is.Not.Null);
        Assert.That(count, Is.Not.Null); Assert.That(visible, Is.Not.Null);
        Assert.That((bool)visible.GetValue(audio), Is.False);
        clear.Invoke(audio, null);
        Vector3 point = player.transform.position + Vector3.right;
        Assert.That(audio.PlayAt(AudioCueId.PlayerDash, point), Is.True);
        object entry = read.Invoke(audio, new object[] { 0 });
        Assert.That(Value<AudioCueId>(entry, "CueId"), Is.EqualTo(AudioCueId.PlayerDash));
        Assert.That(Value<string>(entry, "ClipName"), Is.EqualTo("PlayerDash"));
        Assert.That(Value<string>(entry, "CallerFile"), Does.EndWith("AudioMonitorTests.cs"));
        Assert.That(Value<string>(entry, "CallerMember"), Is.EqualTo(nameof(VerifyMainRun)));
        Assert.That(Value<int>(entry, "CallerLine"), Is.GreaterThan(0));
        Assert.That(Value<Vector3?>(entry, "Position"), Is.EqualTo(point));
        int accepted = (int)count.GetValue(audio);
        Assert.That(audio.PlayAt(AudioCueId.PlayerDash, point), Is.False, "Immediate cooldown request is rejected.");
        Assert.That((int)count.GetValue(audio), Is.EqualTo(accepted), "Rejected requests must not pretend to be playback.");

        var movement = player.GetComponent<CharacterMovement2D>();
        var inputOwner = player.GetComponentInChildren<OrbitalInteractionController>();
        float scale = Time.timeScale, fixedStep = Time.fixedDeltaTime;
        bool cursor = Cursor.visible, blocked = inputOwner.IsGameplayInputBlocked;
        var lockMode = Cursor.lockState;
        toggle.Invoke(audio, null);
        Assert.That((bool)visible.GetValue(audio), Is.True);
        Assert.That(inputOwner.IsGameplayInputBlocked, Is.EqualTo(blocked));
        Assert.That(Time.timeScale, Is.EqualTo(scale));
        Assert.That(Time.fixedDeltaTime, Is.EqualTo(fixedStep));
        Assert.That(Cursor.visible, Is.EqualTo(cursor)); Assert.That(Cursor.lockState, Is.EqualTo(lockMode));
        var monitor = audio.transform.Find("[Dev] Audio monitor");
        Assert.That(monitor, Is.Not.Null);
        var pointerHits = new List<RaycastResult>();
        EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current)
            { position = new Vector2(Screen.width - 60, Screen.height / 2f) }, pointerHits);
        Assert.That(pointerHits.Any(hit => hit.gameObject.transform.IsChildOf(monitor)), Is.False,
            "Pointer targeting inside the visible panel must pass through to the game.");
        Vector3 before = player.transform.position;
        movement.MovementIntent = () => Vector2.right;
        float start = Time.realtimeSinceStartup;
        while (Time.realtimeSinceStartup - start < .35f) yield return null;
        Assert.That(player.transform.position.x - before.x, Is.GreaterThan(.2f), "Player must keep moving while the panel is visible.");
        typeof(CharacterMovement2D).GetMethod("TryStartDash", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(movement, null);
        entry = read.Invoke(audio, new object[] { 0 });
        Assert.That(Value<AudioCueId>(entry, "CueId"), Is.EqualTo(AudioCueId.PlayerDash));
        Assert.That(Value<string>(entry, "CallerFile"), Does.EndWith("PlayerFeedbackPresentation.cs"));
        Assert.That(Value<string>(entry, "CallerMember"), Is.EqualTo("OnDash"));
        yield return null;
        yield return null;
        string output = Path.GetFullPath("Artifacts/GeneratedQA/AudioMonitor-20261026");
        Directory.CreateDirectory(output);
        string screenshot = Path.Combine(output, "main-run-panel.png");
        if (File.Exists(screenshot)) File.Delete(screenshot);
        ScreenCapture.CaptureScreenshot(screenshot);
        yield return CoreTestSupport.Await(() => File.Exists(screenshot));
        float firstCapture = Time.realtimeSinceStartup;
        Vector3 firstPosition = player.transform.position;
        while (Time.realtimeSinceStartup - firstCapture < .3f) yield return null;
        Assert.That(player.transform.position.x, Is.GreaterThan(firstPosition.x), "The run must continue between visible panel frames.");
        screenshot = Path.Combine(output, "main-run-panel-later.png");
        if (File.Exists(screenshot)) File.Delete(screenshot);
        ScreenCapture.CaptureScreenshot(screenshot);
        yield return CoreTestSupport.Await(() => File.Exists(screenshot));
        movement.MovementIntent = () => Vector2.zero;
        toggle.Invoke(audio, null);
        Assert.That((bool)visible.GetValue(audio), Is.False);
        Assert.That(Time.timeScale, Is.EqualTo(scale));
        Time.timeScale = 0f;
        toggle.Invoke(audio, null); toggle.Invoke(audio, null);
        Assert.That(Time.timeScale, Is.Zero, "Showing/hiding diagnostics cannot resume an already paused game.");
        Time.timeScale = scale;

        // Exercise real playback, not synthetic debug events; silent short clips avoid an audition burst.
        var clip = AudioClip.Create("Audio monitor bounded-history test", 960, 1, 48000, false);
        try
        {
            clear.Invoke(audio, null);
            for (int i = 0; i < 80; i++)
                Assert.That(audio.PlayExternalOneShot(clip, new Vector3(i, 0), 0f, AudioCategory.SFX), Is.True);
            Assert.That((int)count.GetValue(audio), Is.EqualTo(64), "History memory must stay bounded in a long run.");
            entry = read.Invoke(audio, new object[] { 0 });
            Assert.That(Value<string>(entry, "ClipName"), Is.EqualTo(clip.name));
            Assert.That(Value<Vector3?>(entry, "Position"), Is.EqualTo(new Vector3(79, 0)));
            Assert.That(Value<string>(entry, "CallerMember"), Is.EqualTo(nameof(VerifyMainRun)));
            entry = read.Invoke(audio, new object[] { 63 });
            Assert.That(Value<Vector3?>(entry, "Position"), Is.EqualTo(new Vector3(16, 0)));

            var directOwner = new GameObject("Direct player-hit audio fixture", typeof(AudioSource), typeof(PlayerHitSound));
            try
            {
                var hit = directOwner.GetComponent<PlayerHitSound>();
                typeof(PlayerHitSound).GetField("hitClips", BindingFlags.Instance | BindingFlags.NonPublic)
                    .SetValue(hit, new[] { clip });
                hit.Play();
                entry = read.Invoke(audio, new object[] { 0 });
                Assert.That(Value<string>(entry, "CallerFile"), Does.EndWith("PlayerHitSound.cs"));
                Assert.That(Value<string>(entry, "SourceObject"), Is.EqualTo(directOwner.name));
                Assert.That(directOwner.GetComponent<AudioSource>().isPlaying, Is.True);
            }
            finally { Object.Destroy(directOwner); }

            // Music/ambience use dedicated sources, not the SFX pool; repeated keep-alive calls aren't new launches.
            clear.Invoke(audio, null);
            Assert.That(audio.PlayMusic(AudioCueId.BunkerMusic), Is.True);
            entry = read.Invoke(audio, new object[] { 0 });
            Assert.That(Value<AudioCueId>(entry, "CueId"), Is.EqualTo(AudioCueId.BunkerMusic));
            Assert.That(Value<AudioCategory>(entry, "Category"), Is.EqualTo(AudioCategory.Music));
            Assert.That(audio.PlayMusic(AudioCueId.BunkerMusic), Is.True);
            Assert.That((int)count.GetValue(audio), Is.EqualTo(1));
            Assert.That(audio.PlayAmbience(AudioCueId.PlayerDash), Is.False, "Wrong-category requests cannot invent a history row.");
            Assert.That((int)count.GetValue(audio), Is.EqualTo(1));
            var loop = audio.StartLoop(AudioCueId.StartScreenMusic, player.transform);
            Assert.That(loop, Is.Not.Null);
            Assert.That(loop.IsPlaying, Is.True);
            entry = read.Invoke(audio, new object[] { 0 });
            Assert.That(Value<AudioCueId>(entry, "CueId"), Is.EqualTo(AudioCueId.StartScreenMusic));
            Assert.That(Value<string>(entry, "SourceObject"), Is.EqualTo(player.name));
            loop.Stop();
            Assert.That(loop.IsPlaying, Is.False);
        }
        finally { Object.Destroy(clip); }
    }

    private static T Value<T>(object entry, string name) => (T)entry.GetType().GetProperty(name).GetValue(entry);
}
#endif
