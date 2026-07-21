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
    [SerializeField] private WorldAccelerationRule worldAccelerationRule;
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

    public void ApplyLevelMechanics(LevelNodeData node)
    {
        ResolveLevelMechanics();

        bool holdPointEnabled = node != null && node.hasHoldZoneEvent;
        worldEventSpawner?.SetHoldPointEnabled(holdPointEnabled);

        if (node != null && node.hasWorldAccelerationRule)
            worldAccelerationRule?.StartRule();
        else
            worldAccelerationRule?.StopRule();

        if (node != null && node.hasNoDamageChallenge)
            noDamageChallenge?.StartChallenge();
        else
            noDamageChallenge?.CancelChallenge();
    }

    private IEnumerator BossDefeatedRoutine()
    {
        RegisterCurrentLevelCompletion();
        RunStateManager.Instance?.RegisterCompletedLevel();

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

        if (runState == null)
        {
            Debug.LogWarning(
                "[RunFlowController] RunStateManager is missing. " +
                "Level modifier completion was not registered."
            );

            return;
        }

        LevelNodeData completedNode = runState.SelectedLevelNode;

        if (completedNode == null)
        {
            Debug.Log(
                "[RunFlowController] First/default level completed. " +
                "No modifier unlock progress to register."
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

        string modifierId = GetModifierUnlockId(completedNode.weatherType);

        if (string.IsNullOrWhiteSpace(modifierId))
            return;

        Debug.Log(
    $"[RunFlowController] Register completion: " +
    $"type={UnlockConditionType.CompleteLevelModifier}, " +
    $"targetId='{modifierId}', " +
    $"levelNode='{completedNode.name}'"
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

    private string GetModifierUnlockId(LevelWeatherType weatherType)
    {
        switch (weatherType)
        {
            case LevelWeatherType.Darkness:
                return "Darkness";

            case LevelWeatherType.Rain:
                return "Rain";

            case LevelWeatherType.Snow:
                return "Snow";

            default:
                return string.Empty;
        }
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

        if (worldAccelerationRule == null)
            worldAccelerationRule = FindFirstObjectByType<WorldAccelerationRule>();

        if (noDamageChallenge == null)
            noDamageChallenge = FindFirstObjectByType<NoDamageChallenge>();
    }
}
