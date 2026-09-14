#if UNITY_EDITOR || DEVELOPMENT_BUILD
using Random = BotRunSeed.EventRandom;
#endif
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public sealed class ProductionAnomalySite : MonoBehaviour
{
    private static readonly List<ProductionAnomalySite> activeSites = new();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
    internal static event System.Action VisualTargetsChanged;
#endif

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
    private Color originalBoundaryColor;
    private float originalBoundaryWidth;
#endif
    private bool isSpecial;
    private bool completed;
    private bool initialized;
    private int completedMainEvents;
    private bool specialAssaultStarted;
    private bool specialInstructionsShown;
    private float nextAssaultAttempt;
    private EnemySpawner assaultSpawner;
    private Vector2 siteSize;
    private Material material;
    private LineRenderer boundary;
    private Mesh territoryMesh;
    private MeshRenderer territoryFill;

    public bool IsCompleted => completed;
    public bool IsSpecial => isSpecial;
    public int CompletedMainEvents => completedMainEvents;
    public bool HasStartedSpecialAssault => specialAssaultStarted;
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
        {
            activeSites.Add(this);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            VisualTargetsChanged?.Invoke();
#endif
        }

        if (initialized)
            SubscribeToEventSpawner();
    }

    private void OnDisable()
    {
        UnsubscribeFromEventSpawner();
        StopAllCoroutines();

        if (activeSites.Remove(this))
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            VisualTargetsChanged?.Invoke();
#endif
        }
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
        BuildBoundary(size, TerritoryColor(anomaly));
        anomalyZone = anomalyController?.SpawnSiteZone(
            anomaly,
            position,
            size
        );
        eventPosition = SelectEventPosition();
        bool spawned = SpawnEvent(false);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        VisualTargetsChanged?.Invoke();
#endif
        return spawned;
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
        BuildBoundary(size, new Color(0.9f, 0.3f, 0.85f, 0.9f));

        eventPosition = SelectEventPosition();
        bool spawned = SpawnEvent(true);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        VisualTargetsChanged?.Invoke();
#endif
        return spawned;
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    internal WorldEvent SiteEvent => activeEvent;

    public void RemoveForLayout()
    {
        CollapseEnvironment();
        gameObject.SetActive(false);
        if (activeEvent != null)
        {
            activeEvent.gameObject.SetActive(false);
            eventSpawner.ClearDebugEvent(activeEvent);
        }
        Destroy(gameObject);
    }

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

    internal bool HasVisualTuner => ResolveVisualTunable() != null;

    internal string VisualTunerTypeName => ResolveVisualTunable()?.VisualTypeName
        ?? "UNSUPPORTED";

    internal AnomalyVisualTuningCapabilities VisualTunerCapabilities =>
        ResolveVisualTunable()?.VisualCapabilities ??
        AnomalyVisualTuningCapabilities.None;

    internal AnomalyVisualTuningValues VisualTunerValues =>
        ResolveVisualTunable()?.VisualValues ?? default;

    internal int ArtHookRootCount => CountArtHooks(countInstances: false);
    internal int InstantiatedArtHookCount => CountArtHooks(countInstances: true);
    internal bool ArtHooksVisible
    {
        get
        {
            List<AnomalyArtHooks> hooks = CollectArtHooks();
            return hooks.Count == 0 || hooks[0].IsVisible;
        }
    }

    internal void ApplyVisualTunerValues(AnomalyVisualTuningValues values)
    {
        IAnomalyVisualTunable tunable = ResolveVisualTunable();

        if (tunable == null)
            return;

        tunable.ApplyVisualValues(values);
        ApplyBoundaryPresentation(values, tunable.VisualCapabilities);
    }

    internal void SetArtHooksVisible(bool visible)
    {
        List<AnomalyArtHooks> hooks = CollectArtHooks();
        for (int i = 0; i < hooks.Count; i++)
            hooks[i]?.SetVisible(visible);
    }

    internal void ResetVisualTuner()
    {
        IAnomalyVisualTunable tunable = ResolveVisualTunable();
        tunable?.ResetVisualValues();

        if (boundary != null)
        {
            boundary.startColor = originalBoundaryColor;
            boundary.endColor = originalBoundaryColor;
            boundary.startWidth = originalBoundaryWidth;
            boundary.endWidth = originalBoundaryWidth;
            UpdateBoundaryGeometry(1f);
        }
    }

    internal void ApplyVisualTunerPreset(string preset)
    {
        IAnomalyVisualTunable tunable = ResolveVisualTunable();

        if (tunable == null)
            return;

        if (string.Equals(preset, "RESET", System.StringComparison.Ordinal))
        {
            ResetVisualTuner();
            return;
        }

        tunable.ResetVisualValues();
        AnomalyVisualTuningValues values = tunable.VisualValues;

        switch (preset)
        {
            case "CLEAN":
                values.BoundaryWidth *= 0.72f;
                values.InnerLineWidth *= 0.78f;
                values.EdgeGlow *= 0.72f;
                values.PulseStrength *= 0.55f;
                values.PatternSpeed *= 0.7f;
                break;
            case "AGGRESSIVE":
                values.BoundaryWidth *= 1.5f;
                values.InnerLineWidth *= 1.35f;
                values.EdgeGlow *= 1.65f;
                values.PulseSpeed *= 1.5f;
                values.PulseStrength = Mathf.Max(
                    0.35f,
                    values.PulseStrength * 1.6f
                );
                break;
            case "MINIMAL":
                values.BoundaryWidth *= 0.55f;
                values.InnerLineWidth *= 0.55f;
                values.EdgeGlow *= 0.25f;
                values.PulseSpeed = 0f;
                values.PulseStrength = 0f;
                values.PatternSpeed *= 0.25f;
                values.FillAlpha *= 0.45f;
                break;
            default:
                return;
        }

        ApplyVisualTunerValues(values);
    }

    internal string GetVisualTunerValuesText()
    {
        IAnomalyVisualTunable tunable = ResolveVisualTunable();

        return tunable != null
            ? AnomalyVisualTuningFormatter.Format(
                tunable.VisualTypeName,
                tunable.VisualCapabilities,
                tunable.VisualValues
            )
            : "No supported anomaly visual target.";
    }

    private IAnomalyVisualTunable ResolveVisualTunable()
    {
        LocalAnomalyZone zone = AnomalyZone;

        if (zone is IAnomalyVisualTunable zoneTunable)
            return zoneTunable;

        return specialEnvironment as IAnomalyVisualTunable;
    }

    private int CountArtHooks(bool countInstances)
    {
        List<AnomalyArtHooks> hooks = CollectArtHooks();
        int count = 0;
        for (int i = 0; i < hooks.Count; i++)
        {
            if (hooks[i] == null)
                continue;

            count += countInstances
                ? hooks[i].InstantiatedArtCount
                : hooks[i].RootCount;
        }

        return count;
    }

    private List<AnomalyArtHooks> CollectArtHooks()
    {
        List<AnomalyArtHooks> result = new();
        result.AddRange(GetComponentsInChildren<AnomalyArtHooks>(true));

        LocalAnomalyZone zone = AnomalyZone;
        if (zone != null && !zone.transform.IsChildOf(transform))
        {
            result.AddRange(
                zone.GetComponentsInChildren<AnomalyArtHooks>(true));
        }

        return result;
    }

    private void ApplyBoundaryPresentation(
        AnomalyVisualTuningValues values,
        AnomalyVisualTuningCapabilities capabilities)
    {
        if (boundary == null)
            return;

        if ((capabilities & AnomalyVisualTuningCapabilities.PrimaryColor) != 0)
        {
            Color color = values.PrimaryColor;
            float alphaMultiplier =
                (capabilities &
                    AnomalyVisualTuningCapabilities.BoundaryAlpha) != 0
                    ? Mathf.Clamp01(values.BoundaryAlpha)
                    : 1f;
            color.a = originalBoundaryColor.a * alphaMultiplier;
            boundary.startColor = color;
            boundary.endColor = color;
        }

        UpdateBoundaryGeometry(values.VisualScale);
    }

    private void UpdateBoundaryGeometry(float scale)
    {
        if (boundary == null)
            return;

        Vector2 half = siteSize * 0.5f * Mathf.Clamp(scale, 0.25f, 3f);
        boundary.SetPosition(0, new Vector3(-half.x, -half.y));
        boundary.SetPosition(1, new Vector3(-half.x, half.y));
        boundary.SetPosition(2, new Vector3(half.x, half.y));
        boundary.SetPosition(3, new Vector3(half.x, -half.y));
        boundary.SetPosition(4, new Vector3(-half.x, -half.y));
    }
#endif

    private bool SpawnEvent(bool suppressStandardReward)
    {
        if (eventSpawner == null || eventPrefab == null)
            return false;

        SubscribeToEventSpawner();

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

        Transform player = PlayerRuntimeReference.ResolvePlayerTransform(forceLookup: true);
        Vector2 playerPosition = player != null
            ? player.position
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
        if (this == null || worldEvent == null || !isActiveAndEnabled || completed ||
            worldEvent != activeEvent)
            return;

        activeEvent = null;
        completedMainEvents++;
        if (isSpecial && completedMainEvents < 2)
        {
            specialInstructionsShown = true;
            RunMessageService.Instance?.ShowCustom("ОСОБАЯ АНОМАЛИЯ: 1/2",
                "Выполните второе событие. При его запуске начнётся Assault.", 4f);
            eventPosition = SelectEventPosition();
            StartCoroutine(RespawnEventAfterDelay());
            return;
        }

        completed = true;
        CollapseEnvironment();

        if (isSpecial)
        {
            // Completion owns duplicate protection; the existing queue owns
            // cards, staged placement, cancellation and sector-transition gating.
            UpgradeManager.Instance.GrantSpecialAnomalyRing();
        }
        else
            RunMessageService.Instance?.ShowCustom(
                "ANOMALY STABILIZED",
                "STANDARD REWARD AVAILABLE",
                2f
            );
    }

    private void HandleEventFailed(WorldEvent worldEvent)
    {
        if (this == null || worldEvent == null || !isActiveAndEnabled || completed ||
            worldEvent != activeEvent)
            return;

        activeEvent = null;

        if (worldEvent != null)
            Destroy(worldEvent.gameObject);

        StartCoroutine(RespawnEventAfterDelay());
    }

    private IEnumerator RespawnEventAfterDelay()
    {
        do
        {
            yield return new WaitForSeconds(2f);
            if (completed || activeEvent != null) yield break;
        } while (!SpawnEvent(isSpecial) && isSpecial);
    }

    private void Update()
    {
        if (!initialized || !isSpecial || completed) return;
        if (!specialInstructionsShown)
        {
            var player = PlayerRuntimeReference.ResolvePlayerTransform(forceLookup: true);
            if (player != null)
            {
                Vector2 offset = player.position - transform.position;
                if (Mathf.Abs(offset.x) < siteSize.x * 0.5f && Mathf.Abs(offset.y) < siteSize.y * 0.5f)
                {
                    specialInstructionsShown = true;
                    RunMessageService.Instance?.ShowCustom("ОСОБАЯ АНОМАЛИЯ: 0/2",
                        "Выполните два события, чтобы получить новое кольцо.", 4f);
                }
            }
        }
        if (completedMainEvents != 1 || specialAssaultStarted || activeEvent == null ||
            !activeEvent.IsStarted || Time.time < nextAssaultAttempt) return;
        nextAssaultAttempt = Time.time + 1f;
        if (assaultSpawner == null) assaultSpawner = FindFirstObjectByType<EnemySpawner>();
        // A busy/unsafe assault window is retried; failures and event retries never duplicate a launched assault.
        if (assaultSpawner != null && assaultSpawner.TryStartRandomSiteAssault())
            specialAssaultStarted = true;
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
        if (territoryFill != null)
            territoryFill.enabled = false;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        VisualTargetsChanged?.Invoke();
#endif
    }

    private static Color TerritoryColor(LocalAnomalyData anomaly)
    {
        if (anomaly == null) return new Color(0.25f, 0.8f, 0.9f, 0.9f);
        return anomaly.AnomalyType switch
        {
            LocalAnomalyType.Berserk => new Color(1f, 0.35f, 0.3f, 0.9f),
            LocalAnomalyType.Stasis => new Color(0.25f, 0.7f, 1f, 0.9f),
            LocalAnomalyType.ExplosiveZone => new Color(1f, 0.75f, 0.2f, 0.9f),
            LocalAnomalyType.Gravity => new Color(0.65f, 0.5f, 1f, 0.9f),
            LocalAnomalyType.Glitch => new Color(0.35f, 0.95f, 0.6f, 0.9f),
            _ => new Color(0.25f, 0.8f, 0.9f, 0.9f)
        };
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
            0.18f,
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
        // A quiet territory tint makes the partition readable at sector zoom-out.
        // This mesh has no collider and does not participate in anomaly effects.
        Color fillColor = new(color.r, color.g, color.b, 0.2f);
        territoryMesh = new Mesh
        {
            name = "Anomaly territory fill",
            vertices = new[] { new Vector3(-half.x, -half.y), new Vector3(-half.x, half.y),
                new Vector3(half.x, half.y), new Vector3(half.x, -half.y) },
            triangles = new[] { 0, 1, 2, 0, 2, 3 },
            colors = new[] { fillColor, fillColor, fillColor, fillColor }
        };
        territoryMesh.RecalculateBounds();
        var fillObject = new GameObject("Territory tint", typeof(MeshFilter), typeof(MeshRenderer));
        fillObject.transform.SetParent(transform, false);
        fillObject.GetComponent<MeshFilter>().sharedMesh = territoryMesh;
        territoryFill = fillObject.GetComponent<MeshRenderer>();
        territoryFill.sharedMaterial = material;
        territoryFill.sortingLayerName = "Effects";
        territoryFill.sortingOrder = -10;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        originalBoundaryColor = color;
        originalBoundaryWidth = boundary.startWidth;
#endif
    }

    private void OnDestroy()
    {
        if (activeSites.Remove(this))
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            VisualTargetsChanged?.Invoke();
#endif
        }

        UnsubscribeFromEventSpawner();

        if (material != null)
            Destroy(material);
        if (territoryMesh != null)
            Destroy(territoryMesh);
    }

    private void SubscribeToEventSpawner()
    {
        if (eventSpawner == null)
            return;

        eventSpawner.EventCompleted -= HandleEventCompleted;
        eventSpawner.EventFailed -= HandleEventFailed;
        eventSpawner.EventCompleted += HandleEventCompleted;
        eventSpawner.EventFailed += HandleEventFailed;
    }

    private void UnsubscribeFromEventSpawner()
    {
        if (eventSpawner == null)
            return;

        eventSpawner.EventCompleted -= HandleEventCompleted;
        eventSpawner.EventFailed -= HandleEventFailed;
    }
}
