using System.Collections.Generic;
using UnityEngine;

public sealed class ProductionExplorationSectorController : MonoBehaviour
{
    private const int NormalSiteCount = 3;
    private const int TotalSiteCount = 4;
    private const int LayoutAttempts = 200;
    private const int CoverageGridSize = 30;
    private const int SpecialLineSamples = 20;
    private const float MinimumCoverage = 0.85f;
    private const float MaximumCoverage = 0.95f;

    private readonly struct SiteRegion
    {
        public Vector2 Center { get; }
        public Vector2 Size { get; }
        public Rect Bounds => new(Center - Size * 0.5f, Size);

        public SiteRegion(Vector2 center, Vector2 size)
        {
            Center = center;
            Size = size;
        }
    }

    private readonly struct LayoutDiagnostics
    {
        public int Attempts { get; }
        public float Coverage { get; }
        public float SpecialDistance { get; }
        public float MaximumTravelDistance { get; }
        public float NormalLineShare { get; }
        public string ExitMembership { get; }
        public bool UsedFallback { get; }

        public LayoutDiagnostics(
            int attempts,
            float coverage,
            float specialDistance,
            float maximumTravelDistance,
            float normalLineShare,
            string exitMembership,
            bool usedFallback)
        {
            Attempts = attempts;
            Coverage = coverage;
            SpecialDistance = specialDistance;
            MaximumTravelDistance = maximumTravelDistance;
            NormalLineShare = normalLineShare;
            ExitMembership = exitMembership;
            UsedFallback = usedFallback;
        }
    }

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
                out Vector2[] normalSizes,
                out Vector2 specialPosition,
                out Vector2 specialSize,
                out Vector2 exitPosition,
                out LayoutDiagnostics layoutDiagnostics))
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
                normalSizes[i],
                normalAnomalies[i % normalAnomalies.Length],
                siteEvents[i % siteEvents.Count],
                eventSpawner,
                anomalyController,
                exitPosition,
                config.ExitRadius
            );
        }

        AnomalyPowerType specialPower = SelectSpecialPower();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        LogLayoutDiagnostics(
            layoutDiagnostics,
            specialPower,
            normalPositions,
            normalSizes,
            specialPosition,
            specialSize,
            exitPosition
        );
#endif
        GameObject specialObject = new(
            $"Special Anomaly Site - {specialPower}"
        );
        ProductionAnomalySite specialSite =
            specialObject.AddComponent<ProductionAnomalySite>();
        specialSite.InitializeSpecial(
            specialPosition,
            specialSize,
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
            $"1 Special ({specialPower}), {layoutDiagnostics.Coverage:P0} " +
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
        out Vector2[] normalSizes,
        out Vector2 specialPosition,
        out Vector2 specialSize,
        out Vector2 exitPosition,
        out LayoutDiagnostics diagnostics)
    {
        normalPositions = new Vector2[NormalSiteCount];
        normalSizes = new Vector2[NormalSiteCount];
        specialPosition = default;
        specialSize = default;
        exitPosition = default;
        diagnostics = default;

        Bounds bounds = gameplayArea.PlayableArea.bounds;

        if (bounds.size.x <= 0f || bounds.size.y <= 0f)
        {
            Debug.LogError(
                "[ExplorationSector] Gameplay bounds have no usable area."
            );
            return false;
        }

        GameObject player = GameObject.FindGameObjectWithTag("Player");
        Vector2 playerPosition = player != null
            ? player.transform.position
            : bounds.center;
        Rect playable = new(
            bounds.min.x,
            bounds.min.y,
            bounds.size.x,
            bounds.size.y
        );
        Rect usable = InsetRect(playable, config.EdgePadding);
        float maximumTravelDistance = GetMaximumCornerDistance(
            usable,
            playerPosition
        );

        for (int attempt = 1; attempt <= LayoutAttempts; attempt++)
        {
            SiteRegion[] regions = CreateRandomRegions(
                usable,
                playerPosition,
                out int specialIndex
            );

            if (!ValidateLayout(
                    playable,
                    playerPosition,
                    regions,
                    specialIndex,
                    maximumTravelDistance,
                    out float coverage,
                    out float lineShare))
            {
                continue;
            }

            exitPosition = SelectExitPosition(playable, playerPosition);
            ApplyLayout(
                regions,
                specialIndex,
                normalPositions,
                normalSizes,
                out specialPosition,
                out specialSize
            );
            diagnostics = new LayoutDiagnostics(
                attempt,
                coverage,
                Vector2.Distance(playerPosition, specialPosition),
                maximumTravelDistance,
                lineShare,
                GetExitMembership(exitPosition, regions, specialIndex),
                false
            );
            return true;
        }

        for (int fallback = 0; fallback < 4; fallback++)
        {
            SiteRegion[] regions = CreateFallbackRegions(
                usable,
                playerPosition,
                fallback,
                out int specialIndex
            );

            if (!ValidateLayout(
                    playable,
                    playerPosition,
                    regions,
                    specialIndex,
                    maximumTravelDistance,
                    out float coverage,
                    out float lineShare))
            {
                continue;
            }

            exitPosition = SelectExitPosition(playable, playerPosition);
            ApplyLayout(
                regions,
                specialIndex,
                normalPositions,
                normalSizes,
                out specialPosition,
                out specialSize
            );
            diagnostics = new LayoutDiagnostics(
                LayoutAttempts + fallback + 1,
                coverage,
                Vector2.Distance(playerPosition, specialPosition),
                maximumTravelDistance,
                lineShare,
                GetExitMembership(exitPosition, regions, specialIndex),
                true
            );
            Debug.LogWarning(
                $"[ExplorationSector] Layout generator used asymmetric " +
                $"fallback {fallback + 1}."
            );
            return true;
        }

        Debug.LogError(
            "[ExplorationSector] Could not produce a valid asymmetric layout."
        );
        return false;
    }

    private SiteRegion[] CreateRandomRegions(
        Rect usable,
        Vector2 playerPosition,
        out int specialIndex)
    {
        bool vertical = Random.value < 0.5f;
        bool farLow = vertical
            ? playerPosition.x - usable.xMin >= usable.xMax - playerPosition.x
            : playerPosition.y - usable.yMin >= usable.yMax - playerPosition.y;

        if (Mathf.Abs(vertical
                ? playerPosition.x - usable.center.x
                : playerPosition.y - usable.center.y) < 0.5f)
        {
            farLow = Random.value < 0.5f;
        }

        float narrowRatio = Random.Range(0.34f, 0.39f);
        float splitA = Random.Range(0.34f, 0.66f);
        float splitB;

        do
        {
            splitB = Random.Range(0.34f, 0.66f);
        }
        while (Mathf.Abs(splitA - splitB) < 0.12f);

        Rect[] cells = BuildStaggeredCells(
            usable,
            vertical,
            farLow,
            narrowRatio,
            splitA,
            splitB
        );
        SiteRegion[] regions = BuildRegionsFromCells(
            cells,
            Mathf.Clamp(config.TargetAnomalyCoverage, 0.87f, 0.92f),
            true
        );
        specialIndex = SelectFarSpecial(
            regions,
            0,
            playerPosition
        );
        return regions;
    }

    private static SiteRegion[] CreateFallbackRegions(
        Rect usable,
        Vector2 playerPosition,
        int fallbackIndex,
        out int specialIndex)
    {
        bool vertical = fallbackIndex % 2 == 0;
        bool farLow = vertical
            ? playerPosition.x >= usable.center.x
            : playerPosition.y >= usable.center.y;
        float narrowRatio = fallbackIndex < 2 ? 0.36f : 0.38f;
        float splitA = fallbackIndex % 2 == 0 ? 0.36f : 0.63f;
        float splitB = fallbackIndex % 2 == 0 ? 0.61f : 0.38f;
        Rect[] cells = BuildStaggeredCells(
            usable,
            vertical,
            farLow,
            narrowRatio,
            splitA,
            splitB
        );
        SiteRegion[] regions = BuildRegionsFromCells(cells, 0.89f, false);
        specialIndex = SelectFarSpecial(
            regions,
            0,
            playerPosition
        );
        return regions;
    }

    private static Rect[] BuildStaggeredCells(
        Rect area,
        bool vertical,
        bool farLow,
        float narrowRatio,
        float narrowSplit,
        float broadSplit)
    {
        if (!vertical)
        {
            Rect[] rotated = BuildStaggeredCells(
                new Rect(area.yMin, area.xMin, area.height, area.width),
                true,
                farLow,
                narrowRatio,
                narrowSplit,
                broadSplit
            );

            for (int i = 0; i < rotated.Length; i++)
            {
                Rect rect = rotated[i];
                rotated[i] = new Rect(
                    rect.yMin,
                    rect.xMin,
                    rect.height,
                    rect.width
                );
            }

            return rotated;
        }

        float cut = farLow
            ? area.xMin + area.width * narrowRatio
            : area.xMax - area.width * narrowRatio;
        Rect low = new(area.xMin, area.yMin, cut - area.xMin, area.height);
        Rect high = new(cut, area.yMin, area.xMax - cut, area.height);
        Rect left = farLow ? low : high;
        Rect right = farLow ? high : low;
        float leftSplit = farLow ? narrowSplit : broadSplit;
        float rightSplit = farLow ? broadSplit : narrowSplit;

        return new[]
        {
            BottomCell(left, leftSplit),
            TopCell(left, leftSplit),
            BottomCell(right, rightSplit),
            TopCell(right, rightSplit)
        };
    }

    private static Rect BottomCell(Rect source, float split) => new(
        source.xMin,
        source.yMin,
        source.width,
        source.height * split
    );

    private static Rect TopCell(Rect source, float split) => new(
        source.xMin,
        source.yMin + source.height * split,
        source.width,
        source.height * (1f - split)
    );

    private static SiteRegion[] BuildRegionsFromCells(
        Rect[] cells,
        float targetCoverage,
        bool randomize)
    {
        SiteRegion[] result = new SiteRegion[cells.Length];
        float baseScale = Mathf.Sqrt(targetCoverage);

        for (int i = 0; i < cells.Length; i++)
        {
            Rect cell = cells[i];
            float xVariation = randomize ? Random.Range(0.985f, 1.015f) : 1f;
            float yVariation = randomize ? Random.Range(0.985f, 1.015f) : 1f;
            float scaleX = Mathf.Clamp(baseScale * xVariation, 0.91f, 0.975f);
            float scaleY = Mathf.Clamp(baseScale * yVariation, 0.91f, 0.975f);
            Vector2 size = new(cell.width * scaleX, cell.height * scaleY);
            Vector2 slack = cell.size - size;
            Vector2 jitter = randomize
                ? new Vector2(
                    Random.Range(-slack.x, slack.x) * 0.32f,
                    Random.Range(-slack.y, slack.y) * 0.32f
                )
                : Vector2.zero;
            result[i] = new SiteRegion(cell.center + jitter, size);
        }

        return result;
    }

    private static int SelectFarSpecial(
        SiteRegion[] regions,
        int narrowStartIndex,
        Vector2 playerPosition)
    {
        int first = Mathf.Clamp(narrowStartIndex, 0, regions.Length - 2);
        int second = first + 1;
        return Vector2.SqrMagnitude(regions[first].Center - playerPosition) >=
            Vector2.SqrMagnitude(regions[second].Center - playerPosition)
                ? first
                : second;
    }

    private static bool ValidateLayout(
        Rect playable,
        Vector2 playerPosition,
        SiteRegion[] regions,
        int specialIndex,
        float maximumTravelDistance,
        out float coverage,
        out float lineShare)
    {
        coverage = SampleCoverage(playable, regions);
        lineShare = SampleNormalShareOnSpecialLine(
            playerPosition,
            regions,
            specialIndex
        );

        float specialDistance = Vector2.Distance(
            playerPosition,
            regions[specialIndex].Center
        );
        float normalDistanceSum = 0f;
        float smallestArea = float.PositiveInfinity;
        float largestArea = 0f;

        for (int i = 0; i < regions.Length; i++)
        {
            float area = regions[i].Size.x * regions[i].Size.y;
            smallestArea = Mathf.Min(smallestArea, area);
            largestArea = Mathf.Max(largestArea, area);

            if (i != specialIndex)
            {
                normalDistanceSum += Vector2.Distance(
                    playerPosition,
                    regions[i].Center
                );
            }
        }

        float averageNormalDistance = normalDistanceSum / NormalSiteCount;
        return coverage >= MinimumCoverage && coverage <= MaximumCoverage &&
            specialDistance >= maximumTravelDistance * 0.55f &&
            specialDistance >= averageNormalDistance +
                maximumTravelDistance * 0.035f &&
            lineShare >= 0.4f && lineShare <= 0.65f &&
            largestArea >= smallestArea * 1.12f;
    }

    private static float SampleCoverage(Rect playable, SiteRegion[] regions)
    {
        int covered = 0;
        int total = CoverageGridSize * CoverageGridSize;

        for (int y = 0; y < CoverageGridSize; y++)
        {
            for (int x = 0; x < CoverageGridSize; x++)
            {
                Vector2 point = new(
                    Mathf.Lerp(playable.xMin, playable.xMax,
                        (x + 0.5f) / CoverageGridSize),
                    Mathf.Lerp(playable.yMin, playable.yMax,
                        (y + 0.5f) / CoverageGridSize)
                );

                for (int i = 0; i < regions.Length; i++)
                {
                    if (!regions[i].Bounds.Contains(point))
                        continue;

                    covered++;
                    break;
                }
            }
        }

        return covered / (float)total;
    }

    private static float SampleNormalShareOnSpecialLine(
        Vector2 playerPosition,
        SiteRegion[] regions,
        int specialIndex)
    {
        int normalSamples = 0;

        for (int sample = 0; sample < SpecialLineSamples; sample++)
        {
            float t = (sample + 0.5f) / SpecialLineSamples;
            Vector2 point = Vector2.Lerp(
                playerPosition,
                regions[specialIndex].Center,
                t
            );

            for (int i = 0; i < regions.Length; i++)
            {
                if (i == specialIndex || !regions[i].Bounds.Contains(point))
                    continue;

                normalSamples++;
                break;
            }
        }

        return normalSamples / (float)SpecialLineSamples;
    }

    private Vector2 SelectExitPosition(Rect playable, Vector2 playerPosition)
    {
        float clearance = config.EdgePadding + config.ExitRadius + 0.5f;
        Rect exitArea = InsetRect(playable, clearance);
        float minimumDistance = Mathf.Max(
            14f,
            GetMaximumCornerDistance(exitArea, playerPosition) * 0.3f
        );

        for (int attempt = 0; attempt < 96; attempt++)
        {
            Vector2 candidate = new(
                Random.Range(exitArea.xMin, exitArea.xMax),
                Random.Range(exitArea.yMin, exitArea.yMax)
            );

            if (Vector2.Distance(candidate, playerPosition) >= minimumDistance)
                return candidate;
        }

        Vector2[] corners = GetCorners(exitArea);
        Vector2 farthest = corners[0];

        for (int i = 1; i < corners.Length; i++)
        {
            if (Vector2.SqrMagnitude(corners[i] - playerPosition) >
                Vector2.SqrMagnitude(farthest - playerPosition))
            {
                farthest = corners[i];
            }
        }

        return farthest;
    }

    private static void ApplyLayout(
        SiteRegion[] regions,
        int specialIndex,
        Vector2[] normalPositions,
        Vector2[] normalSizes,
        out Vector2 specialPosition,
        out Vector2 specialSize)
    {
        specialPosition = regions[specialIndex].Center;
        specialSize = regions[specialIndex].Size;
        int normal = 0;

        for (int i = 0; i < regions.Length; i++)
        {
            if (i == specialIndex)
                continue;

            normalPositions[normal] = regions[i].Center;
            normalSizes[normal] = regions[i].Size;
            normal++;
        }
    }

    private static string GetExitMembership(
        Vector2 exitPosition,
        SiteRegion[] regions,
        int specialIndex)
    {
        for (int i = 0; i < regions.Length; i++)
        {
            if (!regions[i].Bounds.Contains(exitPosition))
                continue;

            return i == specialIndex ? "SPECIAL" : "NORMAL";
        }

        return "NONE";
    }

    private static Rect InsetRect(Rect source, float inset)
    {
        float safeInset = Mathf.Clamp(
            inset,
            0f,
            Mathf.Min(source.width, source.height) * 0.45f
        );
        return new Rect(
            source.xMin + safeInset,
            source.yMin + safeInset,
            source.width - safeInset * 2f,
            source.height - safeInset * 2f
        );
    }

    private static float GetMaximumCornerDistance(
        Rect area,
        Vector2 position)
    {
        Vector2[] corners = GetCorners(area);
        float maximum = 0f;

        for (int i = 0; i < corners.Length; i++)
            maximum = Mathf.Max(maximum, Vector2.Distance(position, corners[i]));

        return maximum;
    }

    private static Vector2[] GetCorners(Rect area) => new[]
    {
        new Vector2(area.xMin, area.yMin),
        new Vector2(area.xMin, area.yMax),
        new Vector2(area.xMax, area.yMin),
        new Vector2(area.xMax, area.yMax)
    };

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    private static void LogLayoutDiagnostics(
        LayoutDiagnostics diagnostics,
        AnomalyPowerType specialPower,
        Vector2[] normalPositions,
        Vector2[] normalSizes,
        Vector2 specialPosition,
        Vector2 specialSize,
        Vector2 exitPosition)
    {
        string fallback = diagnostics.UsedFallback ? " (fallback)" : string.Empty;
        Debug.Log(
            "SECTOR LAYOUT\n" +
            $"Coverage: {diagnostics.Coverage:P1}\n" +
            $"Player: {GameObject.FindGameObjectWithTag("Player")?.transform.position}\n" +
            $"Normal 1: center {normalPositions[0]}, size {normalSizes[0]}\n" +
            $"Normal 2: center {normalPositions[1]}, size {normalSizes[1]}\n" +
            $"Normal 3: center {normalPositions[2]}, size {normalSizes[2]}\n" +
            $"Special {specialPower}: center {specialPosition}, size " +
            $"{specialSize}, distance {diagnostics.SpecialDistance:F1}/" +
            $"{diagnostics.MaximumTravelDistance:F1}\n" +
            $"Line-to-Special in Normal: {diagnostics.NormalLineShare:P0}\n" +
            $"Exit: {exitPosition}, inside {diagnostics.ExitMembership}\n" +
            $"Attempts: {diagnostics.Attempts}{fallback}"
        );
    }
#endif

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
