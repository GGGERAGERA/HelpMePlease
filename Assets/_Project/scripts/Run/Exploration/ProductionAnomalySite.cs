using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public sealed class ProductionAnomalySite : MonoBehaviour
{
    private static readonly List<ProductionAnomalySite> activeSites = new();

    private WorldEventSpawner eventSpawner;
    private LevelAnomalyController anomalyController;
    private WorldEvent eventPrefab;
    private WorldEvent activeEvent;
    private Vector2 eventPosition;
    private Vector2 exitPosition;
    private float exitRadius;
    private LocalAnomalyZone anomalyZone;
    private IProductionAnomalySiteEnvironment specialEnvironment;
    private ProductionAnomalySiteDefinition specialDefinition;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
    private LocalAnomalyData normalAnomaly;
#endif
    private bool isSpecial;
    private bool completed;
    private bool initialized;
    private Vector2 siteSize;
    private Material material;
    private LineRenderer boundary;

    public bool IsCompleted => completed;
    public bool IsSpecial => isSpecial;
    public bool IsMapVisible => initialized && !completed &&
        isActiveAndEnabled;
    public Vector2 SiteSize => siteSize;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
    public LocalAnomalyZone AnomalyZone => isSpecial
        ? specialEnvironment?.AnomalyZone
        : anomalyZone;
    public string DebugZoneName => isSpecial
        ? $"SPECIAL {specialDefinition?.SiteDisplayName ?? "NONE"}"
        : normalAnomaly != null
            ? $"NORMAL {normalAnomaly.AnomalyType.ToString().ToUpperInvariant()}"
            : "NORMAL NONE";
#endif
    public static IReadOnlyList<ProductionAnomalySite> ActiveSites =>
        activeSites;

    private void OnEnable()
    {
        if (!activeSites.Contains(this))
            activeSites.Add(this);
    }

    private void OnDisable()
    {
        activeSites.Remove(this);
    }

    public bool InitializeNormal(
        Vector2 position,
        Vector2 size,
        LocalAnomalyData anomaly,
        WorldEvent prefab,
        WorldEventSpawner events,
        LevelAnomalyController anomalies,
        Vector2 sectorExitPosition,
        float sectorExitRadius)
    {
        transform.position = position;
        eventSpawner = events;
        anomalyController = anomalies;
        eventPrefab = prefab;
        exitPosition = sectorExitPosition;
        exitRadius = Mathf.Max(0f, sectorExitRadius);
        isSpecial = false;
        specialDefinition = null;
        specialEnvironment = null;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        normalAnomaly = anomaly;
#endif
        siteSize = size;
        initialized = true;
        BuildBoundary(size, new Color(0.2f, 0.8f, 0.9f, 0.8f));
        anomalyZone = anomalyController?.SpawnSiteZone(
            anomaly,
            position,
            size
        );
        eventPosition = SelectEventPosition();
        return SpawnEvent(false);
    }

    public bool InitializeSpecial(
        Vector2 position,
        Vector2 size,
        AnomalyPowerType power,
        WorldEvent prefab,
        WorldEventSpawner events,
        LevelAnomalyController anomalies,
        GameObject environmentServicesHost,
        ExplorationSectorConfig config,
        Vector2 sectorExitPosition,
        float sectorExitRadius)
    {
        if (!ProductionAnomalySiteDefinitionRegistry.TryGet(
                power,
                out ProductionAnomalySiteDefinition definition))
        {
            Debug.LogError(
                $"[AnomalySite] No Site definition registered for '{power}'.",
                this
            );
            return false;
        }

        transform.position = position;
        eventSpawner = events;
        anomalyController = anomalies;
        eventPrefab = prefab;
        exitPosition = sectorExitPosition;
        exitRadius = Mathf.Max(0f, sectorExitRadius);
        isSpecial = true;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        normalAnomaly = null;
#endif
        specialDefinition = definition;
        siteSize = size;
        specialEnvironment = definition.CreateEnvironment(
            new ProductionAnomalySiteContext(
                gameObject,
                environmentServicesHost,
                position,
                size,
                anomalyController,
                config
            )
        );

        if (specialEnvironment == null)
        {
            Debug.LogError(
                $"[AnomalySite] Definition '{power}' did not create an " +
                "environment.",
                this
            );
            specialDefinition = null;
            return false;
        }

        initialized = true;
        BuildBoundary(size, new Color(1f, 0.35f, 0.1f, 0.9f));

        eventPosition = SelectEventPosition();
        return SpawnEvent(true);
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    public bool ContainsWorldPosition(Vector2 position)
    {
        Vector2 offset = position - (Vector2)transform.position;
        Vector2 half = siteSize * 0.5f;
        return initialized && Mathf.Abs(offset.x) <= half.x &&
            Mathf.Abs(offset.y) <= half.y;
    }

    public void SetDebugVisualEmphasis(float multiplier)
    {
        specialEnvironment?.SetDebugVisualEmphasis(multiplier);
    }
#endif

    private bool SpawnEvent(bool suppressStandardReward)
    {
        if (eventSpawner == null || eventPrefab == null)
            return false;

        eventSpawner.EventCompleted -= HandleEventCompleted;
        eventSpawner.EventFailed -= HandleEventFailed;
        eventSpawner.EventCompleted += HandleEventCompleted;
        eventSpawner.EventFailed += HandleEventFailed;

        bool spawned = eventSpawner.SpawnSiteEventAt(
            eventPrefab,
            eventPosition,
            transform.position,
            siteSize,
            suppressStandardReward,
            out activeEvent
        );

        if (!spawned)
        {
            Debug.LogWarning(
                $"[AnomalySite] Could not spawn event at {transform.position}."
            );
        }

        return spawned;
    }

    private Vector2 SelectEventPosition()
    {
        if (eventSpawner == null || eventPrefab == null)
            return transform.position;

        const int Attempts = 64;
        const float EdgeSafety = 1f;
        const float ExitSafety = 1f;
        const float PlayerSafety = 7f;
        float footprint = eventSpawner.GetSiteEventFootprintRadius(
            eventPrefab
        );
        Vector2 half = siteSize * 0.5f;
        Vector2 innerSeventyPercent = siteSize * 0.35f;
        Vector2 available = new(
            Mathf.Min(innerSeventyPercent.x, half.x - footprint - EdgeSafety),
            Mathf.Min(innerSeventyPercent.y, half.y - footprint - EdgeSafety)
        );

        if (available.x <= 0f || available.y <= 0f)
            return transform.position;

        GameObject player = GameObject.FindGameObjectWithTag("Player");
        Vector2 playerPosition = player != null
            ? player.transform.position
            : new Vector2(float.PositiveInfinity, float.PositiveInfinity);
        float minimumCenterOffset = Mathf.Min(siteSize.x, siteSize.y) * 0.16f;

        for (int attempt = 0; attempt < Attempts; attempt++)
        {
            Vector2 local = new(
                Random.Range(-available.x, available.x),
                Random.Range(-available.y, available.y)
            );

            if (local.magnitude < minimumCenterOffset)
                continue;

            Vector2 candidate = (Vector2)transform.position + local;
            if (!IsEventPositionValid(
                    candidate,
                    playerPosition,
                    footprint,
                    PlayerSafety,
                    ExitSafety,
                    EdgeSafety))
                continue;

            return candidate;
        }

        float startAngle = Random.Range(0f, Mathf.PI * 2f);
        for (int i = 0; i < 8; i++)
        {
            float angle = startAngle + i * Mathf.PI * 0.25f;
            Vector2 local = new(
                Mathf.Cos(angle) * available.x * 0.82f,
                Mathf.Sin(angle) * available.y * 0.82f
            );
            Vector2 candidate = (Vector2)transform.position + local;
            if (IsEventPositionValid(
                    candidate,
                    playerPosition,
                    footprint,
                    PlayerSafety,
                    ExitSafety,
                    EdgeSafety))
            {
                return candidate;
            }
        }

        Debug.LogWarning(
            $"[AnomalySite] Could not find varied placement for " +
            $"'{eventPrefab.name}', using Site center.",
            this
        );
        return transform.position;
    }

    private bool IsEventPositionValid(
        Vector2 candidate,
        Vector2 playerPosition,
        float footprint,
        float playerSafety,
        float exitSafety,
        float eventClearance)
    {
        return Vector2.Distance(candidate, playerPosition) >= playerSafety &&
            Vector2.Distance(candidate, exitPosition) >=
                footprint + exitRadius + exitSafety &&
            eventSpawner.IsSiteEventPositionClear(
                eventPrefab,
                candidate,
                eventClearance
            );
    }

    private void HandleEventCompleted(WorldEvent worldEvent)
    {
        if (completed || worldEvent != activeEvent)
            return;

        completed = true;
        activeEvent = null;
        CollapseEnvironment();

        if (isSpecial)
            GrantSpecialPower();
        else
            RunMessageService.Instance?.ShowCustom(
                "ANOMALY STABILIZED",
                "STANDARD REWARD AVAILABLE",
                2f
            );
    }

    private void HandleEventFailed(WorldEvent worldEvent)
    {
        if (completed || worldEvent != activeEvent)
            return;

        activeEvent = null;

        if (worldEvent != null)
            Destroy(worldEvent.gameObject);

        StartCoroutine(RespawnEventAfterDelay());
    }

    private IEnumerator RespawnEventAfterDelay()
    {
        yield return new WaitForSeconds(2f);

        if (!completed)
            SpawnEvent(isSpecial);
    }

    private void GrantSpecialPower()
    {
        if (specialDefinition == null)
        {
            Debug.LogError(
                "[AnomalySite] Cannot grant a special power without a " +
                "Site definition.",
                this
            );
            return;
        }

        AnomalyPowerType powerReward = specialDefinition.PowerReward;
        RunStateManager runState = RunStateManager.Instance;
        bool added = runState != null &&
            runState.TryAddAnomalyPower(powerReward);

        if (added)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            AnomalyPowerRuntime.EnsurePower(player, powerReward);
            RunMessageService.Instance?.ShowCustom(
                "ANOMALY POWER ACQUIRED",
                $"{specialDefinition.PowerDisplayName}  " +
                $"{runState.AnomalyPowers.Count}/3",
                2.5f
            );
        }
        else
        {
            RunMessageService.Instance?.ShowCustom(
                "ANOMALY POWER UNCHANGED",
                "DUPLICATES AND SLOT OVERFLOW ARE DISABLED",
                2.5f
            );
        }
    }

    private void CollapseEnvironment()
    {
        if (anomalyZone != null)
        {
            anomalyController?.CollapseSiteZone(anomalyZone);
            anomalyZone = null;
        }

        if (specialEnvironment != null)
        {
            specialEnvironment.Collapse();
            specialEnvironment = null;
        }

        if (boundary != null)
            boundary.enabled = false;
    }

    private void BuildBoundary(Vector2 size, Color color)
    {
        material = AnomalyPowerVisuals.CreateMaterial(
            "Anomaly Site Runtime Material"
        );
        boundary = AnomalyPowerVisuals.CreateLine(
            transform,
            "Site Boundary",
            color,
            0.08f,
            5,
            material
        );
        boundary.useWorldSpace = false;
        Vector2 half = size * 0.5f;
        boundary.SetPosition(0, new Vector3(-half.x, -half.y));
        boundary.SetPosition(1, new Vector3(-half.x, half.y));
        boundary.SetPosition(2, new Vector3(half.x, half.y));
        boundary.SetPosition(3, new Vector3(half.x, -half.y));
        boundary.SetPosition(4, new Vector3(-half.x, -half.y));
    }

    private void OnDestroy()
    {
        activeSites.Remove(this);

        if (eventSpawner != null)
        {
            eventSpawner.EventCompleted -= HandleEventCompleted;
            eventSpawner.EventFailed -= HandleEventFailed;
        }

        if (material != null)
            Destroy(material);
    }
}
