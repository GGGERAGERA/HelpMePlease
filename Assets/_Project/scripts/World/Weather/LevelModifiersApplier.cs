using UnityEngine;

public sealed class LevelModifiersApplier : MonoBehaviour
{
    [Header("Level")]
    [SerializeField] private LevelNodeData defaultLevel;

    [Header("Enemy Systems")]
    [SerializeField] private EnemySpawner enemySpawner;

    [Header("Run Flow")]
    [SerializeField] private RunFlowController runFlowController;
    [SerializeField] private LevelAnomalyController anomalyController;
    [SerializeField] private WorldRuleController worldRuleController;

    [Header("Endless Difficulty")]
    [SerializeField, Min(0f)] private float healthGrowthPerLevel = 0.04f;
    [SerializeField, Min(0f)] private float speedGrowthPerLevel = 0f;
    [SerializeField, Min(0f)] private float spawnRateGrowthPerLevel = 0.12f;

    private System.Collections.IEnumerator Start()
    {
        // CharacterSpawner creates the player and the current legacy spawner during Start.
        // Waiting one frame removes script execution-order coupling.
        yield return null;

        if (enemySpawner == null || !enemySpawner.gameObject.scene.IsValid())
            enemySpawner = FindFirstObjectByType<EnemySpawner>();

        if (runFlowController == null || !runFlowController.gameObject.scene.IsValid())
            runFlowController = FindFirstObjectByType<RunFlowController>();

        if (anomalyController == null || !anomalyController.gameObject.scene.IsValid())
            anomalyController = FindFirstObjectByType<LevelAnomalyController>();

        if (worldRuleController == null ||
            !worldRuleController.gameObject.scene.IsValid())
        {
            worldRuleController = FindFirstObjectByType<WorldRuleController>();
        }

        ApplySelectedNode();
    }

    private void ApplySelectedNode()
    {
        RunStateManager runState = RunStateManager.Instance;
        LevelNodeData node = runState != null
            ? runState.SelectedLevelNode
            : null;

        if (node == null)
            node = defaultLevel;
        int currentLevel = runState != null
            ? Mathf.Max(1, runState.CurrentLevel)
            : 1;

        if (node == null)
        {
            Debug.LogError(
                "[LevelModifiersApplier] No LevelNodeData is available. " +
                "Applying neutral World Rule."
            );
            enemySpawner?.SetSpawnProfile(null, currentLevel);
            runFlowController?.ApplyLevelMechanics(null);
            ExperienceManager.Instance?.SetLevelXpGainMultiplier(1f);
            ApplyEndlessEnemyScaling(currentLevel, 1f, 1f, 1f);
            anomalyController?.BeginLevel(null);
            worldRuleController?.Apply(null);

#if UNITY_EDITOR
            LogWorldRule(null);
#endif

            return;
        }

        ApplyEnemyModifiers(node, currentLevel);

        if (node.WorldRule == null)
        {
            Debug.LogError(
                $"[LevelModifiersApplier] LevelNodeData '{node.name}' " +
                "has no WorldRuleData. Applying neutral World Rule.",
                node
            );
        }

        worldRuleController?.Apply(node.WorldRule);
        runFlowController?.ApplyLevelMechanics(node);
        ExperienceManager.Instance?.SetLevelXpGainMultiplier(
            node.ExperienceGainMultiplier
        );
        anomalyController?.BeginLevel(node);

#if UNITY_EDITOR
        LogWorldRule(node);
#endif

        Debug.Log($"[LevelModifiersApplier] Applied node: {node.nodeName}");
    }

#if UNITY_EDITOR
    private static void LogWorldRule(LevelNodeData node)
    {
        string nodeName = node != null ? node.name : "<null>";
        string ruleId = node != null && node.WorldRule != null
            ? node.WorldRule.Id
            : "<null>";

        Debug.Log(
            "[WorldRule]\n" +
            $"Node='{nodeName}'\n" +
            $"Rule='{ruleId}'"
        );
    }
#endif

    private void ApplyEnemyModifiers(LevelNodeData node, int currentLevel)
    {
        enemySpawner?.SetSpawnProfile(node.SpawnProfile, currentLevel);
        ApplyEndlessEnemyScaling(
            currentLevel,
            node.enemyHealthMultiplier,
            node.enemySpeedMultiplier,
            node.spawnRateMultiplier
        );
    }

    private void ApplyEndlessEnemyScaling(
        int currentLevel,
        float nodeHealthMultiplier,
        float nodeSpeedMultiplier,
        float nodeSpawnRateMultiplier
    )
    {
        if (enemySpawner == null)
            return;

        int levelIndex = Mathf.Max(0, currentLevel - 1);

        float healthMultiplier =
            nodeHealthMultiplier * (1f + healthGrowthPerLevel * levelIndex);
        float speedMultiplier =
            nodeSpeedMultiplier * (1f + speedGrowthPerLevel * levelIndex);
        float spawnRateMultiplier =
            nodeSpawnRateMultiplier * (1f + spawnRateGrowthPerLevel * levelIndex);

        enemySpawner.SetLevelScaling(
            healthMultiplier,
            speedMultiplier,
            spawnRateMultiplier
        );
    }

}
