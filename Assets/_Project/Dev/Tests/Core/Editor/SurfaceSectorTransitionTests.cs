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
public sealed class SurfaceSectorTransitionTests
{
    [SetUp] public void Preserve() => CoreTestSupport.PreservePreferences();
    [UnityTearDown] public IEnumerator Cleanup() => CoreTestSupport.CleanupPlayMode();
    static T Read<T>(Object o,string field) => (T)o.GetType().GetField(field,BindingFlags.NonPublic|BindingFlags.Instance).GetValue(o);
    [UnityTest] public IEnumerator InitialRuleThenRandomChoiceAndImmediateTimedExit()
    {
        EditorSceneManager.OpenScene("Assets/_Project/Scenes/MainBuild/StartScreen.unity");
        yield return new EnterPlayMode(); yield return Exercise();
    }
    static IEnumerator Exercise()
    {
        Object.FindFirstObjectByType<StartScreenController>().Begin();
        yield return CoreTestSupport.Await(() => SceneManager.GetActiveScene().name == RunEndService.BunkerSceneName && !SceneTransitionOverlay.IsTransitioning);
        var map = MetaProgressionManager.Instance.SurfaceMap; Assert.That(map.TrySelect("C1"),Is.True);
        var starter = Object.FindFirstObjectByType<BunkerRunStarter>(); starter.StartSurfaceRun(starter.transform);
        yield return CoreTestSupport.Await(() => SceneManager.GetActiveScene().name == "MVP" && !SceneTransitionOverlay.IsTransitioning && Object.FindFirstObjectByType<OrbitalStationRuntime>() is { IsInitialized:true });
        var run = RunStateManager.Instance; var initial = run.CurrentConfig.WorldRule;
        Assert.That(run.CurrentSector.WorldRule,Is.SameAs(initial));
        var player = Object.FindFirstObjectByType<CharacterSpawner>().SpawnedPlayer; player.GetComponent<PlayerHealth>().AddMaxHealth(1000000);
        var spawner = Object.FindFirstObjectByType<EnemySpawner>(); var flow = RunFlowController.Instance;
        float target = Read<float>(spawner,"assaultNextAt"), deadline = run.CurrentSector.StageProfile.ExitActivationTime;
        Assert.That(deadline-target,Is.InRange(10f,15f),"Automatic formation must target the pre-exit window");
        // Reproduce the old blocking state while skipping idle timer wait.
        var progress = typeof(EnemySpawner).GetField("firstAutomaticAssault",BindingFlags.Instance|BindingFlags.NonPublic);
        progress.SetValue(spawner,System.Enum.Parse(progress.FieldType,"Recovering"));
        typeof(EnemySpawner).GetField("recoveryRemaining",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(spawner,30f);
        typeof(RunFlowController).GetProperty("SectorElapsedTime").SetValue(flow,deadline);
        yield return null;
        Assert.That(flow.IsExitUnlocked,Is.True,"Timer end must unlock without waiting for assault recovery");
        var exit = ProductionSectorExit.ActiveExits.First(); Assert.That(exit.IsAvailable,Is.True);
        player.transform.position = exit.transform.position; Physics2D.SyncTransforms();
        var manager = Object.FindFirstObjectByType<LevelChoiceManager>();
        yield return CoreTestSupport.Await(() => manager.IsChoosing);
        var panel = Object.FindFirstObjectByType<LevelChoicePanelView>();
        var cards = Read<LevelChoiceCardView[]>(panel,"cardViews").Where(c => c.gameObject.activeInHierarchy).ToArray();
        Assert.That(cards.Length,Is.EqualTo(3)); Assert.That(cards.Select(c=>c.Rule).Distinct().Count(),Is.EqualTo(3));
        var selected = cards.First(c => c.Rule != initial); var nextRule = selected.Rule;
        System.IO.Directory.CreateDirectory("Artifacts/GeneratedQA/SurfaceMap");
        ScreenCapture.CaptureScreenshot("Artifacts/GeneratedQA/SurfaceMap/random-sector-choice.png"); yield return null;
        Read<Button>(selected,"button").onClick.Invoke(); var confirm = Read<Button>(panel,"confirmButton"); Assert.That(confirm.interactable,Is.True); confirm.onClick.Invoke();
        yield return CoreTestSupport.Await(() => !SceneTransitionOverlay.IsTransitioning && run.CurrentSector.SectorNumber == 2 && Object.FindFirstObjectByType<OrbitalStationRuntime>() is { IsInitialized:true });
        Assert.That(run.CurrentSector.WorldRule,Is.SameAs(nextRule)); Assert.That(run.CurrentSector.WorldRule,Is.Not.SameAs(initial));
        Assert.That(run.CurrentConfig.WorldRule,Is.SameAs(initial),"Initial config remains the run snapshot");
    }
}
#endif
