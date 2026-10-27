#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using NUnit.Framework;
using Subject42.Combat.OrbitalStation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;

[Category("Core")]
public sealed class Subject42GoldenPathTests
{
    [SetUp] public void PreservePreferences() => CoreTestSupport.PreservePreferences();
    [UnityTearDown] public IEnumerator Cleanup() => CoreTestSupport.CleanupPlayMode();

    [Test]
    public void BunkerBaselineRequiresAuthoredIdleComponentsAndDestroyedRunObjects()
    {
        var station = new GoldenBunkerComponentState("MainMenu|Station|SYSTEM/Player/Gera/Station", true, false);
        var rewardFlow = new GoldenBunkerComponentState("MainMenu|RewardFlow|SYSTEM/Player/Gera/Station", true, false);
        var baseline = new[] { station, rewardFlow };
        Assert.That(GoldenPathResult.BunkerComponentsMatchBaseline(baseline, new[] { station, rewardFlow }, true), Is.True,
            "A newly loaded bunker may contain the same authored idle components as before the run.");
        Assert.That(GoldenPathResult.BunkerComponentsMatchBaseline(baseline, baseline, false), Is.False, "Tracked run objects survived.");
        Assert.That(GoldenPathResult.BunkerComponentsMatchBaseline(baseline, new[] { station, rewardFlow, station }, true), Is.False, "Duplicate baseline path.");
        Assert.That(GoldenPathResult.BunkerComponentsMatchBaseline(baseline, new[] { station }, true), Is.False, "Missing baseline component.");
        Assert.That(GoldenPathResult.BunkerComponentsMatchBaseline(baseline, new[] {
            new GoldenBunkerComponentState(station.Key, true, true), rewardFlow }, true), Is.False, "Initialized/state/owner leak.");
        Assert.That(GoldenPathResult.BunkerComponentsMatchBaseline(baseline, new[] {
            station, new GoldenBunkerComponentState(rewardFlow.Key, false, true) }, true), Is.False, "Even inactive pending reward is a leak.");
        Assert.That(GoldenPathResult.BunkerComponentsMatchBaseline(baseline, new[] {
            station, rewardFlow, new GoldenBunkerComponentState("UnexpectedStation", true, false) }, true), Is.False, "Unregistered active object.");
    }

    [TestCase(OrbitalRewardKind.ShootingDamage)]
    [TestCase(OrbitalRewardKind.ShootingRate)]
    [TestCase(OrbitalRewardKind.ContactDamage)]
    [TestCase(OrbitalRewardKind.ContactSize)]
    public void CategoryRewardRequiresExactlyOneIsolatedUpgrade(OrbitalRewardKind kind)
    {
        var before = OrbitalRunState.CreateDefault(42);
        // Exercise multiplication on an already upgraded state as well as the baseline.
        Assert.That(before.TryUpgradeCategory(kind), Is.True);
        OrbitalRunState Copy() => JsonUtility.FromJson<OrbitalRunState>(JsonUtility.ToJson(before));
        var after = Copy();
        Assert.That(after.TryUpgradeCategory(kind), Is.True);
        bool exact = GoldenPathResult.RewardDeltaIsExact(before, after, kind);
        Assert.That(exact, Is.True, () => CategoryFailureDetails(before, after, kind));

        after = Copy();
        after.Revision++;
        Assert.That(GoldenPathResult.RewardDeltaIsExact(before, after, kind), Is.False, "Missing upgrade");
        after = Copy();
        Assert.That(after.TryUpgradeCategory(kind), Is.True);
        Assert.That(after.TryUpgradeCategory(kind), Is.True);
        after.Revision = before.Revision + 1;
        Assert.That(GoldenPathResult.RewardDeltaIsExact(before, after, kind), Is.False, "Double multiplier with one revision");
        after = Copy();
        Assert.That(after.TryUpgradeCategory(kind), Is.True);
        var other = kind == OrbitalRewardKind.ShootingDamage ? OrbitalRewardKind.ContactSize : OrbitalRewardKind.ShootingDamage;
        Assert.That(after.TryUpgradeCategory(other), Is.True);
        after.Revision = before.Revision + 1;
        Assert.That(GoldenPathResult.RewardDeltaIsExact(before, after, kind), Is.False, "Unrelated category changed");
        after = Copy();
        Assert.That(after.TryUpgradeCategory(kind), Is.True);
        after.Rings[0].MountCapacity++;
        Assert.That(GoldenPathResult.RewardDeltaIsExact(before, after, kind), Is.False, "Ring structure changed");
        after = Copy();
        Assert.That(after.TryUpgradeCategory(kind), Is.True);
        after.Modules[0].DamageLevel++;
        Assert.That(GoldenPathResult.RewardDeltaIsExact(before, after, kind), Is.False, "Module changed");
        after = Copy();
        Assert.That(after.TryUpgradeCategory(kind), Is.True);
        after.Revision++;
        Assert.That(GoldenPathResult.RewardDeltaIsExact(before, after, kind), Is.False, "Revision must increase once");
    }

    private static string CategoryFailureDetails(OrbitalRunState before, OrbitalRunState after, OrbitalRewardKind kind)
    {
        string Multiplier(string name, float previous, float actual, float factor)
        {
            float expected = previous * factor;
            return $"{name}: before={previous.ToString("R", System.Globalization.CultureInfo.InvariantCulture)} " +
                $"actual={actual.ToString("R", System.Globalization.CultureInfo.InvariantCulture)} " +
                $"expected={expected.ToString("R", System.Globalization.CultureInfo.InvariantCulture)} equal={actual == expected}";
        }
        return $"kind={kind}; Revision before={before.Revision} after={after.Revision} expected={before.Revision + 1}\n" +
            Multiplier("ShootingDamage", before.ShootingDamageMultiplier, after.ShootingDamageMultiplier, kind == OrbitalRewardKind.ShootingDamage ? 1.2f : 1f) + "\n" +
            Multiplier("ShootingRate", before.ShootingRateMultiplier, after.ShootingRateMultiplier, kind == OrbitalRewardKind.ShootingRate ? 1.15f : 1f) + "\n" +
            Multiplier("ContactDamage", before.ContactDamageMultiplier, after.ContactDamageMultiplier, kind == OrbitalRewardKind.ContactDamage ? 1.2f : 1f) + "\n" +
            Multiplier("ContactSize", before.ContactSizeMultiplier, after.ContactSizeMultiplier, kind == OrbitalRewardKind.ContactSize ? 1.15f : 1f) + "\n" +
            $"ringsEqual={before.Rings.Select(r => JsonUtility.ToJson(r)).SequenceEqual(after.Rings.Select(r => JsonUtility.ToJson(r)))}; " +
            $"modulesEqual={before.Modules.Select(m => JsonUtility.ToJson(m)).SequenceEqual(after.Modules.Select(m => JsonUtility.ToJson(m)))}\n" +
            $"before={JsonUtility.ToJson(before)}\nafter={JsonUtility.ToJson(after)}";
    }

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
        if (batch.Result.Status == "Failed")
            foreach (var failedRun in batch.Result.Results)
                if (failedRun.GoldenPath != null)
                    foreach (string damage in failedRun.GoldenPath.RecentDamage)
                        Debug.Log("Golden Path RecentDamage: " + damage);
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
