#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

// A single short production MVP check. No lab, bot or batch runner.
public sealed class OrbitalRelayPresentationTests
{
    public const string Output = "Artifacts/GeneratedQA/OrbitalRelay/Presentation";
    [Serializable] private sealed class Preference { public string key; public bool exists; public int value; }
    [Serializable] private sealed class Preferences { public Preference[] values; }
    [SetUp] public void Setup() => CoreTestSupport.PreservePreferences();
    [UnityTearDown] public IEnumerator Cleanup()
    {
        if (Application.isPlaying) { Time.timeScale = 1; yield return new ExitPlayMode(); }
        string saved = SessionState.GetString("Subject42.Core.Preferences", "");
        if (saved.Length > 0)
            foreach (var p in JsonUtility.FromJson<Preferences>(saved).values)
                if (p.exists) PlayerPrefs.SetInt(p.key, p.value); else PlayerPrefs.DeleteKey(p.key);
        PlayerPrefs.Save(); SessionState.EraseString("Subject42.Core.Preferences");
    }

    [UnityTest, Timeout(120000)] public IEnumerator AuthoredVisualsAndAllExitPathsInMVP()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<OrbitalRelayEvent>(OrbitalRelayAuthoring.PrefabPath);
        Assert.That(prefab.transform.Find("ArenaVisual"), Is.Not.Null, "Production arena visual is missing.");
        EditorSceneManager.OpenScene("Assets/_Project/Scenes/MainBuild/MVP.unity");
        yield return new EnterPlayMode();
        yield return ExerciseMVP();
    }
    private static IEnumerator ExerciseMVP()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<OrbitalRelayEvent>(OrbitalRelayAuthoring.PrefabPath);
        yield return null; yield return null;
        Object.FindFirstObjectByType<CharacterSpawner>().DebugStartDefaultRunIfMissing();
        yield return CoreTestSupport.Await(() => PlayerRuntimeReference.ResolvePlayerTransform(forceLookup: true) != null);
        Application.runInBackground = true;
        var gameView = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("UnityEditor.GameView")).First(t => t != null);
        EditorWindow.GetWindow(gameView).Show();
        var player = PlayerRuntimeReference.ResolvePlayerTransform(forceLookup: true);
        var host = new GameObject("Relay presentation MVP check");
        var owner = host.AddComponent<WorldEventSpawner>();
        owner.ConfigureDebugEventPrefabs(new WorldEvent[] { prefab }); owner.ConfigureSiteControlledMode(1);
        var config = AssetDatabase.LoadAssetAtPath<OrbitalRelayConfig>(OrbitalRelayAuthoring.ConfigPath);
        config.TryGetSettings(out var settings, out _);
        for (int exit = 0; exit < 4; exit++)
        {
            Assert.That(owner.SpawnDebugEventAt(prefab, player.position, true, out var e), Is.True);
            var relay = (OrbitalRelayEvent)e;
            var view = relay.GetComponent<OrbitalRelayPresentation>();
            var arena = relay.transform.Find("ArenaVisual");
            var canvas = relay.transform.Find("PresentationCanvas");
            Assert.That(arena.gameObject.activeSelf, Is.False);
            Assert.That(canvas.gameObject.activeSelf, Is.False);
            Assert.That(relay.CanInteract, Is.True);
            relay.Interact(); // Same handler invoked by PlayerInteractor's E input.
            Assert.That(relay.IsStarted, Is.True);
            Assert.That(arena.gameObject.activeSelf, Is.True);
            Assert.That(canvas.gameObject.activeSelf, Is.True);
            Assert.That(arena.Find("ArenaBorder").GetComponent<SpriteRenderer>().color.a,
                Is.GreaterThan(arena.Find("SpawnOuter").GetComponent<SpriteRenderer>().color.a));
            foreach (var label in canvas.GetComponentsInChildren<TMP_Text>(true))
                Assert.That(label.font, Is.EqualTo(TMP_Settings.defaultFontAsset));
            Time.timeScale = 0; // Freeze combat for a few frame-based captures; never change authored timings.
            var clock = (OrbitalRelayState)typeof(OrbitalRelayEvent).GetField("state", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(relay);
            foreach (var node in relay.GetComponentsInChildren<OrbitalRelayNode>(true))
            {
                float r = Vector2.Distance(relay.transform.position, node.transform.position);
                Assert.That(r, Is.InRange(3.5f, 6.5f));
                node.TryGetContactCircle(out var center, out float radius);
                Assert.That(Vector2.Distance(relay.transform.position, center) + radius, Is.LessThan(9));
            }
            if (exit == 0)
            {
                yield return Capture("stabilization");
                // Advance the real clock in the check only, using unchanged production settings.
                for (int i = 0; i < settings.RequiredActivations; i++) clock.Tick(settings.ContactTime, true);
                view.Render(clock.Snapshot); view.PlayTransition();
                yield return Capture("transition");
                clock.Tick(settings.TransitionDuration, false); view.Render(clock.Snapshot);
                clock.Tick(settings.ContactTime, true); view.Render(clock.Snapshot);
                yield return Capture("bonus");
                clock.Tick(clock.Snapshot.RemainingTime - 4.5f, false); view.Render(clock.Snapshot);
                yield return Capture("urgent");
                clock.Tick(5, false); view.Render(clock.Snapshot);
                typeof(OrbitalRelayEvent).GetMethod("Finish", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(relay, null);
                Assert.That(arena.gameObject.activeSelf, Is.False, "Completed snapshots must hide the arena immediately.");
                Assert.That(canvas.gameObject.activeSelf, Is.False);
                Object.Destroy(relay.gameObject);
            }
            else
            {
                if (exit == 1) relay.Cancel();
                if (exit >= 2)
                {
                    for (int i = 0; i < settings.RequiredActivations; i++) clock.Tick(settings.ContactTime, true);
                    view.Render(clock.Snapshot); view.PlayTransition();
                    clock.Tick(settings.TransitionDuration, false); view.Render(clock.Snapshot);
                    if (exit == 2) owner.ClearDebugEvent(relay); else relay.Cancel();
                }
                Assert.That(arena.gameObject.activeSelf, Is.False, "Exit must hide world visuals synchronously.");
                Assert.That(canvas.gameObject.activeSelf, Is.False, "Exit must hide the whole Canvas, including transition overlays.");
                if (relay != null) Object.Destroy(relay.gameObject);
            }
            yield return null;
            Assert.That(Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None).Any(c => c.name == "PresentationCanvas"), Is.False);
            Time.timeScale = 1;
        }
        Object.Destroy(host);
    }
    private static IEnumerator Capture(string name)
    {
        Directory.CreateDirectory(Output);
        yield return new WaitForSecondsRealtime(name == "urgent" ? .05f : .3f);
        Canvas.ForceUpdateCanvases();
        foreach (var relay in Object.FindObjectsByType<OrbitalRelayPresentation>(FindObjectsSortMode.None))
            foreach (var label in relay.GetComponentsInChildren<TMP_Text>())
            {
                label.ForceMeshUpdate();
                Assert.That(label.isTextOverflowing, Is.False, label.name + " overflows: " + label.text);
            }
        ScreenCapture.CaptureScreenshot(Output + "/" + name + ".png");
        yield return null; yield return null;
    }
}

[InitializeOnLoad] public static class OrbitalRelayPresentationVerification
{
    private const string RunningKey = "RelayPresentationCheck";
    private const string AnyRunKey = "Subject42.Verification.AnyRunActive";
    static OrbitalRelayPresentationVerification()
    {
        ScriptableObject.CreateInstance<TestRunnerApi>().RegisterCallbacks(new Results());
        EditorApplication.update += () =>
        {
            string request = OrbitalRelayPresentationTests.Output + "/check.request";
            if (!File.Exists(request) || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode || SessionState.GetBool(AnyRunKey, false)) return;
            File.Delete(request);
            SessionState.SetBool(AnyRunKey, true);
            SessionState.SetBool(RunningKey, true);
            try { ScriptableObject.CreateInstance<TestRunnerApi>().Execute(new ExecutionSettings(new Filter
                { testMode = UnityEditor.TestTools.TestRunner.Api.TestMode.EditMode, testNames = new[] { nameof(OrbitalRelayPresentationTests) } })); }
            catch { SessionState.EraseBool(AnyRunKey); SessionState.EraseBool(RunningKey); throw; }
        };
    }
    private sealed class Results : IErrorCallbacks
    {
        public void RunStarted(ITestAdaptor tests) { SessionState.SetBool(AnyRunKey, true); }
        public void TestStarted(ITestAdaptor test) { }
        public void TestFinished(ITestResultAdaptor result) { }
        public void RunFinished(ITestResultAdaptor result)
        {
            SessionState.EraseBool(AnyRunKey);
            if (!SessionState.GetBool(RunningKey, false)) return;
            SessionState.EraseBool(RunningKey);
            Directory.CreateDirectory(OrbitalRelayPresentationTests.Output);
            TestRunnerApi.SaveResultToFile(result, OrbitalRelayPresentationTests.Output + "/results.xml");
            if (result.PassCount + result.FailCount == 0)
            {
                const string message = "FAIL: the request produced no passing or failing test cases. Check filter names and results.xml for skipped/inconclusive cases.";
                File.WriteAllText(OrbitalRelayPresentationTests.Output + "/run-error.txt", message);
                Debug.LogError(message);
            }
            else if (File.Exists(OrbitalRelayPresentationTests.Output + "/run-error.txt")) File.Delete(OrbitalRelayPresentationTests.Output + "/run-error.txt");
        }
        public void OnError(string message)
        {
            SessionState.EraseBool(AnyRunKey);
            if (!SessionState.GetBool(RunningKey, false)) return;
            SessionState.EraseBool(RunningKey);
            Directory.CreateDirectory(OrbitalRelayPresentationTests.Output);
            File.WriteAllText(OrbitalRelayPresentationTests.Output + "/run-error.txt", message);
        }
    }
}
#endif
