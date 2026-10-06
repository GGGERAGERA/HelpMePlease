#if UNITY_EDITOR
using NUnit.Framework;
using UnityEngine;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

public sealed class CorridorV2Tests
{
    [Test] public void DefaultRouteRequiresFiveOrderedCheckpoints()
    {
        var route = new CorridorV2Route(false, 0);
        Assert.That(route.TryAdvance(new Vector2(59, 0), new Vector2(60, 0)), Is.False,
            "Direct EXIT must not grant the next gate.");
        Assert.That(route.ExitOpen, Is.False);
        Assert.That(route.CheckpointCount, Is.EqualTo(5));
    }

    [Test] public void LabEventHasCollapseAndFinalPushInsteadOfSurvivalDeadline()
    {
        Assert.That(typeof(CorridorV2Event).GetProperty("CollapseDistance"), Is.Not.Null);
        Assert.That(typeof(CorridorV2Event).GetProperty("IsFinalPush"), Is.Not.Null);
    }

    [TestCase(CorridorV2Preset.Straight, 0)]
    [TestCase(CorridorV2Preset.L, 1)]
    [TestCase(CorridorV2Preset.Zigzag, 2)]
    [TestCase(CorridorV2Preset.Zigzag, 3)]
    public void RoutesRequirePhysicalOrderedForwardGateCrossings(CorridorV2Preset preset, int turns)
    {
        var route = new CorridorV2Route(new CorridorV2Settings { routePreset = preset }, turns);
        var second = route.Point(route.CheckpointDistance(1), out var secondDirection);
        Assert.That(route.TryAdvance(second - secondDirection, second + secondDirection), Is.False);
        var first = route.Point(route.CheckpointDistance(0), out var firstDirection);
        Assert.That(route.TryAdvance(first + firstDirection, first - firstDirection), Is.False);
        Vector2 side = new(-firstDirection.y, firstDirection.x);
        Assert.That(route.TryAdvance(first - firstDirection + side * 3.3f, first + firstDirection + side * 3.3f), Is.False);
        Assert.That(route.TryAdvance(route.Vertices[0], second), Is.False, "Teleport cannot grant progress.");
        for (int i = 0; i < route.CheckpointCount; i++)
        {
            Vector2 gate = route.Point(route.CheckpointDistance(i), out var forward);
            Assert.That(route.TryAdvance(gate - forward * .5f, gate + forward * .5f), Is.True);
            Assert.That(route.Completed, Is.EqualTo(i + 1));
            Assert.That(route.TryAdvance(gate - forward, gate + forward), Is.False, "No double activation.");
        }
        Assert.That(route.ExitOpen, Is.True);
        Assert.That(route.Length, Is.EqualTo(129f));
    }

    [Test] public void BentRouteRejectsChordAndConfiguresGateCount()
    {
        var route = new CorridorV2Route(new CorridorV2Settings { checkpointCount = 4, routePreset = CorridorV2Preset.L });
        Vector2 corner = route.Vertices[2];
        Assert.That(route.StaysInside(corner - Vector2.right * 8.4f, corner + Vector2.up * 8.4f), Is.False);
        Assert.That(route.CheckpointCount, Is.EqualTo(4));
    }

    [SetUp] public void Setup() => CoreTestSupport.PreservePreferences();
    [UnityTearDown] public IEnumerator Cleanup() => CoreTestSupport.CleanupPlayMode();

    [UnityTest, Timeout(90000)] public IEnumerator LabRoutesCollisionPressureHazardsAndCompletion()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
        yield return new EnterPlayMode();
        yield return ExerciseLab();
    }

    private static IEnumerator ExerciseLab()
    {
        LogAssert.Expect(LogType.Error, "[TacticalMapHUD] Authored shell or scene references are missing.");
        EditorSceneManager.LoadSceneInPlayMode(WorldSystemsLabController.ScenePath,
            new UnityEngine.SceneManagement.LoadSceneParameters(UnityEngine.SceneManagement.LoadSceneMode.Single));
        yield return null;
        Application.runInBackground = true;
        var owner = Object.FindFirstObjectByType<WorldSystemsLabController>();
        var lab = owner.GetComponent<CorridorV2Lab>();
        var body = owner.Player.GetComponent<Rigidbody2D>();
        var movement = owner.Player.GetComponent<CharacterMovement2D>();
        var health = owner.Player.GetComponent<PlayerHealth>();
        var area = Object.FindFirstObjectByType<GameplayAreaService>();
        Vector3 originalAreaScale = area.transform.localScale;
        Set(owner, "panelVisible", false);
        Set(lab, "crowd", false);
        Directory.CreateDirectory("Artifacts/GeneratedQA/CorridorV2");
        foreach (CorridorV2Preset preset in System.Enum.GetValues(typeof(CorridorV2Preset)))
        {
            lab.Tuning.routePreset = preset;
            lab.StartCorridor(); yield return null;
            var game = lab.Current;
            Assert.That(game.IsStarted, Is.True, preset.ToString());
            Assert.That(game.Walls.Count, Is.GreaterThan(0));
            Assert.That(game.Walls.All(w => !w.isTrigger), Is.True);
            Vector2 directExit = game.Route.Point(game.Route.Length, out _);
            body.position = directExit; yield return null;
            Assert.That(game.IsCompleted, Is.False, "Exit without gates stays locked.");
            owner.ClearEvents(); yield return null;
            Assert.That(game == null, Is.True, "Reset destroys walls/visuals/event.");
        }
        lab.Tuning.routePreset = CorridorV2Preset.Straight;
        lab.StartCorridor(); yield return null;
        var collisionGame = lab.Current;
        Vector2 center = collisionGame.Route.Point(6, out Vector2 forward);
        Vector2 side = new(-forward.y, forward.x);
        body.position = center; movement.MovementIntent = () => side;
        yield return Wait(1.2f);
        Assert.That(Vector2.Dot(body.position - center, side), Is.LessThan(collisionGame.Route.HalfWidth), "Player blocked by wall.");
        // Dash uses the same collision pair filtering as regular physics movement.
        movement.MovementIntent = null;
        typeof(CharacterMovement2D).GetField("moveInput", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(movement, side);
        typeof(CharacterMovement2D).GetMethod("TryStartDash", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(movement, null);
        yield return Wait(.3f);
        Assert.That(collisionGame.Route.Contains(body.position), Is.True, "Dash cannot escape.");
        movement.MovementIntent = () => Vector2.zero;
        body.position = center;
        var enemyPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/prefabs/Enemies/p_Enemy_default.prefab");
        var enemy = Object.Instantiate(enemyPrefab, center + side * 6, Quaternion.identity);
        yield return Wait(2f);
        Assert.That(Vector2.Dot((Vector2)enemy.transform.position - center, side), Is.LessThan(collisionGame.Route.HalfWidth - .3f), "Ordinary chase AI crosses inward.");
        body.position = center + side * 8; // Test placement only: ordinary player movement remains blocked.
        owner.Player.position = body.position;
        Physics2D.SyncTransforms();
        float chaseDeadline = Time.time + 7f; // Allow the normal contact attack/stop pause to finish.
        while (Vector2.Dot((Vector2)enemy.transform.position - center, side) <= collisionGame.Route.HalfWidth + .3f && Time.time < chaseDeadline)
            yield return null;
        File.WriteAllText("Artifacts/GeneratedQA/CorridorV2/collision.txt", $"player {body.position}; enemy {enemy.transform.position}; wall half-width {collisionGame.Route.HalfWidth}");
        Assert.That(Vector2.Dot((Vector2)enemy.transform.position - center, side), Is.GreaterThan(collisionGame.Route.HalfWidth + .3f), "Same chase AI crosses outward.");
        Object.Destroy(enemy);
        owner.ClearEvents(); yield return null;

        lab.Tuning.strikeInterval = 100f; // Isolate manual pattern probes from the automatic schedule.
        lab.StartCorridor(); yield return null;
        var pressureGame = lab.Current;
        var gate = pressureGame.Route.Point(pressureGame.Route.CheckpointDistance(0), out forward);
        body.position = gate - forward; yield return null;
        body.position = gate + forward; yield return null;
        Assert.That(pressureGame.Route.Completed, Is.EqualTo(1));
        Assert.That(pressureGame.RocketsLaunched, Is.Zero, "First segment is quiet.");
        float hp = health.CurrentHealth;
        yield return Wait(4.5f); // Camping immediately AFTER a cleared gate must also be unsafe.
        Assert.That(pressureGame.CollapseDistance, Is.GreaterThan(pressureGame.Route.CheckpointDistance(0)));
        Assert.That(health.CurrentHealth, Is.LessThan(hp), "Visible front applies recurring pressure damage.");
        yield return Capture("collapse");
        health.SetRuntimeHealth(1000, 1000);
        var strikes = (CorridorV2Strikes)typeof(CorridorV2Event).GetField("strikes", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(pressureGame);
        foreach (CorridorV2StrikePattern pattern in System.Enum.GetValues(typeof(CorridorV2StrikePattern)))
        {
            strikes.Cancel();
            body.position = pressureGame.Route.Point(25, out _); yield return null;
            int launched = strikes.Launched;
            strikes.StartPattern(pattern, body.position);
            yield return Wait(.2f);
            Assert.That(strikes.Pending, Is.GreaterThan(0));
            yield return Capture("strike-" + pattern);
            yield return Wait(2.5f);
            Assert.That(strikes.Launched, Is.GreaterThan(launched));
        }
        owner.ClearEvents(); yield return null;
        Assert.That(pressureGame == null, Is.True);
        lab.Tuning.strikeInterval = 3.2f;

        // One complete default-speed traversal with normal enemies and real player physics.
        Set(lab, "crowd", true); lab.Tuning.routePreset = CorridorV2Preset.Zigzag;
        lab.StartCorridor(); yield return null;
        var run = lab.Current; string result = null;
        // Traversal smoke verifies physics/lifecycle; this deterministic steering does not dodge attacks.
        health.SetRuntimeHealth(1000, 1000);
        run.Finished += message => result = message;
        bool sawFinal = false; float finalStart = 0;
        float traversalStart = Time.time;
        movement.MovementIntent = () =>
        {
            if (run == null || run.IsCompleted) return Vector2.zero;
            float distance = run.Route.Project(body.position, out _);
            var target = run.Route.Point(Mathf.Min(distance + 1.5f, run.Route.Length), out _);
            return (target - body.position).normalized;
        };
        float deadline = Time.time + 32;
        while (run != null && result == null && Time.time < deadline)
        {
            if (run.IsFinalPush && !sawFinal)
            {
                sawFinal = true; finalStart = Time.time;
                Assert.That(run.ExitReady, Is.False);
                yield return Capture("final-push");
            }
            yield return null;
        }
        movement.MovementIntent = () => Vector2.zero;
        Assert.That(sawFinal, Is.True); Assert.That(result, Does.StartWith("EXIT reached"));
        Assert.That(Time.time - traversalStart, Is.InRange(20f, 25f), "Default traversal rhythm.");
        Assert.That(Time.time - finalStart, Is.GreaterThanOrEqualTo(lab.Tuning.finalPushDuration));
        yield return null; Assert.That(run == null, Is.True);
        Assert.That(Object.FindObjectsByType<CorridorV2Event>(FindObjectsSortMode.None), Is.Empty);
        Assert.That(Object.FindObjectsByType<EnemyChaseMovement>(FindObjectsSortMode.None), Is.Empty);
        Assert.That(area.transform.localScale, Is.EqualTo(originalAreaScale), "Completion restores area without manual reset.");
        owner.ClearEvents(); yield return null;
        File.WriteAllText("Artifacts/GeneratedQA/CorridorV2/result.txt", result);
        lab.StartCorridor(); yield return null;
        var deathRun = lab.Current;
        yield return Wait(.7f);
        Assert.That(health.TakeDamage(health.MaxHealth + 1, Vector2.zero), Is.True);
        yield return CoreTestSupport.Await(() => deathRun == null);
        Assert.That(area.transform.localScale, Is.EqualTo(originalAreaScale));
        Assert.That(Object.FindObjectsByType<EnemyChaseMovement>(FindObjectsSortMode.None), Is.Empty);
        lab.StartCorridor(); yield return null;
        Assert.That(health.IsDead, Is.False); Assert.That(lab.Current.IsStarted, Is.True);
        owner.ClearEvents(); yield return null;
    }
    private static IEnumerator Capture(string name)
    {
        string path = "Artifacts/GeneratedQA/CorridorV2/" + name + ".png";
        if (File.Exists(path)) File.Delete(path);
        ScreenCapture.CaptureScreenshot(path);
        yield return CoreTestSupport.Await(() => File.Exists(path));
    }
    private static IEnumerator Wait(float seconds)
    {
        float until = Time.time + seconds;
        while (Time.time < until) yield return null;
    }
    private static void Set(object target, string field, object value) =>
        target.GetType().GetField(field, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(target, value);
}
#endif
