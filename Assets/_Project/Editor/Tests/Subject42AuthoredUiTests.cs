#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Subject42.Combat.OrbitalStation;
using Object = UnityEngine.Object;

public sealed class Subject42AuthoredUiTests
{
    private const string Folder = "Assets/_Project/prefabs/UI/Authored/";

    [Test]
    public void ProductionScenesHaveRequiredUiReferencesAndOneEventSystem()
    {
        var report = Subject42ProjectValidator.ValidateAuthoredUi();
        Assert.That(report.ErrorCount, Is.Zero, report.FormatErrors());
    }

    [TestCase("AnomalySlotHUD", 4)]
    [TestCase("TacticalMapShell", 43)]
    [TestCase("WorldLootReelView", 13)]
    [TestCase("DeathResultWindow", 57)]
    public void ShellsAreAuthoredWithoutDynamicItems(string name, int transforms)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Folder + name + ".prefab");
        Assert.That(prefab, Is.Not.Null);
        Assert.That(prefab.GetComponentsInChildren<Transform>(true).Length, Is.EqualTo(transforms));
        foreach (var text in prefab.GetComponentsInChildren<TMP_Text>(true))
        {
            Assert.That(text.font, Is.Not.Null, text.name);
            Assert.That(text.fontSharedMaterial, Is.Not.Null, text.name);
        }
        foreach (var component in prefab.GetComponentsInChildren<Component>(true))
            Assert.That(component, Is.Not.Null, "Missing prefab script");
    }

    [TestCase(typeof(AnomalySlotHUD), "Authored title/value")]
    [TestCase(typeof(TacticalMapHUD), "Authored shell or scene")]
    [TestCase(typeof(WorldLootRewardReel), "Authored shell references")]
    [TestCase(typeof(DeathResultPresentation), "Authored result references")]
    [TestCase(typeof(BunkerContext), "Authored progression/loadout")]
    public void MissingMandatoryReferencesDisableWithoutConstructingUi(Type type, string error)
    {
        var root = new GameObject("Missing UI fixture");
        root.SetActive(false);
        try
        {
            var component = (MonoBehaviour)root.AddComponent(type);
            LogAssert.Expect(LogType.Error, new Regex(Regex.Escape(error)));
            Call(component, "Awake");
            Assert.That(component.enabled, Is.False);
            Assert.That(root.GetComponentsInChildren<Transform>(true).Length, Is.EqualTo(1));
        }
        finally { Object.DestroyImmediate(root); }
    }

    [Test]
    public void MissingGameplayHostReferencesDisableWithoutAddingSystems()
    {
        var root = new GameObject("Missing host fixture");
        root.SetActive(false);
        try
        {
            var host = root.AddComponent<LevelModifiersApplier>();
            var start = (IEnumerator)typeof(LevelModifiersApplier).GetMethod("Start", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(host, null);
            Assert.That(start.MoveNext(), Is.True);
            LogAssert.Expect(LogType.Error, new Regex("Authored scene-host references are missing"));
            Assert.That(start.MoveNext(), Is.False);
            Assert.That(host.enabled, Is.False);
            Assert.That(root.GetComponent<RunThreatController>(), Is.Null);
            Assert.That(root.GetComponent<ProductionExplorationSectorController>(), Is.Null);
        }
        finally { Object.DestroyImmediate(root); }
    }

    [UnityTest]
    public IEnumerator AuthoredUiSupportsLootPauseResultsAndSceneTransitions()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
        yield return new EnterPlayMode();
        yield return ExerciseProductionUi();
        yield return new ExitPlayMode();
    }

    private static IEnumerator ExerciseProductionUi()
    {
        SceneManager.LoadSceneAsync("MainMenu");
        yield return Await(() => SceneManager.GetActiveScene().name == "MainMenu" && One<BunkerContext>() != null);
        for (int i = 0; i < 5; i++) yield return null;
        var intro = One<BunkerIntroController>();
        if (intro != null) { intro.StopAllCoroutines(); Call(intro, "FinishIntro", false); }
        for (int i = 0; i < 10; i++) yield return null;
        var context = One<BunkerContext>();
        Assert.That(context.StationProgression, Is.SameAs(context.GetComponent<BunkerStationProgressionService>()));
        Assert.That(context.GetComponents<BunkerStationProgressionService>().Length, Is.EqualTo(1));
        Assert.That(context.GetComponents<BunkerPlayerLoadoutController>().Length, Is.EqualTo(1));
        AssertEvents();
        context.Panels.OpenCharacterSelection();
        var selection = (BunkerSelectionWindow)Get(Get(context.Panels, "selectionPanelController"), "sharedSelectionWindow");
        Assert.That(selection.IsOpen, Is.True, selection.name + " active=" + selection.gameObject.activeInHierarchy + " source=" + Get(selection, "source"));
        var confirm = (Button)Get(selection, "confirmButton");
        PointerClick(confirm);
        context.Panels.OpenUpgrade();
        Assert.That(selection.IsOpen, Is.True);
        context.Panels.OpenAnomalyStabilizers();
        Assert.That(selection.IsOpen, Is.True);
        // Exercise the actual authored investment input without changing persisted meta progression.
        var progress = (BunkerProgressionView)Get(selection, "stationProgress");
        int investment = 0;
        progress.Bind(new BunkerProgressionModel { TargetId = "test", Level = 1, MaxLevel = 3,
            SupportsPartialInvestment = true, RequiredProgress = 500, AvailableCurrency = 500,
            CanUpgrade = () => true, Invest = amount => investment += amount });
        var investmentButton = (Button)Get(progress, "upgradeButton");
        var pointer = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left };
        ExecuteEvents.Execute(investmentButton.gameObject, pointer, ExecuteEvents.pointerDownHandler);
        Assert.That((bool)Get(progress, "investing"), Is.True);
        ExecuteEvents.Execute(investmentButton.gameObject, pointer, ExecuteEvents.pointerUpHandler);
        Assert.That(investment, Is.EqualTo(1));
        Assert.That((bool)Get(progress, "investing"), Is.False);
        context.Panels.CloseAll();
        yield return StartRun();
        Time.timeScale = 0;
        AssertEvents();
        Assert.That(One<RunThreatController>(), Is.Not.Null);
        var anomaly = One<AnomalySlotHUD>();
        int subscribers = Subscribers(RunStateManager.Instance.AnomalyInventory, "Changed");
        anomaly.enabled = false;
        Assert.That(Subscribers(RunStateManager.Instance.AnomalyInventory, "Changed"), Is.EqualTo(subscribers - 1));
        anomaly.enabled = true;
        Assert.That(Subscribers(RunStateManager.Instance.AnomalyInventory, "Changed"), Is.EqualTo(subscribers));
        var map = One<TacticalMapHUD>();
        Call(map, "RefreshMarkers");
        var markers = (IList)Get(map, "breakableMarkers");
        object firstMarker = markers.Count > 0 ? markers[0] : null;
        Call(map, "RefreshMarkers");
        if (firstMarker != null) Assert.That(markers[0], Is.SameAs(firstMarker));

        var reward = ScriptableObject.CreateInstance<UiProbeReward>();
        int claims = 0;
        Assert.That(WorldLootRewardReel.TryShow(new[] { reward }, Vector3.zero, _ => claims++), Is.True);
        var reel = One<WorldLootRewardReel>();
        reel.enabled = false;
        Set(reel, "stateElapsed", 1f); Call(reel, "UpdateTransfer");
        Set(reel, "stateElapsed", 1f); Call(reel, "UpdatePanelReveal");
        Set(reel, "stateElapsed", 1f);
        var stop = (Button)Get(reel, "stopButton"); stop.interactable = true;
        yield return RenderFrames();
        PointerClick(stop, true);
        Assert.That(Get(reel, "state").ToString(), Is.EqualTo("Braking"));
        Set(reel, "stateElapsed", 10f); Call(reel, "UpdateBraking");
        Set(reel, "stateElapsed", 10f); Call(reel, "UpdateSnapping");
        Call(reel, "ShowReveal");
        PointerClick(stop); // A stale stop click cannot repeat the award.
        Assert.That(reward.Applied, Is.EqualTo(1));
        Assert.That(claims, Is.EqualTo(1));
        Call(reel, "CloseWithoutReward");
        Assert.That(WorldLootRewardReel.IsActive, Is.False);
        Object.Destroy(reward);
        reel.enabled = true;
        var pause = One<PauseMenuUI>();
        Time.timeScale = 1;
        Call(pause, "HandleEscape"); Assert.That(pause.IsPaused, Is.True); Assert.That(Time.timeScale, Is.Zero);
        Call(pause, "HandleEscape"); Assert.That(pause.IsPaused, Is.False); Assert.That(Time.timeScale, Is.EqualTo(1));
        Time.timeScale = 0;

        // Sector reload must replace scene-local UI, including EventSystem and the hidden reel.
        var previousStation = One<OrbitalStationRuntime>();
        var run = RunStateManager.Instance;
        var oldSector = run.CurrentSector;
        var stage2 = AssetDatabase.FindAssets("t:StageProfileData").Select(id => AssetDatabase.LoadAssetAtPath<StageProfileData>(AssetDatabase.GUIDToAssetPath(id))).First(s => s.SectorNumber == 2);

        Call(One<LevelChoiceManager>(), "TransitionToSector", new RunSector(2, stage2, oldSector.WorldRule, oldSector.LocalAnomaly));
        yield return Await(() => One<OrbitalStationRuntime>() != null && One<OrbitalStationRuntime>() != previousStation && One<OrbitalStationRuntime>().IsInitialized);
        Time.timeScale = 0; AssertEvents();
        var result = One<RunResultView>();
        for (int i = 0; i < 30; i++) RunStatsManager.Instance.AddKill();
        var summary = run.GetRunSummarySnapshot(RunEndReason.PlayerDied);
        int currencyBefore = CurrencyManager.Instance.TotalGold;
        var health = Object.FindFirstObjectByType<CharacterSpawner>().SpawnedPlayer.GetComponent<PlayerHealth>();
        Call(health, "Die");
        var death = One<DeathResultPresentation>();
        Assert.That(((TMP_Text)Get(death, "kills")).text, Is.EqualTo(summary.Kills.ToString()));
        Assert.That(((TMP_Text)Get(death, "gold")).text, Is.EqualTo(summary.GoldEarned.ToString()));
        Assert.That(((RectTransform)Get(death, "window")).gameObject.activeSelf, Is.True);
        Assert.That(((Canvas)Get(death, "modalCanvas")).overrideSorting, Is.True);
        Assert.That(((Canvas)Get(death, "modalCanvas")).sortingOrder, Is.GreaterThan(55));
        int hierarchy = result.GetComponentsInChildren<Transform>(true).Length;
        result.Show(false);result.Show(true);result.Show(false);
        Assert.That(result.GetComponentsInChildren<Transform>(true).Length, Is.EqualTo(hierarchy));
        var deadStation = One<OrbitalStationRuntime>();
        var restart = (Button)Get(death, "restartButton");
        yield return RenderFrames();
        PointerClick(restart, true);PointerClick(restart);
        yield return Await(() => One<OrbitalStationRuntime>() != null && One<OrbitalStationRuntime>() != deadStation && One<OrbitalStationRuntime>().IsInitialized);
        Time.timeScale = 0; AssertEvents();
        Assert.That(CurrencyManager.Instance.TotalGold, Is.EqualTo(currencyBefore + summary.GoldEarned));
        // Death return button uses the same existing idempotent RunEndService.
        result = One<RunResultView>();result.Show(false);
        death = One<DeathResultPresentation>();
        PointerClick((Button)Get(death, "bunkerButton"));
        yield return Await(() => SceneManager.GetActiveScene().name == "MainMenu" && One<BunkerContext>() != null);
        AssertEvents();
        yield return StartRun();
        // The real victory route goes to the Bunker summary, not the legacy victory panel.
        run = RunStateManager.Instance; oldSector = run.CurrentSector;
        var stage4 = AssetDatabase.FindAssets("t:StageProfileData").Select(id => AssetDatabase.LoadAssetAtPath<StageProfileData>(AssetDatabase.GUIDToAssetPath(id))).First(s => s.SectorNumber == RunRoute.FinalBossSector);
        run.SetCurrentSector(new RunSector(RunRoute.FinalBossSector, stage4, oldSector.WorldRule, oldSector.LocalAnomaly));
        Time.timeScale = 0;
        var ending = RunEndService.Instance;
        ending.CompleteRunVictory();
        int afterVictory = CurrencyManager.Instance.TotalGold;
        ending.CompleteRunVictory();
        Assert.That(CurrencyManager.Instance.TotalGold, Is.EqualTo(afterVictory));
        yield return Await(() => SceneManager.GetActiveScene().name == "MainMenu" && One<BunkerContext>() != null);
        AssertEvents();
        yield return Await(() => One<BunkerRunSummaryPresenter>() != null && Get(One<BunkerRunSummaryPresenter>(), "notification") != null);
        Assert.That(((RectTransform)Get(One<BunkerRunSummaryPresenter>(), "notification")).gameObject.activeInHierarchy, Is.True);
        Time.timeScale = 1;
    }

    private static IEnumerator StartRun()
    {
        var starter = One<BunkerRunStarter>();
        var character = AssetDatabase.FindAssets("t:CharacterData").Select(id => AssetDatabase.LoadAssetAtPath<CharacterData>(AssetDatabase.GUIDToAssetPath(id))).First(c => c.characterPrefab != null);
        RunSelectionManager.Instance.SelectCharacter(character);
        starter.StartRun((Transform)Get(starter, "cameraRig"));
        yield return Await(() => SceneManager.GetActiveScene().name == "MVP" && One<OrbitalStationRuntime>() != null && One<OrbitalStationRuntime>().IsInitialized && One<ProductionExplorationSectorController>() != null);
    }

    private static IEnumerator RenderFrames()
    {
        int frame = Time.frameCount + 2;
        float end = Time.realtimeSinceStartup + 5f;
        while (Time.frameCount < frame && Time.realtimeSinceStartup < end) yield return null;
    }

    private static IEnumerator Await(Func<bool> condition)
    {
        float deadline = Time.realtimeSinceStartup + 30f;
        while (!condition() && Time.realtimeSinceStartup < deadline) yield return null;
        Assert.That(condition(), Is.True, "Timed out waiting for production scene/UI");
    }
    private static void AssertEvents()
    {
        var all = Object.FindObjectsByType<EventSystem>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        Assert.That(all.Length, Is.EqualTo(1));
        Assert.That(all[0].isActiveAndEnabled, Is.True);
        Assert.That(all[0].GetComponent<StandaloneInputModule>(), Is.Not.Null);
        Assert.That(Object.FindObjectsByType<WorldLootRewardReel>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length,
            Is.EqualTo(SceneManager.GetActiveScene().name == "MVP" ? 1 : 0));
    }
    private static void PointerClick(Button button, bool verifyRaycast = false)
    {
        var pointer = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left };
        if (verifyRaycast)
        {
            Canvas.ForceUpdateCanvases();
            var canvas = button.GetComponentInParent<Canvas>().rootCanvas;
            var rect = (RectTransform)button.transform;
            pointer.position = RectTransformUtility.WorldToScreenPoint(
                canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera,
                rect.TransformPoint(rect.rect.center));
            var hits = new System.Collections.Generic.List<RaycastResult>();
            EventSystem.current.RaycastAll(pointer, hits);
            Assert.That(hits.Count, Is.GreaterThan(0), button.name + " must receive UI raycasts; active=" + button.gameObject.activeInHierarchy + " depth=" + button.targetGraphic.depth + " cull=" + button.targetGraphic.canvasRenderer.cull + " point=" + pointer.position + " screen=" + Screen.width + "x" + Screen.height);
            Assert.That(hits[0].gameObject.GetComponentInParent<Button>(), Is.SameAs(button), button.name + " is occluded");
        }
        ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerEnterHandler);
        ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerClickHandler);
        ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerExitHandler);
    }
    private static T One<T>() where T : Object => Object.FindFirstObjectByType<T>(FindObjectsInactive.Include);
    private static FieldInfo Field(object target, string name)
    {
        for (var type = target.GetType(); type != null; type = type.BaseType)
        { var field = type.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic); if (field != null) return field; }
        throw new MissingFieldException(name);
    }
    private static object Get(object target, string name) => Field(target, name).GetValue(target);
    private static void Set(object target, string name, object value) => Field(target, name).SetValue(target, value);
    private static int Subscribers(object target, string name) => ((Delegate)Get(target, name))?.GetInvocationList().Length ?? 0;
    private static void Call(object target, string name, params object[] args) => target.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, args);
}

public sealed class UiProbeReward : WorldLootRewardDefinition
{
    public int Applied;
    public override bool Apply() { Applied++; return true; }
}
#endif

