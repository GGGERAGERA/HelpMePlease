using System.Collections;
using UnityEngine;
using Subject42.Combat.OrbitalStation;

public enum RunPhase { NormalSector, WaitingForRewards, FinalBossIntro, FinalBossCombat, Victory, Stopped }

public sealed class RunFlowController : MonoBehaviour
{
#if UNITY_EDITOR || DEVELOPMENT_BUILD
    public static event System.Action DebugVictoryConfirmed;
#endif
    public static RunFlowController Instance { get; private set; }
    public event System.Action ExitUnlocked;
    public event System.Action ExitReached;

    [Header("Level Choice")]
    [SerializeField] private LevelChoiceManager levelChoiceManager;

    [Header("Level Mechanics")]
    [SerializeField] private WorldEventSpawner worldEventSpawner;
    [SerializeField] private NoDamageChallenge noDamageChallenge;

    [Header("Final Boss Phase")]
    [SerializeField] private RunBossSpawner bossSpawner;
    [SerializeField] private CharacterSpawner characterSpawner;
    [SerializeField] private WorldHazardDirector worldHazardDirector;

    private EnemySpawner enemySpawner;
    private EnemyHealth finalBoss;
    private bool levelCompleted;
    private StageProfileData sectorProfile;
    private int runId;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
    internal void DebugBindRun(int id) => runId = id;
#endif
    public float SectorElapsedTime { get; private set; }
    public bool IsExitUnlocked { get; private set; }
    public float ExitMinimumTimeRemaining => sectorProfile != null
        ? Mathf.Max(0f, sectorProfile.ExitActivationTime - SectorElapsedTime) : 0f;
    public bool IsExitRecovering => enemySpawner != null && enemySpawner.IsRecoveringFromFirstAutomaticAssault;
    private bool CanUnlockExit => TutorialController.IsActive ? TutorialController.Active.GoalCompleted : sectorProfile != null &&
        SectorElapsedTime >= sectorProfile.ExitActivationTime &&
        ((enemySpawner != null && enemySpawner.HasRecoveredFromFirstAutomaticAssault) ||
         (SectorElapsedTime >= sectorProfile.ExitUnlockTimeout &&
          (enemySpawner == null || !enemySpawner.HasStartedFirstAutomaticAssault)));
    public RunPhase Phase { get; private set; }
    public EnemyHealth FinalBoss => finalBoss;
    public bool IsVictoryConfirmed => Phase == RunPhase.Victory;

    public void BindEnemySpawner(EnemySpawner spawner) => enemySpawner = spawner;

    public void InitializeSector(StageProfileData profile)
    {
        runId = RunStateManager.Instance.RunId;
        Phase = RunPhase.NormalSector;
        levelCompleted = false;
        finalBoss = null;
        sectorProfile = profile;
        SectorElapsedTime = 0f;
        IsExitUnlocked = false;
        TutorialController.Prepare(this);
        worldHazardDirector?.Initialize(profile != null ? profile.WorldHazards : null,
            this, characterSpawner, GameplayAreaService.Instance);
    }

    private void Update()
    {
        if (Phase == RunPhase.NormalSector && !levelCompleted)
        {
            SectorElapsedTime += Time.deltaTime;
            // Availability is permanent within this sector, including a timeout unlock.
            if (!IsExitUnlocked && CanUnlockExit)
            {
                IsExitUnlocked = true;
                ExitUnlocked?.Invoke();
            }
        }
    }

    private bool CanContinue => isActiveAndEnabled &&
        Phase != RunPhase.Stopped && Phase != RunPhase.Victory &&
        RunStateManager.Instance != null && RunStateManager.Instance.IsActiveRun(runId) &&
        characterSpawner != null && characterSpawner.SpawnedPlayer != null &&
        !characterSpawner.SpawnedPlayer.GetComponent<PlayerHealth>().IsDead &&
        characterSpawner.SpawnedPlayer.GetComponentInChildren<OrbitalStationRuntime>() is { IsInitialized: true };

    private bool RewardsResolved =>
        (UpgradeManager.Instance == null || UpgradeManager.Instance.IsRewardQueueIdle) &&
        characterSpawner.SpawnedPlayer.GetComponentInChildren<OrbitalStationRuntime>().InputOwner.CanTransition;

    public void StopRunGameplay()
    {
        worldHazardDirector?.Cancel();
        AudioService.Instance?.StopAllManagedLoops();
        StopAllCoroutines();
        if (Phase != RunPhase.Victory) Phase = RunPhase.Stopped;
        enemySpawner?.StopSpawning();
    }

    private void OnEnable() => RunStateManager.EnsureExists().RegisterSceneCleanup(ReleaseRunScene);
    private void OnDisable()
    {
        RunStateManager.Instance?.UnregisterSceneCleanup(ReleaseRunScene);
        StopRunGameplay();
    }

    private void ReleaseRunScene()
    {
        StopRunGameplay();
        Phase = RunPhase.Stopped;
        finalBoss = null;
        sectorProfile = null;
        SectorElapsedTime = 0f;
        IsExitUnlocked = false;
        noDamageChallenge?.CancelChallenge();
    }

    public bool IsLevelCompleted => levelCompleted;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public void HandleBossDefeated(EnemyHealth boss)
    {
        if (!CanContinue || Phase != RunPhase.FinalBossCombat ||
            boss == null || boss != finalBoss || !boss.IsDead)
            return;

        Phase = RunPhase.Victory;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        DebugVictoryConfirmed?.Invoke();
#endif
        StopRunGameplay();
        // Let EnemyHealth finish its death/loot callbacks before unloading the scene.
        StartCoroutine(CompleteVictory());
    }

    private IEnumerator CompleteVictory()
    {
        yield return null;
        if (RunStateManager.Instance != null && RunStateManager.Instance.IsActiveRun(runId))
            RunEndService.Instance?.CompleteRunVictory();
    }

    public bool HandleExitReached()
    {
        if (levelCompleted || Phase != RunPhase.NormalSector || !IsExitUnlocked ||
            RunStateManager.Instance == null || !RunStateManager.Instance.IsActiveRun(runId))
            return false;

        RunStateManager runState = RunStateManager.Instance;
        int sectorNumber = runState != null && runState.CurrentSector != null
            ? runState.CurrentSector.SectorNumber
            : 0;

        if (!RunRoute.IsExplorationSector(sectorNumber))
        {
            Debug.LogWarning(
                $"[RunFlowController] Exit ignored in sector {sectorNumber}."
            );
            return false;
        }

        if (RunRoute.IsFinalSector(sectorNumber))
        {
            if (!CanContinue || bossSpawner == null || enemySpawner == null ||
                RunEndService.Instance == null || !bossSpawner.CanSpawn(runState.CurrentSector))
            {
                Debug.LogError("[RunFlowController] Final boss phase dependencies are missing.", this);
                return false;
            }

            levelCompleted = true;
            Phase = RunPhase.WaitingForRewards;
            enemySpawner.StopSpawning();
            RegisterCurrentLevelCompletion();
            runState.RegisterCompletedLevel();
            StartCoroutine(FinalBossRoutine());
            return true;
        }

        LevelChoiceManager manager = ResolveLevelChoiceManager();

        if (manager == null)
        {
            Debug.LogError(
                "[RunFlowController] LevelChoiceManager not found."
            );
            return false;
        }

        levelCompleted = true;

        if (!manager.TryShowChoices())
        {
            levelCompleted = false;
            return false;
        }

        RegisterCurrentLevelCompletion();
        runState?.RegisterCompletedLevel();
        ExitReached?.Invoke();
        return true;
    }

    public void ApplyLevelMechanics()
    {
        ResolveLevelMechanics();
        worldEventSpawner?.SetHoldPointEnabled(false);
        noDamageChallenge?.CancelChallenge();
    }

    private IEnumerator FinalBossRoutine()
    {
        // Defer to the next frame so all callbacks from entering the zone finish
        // enqueueing rewards. Polling owns no callbacks in the reward manager.
        yield return null;
        while (CanContinue && !RewardsResolved) yield return null;
        if (!CanContinue) { StopRunGameplay(); yield break; }

        Phase = RunPhase.FinalBossIntro;
        RunMessageService.Instance?.Show(RunMessageType.BossIncoming);
        AudioService.Instance?.Play(AudioCueId.BossSpawn);
        CameraShake.Instance?.Shake(2f, 0.05f);

        float remaining = sectorProfile.BossPreparationDuration;
        while (CanContinue && (remaining > 0f || !RewardsResolved))
        {
            if (RewardsResolved) remaining -= Time.deltaTime;
            yield return null;
        }

        while (CanContinue)
        {
            if (RewardsResolved && bossSpawner.TrySpawn(
                RunStateManager.Instance.CurrentSector,
                characterSpawner.SpawnedPlayer.transform, out finalBoss))
            {
                Phase = RunPhase.FinalBossCombat;
                yield break;
            }
            // A temporarily blocked spawn area must not permanently lock the run.
            yield return new WaitForSeconds(0.5f);
        }
        StopRunGameplay();
    }

    private void RegisterCurrentLevelCompletion()
    {
        RunStateManager runState = RunStateManager.Instance;

        if (runState == null || runState.CurrentSector == null)
        {
            Debug.LogError(
                "[RunFlowController] CurrentSector is missing. " +
                "World Rule completion was not registered."
            );

            return;
        }

        WorldRuleData completedRule = runState.CurrentSector.WorldRule;
        if (runState.IsDevelopmentRun) return;

        if (completedRule == null)
        {
            Debug.Log(
                "[RunFlowController] No WorldRule unlock progress to register."
            );

            return;
        }

        if (UnlockProgressService.Instance == null)
        {
            Debug.LogWarning(
                "[RunFlowController] UnlockProgressService is missing."
            );

            return;
        }

        string modifierId = GetModifierUnlockId(completedRule);

        if (string.IsNullOrWhiteSpace(modifierId))
            return;

        Debug.Log(
            $"[RunFlowController] Register completion: " +
            $"type={UnlockConditionType.CompleteLevelModifier}, " +
            $"targetId='{modifierId}', " +
            $"worldRule='{completedRule.name}'"
        );
        UnlockProgressService.Instance.AddProgressByCondition(
            UnlockConditionType.CompleteLevelModifier,
            modifierId,
            1
        );

        Debug.Log(
            $"[RunFlowController] Completed level modifier: {modifierId}"
        );
    }

    private string GetModifierUnlockId(WorldRuleData rule)
    {
        if (rule != null)
        {
            switch (rule.RuleType)
            {
                case WorldRuleType.Darkness:
                case WorldRuleType.Rain:
                case WorldRuleType.Snow:
                    return rule.Id;

                case WorldRuleType.None:
                case WorldRuleType.Wind:
                case WorldRuleType.Golden:
                case WorldRuleType.Condensation:
                    return string.Empty;
            }
        }

        return string.Empty;
    }

    private void OpenLevelChoice()
    {
        LevelChoiceManager manager = ResolveLevelChoiceManager();

        if (manager == null)
        {
            Debug.LogError(
                "[RunFlowController] LevelChoiceManager not found."
            );

            return;
        }

        manager.ShowChoices();
    }

    private LevelChoiceManager ResolveLevelChoiceManager()
    {
        if (levelChoiceManager == null)
            levelChoiceManager = FindFirstObjectByType<LevelChoiceManager>();

        return levelChoiceManager;
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    public bool CanDebugCompleteCurrentLevel
    {
        get
        {
            RunStateManager runState = RunStateManager.Instance;
            LevelChoiceManager manager = ResolveLevelChoiceManager();

            if (levelCompleted || Phase != RunPhase.NormalSector || runState == null ||
                runState.CurrentSector == null)
            {
                return false;
            }

            int sectorNumber = runState.CurrentSector.SectorNumber;

            if (RunRoute.IsFinalSector(sectorNumber))
                return CanContinue && bossSpawner != null && enemySpawner != null;

            return RunRoute.IsExplorationSector(sectorNumber) &&
                manager != null && !manager.IsChoosing;
        }
    }

    public bool TryDebugCompleteCurrentLevel()
    {
        if (!CanDebugCompleteCurrentLevel)
            return false;

        return HandleExitReached();
    }

    public bool CanDebugOpenLevelChoice
    {
        get
        {
            RunStateManager runState = RunStateManager.Instance;
            LevelChoiceManager manager = ResolveLevelChoiceManager();
            return levelCompleted &&
                runState != null &&
                runState.CurrentSector != null &&
                RunRoute.HasNextSector(
                    runState.CurrentSector.SectorNumber
                ) &&
                manager != null &&
                !manager.IsChoosing;
        }
    }

    public bool TryDebugOpenLevelChoice()
    {
        if (!CanDebugOpenLevelChoice)
            return false;

        OpenLevelChoice();
        LevelChoiceManager manager = ResolveLevelChoiceManager();
        return manager != null && manager.IsChoosing;
    }
#endif

    private void ResolveLevelMechanics()
    {
        if (worldEventSpawner == null)
            worldEventSpawner = FindFirstObjectByType<WorldEventSpawner>();

        if (noDamageChallenge == null)
            noDamageChallenge = FindFirstObjectByType<NoDamageChallenge>();
    }
}
