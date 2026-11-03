#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Subject42.Combat.OrbitalStation;
using Subject42.Visuals;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

public sealed class F1RemovalSmokeTests
{
    [UnityTest]
    public IEnumerator ProductionMovesFiresAndUsesNativeConstructorWithoutDebugMenu()
    {
        CoreTestSupport.PreservePreferences();
        typeof(BunkerStationPresentationTests).GetMethod("SetResolution",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static).Invoke(null, null);
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
        yield return new EnterPlayMode();
        yield return Scenario();
    }

    private static IEnumerator Scenario()
    {
        const string output = "Artifacts/DebugMenuAudit/F1Removal-20261031";
        Directory.CreateDirectory(output);
        yield return CoreTestSupport.LoadBunker();
        RunSelectionManager.Instance.SelectCharacter(AssetDatabase.LoadAssetAtPath<CharacterData>(
            "Assets/_Project/Data/Characters/01_Gera.asset"));
        var starter = Object.FindFirstObjectByType<BunkerRunStarter>();
        starter.StartRun(starter.transform);
        yield return CoreTestSupport.Await(() => SceneManager.GetActiveScene().name == "MVP" &&
            ProductionSceneComposition.Active is { IsReady: true } && !SceneTransitionOverlay.IsTransitioning &&
            Object.FindFirstObjectByType<CharacterSpawner>()?.Station is { IsInitialized: true });
        yield return null;
        foreach (string type in new[] { "Subject42DebugMenu", "Subject42.Visuals.LiveVisualStudio", "Subject42.Visuals.LiveVisualGesture" })
            Assert.That(AppDomain.CurrentDomain.GetAssemblies().Any(assembly => assembly.GetType(type, false) != null),
                Is.False, type + " must be physically absent, including F1/F8 bootstrap and input listeners.");

        var station = Object.FindFirstObjectByType<CharacterSpawner>().Station;
        var player = PlayerRuntimeReference.CachedPlayer;
        var movement = player.GetComponent<CharacterMovement2D>();
        var body = player.GetComponent<Rigidbody2D>();
        var input = station.InputOwner;
        var spawner = Object.FindFirstObjectByType<EnemySpawner>();
        Assert.That(station.UsesSegmentedOrbit, Is.True);
        Assert.That(input.IsGameplayInputBlocked || input.IsGameplayPointerBlocked, Is.False);
        Assert.That(movement.MovementIntent, Is.Null, "Removing F1 must not create an input owner or a bot.");
        Assert.That(BotRunSession.Current, Is.Null);
        Assert.That(LiveVisualRuntime.Instance, Is.Not.Null, "Production profile runtime survives UI removal.");
        Assert.That(Time.timeScale, Is.GreaterThan(0f));
        spawner.StopSpawning();
        var pistol = station.Modules.OfType<OrbitalProjectileModule>().First(module => module.Kind == OrbitalModuleKind.Pistol);
        int shotsBefore = pistol.ShotsFired;
        float moved = 0f;
        try
        {
            // Existing bot/lab intent feeds the real production Update/FixedUpdate, collision and acceleration.
            foreach (var direction in new[] { Vector2.right, Vector2.up, Vector2.left, Vector2.down })
            {
                Vector2 from = body.position;
                movement.MovementIntent = () => direction;
                yield return new WaitForSecondsRealtime(.18f);
                moved += Vector2.Distance(from, body.position);
            }
        }
        finally { movement.MovementIntent = null; }
        Assert.That(moved, Is.GreaterThan(.2f), "Actual Rigidbody2D locomotion must remain live.");
        yield return CoreTestSupport.Await(() => pistol.ShotsFired > shotsBefore);
        Assert.That(station.GetComponentsInChildren<OrbitalRingView>(true)
            .SelectMany(view => new[] { view.BackLine, view.FrontLine })
            .Any(line => line != null && line.enabled && line.positionCount > 2), Is.True);
        ScreenCapture.CaptureScreenshot(Path.GetFullPath(output + "/mvp-movement-and-fire.png"));
        yield return new WaitForSecondsRealtime(.15f);

        float resumeScale = Time.timeScale;
        Assert.That(input.TryToggleConstructor(), Is.True, "Use the same existing entry point as Tab.");
        Assert.That(input.IsConstructing, Is.True);
        Assert.That(Time.timeScale, Is.Zero);
        Canvas.ForceUpdateCanvases();
        yield return null;
        ScreenCapture.CaptureScreenshot(Path.GetFullPath(output + "/native-tab-constructor.png"));
        yield return new WaitForSecondsRealtime(.15f);
        Assert.That(input.TryToggleConstructor(), Is.True);
        Assert.That(input.IsIdle, Is.True);
        Assert.That(Time.timeScale, Is.EqualTo(resumeScale));
        Assert.That(input.IsGameplayInputBlocked || input.IsGameplayPointerBlocked, Is.False);
        Assert.That(movement.MovementIntent, Is.Null);
        File.WriteAllText(output + "/result.txt", "PASS: MainMenu -> MVP; no F1/LVS/gesture classes or menu bootstrap/input listeners.\n" +
            $"PASS: real production movement travelled {moved:0.000}m through existing intent/physics; ordinary Segmented pistol fired; ring geometry remains.\n" +
            "PASS: production visual runtime present; native Tab entry opens/closes constructor; pause resumes and input is unblocked.\n" +
            "One scripted Editor Play Mode smoke. MovementIntent and the native Tab handler were invoked; physical keyboard input was not synthesized.\n" +
            Application.dataPath + "\nUnity=" + Application.unityVersion);
    }

    [UnityTearDown]
    public IEnumerator Cleanup()
    {
        var cleanup = CoreTestSupport.CleanupPlayMode();
        while (cleanup.MoveNext()) yield return cleanup.Current;
        EditorSceneManager.OpenScene("Assets/_Project/Scenes/MainBuild/MVP.unity");
    }
}
#endif
