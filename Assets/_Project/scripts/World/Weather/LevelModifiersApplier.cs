using UnityEngine;

public sealed class LevelModifiersApplier : MonoBehaviour
{
    [Header("Editor Direct Play")]
    [SerializeField] private StageProfileData devStageProfile;
    [SerializeField] private WorldRuleData devWorldRule;
    [SerializeField] private LocalAnomalyData devLocalAnomaly;

    [Header("Enemy Systems")]
    [SerializeField] private EnemySpawner enemySpawner;

    [Header("Run Flow")]
    [SerializeField] private RunFlowController runFlowController;
    [SerializeField] private LevelAnomalyController anomalyController;
    [SerializeField] private WorldRuleController worldRuleController;

    private void Awake()
    {
#if UNITY_EDITOR
        RunStateManager runState = RunStateManager.Instance;

        if (runState != null && runState.CurrentSector != null)
            return;

        if (devStageProfile == null ||
            devWorldRule == null ||
            devLocalAnomaly == null)
        {
            Debug.LogError(
                "[DevRunBootstrap] Direct MVP play configuration is missing.",
                this
            );
            return;
        }

        if (devStageProfile.SectorNumber != 1)
        {
            Debug.LogError(
                "[DevRunBootstrap] devStageProfile must describe sector 1.",
                this
            );
            return;
        }

        runState = RunStateManager.EnsureExists();
        runState.SetCurrentSector(new RunSector(
            1,
            devStageProfile,
            devWorldRule,
            devLocalAnomaly
        ));

        Debug.Log(
            "[DevRunBootstrap] Created Sector 1 for direct MVP play.",
            this
        );
#endif
    }

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
        int currentLevel = runState != null
            ? Mathf.Max(1, runState.CurrentLevel)
            : 1;
        RunSector sector = runState != null
            ? runState.CurrentSector
            : null;

        if (sector != null)
        {
            ApplyCurrentSector(sector, currentLevel);

#if UNITY_EDITOR
            LogSectorRuntime(sector);
#endif
            return;
        }

        Debug.LogError(
            "[LevelModifiersApplier] CurrentSector is missing. " +
            "No level modifiers were applied.",
            this
        );
    }

#if UNITY_EDITOR
    private static void LogSectorRuntime(RunSector sector)
    {
        Debug.Log(
            "[SectorRuntime]\n" +
            "Source=RunSector\n" +
            $"Sector={sector.SectorNumber}\n" +
            $"StageProfile='{GetAssetName(sector.StageProfile)}'\n" +
            $"WorldRule='{GetAssetName(sector.WorldRule)}'\n" +
            $"LocalAnomaly='{GetAssetName(sector.LocalAnomaly)}'"
        );
    }

    private static string GetAssetName(Object asset)
    {
        return asset != null ? asset.name : "<null>";
    }
#endif

    private void ApplyCurrentSector(
        RunSector sector,
        int currentLevel)
    {
        enemySpawner?.SetSpawnProfile(
            sector.SpawnProfile,
            currentLevel
        );
        enemySpawner?.SetLevelScaling(
            sector.EnemyHealthMultiplier *
                GetWorldRuleEnemyHealthMultiplier(sector.WorldRule),
            sector.EnemySpeedMultiplier,
            sector.SpawnPressureMultiplier
        );
        worldRuleController?.Apply(sector.WorldRule);
        runFlowController?.ApplyLevelMechanics();
        ExperienceManager.Instance?.SetLevelXpGainMultiplier(
            sector.ExperienceGainMultiplier
        );
        anomalyController?.Apply(sector.LocalAnomaly);
    }

    private static float GetWorldRuleEnemyHealthMultiplier(
        WorldRuleData rule)
    {
        return rule != null ? rule.EnemyHealthMultiplier : 1f;
    }

}
