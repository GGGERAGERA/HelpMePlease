using System.Collections;
using UnityEngine;

public sealed class RunFlowController : MonoBehaviour
{
    public static RunFlowController Instance { get; private set; }

    [Header("Level Choice")]
    [SerializeField] private LevelChoiceManager levelChoiceManager;

    [Header("Boss Defeat Flow")]
    [SerializeField, Min(0f)] private float levelChoiceDelay = 5f;
    [SerializeField] private bool stopEnemySpawnerAfterBoss = true;

    [Header("Completion")]
    [SerializeField] private RunCompletionCleaner completionCleaner;

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

        if (sectorNumber == 10)
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

        if (sectorNumber > 10)
        {
            Debug.LogError(
                $"[RunFlowController] Invalid sector {sectorNumber}; " +
                "the main route ends at sector 10."
            );
            yield break;
        }

        runState.RegisterCompletedLevel();

        if (stopEnemySpawnerAfterBoss)
            StopEnemySpawner();

        if (completionCleaner != null)
            completionCleaner.ClearRemainingEnemies();
        else
            Debug.LogWarning(
                "[RunFlowController] RunCompletionCleaner is not assigned."
            );

        RunMessageService.Instance?.Show(RunMessageType.BossDefeated);

        yield return new WaitForSeconds(levelChoiceDelay);

        OpenLevelChoice();
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

    private void StopEnemySpawner()
    {
        EnemySpawner spawner = FindFirstObjectByType<EnemySpawner>();

        if (spawner == null)
            return;

        spawner.StopSpawning();
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

    private void ResolveLevelMechanics()
    {
        if (worldEventSpawner == null)
            worldEventSpawner = FindFirstObjectByType<WorldEventSpawner>();

        if (noDamageChallenge == null)
            noDamageChallenge = FindFirstObjectByType<NoDamageChallenge>();
    }
}
