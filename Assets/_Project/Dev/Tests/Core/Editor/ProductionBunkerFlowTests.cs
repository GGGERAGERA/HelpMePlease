#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Subject42.Combat.OrbitalStation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public sealed class ProductionBunkerFlowTests
{
    private sealed class Storage : ISurfaceMapStorage { public string Load(string id) => ""; public void Save(string id,string json) { } }
    [SetUp] public void Preserve() => CoreTestSupport.PreservePreferences();
    [UnityTearDown] public IEnumerator Cleanup() => CoreTestSupport.CleanupPlayMode();
    private static T Read<T>(Object target,string name) => (T)target.GetType().GetField(name,BindingFlags.NonPublic|BindingFlags.Instance).GetValue(target);
    private static BunkerStation Station(BunkerStationType kind) => Object.FindObjectsByType<BunkerStation>(FindObjectsSortMode.None).First(s => Read<BunkerStationType>(s,"stationType") == kind);

    [UnityTest] public IEnumerator StartupStationsSurfaceRunRestartAbortDeathAndVictory()
    {
        EditorSceneManager.OpenScene("Assets/_Project/Scenes/MainBuild/StartScreen.unity");
        yield return new EnterPlayMode();
        // Allocate captured locals after domain reload: the Test Runner cannot restore closure objects.
        yield return ExerciseProductionFlow();
    }
    private static IEnumerator ExerciseProductionFlow()
    {
        Object.FindFirstObjectByType<StartScreenController>().Begin();
        yield return CoreTestSupport.Await(() => SceneManager.GetActiveScene().name == RunEndService.BunkerSceneName && !SceneTransitionOverlay.IsTransitioning);
        var context = BunkerContext.Instance;
        Assert.That(context, Is.Not.Null); Assert.That(context.StationProgression, Is.SameAs(BunkerStationProgressionService.Instance));
        Assert.That(Object.FindObjectsByType<RunSelectionManager>(FindObjectsSortMode.None).Length, Is.EqualTo(1));
        Assert.That(Object.FindObjectsByType<MetaProgressionManager>(FindObjectsSortMode.None).Length, Is.EqualTo(1));
        Assert.That(Object.FindObjectsByType<BunkerStationProgressionService>(FindObjectsSortMode.None).Length, Is.EqualTo(1));
        UnityEngine.ScreenCapture.CaptureScreenshot("Artifacts/SurfaceMap/production-bunker.png");
        foreach (var kind in new[] { BunkerStationType.CharacterSelection, BunkerStationType.WeaponSelection, BunkerStationType.Upgrade, BunkerStationType.AnomalyStabilizer })
        {
            Station(kind).Interact(); Assert.That(Object.FindFirstObjectByType<SelectionPanelController>().IsOpen, Is.True, kind.ToString()); context.Panels.CloseAll(false);
        }
        var pause = Object.FindFirstObjectByType<PauseMenuUI>(); pause.Pause(); Assert.That(pause.IsPaused, Is.True); pause.Resume();
        var football = Object.FindFirstObjectByType<FootballMinigame>(); Assert.That(football, Is.Not.Null);
        Assert.That(Read<CameraFollow>(football,"cameraFollow"), Is.Not.Null);

        // Exercise the real controllers without writing map progression into the user's save.
        var map = AssetDatabase.LoadAssetAtPath<SurfaceMapDefinition>(SurfaceMapProductionAuthoring.MapPath);
        var service = new SurfaceMapService(map,new Storage());
        typeof(MetaProgressionManager).GetProperty("SurfaceMap").SetValue(MetaProgressionManager.Instance,service);
        var exit = Station(BunkerStationType.StartRun); var target = Read<Transform>(exit,"runTransitionTarget");
        exit.Interact();
        var view = Object.FindFirstObjectByType<SurfaceMapView>(); Assert.That(view, Is.Not.Null);
        Assert.That(context.Panels.IsAnyPanelOpen, Is.True); Assert.That(service.TrySelect("C1"), Is.True);
        yield return null;
        UnityEngine.ScreenCapture.CaptureScreenshot("Artifacts/SurfaceMap/surface-map.png");
        yield return null;
        Read<Button>(view,"startButton").onClick.Invoke();
        yield return ReadyRun();
        var run = RunStateManager.Instance;
        Assert.That(run, Is.Not.Null, "RunState singleton lost after scene transition; instances=" + Object.FindObjectsByType<RunStateManager>(FindObjectsSortMode.None).Length);
        var config = run.CurrentConfig;
        Assert.That(config, Is.Not.Null, "RunConfig lost after scene transition");
        Assert.That(config.SectorId, Is.EqualTo("C1")); Assert.That(config.Gold, Is.EqualTo(1.5f)); Assert.That(run.ThreatValue, Is.GreaterThanOrEqualTo(15));
        var firstSector = run.CurrentSector;
        Assert.That(firstSector, Is.Not.Null); Assert.That(firstSector.StageProfile, Is.Not.Null);
        Assert.That(firstSector.SpawnPressureMultiplier, Is.EqualTo(firstSector.StageProfile.SpawnPressureMultiplier * config.SpawnPressure));
        var exploration = Object.FindFirstObjectByType<ProductionExplorationSectorController>();
        Assert.That(exploration, Is.Not.Null); Assert.That(exploration.Config, Is.SameAs(config.LayoutProfile));

        Assert.That(RunEndService.Instance, Is.Not.Null, "Gameplay RunEndService is missing");
        int previousId = run.RunId;
        RunEndService.Instance.RestartRun(RunEndReason.ReturnedToBunker);
        yield return CoreTestSupport.Await(() => run.RunId > previousId && !SceneTransitionOverlay.IsTransitioning && Object.FindFirstObjectByType<OrbitalStationRuntime>() is { IsInitialized: true });
        Assert.That(run.CurrentConfig, Is.SameAs(config)); Assert.That(service.GetStatus("C2"), Is.EqualTo(SurfaceSectorStatus.Locked));
        Assert.That(RunEndService.Instance, Is.Not.Null);
        RunEndService.Instance.ReturnToBunker(); yield return Bunker();
        Assert.That(service.GetStatus("C1"), Is.EqualTo(SurfaceSectorStatus.Available));

        // Start C again and use the actual death -> game-over -> bunker route.
        context = BunkerContext.Instance; exit = Station(BunkerStationType.StartRun); target = Read<Transform>(exit,"runTransitionTarget");
        context.RunStarter.StartSurfaceRun(target); yield return ReadyRun();
        var player = Object.FindFirstObjectByType<CharacterSpawner>().SpawnedPlayer;
        player.GetComponent<PlayerHealth>().TakeDamage(player.GetComponent<PlayerHealth>().MaxHealth * 2f,Vector2.zero);
        GameOverManager.Instance.MainMenu(); yield return Bunker();
        Assert.That(service.GetStatus("C2"), Is.EqualTo(SurfaceSectorStatus.Locked));

        // A Victory reason without final-boss confirmation must not advance the map.
        context = BunkerContext.Instance; exit = Station(BunkerStationType.StartRun); target = Read<Transform>(exit,"runTransitionTarget");
        context.RunStarter.StartSurfaceRun(target); yield return ReadyRun();
        run.EndRun(RunEndReason.Victory,run.RunId); Assert.That(service.GetStatus("C2"), Is.EqualTo(SurfaceSectorStatus.Locked));
        RunEndService.RecoverToBunker(); yield return Bunker();

        // Skip route travel only: the existing final boss flow still spawns and confirms a real boss death.
        context = BunkerContext.Instance; exit = Station(BunkerStationType.StartRun); target = Read<Transform>(exit,"runTransitionTarget");
        context.RunStarter.StartSurfaceRun(target); yield return ReadyRun();
        var finalProfile = AssetDatabase.LoadAssetAtPath<StageProfileData>("Assets/_Project/Data/Stages/StageProfiles/StageProfile_10.asset");
        run.SetCurrentSector(new RunSector(RunRoute.FinalSector,finalProfile,config.WorldRule,config.LocalAnomaly));
        var flow = RunFlowController.Instance; flow.InitializeSector(finalProfile);
        typeof(RunFlowController).GetProperty("IsExitUnlocked").SetValue(flow,true);
        Assert.That(flow.HandleExitReached(), Is.True);
        yield return CoreTestSupport.Await(() => flow.FinalBoss != null && flow.Phase == RunPhase.FinalBossCombat);
        flow.FinalBoss.TakeDamage(flow.FinalBoss.CurrentHealth * 2f,flow.FinalBoss.transform.position);
        yield return Bunker();
        Assert.That(service.GetStatus("C1"), Is.EqualTo(SurfaceSectorStatus.Completed));
        Assert.That(service.GetStatus("C2"), Is.EqualTo(SurfaceSectorStatus.Available));
        Assert.That(run.CurrentConfig, Is.SameAs(RunConfig.Default));
        Assert.That(Object.FindObjectsByType<MetaProgressionManager>(FindObjectsSortMode.None).Length, Is.EqualTo(1));
    }
    private static IEnumerator ReadyRun()
    {
        yield return CoreTestSupport.Await(() => SceneManager.GetActiveScene().name == "MVP" && !SceneTransitionOverlay.IsTransitioning && Object.FindFirstObjectByType<OrbitalStationRuntime>() is { IsInitialized: true });
        var spawner = Object.FindFirstObjectByType<CharacterSpawner>();
        Assert.That(spawner, Is.Not.Null); Assert.That(spawner.SpawnedPlayer, Is.Not.Null);
        var health = spawner.SpawnedPlayer.GetComponent<PlayerHealth>(); Assert.That(health, Is.Not.Null);
        health.AddMaxHealth(1000000f);
    }
    private static IEnumerator Bunker() => CoreTestSupport.Await(() => SceneManager.GetActiveScene().name == RunEndService.BunkerSceneName && !SceneTransitionOverlay.IsTransitioning);
}
#endif
