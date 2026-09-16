#if UNITY_EDITOR
using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public sealed class StartScreenPresentationTests
{
    [SetUp] public void PreservePreferences() => CoreTestSupport.PreservePreferences();
    [UnityTearDown] public IEnumerator Cleanup() => CoreTestSupport.CleanupPlayMode();

    private const string ScenePath = "Assets/_Project/Scenes/MainBuild/StartScreen.unity";
    private static Type PresentationType => typeof(StartScreenController).Assembly.GetType("StartScreenAtmosphere");

    [Test]
    public void BackgroundIsAuthoredAndDoesNotInterceptButtons()
    {
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
        try
        {
            Assert.That(PresentationType, Is.Not.Null, "Missing capsule atmosphere component");
            var effect = scene.GetRootGameObjects().SelectMany(x => x.GetComponentsInChildren(PresentationType, true)).Single();
            var image = effect.GetComponent<Image>();
            Assert.That(image.sprite, Is.Not.Null);
            Assert.That(image.raycastTarget, Is.False);
            Assert.That(image.sprite.texture.width, Is.GreaterThan(1000));
            var controller = scene.GetRootGameObjects().SelectMany(x => x.GetComponentsInChildren<StartScreenController>(true)).Single();
            var data = new SerializedObject(controller);
            foreach (var field in new[] { "beginButton", "settingsButton", "exitButton", "settings", "menu" })
                Assert.That(data.FindProperty(field).objectReferenceValue, Is.Not.Null, field);
        }
        finally { EditorSceneManager.CloseScene(scene, true); }
    }

    [TestCase(1920, 1080)]
    [TestCase(1024, 768)]
    [TestCase(2560, 1080)]
    public void BackgroundCoversViewportAtBothParallaxExtremes(int width, int height)
    {
        Assert.That(PresentationType, Is.Not.Null);
        var method = PresentationType.GetMethod("CalculateCoverSize", BindingFlags.Public | BindingFlags.Static);
        var size = (Vector2)method.Invoke(null, new object[] { new Vector2(width, height), 16f / 9f, new Vector2(18, 10) });
        Assert.That(size.x, Is.GreaterThanOrEqualTo(width + 36));
        Assert.That(size.y, Is.GreaterThanOrEqualTo(height + 20));
        Assert.That(size.x / size.y, Is.EqualTo(16f / 9f).Within(.001));
    }

    [UnityTest]
    public IEnumerator SettingsCloseRestoresMenuAndStartLoadsBunker()
    {
        EditorSceneManager.OpenScene(ScenePath);
        EditorWindow.GetWindow(typeof(Editor).Assembly.GetType("UnityEditor.GameView")).Show();
        yield return new EnterPlayMode();
        yield return null;
        var controller = Object.FindFirstObjectByType<StartScreenController>();
        Assert.That(controller, Is.Not.Null);
        var data = new SerializedObject(controller);
        var settings = (AudioSettingsPanel)data.FindProperty("settings").objectReferenceValue;
        var menu = (CanvasGroup)data.FindProperty("menu").objectReferenceValue;
        var settingsButton = (Button)data.FindProperty("settingsButton").objectReferenceValue;
        var beginButton = (Button)data.FindProperty("beginButton").objectReferenceValue;
        System.IO.Directory.CreateDirectory("Artifacts/StartScreen");
        yield return null;
        ScreenCapture.CaptureScreenshot("Artifacts/StartScreen/menu-clear.png");
        yield return new WaitForSecondsRealtime(.4f);
        AssertReachable(beginButton);
        AssertReachable(settingsButton);
        settingsButton.onClick.Invoke();
        Assert.That(settings.IsOpen, Is.True);
        Assert.That(menu.interactable, Is.False);
        yield return null;
        ScreenCapture.CaptureScreenshot("Artifacts/StartScreen/menu-settings.png");
        yield return new WaitForSecondsRealtime(.4f);
        settings.Close();
        Assert.That(menu.interactable, Is.True);
        Assert.That(settings.IsOpen, Is.False);
        var fog = Object.FindObjectsByType<RawImage>(FindObjectsSortMode.None).Single(x => x.name == "Capsule condensation");
        Assert.That(fog.raycastTarget, Is.False);
        var atmosphere = fog.GetComponentInParent(PresentationType);
        var nextBreath = PresentationType.GetField("nextCondensation", BindingFlags.Instance | BindingFlags.NonPublic);
        nextBreath.SetValue(atmosphere, Time.unscaledTime - 3f);
        float previousTimeScale = Time.timeScale;
        Time.timeScale = 0;
        yield return null;
        yield return null;
        Assert.That(fog.color.a, Is.GreaterThan(.1f), "Condensation must animate even while time is paused");
        ScreenCapture.CaptureScreenshot("Artifacts/StartScreen/menu-condensation.png");
        yield return new WaitForSecondsRealtime(.4f);
        nextBreath.SetValue(atmosphere, Time.unscaledTime - 10f);
        yield return null;
        yield return null;
        Assert.That(fog.color.a, Is.Zero, "Glass must clear between breaths");
        Time.timeScale = previousTimeScale;
        beginButton.onClick.Invoke();
        float deadline = Time.realtimeSinceStartup + 30;
        while (SceneManager.GetActiveScene().name != "MainMenu" && Time.realtimeSinceStartup < deadline)
            yield return null;
        Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("MainMenu"));
        while (SceneTransitionOverlay.IsTransitioning && Time.realtimeSinceStartup < deadline)
            yield return null;
        Assert.That(SceneTransitionOverlay.IsTransitioning, Is.False, "Loading overlay must release input");
        // Shared cleanup unloads the bunker before persistent services are destroyed.
        // Exiting Play Mode directly destroys localization before the football HUD resets.
    }

    private static void AssertReachable(Button button)
    {
        var pointer = new PointerEventData(EventSystem.current)
        {
            position = RectTransformUtility.WorldToScreenPoint(null, button.transform.position)
        };
        var hits = new System.Collections.Generic.List<RaycastResult>();
        EventSystem.current.RaycastAll(pointer, hits);
        Assert.That(hits.Count, Is.GreaterThan(0), button.name);
        Assert.That(hits[0].gameObject.GetComponentInParent<Button>(), Is.EqualTo(button),
            "Background or decoration intercepted " + button.name);
    }
}
#endif
