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
public sealed class SurfaceMarkerFlowTests
{
    [SetUp] public void Preserve() => CoreTestSupport.PreservePreferences();
    [UnityTearDown] public IEnumerator Cleanup() => CoreTestSupport.CleanupPlayMode();
    static T Read<T>(Object obj,string field) => (T)obj.GetType().GetField(field,BindingFlags.NonPublic|BindingFlags.Instance).GetValue(obj);
    [UnityTest] public IEnumerator MarkerToRealExplorationEventToVictoryAndStorage()
    {
        EditorSceneManager.OpenScene("Assets/_Project/Scenes/MainBuild/StartScreen.unity");
        yield return new EnterPlayMode(); yield return Exercise();
    }
    static IEnumerator Exercise()
    {
        Object.FindFirstObjectByType<StartScreenController>().Begin();
        yield return CoreTestSupport.Await(() => SceneManager.GetActiveScene().name == RunEndService.BunkerSceneName && !SceneTransitionOverlay.IsTransitioning);
        var storage = new SurfaceMarkerTests.Storage();
        var map = AssetDatabase.LoadAssetAtPath<SurfaceMapDefinition>(SurfaceMapProductionAuthoring.MapPath);
        var service = new SurfaceMapService(map,storage);
        typeof(MetaProgressionManager).GetProperty("SurfaceMap").SetValue(MetaProgressionManager.Instance,service);
        var station = Object.FindObjectsByType<BunkerStation>(FindObjectsSortMode.None).First(s => Read<BunkerStationType>(s,"stationType") == BunkerStationType.StartRun);
        station.Interact(); var view = Object.FindFirstObjectByType<SurfaceMapView>(); service.TrySelect("D1"); yield return null;
        Assert.That(Read<TMPro.TMP_Text>(view,"details").text,Is.EqualTo("UNKNOWN ACTIVITY\nSignal detected"));
        Assert.That(Object.FindFirstObjectByType<SurfaceMapNavigation>(),Is.Not.Null);
        System.IO.Directory.CreateDirectory("Artifacts/GeneratedQA/SurfaceMap");
        ScreenCapture.CaptureScreenshot("Artifacts/GeneratedQA/SurfaceMap/marker-map.png"); yield return null;
        Read<Button>(view,"startButton").onClick.Invoke();
        yield return CoreTestSupport.Await(() => SceneManager.GetActiveScene().name == "MVP" && !SceneTransitionOverlay.IsTransitioning && Object.FindFirstObjectByType<OrbitalStationRuntime>() is { IsInitialized:true });
        var run = RunStateManager.Instance; var config = run.CurrentConfig;
        var player = Object.FindFirstObjectByType<CharacterSpawner>().SpawnedPlayer; player.GetComponent<PlayerHealth>().AddMaxHealth(1000000);
        // Verify the actual site placement, source prefab identity and production completion callback.
        var required = config.Content.GuaranteedEvents.Single();
        var site = ProductionAnomalySite.ActiveSites.Where(s => !s.IsSpecial && Read<WorldEvent>(s,"eventPrefab") == required)
            .OrderByDescending(s => s.SiteSize.x * s.SiteSize.y).First();
        var worldEvent = (FalseSignalEvent)Read<WorldEvent>(site,"activeEvent");
        Assert.That(worldEvent.SourcePrefab, Is.SameAs(required));
        player.transform.position = worldEvent.transform.position; worldEvent.Interact();
        yield return CoreTestSupport.Await(() => worldEvent.IsStarted);
        var realSignal = Object.FindObjectsByType<FalseSignalPoint>(FindObjectsSortMode.None).First(p => Read<bool>(p,"isReal"));
        worldEvent.ResolveSignal(realSignal,true); Assert.That(worldEvent.IsCompleted,Is.True);
        Assert.That(storage.values.Count,Is.Zero); service.Content.TryGetMarker("D1",out var marker); Assert.That(marker.State,Is.EqualTo(SurfaceMarkerState.Active));
        // The real site reward must be selected before the final boss can start.
        yield return CoreTestSupport.Await(() => UpgradeManager.Instance.DebugCurrentChoices.Count > 0);
        Assert.That(UpgradeManager.Instance.DebugSelectCurrentChoice(0), Is.True);
        yield return CoreTestSupport.Await(() => UpgradeManager.Instance.IsRewardQueueIdle);
        var profile = AssetDatabase.LoadAssetAtPath<StageProfileData>("Assets/_Project/Data/Stages/StageProfiles/StageProfile_10.asset");
        run.SetCurrentSector(new RunSector(RunRoute.FinalSector,profile,config.WorldRule,config.LocalAnomaly));
        var flow = RunFlowController.Instance; flow.InitializeSector(profile); typeof(RunFlowController).GetProperty("IsExitUnlocked").SetValue(flow,true);
        Assert.That(flow.HandleExitReached(),Is.True);
        yield return CoreTestSupport.Await(() => flow.FinalBoss != null && flow.Phase == RunPhase.FinalBossCombat);
        flow.FinalBoss.TakeDamage(flow.FinalBoss.CurrentHealth*2,flow.FinalBoss.transform.position);
        yield return CoreTestSupport.Await(() => SceneManager.GetActiveScene().name == RunEndService.BunkerSceneName && !SceneTransitionOverlay.IsTransitioning);
        var reloaded = new SurfaceMapService(map,storage); reloaded.Content.TryGetMarker("D1",out marker);
        Assert.That(marker.State,Is.EqualTo(SurfaceMarkerState.Revealed)); Assert.That(reloaded.GetStatus("D1"),Is.EqualTo(SurfaceSectorStatus.Completed));
    }
}
#endif
