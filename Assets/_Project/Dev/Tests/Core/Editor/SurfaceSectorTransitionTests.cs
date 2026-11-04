#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using TMPro;
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
        PrepareProductionBunker();
        yield return new EnterPlayMode();
        yield return ExerciseRoute();
    }

    [UnityTest, Timeout(70000)]
    public IEnumerator ProductionMapShowsFiveWastelandsAtSectorBoundaries()
    {
        PrepareProductionBunker();
        yield return new EnterPlayMode();
        yield return ExerciseAtlas();
    }

    private static void PrepareProductionBunker()
    {
        BunkerDebugTools.SetGameView1600();
        PlayerPrefs.BeginSandbox(true);
        PlayerPrefs.SetInt(BunkerIntroController.ViewedPreferenceKey, 1);
        PlayerPrefs.SetInt(TutorialController.CompletionKey, 1);
        EditorSceneManager.OpenScene("Assets/_Project/Scenes/MainBuild/MainMenu.unity");
    }

    private static void OpenRoute()
    {
        LocalizationService.Instance.SetLanguage(GameLanguage.Russian);
        var starter = Object.FindFirstObjectByType<BunkerRunStarter>();
        Object.FindFirstObjectByType<BunkerPanelManager>().OpenDepthSelect(starter.transform);
    }

    private static IEnumerator ExerciseAtlas() => ExerciseRoute(false);

    private static IEnumerator ExerciseRoute(bool full = true)
    {
        yield return CoreTestSupport.Await(() => ProductionSceneComposition.Active is { IsReady: true });
        var map = Object.FindFirstObjectByType<BunkerRunStarter>().SurfaceMap;
        if (!full)
        {
            Assert.That(map.TrySelect("C1"), Is.True);
            Assert.That(map.TryBuildRunConfig(out var priorClear), Is.True);
            Assert.That(map.CompleteVictory(priorClear, RunEndReason.Victory, true), Is.True);
        }

        OpenRoute();
        yield return null;
        AssertMapLevel(1);
        Assert.That(GameObject.Find("Boss icon"), Is.Not.Null);
        for (int level = 2; level <= RunRoute.LevelCount; level++)
        {
            var future = GameObject.Find($"Route location {level}-1");
            Assert.That(future, Is.Not.Null, $"Location level {level} must be visible before the run.");
            Assert.That(future.GetComponent<Button>(), Is.Null);
            Assert.That(future.GetComponentInChildren<TMP_Text>().text, Is.Not.EqualTo("·"));
        }
        Assert.That(AvailableButtons().Count(), Is.EqualTo(3));
        Assert.That(GameObject.Find("Route location 6-1"), Is.Null);

        if (!full)
        {
            var navigation = Object.FindFirstObjectByType<SurfaceMapNavigation>();
            Vector2 original = navigation.Content.anchoredPosition;
            var pointer = new PointerEventData(EventSystem.current) { scrollDelta = Vector2.down };
            ExecuteEvents.Execute(navigation.gameObject, pointer, ExecuteEvents.scrollHandler);
            Assert.That(navigation.Content.anchoredPosition.x, Is.LessThan(original.x));
            navigation.Pan(original - navigation.Content.anchoredPosition);
            var future = GameObject.Find("Route location 2-1");
            ExecuteEvents.Execute(future, pointer, ExecuteEvents.pointerEnterHandler);
            Assert.That(GameObject.Find("Node intel").GetComponent<TMP_Text>().text, Does.Contain("Погода"));
            Assert.That(GameObject.Find("Route confirm"), Is.Null, "Future nodes can be inspected without entering them.");
            Assert.That(GameObject.Find("Route briefing").GetComponent<SurfaceMapPlate>().raycastTarget, Is.False,
                "The preview must not intercept its own hover when the map is panned.");
            ExecuteEvents.Execute(future, pointer, ExecuteEvents.pointerExitHandler);
        }

        Click(GameObject.Find("Route location 1-2").GetComponent<Button>());
        Assert.That(GameObject.Find("Route selection glow"), Is.Not.Null);
        if (!full)
        {
            const string output = "Artifacts/GeneratedQA/SurfaceMap";
            Directory.CreateDirectory(output);
            ScreenCapture.CaptureScreenshot(output + "/production-route-five-locations.png");
            yield return new WaitForSecondsRealtime(.25f);
        }
        else yield return null;

        Click(GameObject.Find("Route confirm").GetComponent<Button>());
        yield return null;
        var picker = Object.FindFirstObjectByType<ExpeditionTaskPicker>();
        Click(picker.transform.Find("TaskPanel/Launch").GetComponent<Button>());
        yield return CoreTestSupport.Await(() => SceneManager.GetActiveScene().name == "MVP" &&
            ProductionSceneComposition.Active is { IsReady: true } && !SceneTransitionOverlay.IsTransitioning);
        var run = RunStateManager.Instance;
        var graph = run.RouteGraph;
        var location = graph.Entries[1];
        Assert.That(run.CurrentConfig.SectorId, Is.EqualTo(full ? "C1" : "C2"));

        for (int level = 1; level <= RunRoute.LevelCount; level++)
        {
            Assert.That(run.RouteGraph, Is.SameAs(graph), "The visible graph stays fixed throughout the run.");
            Assert.That(run.CurrentSector.RouteNode, Is.SameAs(location));
            int lastSector = level * RunRoute.SectorsPerLevel;
            if (full || level == 1)
            {
                for (int sector = location.SectorNumber; sector < lastSector; sector++)
                {
                    AssertBattleSector(run, location, sector);
                    FinishSector();
                    int nextSector = sector + 1;
                    yield return CoreTestSupport.Await(() => IsPlayingSector(run, nextSector));
                    Assert.That(run.CompletedLevels, Is.EqualTo(level - 1));
                }
            }
            else
            {
                // Skip the intervening combat in this smoke while exercising the real level exits.
                var first = run.CurrentSector;
                run.SetCurrentSector(new RunSector(lastSector, first.StageProfile, location.WorldRule, first.LocalAnomaly, location));
                RunFlowController.Instance.InitializeSector(first.StageProfile);
            }

            AssertBattleSector(run, location, lastSector);
            FinishSector();
            if (level == RunRoute.LevelCount) break;

            var manager = Object.FindFirstObjectByType<LevelChoiceManager>();
            yield return CoreTestSupport.Await(() => manager.IsChoosing);
            Assert.That(run.CompletedLevels, Is.EqualTo(level));
            AssertMapLevel(level + 1);
            var buttons = AvailableButtons().ToArray();
            Assert.That(buttons.Select(button => button.name), Is.EquivalentTo(location.Next.Select(NodeName)));
            Assert.That(GameObject.Find(NodeName(location)).GetComponent<Button>(), Is.Null);
            location = location.Next[0];
            Click(buttons.Single(button => button.name == NodeName(location)));
            yield return null;
            Click(GameObject.Find("Route confirm").GetComponent<Button>());
            yield return CoreTestSupport.Await(() => IsPlayingSector(run, lastSector + 1));
        }

        yield return CoreTestSupport.Await(() => RunFlowController.Instance.Phase == RunPhase.FinalBossCombat);
        Assert.That(run.CompletedLevels, Is.EqualTo(RunRoute.LevelCount));
        Assert.That(run.CurrentSector.SectorNumber, Is.EqualTo(RunRoute.FinalSector));
        Assert.That(AvailableButtons(), Is.Empty);
        var boss = RunFlowController.Instance.FinalBoss;
        Assert.That(boss != null && boss.IsBoss && !boss.IsDead, Is.True);
        if (!full) yield break;

        boss.TakeDamage(boss.CurrentHealth * 2f, boss.transform.position);
        yield return CoreTestSupport.Await(() => run.IsRunEnded &&
            SceneManager.GetActiveScene().name == RunEndService.BunkerSceneName && !SceneTransitionOverlay.IsTransitioning);
        Assert.That(Object.FindFirstObjectByType<BunkerRunStarter>().SurfaceMap.GetStatus("C2"),
            Is.EqualTo(SurfaceSectorStatus.Available));
    }

    private static void AssertMapLevel(int level)
    {
        var header = GameObject.Find("Route stage name").GetComponent<TMP_Text>();
        Assert.That(header.text, Is.EqualTo($"Пустошь {level}/5"));
        Assert.That(Object.FindObjectsByType<TMP_Text>(FindObjectsSortMode.None)
            .Where(label => label.text?.StartsWith("Пустошь ") == true), Is.EqualTo(new[] { header }));
    }

    private static void AssertBattleSector(RunStateManager run, RouteNode location, int sector)
    {
        Assert.That(run.CurrentSector.SectorNumber, Is.EqualTo(sector));
        Assert.That(run.CurrentSector.RouteNode, Is.SameAs(location));
        Assert.That(run.CurrentSector.Duration, Is.EqualTo(60f));
        Assert.That(GameObject.Find("Route stage name"), Is.Null, "The route map only opens between locations.");
        var hud = Object.FindFirstObjectByType<RunRouteProgressView>();
        Assert.That(hud.transform.Find("Sector").GetComponent<TMP_Text>().text, Is.EqualTo($"{location.Level}/5"));
        var rule = location.WorldRule;
        Assert.That(WorldRuleController.Instance.ActiveRule,
            Is.SameAs(rule != null && rule.RuleType != WorldRuleType.None ? rule : null));
    }

    private static void FinishSector() => typeof(RunFlowController).GetProperty(nameof(RunFlowController.SectorElapsedTime))
        .SetValue(RunFlowController.Instance, RunRoute.SectorDuration);

    private static bool IsPlayingSector(RunStateManager run, int sector) =>
        !SceneTransitionOverlay.IsTransitioning && run.CurrentSector.SectorNumber == sector &&
        RunFlowController.Instance is { Phase: RunPhase.NormalSector };

    private static IEnumerable<Button> AvailableButtons() => Object.FindObjectsByType<Button>(FindObjectsSortMode.None)
        .Where(button => button.name.StartsWith("Route location "));

    private static string NodeName(RouteNode node) => $"Route location {node.Level}-{node.Row + 1}";

    private static void Click(Button button)
    {
        Canvas.ForceUpdateCanvases();
        Camera camera = button.GetComponentInParent<GraphicRaycaster>()?.eventCamera;
        var pointer = new PointerEventData(EventSystem.current)
        {
            position = RectTransformUtility.WorldToScreenPoint(camera, button.transform.position),
            button = PointerEventData.InputButton.Left
        };
        var hits = new List<RaycastResult>();
        EventSystem.current.RaycastAll(pointer, hits);
        Assert.That(hits.Count, Is.GreaterThan(0), $"{button.name} at {pointer.position}, Game View {Screen.width}×{Screen.height}");
        var handler = ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject);
        Assert.That(handler, Is.SameAs(button.gameObject),
            $"{button.name} at {pointer.position} hit {string.Join(", ", hits.Select(hit => hit.gameObject.name))}");
        ExecuteEvents.Execute(handler, pointer, ExecuteEvents.pointerClickHandler);
    }
}
#endif
