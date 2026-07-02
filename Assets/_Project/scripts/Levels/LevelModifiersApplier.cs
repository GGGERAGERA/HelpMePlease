using UnityEngine;

public sealed class LevelModifiersApplier : MonoBehaviour
{
    [Header("Enemy Systems")]
    [SerializeField] private EnemySpawner enemySpawner;

    [Header("Environment")]
    [SerializeField] private GameObject darknessObject;
    [SerializeField] private GameObject rainObject;
    [SerializeField] private GameObject snowObject;

    [Header("Events")]
    [SerializeField] private GameObject holdZoneEventObject;
    [SerializeField] private GameObject extraChestObject;

    private void Start()
    {
        ApplySelectedNode();
    }

    private void ApplySelectedNode()
    {
        LevelNodeData node = SelectedLevelNodeStore.SelectedNode;

        if (node == null)
        {
            DisableEnvironment();
            return;
        }

        ApplyEnemyModifiers(node);
        ApplyWeather(node);
        ApplyEvents(node);

        Debug.Log($"[LevelModifiersApplier] Applied node: {node.nodeName}");
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
                if (darknessObject != null)
                    darknessObject.SetActive(true);
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
        if (darknessObject != null)
            darknessObject.SetActive(false);

        if (rainObject != null)
            rainObject.SetActive(false);

        if (snowObject != null)
            snowObject.SetActive(false);

        if (holdZoneEventObject != null)
            holdZoneEventObject.SetActive(false);

        if (extraChestObject != null)
            extraChestObject.SetActive(false);
    }
}