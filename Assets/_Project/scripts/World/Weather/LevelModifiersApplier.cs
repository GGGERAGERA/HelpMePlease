using UnityEngine;
using UnityEngine.Rendering.Universal;

public sealed class LevelModifiersApplier : MonoBehaviour
{
    [Header("Level")]
    [SerializeField] private LevelNodeData defaultLevel;

    [Header("Enemy Systems")]
    [SerializeField] private EnemySpawner enemySpawner;

    [Header("Environment")]
    [SerializeField] private GameObject rainObject;
    [SerializeField] private GameObject snowObject;

    [Header("Lighting")]
    [SerializeField] private Light2D globalLight;
    [SerializeField] private float normalLightIntensity = 1f;
    [SerializeField] private float darknessLightIntensity = 0.01f;

    [Header("Run Flow")]
    [SerializeField] private RunFlowController runFlowController;

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
            enemySpawner?.SetSpawnProfile(null, currentLevel);
            runFlowController?.ApplyLevelMechanics(null);
            ApplyEndlessEnemyScaling(currentLevel, 1f, 1f, 1f);
            DisableEnvironment();

            RunMessageService.Instance?.ShowCustom(
                $"LEVEL {currentLevel}",
                "Survive until the boss appears.",
                4f
            );

            return;
        }

        ApplyEnemyModifiers(node, currentLevel);
        ApplyWeather(node);
        runFlowController?.ApplyLevelMechanics(node);

        Debug.Log($"[LevelModifiersApplier] Applied node: {node.nodeName}");
        RunMessageService.Instance?.ShowCustom(
            $"LEVEL {currentLevel}",
            $"{node.nodeName}\n{node.description}",
            4f
        );
    }

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

    private void ApplyWeather(LevelNodeData node)
    {
        DisableEnvironment();

        switch (node.weatherType)
        {
            case LevelWeatherType.Darkness:
                ApplyLighting(node);
                break;

            case LevelWeatherType.Rain:
                if (rainObject != null)
                    rainObject.SetActive(true);
                break;

            case LevelWeatherType.Snow:
                if (snowObject != null)
                    snowObject.SetActive(true);
                break;
        }
    }

    private void DisableEnvironment()
    {
        if (globalLight != null)
            globalLight.intensity = normalLightIntensity;

        if (rainObject != null)
            rainObject.SetActive(false);

        if (snowObject != null)
            snowObject.SetActive(false);

    }

    private void ApplyLighting(LevelNodeData node)
    {
        if (globalLight == null)
            return;

        globalLight.intensity = node.weatherType == LevelWeatherType.Darkness
            ? darknessLightIntensity
            : normalLightIntensity;
    }
}
