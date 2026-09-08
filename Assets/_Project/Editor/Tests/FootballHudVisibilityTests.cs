using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public sealed class FootballHudVisibilityTests
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

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
