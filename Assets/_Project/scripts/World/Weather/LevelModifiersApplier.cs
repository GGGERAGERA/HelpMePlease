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
    [SerializeField] private ExplorationSectorConfig explorationConfig;

    [SerializeField] private RunThreatController threatController;
    [SerializeField] private GameplayAreaService gameplayArea;
    [SerializeField] private WorldEventSpawner eventSpawner;
    [SerializeField] private ProductionExplorationSectorController exploration;
    private CharacterSpawner characters;
    public bool IsInitialized { get; private set; }

    public void BindScene(CharacterSpawner playerOwner, EnemySpawner enemies)
    {
        characters = playerOwner;
        enemySpawner = enemies;
        eventSpawner.BindScene(enemies, gameplayArea);
        worldRuleController.BindEnemySpawner(enemies);
    }

    public void PrepareDirectRun(CharacterData character)
    {
        RunStateManager runState = RunStateManager.Instance;

        if (runState != null && runState.CurrentSector != null && runState.OrbitalStationState != null)
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
        runState.BeginNewRun(character, null, devStageProfile, devWorldRule, devLocalAnomaly, true);

        Debug.Log(
            "[DevRunBootstrap] Created Sector 1 for direct MVP play.",
            this
        );
    }

    private void Start()
    {
        if (characters == null || enemySpawner == null || exploration == null ||
            runFlowController == null || anomalyController == null || worldRuleController == null ||
            threatController == null || gameplayArea == null || eventSpawner == null || explorationConfig == null)
        {
            Debug.LogError("[LevelModifiersApplier] Authored scene-host references are missing.", this);
            enabled = false;
            return;
        }
        if (characters.SpawnedPlayer != null) InitializeForPlayer(characters.SpawnedPlayer);
        else characters.CharacterSpawned += InitializeForPlayer;
    }

    private void InitializeForPlayer(GameObject player)
    {
        characters.CharacterSpawned -= InitializeForPlayer;
        worldRuleController.BindPlayer(player);
        ApplySelectedNode();
    }

    private void OnDestroy()
    {
        if (characters != null) characters.CharacterSpawned -= InitializeForPlayer;
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
            IsInitialized = ApplyCurrentSector(sector, currentLevel);

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

    private bool ApplyCurrentSector(
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
        if (!TutorialController.IsTutorialSector) worldRuleController?.Apply(sector.WorldRule);
        runFlowController.BindEnemySpawner(enemySpawner);
        runFlowController.InitializeSector(sector.StageProfile);
        runFlowController.ApplyLevelMechanics();
        ExperienceManager.Instance?.SetLevelXpGainMultiplier(
            sector.ExperienceGainMultiplier
        );

        if (TutorialController.IsTutorialSector || RunRoute.IsExplorationSector(sector.SectorNumber))
            return ApplyExplorationSector();
        return true;
    }

    private bool ApplyExplorationSector()
    {
        return exploration.Initialize(
            RunStateManager.Instance?.CurrentConfig.LayoutProfile ?? explorationConfig,
            gameplayArea,
            enemySpawner,
            eventSpawner,
            anomalyController,
            runFlowController
        );
    }

    private static float GetWorldRuleEnemyHealthMultiplier(
        WorldRuleData rule)
    {
        return rule != null ? rule.EnemyHealthMultiplier : 1f;
    }

}
