#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

// Opt-in measurement only. No production assets, balance values or gameplay formulas are changed.
public sealed class RunPressureIntensityAuditTests
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private const string Output = "Artifacts/GeneratedQA/RunPressureIntensity";
    [Serializable] private sealed class Kind
    {
        public string id, prefab;
        public float weight;
        public int authoredTypeCap;
        public bool exactTypeCap;
    }
    [Serializable] private sealed class Count { public string id, prefab; public int count; }
    [Serializable] private sealed class Trial
    {
        public int seed, spawned, peakAlive, finalAlive;
        public float meanAlive;
        public Count[] composition;
    }
    [Serializable] private sealed class Row
    {
        public float pressure, interval, refill, potentialPerMinute;
        public int phase, batch, aliveCap, cyclesIn60, singleSlotRefillCount;
        public Kind[] eligible;
        public Trial[] freeSlots, noRemovals;
    }
    [Serializable] private sealed class Audit
    {
        public string utc, scene = "MVP", stage = "StageProfile_03", profile = "ESP_Level_03", worldRule = "None";
        public string method = "Actual production SpawnCycle calls on a frozen scene, scheduled by GetCurrentSpawnInterval; 60 virtual game seconds, no frame quantization or automatic formations. Free slots retire actors after each cycle without deaths/rewards. Three seeds per pressure and mode.";
        public float sectorSpawnPressure = 1.24f;
        public Row[] rows;
    }

    [UnityTearDown]
    public IEnumerator Cleanup()
    {
        Time.timeScale = 1f;
        if (Application.isPlaying) yield return new ExitPlayMode();
        GameplayRandom.End();
        PlayerPrefs.EndSandbox();
    }

    [UnityTest, Timeout(120000)]
    public IEnumerator CompareExistingStagesWithTheSameProductionProfile()
    {
        PlayerPrefs.BeginSandbox(true);
        PlayerPrefs.SetInt(BunkerIntroController.ViewedPreferenceKey, 1);
        PlayerPrefs.SetInt(TutorialController.CompletionKey, 1);
        EditorSceneManager.OpenScene("Assets/_Project/Scenes/MainBuild/MainMenu.unity");
        yield return new EnterPlayMode();
        yield return Measure();
    }

    private static IEnumerator Measure()
    {
        yield return CoreTestSupport.Await(() => ProductionSceneComposition.Active is { IsReady: true });
        var starter = Object.FindFirstObjectByType<BunkerRunStarter>();
        starter.StartRun(starter.transform);
        yield return CoreTestSupport.Await(() => SceneManager.GetActiveScene().name == "MVP" &&
            ProductionSceneComposition.Active is { IsReady: true } && !SceneTransitionOverlay.IsTransitioning);
        yield return CoreTestSupport.SelectInitialRouteNode();
        yield return CoreTestSupport.Await(() => RunFlowController.Instance.Phase == RunPhase.NormalSector);
        PlayerRuntimeReference.PlayerHealth.DebugInvulnerable = true;
        RunFlowController.Instance.StopRunGameplay();
        Time.timeScale = 0f;

        var run = RunStateManager.Instance;
        var stage = AssetDatabase.LoadAssetAtPath<StageProfileData>("Assets/_Project/Data/Stages/StageProfiles/StageProfile_03.asset");
        var none = AssetDatabase.LoadAssetAtPath<WorldRuleData>("Assets/_Project/Data/World/Rules/WorldRule_None.asset");
        Assert.That(none, Is.Not.Null);
        run.SetCurrentSector(new RunSector(3, stage, none, run.CurrentSector.LocalAnomaly));
        WorldRuleController.Instance.Apply(none);
        var spawner = Object.FindFirstObjectByType<EnemySpawner>();
        var threat = Object.FindFirstObjectByType<RunThreatController>();
        var config = AssetDatabase.LoadAssetAtPath<RunThreatConfig>("Assets/_Project/Data/World/RunThreatConfig.asset");
        Retire(spawner);
        yield return null;
        spawner.SetSpawnProfile(stage.SpawnProfile, 3);
        spawner.SetLevelScaling(stage.EnemyHealthMultiplier, stage.EnemySpeedMultiplier, stage.SpawnPressureMultiplier);
        spawner.SetWorldRuleSpawnPressureMultiplier(1f);
        spawner.SetWorldEventSpawnPressureMultiplier(1f);
        spawner.SetWorldAcceleration(1f);
        spawner.DebugSetCaptureDensity(1f);
        spawner.ResumeSpawning();
        threat.Initialize(config, spawner);

        var result = new Audit { utc = DateTime.UtcNow.ToString("O"), rows = new Row[5] };
        var pressures = new[] { 0f, 25f, 50f, 75f, 100f };
        var expectedCaps = new[] { 12, 21, 27, 30, 30 };
        var expectedCounts = new[] { 35, 51, 82, 105, 105 };
        var expectedIntervals = new[] { 1.70625f, 1.1693548f, 1.4616935f, 1.6854839f, 1.6854839f };
        for (int index = 0; index < pressures.Length; index++)
        {
            run.SetThreatForDebug(pressures[index]);
            threat.DebugRefreshPreset();
            var row = result.rows[index] = new Row
            {
                pressure = pressures[index], phase = threat.AppliedPresetIndex,
                interval = Call<float>(spawner, "GetCurrentSpawnInterval"), refill = spawner.NormalRefillMultiplier,
                batch = Call<int>(spawner, "GetCurrentEnemiesPerCycle"), aliveCap = Call<int>(spawner, "GetCurrentMaxAlive"),
                freeSlots = new Trial[3], noRemovals = new Trial[3]
            };
            Assert.That(row.refill, Is.EqualTo(1f));
            Assert.That(row.interval, Is.EqualTo(expectedIntervals[index]).Within(.00001f));
            Assert.That(row.aliveCap, Is.EqualTo(expectedCaps[index]));
            row.cyclesIn60 = Mathf.FloorToInt(60f / row.interval);
            row.potentialPerMinute = 60f * row.batch / row.interval;
            row.eligible = stage.SpawnProfile.Phases[row.phase].enemies
                .Where(entry => (bool)typeof(EnemySpawner).GetMethod("CanSpawn", Private).Invoke(spawner, new object[] { entry }))
                .Select(entry => new Kind { id = entry.enemyPrefab.GetComponent<EnemyIdentity>()?.EnemyId ?? entry.enemyPrefab.name,
                    prefab = entry.enemyPrefab.name, weight = entry.weight,
                    authoredTypeCap = entry.maxAliveOfType, exactTypeCap = entry.useExactAliveLimit }).ToArray();
            for (int sample = 0; sample < 3; sample++)
            {
                row.freeSlots[sample] = Sample(spawner, row, 4200 + sample, true);
                Assert.That(row.freeSlots[sample].spawned, Is.EqualTo(expectedCounts[index]), "Actual SpawnCycle creation with free slots.");
                Retire(spawner);
                yield return null;
                row.noRemovals[sample] = Sample(spawner, row, 4200 + sample, false);
                Assert.That(row.noRemovals[sample].peakAlive, Is.EqualTo(expectedCaps[index]));
                Assert.That(row.noRemovals[sample].finalAlive, Is.EqualTo(expectedCaps[index]));
                if (sample == 0)
                {
                    var tracked = (IList)typeof(EnemySpawner).GetField("activeEnemies", Private).GetValue(spawner);
                    var first = tracked[0];
                    Object.Destroy((GameObject)first.GetType().GetField("instance").GetValue(first));
                    yield return null;
                    int before = spawner.DebugCaptureAliveCount;
                    Assert.That(before, Is.EqualTo(row.aliveCap - 1));
                    typeof(EnemySpawner).GetMethod("SpawnCycle", Private).Invoke(spawner, new object[] { int.MaxValue });
                    row.singleSlotRefillCount = spawner.DebugCaptureAliveCount - before;
                    Assert.That(row.singleSlotRefillCount, Is.EqualTo(1), "A larger batch cannot refill more than the single available slot.");
                }
                Retire(spawner);
                yield return null;
            }
        }
        Assert.That(result.rows[4].interval, Is.EqualTo(result.rows[3].interval));
        Assert.That(result.rows[4].aliveCap, Is.EqualTo(result.rows[3].aliveCap));
        for (int sample = 0; sample < 3; sample++)
            Assert.That(JsonUtility.ToJson(result.rows[4].freeSlots[sample]),
                Is.EqualTo(JsonUtility.ToJson(result.rows[3].freeSlots[sample])), "100% has no additional stage or composition change.");
        Directory.CreateDirectory(Output);
        File.WriteAllText(Output + "/measurements.json", JsonUtility.ToJson(result, true));
    }

    private static Trial Sample(EnemySpawner spawner, Row row, int seed, bool retireEachCycle)
    {
        GameplayRandom.End();
        GameplayRandom.Begin(seed);
        var trial = new Trial { seed = seed };
        var counts = new Dictionary<string, Count>();
        void Spawned(EnemyHealth enemy)
        {
            string id = enemy.GetComponent<EnemyIdentity>()?.EnemyId ?? enemy.name;
            string prefab = enemy.name.Replace("(Clone)", string.Empty);
            if (!counts.TryGetValue(prefab, out var count))
                counts[prefab] = count = new Count { id = id, prefab = prefab };
            count.count++;
            trial.spawned++;
        }
        EnemyHealth.SpawnConfigured += Spawned;
        float areaUnderAlive = 0f, previousTime = 0f;
        try
        {
            for (int cycle = 1; cycle <= row.cyclesIn60; cycle++)
            {
                float time = cycle * row.interval;
                areaUnderAlive += spawner.DebugCaptureAliveCount * (time - previousTime);
                typeof(EnemySpawner).GetMethod("SpawnCycle", Private).Invoke(spawner, new object[] { int.MaxValue });
                trial.peakAlive = Mathf.Max(trial.peakAlive, spawner.DebugCaptureAliveCount);
                if (retireEachCycle) Retire(spawner);
                previousTime = time;
            }
            trial.finalAlive = spawner.DebugCaptureAliveCount;
            areaUnderAlive += trial.finalAlive * (60f - previousTime);
            trial.meanAlive = areaUnderAlive / 60f;
            trial.composition = counts.OrderBy(pair => pair.Key).Select(pair => pair.Value).ToArray();
        }
        finally { EnemyHealth.SpawnConfigured -= Spawned; GameplayRandom.End(); }
        return trial;
    }

    private static void Retire(EnemySpawner spawner)
    {
        foreach (var enemy in EnemyHealth.ActiveInstances.ToArray())
            if (enemy != null && !enemy.IsBoss) enemy.gameObject.SetActive(false);
        spawner.ClearDebugSpawnedEnemies();
    }
    private static T Call<T>(EnemySpawner spawner, string name) =>
        (T)typeof(EnemySpawner).GetMethod(name, Private).Invoke(spawner, null);
}
#endif
