using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Subject42.Combat.OrbitalStation;
using UnityEngine;

public enum TutorialStep { Movement, FirstEnemies, Experience, FirstReward, OrbitalPlacement, SectorGoal, FirstEvent, Exit, Completed }

// Guides Level 0 through the ordinary combat, relay and reward owners.
public sealed class TutorialController : MonoBehaviour
{
    public const string CompletionKey = "Subject42.Tutorial.Completed";
    public static TutorialController Active { get; private set; }
    public static bool IsActive => Active != null && Active.isActiveAndEnabled && Active.Step != TutorialStep.Completed;
    public static bool IsTutorialSector => RunStateManager.Instance?.CurrentSector?.SectorNumber == RunRoute.TutorialSector;
    public static bool NeedsTutorial
    {
        get
        {
            if (RunDevelopmentOverrides.SuppressTutorial?.Invoke() == true) return false;
            return PlayerPrefs.GetInt(CompletionKey, 0) == 0;
        }
    }
    public static bool ShouldStart => IsTutorialSector && RunStateManager.Instance.IsActiveRun(RunStateManager.Instance.RunId);

    public TutorialStep Step { get; private set; } = TutorialStep.Movement;
    public event Action<TutorialStep> StepChanged;
    public Transform FocusTarget { get; private set; }
    public OrbitalRelayEvent TargetEvent { get; private set; }
    public bool GoalCompleted { get; private set; }
    public bool NeedsFirstWeapon => Step <= TutorialStep.OrbitalPlacement;
    public GameObject Player { get; private set; }
    public OrbitalStationRuntime Station { get; private set; }
    public UpgradePanelView RewardPanel { get; private set; }
    public OrbitalModuleKind? PlacementKind { get; private set; }

    private CharacterSpawner characters;
    private CharacterMovement2D movement;
    private EnemySpawner enemies;
    private WorldEventSpawner events;
    private RunFlowController flow;
    private UpgradeManager rewards;
    private ProductionSectorExit sectorExit;
    private TutorialOverlayPresenter overlay;
    public void SetHintVisible(bool visible)
    {
        if (overlay != null) overlay.SetVisible(visible);
    }
    private readonly List<EnemyHealth> firstEnemies = new();
    private float travelled;
    private Coroutine relayRetry;
    public void ConfigureTarget(OrbitalRelayEvent target)
    {
        if (TargetEvent != null) TargetEvent.PlayerEntered -= OnZoneEntered;
        TargetEvent = target;
        if (TargetEvent != null) TargetEvent.PlayerEntered += OnZoneEntered;
    }

    public static void Prepare(RunFlowController flow)
    {
        if (!ShouldStart || Active != null) return;
        var controller = flow.gameObject.AddComponent<TutorialController>();
        controller.flow = flow;
    }

    private void Awake() => Active = this;

    private void Start()
    {
        characters = FindFirstObjectByType<CharacterSpawner>();
        enemies = FindFirstObjectByType<EnemySpawner>();
        events = FindFirstObjectByType<WorldEventSpawner>();
        rewards = UpgradeManager.Instance;
        sectorExit = FindFirstObjectByType<ProductionSectorExit>();
        if (characters == null || enemies == null || events == null || rewards == null || sectorExit == null)
        {
            Debug.LogError("[Tutorial] Production dependencies missing; guidance disabled.", this);
            enabled = false;
            return;
        }
        characters.CharacterSpawned += BindPlayer;
        if (characters.SpawnedPlayer != null) BindPlayer(characters.SpawnedPlayer);
        if (!enabled) return;
        ConfigureTarget(TargetEvent != null ? TargetEvent : events.SpawnedEvents.OfType<OrbitalRelayEvent>()
            .OrderBy(e => ((Vector2)e.transform.position - (Vector2)(Player != null ? Player.transform.position : Vector3.zero)).sqrMagnitude)
            .FirstOrDefault());
        if (TargetEvent == null)
        {
            Debug.LogError("[Tutorial] Authored Orbital Relay is missing.", this);
            enabled = false;
            return;
        }
        events.EventStarted += OnEventStarted;
        events.EventCompleted += OnEventCompleted;
        events.EventFailed += OnEventFailed;
        rewards.RewardOpened += OnRewardOpened;
        rewards.RewardChosen += OnRewardChosen;
        rewards.RewardCommitted += OnRewardCommitted;
        ExperiencePickup.Spawned += OnPickupSpawned;
        ExperiencePickup.Collected += OnPickupCollected;
        flow.ExitUnlocked += OnExitUnlocked;
        flow.ExitReached += OnExitReached;
        overlay = flow.TutorialPresentation;
        overlay.Bind(this);
        FocusTarget = Player != null ? Player.transform : null;
    }

    private void BindPlayer(GameObject player)
    {
        if (movement != null) movement.Travelled -= OnTravelled;
        Player = player;
        movement = player.GetComponent<CharacterMovement2D>();
        Station = player.GetComponentInChildren<OrbitalStationRuntime>();
        // Production starts with one occupied mount. Prepare exactly one free tutorial target
        // through the existing transaction; never mutate the normal starting state/config.
        if (Station != null && Station.IsInitialized && Station.State.FreeBuiltMounts == 0)
        {
            var ring = Station.Rings.FirstOrDefault(r => r.State.MountCount < r.State.MountCapacity);
            if (ring == null || !Station.AddMount(ring.RingId, out _))
            {
                Debug.LogError("[Tutorial] Could not prepare a free first-reward mount.", this);
                enabled = false;
                return;
            }
        }
        if (movement != null) movement.Travelled += OnTravelled;
        if (Step == TutorialStep.Movement) FocusTarget = player.transform;
    }

    private void SetStep(TutorialStep step, Transform target = null)
    {
        Step = step;
        FocusTarget = target;
        StepChanged?.Invoke(step);
    }

    private void OnTravelled(float distance)
    {
        if (Step != TutorialStep.Movement) return;
        travelled += distance;
        if (travelled < 2.5f) return;
        SetStep(TutorialStep.FirstEnemies);
        StartCoroutine(SpawnFirstEnemies());
    }

    private IEnumerator SpawnFirstEnemies()
    {
        while (firstEnemies.Count < 3 && Step == TutorialStep.FirstEnemies)
        {
            var enemy = enemies.SpawnTutorialEnemy(Player.transform.position);
            if (enemy == null)
            {
                // A temporarily blocked spawn point must not disable the tutorial.
                yield return new WaitForSeconds(0.5f);
                continue;
            }
            firstEnemies.Add(enemy);
            enemy.OnDied += OnEnemyDied;
            if (FocusTarget == null) FocusTarget = enemy.transform;
        }
    }

    private void OnEnemyDied(EnemyHealth enemy)
    {
        if (Step == TutorialStep.FirstEnemies) SetStep(TutorialStep.Experience);
    }

    private void OnPickupSpawned(ExperiencePickup pickup)
    {
        if (Step != TutorialStep.Experience || Player == null) return;
        if (FocusTarget == null || (pickup.transform.position - Player.transform.position).sqrMagnitude <
            (FocusTarget.position - Player.transform.position).sqrMagnitude) FocusTarget = pickup.transform;
    }

    private void OnPickupCollected(ExperiencePickup pickup)
    {
        if (Step == TutorialStep.Experience) SetStep(TutorialStep.FirstReward);
    }

    private void OnRewardOpened(UpgradePanelView panel)
    {
        // Escape from placement uses the same hand and returns to the same tutorial step.
        RewardPanel = panel;
        if (Step is TutorialStep.FirstReward or TutorialStep.OrbitalPlacement)
            SetStep(TutorialStep.FirstReward);
    }

    private void OnRewardChosen(UpgradeData reward)
    {
        if (Step != TutorialStep.FirstReward || reward is not OrbitalRewardData orbital) return;
        PlacementKind = OrbitalRewardProvider.GetModuleKind(orbital.RewardKind);
        if (PlacementKind.HasValue && orbital.RequiresArenaSelection) SetStep(TutorialStep.OrbitalPlacement);
    }

    private void OnRewardCommitted(UpgradeData reward)
    {
        if (Step != TutorialStep.OrbitalPlacement) return;
        SetStep(TutorialStep.SectorGoal, TargetEvent.transform);
        if (TargetEvent.IsPlayerInStartZone) OnZoneEntered();
    }

    public bool CanStartEvent(WorldEvent candidate) => candidate == TargetEvent && Step >= TutorialStep.SectorGoal;

    private void OnZoneEntered()
    {
        if (Step == TutorialStep.SectorGoal && TargetEvent != null)
            events.TryStartProductionEvent(TargetEvent);
    }

    private void OnEventStarted(WorldEvent started)
    {
        if (started is IWorldEventObjectiveProvider provider)
        {
            var guidance = provider.GetObjectiveGuidance();
            RunMessageService.Instance?.ShowCustom(guidance.TitleKey, guidance.DescriptionKey);
        }
        if (started == TargetEvent && Step == TutorialStep.SectorGoal)
            SetStep(TutorialStep.FirstEvent, started.transform);
    }

    private void OnEventCompleted(WorldEvent completed)
    {
        if (completed != TargetEvent || Step != TutorialStep.FirstEvent) return;
        rewards.RunWhenRewardQueueIsIdle(() =>
        {
            if (this != null && isActiveAndEnabled && TargetEvent == completed && Step == TutorialStep.FirstEvent)
                GoalCompleted = true;
        });
    }

    private void OnEventFailed(WorldEvent failed)
    {
        if (failed != TargetEvent || Step != TutorialStep.FirstEvent || relayRetry != null) return;
        GoalCompleted = false;
        SetStep(TutorialStep.SectorGoal);
        RunMessageService.Instance?.ShowCustom("event.relay.failed", "event.relay.retry");
        relayRetry = StartCoroutine(RetryRelay());
    }

    private IEnumerator RetryRelay()
    {
        yield return new WaitForSeconds(2f);
        var sector = FindFirstObjectByType<ProductionExplorationSectorController>();
        if (isActiveAndEnabled && Step == TutorialStep.SectorGoal && sector != null && sector.TryRespawnTutorialRelay(out var target))
        {
            ConfigureTarget(target);
            FocusTarget = target.transform;
        }
        relayRetry = null;
    }

    private void LateUpdate()
    {
        if (TargetEvent == null || GoalCompleted || relayRetry != null) return;
        if (Step == TutorialStep.SectorGoal && TargetEvent.IsPlayerInStartZone) OnZoneEntered();
        if (Step == TutorialStep.FirstEvent)
            FocusTarget = TargetEvent.ActiveNode != null ? TargetEvent.ActiveNode.transform : TargetEvent.transform;
    }

    private void OnExitUnlocked()
    {
        if (GoalCompleted) SetStep(TutorialStep.Exit, sectorExit.transform);
    }

    private void OnExitReached()
    {
        if (Step != TutorialStep.Exit || !GoalCompleted) return;
        PlayerPrefs.SetInt(CompletionKey, 1);
        PlayerPrefs.Save();
        SetStep(TutorialStep.Completed);
        enabled = false;
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    public static void ResetCompletion()
    {
        PlayerPrefs.DeleteKey(CompletionKey);
        PlayerPrefs.Save();
        Debug.Log("[Tutorial] RESET TUTORIAL — start a fresh run to replay.");
    }
#endif

    private void OnDisable()
    {
        StopAllCoroutines(); relayRetry = null;
        if (characters != null) characters.CharacterSpawned -= BindPlayer;
        if (movement != null) movement.Travelled -= OnTravelled;
        if (TargetEvent != null) TargetEvent.PlayerEntered -= OnZoneEntered;
        foreach (var enemy in firstEnemies) if (enemy != null) enemy.OnDied -= OnEnemyDied;
        ExperiencePickup.Spawned -= OnPickupSpawned;
        ExperiencePickup.Collected -= OnPickupCollected;
        if (events != null) { events.EventStarted -= OnEventStarted; events.EventCompleted -= OnEventCompleted; events.EventFailed -= OnEventFailed; }
        if (rewards != null)
        {
            rewards.RewardOpened -= OnRewardOpened;
            rewards.RewardChosen -= OnRewardChosen;
            rewards.RewardCommitted -= OnRewardCommitted;
        }
        if (flow != null) { flow.ExitUnlocked -= OnExitUnlocked; flow.ExitReached -= OnExitReached; }
        if (overlay != null) overlay.Bind(null);
        if (Active == this) Active = null;
    }
}
