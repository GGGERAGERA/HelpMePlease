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
    private const System.Reflection.BindingFlags InstanceMembers =
        System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;

    [UnityTearDown]
    public IEnumerator Cleanup()
    {
        if (Application.isPlaying) yield return new ExitPlayMode();
        PlayerPrefs.EndSandbox();
        GameplayRandom.End();
    }

    [UnityTest, Timeout(150000)]
    public IEnumerator ProductionRouteReachesExistingBossAfterFiveLevels()
    {
        PrepareProductionBunker();
        yield return new EnterPlayMode();
        yield return ExerciseRoute();
    }

    [UnityTest, Timeout(70000)]
    public IEnumerator ProductionMapRevealsOnlyNextSector()
    {
        PrepareProductionBunker();
        yield return new EnterPlayMode();
        yield return ExerciseRoute(false);
    }

    [UnityTest, Timeout(50000)]
    public IEnumerator DebugMenuCompletesSectorBeforeDeadline()
    {
        PrepareProductionBunker();
        yield return new EnterPlayMode();
        yield return ExerciseDebugMenuCompletion();
    }

    private static IEnumerator ExerciseDebugMenuCompletion()
    {
        yield return CoreTestSupport.Await(() => ProductionSceneComposition.Active is { IsReady: true });
        var starter = Object.FindFirstObjectByType<BunkerRunStarter>();
        starter.StartRun(starter.transform);
        yield return CoreTestSupport.Await(() => SceneManager.GetActiveScene().name == "MVP" &&
            ProductionSceneComposition.Active is { IsReady: true } && !SceneTransitionOverlay.IsTransitioning);
        yield return CoreTestSupport.SelectInitialRouteNode();

        var flow = RunFlowController.Instance;
        var run = RunStateManager.Instance;
        var choice = Object.FindFirstObjectByType<LevelChoiceManager>();
        var menu = Object.FindFirstObjectByType<Subject42DebugMenu>();
        Assert.That(flow.IsExitUnlocked, Is.False);
        Assert.That(run.IsDevelopmentRun, Is.False);
        int exits = 0;
        flow.ExitReached += () => exits++;

        const System.Reflection.BindingFlags privateInstance = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
        var menuType = typeof(Subject42DebugMenu);
        var worldTab = System.Enum.Parse(menuType.GetNestedType("DebugTab", System.Reflection.BindingFlags.NonPublic), "WorldEvents");
        menuType.GetMethod("SelectTab", privateInstance).Invoke(menu, new[] { worldTab, (object)true });
        menuType.GetMethod("OpenMenu", privateInstance).Invoke(menu, null);
        var root = (Transform)menuType.GetField("sectionParent", privateInstance).GetValue(menu);
        var row = root.GetComponentsInChildren<RectTransform>().Single(item => item.name == "Завершить текущий сектор");
        var complete = row.GetComponentInChildren<Button>();
        Assert.That(complete.interactable, Is.True, "F1 must offer early completion while the exit is locked by the timer.");
        Assert.That(root.GetComponentsInChildren<TMP_Text>().Any(label =>
            label.text.Contains("Зачистка огневой группы") || label.text.Contains("Сопровождение груза")), Is.False);

        complete.onClick.Invoke();
        Assert.That(flow.IsLevelCompleted, Is.True);
        Assert.That(flow.ExitMinimumTimeRemaining, Is.Zero);
        Assert.That(run.IsDevelopmentRun, Is.True);
        Assert.That(choice.IsChoosing, Is.True);
        Assert.That(exits, Is.EqualTo(1));
        Assert.That(flow.TryDebugCompleteCurrentLevel(), Is.False);
        Assert.That(exits, Is.EqualTo(1));
        Assert.That(choice.DebugSelectFirstRule(), Is.True);
        yield return CoreTestSupport.Await(() => run.CurrentSector.SectorNumber == 2 && !SceneTransitionOverlay.IsTransitioning);
        Assert.That(RunFlowController.Instance.IsLevelCompleted, Is.False);
    }

    [Test]
    public void SeededGraphHasFiveReachableColumnsAndDistinctForks()
    {
        Assert.That(RunRoute.LevelCount, Is.EqualTo(5), "The demo has five combat levels.");
        Assert.That(RunRoute.IsFinalSector(5), Is.True);
        Assert.That(RunRoute.HasNextSector(5), Is.False);
        const System.Reflection.BindingFlags instance = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
        const System.Reflection.BindingFlags staticMethod = System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic;
        var source = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/prefabs/Bootstrap/GameplayRun.prefab")
            .GetComponentInChildren<LevelChoiceManager>(true);
        var rules = (WorldRuleData[])typeof(LevelChoiceManager).GetField("availableWorldRules", instance).GetValue(source);
        var generate = typeof(RunRouteGraph).GetMethod("Generate", staticMethod);
        RunRouteGraph Generate(int seed) => (RunRouteGraph)generate.Invoke(null,
            new object[] { seed, rules, null, false, null });
        string Fingerprint(RunRouteGraph graph) => string.Join("|",
            Enumerable.Range(1, RunRoute.LevelCount).SelectMany(graph.NodesAt).Select(node =>
                $"{node.SectorNumber}:{node.Row}:{node.Difficulty}:{node.DurationSeconds}:{node.WorldRule?.RuleType}:{node.RewardProfile}:{string.Join(",", node.Next.Select(next => next.Row))}"));

        Assert.That(Fingerprint(Generate(734)), Is.EqualTo(Fingerprint(Generate(734))));
        Assert.That(Fingerprint(Generate(734)), Is.Not.EqualTo(Fingerprint(Generate(735))));
        var goldenDifficulties = new HashSet<RouteDifficulty>();
        for (int seed = 0; seed < 24; seed++)
        {
            var graph = Generate(seed);
            Assert.That(graph.Seed, Is.EqualTo(seed));
            IReadOnlyList<RouteNode> reachable = graph.Entries;
            for (int sector = 1; sector <= RunRoute.LevelCount; sector++)
            {
                var nodes = graph.NodesAt(sector);
                Assert.That(nodes, Is.EquivalentTo(reachable), $"Seed {seed}, sector {sector}");
                Assert.That(nodes.Count, Is.InRange(1, 3));
                Assert.That(nodes.Select(node => node.DurationSeconds), Is.EquivalentTo(new[] { 30, 60, 90 }));
                Assert.That(nodes.Select(node => node.Difficulty), Is.EquivalentTo(new[] { RouteDifficulty.Easy, RouteDifficulty.Medium, RouteDifficulty.Hard }));
                Assert.That(nodes.Select(node => node.RewardProfile).Distinct(), Is.EquivalentTo(new[] { RewardProfile.WeaponLevel, RewardProfile.ModuleLevel }));
                foreach (var node in nodes)
                {
                    Assert.That(node.SectorNumber, Is.EqualTo(sector));
                    if (node.WorldRule?.RuleType == WorldRuleType.Golden) goldenDifficulties.Add(node.Difficulty);
                    if (sector == RunRoute.FinalSector) Assert.That(node.Next, Is.Empty);
                    else
                    {
                        Assert.That(node.Next, Is.Not.Empty);
                        Assert.That(node.Next.All(next => next.SectorNumber == sector + 1), Is.True);
                    }
                }
                reachable = nodes.SelectMany(node => node.Next).Distinct().ToArray();
            }
        }
        Assert.That(goldenDifficulties.Count, Is.GreaterThan(1), "World Rule is independent of difficulty.");
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

    private static IEnumerator ExerciseRoute(bool full = true)
    {
        yield return CoreTestSupport.Await(() => ProductionSceneComposition.Active is { IsReady: true });
        OpenRoute();
        yield return null;
        AssertMapLevel(1);
        var starter = Object.FindFirstObjectByType<BunkerRunStarter>();
        var entryGraph = (RunRouteGraph)typeof(BunkerRunStarter).GetField("surfaceGraph", InstanceMembers).GetValue(starter);
        Assert.That(GameObject.Find("Boss icon"), Is.Not.Null, "The final landmark exists at entry.");
        Assert.That(GameObject.Find("Route location 2-1"), Is.Not.Null, "The next column is visible for planning.");
        Assert.That(GameObject.Find("Route location 5-1"), Is.Not.Null, "Fog must retain the final combat column.");
        foreach (var name in new[] { "Route location 1-2", "Route location 2-1", "Route location 5-1" })
        {
            var swords = GameObject.Find(name).GetComponentInChildren<SurfaceMapGlyph>();
            Assert.That(swords, Is.Not.Null, $"{name} shows combat swords.");
            Assert.That(swords.raycastTarget, Is.False);
        }
        Assert.That(GameObject.Find("Route location 6-1"), Is.Null, "The boss follows the fifth combat column.");
        var roads = GameObject.Find("Roads").GetComponent<Graphic>();
        var tracks = (IList)roads.GetType().GetField("tracks", InstanceMembers).GetValue(roads);
        int expectedRoads = entryGraph.Entries.Count + entryGraph.NodesAt(RunRoute.FinalSector).Count +
            Enumerable.Range(1, RunRoute.TotalSectors).SelectMany(entryGraph.NodesAt).Sum(node => node.Next.Count);
        Assert.That(tracks.Count, Is.EqualTo(expectedRoads));
        Assert.That(PreviewFeedback(4), Is.Not.Null);
        Assert.That(PreviewFeedback(5), Is.Null);
        Assert.That(GameObject.Find("Type mark"), Is.Null);
        Assert.That(GameObject.Find("Route pressure").GetComponent<TMP_Text>().GetParsedText(), Is.EqualTo("Давление I / IV"));
        Assert.That(AvailableButtons().Count(), Is.EqualTo(3));
        Assert.That(GameObject.Find("Route selection outline"), Is.Null, "Opening the map does not choose a node.");
        var preview = GameObject.Find("Route location 2-1");
        Assert.That(preview.GetComponent<Button>(), Is.Null);
        var pointer = new PointerEventData(EventSystem.current);
        ExecuteEvents.Execute(preview, pointer, ExecuteEvents.pointerEnterHandler);
        Assert.That(GameObject.Find("Node intel").GetComponent<TMP_Text>().text, Does.StartWith("Правило"));
        Assert.That(GameObject.Find("Route confirm"), Is.Null, "Preview has no launch action.");
        ExecuteEvents.Execute(preview, pointer, ExecuteEvents.pointerExitHandler);
        Click(GameObject.Find("Route location 1-2").GetComponent<Button>());
        Assert.That(GameObject.Find("Route selection outline"), Is.Not.Null);
        var intel = GameObject.Find("Node intel").GetComponent<TMP_Text>();
        intel.ForceMeshUpdate();
        string briefing = intel.GetParsedText();
        Assert.That(briefing.Split('\n').Select(line => line.Split('·')[0].Trim()),
            Is.EqualTo(new[] { "Правило", "Время прохождения", "Награда", "Сложность" }));
        Assert.That(briefing, Does.Contain($"{entryGraph.Entries[1].DurationSeconds} с"));
        var branches = (ICollection<int>)roads.GetType().GetField("plannedTracks", InstanceMembers).GetValue(roads);
        Assert.That(branches.Count, Is.EqualTo(entryGraph.Entries[1].Next.Count));

        if (!full)
        {
            const string output = "Artifacts/GeneratedQA/SurfaceMap";
            Directory.CreateDirectory(output);
            var navigation = Object.FindFirstObjectByType<SurfaceMapNavigation>();
            AssertInViewport(navigation, "Route location 4-1");
            yield return null;
            ScreenCapture.CaptureScreenshot(output + "/production-route-start.png");
            yield return new WaitForSecondsRealtime(.25f);
            Vector2 original = navigation.Content.anchoredPosition;
            Vector3 marker = GameObject.Find("Route character").transform.localPosition;
            var selection = GameObject.Find("Route selection outline");
            var scroll = new PointerEventData(EventSystem.current) { scrollDelta = Vector2.down * 40f };
            ExecuteEvents.Execute(navigation.gameObject, scroll, ExecuteEvents.scrollHandler);
            Assert.That(navigation.Content.anchoredPosition.x, Is.LessThan(original.x));
            Assert.That(GameObject.Find("Route character").transform.localPosition, Is.EqualTo(marker));
            Assert.That(GameObject.Find("Route selection outline"), Is.SameAs(selection));
            Assert.That(PreviewFeedback(5), Is.Null);
            AssertInViewport(navigation, "Route location 5-1");
            AssertInViewport(navigation, "Boss icon");
            yield return null;
            ScreenCapture.CaptureScreenshot(output + "/production-route-scrolled-right.png");
            yield return new WaitForSecondsRealtime(.25f);
            navigation.Pan(original - navigation.Content.anchoredPosition);
        }

        yield return null;
        Click(GameObject.Find("Route confirm").GetComponent<Button>());
        yield return null;
        var picker = Object.FindFirstObjectByType<ExpeditionTaskPicker>();
        Click(picker.transform.Find("TaskPanel/Launch").GetComponent<Button>());
        yield return CoreTestSupport.Await(() => SceneManager.GetActiveScene().name == "MVP" &&
            ProductionSceneComposition.Active is { IsReady: true } && !SceneTransitionOverlay.IsTransitioning);
        var run = RunStateManager.Instance;
        var graph = run.RouteGraph;
        var location = graph.Entries[1];
        Assert.That(run.CurrentConfig.SectorId, Is.EqualTo("C1"));
        var health = Object.FindFirstObjectByType<CharacterSpawner>().SpawnedPlayer.GetComponent<PlayerHealth>();
        health.DebugInvulnerable = true;
        run.SetThreatForDebug(52f);
        yield return null;
        Assert.That(GameObject.Find("Combat pressure").GetComponent<TMP_Text>().text, Is.EqualTo("Давление III / IV"));

        for (int sector = 1; sector <= (full ? RunRoute.FinalSector : 2); sector++)
        {
            Assert.That(run.RouteGraph, Is.SameAs(graph));
            AssertBattleSector(run, location, sector);
            if (!full && sector == 2)
            {
                // Skip the intervening travel only. Exercise the real deadline and boss spawner.
                var final = graph.NodesAt(RunRoute.FinalSector)[0];
                var profile = UnityEditor.AssetDatabase.LoadAssetAtPath<StageProfileData>(
                    "Assets/_Project/Data/Stages/StageProfiles/StageProfile_03.asset");
                run.SetCurrentSector(new RunSector(final.SectorNumber, profile, final.WorldRule, run.CurrentSector.LocalAnomaly, final));
                var flow = RunFlowController.Instance;
                flow.InitializeSector(profile);
                FinishSector();
                yield return CoreTestSupport.Await(() => flow.Phase == RunPhase.FinalBossCombat);
                Assert.That(flow.FinalBoss, Is.Not.Null);
                Assert.That(flow.FinalBoss.IsBoss, Is.True);
                Assert.That(AvailableButtons(), Is.Empty);
                yield break;
            }
            var build = run.OrbitalStationState.Telekinetic;
            bool weapon = location.RewardProfile == RewardProfile.WeaponLevel;
            int RewardRank() => build.Items.Concat(build.Reserve).Where(item => weapon ? item.IsWeapon : item.CanMerge)
                .Sum(item => item.Level - 1);
            int rankBefore = RewardRank();
            FinishSector();
            if (sector == RunRoute.FinalSector) break;

            var manager = Object.FindFirstObjectByType<LevelChoiceManager>();
            yield return CoreTestSupport.Await(() => manager.IsChoosing);
            Assert.That(run.CompletedLevels, Is.EqualTo(sector));
            Assert.That(RewardRank(), Is.EqualTo(rankBefore + 1));
            AssertMapLevel(sector + 1);
            var pressure = GameObject.Find("Route pressure").GetComponent<TMP_Text>();
            pressure.ForceMeshUpdate();
            Assert.That(pressure.GetParsedText(),
                Is.EqualTo($"Давление {ThreatTierPresentation.Format(ThreatTierPresentation.FromPressure(run.ThreatValue))}"));
            var buttons = AvailableButtons().ToArray();
            Assert.That(buttons.Select(button => button.name), Is.EquivalentTo(location.Next.Select(NodeName)));
            Assert.That(GameObject.Find(NodeName(location)).GetComponent<Button>(), Is.Null);
            if (sector + 4 <= RunRoute.TotalSectors)
                Assert.That(PreviewFeedback(sector + 4), Is.Not.Null, "The preview advances after completing a sector.");
            location = location.Next[0];
            Click(buttons.Single(button => button.name == NodeName(location)));
            yield return null;
            Click(GameObject.Find("Route confirm").GetComponent<Button>());
            yield return CoreTestSupport.Await(() => IsPlayingSector(run, sector + 1));
        }

        yield return CoreTestSupport.Await(() => RunFlowController.Instance.Phase == RunPhase.FinalBossCombat);
        Assert.That(run.CompletedLevels, Is.EqualTo(RunRoute.LevelCount));
        Assert.That(run.CurrentSector.SectorNumber, Is.EqualTo(RunRoute.FinalSector));
        Assert.That(AvailableButtons(), Is.Empty);
        var boss = RunFlowController.Instance.FinalBoss;
        Assert.That(boss != null && boss.IsBoss && !boss.IsDead, Is.True);
        boss.TakeDamage(boss.CurrentHealth * 2f, boss.transform.position);
        yield return CoreTestSupport.Await(() => run.IsRunEnded &&
            SceneManager.GetActiveScene().name == RunEndService.BunkerSceneName && !SceneTransitionOverlay.IsTransitioning);
        Assert.That(Object.FindFirstObjectByType<BunkerRunStarter>().SurfaceMap.GetStatus("C2"),
            Is.EqualTo(SurfaceSectorStatus.Available));
    }

    private static void AssertMapLevel(int level)
    {
        var header = GameObject.Find("Route stage name").GetComponent<TMP_Text>();
        Assert.That(header.text, Is.EqualTo($"Уровень {level}/5"));
        Assert.That(Object.FindObjectsByType<TMP_Text>(FindObjectsSortMode.None)
            .Where(label => label.name == "Route stage name"), Is.EqualTo(new[] { header }));
    }

    private static void AssertBattleSector(RunStateManager run, RouteNode location, int sector)
    {
        Assert.That(run.CurrentSector.SectorNumber, Is.EqualTo(sector));
        Assert.That(run.CurrentSector.RouteNode, Is.SameAs(location));
        Assert.That(run.CurrentSector.Duration, Is.EqualTo(location.DurationSeconds));
        Assert.That(GameObject.Find("Route stage name"), Is.Null, "Combat starts after confirming the selected sector.");
        var hud = Object.FindFirstObjectByType<RunRouteProgressView>();
        Assert.That(hud.transform.Find("Sector").GetComponent<TMP_Text>().text, Is.EqualTo($"{sector}/5"));
        var rule = location.WorldRule;
        Assert.That(WorldRuleController.Instance.ActiveRule,
            Is.SameAs(rule != null && rule.RuleType != WorldRuleType.None ? rule : null));
    }

    private static void FinishSector() => typeof(RunFlowController).GetProperty(nameof(RunFlowController.SectorElapsedTime))
        .SetValue(RunFlowController.Instance, RunStateManager.Instance.CurrentSector.Duration);

    private static bool IsPlayingSector(RunStateManager run, int sector) =>
        !SceneTransitionOverlay.IsTransitioning && run.CurrentSector.SectorNumber == sector &&
        RunFlowController.Instance is { Phase: RunPhase.NormalSector };

    private static IEnumerable<Button> AvailableButtons() => Object.FindObjectsByType<Button>(FindObjectsSortMode.None)
        .Where(button => button.name.StartsWith("Route location "));

    private static SurfaceMapNodeFeedback PreviewFeedback(int sector) =>
        GameObject.Find($"Route location {sector}-1").GetComponent<SurfaceMapNodeFeedback>();

    private static void AssertInViewport(SurfaceMapNavigation navigation, string name)
    {
        var viewport = (RectTransform)navigation.transform;
        Camera camera = viewport.GetComponentInParent<GraphicRaycaster>().eventCamera;
        Vector2 point = RectTransformUtility.WorldToScreenPoint(camera, GameObject.Find(name).transform.position);
        Assert.That(RectTransformUtility.RectangleContainsScreenPoint(viewport, point, camera), Is.True,
            $"{name} must be in the Game View at {point}.");
    }

    private static string NodeName(RouteNode node) => $"Route location {node.SectorNumber}-{node.Row + 1}";

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
