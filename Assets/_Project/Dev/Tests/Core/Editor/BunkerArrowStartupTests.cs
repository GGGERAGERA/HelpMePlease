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

// Direct production startup, without preference overrides, teleporting or camera framing.
public sealed class BunkerArrowStartupTests
{
    private const string Output = "Artifacts/GeneratedQA/BunkerInteraction";

    [UnityTest]
    public IEnumerator StationHoverAndClickIgnorePlayerPositionAndOriginalArrowBobs()
    {
        EditorSceneManager.OpenScene(BunkerInteractionAuthoring.ScenePath);
        EditorWindow.GetWindow(typeof(Editor).Assembly.GetType("UnityEditor.GameView")).Focus();
        yield return new EnterPlayMode();
        yield return ExercisePointerAndArrow();
    }

    private static IEnumerator ExercisePointerAndArrow()
    {
        yield return AwaitGameplayPresentation();
        var follow = Object.FindFirstObjectByType<CameraFollow>();
        var camera = follow.ControlledCamera;
        Assert.That(follow.IsPresentationSuspended, Is.False);
        Assert.That(follow.target, Is.SameAs(PlayerRuntimeReference.PlayerTransform));
        Assert.That(camera.targetTexture, Is.Null, "Use the player's Game View camera.");
        yield return WaitForRunningTime(1f);

        var arrow = Object.FindObjectsByType<BunkerInteractionArrow>(FindObjectsSortMode.None)
            .Single(a => a.Source is BunkerStation && a.Source.name == "p_bunkerUpgrade1");
        Assert.That(((IBunkerInteractable)arrow.Source).CanInteract, Is.True);
        Assert.That(Vector2.Distance(PlayerRuntimeReference.PlayerTransform.position, arrow.Source.transform.position), Is.GreaterThan(6f));
        var cursor = Object.FindFirstObjectByType<BunkerCursorInteractor>();
        cursor.enabled = false; // Drive the existing pointer path without OS mouse updates replacing the test position.
        cursor.UpdateHoverAt(arrow.ClickPosition);
        Assert.That(arrow.IsHovered, Is.True, "Hover must select the arrow independently of player position.");
        Assert.That(arrow.CanInteractNow, Is.True);
        var visual = arrow.GetComponentInChildren<SpriteRenderer>(true);
        var animation = arrow.GetComponent<Animator>();
        Assert.That(animation.runtimeAnimatorController, Is.SameAs(AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(
            "Assets/_Project/prefabs/Bunker/PF_InteractionArrow.controller")));
        Directory.CreateDirectory(Output);
        var proof = new System.Text.StringBuilder();
        proof.AppendLine("project=" + Application.dataPath + " pid=" + System.Diagnostics.Process.GetCurrentProcess().Id
            + " scene=" + arrow.gameObject.scene.path + " camera=" + BunkerInteractionAuthoring.PathOf(camera.transform)
            + " player=" + PlayerRuntimeReference.PlayerTransform.position + " cameraSuspended=" + follow.IsPresentationSuspended);
        float minimumY = float.MaxValue, maximumY = float.MinValue;
        for (int frame = 0; frame < 3; frame++)
        {
            if (frame > 0) yield return WaitForRunningTime(.65f);
            string path = Output + "/production-startup-" + frame + ".png";
            if (File.Exists(path)) File.Delete(path);
            float y = visual.transform.localPosition.y;
            minimumY = Mathf.Min(minimumY, y);
            maximumY = Mathf.Max(maximumY, y);
            proof.AppendLine("capture=" + path + " time=" + Time.time + " frame=" + Time.frameCount
                + " objectActive=" + arrow.gameObject.activeInHierarchy + " rendererEnabled=" + visual.enabled
                + " visualY=" + y + " cameraPosition=" + camera.transform.position + " zoom=" + camera.orthographicSize);
            ScreenCapture.CaptureScreenshot(Path.GetFullPath(path));
            yield return CoreTestSupport.Await(() => File.Exists(path));
            File.WriteAllText(Output + "/production-startup-proof.txt", proof.ToString());
            Assert.That(visual.enabled, Is.True, "Available station marker must render independently of player position.");
            var pixels = new Texture2D(2, 2);
            try
            {
                Assert.That(pixels.LoadImage(File.ReadAllBytes(path)), Is.True);
                Vector3 min = camera.WorldToViewportPoint(visual.bounds.min);
                Vector3 max = camera.WorldToViewportPoint(visual.bounds.max);
                Assert.That(min.z, Is.GreaterThan(0));
                Assert.That(min.x, Is.InRange(0f, 1f));
                Assert.That(max.y, Is.InRange(0f, 1f));
                int cyan = 0;
                for (int py = Mathf.Max(0, Mathf.FloorToInt(min.y * pixels.height)); py < Mathf.Min(pixels.height, Mathf.CeilToInt(max.y * pixels.height)); py++)
                for (int px = Mathf.Max(0, Mathf.FloorToInt(min.x * pixels.width)); px < Mathf.Min(pixels.width, Mathf.CeilToInt(max.x * pixels.width)); px++)
                {
                    Color color = pixels.GetPixel(px, py);
                    if (color.g > .6f && color.b > .7f && color.r < color.g * .8f) cyan++;
                }
                Assert.That(cyan, Is.GreaterThan(30), "Final Game View PNG must contain the cyan marker at the station.");
                proof.AppendLine("cyanPixels=" + cyan);
            }
            finally { Object.DestroyImmediate(pixels); }
        }
        Assert.That(maximumY - minimumY, Is.GreaterThan(.06f), "The original arrow must bob between rendered frames.");
        File.WriteAllText(Output + "/production-startup-proof.txt", proof.ToString());
        var bodyProxy = arrow.Source.GetComponentsInChildren<BunkerInteractableCollider>()
            .First(p => p.GetComponent<BunkerInteractionArrow>() == null && p.GetComponent<Collider2D>().enabled);
        cursor.UpdateHoverAt(bodyProxy.GetComponent<Collider2D>().bounds.center);
        Assert.That(typeof(BunkerCursorInteractor).GetField("current", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(cursor), Is.SameAs(bodyProxy),
            "Body hover must select its collider: " + BunkerInteractionAuthoring.PathOf(bodyProxy.transform)
            + " interactable=" + bodyProxy.Interactable + " hoverable=" + bodyProxy.Hoverable);
        Assert.That(arrow.Source.GetComponentsInChildren<SpriteRenderer>()
            .Any(r => r.sharedMaterial.HasProperty("_EnableOutline") && r.sharedMaterial.GetFloat("_EnableOutline") > 0f)
            || arrow.Source.GetComponentsInChildren<LineRenderer>().Any(r => r.enabled), Is.True,
            "Hovering the station body must enable a rendered outline independently of player position.");
        string hoverPath = Output + "/unrestricted-body-hover.png";
        if (File.Exists(hoverPath)) File.Delete(hoverPath);
        ScreenCapture.CaptureScreenshot(Path.GetFullPath(hoverPath));
        yield return CoreTestSupport.Await(() => File.Exists(hoverPath));
        var panels = Object.FindFirstObjectByType<BunkerPanelManager>();
        Assert.That(cursor.TryInteractAt(bodyProxy.GetComponent<Collider2D>().bounds.center), Is.True);
        yield return null;
        Assert.That(panels.IsAnyPanelOpen, Is.True, "Clicking the station body opens its existing window from the startup position.");
        Assert.That(visual.enabled, Is.False, "Modal UI still hides world markers.");
        Assert.That(arrow.CanInteractNow, Is.False);
        panels.CloseAll(false);
        yield return null;
        Assert.That(visual.enabled, Is.True, "Closing the modal restores the marker.");
        cursor.UpdateHoverAt(arrow.ClickPosition);
        Assert.That(arrow.IsHovered, Is.True);
        Assert.That(cursor.TryInteractAt(arrow.ClickPosition), Is.True, "The arrow uses the same unrestricted click path as the station body.");
        yield return null;
        Assert.That(panels.IsAnyPanelOpen, Is.True);
        string openedPath = Output + "/unrestricted-click-open.png";
        if (File.Exists(openedPath)) File.Delete(openedPath);
        ScreenCapture.CaptureScreenshot(Path.GetFullPath(openedPath));
        yield return CoreTestSupport.Await(() => File.Exists(openedPath));
        panels.CloseAll(false);
        yield return null;
        var source = arrow.Source;
        source.enabled = false;
        yield return null;
        Assert.That(visual.enabled, Is.False, "Unavailable stations have no marker.");
        source.enabled = true;
        yield return null;
        Assert.That(visual.enabled, Is.True);
        var characters = Object.FindObjectsByType<BunkerInteractionArrow>(FindObjectsSortMode.None)
            .Single(a => a.Source.name == "p_bunkerPlayerShowcase 1");
        Assert.That(Vector2.Distance(PlayerRuntimeReference.PlayerTransform.position, characters.Source.transform.position), Is.GreaterThan(10f));
        var characterBody = characters.Source.GetComponentsInChildren<BunkerInteractableCollider>()
            .First(p => p.GetComponent<BunkerInteractionArrow>() == null && p.GetComponent<Collider2D>().enabled);
        var characterPoint = (Vector2)characterBody.GetComponent<Collider2D>().bounds.center;
        cursor.UpdateHoverAt(characterPoint);
        Assert.That(characters.Source.GetComponentsInChildren<SpriteRenderer>()
            .Any(r => r.sharedMaterial.HasProperty("_EnableOutline") && r.sharedMaterial.GetFloat("_EnableOutline") > 0f)
            || characters.Source.GetComponentsInChildren<LineRenderer>().Any(r => r.enabled), Is.True,
            "The character station from the reported scenario must highlight without approaching it.");
        Assert.That(cursor.TryInteractAt(characterPoint), Is.True);
        yield return null;
        Assert.That(panels.IsAnyPanelOpen, Is.True);
        string characterPath = Output + "/unrestricted-character-open.png";
        if (File.Exists(characterPath)) File.Delete(characterPath);
        ScreenCapture.CaptureScreenshot(Path.GetFullPath(characterPath));
        yield return CoreTestSupport.Await(() => File.Exists(characterPath));
        panels.CloseAll(false);
        yield return null;
        File.AppendAllText(Output + "/production-startup-proof.txt", "body and arrow hover/click independent of player position; modal hide/restore; source disable/enable: PASS\n");
    }

    [UnityTearDown]
    public IEnumerator ExitProductionPlayMode()
    {
        if (EditorApplication.isPlaying) yield return new ExitPlayMode();
    }

    [UnityTest]
    public IEnumerator SceneTransitionRebindsOriginalMarkersToTheNewPlayer()
    {
        EditorSceneManager.OpenScene(BunkerInteractionAuthoring.ScenePath);
        yield return new EnterPlayMode();
        yield return ExerciseReentry();
    }

    private static IEnumerator ExerciseReentry()
    {
        yield return AwaitGameplayPresentation();
        var previous = ProductionSceneComposition.Active;
        Assert.That(SceneTransitionOverlay.Load(RunEndService.BunkerSceneName), Is.True);
        yield return CoreTestSupport.Await(() => ProductionSceneComposition.Active != previous
            && ProductionSceneComposition.Active is { IsReady: true } && !SceneTransitionOverlay.IsTransitioning);
        yield return AwaitGameplayPresentation();
        var arrows = Object.FindObjectsByType<BunkerInteractionArrow>(FindObjectsSortMode.None);
        Assert.That(arrows.Length, Is.GreaterThanOrEqualTo(11));
        foreach (var arrow in arrows)
        {
            Assert.That(arrow.IsVisible, Is.EqualTo(((IBunkerInteractable)arrow.Source).CanInteract));
            Assert.That(arrow.GetComponent<Animator>().runtimeAnimatorController.name, Is.EqualTo("PF_InteractionArrow"));
        }
        Assert.That(Object.FindFirstObjectByType<CameraFollow>().target, Is.SameAs(PlayerRuntimeReference.PlayerTransform));
    }

    private static IEnumerator AwaitGameplayPresentation()
    {
        float deadline = Time.realtimeSinceStartup + 120f;
        while (!(ProductionSceneComposition.Active is { IsReady: true }
            && Object.FindFirstObjectByType<BunkerPlayerLoadoutController>() is { IsReady: true }
            && !SceneTransitionOverlay.IsTransitioning
            && Object.FindFirstObjectByType<BunkerIntroController>() is not { IsPlaying: true }
            && Object.FindFirstObjectByType<CameraFollow>() is { isActiveAndEnabled: true }))
        {
            Assert.That(Time.realtimeSinceStartup, Is.LessThan(deadline), "Production gameplay presentation did not become ready, including the intro.");
            yield return null;
        }
    }

    [UnityTest]
    public IEnumerator GallerySwitchesOnlyOnPhysicalBallImpact()
    {
        EditorSceneManager.OpenScene(BunkerInteractionAuthoring.ScenePath);
        EditorWindow.GetWindow(typeof(Editor).Assembly.GetType("UnityEditor.GameView")).Focus();
        yield return new EnterPlayMode();
        yield return ExerciseGalleryBall();
    }

    private static IEnumerator ExerciseGalleryBall()
    {
        yield return AwaitGameplayPresentation();
        var gallery = Object.FindFirstObjectByType<Subject42.Bunker.Gallery.EnemyGalleryController>();
        var cursor = Object.FindFirstObjectByType<BunkerCursorInteractor>();
        cursor.enabled = false;
        int initial = gallery.CurrentIndex;
        foreach (var arrow in Object.FindObjectsByType<BunkerInteractionArrow>(FindObjectsSortMode.None).Where(a => a.Source == gallery))
            Assert.That(cursor.TryInteractAt(arrow.ClickPosition), Is.False, "Gallery selectors must never switch from a pointer click.");
        Assert.That(((object)gallery) is IBunkerInteractable, Is.False, "The physical gallery target does not implement pointer/keyboard interaction.");
        Assert.That(gallery.GetComponentsInChildren<BunkerInteractionArrow>(true), Is.Empty);
        Assert.That(cursor.TryInteractAt(gallery.GetComponent<Collider2D>().bounds.center), Is.False);
        yield return WaitForRunningTime(.3f);
        Assert.That(gallery.CurrentIndex, Is.EqualTo(initial));
        Assert.That(gallery.GetComponent<Collider2D>().isTrigger, Is.False);

        var follow = Object.FindFirstObjectByType<CameraFollow>();
        follow.BeginTemporaryFocus(gallery, gallery.transform, 1f, 1f, .2f);
        yield return WaitForRunningTime(1.2f);
        Assert.That(follow.IsPresentationSuspended, Is.False);
        Directory.CreateDirectory(Output);
        string before = Output + "/gallery-ball-before.png";
        if (File.Exists(before)) File.Delete(before);
        ScreenCapture.CaptureScreenshot(Path.GetFullPath(before));
        yield return CoreTestSupport.Await(() => File.Exists(before));

        var ball = Object.FindObjectsByType<BallRollVisual>(FindObjectsSortMode.None)
            .Where(b => b.name == "EnemyGalleryBall")
            .OrderBy(b => Mathf.Abs(b.transform.position.x - gallery.transform.position.x)).First();
        var startPosition = ball.transform.position;
        ball.Kick((Vector2)gallery.transform.position - (Vector2)ball.transform.position, ball.maxPower);
        yield return CoreTestSupport.Await(() => gallery.CurrentIndex != initial);
        Assert.That(gallery.CurrentIndex, Is.EqualTo((initial + 1) % 5), "A real solid-ball collision advances exactly one entry.");
        string after = Output + "/gallery-ball-after.png";
        if (File.Exists(after)) File.Delete(after);
        ScreenCapture.CaptureScreenshot(Path.GetFullPath(after));
        yield return CoreTestSupport.Await(() => File.Exists(after));
        File.WriteAllText(Output + "/gallery-ball-proof.txt", "project=" + Application.dataPath
            + " pid=" + System.Diagnostics.Process.GetCurrentProcess().Id + " scene=" + gallery.gameObject.scene.path
            + " camera=" + BunkerInteractionAuthoring.PathOf(follow.ControlledCamera.transform)
            + " pointerClick=false pageBefore=" + initial + " pageAfter=" + gallery.CurrentIndex
            + " ballStart=" + startPosition + " ballContact=" + ball.transform.position
            + " cameraUsesTemporaryFocus=true; actual BallRollVisual.Kick and Physics2D collision; no direct gallery callback invocation");
        follow.EndTemporaryFocus(gallery, .2f);
    }

    private static IEnumerator WaitForRunningTime(float seconds)
    {
        float until = Time.time + seconds;
        yield return CoreTestSupport.Await(() => Time.time >= until);
    }
}
#endif
