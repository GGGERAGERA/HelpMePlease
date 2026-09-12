#if UNITY_EDITOR
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

public sealed class BunkerStartScreenTests
{
    private const string ScenePath = "Assets/_Project/Scenes/MainBuild/StartScreen.unity";
    [Test, Order(0)]
    public void AuthoredStartSceneIsFirstInBuild()
    {
        EditorSceneManager.OpenScene("Assets/_Project/Scenes/MainBuild/MainMenu.unity", OpenSceneMode.Single);
        Assert.That(EditorBuildSettings.scenes.First(s => s.enabled).path, Is.EqualTo(ScenePath));
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
        try
        {
            var controller = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<StartScreenController>()).Single();
            Assert.That(controller.transform.Find("Canvas/MainPanel"), Is.Not.Null);
            Assert.That(controller.transform.Find("Canvas/Sections"), Is.Not.Null);
            Assert.That(controller.GetComponentsInChildren<Button>().Length, Is.EqualTo(3));
            Assert.That(scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<BunkerIntroController>()), Is.Empty);
        }
        finally { EditorSceneManager.CloseScene(scene, true); }
    }

    [UnityTest, Order(1)]
    public IEnumerator BeginLoadsIntroAndBunkerReloadDoesNotReturnToStart()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
        yield return new EnterPlayMode();
        yield return SceneManager.LoadSceneAsync("StartScreen");
        yield return null;
        var screen = Object.FindFirstObjectByType<StartScreenController>();
        Assert.That(screen, Is.Not.Null);
        Assert.That(Object.FindFirstObjectByType<BunkerIntroController>(), Is.Null);
        var begin = screen.GetComponentsInChildren<Button>().Single(b => b.name == "BEGIN");
        ExecuteEvents.Execute(begin.gameObject, new BaseEventData(EventSystem.current), ExecuteEvents.cancelHandler);
        yield return null;
        Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("StartScreen"));
        Directory.CreateDirectory("Artifacts/GeneratedQA/StartScreen");
        ScreenCapture.CaptureScreenshot("Artifacts/GeneratedQA/StartScreen/start.png");
        yield return null;
        screen.OpenSettings();
        var settings = Object.FindFirstObjectByType<AudioSettingsPanel>();
        Assert.That(settings.IsOpen, Is.True);
        Assert.That(AudioSettingsService.Instance, Is.Not.Null);
        screen.Begin();
        Assert.That(SceneTransitionOverlay.IsTransitioning, Is.False);
        settings.Close();
        Assert.That(begin.IsInteractable(), Is.True);
        screen.Begin();
        screen.Begin();
        Assert.That(begin.IsInteractable(), Is.False);
        float deadline = Time.realtimeSinceStartup + 25;
        while ((SceneManager.GetActiveScene().name != "MainMenu" || SceneTransitionOverlay.IsTransitioning) && Time.realtimeSinceStartup < deadline)
            yield return null;
        Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("MainMenu"));
        Assert.That(SceneTransitionOverlay.IsTransitioning, Is.False);
        Assert.That(Object.FindFirstObjectByType<StartScreenController>(), Is.Null);
        var intro = Object.FindFirstObjectByType<BunkerIntroController>();
        Assert.That(intro.IsPlaying || PlayerPrefs.GetInt(BunkerIntroController.ViewedPreferenceKey) == 1, Is.True);
        intro.StopAllCoroutines();
        typeof(BunkerIntroController).GetMethod("FinishIntro", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(intro, new object[] { false });
        yield return SceneManager.LoadSceneAsync("MainMenu");
        yield return null;
        Assert.That(Object.FindFirstObjectByType<StartScreenController>(), Is.Null);
    }

    [UnityTearDown]
    public IEnumerator LeavePlayMode()
    {
        if (Application.isPlaying) yield return new ExitPlayMode();
    }
}
#endif
