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

public sealed partial class Subject42GoldenPathTests
{
    // A known wind-particle initialization assertion is outside this gameplay/state suite.
    private static void ExpectKnownVisualLog(string message, string stack, LogType type)
    {
        if (type == LogType.Assert && message.StartsWith("Setting the duration while system is still playing", System.StringComparison.Ordinal)
            && stack.Contains("WorldRuleVisual:EnsureWindResources")) LogAssert.Expect(type, message);
    }

    [SetUp]
    public void PreservePreferences() => new Subject42FinalBossFlowTests().PreserveRewardsAndUnlockProgress();

    [Category("Core")]
    [UnityTest, Timeout(1200000)]
    public IEnumerator Gera_Circle_GoldenPath() => Run(CharacterId.Gera, "01_Gera");

    [Category("Core")]
    [UnityTest, Timeout(1200000)]
    public IEnumerator DiMag_FigureEight_GoldenPath() => Run(CharacterId.DiMag, "02_Di-mag");

    [Category("Core")]
    [UnityTest, Timeout(1200000)]
    public IEnumerator Vika_Custom_GoldenPath() => Run(CharacterId.Vika, "03_Vika");

    private static IEnumerator Run(CharacterId expectedId, string assetName)
    {
        yield return new EnterPlayMode();
        Application.logMessageReceived += ExpectKnownVisualLog;
        yield return SceneManager.LoadSceneAsync("MainMenu");
        float deadline = Time.realtimeSinceStartup + 30f;
        while ((Object.FindFirstObjectByType<BunkerRunStarter>() == null || SceneTransitionOverlay.IsTransitioning) &&
               Time.realtimeSinceStartup < deadline) yield return null;
        Assert.That(Object.FindFirstObjectByType<BunkerRunStarter>(), Is.Not.Null, "Bunker startup timed out");
        Assert.That(SceneTransitionOverlay.IsTransitioning, Is.False);
        var character = AssetDatabase.LoadAssetAtPath<CharacterData>(
            "Assets/_Project/Scriptable Objects/Characters/" + assetName + ".asset");
        Assert.That(character, Is.Not.Null);
        Assert.That(character.Id, Is.EqualTo(expectedId));
        RunSelectionManager.Instance.SelectCharacter(character);
        var session = BotRunSession.Ensure();
        var batch = session.GetComponent<BotBatchRunner>() ?? session.gameObject.AddComponent<BotBatchRunner>();
        Assert.That(batch.StartGoldenPathBatch(1, BotSeedMode.Fixed, 48151623, 5f), Is.True);
        deadline = Time.realtimeSinceStartup + 900f;
        int firstRunId = 0;
        bool observedSecondRunCharacter = false;
        while (batch.IsActive && Time.realtimeSinceStartup < deadline)
        {
            var spawned = Object.FindFirstObjectByType<CharacterSpawner>();
            var run = RunStateManager.Instance;
            if (spawned != null && spawned.SpawnedPlayer != null && spawned.SpawnedCharacterData != null &&
                run != null && !run.IsRunEnded)
            {
                Assert.That(spawned.SpawnedCharacterData.Id, Is.EqualTo(expectedId),
                    "Requested character must survive every sector and second-run startup");
                if (firstRunId == 0) firstRunId = run.RunId;
                else if (run.RunId != firstRunId) observedSecondRunCharacter = true;
            }
            yield return null;
        }
        Assert.That(batch.IsActive, Is.False, "Golden Path timed out: " + expectedId);
        Assert.That(batch.Result.Status, Is.EqualTo("Completed"), batch.Result.StopReason);
        Assert.That(batch.Result.CompletedRuns, Is.EqualTo(1));
        Assert.That(batch.Result.Results.Count, Is.EqualTo(1));
        var result = batch.Result.Results.Single();
        Assert.That(result.Character, Is.EqualTo(character.name), "Runner must exercise the requested character");
        Assert.That(result.Result, Is.EqualTo("GoldenPathPassed"), batch.Result.Report());
        Assert.That(result.GoldenPath.SectorsCompleted, Is.EqualTo(3));
        Assert.That(result.GoldenPath.BossKilled, Is.True);
        Assert.That(result.GoldenPath.BunkerClean, Is.True);
        Assert.That(result.GoldenPath.SecondRunBaseline, Is.True);
        Assert.That(observedSecondRunCharacter, Is.True, "Second run must spawn the same requested character");
        Assert.That(RunSelectionManager.Instance.SelectedCharacter.Id, Is.EqualTo(expectedId));
        yield return new ExitPlayMode();
    }
    [UnityTearDown]
    public IEnumerator Cleanup()
    {
        Application.logMessageReceived -= ExpectKnownVisualLog;
        if (BotRunSession.Current != null)
        {
            var batch = BotRunSession.Current.GetComponent<BotBatchRunner>();
            if (batch != null && batch.IsActive) batch.StopBatch();
        }
        var cleanup = new Subject42FinalBossFlowTests().CleanupPlayMode();
        while (cleanup.MoveNext()) yield return cleanup.Current;
        BotRunSeed.End();
    }
}
#endif
