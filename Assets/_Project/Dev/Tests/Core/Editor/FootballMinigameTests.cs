#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

public sealed class FootballMinigameTests
{
    private const string Root = "Assets/_Project/";
    private static T Read<T>(Object owner, string field) =>
        (T)owner.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(owner);

    [SetUp] public void Setup() => CoreTestSupport.PreservePreferences();
    [UnityTearDown] public IEnumerator Cleanup() => CoreTestSupport.CleanupPlayMode();

    [Test]
    public void AuthoredLayoutHasThreeGoalsAlignedFieldsAndRoomPanel()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "prefabs/Bunker/Minigames/MinigameArena_VS.prefab");
        var arena = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        try
        {
            Physics2D.SyncTransforms();
            var game = arena.GetComponentInChildren<FootballMinigame>(true);
            var goals = arena.GetComponentsInChildren<FootballGateScoreZone>(true).OrderBy(g => g.transform.position.x).ToArray();
            Assert.That(goals.Length, Is.EqualTo(3));
            Assert.That(goals.Select(g => g.transform.position.y).Distinct().Count(), Is.EqualTo(1));
            Assert.That(goals[1].transform.position.x - goals[0].transform.position.x,
                Is.EqualTo(goals[2].transform.position.x - goals[1].transform.position.x).Within(.001f));
            var lanes = Read<FootballTargetLane[]>(game, "anomalyLanes");
            var spawns = Read<Transform[]>(game, "anomalySpawnPoints");
            for (int i = 0; i < lanes.Length; i++)
            {
                Assert.That(lanes[i].LeftAnchor.position.y, Is.EqualTo(lanes[i].RightAnchor.position.y).Within(.001f));
                Assert.That(spawns[i].position.y, Is.EqualTo(lanes[i].LeftAnchor.position.y).Within(.001f));
            }
            var hud = arena.GetComponentInChildren<FootballMinigameHUD>(true);
            Assert.That(hud.GetComponent<Canvas>().renderMode, Is.EqualTo(RenderMode.WorldSpace));
            var frame = Read<BoxCollider2D>(game, "cameraBounds").bounds;
            var panel = Read<GameObject>(hud, "panelRoot").GetComponent<RectTransform>();
            var corners = new Vector3[4]; panel.GetWorldCorners(corners);
            Assert.That(corners.All(c => frame.Contains(new Vector3(c.x, c.y, frame.center.z))), Is.True,
                $"The physical scoreboard must fit inside the round camera bounds: {frame}; corners {string.Join(", ", corners.Select(c => c.ToString()))}; canvas scale {hud.transform.lossyScale}.");
            Assert.That(Read<BoxCollider2D>(game, "arenaBounds").bounds.max.x, Is.LessThan(corners.Min(c => c.x)));
        }
        finally { Object.DestroyImmediate(arena); }
    }

    [UnityTest]
    public IEnumerator StartScoreCancelAndRestartReleaseSessionOwnership()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
        yield return new EnterPlayMode();
        yield return CoreTestSupport.LoadBunker();
        // Allocate the scenario's captured locals after the Play Mode domain
        // reload; the test runner cannot restore compiler-generated closures.
        yield return RunRoundScenario();
    }

    private static IEnumerator RunRoundScenario()
    {
        var game = Object.FindFirstObjectByType<FootballMinigame>();
        Assert.That(game, Is.Not.Null);
        var player = Object.FindObjectsByType<CharacterMovement2D>(FindObjectsSortMode.None).First(p => p.CompareTag("Player"));
        var playerBody = player.GetComponent<Rigidbody2D>();
        var hud = Read<FootballMinigameHUD>(game, "hud");
        var panel = Read<GameObject>(hud, "panelRoot");
        Assert.That(panel.activeSelf, Is.False, "The scoreboard must be hidden outside a round.");
        var missingArrows = Object.FindObjectsByType<BunkerStation>(FindObjectsSortMode.None)
            .Where(s => s.CanInteract && !s.GetComponentsInChildren<Transform>(true)
                .Any(t => t.name == "PF_InteractionArrow" && t.gameObject.activeInHierarchy))
            .Select(s => s.name).ToArray();
        Assert.That(missingArrows, Is.Empty, "Every functional bunker station must have its permanent interaction arrow.");
        var provider = Object.FindFirstObjectByType<MissionProvider>();
        Assert.That(provider.CanInteract, Is.True);
        Assert.That(provider.transform.Find("PF_InteractionArrow").gameObject.activeInHierarchy, Is.True);
        // Teleports are test setup; keep interpolation from restoring a stale
        // displayed position during Physics2D.SyncTransforms.
        playerBody.interpolation = RigidbodyInterpolation2D.None;
        player.transform.position = game.StartStation.transform.position + new Vector3(20f, -20f);
        playerBody.position = player.transform.position;
        Physics2D.SyncTransforms();
        yield return new WaitForFixedUpdate();
        yield return new WaitForFixedUpdate();
        Assert.That(game.StartStation.CanInteract, Is.True);
        Assert.That(Read<GameObject>(game.StartStation, "interactionArrow").activeInHierarchy, Is.True, "Football guidance must be visible from a distance.");
        var hover = game.StartStation.GetComponent<BunkerHoverOutline>();
        hover.SetHovered(true);
        Assert.That(Read<Material[]>(hover, "materials").Any(m => m.IsKeywordEnabled("_OUTLINE_ON")), Is.True, "The available station must highlight while the player is far away.");
        hover.SetHovered(false);
        var door = Read<BunkerGateVisual>(game, "entranceDoor");
        var camera = Read<CameraFollow>(game, "cameraFollow").ControlledCamera;
        var originalRect = camera.rect;
        int best = game.BestScore;
        game.StartStation.Interact();
        Assert.That(game.IsRunning, Is.True);
        Assert.That(game.PlayBounds.Contains(player.transform.position), Is.True, "Remote start must place the player inside before the door closes.");
        Assert.That(panel.activeSelf, Is.True);
        Assert.That(door.IsOpen, Is.False);
        Assert.That(game.ActiveBallCount, Is.EqualTo(4));
        Assert.That(game.ActiveAnomalyCount, Is.EqualTo(2));
        Assert.That(game.ActiveTargetCount, Is.EqualTo(3));
        Assert.That(game.StartStation.gameObject.activeInHierarchy, Is.True, "Only the marker may hide during a round.");
        float settleUntil = Time.realtimeSinceStartup + 1f;
        while (Time.realtimeSinceStartup < settleUntil) yield return null;
        Assert.That(game.IsRunning, Is.True, "The player must remain inside the arena after test setup.");
        Assert.That(game.ActiveBallCount, Is.EqualTo(4), "The running round must retain all registered balls after settling.");
        var ball = game.Ball;
        Assert.That(ball != null, Is.True, "The first registered ball must be a live Unity object.");
        var body = ball.GetComponent<Rigidbody2D>();
        Assert.That(body != null, Is.True, "The authored ball must retain its Rigidbody2D.");
        body.interpolation = RigidbodyInterpolation2D.None;
        ball.Kick(Vector2.up, 6f);
        yield return new WaitForFixedUpdate();
        Assert.That(body.linearVelocity.y, Is.GreaterThan(0f));
        var zones = Read<System.Collections.Generic.List<GravityZone>>(game, "activeAnomalies");
        var zone = zones[0];
        // Shoot across the actual trigger boundary, rather than depending on
        // enter/stay events produced by a teleport into a moving static trigger.
        body.transform.position = zone.transform.position + Vector3.right * 4.5f;
        body.position = (Vector2)zone.transform.position + Vector2.right * 4.5f;
        body.linearVelocity = Vector2.left * 4f;
        body.WakeUp();
        Physics2D.SyncTransforms();
        yield return CoreTestSupport.Await(() => body.linearVelocity.x < -4.01f);
        Assert.That(game.IsRunning, Is.True);
        Assert.That(body.linearVelocity.x, Is.LessThan(-4f), "The attractive field must accelerate a ball toward its center.");
        var target = Read<FootballScoreZone[]>(game, "targetPool")[0];
        int points = target.Points;
        Assert.That(target.IsAcceptingBalls, Is.True);
        body.transform.position = target.transform.position;
        body.position = target.transform.position; body.linearVelocity = Vector2.zero;
        body.WakeUp();
        Physics2D.SyncTransforms();
        Assert.That(target.GetComponent<CircleCollider2D>().Distance(ball.GetComponents<CircleCollider2D>().First(c => !c.isTrigger)).isOverlapped, Is.True);
        yield return CoreTestSupport.Await(() => game.Score > 0);
        Assert.That(game.Score, Is.EqualTo(points));
        var goal = Read<FootballGateScoreZone[]>(game, "gates")[0];
        body.transform.position = goal.transform.position;
        body.position = goal.transform.position; body.linearVelocity = Vector2.zero;
        body.WakeUp();
        Physics2D.SyncTransforms();
        yield return CoreTestSupport.Await(() => game.GoalCount > 0);
        Assert.That(game.Score, Is.EqualTo(points + 20));
        Assert.That(game.GoalCount, Is.EqualTo(1));
        float normalTimeScale = Time.timeScale;
        typeof(BallRollVisual).GetMethod("BeginAim", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(ball, null);
        Time.timeScale = .2f;
        game.CancelCurrentRound();
        Assert.That(Time.timeScale, Is.EqualTo(normalTimeScale), "Cancellation must release ball slow motion.");
        Assert.That(game.CanStart, Is.True);
        Assert.That(panel.activeSelf, Is.False);
        Assert.That(door.IsOpen, Is.True);
        Assert.That(camera.rect, Is.EqualTo(originalRect));
        Assert.That(game.ActiveBallCount, Is.Zero);
        Assert.That(game.ActiveAnomalyCount, Is.Zero);
        Assert.That(game.ActiveTargetCount, Is.Zero);
        Assert.That(game.BestScore, Is.EqualTo(best), "Cancellation must not grant a record reward.");
        yield return new WaitForFixedUpdate();
        yield return new WaitForFixedUpdate();
        Assert.That(game.StartStation.CanInteract, Is.True, "Remaining at START must allow an explicit restart.");
        game.StartStation.Interact();
        Assert.That(game.IsRunning, Is.True);
        Assert.That(game.Score, Is.Zero);
        var pause = Object.FindFirstObjectByType<PauseMenuUI>();
        pause.Pause();
        Assert.That(pause.IsPaused, Is.True);
        game.CancelCurrentRound();
        Assert.That(game.CanStart, Is.True);
        Assert.That(door.IsOpen, Is.True);
        Assert.That(Time.timeScale, Is.Zero, "Football must not take time ownership away from pause.");
        pause.Resume();
        game.StartStation.Interact();
        Assert.That(game.IsRunning, Is.True);
        game.CompleteGame(); // Zero score: exercise completion without changing records or rewards.
        Assert.That(panel.activeSelf, Is.False);
        Assert.That(game.CanStart, Is.True);
        Assert.That(door.IsOpen, Is.True);
        game.StartStation.Interact();
        Assert.That(game.IsRunning, Is.True);
        game.OnPlayerLeftArena();
        Assert.That(game.CanStart, Is.True);
        Assert.That(door.IsOpen, Is.True);
        Assert.That(camera.rect, Is.EqualTo(originalRect));
    }
}
#endif
