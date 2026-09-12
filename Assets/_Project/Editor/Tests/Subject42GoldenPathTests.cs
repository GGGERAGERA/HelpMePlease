#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Subject42.Combat.OrbitalStation;
using Object = UnityEngine.Object;

public sealed class Subject42GoldenPathTests
{
    // A known wind-particle initialization assertion is outside this gameplay/state suite.
    private static void ExpectKnownVisualLog(string message, string stack, LogType type)
    {
        if (type == LogType.Assert && message.StartsWith("Setting the duration while system is still playing", System.StringComparison.Ordinal)
            && stack.Contains("WorldRuleVisual:EnsureWindResources")) LogAssert.Expect(type, message);
    }
    [Test]
    public void RewardCheckRejectsDoubleGrantWithinOneCommit()
    {
        var check = typeof(GoldenPathResult).GetMethod("RewardDeltaIsExact");
        Assert.That(check, Is.Not.Null, "Exact reward delta validator is required");
        var before = OrbitalRunState.CreateDefault(1);
        var after = JsonUtility.FromJson<OrbitalRunState>(JsonUtility.ToJson(before));
        after.AddRing();
        Assert.That(check.Invoke(null, new object[] { before, after, OrbitalRewardKind.NewRing }), Is.EqualTo(true));
        after.AddRing();
        Assert.That(check.Invoke(null, new object[] { before, after, OrbitalRewardKind.NewRing }), Is.EqualTo(false));
    }

    [Test]
    public void BaselineRejectsLeakedDamageAndRingPower()
    {
        var state = OrbitalRunState.CreateDefault(29);
        Assert.That(GoldenPathResult.BaselineMatches(state), Is.True);
        state.Modules[0].DamageLevel = 1;
        Assert.That(GoldenPathResult.BaselineMatches(state), Is.False);
        state.Modules[0].DamageLevel = 0;
        state.Rings[0].PowerUpgradeLevel = 1;
        Assert.That(GoldenPathResult.BaselineMatches(state), Is.False);
    }

    [Test]
    public void GoldenPathIsAnExistingBatchMode()
    {
        Assert.That(typeof(BotBatchRunner).GetMethod("StartGoldenPathBatch"), Is.Not.Null,
            "Golden Path must extend BotBatchRunner");
    }

    [UnityTest, Timeout(7200000)]
    public IEnumerator One() => Run(1);
    [UnityTest, Timeout(7200000)]
    public IEnumerator Ten() => Run(10);
    [UnityTest, Timeout(7200000)]
    public IEnumerator Hundred() => Run(100);
    [UnityTest, Timeout(7200000)]
    public IEnumerator TenAtFive() => Run(10, speed: 5f);
    [UnityTest, Timeout(7200000)]
    public IEnumerator HundredAtFive() => Run(100, speed: 5f);
    [UnityTest, Timeout(7200000)]
    public IEnumerator ReplayLastFailure()
    {
        var history = JsonUtility.FromJson<GoldenPathBatchHistory>(System.IO.File.ReadAllText("Artifacts/BotBatches/golden_path_history.json"));
        int seed = history.Batches.SelectMany(b => b.Results).Last(r => r.GoldenPath?.Result == "FAIL").Seed;
        return Run(1, seed);
    }
    [UnityTest, Timeout(7200000)]
    public IEnumerator ReplayLastFailureAtFive()
    {
        var history = JsonUtility.FromJson<GoldenPathBatchHistory>(System.IO.File.ReadAllText("Artifacts/BotBatches/golden_path_history.json"));
        int seed = history.Batches.SelectMany(b => b.Results).Last(r => r.GoldenPath?.Result == "FAIL").Seed;
        return Run(1, seed, 5f);
    }

    private static IEnumerator Run(int count, int firstSeed = 48151623, float speed = 10f)
    {
        yield return new EnterPlayMode();
        Application.logMessageReceived += ExpectKnownVisualLog;
        yield return SceneManager.LoadSceneAsync("MainMenu");
        float deadline = Time.realtimeSinceStartup + 30;
        while (Object.FindFirstObjectByType<BunkerRunStarter>() == null && Time.realtimeSinceStartup < deadline) yield return null;
        var character = AssetDatabase.FindAssets("t:CharacterData")
            .Select(id => AssetDatabase.LoadAssetAtPath<CharacterData>(AssetDatabase.GUIDToAssetPath(id)))
            .Where(c => c != null && c.characterPrefab != null).OrderBy(c => c.name, System.StringComparer.Ordinal).First();
        RunSelectionManager.Instance.SelectCharacter(character);
        var session = BotRunSession.Ensure();
        var batch = session.gameObject.AddComponent<BotBatchRunner>();
        var start = typeof(BotBatchRunner).GetMethod("StartGoldenPathBatch");
        Assert.That(start, Is.Not.Null);
        Assert.That(start.Invoke(batch, new object[] { count, BotSeedMode.Auto, firstSeed, speed }), Is.EqualTo(true));
        deadline = Time.realtimeSinceStartup + 7000;
        while (batch.IsActive && Time.realtimeSinceStartup < deadline) yield return null;
        Assert.That(batch.IsActive, Is.False, "Golden Path batch timed out");
        Assert.That(batch.Result.Status, Is.EqualTo("Completed"), batch.Result.StopReason);
        Assert.That(batch.Result.CompletedRuns, Is.EqualTo(count));
        Assert.That(batch.Result.Results.All(r => r.Result == "GoldenPathPassed"), Is.True, batch.Result.Report());
        yield return new ExitPlayMode();
    }
    [UnityTearDown]
    public IEnumerator Cleanup()
    {
        Application.logMessageReceived -= ExpectKnownVisualLog;
        if (Application.isPlaying) yield return new ExitPlayMode();
        BotRunSeed.End();
    }
}
#endif
