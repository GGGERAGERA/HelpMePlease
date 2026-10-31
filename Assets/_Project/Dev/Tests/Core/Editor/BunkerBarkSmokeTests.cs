#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public sealed class BunkerBarkSmokeTests
{
    public const string Output = "Artifacts/GeneratedQA/BunkerBarks";
    private const string Running = "Subject42.BunkerBarks.Tests";
    private const string AnyRun = "Subject42.Verification.AnyRunActive";

    static BunkerBarkSmokeTests()
    {
        ScriptableObject.CreateInstance<TestRunnerApi>().RegisterCallbacks(new Results());
        EditorApplication.update += () =>
        {
            string request = Output + "/run-tests.request";
            if (!File.Exists(request) || EditorApplication.isCompiling || EditorApplication.isUpdating ||
                EditorApplication.isPlayingOrWillChangePlaymode || SessionState.GetBool(AnyRun, false)) return;
            File.Delete(request);
            SessionState.SetBool(Running, true);
            SessionState.SetBool(AnyRun, true);
            ScriptableObject.CreateInstance<TestRunnerApi>().Execute(new ExecutionSettings(new Filter
            { testMode = UnityEditor.TestTools.TestRunner.Api.TestMode.EditMode, testNames = new[] { nameof(BunkerBarkSmokeTests) } }));
        };
    }

    [Test]
    public void MainMenuHasAuthoredBarksAndRealRoomTriggers()
    {
        var scene = EditorSceneManager.OpenScene("Assets/_Project/Scenes/MainBuild/MainMenu.unity");
        var components = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<MonoBehaviour>(true)).Where(c => c != null).ToArray();
        Assert.That(components.Count(c => c.GetType().Name == "BunkerAmbientBarks"), Is.EqualTo(1), "MainMenu needs an authored ambient bark controller.");
        Assert.That(components.Count(c => c.GetType().Name == "BunkerBarkRoomTrigger"), Is.GreaterThanOrEqualTo(3));
        var barks = components.OfType<BunkerAmbientBarks>().Single();
        var data = new SerializedObject(barks);
        foreach (string property in new[] { "player", "view", "panels" })
            Assert.That(data.FindProperty(property).objectReferenceValue, Is.Not.Null, property);
        Assert.That(data.FindProperty("lines").arraySize, Is.InRange(10, 15));
        Assert.That(data.FindProperty("duration").floatValue, Is.EqualTo(3.6f));
        Assert.That(data.FindProperty("blockingInterfaces").arraySize, Is.GreaterThanOrEqualTo(2));
        var zones = components.OfType<BunkerBarkRoomTrigger>().Select(t => t.GetComponent<BoxCollider2D>()).ToArray();
        foreach (var zone in zones) Assert.That(zone.isTrigger, Is.True);
        for (int i = 0; i < zones.Length; i++)
            for (int j = i + 1; j < zones.Length; j++)
                Assert.That(zones[i].bounds.Intersects(zones[j].bounds), Is.False, "Room triggers must not overlap.");
    }

    [SetUp] public void Preserve() => CoreTestSupport.PreservePreferences();
    [UnityTearDown] public IEnumerator Cleanup() => CoreTestSupport.CleanupPlayMode();

    [UnityTest]
    public IEnumerator BubbleRoomEntryEventAndMenuSmoke()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
        EditorWindow.GetWindow(typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView")).Focus();
        yield return new EnterPlayMode();
        Application.runInBackground = true;
        PlayerPrefs.SetInt(BunkerIntroController.ViewedPreferenceKey, 1);
        yield return CoreTestSupport.LoadBunker();
        // Allocate captured locals after EnterPlayMode's domain reload.
        yield return CheckLoadedBunker();
    }

    private static IEnumerator CheckLoadedBunker()
    {
        yield return null;
        var barks = Object.FindFirstObjectByType<BunkerAmbientBarks>();
        Assert.That(barks, Is.Not.Null);
        Assert.That(barks.IsBlocked, Is.False);
        var view = Object.FindFirstObjectByType<BunkerBarkView>(FindObjectsInactive.Include);
        Assert.That(view, Is.Not.Null, "Scene view component");
        Assert.That(view.gameObject.activeInHierarchy, Is.True, "Bark canvas must be active");
        var group = view.GetComponentInChildren<CanvasGroup>();
        Assert.That(group, Is.Not.Null, "Pixel bubble CanvasGroup");
        Assert.That(barks.Player, Is.Not.Null, "Controlled player");
        var movement = barks.Player.GetComponent<CharacterMovement2D>();
        Assert.That(movement, Is.Not.Null, "Player movement component");
        Assert.That(movement.isActiveAndEnabled, Is.True);

        // Force only the test's timer; production Inspector stays at 60–120 s.
        Set(barks, "randomRemaining", 0f);
        yield return null;
        Assert.That(barks.IsSpeaking, Is.True);
        string random = barks.CurrentText;
        var trigger = Object.FindObjectsByType<BunkerBarkRoomTrigger>(FindObjectsSortMode.None).Single(t => t.RoomId == "chill");
        var zone = trigger.GetComponent<BoxCollider2D>();
        var body = barks.Player.GetComponent<Rigidbody2D>();
        body.position = FindOpenSpot(zone, barks.Player);
        Physics2D.SyncTransforms();
        yield return new WaitForFixedUpdate();
        yield return new WaitForFixedUpdate();
        yield return CoreTestSupport.Await(() => group.alpha > .95f);
        Assert.That(barks.CurrentText, Is.Not.EqualTo(random), "Physical entry must preempt a random line.");
        Assert.That(new[] { "Диван всё ещё на моей стороне.", "Можно просто посидеть. Без причины." }, Does.Contain(barks.CurrentText));
        Assert.That(group.alpha, Is.GreaterThan(.95f));
        Assert.That(barks.TryPlayEvent("story_reflection"), Is.False, "Room has priority.");
        Assert.That(barks.TryEnterRoom("chill"), Is.False, "No threshold spam.");
        Assert.That(Read<float>(barks, "randomRemaining"), Is.InRange(59f, 120f));
        Assert.That(movement.isActiveAndEnabled, Is.True);
        Vector2 beforeMove = body.position;
        movement.MovementIntent = () => Vector2.right;
        for (int i = 0; i < 12; i++) yield return new WaitForFixedUpdate();
        movement.MovementIntent = null;
        Assert.That(Vector2.Distance(beforeMove, body.position), Is.GreaterThan(.05f),
            $"Speech must allow movement: {beforeMove} -> {body.position}, speed={movement.speed}, step={Time.fixedDeltaTime}, simulated={body.simulated}.");
        Directory.CreateDirectory(Output);
        yield return null;
        ScreenCapture.CaptureScreenshot(Output + "/room.png");
        yield return CoreTestSupport.Await(() => !barks.IsSpeaking);
        Assert.That(barks.IsSpeaking, Is.False, "3.6 s lifetime includes fades.");
        Assert.That(group.alpha, Is.Zero);

        Assert.That(barks.TryPlayEvent("story_reflection"), Is.True);
        yield return CoreTestSupport.Await(() => group.alpha > .95f);
        Assert.That(barks.CurrentText, Is.EqualTo("Ладно. Посмотрим, что дальше."));
        yield return null;
        ScreenCapture.CaptureScreenshot(Output + "/event.png");
        yield return new WaitForSecondsRealtime(.15f); // Capture is asynchronous; keep this frame unobstructed.
        var panels = Object.FindFirstObjectByType<BunkerPanelManager>();
        panels.OpenSettings();
        yield return null;
        Assert.That(barks.IsSpeaking, Is.False);
        Assert.That(group.alpha, Is.Zero);
        Assert.That(barks.TryPlayEvent("return_from_run"), Is.False);
        ScreenCapture.CaptureScreenshot(Output + "/menu.png");
        yield return new WaitForSecondsRealtime(.15f);
        panels.CloseAll(false);
        yield return null;
        Assert.That(barks.IsSpeaking, Is.False, "No stale speech on closing a menu.");
        Assert.That(barks.TryPlayEvent("story_reflection"), Is.False, "No immediate repeat even after interruption.");
        Assert.That(barks.TrySpeak("Тихо. Кто-то здесь есть.", trigger.transform), Is.True, "Future NPC API.");
        File.WriteAllText(Output + "/smoke.txt", "PASS: random -> physical room entry with priority; fade/lifetime; event API; menu suppression; repeat guard; NPC speaker; movement enabled.\n");
    }

    private static void Set(BunkerAmbientBarks owner, string field, object value) =>
        typeof(BunkerAmbientBarks).GetField(field, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).SetValue(owner, value);
    private static T Read<T>(BunkerAmbientBarks owner, string field) =>
        (T)typeof(BunkerAmbientBarks).GetField(field, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(owner);

    private static Vector2 FindOpenSpot(BoxCollider2D zone, Transform player)
    {
        // Room centres can be occupied by the artist-authored tables/sofas.
        Bounds bounds = zone.bounds;
        for (float y = bounds.min.y + 2f; y < bounds.max.y - 2f; y += 2f)
            for (float x = bounds.min.x + 2f; x < bounds.max.x - 2f; x += 2f)
            {
                var point = new Vector2(x, y);
                if (Physics2D.OverlapBoxAll(point, new Vector2(3f, 2f), 0f)
                    .All(c => c.isTrigger || c.transform.IsChildOf(player))) return point;
            }
        Assert.Fail("No clear walking area inside the production room trigger.");
        return bounds.center;
    }

    private sealed class Results : IErrorCallbacks
    {
        public void RunStarted(ITestAdaptor tests) { }
        public void TestStarted(ITestAdaptor test) { }
        public void TestFinished(ITestResultAdaptor result) { }
        public void RunFinished(ITestResultAdaptor result)
        {
            if (!SessionState.GetBool(Running, false)) return;
            SessionState.EraseBool(Running);
            SessionState.EraseBool(AnyRun);
            Directory.CreateDirectory(Output);
            TestRunnerApi.SaveResultToFile(result, Output + "/results.xml");
        }
        public void OnError(string message)
        {
            if (!SessionState.GetBool(Running, false)) return;
            SessionState.EraseBool(Running);
            SessionState.EraseBool(AnyRun);
            Directory.CreateDirectory(Output);
            File.WriteAllText(Output + "/error.txt", message);
        }
    }
}
#endif
