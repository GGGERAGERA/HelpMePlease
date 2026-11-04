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
    public IEnumerator ClearanceRunsStandardCombat()
    {
        PrepareProductionBunker();
        yield return new EnterPlayMode();
        yield return StartLocation(LocationType.Clearance);
        Assert.That(Object.FindFirstObjectByType<EnemySpawner>().IsSpawningEnabled, Is.True);
        Assert.That(GameObject.Find("Ground mine"), Is.Null);
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
        yield return StartLocation(LocationType.Clearance);

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

    [UnityTest, Timeout(50000)]
    public IEnumerator MinefieldWarnsAndDetonatesWithOrdinarySpawnsEnabled()
    {
        PrepareProductionBunker();
        yield return new EnterPlayMode();
        yield return ExerciseMinefield();
    }

    private static IEnumerator ExerciseMinefield()
    {
        yield return StartLocation(LocationType.Minefield);
        var director = Object.FindFirstObjectByType<WorldHazardDirector>();
        Assert.That(director, Is.Not.Null, "MVP hazard director");
        yield return CoreTestSupport.Await(() => director.HasPendingAttack && GameObject.Find("Ground mine") != null);
        Assert.That(Object.FindFirstObjectByType<EnemySpawner>().IsSpawningEnabled, Is.True);
        yield return CoreTestSupport.Await(() => !director.HasPendingAttack);
        Assert.That(RunFlowController.Instance.SectorElapsedTime, Is.LessThan(60f));
    }

    [UnityTest, Timeout(55000)]
    public IEnumerator ForgottenCacheGrantsOnceThroughStandardReel()
    {
        PrepareProductionBunker();
        yield return new EnterPlayMode();
        yield return ExerciseForgottenCache();
    }

    private static IEnumerator ExerciseForgottenCache()
    {
        yield return StartLocation(LocationType.UnknownSignal, SurpriseProfile.ForgottenCache);
        Time.timeScale = 2f;
        yield return CoreTestSupport.Await(() => GameObject.Find("Incident forgotten cache") != null);
        Assert.That(WorldLootChest.ActiveInstances.Count(chest => chest.name == "Incident forgotten cache"), Is.EqualTo(1));
        var cache = GameObject.Find("Incident forgotten cache").GetComponent<WorldLootChest>();
        var rewards = UpgradeManager.Instance;
        int commits = 0;
        rewards.DebugRewardCommitted += _ => commits++;
        cache.Interact();
        Time.timeScale = 0f;
        FinishSector();
        yield return null;
        Assert.That(cache.State, Is.EqualTo(WorldLootChest.ChestState.Opening));
        Assert.That(cache.isActiveAndEnabled, Is.True, "An already opened chest stays under the normal reward flow.");
        Time.timeScale = 1f;
        cache.NotifyOpeningAnimationComplete();
        cache.NotifyOpeningAnimationComplete();
        var reel = Object.FindFirstObjectByType<WorldLootRewardReel>();
        Assert.That(reel, Is.Not.Null);
        var stop = reel.GetComponentsInChildren<Button>(true).Single();
        yield return CoreTestSupport.Await(() => stop.isActiveAndEnabled && stop.interactable);
        stop.onClick.Invoke();
        yield return CoreTestSupport.Await(() => WorldLootRewardReel.LastClaimedUpgrade != null);
        yield return RewardScenarioAssertions.FinishReward();
        Assert.That(commits, Is.EqualTo(1));
    }

    [UnityTest, Timeout(50000)]
    public IEnumerator AmbushWarnsBeforeSpawningAndClearsOnDeparture()
    {
        PrepareProductionBunker();
        yield return new EnterPlayMode();
        yield return ExerciseAmbush();
    }

    private static IEnumerator ExerciseAmbush()
    {
        yield return StartLocation(LocationType.UnknownSignal, SurpriseProfile.Ambush);
        Time.timeScale = 2f;
        var exploration = ProductionExplorationSectorController.ActiveInstance;
        var waves = (IList)typeof(ProductionExplorationSectorController).GetField("routeWaves", InstanceMembers).GetValue(exploration);
        var messages = RunMessageService.Instance.View;
        string title = LocalizationService.Instance.Get("route.incident.ambush");
        yield return CoreTestSupport.Await(() => messages.IsPanelVisible && messages.GetComponentsInChildren<TMP_Text>()
            .Any(text => string.Equals(text.text, title, System.StringComparison.OrdinalIgnoreCase)));
        Assert.That(waves, Is.Empty, "The warning precedes the additional group.");
        yield return CoreTestSupport.Await(() => waves.Count == 1);
        Time.timeScale = 0f;
        FinishSector();
        yield return null;
        Assert.That(waves, Is.Empty, "The wave expires even while departure waits.");
        Time.timeScale = 1f;
        yield return CoreTestSupport.Await(() => Object.FindFirstObjectByType<LevelChoiceManager>().IsChoosing);
        Assert.That(waves, Is.Empty, "The departing sector releases its ambush.");
    }

    [UnityTest, Timeout(50000)]
    public IEnumerator FieldFaultUsesTemporaryRegisteredAnomaly()
    {
        PrepareProductionBunker();
        yield return new EnterPlayMode();
        yield return ExerciseFieldFault();
    }

    private static IEnumerator ExerciseFieldFault()
    {
        yield return StartLocation(LocationType.UnknownSignal, SurpriseProfile.FieldFault);
        Time.timeScale = 2f;
        yield return CoreTestSupport.Await(() => GameObject.Find("Incident field fault") != null);
        var zone = GameObject.Find("Incident field fault").GetComponent<LocalAnomalyZone>();
        Physics2D.SyncTransforms();
        Vector2 point = zone.FocusArea.bounds.center;
        Assert.That(LevelAnomalyController.Instance.FindOrbitZone(point), Is.SameAs(zone));
        yield return CoreTestSupport.Await(() => LevelAnomalyController.Instance.FindOrbitZone(point) != zone);
        Assert.That(RunFlowController.Instance.SectorElapsedTime, Is.LessThan(60f));
    }

    [UnityTest, Timeout(50000)]
    public IEnumerator MedStationHealsThroughExistingPickup()
    {
        PrepareProductionBunker();
        yield return new EnterPlayMode();
        yield return ExerciseMedStation();
    }

    private static IEnumerator ExerciseMedStation()
    {
        yield return StartLocation(LocationType.UnknownSignal, SurpriseProfile.MedStation);
        Time.timeScale = 2f;
        yield return CoreTestSupport.Await(() => GameObject.Find("Incident med station") != null);
        var medkit = GameObject.Find("Incident med station").GetComponent<RarePickup>();
        var health = PlayerRuntimeReference.PlayerHealth;
        float before = health.MaxHealth * .5f;
        health.SetRuntimeHealth(health.MaxHealth, before);
        PlacePlayer(medkit.transform.position);
        yield return CoreTestSupport.Await(() => health.CurrentHealth > before);
        Assert.That(health.CurrentHealth, Is.EqualTo(before + health.MaxHealth * .25f).Within(.01f));
    }

    [UnityTest, Timeout(50000)]
    public IEnumerator EmergencyProtocolRewardsExistingEventAndWaveOnce()
    {
        PrepareProductionBunker();
        yield return new EnterPlayMode();
        yield return ExerciseEmergencyProtocol();
    }

    private static IEnumerator ExerciseEmergencyProtocol()
    {
        yield return StartLocation(LocationType.UnknownSignal, SurpriseProfile.EmergencyProtocol);
        var events = Object.FindFirstObjectByType<WorldEventSpawner>();
        var relay = events.SpawnedEvents.OfType<OrbitalRelayEvent>().First();
        var start = (Collider2D)typeof(OrbitalRelayEvent).GetField("startArea", InstanceMembers).GetValue(relay);
        PlacePlayer(start.bounds.center);
        Assert.That(relay.CanInteract, Is.True);
        relay.Interact();
        Time.timeScale = 2f;
        yield return CoreTestSupport.Await(() => GameObject.Find("Emergency protocol offer") != null);
        var offer = GameObject.Find("Emergency protocol offer").GetComponent<Interactable>();
        Assert.That(events.ActiveEvent, Is.SameAs(relay));
        Assert.That(GameObject.Find("Incident protocol bonus chest"), Is.Null);
        var before = EnemyHealth.ActiveInstances.ToArray();
        offer.Interact();
        Assert.That(offer.CanInteract, Is.False);
        var guards = EnemyHealth.ActiveInstances.Where(enemy => !before.Contains(enemy)).ToArray();
        Assert.That(guards, Is.Not.Empty);
        foreach (var guard in guards) guard.TakeDamage(10000f, guard.transform.position);
        Assert.That(GameObject.Find("Incident protocol bonus chest"), Is.Null, "The original event still has to complete.");
        var complete = typeof(WorldEvent).GetMethod("CompleteEvent", InstanceMembers);
        complete.Invoke(relay, null);
        complete.Invoke(relay, null);
        Assert.That(WorldLootChest.ActiveInstances.Count(chest => chest.name == "Incident protocol bonus chest"), Is.EqualTo(1));
    }

    private const System.Reflection.BindingFlags InstanceMembers = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;

    private static void PlacePlayer(Vector3 point)
    {
        var player = PlayerRuntimeReference.PlayerTransform;
        player.position = point;
        if (player.TryGetComponent<Rigidbody2D>(out var body)) { body.position = point; body.linearVelocity = Vector2.zero; }
        Physics2D.SyncTransforms();
    }

    private static IEnumerator StartLocation(LocationType type, SurpriseProfile incident = SurpriseProfile.None)
    {
        yield return CoreTestSupport.Await(() => ProductionSceneComposition.Active is { IsReady: true });
        var starter = Object.FindFirstObjectByType<BunkerRunStarter>();
        const System.Reflection.BindingFlags instance = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
        const System.Reflection.BindingFlags staticMethod = System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic;
        var source = (LevelChoiceManager)typeof(BunkerRunStarter).GetField("routeSource", instance).GetValue(starter);
        var initial = (WorldRuleData)typeof(BunkerRunStarter).GetField("startingWorldRule", instance).GetValue(starter);
        var rules = (WorldRuleData[])typeof(LevelChoiceManager).GetField("availableWorldRules", instance).GetValue(source);
        var weights = (RouteLocationWeight[])typeof(LevelChoiceManager).GetField("locationWeights", instance).GetValue(source);
        var generate = typeof(RunRouteGraph).GetMethod("Generate", staticMethod);
        bool Matches(RouteNode node) => node.LocationType == type && node.SurpriseProfile == incident;
        int seed = Enumerable.Range(1, 250).First(candidate =>
            ((RunRouteGraph)generate.Invoke(null, new object[] { candidate, rules, weights, initial, false, null, .2f }))
                .Entries.Any(Matches));
        GameplayRandom.Begin(seed);
        starter.StartRun(starter.transform);
        yield return CoreTestSupport.Await(() => SceneManager.GetActiveScene().name == "MVP" &&
            ProductionSceneComposition.Active is { IsReady: true } && !SceneTransitionOverlay.IsTransitioning);
        var manager = Object.FindFirstObjectByType<LevelChoiceManager>();
        yield return CoreTestSupport.Await(() => manager.IsChoosing);
        var run = RunStateManager.Instance;
        var selected = run.RouteGraph.Entries.First(Matches);
        Assert.That((bool)typeof(LevelChoiceManager).GetMethod("SelectNode", instance).Invoke(manager, new object[] { selected }), Is.True);
        Assert.That(run.CurrentSector.RouteNode, Is.SameAs(selected));
        Assert.That(run.CurrentSector.Duration, Is.EqualTo(60f));
        Assert.That(WorldRuleController.Instance.ActiveRule,
            Is.SameAs(selected.WorldRule != null && selected.WorldRule.RuleType != WorldRuleType.None ? selected.WorldRule : null));
        Object.FindFirstObjectByType<CharacterSpawner>().SpawnedPlayer.GetComponent<PlayerHealth>().DebugInvulnerable = true;
        yield return null;
    }

    [Test]
    public void SeededGraphHasFifteenReachableColumnsAndDistinctForks()
    {
        const System.Reflection.BindingFlags instance = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
        const System.Reflection.BindingFlags staticMethod = System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic;
        var source = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/prefabs/Bootstrap/GameplayRun.prefab")
            .GetComponentInChildren<LevelChoiceManager>(true);
        var rules = (WorldRuleData[])typeof(LevelChoiceManager).GetField("availableWorldRules", instance).GetValue(source);
        var weights = (RouteLocationWeight[])typeof(LevelChoiceManager).GetField("locationWeights", instance).GetValue(source);
        var generate = typeof(RunRouteGraph).GetMethod("Generate", staticMethod);
        RunRouteGraph Generate(int seed, float chance = .2f) => (RunRouteGraph)generate.Invoke(null,
            new object[] { seed, rules, weights, null, false, null, chance });
        string Fingerprint(RunRouteGraph graph) => string.Join("|",
            Enumerable.Range(1, 15).SelectMany(graph.NodesAt).Select(node =>
                $"{node.SectorNumber}:{node.Row}:{node.LocationType}:{node.WorldRule?.RuleType}:{node.SurpriseProfile}:{string.Join(",", node.Next.Select(next => next.Row))}"));

        Assert.That(Fingerprint(Generate(734)), Is.EqualTo(Fingerprint(Generate(734))));
        Assert.That(Fingerprint(Generate(734)), Is.Not.EqualTo(Fingerprint(Generate(735))));
        Assert.That(Enumerable.Range(1, 15).SelectMany(Generate(734, 0f).NodesAt)
            .Where(node => node.LocationType != LocationType.UnknownSignal).All(node => node.SurpriseProfile == SurpriseProfile.None), Is.True);
        var combinations = new HashSet<(LocationType, WorldRuleType)>();
        for (int seed = 0; seed < 24; seed++)
        {
            var graph = Generate(seed);
            Assert.That(graph.Seed, Is.EqualTo(seed));
            IReadOnlyList<RouteNode> reachable = graph.Entries;
            for (int sector = 1; sector <= 15; sector++)
            {
                var nodes = graph.NodesAt(sector);
                Assert.That(nodes, Is.EquivalentTo(reachable), $"Seed {seed}, sector {sector}");
                Assert.That(nodes.Count, Is.InRange(1, 3));
                Assert.That(nodes.Select(node => node.LocationType).Distinct().Count(), Is.EqualTo(nodes.Count));
                foreach (var node in nodes)
                {
                    Assert.That(node.SectorNumber, Is.EqualTo(sector));
                    Assert.That(node.Level, Is.EqualTo((sector - 1) / 3 + 1));
                    if (node.LocationType == LocationType.UnknownSignal)
                        Assert.That(node.SurpriseProfile, Is.Not.EqualTo(SurpriseProfile.None));
                    if (node.WorldRule != null) combinations.Add((node.LocationType, node.WorldRule.RuleType));
                    if (sector == 15) Assert.That(node.Next, Is.Empty);
                    else
                    {
                        Assert.That(node.Next, Is.Not.Empty);
                        Assert.That(node.Next.All(next => next.SectorNumber == sector + 1), Is.True);
                    }
                }
                reachable = nodes.SelectMany(node => node.Next).Distinct().ToArray();
            }
        }
        Assert.That(combinations.Where(pair => pair.Item2 == WorldRuleType.Golden).Select(pair => pair.Item1).Distinct().Count(),
            Is.GreaterThan(1), "Golden weather appears over different location types.");
        Assert.That(combinations.Select(pair => pair.Item1).Distinct(), Is.EquivalentTo(new[]
        {
            LocationType.Clearance, LocationType.Minefield, LocationType.UnknownSignal
        }));
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
        Assert.That(GameObject.Find("Route location 15-1"), Is.Not.Null, "Fog must retain the far silhouette.");
        var roads = GameObject.Find("Roads").GetComponent<Graphic>();
        var tracks = (IList)roads.GetType().GetField("tracks", InstanceMembers).GetValue(roads);
        int expectedRoads = entryGraph.Entries.Count + entryGraph.NodesAt(RunRoute.FinalSector).Count +
            Enumerable.Range(1, RunRoute.TotalSectors).SelectMany(entryGraph.NodesAt).Sum(node => node.Next.Count);
        Assert.That(tracks.Count, Is.EqualTo(expectedRoads));
        Assert.That(TypeMark(4), Is.Not.Null);
        Assert.That(TypeMark(5), Is.Null);
        Assert.That(AvailableButtons().Count(), Is.EqualTo(3));
        Assert.That(GameObject.Find("Route selection glow"), Is.Null, "Opening the map does not choose a node.");
        var preview = GameObject.Find("Route location 2-1");
        Assert.That(preview.GetComponent<Button>(), Is.Null);
        var pointer = new PointerEventData(EventSystem.current);
        ExecuteEvents.Execute(preview, pointer, ExecuteEvents.pointerEnterHandler);
        Assert.That(GameObject.Find("Node intel").GetComponent<TMP_Text>().text,
            Does.Contain("Погода").And.Not.Contain("Пустошь"));
        Assert.That(GameObject.Find("Route confirm"), Is.Null, "Preview has no launch action.");
        ExecuteEvents.Execute(preview, pointer, ExecuteEvents.pointerExitHandler);
        Click(GameObject.Find("Route location 1-2").GetComponent<Button>());
        Assert.That(GameObject.Find("Route selection glow"), Is.Not.Null);
        var intel = GameObject.Find("Node intel").GetComponent<TMP_Text>();
        Assert.That(intel.text, Does.StartWith(LocalizationService.Instance.Get(entryGraph.Entries[1].LocationLabelKey))
            .And.Contain("Погода").And.Not.Contain("Пустошь"));
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
            var selection = GameObject.Find("Route selection glow");
            var scroll = new PointerEventData(EventSystem.current) { scrollDelta = Vector2.down * 40f };
            ExecuteEvents.Execute(navigation.gameObject, scroll, ExecuteEvents.scrollHandler);
            Assert.That(navigation.Content.anchoredPosition.x, Is.LessThan(original.x - 1000f));
            Assert.That(GameObject.Find("Route character").transform.localPosition, Is.EqualTo(marker));
            Assert.That(GameObject.Find("Route selection glow"), Is.SameAs(selection));
            Assert.That(TypeMark(14), Is.Null);
            AssertInViewport(navigation, "Route location 14-1");
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
            FinishSector();
            if (sector == RunRoute.FinalSector) break;

            var manager = Object.FindFirstObjectByType<LevelChoiceManager>();
            yield return CoreTestSupport.Await(() => manager.IsChoosing);
            Assert.That(run.CompletedLevels, Is.EqualTo(sector / RunRoute.SectorsPerLevel));
            AssertMapLevel(RunRoute.LevelOf(sector + 1));
            var buttons = AvailableButtons().ToArray();
            Assert.That(buttons.Select(button => button.name), Is.EquivalentTo(location.Next.Select(NodeName)));
            Assert.That(GameObject.Find(NodeName(location)).GetComponent<Button>(), Is.Null);
            if (sector <= RunRoute.TotalSectors - 5)
            {
                Assert.That(TypeMark(sector + 4), Is.Not.Null, "The preview advances after completing a sector.");
                Assert.That(TypeMark(sector + 5), Is.Null, "Further landmarks still conceal their types.");
            }
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
        Assert.That(header.text, Is.EqualTo($"Пустошь {level}/5"));
        Assert.That(Object.FindObjectsByType<TMP_Text>(FindObjectsSortMode.None)
            .Where(label => label.name == "Route stage name"), Is.EqualTo(new[] { header }));
    }

    private static void AssertBattleSector(RunStateManager run, RouteNode location, int sector)
    {
        Assert.That(run.CurrentSector.SectorNumber, Is.EqualTo(sector));
        Assert.That(run.CurrentSector.RouteNode, Is.SameAs(location));
        Assert.That(run.CurrentSector.Duration, Is.EqualTo(60f));
        Assert.That(GameObject.Find("Route stage name"), Is.Null, "Combat starts after confirming the selected sector.");
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

    private static Transform TypeMark(int sector) => GameObject.Find($"Route location {sector}-1").transform.Find("Type mark");

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
