#if UNITY_EDITOR || DEVELOPMENT_BUILD
using Random = BotRunSeed.WorldRandom;
#endif
using System.Collections.Generic;
using UnityEngine;

public sealed class ProductionExplorationSectorController : MonoBehaviour
{
    private const int NormalSiteCount = 6;
    private const int TotalSiteCount = NormalSiteCount + 1;
    private const int LayoutAttempts = 200;
    private const int CoverageGridSize = 30;
    private const int SpecialLineSamples = 20;


    private const int BreakableOverlapBufferSize = 16;

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

    [SerializeField] private PropScatterProfile propScatterProfile;
    private ProductionSectorProps propScatter;
    public int PropSeed => propScatter != null ? propScatter.Seed : 0;
    public int PropCount => propScatter != null ? propScatter.Count : 0;
    public void RegenerateProps() => propScatter?.Regenerate();
    public void ClearProps() => propScatter?.Clear();
    public void ChangePropSeed(int delta) => propScatter?.ChangeSeed(delta);

    private ExplorationSectorConfig config;
    private GameplayAreaService gameplayArea;
    private EnemySpawner enemySpawner;
    private WorldEventSpawner eventSpawner;
    private LevelAnomalyController anomalyController;
    private RunFlowController runFlow;
    private Vector2[] breakableNormalSitePositions;
    private Vector2 breakableSpecialSitePosition;
    private Vector2 breakableExitPosition;
    private bool hasBreakableLayout;
    private Vector2? layoutSpawnPosition;
    private readonly HashSet<WorldEvent> layoutIgnoredEvents = new();
    private readonly List<WorldBreakable> spawnedBreakables = new();
    private readonly List<ResourceNode> resourceNodes = new();
    private readonly Collider2D[] breakableOverlapBuffer =
        new Collider2D[BreakableOverlapBufferSize];

    public static ProductionExplorationSectorController ActiveInstance
        { get; private set; }

    private void OnEnable()
    {
        ActiveInstance = this;
    }

    private void OnDisable()
    {
        if (ActiveInstance == this)
            ActiveInstance = null;
    }

    public bool Initialize(
        ExplorationSectorConfig explorationConfig,
        GameplayAreaService area,
        EnemySpawner enemies,
        WorldEventSpawner events,
        LevelAnomalyController anomalies,
        RunFlowController flow)
    {
        RunThreatController threatController =
            gameObject.GetComponent<RunThreatController>();

        if (threatController == null)
        {
            Debug.LogError("[ExplorationSector] Authored RunThreatController is missing.", this);
            enabled = false;
            return false;
        }


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
        layoutSpawnPosition = null;

        if (!ValidateDependencies())
            return false;

        anomalyController.BeginSiteLayout();
        eventSpawner.ConfigureSiteControlledMode(TotalSiteCount);

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
        if (!specialSite.InitializeSpecial(
                specialPosition,
                specialSize,
                specialPower,
                siteEvents[NormalSiteCount % siteEvents.Count],
                eventSpawner,
                anomalyController,
                gameObject,
                config,
                exitPosition,
                config.ExitRadius))
        {
            Destroy(specialObject);
            return false;
        }

        GameObject exitObject = new("Sector Exit");
        ProductionSectorExit sectorExit =
            exitObject.AddComponent<ProductionSectorExit>();
        sectorExit.Initialize(exitPosition, config.ExitRadius, runFlow);

        breakableNormalSitePositions = normalPositions;
        breakableSpecialSitePosition = specialPosition;
        breakableExitPosition = exitPosition;
        hasBreakableLayout = true;

        int breakableCount = SpawnWorldBreakables(
            normalPositions,
            specialPosition,
            exitPosition
        );

        SpawnResourceNodes();
        // Place decoration after gameplay objects so its profile/seed never drives their placement.
        propScatter = new ProductionSectorProps(transform, gameplayArea, normalPositions,
            specialPosition, exitPosition, config.ExitRadius, propScatterProfile != null
                ? propScatterProfile : Resources.Load<PropScatterProfile>("PropScatterProfile"));
        propScatter.Regenerate();

        SpawnPortalPair();
        threatController.Initialize(config.ThreatConfig, enemySpawner);

        Debug.Log(
            $"[ExplorationSector] Sector ready: {NormalSiteCount} Normal, " +
            $"1 Special ({specialPower}), {layoutDiagnostics.Coverage:P0} " +
            $"map coverage, {breakableCount} breakables, " +
            $"Exit at {exitPosition}."
        );
        return true;
    }

    public ProductionPortalPair PortalPair { get; private set; }

    public bool SpawnPortalPair()
    {
        if (!hasBreakableLayout) return false;
        Physics2D.SyncTransforms();
        Bounds bounds = gameplayArea.PlayableArea.bounds;
        var candidates = new List<Vector2>();
        for (int i = 0; i < 1500; i++)
        {
            Vector2 point = new(Random.Range(bounds.min.x, bounds.max.x), Random.Range(bounds.min.y, bounds.max.y));
            if (!IsFootprintClear(point, Vector2.one * 3f) ||
                Vector2.Distance(point, breakableExitPosition) < config.ExitRadius + 2f) continue;
            // Territories tile the map; portals may occupy them. Physical clearance still applies above.
            foreach (Vector2 other in candidates)
            {
                if (Vector2.Distance(point, other) < Mathf.Max(12f, bounds.size.magnitude * 0.4f)) continue;
                if (PortalPair != null) { PortalPair.gameObject.SetActive(false); Destroy(PortalPair.gameObject); }
                var root = new GameObject("Sector Portal Pair");
                root.transform.SetParent(transform, false);
                PortalPair = root.AddComponent<ProductionPortalPair>();
                PortalPair.Initialize(other, point);
                return true;
            }
            candidates.Add(point);
        }
        Debug.LogWarning("[ExplorationSector] No safe distant portal pair found.");
        return false;
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    public bool RegenerateAnomalyLayout()
    {
        if (!hasBreakableLayout) return false;
        var previousSites = new List<ProductionAnomalySite>(ProductionAnomalySite.ActiveSites);
        foreach (var site in previousSites)
            if (site.SiteEvent != null) layoutIgnoredEvents.Add(site.SiteEvent);
        bool built;
        Vector2[] positions, sizes;
        Vector2 specialPosition, specialSize, exit;
        try
        {
            built = BuildLayout(out positions, out sizes, out specialPosition,
                out specialSize, out exit, out _);
        }
        finally { layoutIgnoredEvents.Clear(); }
        if (!built) return false;
        foreach (var site in previousSites) site.RemoveForLayout();
        anomalyController.BeginSiteLayout();
        eventSpawner.ConfigureSiteControlledMode(TotalSiteCount);
        var events = BuildSiteEventPool();
        var anomalies = BuildNormalAnomalyPool();
        for (int i = 0; i < NormalSiteCount; i++)
            new GameObject($"Normal Anomaly Site {i + 1}").AddComponent<ProductionAnomalySite>()
                .InitializeNormal(positions[i], sizes[i], anomalies[i % anomalies.Length],
                    events[i % events.Count], eventSpawner, anomalyController, exit, config.ExitRadius);
        bool special = new GameObject("Special Anomaly Site").AddComponent<ProductionAnomalySite>()
            .InitializeSpecial(specialPosition, specialSize, SelectSpecialPower(),
                events[NormalSiteCount % events.Count], eventSpawner, anomalyController,
                gameObject, config, exit, config.ExitRadius);
        breakableNormalSitePositions = positions;
        breakableSpecialSitePosition = specialPosition;
        SpawnPortalPair();
        return special;
    }
#endif

    public void ClearResourceNodes()
    {
        foreach (var node in resourceNodes)
        {
            if (node == null) continue;
            node.gameObject.SetActive(false);
            Destroy(node.gameObject);
        }
        resourceNodes.Clear();
    }

    public int SpawnResourceNodes()
    {
        if (!hasBreakableLayout || gameplayArea == null) return 0;
        var player = GameObject.FindGameObjectWithTag("Player");
        if (player == null) return 0;
        ClearResourceNodes();
        ResourceNode[] clickPropPrefabs = config.ClickPropPrefabs;
        if (clickPropPrefabs == null || clickPropPrefabs.Length == 0)
        {
            Debug.LogWarning("[ResourceNode] No ClickProp prefabs configured.", this);
            return 0;
        }
        Physics2D.SyncTransforms();
        var bounds = gameplayArea.PlayableArea.bounds;
        var placed = new List<Vector2>();
        var propsRoot = transform.Find("Sector visual props");
        var props = propsRoot != null ? propsRoot.GetComponentsInChildren<Renderer>() :
            System.Array.Empty<Renderer>();
        int target = Random.Range(5, 9);
        for (int attempt = 0; attempt < 1000 && placed.Count < target; attempt++)
        {
            var position = new Vector2(Random.Range(bounds.min.x, bounds.max.x),
                Random.Range(bounds.min.y, bounds.max.y));
            if (!IsBreakablePositionValid(position, player.transform.position,
                    breakableNormalSitePositions, breakableSpecialSitePosition,
                    breakableExitPosition, placed, 2f, 1f)) continue;
            // Also exclude decoration without colliders and obstacles on other layers.
            bool blocked = false;
            foreach (var prop in props)
            {
                var b = prop.bounds;
                if (position.x >= b.min.x - .65f && position.x <= b.max.x + .65f &&
                    position.y >= b.min.y - .65f && position.y <= b.max.y + .65f)
                { blocked = true; break; }
            }
            if (blocked) continue;
            var filter = new ContactFilter2D { useTriggers = false };
            if (Physics2D.OverlapCircle(position, .65f, filter, breakableOverlapBuffer) > 0)
                continue;
            ResourceNode prefab = clickPropPrefabs[
                Random.Range(0, clickPropPrefabs.Length)];
            if (prefab == null) continue;
            ResourceNode node = Instantiate(
                prefab, position, Quaternion.identity, transform);
            node.name = prefab.name;
            node.Initialize(player.transform);
            resourceNodes.Add(node);
            placed.Add(position);
        }
        if (placed.Count < target)
            Debug.LogWarning($"[ResourceNode] Only {placed.Count}/{target} safe positions found.", this);
        return placed.Count;
    }

    private int SpawnWorldBreakables(
        Vector2[] normalSitePositions,
        Vector2 specialSitePosition,
        Vector2 exitPosition)
    {
        WorldBreakable prefab = config.BreakablePrefab;
        if (prefab == null)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogWarning(
                "[Breakables] requested=0 spawned=0 rejected=0 " +
                "reason=missing-prefab",
                this
            );
#endif
            return 0;
        }

        ClearSpawnedBreakables();

        Bounds bounds = gameplayArea.PlayableArea.bounds;
        GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
        Vector2 playerPosition = playerObject != null
            ? playerObject.transform.position
            : bounds.center;
        int targetCount = Random.Range(
            config.BreakableMinCount,
            config.BreakableMaxCount + 1
        );
        List<Vector2> placed = new(targetCount);
        int failedAnchors = 0;
        int rejected = 0;
        int fallbackSpawned = 0;

        while (placed.Count < targetCount &&
            failedAnchors < config.BreakablePlacementAttempts)
        {
            Vector2 anchor = new(
                Random.Range(bounds.min.x, bounds.max.x),
                Random.Range(bounds.min.y, bounds.max.y)
            );

            if (!IsBreakablePositionValid(
                    anchor,
                    playerPosition,
                    normalSitePositions,
                    specialSitePosition,
                    exitPosition,
                    placed,
                    config.BreakablePlayerClearance,
                    1f))
            {
                failedAnchors++;
                rejected++;
                continue;
            }

            int patternCount = Random.value < config.BreakableClusterChance
                ? Random.Range(2, 4)
                : 1;
            SpawnBreakable(prefab, anchor);
            placed.Add(anchor);

            for (int i = 1; i < patternCount && placed.Count < targetCount; i++)
            {
                Vector2 clusterPosition = anchor +
                    Random.insideUnitCircle.normalized *
                    Random.Range(
                        config.BreakableSpacing,
                        config.BreakableSpacing * 1.35f
                    );

                if (!IsBreakablePositionValid(
                        clusterPosition,
                        playerPosition,
                        normalSitePositions,
                        specialSitePosition,
                        exitPosition,
                        placed,
                        config.BreakablePlayerClearance,
                        1f))
                {
                    rejected++;
                    continue;
                }

                SpawnBreakable(prefab, clusterPosition);
                placed.Add(clusterPosition);
            }

            failedAnchors = 0;
        }

        int fallbackAttempts = 0;
        while (placed.Count < targetCount &&
            fallbackAttempts < config.BreakablePlacementAttempts)
        {
            fallbackAttempts++;
            Vector2 candidate = new(
                Random.Range(bounds.min.x, bounds.max.x),
                Random.Range(bounds.min.y, bounds.max.y)
            );

            if (!IsBreakablePositionValid(
                    candidate,
                    playerPosition,
                    normalSitePositions,
                    specialSitePosition,
                    exitPosition,
                    placed,
                    config.BreakablePlayerClearance,
                    0.75f))
            {
                rejected++;
                continue;
            }

            SpawnBreakable(prefab, candidate);
            placed.Add(candidate);
            fallbackSpawned++;
        }

        if (placed.Count < targetCount)
        {
            Debug.LogWarning(
                $"[ExplorationSector] Placed {placed.Count}/{targetCount} " +
                "world breakables after exhausting safe positions.",
                this
            );
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        int runtimeVisible = 0;
        for (int i = 0; i < spawnedBreakables.Count; i++)
        {
            WorldBreakable breakable = spawnedBreakables[i];
            if (breakable == null || !breakable.gameObject.activeInHierarchy)
                continue;

            SpriteRenderer[] renderers =
                breakable.GetComponentsInChildren<SpriteRenderer>(false);
            for (int rendererIndex = 0;
                rendererIndex < renderers.Length;
                rendererIndex++)
            {
                if (renderers[rendererIndex] != null &&
                    renderers[rendererIndex].enabled)
                {
                    runtimeVisible++;
                    break;
                }
            }
        }

        Debug.Log(
            $"[Breakables] requested={targetCount} spawned={placed.Count} " +
            $"visible={runtimeVisible} rejected={rejected} " +
            $"fallback={fallbackSpawned} " +
            $"bounds={bounds.size.x:0.#}x{bounds.size.y:0.#}",
            this
        );
#endif

        return placed.Count;
    }

    private bool IsBreakablePositionValid(
        Vector2 position,
        Vector2 playerPosition,
        Vector2[] normalSitePositions,
        Vector2 specialSitePosition,
        Vector2 exitPosition,
        List<Vector2> placed,
        float playerClearance,
        float spacingMultiplier)
    {
        float obstacleClearance = config.BreakableObstacleClearance;
        if (!gameplayArea.IsInsidePlayableArea(position, obstacleClearance) ||
            Vector2.Distance(position, playerPosition) <
                playerClearance ||
            Vector2.Distance(position, specialSitePosition) <
                config.BreakableCriticalClearance ||
            Vector2.Distance(position, exitPosition) <
                config.BreakableCriticalClearance + config.ExitRadius)
        {
            return false;
        }

        for (int i = 0; i < normalSitePositions.Length; i++)
        {
            if (Vector2.Distance(position, normalSitePositions[i]) <
                config.BreakableCriticalClearance)
            {
                return false;
            }
        }

        float minimumSpacingSquared =
            config.BreakableSpacing * Mathf.Clamp01(spacingMultiplier);
        minimumSpacingSquared *= minimumSpacingSquared;
        for (int i = 0; i < placed.Count; i++)
        {
            if ((placed[i] - position).sqrMagnitude < minimumSpacingSquared)
                return false;
        }

        ContactFilter2D filter = new();
        filter.SetLayerMask(config.BreakableObstacleMask);
        filter.useTriggers = false;
        int overlapCount = Physics2D.OverlapCircle(
            position,
            obstacleClearance,
            filter,
            breakableOverlapBuffer
        );
        return overlapCount == 0;
    }

    private void SpawnBreakable(WorldBreakable prefab, Vector2 position)
    {
        WorldBreakable instance = Instantiate(
            prefab,
            position,
            prefab.transform.rotation,
            transform
        );
        instance.name = prefab.name;
        spawnedBreakables.Add(instance);
    }

    private void ClearSpawnedBreakables()
    {
        for (int i = 0; i < spawnedBreakables.Count; i++)
        {
            WorldBreakable breakable = spawnedBreakables[i];
            if (breakable == null)
                continue;

            breakable.gameObject.SetActive(false);
            Destroy(breakable.gameObject);
        }

        spawnedBreakables.Clear();
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    public bool DebugSpawnCrateNearPlayer()
    {
        if (!hasBreakableLayout || config == null ||
            config.BreakablePrefab == null || gameplayArea == null)
        {
            return false;
        }

        GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
        if (playerObject == null)
            return false;

        Vector2 playerPosition = playerObject.transform.position;
        List<Vector2> occupied = new(spawnedBreakables.Count);
        for (int i = 0; i < spawnedBreakables.Count; i++)
        {
            if (spawnedBreakables[i] != null &&
                !spawnedBreakables[i].IsBroken)
            {
                occupied.Add(spawnedBreakables[i].transform.position);
            }
        }

        for (int i = 0; i < config.BreakablePlacementAttempts; i++)
        {
            Vector2 direction = Random.insideUnitCircle;
            if (direction.sqrMagnitude < 0.001f)
                direction = Vector2.right;

            Vector2 candidate = playerPosition + direction.normalized *
                Random.Range(1.75f, 3.25f);
            if (!IsBreakablePositionValid(
                    candidate,
                    playerPosition,
                    breakableNormalSitePositions,
                    breakableSpecialSitePosition,
                    breakableExitPosition,
                    occupied,
                    1.5f,
                    0.75f))
            {
                continue;
            }

            SpawnBreakable(config.BreakablePrefab, candidate);
            return true;
        }

        return false;
    }

    public int DebugRespawnSectorBreakables()
    {
        if (!hasBreakableLayout)
            return 0;

        return SpawnWorldBreakables(
            breakableNormalSitePositions,
            breakableSpecialSitePosition,
            breakableExitPosition
        );
    }

    public int DebugBreakAll()
    {
        int brokenCount = 0;
        List<WorldBreakable> snapshot = new(
            WorldBreakable.ActiveInstances
        );

        for (int i = 0; i < snapshot.Count; i++)
        {
            if (snapshot[i] != null &&
                snapshot[i].TakeDamage(float.MaxValue, snapshot[i].transform.position))
            {
                brokenCount++;
            }
        }

        return brokenCount;
    }
#endif

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
        Rect playable = new(bounds.min, bounds.size);
        if (!layoutSpawnPosition.HasValue)
            layoutSpawnPosition = GameObject.FindGameObjectWithTag("Player")?.transform.position ?? bounds.center;
        Vector2 playerPosition = layoutSpawnPosition.Value;

        // Keep the existing physical exit placement; territories may reach its location.
        Physics2D.SyncTransforms();
        bool exitFound = false;
        for (int attempt = 0; attempt < LayoutAttempts; attempt++)
        {
            exitPosition = hasBreakableLayout ? breakableExitPosition : SelectExitPosition(playable, playerPosition);
            if (!IsFootprintClear(exitPosition, Vector2.one * (config.ExitRadius * 2f), true)) continue;
            exitFound = true;
            break;
        }
        if (!exitFound) return false;

        SiteRegion[] regions = CreateMosaicRegions(playable, playerPosition, out int specialIndex);
        ApplyLayout(regions, specialIndex, normalPositions, normalSizes, out specialPosition, out specialSize);
        diagnostics = new LayoutDiagnostics(1, SampleCoverage(playable, regions),
            Vector2.Distance(playerPosition, specialPosition), GetMaximumCornerDistance(playable, playerPosition),
            SampleNormalShareOnSpecialLine(playerPosition, regions, specialIndex),
            GetExitMembership(exitPosition, regions, specialIndex), false);
        return true;
    }

    private static SiteRegion[] CreateMosaicRegions(Rect area, Vector2 spawn, out int specialIndex)
    {
        // A single neutral spawn rectangle is the only hole in the partition.
        Vector2 safeHalf = new(area.width * Random.Range(0.08f, 0.11f),
            area.height * Random.Range(0.08f, 0.11f));
        Rect safe = Rect.MinMaxRect(Mathf.Max(area.xMin, spawn.x - safeHalf.x),
            Mathf.Max(area.yMin, spawn.y - safeHalf.y), Mathf.Min(area.xMax, spawn.x + safeHalf.x),
            Mathf.Min(area.yMax, spawn.y + safeHalf.y));
        var cells = new List<Rect>();
        // Alternating windings vary the T-junctions without overlaps or seams.
        if (Random.value < 0.5f)
        {
            cells.Add(Rect.MinMaxRect(area.xMin, safe.yMax, safe.xMax, area.yMax));
            cells.Add(Rect.MinMaxRect(safe.xMax, safe.yMin, area.xMax, area.yMax));
            cells.Add(Rect.MinMaxRect(safe.xMin, area.yMin, area.xMax, safe.yMin));
            cells.Add(Rect.MinMaxRect(area.xMin, area.yMin, safe.xMin, safe.yMax));
        }
        else
        {
            cells.Add(Rect.MinMaxRect(safe.xMin, safe.yMax, area.xMax, area.yMax));
            cells.Add(Rect.MinMaxRect(safe.xMax, area.yMin, area.xMax, safe.yMax));
            cells.Add(Rect.MinMaxRect(area.xMin, area.yMin, safe.xMax, safe.yMin));
            cells.Add(Rect.MinMaxRect(area.xMin, safe.yMin, safe.xMin, area.yMax));
        }
        // Leave one broad territory whole for the Special; split each of the other three.
        specialIndex = Random.Range(0, cells.Count);
        Rect special = cells[specialIndex];
        cells.RemoveAt(specialIndex);
        var regions = new List<SiteRegion>();
        foreach (Rect cell in cells)
        {
            float split = Random.Range(0.38f, 0.62f);
            bool vertical = cell.width > cell.height * Random.Range(0.85f, 1.15f);
            Rect first, second;
            if (vertical)
            {
                float cut = Mathf.Lerp(cell.xMin, cell.xMax, split);
                first = Rect.MinMaxRect(cell.xMin, cell.yMin, cut, cell.yMax);
                second = Rect.MinMaxRect(cut, cell.yMin, cell.xMax, cell.yMax);
            }
            else
            {
                float cut = Mathf.Lerp(cell.yMin, cell.yMax, split);
                first = Rect.MinMaxRect(cell.xMin, cell.yMin, cell.xMax, cut);
                second = Rect.MinMaxRect(cell.xMin, cut, cell.xMax, cell.yMax);
            }
            regions.Add(new SiteRegion(first.center, first.size));
            regions.Add(new SiteRegion(second.center, second.size));
        }
        // Type assignment uses the existing shuffled pool against a shuffled territory order.
        Shuffle(regions);
        specialIndex = regions.Count;
        regions.Add(new SiteRegion(special.center, special.size));
        return regions.ToArray();
    }
    private static float DistanceToRect(Vector2 point, Rect rect) => Vector2.Distance(point,
        new Vector2(Mathf.Clamp(point.x, rect.xMin, rect.xMax), Mathf.Clamp(point.y, rect.yMin, rect.yMax)));

    private bool IsFootprintClear(Vector2 position, Vector2 size, bool ignoreExit = false)
    {
        Vector2 half = size * 0.5f + Vector2.one * 0.25f;
        for (int y = -1; y <= 1; y++)
        for (int x = -1; x <= 1; x++)
            if (!gameplayArea.IsInsidePlayableArea(position + new Vector2(x * half.x, y * half.y))) return false;
        foreach (var hit in Physics2D.OverlapBoxAll(position, half * 2f, 0f))
        {
            if (hit == gameplayArea.PlayableArea || hit == gameplayArea.SpawnArea) continue;
            // Actors are transient; only the static footprint constrains sector layout.
            if (hit.GetComponentInParent<EnemyHealth>() != null) continue;
            var hitEvent = hit.GetComponentInParent<WorldEvent>();
            if (hitEvent != null && layoutIgnoredEvents.Contains(hitEvent)) continue;
            if (ignoreExit && hit.GetComponentInParent<ProductionSectorExit>() != null) continue;
            if (!hit.isTrigger || hit.GetComponentInParent<WorldEvent>() != null ||
                hit.GetComponentInParent<ResourceNode>() != null ||
                hit.GetComponentInParent<ProductionSectorExit>() != null) return false;
        }
        Rect footprint = new(position - half, half * 2f);
        foreach (var worldEvent in eventSpawner.SpawnedEvents)
            if (worldEvent != null && !layoutIgnoredEvents.Contains(worldEvent) && worldEvent.gameObject.activeInHierarchy &&
                DistanceToRect(worldEvent.transform.position, footprint) <
                eventSpawner.GetSiteEventFootprintRadius(worldEvent) + 0.5f) return false;
        return true;
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

            if (prefab != null && prefab.AllowedInSite)
                result.Add(prefab);
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
        if (runState != null && !runState.AnomalyInventory.IsEmpty)
            return runState.AnomalyInventory.CurrentItem.PowerType;

        List<AnomalyPowerType> available = new();

        for (int i = 0; i < config.SpecialPowerPool.Length; i++)
        {
            AnomalyPowerType power = config.SpecialPowerPool[i];

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
