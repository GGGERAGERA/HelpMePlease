#if UNITY_EDITOR
using System;
using System.Reflection;
using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

public sealed class Subject42GoldenPathLabTests
{
    [Test]
    public void RowColoursSeparatePassCombatRegressionAndAssertion()
    {
        Color pass = GoldenPathLab.RowColor("PASS");
        Color combat = GoldenPathLab.RowColor("BOT_COMBAT_FAIL");
        Color game = GoldenPathLab.RowColor("GAME_REGRESSION");
        Color assertion = GoldenPathLab.RowColor("ASSERTION_FAIL");
        Assert.That(pass.g, Is.GreaterThan(pass.r));
        Assert.That(game.r, Is.GreaterThan(game.g));
        Assert.That(combat.g, Is.GreaterThan(assertion.g));
        Assert.That(assertion.g, Is.GreaterThan(game.g));
        Assert.That(GoldenPathLab.RowColor("ABORTED"), Is.EqualTo(combat));
    }

    [UnityTest]
    public IEnumerator ExistingFailureOpensByRowClickWithoutChangingHistory()
    {
        yield return new EnterPlayMode();
        byte[] historyBefore = File.ReadAllBytes("Artifacts/BotBatches/golden_path_history.json");
        string clipboardBefore = GUIUtility.systemCopyBuffer;
        yield return EditorSceneManager.LoadSceneAsyncInPlayMode(GoldenPathLab.ScenePath, new LoadSceneParameters(LoadSceneMode.Single));
        var lab = GoldenPathLab.Current;
        lab.SetView(GoldenPathLab.ResultsView.Failures);
        EditorWindow.GetWindow(typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView")).Focus();
        int index = Array.FindIndex(lab.Rows, row => row.Run.Seed == 48151625 && GoldenPathSummary.Classify(row.Run) == "BOT_COMBAT_FAIL");
        Assert.That(index, Is.InRange(0, 6), "Existing failing seed must be visible without file search or table scrolling");
        var queue = typeof(EditorGUIUtility).GetMethod("QueueGameViewInputEvent", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        Assert.That(queue, Is.Not.Null, "Editor must support GameView input events");
        float scale = Mathf.Min(Screen.width / 1440f, Screen.height / 900f);
        var point = new Vector2((Screen.width - 1440 * scale) / 2 + 65 * scale,
            (Screen.height - 900 * scale) / 2 + (560 + index * 34 + 17) * scale);
        yield return new WaitForEndOfFrame();
        ScreenCapture.CaptureScreenshot("Artifacts/BotBatches/golden_path_lab_selected.png");
        yield return null;
        queue.Invoke(null, new object[] { new Event { type = EventType.MouseMove, mousePosition = point } });
        yield return new WaitForSecondsRealtime(.1f);
        queue.Invoke(null, new object[] { new Event { type = EventType.MouseDown, button = 0, mousePosition = point } });
        yield return new WaitForSecondsRealtime(.1f);
        queue.Invoke(null, new object[] { new Event { type = EventType.MouseUp, button = 0, mousePosition = point } });
        yield return null;
        Assert.That(lab.SelectedRow, Is.Not.Null, "Click must select the table row");
        Assert.That(lab.SelectedRow.Run.Seed, Is.EqualTo(48151625));
        Assert.That(lab.SelectedRow.Speed, Is.EqualTo(10f), "Selection keeps the original speed, not the 5× new-run setting");
        StringAssert.Contains("Run.PlayerAlive", lab.SelectedDetailText);
        StringAssert.Contains("Player died during regression route", lab.SelectedDetailText);
        foreach (string field in new[] { "DamageTaken:", "RemainingHP:", "RewardsTaken count:", "AssertionsPassed / Failed:", "BossKilled:", "BunkerClean:", "SecondRunBaseline:", "RECENT DAMAGE" })
            StringAssert.Contains(field, lab.SelectedDetailText);
        foreach (string damage in lab.SelectedRow.Run.GoldenPath.RecentDamage.TakeLast(10)) StringAssert.Contains(damage, lab.SelectedDetailText);
        try
        {
            lab.CopySeed(); Assert.That(GUIUtility.systemCopyBuffer, Is.EqualTo("48151625"));
            lab.CopyFailure(); Assert.That(GUIUtility.systemCopyBuffer, Is.EqualTo(lab.SelectedDetailText));
        }
        finally { GUIUtility.systemCopyBuffer = clipboardBefore; }
        var selectedId = lab.SelectedRow.Run.RunId;
        lab.RefreshResults();
        Assert.That(lab.SelectedRow.Run.RunId, Is.EqualTo(selectedId), "Refresh must preserve exact selected attempt");
        yield return new WaitForEndOfFrame();
        ScreenCapture.CaptureScreenshot("Artifacts/BotBatches/golden_path_lab_selected.png");
        yield return null;
        Assert.That(lab.IsBusy, Is.False);
        Assert.That(File.ReadAllBytes("Artifacts/BotBatches/golden_path_history.json").SequenceEqual(historyBefore), Is.True);
        yield return new ExitPlayMode();
    }

    [Test]
    public void CreateSceneOutsideProductionBuild()
    {
        byte[] buildSettings = File.ReadAllBytes("ProjectSettings/EditorBuildSettings.asset");
        // EditMode tests start in an untitled scratch scene; Unity forbids additive
        // scene creation there. Open a saved scene without modifying its assets.
        EditorSceneManager.OpenScene("Assets/_Project/Scenes/MainBuild/MainMenu.unity");
        GoldenPathLabAuthoring.Create();
        Assert.That(File.Exists(GoldenPathLab.ScenePath), Is.True);
        Assert.That(EditorBuildSettings.scenes.Any(s => s.enabled && s.path == GoldenPathLab.ScenePath), Is.False);
        CollectionAssert.AreEqual(buildSettings, File.ReadAllBytes("ProjectSettings/EditorBuildSettings.asset"));
    }

    private static void KnownVisualLog(string message, string stack, LogType type)
    {
        if (type == LogType.Assert && message.StartsWith("Setting the duration while system is still playing", StringComparison.Ordinal)
            && stack.Contains("WorldRuleVisual:EnsureWindResources")) LogAssert.Expect(type, message);
    }

    [UnityTest, Timeout(300000)]
    public IEnumerator OneRunUpdatesLabAndReturnsToDevScene()
    {
        yield return new EnterPlayMode();
        Application.logMessageReceived += KnownVisualLog;
        yield return EditorSceneManager.LoadSceneAsyncInPlayMode(GoldenPathLab.ScenePath, new LoadSceneParameters(LoadSceneMode.Single));
        var lab = GoldenPathLab.Current;
        Assert.That(lab, Is.Not.Null);
        var oldId = lab.DisplayedBatch?.BatchId;
        yield return new WaitForEndOfFrame();
        ScreenCapture.CaptureScreenshot("Artifacts/BotBatches/golden_path_lab_before.png");
        yield return null;
        Assert.That(lab.StartBatch(1), Is.True);
        float deadline = Time.realtimeSinceStartup + 240f;
        bool captured = false;
        while (lab.IsBusy && Time.realtimeSinceStartup < deadline)
        {
            if (!captured && BotRunSession.Current?.IsRunning == true)
            {
                yield return new WaitForEndOfFrame();
                ScreenCapture.CaptureScreenshot("Artifacts/BotBatches/golden_path_lab_running.png");
                captured = true;
            }
            yield return null;
        }
        Assert.That(lab.IsBusy, Is.False, "Lab batch did not finish");
        Assert.That(captured, Is.True, "No live run observed");
        Assert.That(SceneManager.GetActiveScene().path, Is.EqualTo(GoldenPathLab.ScenePath));
        Assert.That(lab.DisplayedBatch.BatchId, Is.Not.EqualTo(oldId));
        Assert.That(lab.DisplayedBatch.Results.Count, Is.EqualTo(1));
        Assert.That(lab.VisibleRowCount, Is.EqualTo(1));
        Assert.That(lab.DisplayedStatus, Is.EqualTo(lab.DisplayedBatch.Results[0].Result == "GoldenPathPassed" ? "GAMEPLAY REGRESSION CLEAN" :
            GoldenPathSummary.Classify(lab.DisplayedBatch.Results[0]) == "GAME_REGRESSION" ? "GAME REGRESSION FOUND" : "BOT FAILS / ABORTED PRESENT"));
        yield return new WaitForEndOfFrame();
        ScreenCapture.CaptureScreenshot("Artifacts/BotBatches/golden_path_lab_after.png");
        yield return null;
        yield return new ExitPlayMode();
    }

    [UnityTest]
    public IEnumerator PreviewExistingResultsWithoutStartingRun()
    {
        yield return new EnterPlayMode();
        byte[] historyBefore = File.ReadAllBytes("Artifacts/BotBatches/golden_path_history.json");
        yield return EditorSceneManager.LoadSceneAsyncInPlayMode(GoldenPathLab.ScenePath, new LoadSceneParameters(LoadSceneMode.Single));
        var lab = GoldenPathLab.Current;
        Assert.That(lab.IsBusy, Is.False);
        Assert.That(lab.DisplayedBatch, Is.Not.Null);
        Assert.That(lab.VisibleRowCount, Is.InRange(1, 20));
        Assert.That(lab.TryGetLastFailed(out var failed, out float speed), Is.True);
        Assert.That(failed.Result, Is.Not.EqualTo("Aborted"));
        Assert.That(speed, Is.EqualTo(5f).Or.EqualTo(10f));
        yield return new WaitForEndOfFrame();
        ScreenCapture.CaptureScreenshot("Artifacts/BotBatches/golden_path_lab_after.png");
        yield return null;
        Assert.That(File.ReadAllBytes("Artifacts/BotBatches/golden_path_history.json").SequenceEqual(historyBefore), Is.True, "Viewing the lab must not change history");
        yield return new ExitPlayMode();
    }

    [UnityTearDown]
    public IEnumerator Cleanup()
    {
        Application.logMessageReceived -= KnownVisualLog;
        if (Application.isPlaying) yield return new ExitPlayMode();
    }

    [TestCase(0, 0, 0, 0, 1, 1, "GAMEPLAY REGRESSION CLEAN")]
    [TestCase(1, 1, 1, 1, 1, 1, "GAME REGRESSION FOUND")]
    [TestCase(0, 1, 0, 0, 1, 1, "BOT FAILS / ABORTED PRESENT")]
    [TestCase(0, 0, 1, 0, 1, 1, "BOT FAILS / ABORTED PRESENT")]
    [TestCase(0, 0, 0, 1, 1, 1, "BOT FAILS / ABORTED PRESENT")]
    [TestCase(0, 0, 0, 0, 0, 1, "BOT FAILS / ABORTED PRESENT")]
    public void StatusDistinguishesCleanIncompleteAndRegression(int game, int combat, int assertion, int aborted, int completed, int requested, string expected)
    {
        var type = typeof(BotBatchRunner).Assembly.GetType("GoldenPathLab");
        Assert.That(type, Is.Not.Null, "Dev lab must exist");
        Assert.That(type.GetMethod("StatusFor").Invoke(null, new object[] { game, combat, assertion, aborted, completed, requested }), Is.EqualTo(expected));
    }
}
#endif
