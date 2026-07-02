using UnityEngine;
using UnityEngine.Rendering.Universal;

public sealed class LevelModifiersApplier : MonoBehaviour
{
    [Header("Enemy Systems")]
    [SerializeField] private EnemySpawner enemySpawner;

    [Header("Environment")]
    [SerializeField] private GameObject rainObject;
    [SerializeField] private GameObject snowObject;

    [Header("Lighting")]
    [SerializeField] private Light2D globalLight;
    [SerializeField] private float normalLightIntensity = 1f;
    [SerializeField] private float darknessLightIntensity = 0.01f;

    [Header("Events")]
    [SerializeField] private GameObject holdZoneEventObject;
    [SerializeField] private GameObject extraChestObject;

    private void Start()
    {
        ApplySelectedNode();
    }

    private void ApplySelectedNode()
    {
        LevelNodeData node = RunStateManager.Instance != null
    ? RunStateManager.Instance.SelectedLevelNode
    : null;

        if (node == null)
        {
            DisableEnvironment();
            return;
        }

        ApplyEnemyModifiers(node);
        ApplyWeather(node);
        ApplyEvents(node);

        Debug.Log($"[LevelModifiersApplier] Applied node: {node.nodeName}");
        RunMessageService.Instance?.ShowCustom(
            $"УРОВЕНЬ {RunStateManager.Instance.CurrentLevel}",
            $"{node.nodeName}\n{node.description}",
            4f
        );
        if (node == null)
        {
            DisableEnvironment();

            RunMessageService.Instance?.ShowCustom(
                "УРОВЕНЬ 1",
                "Выживите до появления босса.",
                4f
            );

            return;
        }
    }

    private void ApplyEnemyModifiers(LevelNodeData node)
    {
        if (enemySpawner == null)
            return;

        enemySpawner.SetLevelScaling(
            node.enemyHealthMultiplier,
            node.enemySpeedMultiplier,
            node.spawnRateMultiplier
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

    private void ApplyEvents(LevelNodeData node)
    {
        if (holdZoneEventObject != null)
            holdZoneEventObject.SetActive(node.hasHoldZoneEvent);

        if (extraChestObject != null)
            extraChestObject.SetActive(node.hasExtraChest);
    }

    private void DisableEnvironment()
    {
        if (globalLight != null)
            globalLight.intensity = normalLightIntensity;

        if (rainObject != null)
            rainObject.SetActive(false);

        if (snowObject != null)
            snowObject.SetActive(false);

        if (holdZoneEventObject != null)
            holdZoneEventObject.SetActive(false);

        if (extraChestObject != null)
            extraChestObject.SetActive(false);
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