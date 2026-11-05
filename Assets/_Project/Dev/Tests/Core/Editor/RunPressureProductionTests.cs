#if UNITY_EDITOR
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public sealed class RunPressureProductionTests
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private const string Output = "Artifacts/GeneratedQA/RunPressure";

    [UnityTearDown]
    public IEnumerator Cleanup()
    {
        if (Application.isPlaying) yield return new ExitPlayMode();
        PlayerPrefs.EndSandbox();
        GameplayRandom.End();
    }

    // Short production route: authored combat, real exits/rewards/maps, no sector-duration waits or boss kill.
    [UnityTest, Timeout(150000)]
    public IEnumerator PressurePersistsThroughFiveSectorsAndBossAndResetsOnlyOnNewRun()
    {
        BunkerDebugTools.SetGameView1600();
        PlayerPrefs.BeginSandbox(true);
        PlayerPrefs.SetInt(BunkerIntroController.ViewedPreferenceKey, 1);
        PlayerPrefs.SetInt(TutorialController.CompletionKey, 1);
        EditorSceneManager.OpenScene("Assets/_Project/Scenes/MainBuild/MainMenu.unity");
        yield return new EnterPlayMode();
        yield return ExercisePressure();
    }

    // Create captured locals after EnterPlayMode's domain reload, as in the existing route smokes.
    private static IEnumerator ExercisePressure()
    {
        yield return CoreTestSupport.Await(() => ProductionSceneComposition.Active is { IsReady: true });
        var starter = Object.FindFirstObjectByType<BunkerRunStarter>();
        var map = starter.SurfaceMap.Definition;
        var entryConfig = map.Find("A1").BuildRunConfig(map.Id);
        Assert.That(entryConfig.ThreatGrowth, Is.EqualTo(.75f), "Exercise a production route with a retired growth multiplier.");
        starter.StartRun(starter.transform, config: entryConfig);
        yield return CoreTestSupport.Await(() => SceneManager.GetActiveScene().name == "MVP" &&
            ProductionSceneComposition.Active is { IsReady: true } && !SceneTransitionOverlay.IsTransitioning);
        var run = RunStateManager.Instance;
        Assert.That(run, Is.Not.Null, "MVP must retain the run created by BunkerRunStarter.");
        Assert.That(run.ThreatValue, Is.Zero);
        float firstMapPressure = run.ThreatValue;
        yield return new WaitForSecondsRealtime(.15f);
        Assert.That(run.ThreatValue, Is.EqualTo(firstMapPressure), "The initial map must not accumulate pressure.");
        yield return CoreTestSupport.SelectInitialRouteNode();
        yield return CoreTestSupport.Await(() => !SceneTransitionOverlay.IsTransitioning &&
            RunFlowController.Instance.Phase == RunPhase.NormalSector);
        PlayerRuntimeReference.PlayerHealth.DebugInvulnerable = true;
        Directory.CreateDirectory(Output);
        var threat = Object.FindFirstObjectByType<RunThreatController>();
        var spawner = Object.FindFirstObjectByType<EnemySpawner>();

        // These snapshots include the actual EnemySpawner formulas and profile selection.
        var pressures = new[] { 0f, 1f, 24f, 25f, 49f, 50f, 74f, 75f, 99f, 100f };
        var presetIndices = new[] { 0, 0, 0, 1, 1, 3, 3, 4, 4, 4 };
        var intervals = new[] { 1.365f, 1f, 1.45f, 1.9f };
        var caps = new[] { 12, 22, 32, 36 };
        var batches = new[] { 1, 1, 2, 3 };
        (float interval, int alive, int batch) previous = default;
        int previousStage = -1;
        for (int i = 0; i < pressures.Length; i++)
        {
            run.SetThreatForDebug(pressures[i]);
            threat.DebugRefreshPreset();
            int stage = pressures[i] >= 75 ? 3 : pressures[i] >= 50 ? 2 : pressures[i] >= 25 ? 1 : 0;
            Assert.That(threat.AppliedPresetIndex, Is.EqualTo(presetIndices[i]));
            Assert.That(Field<float>(spawner, "runThreatSpawnIntervalMultiplier"), Is.EqualTo(intervals[stage]).Within(.0001f));
            Assert.That(Field<int>(spawner, "runThreatMaxAliveCap"), Is.EqualTo(caps[stage]));
            Assert.That(Field<int>(spawner, "runThreatBatchSize"), Is.EqualTo(batches[stage]));
            var current = (Call<float>(spawner, "GetCurrentSpawnInterval"),
                Call<int>(spawner, "GetCurrentMaxAlive"), Call<int>(spawner, "GetCurrentEnemiesPerCycle"));
            if (stage == previousStage) Assert.That(current, Is.EqualTo(previous), "No per-percent difficulty scaling.");
            previous = current;
            previousStage = stage;
            AssertMeter(HUDManager.Instance.GetComponentInChildren<RunPressureMeter>(), pressures[i]);
        }
        yield return new WaitForSeconds(.15f);
        Assert.That(run.ThreatValue, Is.EqualTo(100f), "100% remains the maximum of stage IV.");
        Assert.That(threat.AppliedPresetIndex, Is.EqualTo(4));

        foreach (var crossing in new[] { (24.99f, 25f, 1), (49.99f, 50f, 3), (74.99f, 75f, 4), (99.99f, 100f, 4) })
        {
            run.SetThreatForDebug(crossing.Item1);
            threat.DebugRefreshPreset();
            yield return CoreTestSupport.Await(() => run.ThreatValue >= crossing.Item2);
            Assert.That(threat.AppliedPresetIndex, Is.EqualTo(crossing.Item3), "The combat tick must apply each crossed stage.");
        }

        run.SetThreatForDebug(52f);
        threat.DebugRefreshPreset();
        float before = run.ThreatValue;
        float gameTime = Time.time;
        yield return new WaitForSeconds(.3f);
        Assert.That(run.ThreatValue - before, Is.EqualTo((Time.time - gameTime) * .25f).Within(.025f),
            "The configured rate is linear and does not use the route growth multiplier.");
        Assert.That(spawner.IsSpawningEnabled, Is.True);
        yield return CoreTestSupport.Await(() => spawner.DebugTrackedEnemyCount > 0);
        Assert.That(EnemyHealth.ActiveInstances.Any(enemy => enemy != null && !enemy.IsBoss), Is.True);

        var pause = Object.FindFirstObjectByType<PauseMenuUI>();
        pause.Pause();
        Assert.That(pause.IsPaused, Is.True);
        before = run.ThreatValue;
        yield return new WaitForSecondsRealtime(.2f);
        Assert.That(run.ThreatValue, Is.EqualTo(before));
        pause.Resume();

        run.SetThreatForDebug(75f);
        threat.DebugRefreshPreset();
        yield return null;
        ScreenCapture.CaptureScreenshot(Output + "/combat-pressure.png");
        yield return new WaitForSecondsRealtime(.15f);

        for (int sector = 1; sector <= 5; sector++)
        {
            var flow = RunFlowController.Instance;
            Assert.That(run.CurrentSector.SectorNumber, Is.EqualTo(sector));
            PlayerRuntimeReference.PlayerHealth.DebugInvulnerable = true;
            typeof(RunFlowController).GetProperty(nameof(RunFlowController.SectorElapsedTime))
                .SetValue(flow, run.CurrentSector.Duration);
            yield return CoreTestSupport.Await(() => flow.IsExitUnlocked);
            before = run.ThreatValue;
            yield return new WaitForSeconds(.2f);
            Assert.That(run.ThreatValue, Is.GreaterThan(before), "The physical open exit leaves pressure running.");
            Assert.That(spawner.IsSpawningEnabled, Is.True);
            var exit = ProductionSectorExit.ActiveExits.Single();
            PlayerRuntimeReference.CachedPlayer.transform.position = exit.transform.position;
            Physics2D.SyncTransforms();
            yield return CoreTestSupport.Await(() => UpgradeManager.Instance.IsChoosingUpgrade);
            before = run.ThreatValue;
            yield return new WaitForSecondsRealtime(.15f);
            Assert.That(run.ThreatValue, Is.EqualTo(before), "Rewards freeze pressure.");
            Assert.That(UpgradeManager.Instance.DebugSelectCurrentChoice(0), Is.True);
            if (sector == 5)
            {
                yield return CoreTestSupport.Await(() => flow.Phase == RunPhase.FinalBossCombat);
                Assert.That(run.ThreatValue, Is.GreaterThanOrEqualTo(before), "Boss entry retains the expedition value.");
                Assert.That(flow.FinalBoss != null && flow.FinalBoss.IsBoss, Is.True);
                before = run.ThreatValue;
                yield return new WaitForSeconds(.2f);
                Assert.That(run.ThreatValue, Is.GreaterThan(before), "Combat pressure continues against the boss.");
                break;
            }
            var choice = Object.FindFirstObjectByType<LevelChoiceManager>();
            yield return CoreTestSupport.Await(() => choice.IsChoosing);
            before = run.ThreatValue;
            var mapMeter = GameObject.Find("Route pressure").GetComponentInParent<RunPressureMeter>();
            AssertMeter(mapMeter, before);
            yield return new WaitForSecondsRealtime(.15f);
            Assert.That(run.ThreatValue, Is.EqualTo(before), "The route map freezes pressure.");
            if (sector == 1)
            {
                ScreenCapture.CaptureScreenshot(Output + "/map-pressure.png");
                yield return new WaitForSecondsRealtime(.15f);
            }
            Assert.That(choice.DebugSelectFirstRule(), Is.True);
            yield return CoreTestSupport.Await(() => !SceneTransitionOverlay.IsTransitioning &&
                run.CurrentSector.SectorNumber == sector + 1 && RunFlowController.Instance.Phase == RunPhase.NormalSector);
            Assert.That(run.ThreatValue, Is.InRange(before, before + .15f), "A scene reload retains pressure.");
            spawner = Object.FindFirstObjectByType<EnemySpawner>();
        }

        before = run.ThreatValue;
        var character = run.SelectedCharacter;
        var localAnomaly = run.CurrentSector.LocalAnomaly;
        var worldRule = run.CurrentSector.WorldRule;
        run.EndRun(RunEndReason.ReturnedToBunker);
        Assert.That(run.ThreatValue, Is.EqualTo(before), "Ending a run does not reset its pressure.");
        run.BeginNewRun(character, null, AssetDatabase.LoadAssetAtPath<StageProfileData>(
                "Assets/_Project/Data/Stages/StageProfiles/StageProfile_01.asset"), worldRule, localAnomaly, null,
            isDevelopmentRun: true, config: new RunConfig("test", "test", new RunConfigParameters { initialThreat = 25f }));
        Assert.That(run.ThreatValue, Is.Zero);
        Assert.That(run.ThreatElapsedTime, Is.Zero);
    }

    private static T Field<T>(object target, string name) => (T)target.GetType().GetField(name, Private).GetValue(target);
    private static T Call<T>(object target, string name) => (T)target.GetType().GetMethod(name, Private).Invoke(target, null);

    private static void AssertMeter(RunPressureMeter meter, float value)
    {
        Assert.That(meter, Is.Not.Null);
        Assert.That(meter.Value, Is.EqualTo(value));
        Assert.That(((RectTransform)meter.transform.Find("Pressure track/Pressure fill")).anchorMax.x,
            Is.EqualTo(value / 100f).Within(.00001f));
        Assert.That(meter.GetComponentInChildren<TMP_Text>().text, Does.EndWith("%"));
        Assert.That(meter.transform.Find("Pressure track/Pressure fill").GetComponent<Image>().color,
            Is.EqualTo(value >= 75 ? (Color)new Color32(0xEF, 0x4B, 0x5E, 255) : value >= 50 ? (Color)Subject42UITheme.Coral :
                value >= 25 ? (Color)Subject42UITheme.Amber : (Color)Subject42UITheme.Cyan));
    }
}
#endif
