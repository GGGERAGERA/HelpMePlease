using System.Reflection;
using System.IO;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public sealed class FootballHudVisibilityTests
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

    [TestCase(false, false)]
    [TestCase(true, true)]
    [TestCase(true, false)]
    public void AuthoredRecordPanelShowsRewardsWithoutOverflow(bool claimed, bool devReward)
    {
        var scene = EditorSceneManager.NewPreviewScene();
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/prefabs/MinigameArena_VS.prefab");
        var source = prefab.GetComponentInChildren<FootballMinigameHUD>(true);
        var instance = Object.Instantiate(source.gameObject);
        SceneManager.MoveGameObjectToScene(instance, scene);
        var cameraObject = new GameObject("Football HUD preview", typeof(Camera));
        SceneManager.MoveGameObjectToScene(cameraObject, scene);
        var camera = cameraObject.GetComponent<Camera>();
        var previousTexture = RenderTexture.active;
        var texture = new RenderTexture(1920, 1080, 24);
        Texture2D capture = null;
        try
        {
            var hud = instance.GetComponent<FootballMinigameHUD>();
            var canvas = instance.GetComponent<Canvas>();
            instance.GetComponent<CanvasScaler>().enabled = false;
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = camera;
            instance.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            instance.transform.localScale = Vector3.one;
            ((RectTransform)instance.transform).sizeDelta = new Vector2(1920, 1080);
            camera.scene = scene;
            camera.transform.position = new Vector3(0, 0, -10);
            camera.orthographic = true;
            camera.orthographicSize = 540;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.025f, .04f, .07f);
            camera.targetTexture = texture;
            hud.SetDevRecord(claimed);
            hud.SetGoalStats(6, 120);
            hud.ShowCompleted(470, 470, true, devReward);
            Canvas.ForceUpdateCanvases();
            var dev = (TMP_Text)typeof(FootballMinigameHUD).GetField("devRecordText", Private).GetValue(hud);
            var result = (TMP_Text)typeof(FootballMinigameHUD).GetField("resultText", Private).GetValue(hud);
            Assert.That(dev, Is.Not.Null, "Dev record must be wired in the shipping prefab");
            Assert.That(dev.text, Does.Contain("450"));
            Assert.That(dev.text, claimed ? Does.Contain("BEATEN") : Does.Contain("+200"));
            Assert.That(result.text, Does.Contain("+50"));
            if (devReward) Assert.That(result.text, Does.Contain("+200"));
            else Assert.That(result.text, Does.Not.Contain("+200"));
            foreach (var text in instance.GetComponentsInChildren<TMP_Text>())
            {
                text.ForceMeshUpdate();
                Assert.That(text.isTextOverflowing, Is.False, text.name);
            }
            var corners = new Vector3[4];
            var panel = (GameObject)typeof(FootballMinigameHUD).GetField("panelRoot", Private).GetValue(hud);
            ((RectTransform)panel.transform).GetWorldCorners(corners);
            foreach (var corner in corners)
            {
                var point = camera.WorldToViewportPoint(corner);
                Assert.That(point.x, Is.InRange(0f, 1f));
                Assert.That(point.y, Is.InRange(0f, 1f));
            }
            camera.Render();
            RenderTexture.active = texture;
            capture = new Texture2D(360, 1080, TextureFormat.RGB24, false);
            capture.ReadPixels(new Rect(1560, 0, 360, 1080), 0, 0);
            capture.Apply();
            const string output = "Artifacts/GeneratedQA/FootballRecords";
            Directory.CreateDirectory(output);
            File.WriteAllBytes($"{output}/panel-{claimed}-{devReward}.png", capture.EncodeToPNG());
        }
        finally
        {
            RenderTexture.active = previousTexture;
            camera.targetTexture = null;
            Object.DestroyImmediate(capture);
            Object.DestroyImmediate(texture);
            EditorSceneManager.ClosePreviewScene(scene);
        }
    }

    [TestCase(false)]
    [TestCase(true)]
    public void LeavingAfterRoundHidesResultsOnlyAfterLastPlayerCollider(bool newRecord)
    {
        var root = new GameObject("Football HUD test");
        root.SetActive(false); // Authored scene dependencies are not needed by the exit path.
        var player = new GameObject("Player");
        try
        {
            var game = root.AddComponent<FootballMinigame>();
            var hud = root.AddComponent<FootballMinigameHUD>();
            root.AddComponent<BoxCollider2D>().isTrigger = true;
            var area = root.AddComponent<FootballPlayerArea>();
            var panel = new GameObject("Results"); panel.transform.SetParent(root.transform);
            var mask = new GameObject("Viewport mask"); mask.transform.SetParent(root.transform);
            Set(hud, "panelRoot", panel);
            Set(hud, "viewportMaskRoot", mask);
            Set(game, "hud", hud);
            Set(game, "currentScore", 303);
            Set(game, "bestScore", 303);
            area.Configure(game);
            player.AddComponent<CharacterMovement2D>();
            var body = player.AddComponent<BoxCollider2D>();
            var feet = player.AddComponent<CircleCollider2D>();
            Call(area, "OnTriggerEnter2D", body);
            Call(area, "OnTriggerEnter2D", feet);

            // Completion returns to Idle while keeping the result panel on screen.
            Assert.That(game.State, Is.EqualTo(BunkerMinigameState.Idle));
            hud.ShowCompleted(game.Score, game.BestScore, newRecord);
            Assert.That(panel.activeSelf, Is.True);
            Call(area, "OnTriggerExit2D", body);
            Assert.That(panel.activeSelf, Is.True, "Player is still overlapping the arena");
            Call(area, "OnTriggerExit2D", feet);
            Assert.That(panel.activeSelf, Is.False);
            Assert.That(mask.activeSelf, Is.False);
            Assert.That(game.Score, Is.EqualTo(303));
            Assert.That(game.BestScore, Is.EqualTo(303));

            game.OnPlayerLeftArena(); // Repeated exits remain harmless.
            Assert.That(panel.activeSelf, Is.False);
            hud.ShowRunning(60, 0, game.BestScore);
            Assert.That(panel.activeSelf, Is.True, "The next round can display the HUD again");
        }
        finally
        {
            Object.DestroyImmediate(player);
            Object.DestroyImmediate(root);
        }
    }

    private static void Set(object target, string name, object value) =>
        target.GetType().GetField(name, Private).SetValue(target, value);
    private static void Call(object target, string name, Collider2D collider) =>
        target.GetType().GetMethod(name, Private).Invoke(target, new object[] { collider });
}
