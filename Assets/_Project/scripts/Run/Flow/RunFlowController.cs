using System.Collections;
using UnityEngine;

public sealed class RunFlowController : MonoBehaviour
{
    public static RunFlowController Instance { get; private set; }

    [Header("Level Choice")]
    [SerializeField] private LevelChoiceManager levelChoiceManager;

    [Header("Level Mechanics")]
    [SerializeField] private WorldEventSpawner worldEventSpawner;
    [SerializeField] private NoDamageChallenge noDamageChallenge;

    private bool levelCompleted;

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

    /// <summary>
    /// Вызывается ровно один раз после смерти босса.
    /// </summary>
    public void HandleBossDefeated()
    {
        if (levelCompleted)
            return;

        levelCompleted = true;

        StartCoroutine(BossDefeatedRoutine());
    }

    public bool HandleExitReached()
    {
        if (levelCompleted)
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
        return true;
    }

    public void ApplyLevelMechanics()
    {
        ResolveLevelMechanics();
        worldEventSpawner?.SetHoldPointEnabled(false);
        noDamageChallenge?.CancelChallenge();
    }

    private IEnumerator BossDefeatedRoutine()
    {
        RunStateManager runState = RunStateManager.Instance;

        if (runState == null || runState.CurrentSector == null)
        {
            Debug.LogError(
                "[RunFlowController] CurrentSector is missing after boss defeat."
            );
            yield break;
        }

        RegisterCurrentLevelCompletion();

        int sectorNumber = runState.CurrentSector.SectorNumber;

        if (RunRoute.IsBossSector(sectorNumber))
        {
            RunEndService endService = RunEndService.Instance;

            if (endService == null)
            {
                Debug.LogError(
                    "[RunFlowController] RunEndService is missing. " +
                    "Victory cannot be completed."
                );
                yield break;
            }

            endService.CompleteRunVictory();
            yield break;
        }

        Debug.LogError(
            $"[RunFlowController] Boss defeat is only valid in sector " +
            $"{RunRoute.FinalBossSector}; current sector is {sectorNumber}."
        );
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

            if (levelCompleted || runState == null ||
                runState.CurrentSector == null)
            {
                return false;
            }

            int sectorNumber = runState.CurrentSector.SectorNumber;

            if (RunRoute.IsBossSector(sectorNumber))
                return RunEndService.Instance != null;

            return RunRoute.IsExplorationSector(sectorNumber) &&
                manager != null && !manager.IsChoosing;
        }
    }

    public bool TryDebugCompleteCurrentLevel()
    {
        if (!CanDebugCompleteCurrentLevel)
            return false;

        RunStateManager runState = RunStateManager.Instance;
        int sectorNumber = runState.CurrentSector.SectorNumber;

        if (RunRoute.IsExplorationSector(sectorNumber))
            return HandleExitReached();

        HandleBossDefeated();
        return levelCompleted;
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
                RunRoute.IsExplorationSector(
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
