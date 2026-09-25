#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Subject42.Combat.OrbitalStation;
using Object = UnityEngine.Object;

public sealed class PauseMenuTests
{
    private const string Root = "Assets/_Project/";
    [SetUp] public void Setup() => CoreTestSupport.PreservePreferences();
    [UnityTearDown] public IEnumerator Cleanup() => CoreTestSupport.CleanupPlayMode();

    [Test]
    public void SharedPrefabAndSettingsContract()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "prefabs/UI/PauseMenu.prefab");
        Assert.That(prefab, Is.Not.Null, "Both scenes need an authored shared pause root.");
        Assert.That(prefab.GetComponent<PauseMenuUI>(), Is.Not.Null);
        Assert.That(prefab.GetComponentsInChildren<AudioSettingsPanel>(true).Length, Is.EqualTo(1));
        var settings = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "prefabs/UI/SettingsPanel/SettingsPanel.prefab");
        Assert.That(settings.GetComponentsInChildren<Transform>(true).Any(t => t.name == "AutomaticFireToggle"), Is.False);
        Assert.That(new SerializedObject(settings.GetComponent<AudioSettingsPanel>()).FindProperty("automaticFireToggle"), Is.Null);
    }

    [UnityTest]
    public IEnumerator BunkerEscapeSettingsAndTimeOwnership()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
        yield return new EnterPlayMode();
        yield return CoreTestSupport.LoadBunker();
        var pause = Object.FindFirstObjectByType<PauseMenuUI>();
        Assert.That(pause, Is.Not.Null, "Bunker must have the shared pause controller.");
        var panels = Object.FindFirstObjectByType<BunkerPanelManager>();
        panels.OpenCharacterSelection();
        Escape(pause);
        Assert.That(panels.IsAnyPanelOpen, Is.False);
        Assert.That(pause.IsPaused, Is.False, "Closing a panel must consume this Escape.");
        Time.timeScale = .65f;
        Escape(pause);
        Assert.That(pause.IsPaused, Is.True);
        Assert.That(Time.timeScale, Is.Zero);
        foreach (var button in pause.GetComponentsInChildren<UnityEngine.UI.Button>(true))
            if (button.name is "Restart" or "Bunker") Assert.That(button.gameObject.activeInHierarchy, Is.False);
        var cursor = Object.FindFirstObjectByType<BunkerCursorInteractor>();
        var mask = Field(cursor, "interactionMask").GetValue(cursor);
        var camera = (Camera)Field(cursor, "targetCamera").GetValue(cursor);
        var clickTarget = new GameObject("Pause input regression target");
        clickTarget.SetActive(false);
        clickTarget.layer = 31;
        clickTarget.transform.position = (Vector2)camera.ScreenToWorldPoint(Input.mousePosition);
        var station = clickTarget.AddComponent<BunkerStation>();
        Field(station, "stationType").SetValue(station, BunkerStationType.CharacterSelection);
        clickTarget.AddComponent<BoxCollider2D>().size = Vector2.one * 10f;
        clickTarget.AddComponent<BunkerInteractableCollider>();
        clickTarget.SetActive(true);
        Physics2D.SyncTransforms();
        Field(cursor, "interactionMask").SetValue(cursor, (LayerMask)(1 << 31));
        var click = typeof(BunkerCursorInteractor).GetMethod("TryInteract", BindingFlags.Instance | BindingFlags.NonPublic);
        try
        {
            click.Invoke(cursor, null);
            Assert.That(panels.IsAnyPanelOpen, Is.False, "World clicks must not pass through pause.");
            pause.Resume();
            click.Invoke(cursor, null);
            Assert.That(panels.IsAnyPanelOpen, Is.True, "The same station must work after resuming.");
            panels.CloseAll();
            pause.Pause();
        }
        finally
        {
            Field(cursor, "interactionMask").SetValue(cursor, mask);
            Object.Destroy(clickTarget);
        }
        pause.OpenSettings();
        var settings = pause.GetComponentInChildren<AudioSettingsPanel>(true);
        Assert.That(settings.IsOpen, Is.True);
        Escape(pause);
        Assert.That(settings.IsOpen, Is.False);
        Assert.That(pause.IsPaused, Is.True);
        Assert.That(Time.timeScale, Is.Zero);
        Escape(pause);
        Assert.That(pause.IsPaused, Is.False);
        Assert.That(Time.timeScale, Is.EqualTo(.65f));
        pause.Pause();
        pause.enabled = false;
        Assert.That(Time.timeScale, Is.EqualTo(.65f), "Disabling the owner must release its pause.");
        pause.enabled = true;
        var intro = Object.FindFirstObjectByType<BunkerIntroController>();
        Field(intro, "isRunning").SetValue(intro, true);
        pause.Pause();
        Assert.That(pause.IsPaused, Is.False, "Intro owns input.");
        Field(intro, "isRunning").SetValue(intro, false);
        pause.Pause();
        Assert.That(pause.IsPaused, Is.True);
        pause.Resume();
    }

    private static FieldInfo Field(object target, string name) => target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
    private static void Escape(PauseMenuUI pause) => typeof(PauseMenuUI).GetMethod("HandleEscape", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(pause, null);
}

public sealed class PauseMenuGameplayTests
{
    [SetUp]
    public void Setup()
    {
        CoreTestSupport.PreservePreferences();
        PlayerPrefs.SetInt(TutorialController.CompletionKey, 0);
    }
    [UnitySetUp] public IEnumerator BeginRun() => CoreTestSupport.BeginRun();
    [UnityTearDown] public IEnumerator Cleanup() => CoreTestSupport.CleanupPlayMode();

    [UnityTest]
    public IEnumerator GameplayRewardAndHintPriority()
    {
        var pause = Object.FindFirstObjectByType<PauseMenuUI>();
        // Install first-sector guidance in the running test scene; only its pause behavior is under test.
        PlayerPrefs.DeleteKey(TutorialController.CompletionKey);
        TutorialController.Prepare(RunFlowController.Instance);
        yield return null;
        var tutorial = TutorialController.Active;
        Assert.That(tutorial, Is.Not.Null, $"Tutorial eligible={TutorialController.ShouldStart}, sector={RunStateManager.Instance.CurrentSector?.SectorNumber}");
        var hint = (Canvas)Field(tutorial, "overlayCanvas").GetValue(tutorial);
        Assert.That(hint.enabled, Is.True);
        var rewards = UpgradeManager.Instance;
        Field(rewards, "isChoosingUpgrade").SetValue(rewards, true);
        Time.timeScale = 0f;
        pause.Pause();
        Assert.That(pause.IsPaused, Is.False, "A reward must never be covered by pause.");
        Field(rewards, "isChoosingUpgrade").SetValue(rewards, false);
        Time.timeScale = 1f;
        pause.Pause();
        Assert.That(pause.IsPaused, Is.True);
        Assert.That(hint.enabled, Is.False);
        pause.OpenSettings();
        Assert.That(hint.enabled, Is.False);
        Escape(pause);
        Assert.That(pause.IsPaused, Is.True);
        Escape(pause);
        Assert.That(pause.IsPaused, Is.False);
        Assert.That(hint.enabled, Is.True);
        Assert.That(Time.timeScale, Is.EqualTo(1f));
        var station = Object.FindFirstObjectByType<CharacterSpawner>().SpawnedPlayer.GetComponentInChildren<OrbitalStationRuntime>();
        var relocation = station.GetComponent<OrbitalRelocationController>();
        typeof(OrbitalRelocationController).GetMethod("BeginDrag", BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(relocation, new object[] { station.Modules[0] });
        Assert.That(relocation.IsDragging, Is.True);
        Escape(pause);
        Assert.That(relocation.IsDragging, Is.False);
        Assert.That(pause.IsPaused, Is.False, "Orbital cancellation consumes Escape.");
        Escape(pause);
        Assert.That(pause.IsPaused, Is.False, "The same frame must stay consumed regardless of Update order.");
        yield return null;
        Escape(pause);
        Assert.That(pause.IsPaused, Is.True);
        pause.Resume();
        // Re-entry must bind a fresh controller and not retain paused state.
        Assert.That(SceneTransitionOverlay.Load("MainMenu"), Is.True);
        Assert.That(SceneTransitionOverlay.IsTransitioning, Is.True);
        pause.Pause();
        Assert.That(pause.IsPaused, Is.False);
        yield return CoreTestSupport.Await(() => SceneManager.GetActiveScene().name == "MainMenu" && !SceneTransitionOverlay.IsTransitioning);
        pause = Object.FindFirstObjectByType<PauseMenuUI>();
        pause.Pause();
        Assert.That(pause.IsPaused, Is.True);
        pause.Resume();
        Assert.That(Time.timeScale, Is.EqualTo(1f));
    }

    private static FieldInfo Field(object target, string name) => target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
    private static void Escape(PauseMenuUI pause) => typeof(PauseMenuUI).GetMethod("HandleEscape", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(pause, null);
}
#endif
