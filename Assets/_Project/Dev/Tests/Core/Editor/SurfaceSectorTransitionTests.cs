#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public sealed class SurfaceSectorTransitionTests
{
    [UnityTearDown]
    public IEnumerator Cleanup()
    {
        if (Application.isPlaying) yield return new ExitPlayMode();
        PlayerPrefs.EndSandbox();
    }

    [UnityTest, Timeout(150000)]
    public IEnumerator ProductionRouteReachesExistingBossAfterFiveLevels()
    {
        BunkerDebugTools.SetGameView1600();
        PlayerPrefs.BeginSandbox(false);
        PlayerPrefs.SetInt(BunkerIntroController.ViewedPreferenceKey, 1);
        PlayerPrefs.SetInt(TutorialController.CompletionKey, 1);
        EditorSceneManager.OpenScene("Assets/_Project/Scenes/MainBuild/MainMenu.unity");
        yield return new EnterPlayMode();
        yield return ExerciseRoute();
    }

    private static IEnumerator ExerciseRoute()
    {
        yield return CoreTestSupport.Await(() => ProductionSceneComposition.Active is { IsReady: true });
        var starter = Object.FindFirstObjectByType<BunkerRunStarter>();
        starter.StartRun(starter.transform);
        yield return CoreTestSupport.Await(() => SceneManager.GetActiveScene().name == "MVP" &&
            ProductionSceneComposition.Active is { IsReady: true } && !SceneTransitionOverlay.IsTransitioning);
        var run = RunStateManager.Instance;
        var opening = Object.FindFirstObjectByType<LevelChoiceManager>(FindObjectsInactive.Include);
        Assert.That(opening, Is.Not.Null);
        yield return null;
        Assert.That(opening.IsChoosing, Is.True,
            "The first sector must be chosen on the route map before combat starts.");
        const string output = "Artifacts/GeneratedQA/SurfaceMap";
        Directory.CreateDirectory(output);
        ScreenCapture.CaptureScreenshot(output + "/route-start.png");
        yield return null;

        RouteNode previous = null;
        for (int sector = 1; sector <= 15; sector++)
        {
            var manager = Object.FindFirstObjectByType<LevelChoiceManager>();
            yield return CoreTestSupport.Await(() => manager.IsChoosing);
            var selectable = Object.FindObjectsByType<LevelChoiceCardView>(FindObjectsSortMode.None)
                .Where(view => view.IsAvailable).Select(view => view.Node).ToArray();
            var expected = (sector - 1) % 3 == 0 ? run.RouteGraph.Entries : previous.Next;
            Assert.That(selectable, Is.EquivalentTo(expected));
            if (sector == 1)
            {
                var view = Object.FindObjectsByType<LevelChoiceCardView>(FindObjectsSortMode.None)
                    .Single(card => card.Node == expected[0]);
                Click(view.GetComponent<Button>());
                Click(view.GetComponentInParent<LevelChoicePanelView>()
                    .transform.Find("ConfirmButton").GetComponent<Button>());
            }
            else Assert.That(manager.DebugSelectFirstRule(), Is.True);
            yield return CoreTestSupport.Await(() => !SceneTransitionOverlay.IsTransitioning &&
                RunFlowController.Instance is { Phase: RunPhase.NormalSector } &&
                run.CurrentSector.SectorNumber == sector);
            Assert.That(run.CurrentLevel, Is.EqualTo((sector - 1) / 3 + 1));
            previous = run.CurrentSector.RouteNode;
            Assert.That(previous, Is.SameAs(expected[0]));
            var weather = previous.WorldRule;
            Assert.That(WorldRuleController.Instance.ActiveRule,
                Is.SameAs(weather != null && weather.RuleType != WorldRuleType.None ? weather : null));
            var flow = RunFlowController.Instance;
            Assert.That(flow.ExitMinimumTimeRemaining + flow.SectorElapsedTime, Is.EqualTo(60f).Within(.01f));
            var elapsed = typeof(RunFlowController).GetProperty(nameof(RunFlowController.SectorElapsedTime));
            elapsed.SetValue(flow, 59f);
            yield return null;
            Assert.That(flow.IsLevelCompleted, Is.False);
            elapsed.SetValue(flow, 60f);
            Assert.That(flow.FinalBoss, Is.Null);
            if (sector == 1)
            {
                yield return CoreTestSupport.Await(() => manager.IsChoosing);
                ScreenCapture.CaptureScreenshot(output + "/route-branch.png");
                yield return null;
            }
        }

        yield return CoreTestSupport.Await(() => RunFlowController.Instance.Phase == RunPhase.FinalBossCombat);
        var boss = RunFlowController.Instance.FinalBoss;
        Assert.That(boss != null && boss.IsBoss && !boss.IsDead, Is.True);
        boss.TakeDamage(boss.CurrentHealth * 2f, boss.transform.position);
        yield return CoreTestSupport.Await(() => run.IsRunEnded &&
            SceneManager.GetActiveScene().name == RunEndService.BunkerSceneName &&
            !SceneTransitionOverlay.IsTransitioning);
    }

    private static void Click(Button button)
    {
        Canvas.ForceUpdateCanvases();
        var pointer = new PointerEventData(EventSystem.current)
        {
            position = RectTransformUtility.WorldToScreenPoint(null, button.transform.position),
            button = PointerEventData.InputButton.Left
        };
        var hits = new List<RaycastResult>();
        EventSystem.current.RaycastAll(pointer, hits);
        Assert.That(hits.Count, Is.GreaterThan(0));
        Assert.That(ExecuteEvents.ExecuteHierarchy(hits[0].gameObject, pointer, ExecuteEvents.pointerClickHandler),
            Is.SameAs(button.gameObject));
    }
}
#endif
