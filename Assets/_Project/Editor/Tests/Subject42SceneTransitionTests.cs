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
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

public sealed class Subject42SceneTransitionTests
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    [SetUp] public void SavePreferences() => new Subject42FinalBossFlowTests().PreserveRewardsAndUnlockProgress();
    [UnityTearDown] public IEnumerator Cleanup()
    {
        if (SceneTransitionOverlay.Instance != null) SceneTransitionOverlay.Instance.gameObject.SetActive(false);
        var cleanup = new Subject42FinalBossFlowTests().CleanupPlayMode();
        while (cleanup.MoveNext()) yield return cleanup.Current;
    }

    [UnityTest]
    public IEnumerator ProductionTransitions_LockInput_RestoreState_AndReuseOwner()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
        yield return new EnterPlayMode();
        yield return Exercise();
    }

    [UnityTest]
    public IEnumerator Vika_TransitionsRestartDeathAndBunker()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
        yield return new EnterPlayMode();
        yield return Exercise("Vika");
    }

    private static IEnumerator Exercise(string characterName = "Gera")
    {
        Application.runInBackground = true;
        PlayerPrefs.SetInt(BunkerIntroController.ViewedPreferenceKey, 1);
        SceneManager.LoadSceneAsync("MainMenu");
        float startDeadline = Time.realtimeSinceStartup + 20f;
        while ((SceneManager.GetActiveScene().name != "MainMenu" || RunSelectionManager.Instance == null ||
            Object.FindFirstObjectByType<BunkerRunStarter>() == null) && Time.realtimeSinceStartup < startDeadline)
            yield return null;
        for (int i = 0; i < 5; i++) yield return null;
        var character = AssetDatabase.FindAssets("t:CharacterData").Select(id =>
            AssetDatabase.LoadAssetAtPath<CharacterData>(AssetDatabase.GUIDToAssetPath(id)))
            .First(c => c.characterName == characterName);
        Assert.That(RunSelectionManager.Instance, Is.Not.Null, "Persistent selection after MainMenu Start");
        RunSelectionManager.Instance.SelectCharacter(character);
        StartRun();
        Assert.That(SceneTransitionOverlay.IsTransitioning, Is.True);
        var owner = SceneTransitionOverlay.Instance;
        bool duplicatePrepared = false;
        Assert.That(SceneTransitionOverlay.Load("MVP", () => duplicatePrepared = true), Is.False);
        Assert.That(duplicatePrepared, Is.False);
        yield return Finished();
        CheckGameplay(character);
        Assert.That(Object.FindFirstObjectByType<OrbitalStationRuntime>().Geometry.Type, Is.EqualTo(character.orbitalPath));

        var pause = Object.FindFirstObjectByType<PauseMenuUI>();
        pause.Pause();
        Assert.That(Time.timeScale, Is.Zero);
        Call(pause, "ReturnToBunker");
        yield return Finished();
        Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("MainMenu"));
        Assert.That(RunSelectionManager.Instance.SelectedCharacter, Is.SameAs(character));
        CheckOwner(owner);

        // Zero-duration close/reveal still covers activation and acquires the lock.
        Set(owner, "closeDuration", 0f); Set(owner, "revealDuration", 0f); Set(owner, "minimumHold", 0f);
        StartRun();
        yield return Finished();
        CheckGameplay(character);
        Assert.That(Object.FindFirstObjectByType<OrbitalStationRuntime>().Geometry.Type, Is.EqualTo(character.orbitalPath));
        pause = Object.FindFirstObjectByType<PauseMenuUI>();
        pause.Pause();
        Call(pause, "RestartConfirmed");
        yield return Finished();
        CheckGameplay(character);
        Assert.That(Object.FindFirstObjectByType<OrbitalStationRuntime>().Geometry.Type, Is.EqualTo(character.orbitalPath));
        CheckOwner(owner);

        // Exercise actual death result entry point, including a slow loading hold.
        Set(owner, "closeDuration", .3f); Set(owner, "revealDuration", .35f); Set(owner, "additionalHold", .8f);
        Object.FindFirstObjectByType<CharacterSpawner>().SpawnedPlayer.GetComponent<PlayerHealth>()
            .TakeDamage(float.MaxValue, Vector2.zero);
        Assert.That(Time.timeScale, Is.Zero);
        GameOverManager.Instance.MainMenu();
        yield return Finished();
        Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("MainMenu"));
        CheckOwner(owner);
        Set(owner, "additionalHold", 0f);
        StartRun();
        yield return Finished();
        CheckGameplay(character);
        Assert.That(Object.FindFirstObjectByType<OrbitalStationRuntime>().Geometry.Type, Is.EqualTo(character.orbitalPath));
        Object.FindFirstObjectByType<CharacterSpawner>().SpawnedPlayer.GetComponent<PlayerHealth>()
            .TakeDamage(float.MaxValue, Vector2.zero);
        GameOverManager.Instance.RestartGame();
        yield return Finished();
        CheckGameplay(character);
        Assert.That(Object.FindFirstObjectByType<OrbitalStationRuntime>().Geometry.Type, Is.EqualTo(character.orbitalPath));
        CheckOwner(owner);

        LogAssert.Expect(LogType.Error, "[SceneTransition] Scene '__missing_scene__' is not in the build.");
        bool prepared = false;
        Assert.That(SceneTransitionOverlay.Load("__missing_scene__", () => prepared = true), Is.False);
        Assert.That(prepared, Is.False);
        Assert.That(SceneTransitionOverlay.IsTransitioning, Is.False);

        // Nested callback exceptions must retain an actionable opaque recovery screen.
        LogAssert.Expect(LogType.Exception, new System.Text.RegularExpressions.Regex("InvalidOperationException: transition QA"));
        SceneTransitionOverlay.Load("MVP", null, t => throw new InvalidOperationException("transition QA"));
        Assert.That((bool)Get(owner, "faulted"), Is.True);
        Assert.That(owner.GetComponentInChildren<CanvasGroup>().alpha, Is.EqualTo(1f));
        typeof(SceneTransitionOverlay).GetMethod("Recover", Private).Invoke(owner, new object[] { false });
        yield return Finished();
        CheckGameplay(character);
        Assert.That(Object.FindFirstObjectByType<OrbitalStationRuntime>().Geometry.Type, Is.EqualTo(character.orbitalPath));

        // Delay camera readiness past the watchdog; retry loads a fresh authored camera.
        Set(owner, "readyTimeout", .1f);
        UnityEngine.Events.UnityAction<Scene, LoadSceneMode> breakCamera = (scene, mode) =>
        {
            var camera = Object.FindFirstObjectByType<CameraFollow>();
            camera.enabled = false;
            camera.target = null;
        };
        SceneManager.sceneLoaded += breakCamera;
        LogAssert.Expect(LogType.Error, "[SceneTransition] Ready timeout in 'MVP': check player, ORBITAL, HUD and camera initialization.");
        try
        {
            SceneTransitionOverlay.Load("MVP");
            float failureDeadline = Time.realtimeSinceStartup + 10f;
            while (!(bool)Get(owner, "faulted") && Time.realtimeSinceStartup < failureDeadline) yield return null;
            Assert.That((bool)Get(owner, "faulted"), Is.True);
            Assert.That(owner.GetComponentInChildren<CanvasGroup>().alpha, Is.EqualTo(1f));
        }
        finally { SceneManager.sceneLoaded -= breakCamera; }
        Set(owner, "readyTimeout", 20f);
        typeof(SceneTransitionOverlay).GetMethod("Recover", Private).Invoke(owner, new object[] { false });
        yield return Finished();
        CheckGameplay(character);
        Assert.That(Object.FindFirstObjectByType<OrbitalStationRuntime>().Geometry.Type, Is.EqualTo(character.orbitalPath));
        CheckOwner(owner);
    }

    private static void StartRun()
    {
        var starter = Object.FindFirstObjectByType<BunkerRunStarter>();
        starter.StartRun((Transform)typeof(BunkerRunStarter).GetField("cameraRig", Private).GetValue(starter));
    }

    private static IEnumerator Finished(string capture = null)
    {
        Assert.That(SceneTransitionOverlay.IsTransitioning, Is.True);
        float deadline = Time.realtimeSinceStartup + 30f;
        float nextCapture = 0f;
        int frame = 0;
        string folder = Path.GetFullPath("Artifacts/GeneratedQA/SceneTransitions/" + capture);
        if (capture != null) Directory.CreateDirectory(folder);
        while (SceneTransitionOverlay.IsTransitioning && Time.realtimeSinceStartup < deadline)
        {
            var pause = Object.FindFirstObjectByType<PauseMenuUI>();
            bool wasPaused = pause != null && pause.IsPaused;
            pause?.Pause();
            if (pause != null) Assert.That(pause.IsPaused, Is.EqualTo(wasPaused), "Pause input must be locked");
            if (capture != null && Time.realtimeSinceStartup >= nextCapture)
            {
                ScreenCapture.CaptureScreenshot(Path.Combine(folder, $"{frame++:D3}.png"));
                nextCapture = Time.realtimeSinceStartup + .08f;
            }
            yield return null;
        }
        Assert.That(SceneTransitionOverlay.IsTransitioning, Is.False, "Transition must release its lock");
        Assert.That(Time.timeScale, Is.EqualTo(1f));
        var audio = AudioSettingsService.Instance;
        Assert.That((float)Get(audio, "transitionGain"), Is.EqualTo(1f));
        var mixer = (UnityEngine.Audio.AudioMixer)Get(audio, "mixer");
        Assert.That(mixer.GetFloat(AudioSettingsService.MasterVolumeParameter, out float db), Is.True);
        float expectedDb = audio.MasterVolume <= 0f ? -80f : Mathf.Log10(audio.MasterVolume) * 20f;
        Assert.That(db, Is.EqualTo(expectedDb).Within(.01f));
        Assert.That(SceneTransitionOverlay.Instance.GetComponentInChildren<Canvas>().enabled, Is.False);
        if (capture != null)
        {
            ScreenCapture.CaptureScreenshot(Path.Combine(folder, $"{frame:D3}.png"));
            yield return null;
        }
    }

    private static void CheckGameplay(CharacterData character)
    {
        Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("MVP"));
        var player = Object.FindFirstObjectByType<CharacterSpawner>();
        Assert.That(player.SpawnedCharacterData, Is.SameAs(character));
        var orbital = player.SpawnedPlayer.GetComponentInChildren<OrbitalStationRuntime>();
        Assert.That(orbital.IsInitialized, Is.True);
        Assert.That(orbital.State, Is.SameAs(RunStateManager.Instance.OrbitalStationState));
        Assert.That(orbital.InputOwner.IsGameplayInputBlocked, Is.False);
        Assert.That(orbital.State.Modules[0].ModuleType, Is.EqualTo(OrbitalModuleKind.Pistol));
        Assert.That(HUDManager.Instance.IsPlayerBound, Is.True);
        Assert.That(Object.FindFirstObjectByType<CameraFollow>().target, Is.Not.Null);
    }

    private static void CheckOwner(SceneTransitionOverlay owner)
    {
        Assert.That(Object.FindObjectsByType<SceneTransitionOverlay>(FindObjectsSortMode.None).Length, Is.EqualTo(1));
        Assert.That(SceneTransitionOverlay.Instance, Is.SameAs(owner));
        Assert.That(Object.FindObjectsByType<EventSystem>(FindObjectsSortMode.None).Length, Is.EqualTo(1));
        Assert.That(Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None).Count(x => x.isActiveAndEnabled), Is.EqualTo(1));
    }

    private static object Get(object target, string field) => target.GetType().GetField(field, Private).GetValue(target);
    private static void Set(object target, string field, object value) => target.GetType().GetField(field, Private).SetValue(target, value);
    private static void Call(object target, string method) => target.GetType().GetMethod(method, Private).Invoke(target, null);
}
#endif
