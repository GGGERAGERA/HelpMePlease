#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using System.Linq;
using Subject42.Combat.OrbitalStation;
using UnityEngine;
using UnityEngine.SceneManagement;

// Full production route owned by the existing session, using the same controller, seed and telemetry.
public sealed partial class BotRunSession
{
    private void FixedUpdate()
    {
        if (goldenStage != "Gameplay" || controller == null || flow == null || rewards == null ||
            !rewards.IsRewardQueueIdle || Subject42DebugMenu.IsDebugMenuOpen || Time.timeScale <= 0 || SceneTransitionOverlay.IsTransitioning) return;
        controller.TickGoldenPath(Result.Duration - goldenSectorTime, flow.FinalBoss, goldenEvents, station.State);
    }
    private string goldenStage;
    private GoldenPathResult golden;
    private float goldenDeadline, goldenSectorTime, goldenChoiceAt, goldenCompletedAt;
    private int goldenSector, goldenRunId, rewardBodyLevel;
    private OrbitalRunState goldenSnapshot, rewardBefore;
    private UpgradeData chosenReward;
    private float rewardHealth, rewardMoveSpeed, rewardHealthBonus, rewardSpeedMultiplier;
    private bool directSelection, goldenVictory;
    private WorldEventSpawner goldenEvents;
    private readonly HashSet<EnemyHealth> goldenBosses = new();
    private readonly HashSet<object> goldenRewardTokens = new();
    private readonly List<Transform> goldenOldObjects = new();
    private readonly List<UnityEngine.Object> goldenSceneObjects = new();

    public bool CanStartGoldenPath => !IsRunning && !IsStarting && !SceneTransitionOverlay.IsTransitioning &&
        (FindFirstObjectByType<BunkerRunStarter>() != null && RunSelectionManager.Instance?.SelectedCharacter != null ||
         spawner != null && spawner.SpawnedCharacterData != null && RunEndService.Instance != null);

    public bool StartGoldenPath(int seed, float speed, float originalScale)
    {
        if (!CanStartGoldenPath || (speed != 1 && speed != 5 && speed != 10)) return false;
        restoreScale = originalScale;
        saveStandaloneResult = false;
        OutputPath = null;
        golden = new GoldenPathResult { Seed = seed };
        Result = new BotRunResult { Seed = seed, SimulationSpeed = speed, Strategy = "GoldenPath", GoldenPath = golden };
        IsStarting = BotEnabled = true;
        pendingException = null;
        goldenSector = 0;
        goldenSnapshot = rewardBefore = null;
        goldenVictory = false;
        goldenBosses.Clear(); goldenRewardTokens.Clear(); goldenOldObjects.Clear(); goldenSceneObjects.Clear();
        startedAt = Time.realtimeSinceStartup;
        EnemyHealth.Spawned += GoldenBossSpawned;
        RunFlowController.DebugVictoryConfirmed += GoldenVictoryConfirmed;
        if (FindFirstObjectByType<BunkerRunStarter>() != null) SetGoldenStage("Start Run");
        else
        {
            RunSelectionManager.Instance.SelectCharacter(spawner.SpawnedCharacterData);
            SetGoldenStage("Initial Bunker");
            RunEndService.Instance.ReturnToBunker();
        }
        return true;
    }

    private void SetGoldenStage(string value)
    {
        goldenStage = value;
        goldenDeadline = Time.realtimeSinceStartup + 45f;
        Debug.Log($"[Golden Path] seed={Result.Seed} sector={goldenSector} t={Result.Duration:F1} {value}");
    }

    private string GoldenState() => $"stage={goldenStage}; scene={SceneManager.GetActiveScene().name}; " +
        $"sector={RunStateManager.Instance?.CurrentSector?.SectorNumber}; ended={RunStateManager.Instance?.IsRunEnded}; " +
        $"phase={flow?.Phase}; rewardsIdle={rewards?.IsRewardQueueIdle}; selection={station?.RewardFlow?.CompactStatus}; " +
        $"hp={health?.CurrentHealth}; " + (station?.State?.ToCompactString(goldenSector) ?? "orbital=null");

    private bool GoldenCheck(string assertion, bool valid, string reason)
    {
        if (golden.Check(assertion, valid, goldenSector, Result.Duration, valid ? null : GoldenState(), reason)) return true;
        Finish(BotRunOutcome.GoldenPathFailed, assertion + ": " + reason);
        return false;
    }

    private void TickGoldenPath()
    {
        Result.WallDuration = Time.realtimeSinceStartup - startedAt;
        if (pendingException != null) { GoldenCheck("Runtime.NoException", false, pendingException); return; }
        if (Subject42DebugMenu.IsDebugMenuOpen)
        {
            goldenDeadline += Time.unscaledDeltaTime;
            if (goldenCompletedAt >= 0) goldenCompletedAt += Time.unscaledDeltaTime;
            controller?.Pause("Debug menu"); return;
        }
        if (goldenStage != "Gameplay" && Time.realtimeSinceStartup > goldenDeadline)
        { GoldenCheck("Progression.TransitionDeadline", false, "No progress for 45 real seconds"); return; }
        if (goldenStage == "Initial Bunker")
        {
            if (!SceneTransitionOverlay.IsTransitioning && FindFirstObjectByType<BunkerRunStarter>() != null) SetGoldenStage("Start Run");
            return;
        }
        if (goldenStage == "Start Run" || goldenStage == "Start Second Run")
        {
            if (SceneTransitionOverlay.IsTransitioning) return;
            var starter = FindFirstObjectByType<BunkerRunStarter>();
            if (starter == null) return;
            bool second = goldenStage == "Start Second Run";
            if (!second) { BotRunSeed.Begin(Result.Seed); ownsSeed = true; }
            spawner = null; flow = null; rewards = null; area = null;
            SetGoldenStage(second ? "Second Run Baseline" : "Sector Startup");
            starter.StartRun(starter.transform);
            return;
        }
        if (goldenStage == "Sector Startup" || goldenStage == "Second Run Baseline")
        {
            if (SceneTransitionOverlay.IsTransitioning || spawner == null || spawner.SpawnedPlayer == null ||
                rewards == null || area?.PlayableArea == null || flow == null) return;
            station = spawner.SpawnedPlayer.GetComponentInChildren<OrbitalStationRuntime>();
            if (station == null || !station.IsInitialized) return;
            health = spawner.SpawnedPlayer.GetComponent<PlayerHealth>();
            bool second = goldenStage == "Second Run Baseline";
            if (!GoldenCheck("Run.ActiveAfterStart", !RunStateManager.Instance.IsRunEnded, "Run ended at scene startup")) return;
            int sector = RunStateManager.Instance.CurrentSector?.SectorNumber ?? 0;
            if (!GoldenCheck("Run.SectorSequence", sector == (second ? 1 : goldenSector + 1) && sector <= 3, "Expected next production sector, never sector 4")) return;
            if (goldenSnapshot != null && !second && !CheckGoldenPersistence(goldenSnapshot)) return;
            if (goldenSector == 0 || second)
            {
                var expected = OrbitalRunState.CreateDefault(station.State.RunId);
                if (!GoldenCheck("Orbital.StartingBaseline", station.State.Rings.Count == expected.Rings.Count &&
                    station.State.Modules.Count == expected.Modules.Count && station.State.CoreState.Level == expected.CoreState.Level &&
                    station.State.Rings[0].MountCount == expected.Rings[0].MountCount &&
                    station.State.Modules[0].ModuleType == expected.Modules[0].ModuleType &&
                    station.State.LastProcessedPlayerLevel == 1 && RunStateManager.Instance.CompletedLevels == 0 &&
                    RunStateManager.Instance.PickedUpgrades.Count == 0 && rewards.IsRewardQueueIdle &&
                    !station.RewardFlow.PendingReward.HasValue && station.InputOwner.CanTransition,
                    "Fresh run differs from production baseline")) return;
                if (!GoldenCheck("Cleanup.CompleteBaseline", GoldenPathResult.BaselineMatches(station.State) &&
                    ExperienceManager.Instance.CurrentLevel == 1 && ExperienceManager.Instance.CurrentExp == 0 &&
                    Mathf.Approximately(health.CurrentHealth, health.MaxHealth), "Upgrade, XP or health snapshot leaked into new run")) return;
                if (second)
                {
                    if (!GoldenCheck("Cleanup.NewRunIdentity", station.State.RunId != goldenRunId && goldenOldObjects.All(t => t == null), "Old run identity or objects survived")) return;
                    if (!GoldenCheck("Cleanup.SecondRunBoss", flow.Phase == RunPhase.NormalSector && flow.FinalBoss == null &&
                        !EnemyHealth.ActiveInstances.Any(e => e != null && e.IsBoss), "Boss state survived restart")) return;
                    golden.SecondRunBaseline = true;
                    SetGoldenStage("Final Bunker");
                    RunEndService.Instance.ReturnToBunker();
                    return;
                }
                goldenRunId = station.State.RunId;
            }
            goldenSector = sector;
            Result.Sector = sector; Result.Scene = spawner.gameObject.scene.name; Result.Character = spawner.SpawnedCharacterData.name;
            if (!CheckGoldenObjects()) return;
            controller = new BotController(spawner.SpawnedPlayer.GetComponent<CharacterMovement2D>(), area);
            station.InputOwner.PrepareForExternalPause(); station.InputOwner.DebugSuppressPlayerInput = true;
            telemetry = new BotTelemetry(Result, health, ExperienceManager.Instance, rewards, station.Combat);
            rewards.DebugRewardCommitted += GoldenRewardCommitted;
            goldenEvents = FindFirstObjectByType<WorldEventSpawner>();
            if (goldenEvents != null) goldenEvents.EventCompleted += GoldenObjectiveCompleted;
            goldenSectorTime = Result.Duration;
            goldenCompletedAt = -1f;
            goldenSnapshot = CopyGoldenState(station.State);
            goldenSceneObjects.Add(station); goldenSceneObjects.Add(flow); goldenSceneObjects.Add(rewards);
            goldenOldObjects.AddRange(station.GetComponentsInChildren<Transform>(true));
            IsStarting = false; IsRunning = ownsSpeed = true;
            PhysicalCombatFeedbackRuntime.CancelHitStopForExternalTimeControl();
            Time.timeScale = Result.SimulationSpeed;
            goldenChoiceAt = 0;
            SetGoldenStage("Gameplay");
            return;
        }
        if (goldenStage == "Victory Bunker" || goldenStage == "Final Bunker")
        {
            if (SceneTransitionOverlay.IsTransitioning || FindFirstObjectByType<BunkerRunStarter>() == null) return;
            var run = RunStateManager.Instance;
            if (!GoldenCheck("Cleanup.BunkerRunState", run.IsRunEnded && run.CurrentSector == null && run.OrbitalStationState == null &&
                run.CurrentLevel == 1 && run.CompletedLevels == 0 && run.PickedUpgrades.Count == 0 && run.AccumulatedKills == 0 && run.AccumulatedRunTime == 0,
                "Active sector or ORBITAL state remains in bunker")) return;
            if (!GoldenCheck("Cleanup.SceneObjects", goldenOldObjects.All(t => t == null) && goldenSceneObjects.All(o => o == null) &&
                FindObjectsByType<OrbitalStationRuntime>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length == 0,
                "Previous rings, mounts, modules or scene controllers survived")) return;
            if (!GoldenCheck("Cleanup.RewardsAndBoss", UpgradeManager.Instance == null && RunFlowController.Instance == null &&
                !EnemyHealth.ActiveInstances.Any(e => e != null && e.IsBoss) &&
                FindObjectsByType<OrbitalRewardFlowController>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length == 0,
                "Reward queue, selection or boss survived scene cleanup")) return;
            golden.BunkerClean = true;
            if (goldenStage == "Victory Bunker") { SetGoldenStage("Start Second Run"); return; }
            if (!GoldenCheck("GoldenPath.Complete", golden.SectorsCompleted == 3 && golden.BossKilled && golden.VictoryCalls == 1 &&
                golden.BossSpawns == 1 && golden.RewardsTaken > 0 && golden.DirectMountSelections > 0 &&
                golden.ObjectivesCompleted >= 3 && golden.SecondRunBaseline, "Required route milestone missing")) return;
            Finish(BotRunOutcome.GoldenPathPassed, "Three sectors, boss victory, bunker cleanup and second run verified");
            return;
        }
        if (goldenStage != "Gameplay") return;
        if (goldenVictory)
        {
            CaptureGoldenFinal(); ReleaseGoldenScene(); SetGoldenStage("Victory Bunker"); return;
        }
        if (!GoldenCheck("Run.SceneStillPresent", health != null && flow != null && station != null, "Unexpected scene unload")) return;
        telemetry.Sample(Time.deltaTime);
        if (!GoldenCheck("Run.PlayerAlive", !health.IsDead, "Player died during regression route")) return;
        if (!GoldenCheck("Run.NoSector4", RunStateManager.Instance.CurrentSector?.SectorNumber == goldenSector && goldenSector <= 3, "Unexpected sector transition")) return;
        if (!CheckGoldenObjects()) return;
        if (!CheckGoldenPersistence(goldenSnapshot)) return;
        goldenSnapshot = CopyGoldenState(station.State);
        if (flow.IsLevelCompleted && golden.SectorsCompleted < goldenSector)
        {
            if (!GoldenCheck("Run.CompletedSectorOnce", RunStateManager.Instance.CompletedLevels == goldenSector, "Completion counter mismatch")) return;
            golden.SectorsCompleted = goldenSector;
            goldenCompletedAt = Time.realtimeSinceStartup;
        }
        if (flow.Phase == RunPhase.FinalBossIntro || flow.Phase == RunPhase.FinalBossCombat)
        {
            if (!GoldenCheck("Boss.AfterSector3Only", goldenSector == 3 && golden.SectorsCompleted == 3, "Premature finale")) return;
            if (flow.Phase == RunPhase.FinalBossCombat && !GoldenCheck("Boss.Exists", flow.FinalBoss != null && golden.BossSpawns == 1, "Missing or duplicated boss")) return;
        }
        if (!rewards.IsRewardQueueIdle)
        {
            controller.Pause("Reward " + station.RewardFlow.CompactStatus);
            if (Time.realtimeSinceStartup > goldenDeadline) { GoldenCheck("Rewards.QueueDeadline", false, "Reward queue stalled for 45 real seconds"); return; }
            if (Time.unscaledTime < goldenChoiceAt) return;
            goldenChoiceAt = Time.unscaledTime + .1f;
            if (station.RewardFlow.State == OrbitalRewardFlowState.DirectMountSelection) directSelection = true;
            if (station.RewardFlow.PendingReward.HasValue) station.RewardFlow.DebugChooseFirstValidTarget();
            else if (rewards.DebugCurrentChoices is { Count: > 0 })
            {
                chosenReward = rewards.DebugCurrentChoices.OrderByDescending(GoldenRewardPriority).ThenBy(r => r.name, StringComparer.Ordinal).First();
                rewardBefore = CopyGoldenState(station.State); rewardHealth = health.MaxHealth;
                rewardHealthBonus = health.RunUpgradeMaxHealthBonus;
                rewardMoveSpeed = spawner.SpawnedPlayer.GetComponent<CharacterMovement2D>().speed;
                rewardSpeedMultiplier = spawner.SpawnedPlayer.GetComponent<CharacterMovement2D>().RunUpgradeMoveSpeedMultiplier;
                rewardBodyLevel = chosenReward is OrbitalRewardData body && body.BodyUpgrade != null
                    ? RunStateManager.Instance.ItemSlots.GetLevel(body.BodyUpgrade) : 0;
                int choice = rewards.DebugCurrentChoices.ToList().IndexOf(chosenReward);
                rewards.DebugSelectCurrentChoice(choice);
            }
            return;
        }
        goldenDeadline = Time.realtimeSinceStartup + 45f;
        if (!GoldenCheck("Rewards.SelectionReleased", !station.RewardFlow.PendingReward.HasValue && station.InputOwner.CanTransition, "Queue idle with unfinished arena selection")) return;
        if (flow.IsLevelCompleted && goldenSector < 3)
        {
            if (!GoldenCheck("Progression.SectorChoiceDeadline", Time.realtimeSinceStartup - goldenCompletedAt < 45f, "Sector choices did not become available")) return;
            var choices = FindFirstObjectByType<LevelChoiceManager>();
            if (choices == null || !choices.IsChoosing) return;
            if (!GoldenCheck("Rewards.TransitionOnlyWhenResolved", rewards.IsRewardQueueIdle && station.InputOwner.CanTransition, "Unfinished reward at sector transition")) return;
            goldenSnapshot = CopyGoldenState(station.State);
            ReleaseGoldenScene();
            SetGoldenStage("Sector Startup");
            choices.DebugSelectFirstRule();
            return;
        }
        if (!GoldenCheck("Progression.SectorDeadline", Result.Duration - goldenSectorTime < 600f, "Sector or boss exceeded 600 simulation seconds")) return;
        controller.ObserveGoldenProjectiles();
    }

    private static OrbitalRunState CopyGoldenState(OrbitalRunState state) => JsonUtility.FromJson<OrbitalRunState>(JsonUtility.ToJson(state));
    private bool CheckGoldenPersistence(OrbitalRunState before)
    {
        var now = station.State;
        return GoldenCheck("Orbital.StableRunId", now.RunId == before.RunId, "Run identity changed across sector") &&
            GoldenCheck("Orbital.RingsAndMountsPersist", before.Rings.All(r => now.FindRing(r.StableRingId) is var n && n != null && n.MountCount >= r.MountCount), "Ring ID or built mount disappeared") &&
            GoldenCheck("Orbital.RingUpgradesPersist", before.Rings.All(r => now.FindRing(r.StableRingId) is var n && n != null &&
                n.MountCapacity >= r.MountCapacity && n.PowerMultiplier >= r.PowerMultiplier && n.PowerUpgradeLevel >= r.PowerUpgradeLevel &&
                n.SpeedUpgradeLevel >= r.SpeedUpgradeLevel && n.MountUpgradeLevel >= r.MountUpgradeLevel && n.VisualUpgradeLevel >= r.VisualUpgradeLevel), "Ring upgrades reset") &&
            GoldenCheck("Orbital.CoreUpgradesPersist", now.CoreState.Level >= before.CoreState.Level &&
                now.CoreState.PulseUpgradeLevel >= before.CoreState.PulseUpgradeLevel && now.CoreState.CascadeUpgradeLevel >= before.CoreState.CascadeUpgradeLevel &&
                now.CoreState.LinkMatrixUpgradeLevel >= before.CoreState.LinkMatrixUpgradeLevel && now.CoreState.DamageMultiplier >= before.CoreState.DamageMultiplier &&
                now.CoreState.CooldownMultiplier <= before.CoreState.CooldownMultiplier, "Core upgrades reset") &&
            GoldenCheck("Orbital.ModulesNeverOverwritten", before.Modules.All(m => now.FindModule(m.StableModuleId) is var n && n != null &&
                n.ModuleType == m.ModuleType && n.StableRingId == m.StableRingId && n.MountIndex == m.MountIndex && n.DamageLevel >= m.DamageLevel), "Module removed, replaced or moved unexpectedly");
    }

    private bool CheckGoldenObjects()
    {
        return GoldenCheck("Orbital.CoreExists", station.Core != null, "Core missing") &&
            GoldenCheck("Orbital.ValidState", station.State.Validate(out string error), error) &&
            GoldenCheck("Orbital.RuntimeMatchesState", station.Rings.Count == station.State.Rings.Count && station.Modules.Count == station.State.Modules.Count &&
                station.Rings.All(r => r.Mounts.Count == r.State.MountCount && r.Mounts.All(m => m.Transform != null)) &&
                station.Modules.All(m => station.State.FindModule(m.StableModuleId) != null && m.CurrentMount?.Transform != null), "Runtime objects differ from persisted counts/IDs");
    }

    private int GoldenRewardPriority(UpgradeData data) => data is OrbitalRewardData r ? r.RewardKind switch
    {
        OrbitalRewardKind.MaxHealth when health.CurrentHealth < health.MaxHealth * .7f => 120,
        OrbitalRewardKind.NewRing => 100, OrbitalRewardKind.Pistol => 95,
        OrbitalRewardKind.ArcEmitter => 92, OrbitalRewardKind.ImpulseGun => 90, OrbitalRewardKind.LaserSword => 85,
        OrbitalRewardKind.AddMount => 80, OrbitalRewardKind.RingPower => 70, OrbitalRewardKind.ModuleDamage => 65,
        OrbitalRewardKind.CoreUpgrade => 60, OrbitalRewardKind.MaxHealth => 55, _ => 10
    } : 0;

    private void GoldenRewardCommitted(UpgradeData reward)
    {
        object request = rewards.DebugCurrentRewardRequest;
        if (!GoldenCheck("Rewards.GrantedOnce", request != null && goldenRewardTokens.Add(request), "Reward queue request committed twice or missing")) return;
        bool applied = reward is OrbitalRewardData orbital && rewardBefore != null && (orbital.RewardKind switch
        {
            OrbitalRewardKind.MaxHealth => BodyRewardLevelIsExact(orbital) && Mathf.Approximately(health.MaxHealth,
                rewardHealth + ProductionUpgradeProfiles.MaxHealthBonus(rewardBodyLevel + 1) - rewardHealthBonus) && station.State.Revision == rewardBefore.Revision,
            OrbitalRewardKind.MoveSpeed => BodyRewardLevelIsExact(orbital) && Mathf.Approximately(spawner.SpawnedPlayer.GetComponent<CharacterMovement2D>().speed,
                rewardMoveSpeed * ProductionUpgradeProfiles.MoveSpeedMultiplier(rewardBodyLevel + 1) / rewardSpeedMultiplier) && station.State.Revision == rewardBefore.Revision,
            _ => GoldenPathResult.RewardDeltaIsExact(rewardBefore, station.State, orbital.RewardKind)
        });
        if (!GoldenCheck("Rewards.ChosenRewardApplied", reward == chosenReward && applied, "Chosen reward did not apply exactly once")) return;
        if (!CheckGoldenPersistence(rewardBefore)) return;
        if (directSelection)
        {
            if (!GoldenCheck("Rewards.DirectMountSelectionCompleted", station.State.Modules.Count > rewardBefore.Modules.Count &&
                station.RewardFlow.State == OrbitalRewardFlowState.Completed, "Direct mount did not install a module and complete")) return;
            golden.DirectMountSelections++; directSelection = false;
        }
        golden.RewardsTaken++;
        goldenDeadline = Time.realtimeSinceStartup + 45;
        chosenReward = null; rewardBefore = null;
    }
    private void GoldenObjectiveCompleted(WorldEvent completed)
    { golden.ObjectivesCompleted++; controller?.OnObjectiveCompleted(); }
    private bool BodyRewardLevelIsExact(OrbitalRewardData reward) => reward.BodyUpgrade != null &&
        RunStateManager.Instance.ItemSlots.GetLevel(reward.BodyUpgrade) == rewardBodyLevel + 1;
    private void GoldenBossSpawned(EnemyHealth boss)
    {
        if (!boss.IsBoss) return;
        goldenBosses.Add(boss); golden.BossSpawns++;
        boss.OnDied += GoldenBossDied;
        GoldenCheck("Boss.SpawnOnceAfterSector3", goldenSector == 3 && golden.BossSpawns == 1, "Boss spawned early or more than once");
    }
    private void GoldenBossDied(EnemyHealth boss)
    {
        if (!GoldenCheck("Boss.DeathRegistered", boss.IsDead && goldenBosses.Contains(boss), "Unknown or living boss death")) return;
        golden.BossKilled = true;
    }
    private void GoldenVictoryConfirmed()
    {
        golden.VictoryCalls++;
        goldenVictory = true;
        GoldenCheck("Boss.VictoryOnce", golden.VictoryCalls == 1 && goldenSector == 3, "Repeated or premature victory");
    }
    private void CaptureGoldenFinal()
    {
        golden.FinalRingCount = station.State.Rings.Count;
        golden.FinalModuleCount = station.State.Modules.Count;
        Result.FinalOrbital = CopyGoldenState(station.State);
    }
    private void ReleaseGoldenScene()
    {
        controller?.Dispose(); controller = null;
        telemetry?.Dispose(); telemetry = null;
        if (rewards != null) rewards.DebugRewardCommitted -= GoldenRewardCommitted;
        if (goldenEvents != null) goldenEvents.EventCompleted -= GoldenObjectiveCompleted;
        if (station != null) station.InputOwner.DebugSuppressPlayerInput = false;
    }
    private void EndGoldenPath(BotRunOutcome outcome, string reason)
    {
        if (Result.FinalOrbital == null && station != null && station.IsInitialized) CaptureGoldenFinal();
        if (outcome != BotRunOutcome.GoldenPathPassed && golden.AssertionsFailed == 0)
            golden.Check("Runner.Completed", false, goldenSector, Result.Duration, GoldenState(), reason);
        golden.Result = outcome == BotRunOutcome.GoldenPathPassed && golden.AssertionsFailed == 0 ? "PASS" : "FAIL";
        golden.Duration = Result.Duration;
        ReleaseGoldenScene();
        EnemyHealth.Spawned -= GoldenBossSpawned;
        RunFlowController.DebugVictoryConfirmed -= GoldenVictoryConfirmed;
        foreach (var boss in goldenBosses) if (boss != null) boss.OnDied -= GoldenBossDied;
        goldenStage = null;
        // Keep the first run's orbital snapshot; second-run verification has its own fresh state.
        station = null;
        Debug.Log($"[Golden Path] {golden.Result} seed={golden.Seed} sectors={golden.SectorsCompleted} rewards={golden.RewardsTaken} boss={golden.BossKilled} assertions={golden.AssertionsPassed}/{golden.AssertionsFailed}");
    }
}
#endif

