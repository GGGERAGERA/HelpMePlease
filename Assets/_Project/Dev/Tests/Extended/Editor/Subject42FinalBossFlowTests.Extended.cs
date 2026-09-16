#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Subject42.Combat.OrbitalStation;
using Object = UnityEngine.Object;

public sealed partial class Subject42FinalBossFlowTests
{

    [Category("Extended")]
    [Test]
    public void ProductionRouteEndsAtThreeWithoutAnExtraBossSector()
    {
        Assert.That(RunRoute.TotalSectors, Is.EqualTo(3));
        Assert.That(RunRoute.FinalSector, Is.EqualTo(RunRoute.TotalSectors));
        for (int sector = RunRoute.FirstSector; sector < RunRoute.TotalSectors; sector++)
            Assert.That(RunRoute.HasNextSector(sector), Is.True);
        Assert.That(RunRoute.HasNextSector(RunRoute.TotalSectors), Is.False);
        Assert.That(RunRoute.IsExplorationSector(RunRoute.TotalSectors), Is.True);
        Assert.That(RunRoute.IsExplorationSector(RunRoute.TotalSectors + 1), Is.False);
    }

    [Category("Extended")]
    [UnityTest]
    public IEnumerator FinalZoneWaitsForRewardsSpawnsOneBossAndReturnsVictoryToBunker()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
        yield return new EnterPlayMode();
        yield return ExerciseVictory();
    }

    [Category("Extended")]
    [UnityTest]
    public IEnumerator Vika_FinalBossVictoryAndReturnToBunker()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
        yield return new EnterPlayMode();
        yield return ExerciseVictory("Vika");
    }

    [Category("Extended")]
    [UnityTest]
    public IEnumerator EscapeProtocol_FirstAndRepeatGuardianVictory()
    {
        PlayerPrefs.DeleteKey(MetaProgressionManager.EscapeAccessKey);
        PlayerPrefs.DeleteKey(MetaProgressionManager.EscapeAnnouncedAccessKey);
        PlayerPrefs.SetInt(BunkerIntroController.ViewedPreferenceKey, 1);
        PlayerPrefs.Save();
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
        yield return new EnterPlayMode();
        yield return SceneManager.LoadSceneAsync("MainMenu");
        yield return Await(() => BunkerContext.Instance != null && !SceneTransitionOverlay.IsTransitioning);
        for (int i = 0; i < 10; i++) yield return null;
        Assert.That(MetaProgressionManager.EnsureExists().EscapeAccess, Is.Zero);
        Assert.That(MetaProgressionManager.Instance.HasEscapeUpdate, Is.False);
        var terminal = GameObject.Find("Escape Terminal").GetComponent<BunkerStation>();
        Assert.That(terminal.CanInteract, Is.True);
        terminal.Interact();
        Assert.That(BunkerContext.Instance.Panels.IsAnyPanelOpen, Is.True);
        Assert.That(One<EscapeProtocolView>().GetComponentsInChildren<TMP_Text>().Any(t => t.text == "ACCESS: 0 / 5"), Is.True);
        System.IO.Directory.CreateDirectory("Artifacts/GeneratedQA/EscapeProtocol");
        for (int i = 0; i < 5; i++) yield return null;
        ScreenCapture.CaptureScreenshot("Artifacts/GeneratedQA/EscapeProtocol/terminal-0.png");
        yield return null;
        BunkerContext.Instance.Panels.CloseAll();
        for (int victory = 0; victory < 2; victory++)
        {
            yield return StartRun();
            var run = RunStateManager.Instance;
            var old = run.CurrentSector;
            var finalStage = AssetDatabase.FindAssets("t:StageProfileData").Select(id =>
                AssetDatabase.LoadAssetAtPath<StageProfileData>(AssetDatabase.GUIDToAssetPath(id)))
                .First(stage => stage.SectorNumber == RunRoute.FinalSector);
            run.SetCurrentSector(new RunSector(RunRoute.FinalSector, finalStage, old.WorldRule, old.LocalAnomaly));
            yield return EnterFinalBossAndDefeat();
            yield return Await(() => SceneManager.GetActiveScene().name == "MainMenu" && !SceneTransitionOverlay.IsTransitioning);
            Assert.That(MetaProgressionManager.Instance.EscapeAccess, Is.EqualTo(1));
            yield return Await(() => One<BunkerRunSummaryPresenter>() != null && Get(One<BunkerRunSummaryPresenter>(), "notification") != null);
            var notification = (RectTransform)Get(One<BunkerRunSummaryPresenter>(), "notification");
            Assert.That(notification.GetComponentsInChildren<TMP_Text>().Any(t => t.text == "ACCESS I ACQUIRED"), Is.EqualTo(victory == 0));
            Assert.That(MetaProgressionManager.Instance.HasEscapeUpdate, Is.False);
            GameObject.Find("Escape Terminal").GetComponent<BunkerStation>().Interact();
            Assert.That(One<EscapeProtocolView>().GetComponentsInChildren<TMP_Text>().Any(t => t.text == "ACCESS: 1 / 5"), Is.True);
            if (victory == 0)
            {
                yield return new WaitForSecondsRealtime(3.5f);
                ScreenCapture.CaptureScreenshot("Artifacts/GeneratedQA/EscapeProtocol/terminal-1.png");
                yield return null;
            }
            BunkerContext.Instance.Panels.CloseAll();
        }
        yield return SceneManager.LoadSceneAsync("MainMenu");
        yield return new WaitForSecondsRealtime(.75f);
        Assert.That(MetaProgressionManager.Instance.EscapeAccess, Is.EqualTo(1));
        Assert.That(MetaProgressionManager.Instance.HasEscapeUpdate, Is.False);
        Assert.That(Get(One<BunkerRunSummaryPresenter>(), "notification"), Is.Null);
    }

    [Category("Extended")]
    [UnityTest]
    public IEnumerator EscapeProtocol_DepthCardsSelectAndLaunch()
    {
        PlayerPrefs.DeleteKey(MetaProgressionManager.EscapeAccessKey);
        PlayerPrefs.DeleteKey(MetaProgressionManager.EscapeAnnouncedAccessKey);
        PlayerPrefs.SetInt(BunkerIntroController.ViewedPreferenceKey, 1);
        PlayerPrefs.Save();
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
        yield return new EnterPlayMode();
        yield return SceneManager.LoadSceneAsync("MainMenu");
        yield return Await(() => BunkerContext.Instance != null && !SceneTransitionOverlay.IsTransitioning);
        for (int i = 0; i < 10; i++) yield return null;
        var gate = Object.FindObjectsByType<BunkerStation>(FindObjectsSortMode.None)
            .Single(s => (BunkerStationType)Get(s, "stationType") == BunkerStationType.StartRun);
        gate.Interact();
        var view = One<EscapeProtocolView>();
        Assert.That(view.gameObject.activeInHierarchy, Is.True);
        Assert.That(view.GetType().GetMethod("SelectDepth"), Is.Not.Null, "Level Select must own a selected Depth ID");
        var cards = view.GetComponentsInChildren<UnityEngine.UI.Button>().Where(b => b.name.StartsWith("DepthCard")).OrderBy(b => b.name).ToArray();
        Assert.That(cards.Length, Is.EqualTo(5));
        var action = (UnityEngine.UI.Button)Get(view, "startButton");
        var details = (TMP_Text)Get(view, "detailText");
        cards[0].onClick.Invoke();
        Assert.That(details.text, Does.Contain("SURFACE").And.Contain("3 SECTORS").And.Contain("ACCESS I"));
        Assert.That(action.IsInteractable(), Is.True);
        cards[1].onClick.Invoke();
        Assert.That(details.text, Does.Contain("REQUIRES ACCESS I"));
        Assert.That(action.IsInteractable(), Is.False);
        cards[2].onClick.Invoke();
        Assert.That(details.text, Does.Contain("DEPTH III").And.Contain("REQUIRES ACCESS II"));
        Assert.That(action.IsInteractable(), Is.False);
        action.onClick.Invoke();
        Assert.That(SceneTransitionOverlay.IsTransitioning, Is.False, "Disabled action also rejects direct invocation");
        System.IO.Directory.CreateDirectory("Artifacts/GeneratedQA/DepthSelect");
        for (int i = 0; i < 5; i++) yield return null;
        ScreenCapture.CaptureScreenshot("Artifacts/GeneratedQA/DepthSelect/fresh.png");
        yield return null;
        MetaProgressionManager.Instance.AcquireGuardianAccess();
        BunkerContext.Instance.Panels.CloseAll();
        gate.Interact();
        cards[1].onClick.Invoke();
        Assert.That(details.text, Does.Contain("DEPTH II").And.Contain("UNLOCKED").And.Contain("NOT AVAILABLE IN DEMO"));
        Assert.That(action.IsInteractable(), Is.False);
        action.onClick.Invoke();
        var starter = One<BunkerRunStarter>();
        var startMethod = typeof(BunkerRunStarter).GetMethod("StartRun", new[] { typeof(Transform), typeof(int) });
        Assert.That(startMethod, Is.Not.Null, "Selected Depth ID must reach the starter");
        startMethod.Invoke(starter, new object[] { (Transform)Get(gate, "runTransitionTarget"), 2 });
        Assert.That(SceneTransitionOverlay.IsTransitioning, Is.False, "Starter independently rejects unimplemented Depth II");
        for (int i = 0; i < 5; i++) yield return null;
        ScreenCapture.CaptureScreenshot("Artifacts/GeneratedQA/DepthSelect/access-i.png");
        yield return null;
        cards[0].onClick.Invoke();
        var character = AssetDatabase.FindAssets("t:CharacterData").Select(id =>
            AssetDatabase.LoadAssetAtPath<CharacterData>(AssetDatabase.GUIDToAssetPath(id))).First(c => c.characterName == "Gera");
        RunSelectionManager.Instance.SelectCharacter(character);
        action.onClick.Invoke();
        Assert.That(SceneTransitionOverlay.IsTransitioning, Is.True);
        yield return Await(() => SceneManager.GetActiveScene().name == "MVP");
        yield return Ready();
        Assert.That(RunStateManager.Instance.CurrentSector.SectorNumber, Is.EqualTo(1));
        Assert.That(RunRoute.TotalSectors, Is.EqualTo(3));
        Assert.That(typeof(RunStateManager).GetProperty("CurrentDepthId").GetValue(RunStateManager.Instance), Is.EqualTo(1));
    }
}
#endif
