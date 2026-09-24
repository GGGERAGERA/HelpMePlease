#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;

[Category("Core")]
public sealed class Subject42GoldenPathTests
{
    [SetUp] public void PreservePreferences() => CoreTestSupport.PreservePreferences();
    [UnityTearDown] public IEnumerator Cleanup() => CoreTestSupport.CleanupPlayMode();

    [UnityTest, Timeout(360000)]
    public IEnumerator ProductionS1S2S3BossBunkerAndSecondRun()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
        yield return new EnterPlayMode();
        yield return ExerciseRoute();
    }

    private static IEnumerator ExerciseRoute()
    {
        PlayerPrefs.DeleteKey(TutorialController.CompletionKey);
        yield return CoreTestSupport.LoadBunker();
        RunSelectionManager.Instance.SelectCharacter(AssetDatabase.LoadAssetAtPath<CharacterData>(
            "Assets/_Project/Data/Characters/01_Gera.asset"));
        var session = BotRunSession.Ensure();
        var batch = session.GetComponent<BotBatchRunner>() ?? session.gameObject.AddComponent<BotBatchRunner>();
        Assert.That(batch.StartGoldenPathBatch(1, BotSeedMode.Fixed, 48151623, 5f), Is.True);
        float deadline = Time.realtimeSinceStartup + 300f;
        while (batch.IsActive && Time.realtimeSinceStartup < deadline)
        {
            Assert.That(TutorialController.IsActive, Is.False, "Bot context must bypass an incomplete tutorial.");
            yield return null;
        }
        Assert.That(batch.IsActive, Is.False, "Golden Path exceeded five minutes.");
        Assert.That(batch.Result.Status, Is.EqualTo("Completed"), batch.Result.StopReason);
        Assert.That(batch.Result.CompletedRuns, Is.EqualTo(1));
        var result = batch.Result.Results.Single();
        Assert.That(result.Result, Is.EqualTo("GoldenPathPassed"), batch.Result.Report());
        Assert.That(result.GoldenPath.SectorsCompleted, Is.EqualTo(3));
        Assert.That(result.GoldenPath.BossKilled, Is.True);
        Assert.That(result.GoldenPath.BunkerClean, Is.True);
        Assert.That(result.GoldenPath.SecondRunBaseline, Is.True);
    }
}
#endif
