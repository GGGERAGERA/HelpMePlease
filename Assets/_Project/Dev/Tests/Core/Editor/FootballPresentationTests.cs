#if UNITY_EDITOR
using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

public sealed class FootballPresentationTests
{
    private const string Output = "Artifacts/GeneratedQA/FootballPresentation";

    [Test]
    public void FootballHudUsesReusableAuthoredPanel()
    {
        var panel = AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/_Project/prefabs/Bunker/Minigames/PF_MinigameSidePanel.prefab");
        Assert.That(panel, Is.Not.Null, "The shared authored side panel must exist.");
        var hud = AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/_Project/prefabs/Bunker/Minigames/PF_FootballHUD.prefab");
        var presenter = new SerializedObject(hud.GetComponent<FootballMinigameHUD>());
        var view = presenter.FindProperty("view").objectReferenceValue as MinigameSidePanelView;
        Assert.That(view, Is.Not.Null);
        Assert.That(PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(view.gameObject),
            Is.EqualTo("Assets/_Project/prefabs/Bunker/Minigames/PF_MinigameSidePanel.prefab"));
    }

    [Test]
    public void SharedPanelCanRenderAnotherGameWithoutFootballPresenter()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/_Project/prefabs/Bunker/Minigames/PF_MinigameSidePanel.prefab");
        var panel = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        try
        {
            var view = panel.GetComponent<MinigameSidePanelView>();
            view.SetContent("RELAY", "POINTS", "BEST", "TIME", "E / INTERACT");
            view.SetValues("12.5", "125", "250");
            view.SetStatus("ACTIVE");
            view.SetExtraSection(string.Empty, string.Empty, false);
            view.SetVisible(true);
            var texts = panel.GetComponentsInChildren<TMPro.TMP_Text>(true).ToDictionary(t => t.name);
            Assert.That(texts["PanelTitle"].text, Is.EqualTo("RELAY"));
            Assert.That(texts["ScoreValue"].text, Is.EqualTo("125"));
            Assert.That(texts["BestValue"].text, Is.EqualTo("250"));
            Assert.That(texts["TimeValue"].text, Is.EqualTo("12.5"));
            Assert.That(texts["ControlsText"].text, Is.EqualTo("E / INTERACT"));
            Assert.That(panel.transform.Find("ScoreLegend").gameObject.activeSelf, Is.False);
            view.SetVisible(false);
            Assert.That(panel.activeSelf, Is.False);
        }
        finally { Object.DestroyImmediate(panel); }
    }

    [UnityTest]
    public IEnumerator CaptureProductionStartPresentation()
    {
        CoreTestSupport.PreservePreferences();
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
        yield return new EnterPlayMode();
        yield return CoreTestSupport.LoadBunker();
        yield return CaptureRound();
    }

    [UnityTearDown]
    public IEnumerator CleanupAfterFailure() => CoreTestSupport.CleanupPlayMode();

    [UnityTest]
    public IEnumerator ViewportBarsClearPreviousFrame()
    {
        CoreTestSupport.PreservePreferences();
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
        yield return new EnterPlayMode();
        yield return CoreTestSupport.LoadBunker();
        yield return VerifyViewportBars();
    }

    private static IEnumerator VerifyViewportBars()
    {
        Directory.CreateDirectory(Output);
        var game = Object.FindFirstObjectByType<FootballMinigame>();
        var camera = Camera.main;
        var previousTarget = camera.targetTexture;
        var previousActive = RenderTexture.active;
        var target = RenderTexture.GetTemporary(1200, 800, 24);
        var pixels = new Texture2D(1200, 800, TextureFormat.RGB24, false);
        var originalRect = camera.rect;
        try
        {
            // Reuse one target just as Game View does: first draw the wider view.
            camera.targetTexture = target;
            game.StartStation.Interact();
            yield return null;
            for (int run = 0; run < 2; run++)
            {
                RenderTexture.active = target;
                GL.Clear(true, true, Color.magenta); // unmistakable previous-frame contents
                camera.Render();
                RenderTexture.active = target;
                pixels.ReadPixels(new Rect(0, 0, 1200, 800), 0, 0);
                pixels.Apply();
                File.WriteAllBytes(Output + $"/viewport-bars-{run}.png", pixels.EncodeToPNG());
                var rect = camera.rect;
                Assert.That(rect.width < .999f || rect.height < .999f, Is.True);
                var sample = rect.x > 0f ? pixels.GetPixel(1, 400) : pixels.GetPixel(600, 1);
                Assert.That(sample.maxColorComponent, Is.LessThan(.01f),
                    $"Outside the football viewport must be black, not a retained previous frame: {sample}.");
                game.CancelCurrentRound();
                Assert.That(camera.rect, Is.EqualTo(originalRect));
                Assert.That(game.CanStart, Is.True);
                if (run == 0) game.StartStation.Interact();
            }
        }
        finally
        {
            game.CancelCurrentRound();
            camera.targetTexture = previousTarget;
            RenderTexture.active = previousActive;
            RenderTexture.ReleaseTemporary(target);
            Object.Destroy(pixels);
        }
    }

    private static IEnumerator CaptureRound()
    {
        Directory.CreateDirectory(Output);
        var game = Object.FindFirstObjectByType<FootballMinigame>();
        Assert.That(game, Is.Not.Null);
        Assert.That(game.StartStation.CanInteract, Is.True);
        File.WriteAllText(Output + "/arena-plane.txt",
            $"Arena: {game.PlayBounds}; player before: {PlayerRuntimeReference.CachedPlayer.transform.position}");
        var camera = Camera.main;
        Capture(camera, "before-start");
        game.FrameCamera();
        yield return new WaitForSecondsRealtime(.5f);
        Capture(camera, "before-framed");
        var before = Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None)
            .Where(r => r.enabled && r.gameObject.activeInHierarchy).Select(r => r.GetInstanceID()).ToHashSet();
        var canvasesBefore = Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None)
            .Where(c => c.enabled && c.gameObject.activeInHierarchy).Select(c => c.GetInstanceID()).ToHashSet();
        game.StartStation.Interact();
        yield return new WaitForSecondsRealtime(.5f);
        Assert.That(game.IsRunning, Is.True);
        File.AppendAllText(Output + "/arena-plane.txt",
            $"; player after: {PlayerRuntimeReference.CachedPlayer.transform.position}");
        Capture(camera, "after-start");
        // Schedule capture at Unity's real render boundary, rather than the
        // Editor test runner's simulated WaitForEndOfFrame continuation.
        string gameViewPath = Output + "/game-view-viewport-fixed.png";
        if (File.Exists(gameViewPath)) File.Delete(gameViewPath);
        ScreenCapture.CaptureScreenshot(gameViewPath);
        yield return CoreTestSupport.Await(() => File.Exists(gameViewPath));
        var enabled = Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None)
            .Where(r => r.enabled && r.gameObject.activeInHierarchy && !before.Contains(r.GetInstanceID())).ToArray();
        File.WriteAllLines(Output + "/enabled-renderers.txt", enabled.Select(r =>
            $"{Path(r.transform)} | {r.GetType().Name} | {r.bounds}"));
        File.WriteAllLines(Output + "/enabled-canvases.txt", Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None)
            .Where(c => c.enabled && c.gameObject.activeInHierarchy && !canvasesBefore.Contains(c.GetInstanceID()))
            .Select(c => Path(c.transform)));
        var gravityVisuals = enabled.Where(r => r.transform.name == "Pixel Polarity").ToArray();
        foreach (var visual in gravityVisuals) visual.enabled = false;
        Capture(camera, "after-gravity-hidden");
        foreach (var visual in gravityVisuals) visual.enabled = true;
        File.WriteAllText(Output + "/gravity-visual-count.txt", gravityVisuals.Length.ToString());
        Assert.That(gravityVisuals.Length, Is.EqualTo(2));
        // The presenter belongs to the authored arena, alongside its gameplay owner.
        var hud = game.transform.parent.GetComponentInChildren<FootballMinigameHUD>(true);
        var texts = hud.GetComponentsInChildren<TMPro.TMP_Text>(true).ToDictionary(t => t.name);
        hud.ShowRunning(7.5f, 42, 1234);
        Assert.That(texts["ScoreValue"].text, Is.EqualTo("42"));
        Assert.That(texts["BestValue"].text, Is.EqualTo("1234"));
        Assert.That(texts["TimeValue"].text, Is.EqualTo("7.5"));
        hud.ShowCompleted(42, 1234, false);
        Assert.That(hud.GetComponentInChildren<MinigameSidePanelView>(true).gameObject.activeSelf, Is.False);
        hud.ShowIdle(60f, 1234);
        Assert.That(texts["ScoreValue"].text, Is.EqualTo("0"));
        game.CancelCurrentRound();
        Assert.That(game.IsRunning, Is.False);
        game.StartStation.Interact();
        Assert.That(game.IsRunning, Is.True);
        game.CancelCurrentRound();
    }

    private static string Path(Transform item) => item.parent == null ? item.name : Path(item.parent) + "/" + item.name;

    private static void Capture(Camera camera, string name)
    {
        var previous = camera.targetTexture;
        var active = RenderTexture.active;
        int height = Mathf.Max(1, Mathf.RoundToInt(1200f * Screen.height / Mathf.Max(1, Screen.width)));
        var target = RenderTexture.GetTemporary(1200, height, 24);
        var texture = new Texture2D(1200, height, TextureFormat.RGB24, false);
        try
        {
            camera.targetTexture = target;
            camera.Render();
            RenderTexture.active = target;
            texture.ReadPixels(new Rect(0, 0, 1200, height), 0, 0);
            texture.Apply();
            File.WriteAllBytes(Output + "/" + name + ".png", texture.EncodeToPNG());
        }
        finally
        {
            camera.targetTexture = previous;
            RenderTexture.active = active;
            RenderTexture.ReleaseTemporary(target);
            Object.Destroy(texture);
        }
    }
}
#endif
