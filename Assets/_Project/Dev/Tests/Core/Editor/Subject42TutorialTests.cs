#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using Subject42.Combat.OrbitalStation;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

[Category("Core")]
public sealed class Subject42TutorialTests
{
    private const string Key = "Subject42.Tutorial.Completed";
    [SetUp] public void Setup()
    {
        CoreTestSupport.PreservePreferences();
        PlayerPrefs.DeleteKey(Key);
    }
    [UnityTearDown] public IEnumerator Cleanup() => CoreTestSupport.CleanupPlayMode();
    [UnityTest, Timeout(240000)] public IEnumerator AllEightStepsPersistAcrossRestartAndReset()
    {
        UnityEditor.SceneManagement.EditorSceneManager.NewScene(UnityEditor.SceneManagement.NewSceneSetup.EmptyScene);
        yield return new EnterPlayMode();
        yield return ExerciseTutorial();
    }

    private static IEnumerator ExerciseTutorial()
    {
        Application.runInBackground = true;
        yield return CoreTestSupport.LoadBunker();
        RunSelectionManager.Instance.SelectCharacter(UnityEditor.AssetDatabase.LoadAssetAtPath<CharacterData>(
            "Assets/_Project/Data/Characters/01_Gera.asset"));
        var starter = Object.FindFirstObjectByType<BunkerRunStarter>();
        starter.StartRun(starter.transform);
        yield return CoreTestSupport.Await(() => UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == "MVP" &&
            Object.FindFirstObjectByType<CharacterSpawner>()?.SpawnedPlayer != null && !SceneTransitionOverlay.IsTransitioning);
        yield return CoreTestSupport.Await(() => TutorialController.Active != null && TutorialController.Active.Player != null);
        Debug.Log("[TutorialTest] Production tutorial ready.");
        var controller = TutorialController.Active;
        Assert.That(RunStateManager.Instance.CurrentSector.SectorNumber, Is.EqualTo(0));
        Assert.That(RunStateManager.Instance.CompletedLevels, Is.Zero);
        Assert.That(controller, Is.Not.Null, "Fresh production sector must start TutorialController.");
        Assert.That(controller.Step, Is.EqualTo(TutorialStep.Movement));
        var seen = new List<TutorialStep> { controller.Step };
        controller.StepChanged += step => seen.Add(step);
        controller.StepChanged += step => Debug.Log("[TutorialTest] Step=" + step);
        yield return Capture("01-movement");
        // Standing still and merely gaining XP must not complete movement.
        yield return new WaitForSeconds(0.3f);
        Assert.That(controller.Step, Is.EqualTo(TutorialStep.Movement));
        Assert.That(RunFlowController.Instance.IsExitUnlocked, Is.False);
        ExperienceManager.Instance.AddExperience(ExperienceManager.Instance.ExpToNextLevel);
        Assert.That(ExperienceManager.Instance.CurrentLevel, Is.EqualTo(1), "Early exploration XP must wait for the tutorial pickup.");
        Assert.That(UpgradeManager.Instance.IsChoosingUpgrade, Is.False);
        var player = controller.Player;
        var sector = Object.FindFirstObjectByType<ProductionExplorationSectorController>();
        Assert.That(Object.FindFirstObjectByType<WorldEventSpawner>().SpawnedEvents.Count, Is.EqualTo(1));
        Assert.That(ProductionAnomalySite.ActiveSites, Is.Empty);
        Assert.That(sector.PortalPair, Is.Null);
        Assert.That(sector.PropCount, Is.Zero);
        var earlyChest = WorldLootChestSpawner.SpawnChest(sector.Config.WorldLootChestPrefab, (Vector2)player.transform.position + Vector2.up * 4f);
        Assert.That(earlyChest.CanInteract, Is.False, "Early chest must not consume the first weapon mount.");
        earlyChest.Interact();
        Assert.That(earlyChest.State, Is.EqualTo(WorldLootChest.ChestState.Closed));
        var movement = player.GetComponent<CharacterMovement2D>();
        movement.MovementIntent = () => Vector2.right;
        yield return CoreTestSupport.Await(() => controller.Step == TutorialStep.FirstEnemies);
        movement.MovementIntent = () => Vector2.zero;
        Assert.That(EnemyHealth.ActiveInstances.Count, Is.InRange(2, 3));
        yield return Capture("02-enemies");
        // Existing automatic weapon fire owns the kill. No direct health mutation.
        yield return CoreTestSupport.Await(() => controller.Step == TutorialStep.Experience);
        yield return Capture("03-xp");
        Assert.That(ExperienceManager.Instance.CurrentLevel, Is.EqualTo(1));
        yield return CoreTestSupport.Await(() => controller.FocusTarget != null);
        var pickup = controller.FocusTarget;
        movement.MovementIntent = () => pickup != null ? (Vector2)(pickup.position - player.transform.position).normalized : Vector2.zero;
        yield return CoreTestSupport.Await(() => controller.Step == TutorialStep.FirstReward && UpgradeManager.Instance.IsChoosingUpgrade);
        movement.MovementIntent = () => Vector2.zero;
        yield return Capture("04-reward");
        var choices = UpgradeManager.Instance.DebugCurrentChoices.Cast<OrbitalRewardData>().ToArray();
        Assert.That(choices, Is.Not.Empty);
        Assert.That(choices.All(reward => reward.RequiresArenaSelection &&
            OrbitalRewardProvider.GetModuleKind(reward.RewardKind).HasValue && reward.RewardKind != OrbitalRewardKind.LinkPair), Is.True);
        yield return ClickCard();
        Assert.That(controller.Step, Is.EqualTo(TutorialStep.OrbitalPlacement));
        yield return Capture("05-mount");
        // Cancellation must return to the same hand and not count as placement.
        controller.Station.RewardFlow.CancelForSceneTransition();
        Assert.That(controller.Step, Is.EqualTo(TutorialStep.FirstReward));
        yield return ClickCard();
        int modules = controller.Station.State.Modules.Count;
        Assert.That(controller.Station.RewardFlow.DebugChooseFirstValidTarget(), Is.True);
        yield return CoreTestSupport.Await(() => controller.Step == TutorialStep.SectorGoal);
        Assert.That(controller.Station.State.Modules.Count, Is.EqualTo(modules + 1));
        Assert.That(earlyChest.CanInteract, Is.False, "Level 0 must not introduce extra rewards.");
        Object.Destroy(earlyChest.gameObject);
        yield return Capture("06-sector");
        Assert.That(controller.TargetEvent, Is.Not.Null);
        var relay = controller.TargetEvent;
        var phases = new HashSet<OrbitalRelayPhase>();
        relay.PhaseChanged += phase => phases.Add(phase);
        player.GetComponent<Rigidbody2D>().position = relay.transform.position;
        movement.MovementIntent = () => relay != null
            ? OrbitalRelayBotSteering.GetDesiredMovement(relay, controller.Station, player.transform.position) : Vector2.zero;
        yield return CoreTestSupport.Await(() => controller.Step == TutorialStep.FirstEvent);
        yield return Capture("07-event");
        float deadline = Time.realtimeSinceStartup + 50f;
        yield return CoreTestSupport.Await(() => UpgradeManager.Instance.IsChoosingUpgrade);
        Assert.That(controller.GoalCompleted, Is.False, "Exit waits for the relay Upgrade queue.");
        yield return ClickCard();
        if (controller.Station.RewardFlow.PendingReward.HasValue)
            Assert.That(controller.Station.RewardFlow.DebugChooseFirstValidTarget(), Is.True);
        while (controller.Step != TutorialStep.Exit && Time.realtimeSinceStartup < deadline) yield return null;
        movement.MovementIntent = () => Vector2.zero;
        Assert.That(phases, Does.Contain(OrbitalRelayPhase.Stabilization));
        Assert.That(phases, Does.Contain(OrbitalRelayPhase.Transition));
        Assert.That(phases, Does.Contain(OrbitalRelayPhase.Bonus));
        Assert.That(controller.Step, Is.EqualTo(TutorialStep.Exit), "Actual orbital contacts, Bonus and resolved Upgrade must unlock exit.");
        Assert.That(RunFlowController.Instance.IsExitUnlocked, Is.True);
        Assert.That(RunStateManager.Instance.ThreatValue, Is.Zero);
        Assert.That(UpgradeManager.Instance.IsRewardQueueIdle, Is.True, "Relay Upgrade must be resolved before exit.");
        yield return Capture("08-exit");
        player.GetComponent<Rigidbody2D>().position = controller.FocusTarget.position;
        yield return CoreTestSupport.Await(() => PlayerPrefs.GetInt(Key, 0) == 1);
        Assert.That(seen.Distinct().ToArray(), Is.EqualTo(new[] {
            TutorialStep.Movement, TutorialStep.FirstEnemies, TutorialStep.Experience, TutorialStep.FirstReward,
            TutorialStep.OrbitalPlacement, TutorialStep.SectorGoal, TutorialStep.FirstEvent, TutorialStep.Exit, TutorialStep.Completed }));
        Assert.That(TutorialController.IsActive, Is.False);
        yield return CoreTestSupport.Await(() => RunStateManager.Instance.CurrentSector.SectorNumber == 1 && !SceneTransitionOverlay.IsTransitioning);
        Assert.That(RunStateManager.Instance.CompletedLevels, Is.Zero, "Tutorial is outside the three-sector route.");
        Assert.That(ExperienceManager.Instance.CurrentLevel, Is.EqualTo(2), "Only one tutorial XP reward is granted.");
        Assert.That(Object.FindFirstObjectByType<LevelChoiceManager>().IsChoosing, Is.False);
        Assert.That(TutorialController.Active, Is.Null, "Later sectors must not install tutorial.");
        yield return Restart();
        Assert.That(TutorialController.Active, Is.Null, "Completion must survive a new first-sector run.");
        Assert.That(RunStateManager.Instance.CurrentSector.SectorNumber, Is.EqualTo(1));
        TutorialController.ResetCompletion();
        yield return Restart();
        Assert.That(TutorialController.Active?.Step, Is.EqualTo(TutorialStep.Movement), "RESET must re-enable the next fresh run.");
        Assert.That(RunStateManager.Instance.CurrentSector.SectorNumber, Is.EqualTo(0));
        yield return Capture("09-reset");
    }

    private static IEnumerator Restart()
    {
        int run = RunStateManager.Instance.RunId;
        RunEndService.Instance.RestartRun(RunEndReason.ReturnedToBunker);
        yield return CoreTestSupport.Await(() => RunStateManager.Instance.RunId != run &&
            !SceneTransitionOverlay.IsTransitioning && Object.FindFirstObjectByType<CharacterSpawner>()?.SpawnedPlayer != null);
    }

    private static IEnumerator ClickCard()
    {
        var panel = Object.FindFirstObjectByType<UpgradePanelView>();
        Assert.That(panel, Is.Not.Null);
        var card = panel.GetComponentsInChildren<UpgradeCardView>().First();
        var button = card.GetComponent<Button>();
        yield return CoreTestSupport.Await(() => button.IsInteractable());
        button.onClick.Invoke();
    }

    private static IEnumerator Capture(string name)
    {
        // Camera/UI readback also works while the editor Game tab is not foreground.
        yield return null;
        if (name == "07-event") yield return new WaitForSecondsRealtime(0.5f);
        Canvas.ForceUpdateCanvases();
        var overlay = Object.FindFirstObjectByType<TutorialOverlay>();
        Assert.That(overlay, Is.Not.Null);
        Assert.That(overlay.canvasRenderer, Is.Not.Null, "Dim/frame/arrow need a real CanvasRenderer.");
        var mesh = overlay.canvasRenderer.GetMesh();
        Assert.That(mesh.vertexCount, Is.GreaterThan(0), "Tutorial focus overlay must draw geometry.");
        Directory.CreateDirectory("Artifacts/GeneratedQA/Tutorial");
        ScreenCapture.CaptureScreenshot("Artifacts/GeneratedQA/Tutorial/" + name + ".png");
        yield return null;
    }
}
#endif
