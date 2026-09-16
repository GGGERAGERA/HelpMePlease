#if UNITY_EDITOR
using System;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public sealed class Subject42GoldenPathSummaryTests
{
    private static string Render(GoldenPathBatchHistory history)
    {
        var type = typeof(BotBatchRunner).Assembly.GetType("GoldenPathSummary");
        Assert.That(type, Is.Not.Null, "History summary generator must exist");
        return (string)type.GetMethod("Render").Invoke(null, new object[] { history });
    }

    private static BotRunResult Run(string outcome, string assertion = null, int seed = 7) => new()
    {
        Seed = seed, Character = "Gera", Result = outcome, Sector = 3,
        GoldenPath = new GoldenPathResult
        {
            Result = outcome == "GoldenPathPassed" ? "PASS" : "FAIL",
            Failures = assertion == null ? new() : new() { new GoldenPathFailure { Assertion = assertion, Sector = 3, Reason = "test | reason\nsecond line" } }
        }
    };

    private static BotBatchResult Batch(float speed, params BotRunResult[] runs) => new()
    { SimulationSpeed = speed, Strategy = "GoldenPath", RequestedRuns = runs.Length, Results = new(runs) };

    [Category("Extended")]
    [Test]
    public void AbortOverridesNestedFailureAndDeathIsNotGameRegression()
    {
        var h = new GoldenPathBatchHistory { Batches = new() { Batch(5, Run("Aborted", "Runner.Completed"), Run("GoldenPathFailed", "Run.PlayerAlive", 8)) } };
        string text = Render(h);
        StringAssert.Contains("GAME REGRESSIONS: 0", text);
        StringAssert.Contains("BOT COMBAT FAILS: 1", text);
        StringAssert.Contains("ASSERTION FAILS: 0", text);
        StringAssert.Contains("ABORTED: 1", text);
    }

    [Category("Extended")]
    [Test]
    public void RecoveryRequiresSameScenarioAndRecurrenceReopensIssue()
    {
        var h = new GoldenPathBatchHistory { Batches = new() { Batch(10, Run("GoldenPathFailed", "Cleanup.SceneObjects")), Batch(5, Run("GoldenPathPassed")) } };
        StringAssert.Contains("GAME REGRESSIONS: 1", Render(h));
        h.Batches.Add(Batch(10, Run("GoldenPathPassed")));
        StringAssert.Contains("GAME REGRESSIONS: 0", Render(h));
        StringAssert.Contains("FIXED/HISTORICAL", Render(h));
        h.Batches.Add(Batch(10, Run("GoldenPathFailed", "Cleanup.SceneObjects")));
        StringAssert.Contains("GAME REGRESSIONS: 1", Render(h));
    }

    [Category("Extended")]
    [Test]
    public void UnknownAssertionsStayVisibleAndMarkdownIsEscaped()
    {
        string text = Render(new GoldenPathBatchHistory { Batches = new() { Batch(10, Run("GoldenPathFailed", "Unknown.Check")) } });
        StringAssert.Contains("ASSERTION FAILS: 1", text);
        StringAssert.Contains("test \\| reason", text);
        StringAssert.DoesNotContain("\nsecond line", text);
        StringAssert.DoesNotContain("STATUS: GAMEPLAY REGRESSION CLEAN", text);
    }

    [Category("Extended")]
    [Test]
    public void EmptyHistoryIsNotClean()
    {
        StringAssert.Contains("STATUS: NO DATA", Render(new GoldenPathBatchHistory()));
    }

    [Category("Extended")]
    [TestCase("Runtime.NoException")]
    [TestCase("Rewards.GrantedOnce")]
    [TestCase("Orbital.ModulesNeverOverwritten")]
    [TestCase("Run.NoSector4")]
    [TestCase("Boss.VictoryOnce")]
    [TestCase("Cleanup.SceneObjects")]
    public void RecordedGameInvariantsAreNotCombatFailures(string assertion)
    {
        string text = Render(new GoldenPathBatchHistory { Batches = new() { Batch(10, Run("GoldenPathFailed", assertion)) } });
        StringAssert.Contains("GAME REGRESSIONS: 1", text);
        StringAssert.Contains("BOT COMBAT FAILS: 0", text);
    }

    [Category("Extended")]
    [Test]
    public void LegacyRewardValidatorWithoutApplicationEvidenceIsNotGameBug()
    {
        var token = Run("GoldenPathFailed", "Rewards.GrantedOnce");
        token.GoldenPath.Failures[0].Reason = "Reward token committed twice: 1:2";
        var delta = Run("GoldenPathFailed", "Rewards.ChosenRewardApplied", 8);
        delta.GoldenPath.Failures[0].Reason = "Chosen reward did not apply exactly once";
        string text = Render(new GoldenPathBatchHistory { Batches = new() { Batch(10, token, delta) } });
        StringAssert.Contains("GAME REGRESSIONS: 0", text);
        StringAssert.Contains("ASSERTION FAILS: 2", text);
    }

    [Category("Extended")]
    [Test]
    public void DifferentCharacterAndSeedVersionCannotCloseFailure()
    {
        var otherCharacter = Run("GoldenPathPassed");
        otherCharacter.Character = "Other";
        var otherVersion = Run("GoldenPathPassed");
        otherVersion.SeedVersion = 99;
        string text = Render(new GoldenPathBatchHistory { Batches = new() { Batch(10, Run("GoldenPathFailed", "Cleanup.SceneObjects")), Batch(10, otherCharacter, otherVersion) } });
        StringAssert.Contains("GAME REGRESSIONS: 1", text);
    }

    [Category("Extended")]
    [Test]
    public void AggregateFailureCountDoesNotTurnAbortIntoRegression()
    {
        var batch = Batch(5, Run("GoldenPathPassed"), Run("Aborted", "Runner.Completed"));
        batch.GoldenPathFailed = 25;
        batch.CompletedRuns = 100;
        string text = Render(new GoldenPathBatchHistory { Batches = new() { batch } });
        StringAssert.Contains("requested/completed runs: 2/1", text);
        StringAssert.Contains("ABORTED: 1", text);
        StringAssert.Contains("GAME REGRESSIONS: 0", text);
        StringAssert.Contains("ASSERTION FAILS: 0", text);
    }

    [Category("Extended")]
    [Test]
    public void SummaryGenerationPreservesItsInputHistory()
    {
        string directory = Path.Combine(Path.GetTempPath(), "Subject42Summary-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            string path = Path.Combine(directory, "golden_path_history.json");
            var history = new GoldenPathBatchHistory { Batches = new() { Batch(5f, Run("GoldenPathPassed")) } };
            File.WriteAllText(path, JsonUtility.ToJson(history));
            byte[] before = File.ReadAllBytes(path);
            GoldenPathSummary.WriteFromHistory(path);
            Assert.That(File.ReadAllText(Path.Combine(directory, "golden_path_summary.md")), Is.EqualTo(Render(history)));
            CollectionAssert.AreEqual(before, File.ReadAllBytes(path));
        }
        finally { Directory.Delete(directory, true); }
    }
}
#endif
