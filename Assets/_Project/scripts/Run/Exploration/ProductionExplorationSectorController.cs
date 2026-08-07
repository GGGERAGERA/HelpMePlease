using System.Collections.Generic;
using UnityEngine;

public sealed class ProductionExplorationSectorController : MonoBehaviour
{
    private const int NormalSiteCount = 3;
    private const int TotalSiteCount = 4;

    private ExplorationSectorConfig config;
    private GameplayAreaService gameplayArea;
    private EnemySpawner enemySpawner;
    private WorldEventSpawner eventSpawner;
    private LevelAnomalyController anomalyController;
    private RunFlowController runFlow;

    public bool Initialize(
        ExplorationSectorConfig explorationConfig,
        GameplayAreaService area,
        EnemySpawner enemies,
        WorldEventSpawner events,
        LevelAnomalyController anomalies,
        RunFlowController flow)
    {
        config = explorationConfig != null
            ? explorationConfig
            : Resources.Load<ExplorationSectorConfig>(
                "ProductionRun/ExplorationSectorConfig"
            );
        gameplayArea = area;
        enemySpawner = enemies;
        eventSpawner = events;
        anomalyController = anomalies;
        runFlow = flow;

        if (!ValidateDependencies())
            return false;

        anomalyController.BeginSiteLayout();
        eventSpawner.ConfigureSiteControlledMode(TotalSiteCount);

        GravityTrajectoryService trajectoryService =
            gameObject.GetComponent<GravityTrajectoryService>();

        if (trajectoryService == null)
        {
            trajectoryService =
                gameObject.AddComponent<GravityTrajectoryService>();
        }

        trajectoryService.Disable();

        if (!BuildLayout(
                out Vector2[] normalPositions,
                out Vector2 specialPosition,
                out Vector2 exitPosition,
                out Vector2 anomalyZoneSize))
        {
            return false;
        }

        List<WorldEvent> siteEvents = BuildSiteEventPool();

        if (siteEvents.Count == 0)
        {
            Debug.LogError(
                "[ExplorationSector] No Hold/Corridor/False Signal " +
                "event prefabs are available."
            );
            return false;
        }

        LocalAnomalyData[] normalAnomalies = BuildNormalAnomalyPool();

        for (int i = 0; i < NormalSiteCount; i++)
        {
            GameObject siteObject = new($"Normal Anomaly Site {i + 1}");
            ProductionAnomalySite site =
                siteObject.AddComponent<ProductionAnomalySite>();
            site.InitializeNormal(
                normalPositions[i],
                anomalyZoneSize,
                normalAnomalies[i % normalAnomalies.Length],
                siteEvents[i % siteEvents.Count],
                eventSpawner,
                anomalyController,
                exitPosition,
                config.ExitRadius
            );
        }

        AnomalyPowerType specialPower = SelectSpecialPower();
        GameObject specialObject = new(
            $"Special Anomaly Site - {specialPower}"
        );
        ProductionAnomalySite specialSite =
            specialObject.AddComponent<ProductionAnomalySite>();
        specialSite.InitializeSpecial(
            specialPosition,
            anomalyZoneSize,
            specialPower,
            config.GravityAnomaly,
            siteEvents[NormalSiteCount % siteEvents.Count],
            eventSpawner,
            anomalyController,
            trajectoryService,
            config,
            exitPosition,
            config.ExitRadius
        );

        GameObject exitObject = new("Sector Exit");
        ProductionSectorExit sectorExit =
            exitObject.AddComponent<ProductionSectorExit>();
        sectorExit.Initialize(exitPosition, config.ExitRadius, runFlow);

        RunThreatController threatController =
            gameObject.GetComponent<RunThreatController>();

        if (threatController == null)
            threatController = gameObject.AddComponent<RunThreatController>();

        threatController.Initialize(config.ThreatConfig, enemySpawner);

        Debug.Log(
            $"[ExplorationSector] Sector ready: 3 Normal, " +
            $"1 Special ({specialPower}), {config.TargetAnomalyCoverage:P0} " +
            $"map coverage, Exit at {exitPosition}."
        );
        return true;
    }

    private bool ValidateDependencies()
    {
        if (config == null || gameplayArea == null ||
            gameplayArea.PlayableArea == null || enemySpawner == null ||
            eventSpawner == null || anomalyController == null ||
            runFlow == null || config.ThreatConfig == null)
        {
            Debug.LogError(
                "[ExplorationSector] Production configuration or runtime " +
                "dependencies are missing.",
                this
            );
            return false;
        }

        if (config.NormalAnomalies == null ||
            config.NormalAnomalies.Length == 0 ||
            config.SpecialPowerPool == null ||
            config.SpecialPowerPool.Length == 0)
        {
            Debug.LogError(
                "[ExplorationSector] Site pools are empty.",
                this
            );
            return false;
        }

        return true;
    }

    private bool BuildLayout(
        out Vector2[] normalPositions,
        out Vector2 specialPosition,
        out Vector2 exitPosition,
        out Vector2 anomalyZoneSize)
    {
        normalPositions = new Vector2[NormalSiteCount];
        specialPosition = default;
        exitPosition = default;
        anomalyZoneSize = default;

        Bounds bounds = gameplayArea.PlayableArea.bounds;

        if (bounds.size.x <= 0f || bounds.size.y <= 0f)
        {
            Debug.LogError(
                "[ExplorationSector] Gameplay bounds have no usable area."
            );
            return false;
        }

        // Four old-style production regions together occupy the same target
        // share of the map as the former LevelAnomalyController layout.
        float regionScale = Mathf.Sqrt(
            config.TargetAnomalyCoverage / TotalSiteCount
        );
        anomalyZoneSize = new Vector2(
            bounds.size.x * regionScale,
            bounds.size.y * regionScale
        );

        Vector2 halfZone = anomalyZoneSize * 0.5f;
        float offsetX = bounds.extents.x - config.EdgePadding - halfZone.x;
        float offsetY = bounds.extents.y - config.EdgePadding - halfZone.y;
        float neutralHalfWidthX = offsetX - halfZone.x;
        float neutralHalfWidthY = offsetY - halfZone.y;
        float requiredExitHalfWidth = config.ExitRadius + 0.15f;

        if (offsetX <= 0f || offsetY <= 0f ||
            neutralHalfWidthX < requiredExitHalfWidth ||
            neutralHalfWidthY < requiredExitHalfWidth)
        {
            Debug.LogError(
                "[ExplorationSector] Gameplay bounds cannot fit the " +
                "requested anomaly coverage and the neutral Exit corridor."
            );
            return false;
        }

        List<Vector2> regionPositions = new()
        {
            new Vector2(bounds.center.x - offsetX, bounds.center.y - offsetY),
            new Vector2(bounds.center.x + offsetX, bounds.center.y - offsetY),
            new Vector2(bounds.center.x - offsetX, bounds.center.y + offsetY),
            new Vector2(bounds.center.x + offsetX, bounds.center.y + offsetY)
        };
        Shuffle(regionPositions);

        specialPosition = regionPositions[0];

        for (int i = 0; i < NormalSiteCount; i++)
            normalPositions[i] = regionPositions[i + 1];

        GameObject player = GameObject.FindGameObjectWithTag("Player");
        Vector2 playerPosition = player != null
            ? player.transform.position
            : bounds.center;

        Vector2[] exitCandidates =
        {
            new(bounds.center.x, bounds.max.y - config.EdgePadding - config.ExitRadius),
            new(bounds.center.x, bounds.min.y + config.EdgePadding + config.ExitRadius),
            new(bounds.max.x - config.EdgePadding - config.ExitRadius, bounds.center.y),
            new(bounds.min.x + config.EdgePadding + config.ExitRadius, bounds.center.y)
        };

        float farthestDistance = float.NegativeInfinity;

        for (int i = 0; i < exitCandidates.Length; i++)
        {
            float distance = (exitCandidates[i] - playerPosition).sqrMagnitude;

            if (distance > farthestDistance)
            {
                farthestDistance = distance;
                exitPosition = exitCandidates[i];
            }
        }

        Debug.Log(
            $"[ExplorationSector] Regions: {anomalyZoneSize.x:F2} x " +
            $"{anomalyZoneSize.y:F2}; neutral cross: " +
            $"{neutralHalfWidthX * 2f:F2} x " +
            $"{neutralHalfWidthY * 2f:F2}."
        );

        return true;
    }

    private List<WorldEvent> BuildSiteEventPool()
    {
        List<WorldEvent> result = new();
        IReadOnlyList<WorldEvent> prefabs = eventSpawner.EventPrefabs;

        if (prefabs == null)
            return result;

        for (int i = 0; i < prefabs.Count; i++)
        {
            WorldEvent prefab = prefabs[i];

            if (prefab is CaptureZoneEvent ||
                prefab is EvacuationCorridorEvent ||
                prefab is FalseSignalEvent)
            {
                result.Add(prefab);
            }
        }

        Shuffle(result);
        return result;
    }

    private LocalAnomalyData[] BuildNormalAnomalyPool()
    {
        LocalAnomalyData[] source = config.NormalAnomalies;
        LocalAnomalyData[] result = new LocalAnomalyData[source.Length];
        System.Array.Copy(source, result, source.Length);

        for (int i = result.Length - 1; i > 0; i--)
        {
            int swap = Random.Range(0, i + 1);
            (result[i], result[swap]) = (result[swap], result[i]);
        }

        return result;
    }

    private AnomalyPowerType SelectSpecialPower()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (ProductionSectorDebugController.TryGetSpecialOverride(
                out AnomalyPowerType debugPower))
        {
            return debugPower;
        }
#endif

        RunStateManager runState = RunStateManager.Instance;
        List<AnomalyPowerType> available = new();

        for (int i = 0; i < config.SpecialPowerPool.Length; i++)
        {
            AnomalyPowerType power = config.SpecialPowerPool[i];

            if (runState == null || !runState.HasAnomalyPower(power))
                available.Add(power);
        }

        if (available.Count > 0)
            return available[Random.Range(0, available.Count)];

        return config.SpecialPowerPool[
            Random.Range(0, config.SpecialPowerPool.Length)
        ];
    }

    private static void Shuffle<T>(List<T> values)
    {
        for (int i = values.Count - 1; i > 0; i--)
        {
            int swap = Random.Range(0, i + 1);
            (values[i], values[swap]) = (values[swap], values[i]);
        }
    }
}
