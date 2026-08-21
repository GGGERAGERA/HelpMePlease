using System.Collections.Generic;
using UnityEngine;

public sealed class LevelAnomalyController : MonoBehaviour
{
    public readonly struct LocalAnomalyZoneGeometry
    {
        public LocalAnomalyType Type { get; }
        public Vector2 Center { get; }
        public Vector2 Size { get; }

        public LocalAnomalyZoneGeometry(
            LocalAnomalyType type,
            Vector2 center,
            Vector2 size)
        {
            Type = type;
            Center = center;
            Size = size;
        }
    }

    private readonly struct LocalAnomalyPlacement
    {
        public readonly Vector3 Position;
        public readonly Vector2 Size;
        public readonly LocalAnomalyData Data;

        public LocalAnomalyPlacement(
            Vector3 position,
            Vector2 size,
            LocalAnomalyData data)
        {
            Position = position;
            Size = size;
            Data = data;
        }
    }

    private readonly struct RegionRequest
    {
        public readonly LocalAnomalyData Data;
        public readonly Vector2 Size;

        public RegionRequest(LocalAnomalyData data, Vector2 size)
        {
            Data = data;
            Size = size;
        }
    }

    private sealed class RegionShelf
    {
        public readonly List<RegionRequest> Regions = new();
        public float UsedWidth;
        public float Height;
    }

    private readonly struct ActiveLocalZone
    {
        public readonly Object Source;
        public readonly LocalAnomalyData Data;

        public ActiveLocalZone(Object source, LocalAnomalyData data)
        {
            Source = source;
            Data = data;
        }
    }

    public static LevelAnomalyController Instance { get; private set; }

    [Header("View")]
    [SerializeField] private LocalAnomalyVisual visual;

    [Header("Placement")]
    [SerializeField, Range(3, 5)] private int anomalyCount = 5;
    [SerializeField, Range(0.65f, 0.8f)] private float targetCoverage = 0.7f;
    [SerializeField, Min(0f)] private float edgePadding = 1f;
    [SerializeField] private GameplayAreaService gameplayArea;

    [Header("Production Anomaly Focus")]
    [SerializeField] private SpriteRenderer focusOverlayPrefab;
    [SerializeField] private bool anomalyFocusEnabled = true;
    [SerializeField, Range(0f, 1f)] private float outsideDarkness = 1f;
    [SerializeField, Range(0f, 1f)] private float outsideDesaturation;
    [SerializeField, Range(0.2f, 0.35f)] private float focusTransition = 0.35f;

    private readonly List<LocalAnomalyZone> spawnedZones = new();
    private readonly List<ActiveLocalZone> activeLocalZones = new();
    private readonly HashSet<EnemyHealth> claimedExplosiveDeaths = new();

    private LocalAnomalyData activeAnomaly;
    private bool localCardVisible;
    private LocalAnomalyData displayedLocalAnomaly;
    private Transform focusPlayer;
    private LocalAnomalyZone focusedZone;
    private SpriteRenderer focusOverlay;
    private MaterialPropertyBlock focusProperties;
    private float focusAmount;
    private Bounds focusBounds;
    private bool hasFocusGeometry;
    private bool focusDefaultsCaptured;
    private bool defaultAnomalyFocusEnabled;
    private float defaultOutsideDarkness;
    private float defaultOutsideDesaturation;
    private float defaultFocusTransition;

    private static readonly int FocusAmountId = Shader.PropertyToID("_FocusAmount");
    private static readonly int FocusDarknessId = Shader.PropertyToID("_OutsideDarkness");
    private static readonly int FocusDesaturationId = Shader.PropertyToID("_OutsideDesaturation");
    private static readonly int FocusClearRatioId = Shader.PropertyToID("_ClearRatio");

    public LocalAnomalyData ActiveAnomaly => activeAnomaly;
    public bool IsIntroComplete { get; private set; } = true;
    public float CurrentCoverage { get; private set; }
    public bool IsAnomalyFocusActive => focusAmount > 0f;
    public string FocusedZoneName => focusedZone != null
        ? focusedZone.AnomalyType.ToString()
        : "NONE";
    public bool AnomalyFocusEnabled => anomalyFocusEnabled;
    public float OutsideDarkness => outsideDarkness;
    public float OutsideDesaturation => outsideDesaturation;
    public float OutsideColor => 1f - outsideDesaturation;
    public float FocusTransition => focusTransition;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    public void SetAnomalyFocusEnabled(bool value) => anomalyFocusEnabled = value;
    public void SetOutsideDarkness(float value) => outsideDarkness = Mathf.Clamp01(value);
    public void SetOutsideDesaturation(float value) => outsideDesaturation = Mathf.Clamp01(value);
    public void SetOutsideColor(float value) => outsideDesaturation = 1f - Mathf.Clamp01(value);
    public void SetFocusTransition(float value) => focusTransition = Mathf.Clamp(value, 0.2f, 0.35f);
    public void ResetFocusPresentationForDebug()
    {
        CaptureFocusPresentationDefaults();
        anomalyFocusEnabled = defaultAnomalyFocusEnabled;
        outsideDarkness = defaultOutsideDarkness;
        outsideDesaturation = defaultOutsideDesaturation;
        focusTransition = defaultFocusTransition;
    }
#endif

    public bool TryClaimExplosiveDeath(EnemyHealth enemy)
    {
        return enemy != null && claimedExplosiveDeaths.Add(enemy);
    }

    public bool IsPositionInsideActiveZone(Vector2 position)
    {
        for (int i = 0; i < spawnedZones.Count; i++)
        {
            LocalAnomalyZone zone = spawnedZones[i];

            if (zone == null || !zone.isActiveAndEnabled)
                continue;

            BoxCollider2D collider = zone.GetComponent<BoxCollider2D>();

            if (collider != null && collider.enabled &&
                collider.OverlapPoint(position))
            {
                return true;
            }
        }

        return false;
    }

    public void ResetExplosiveDeathClaim(EnemyHealth enemy)
    {
        if (!ReferenceEquals(enemy, null))
            claimedExplosiveDeaths.Remove(enemy);
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        CaptureFocusPresentationDefaults();
    }

    private void OnEnable()
    {
        // OnDisable clears transient zones and releases the scene singleton.
        // Re-enable restores discoverability without recreating cleared state.
        if (Instance == null)
            Instance = this;
    }

    private void CaptureFocusPresentationDefaults()
    {
        if (focusDefaultsCaptured)
            return;

        focusDefaultsCaptured = true;
        defaultAnomalyFocusEnabled = anomalyFocusEnabled;
        defaultOutsideDarkness = outsideDarkness;
        defaultOutsideDesaturation = outsideDesaturation;
        defaultFocusTransition = focusTransition;
    }

    private void Update()
    {
        UpdateAnomalyFocus();
    }

    private void UpdateAnomalyFocus()
    {
        if (focusPlayer == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            focusPlayer = player != null ? player.transform : null;
        }

        LocalAnomalyZone next = anomalyFocusEnabled && focusPlayer != null
            ? FindFocusZone(focusPlayer.position)
            : null;
        if (next != null)
        {
            focusedZone = next;
            Collider2D nextArea = next.FocusArea;
            if (nextArea != null && nextArea.enabled)
            {
                focusBounds = nextArea.bounds;
                hasFocusGeometry = true;
            }
        }

        float target = next != null ? 1f : 0f;
        focusAmount = Mathf.MoveTowards(
            focusAmount,
            target,
            Time.unscaledDeltaTime / Mathf.Max(0.01f, focusTransition)
        );

        if (!hasFocusGeometry || (focusAmount <= 0f && next == null))
        {
            focusedZone = null;
            hasFocusGeometry = false;
            if (focusOverlay != null)
                focusOverlay.enabled = false;
            return;
        }

        EnsureFocusOverlay();
        if (focusOverlay == null)
            return;

        Bounds bounds = focusBounds;
        Vector2 overlaySize = new(
            Mathf.Max(1f, bounds.size.x + 80f),
            Mathf.Max(1f, bounds.size.y + 80f)
        );
        focusOverlay.transform.position = new Vector3(
            bounds.center.x,
            bounds.center.y,
            0f
        );
        focusOverlay.transform.localScale = overlaySize;

        focusProperties.Clear();
        focusProperties.SetFloat(FocusAmountId, focusAmount);
        focusProperties.SetFloat(FocusDarknessId, outsideDarkness);
        focusProperties.SetFloat(FocusDesaturationId, outsideDesaturation);
        focusProperties.SetVector(
            FocusClearRatioId,
            new Vector4(
                bounds.size.x / overlaySize.x,
                bounds.size.y / overlaySize.y,
                0f,
                0f
            )
        );
        focusOverlay.SetPropertyBlock(focusProperties);
        focusOverlay.enabled = true;

        if (next == null && focusAmount <= 0f)
        {
            focusedZone = null;
            hasFocusGeometry = false;
        }
    }

    private LocalAnomalyZone FindFocusZone(Vector2 position)
    {
        LocalAnomalyZone best = null;
        float bestArea = float.MaxValue;

        for (int i = 0; i < spawnedZones.Count; i++)
        {
            LocalAnomalyZone zone = spawnedZones[i];
            if (zone == null || !zone.isActiveAndEnabled)
                continue;

            Collider2D area = zone.FocusArea;
            if (area == null || !area.enabled || !area.OverlapPoint(position))
                continue;

            float areaSize = area.bounds.size.x * area.bounds.size.y;
            if (areaSize < bestArea)
            {
                best = zone;
                bestArea = areaSize;
            }
        }

        return best;
    }

    private void EnsureFocusOverlay()
    {
        if (focusOverlay != null)
            return;

        if (focusOverlayPrefab == null)
        {
            Debug.LogError(
                "[LevelAnomalyController] Focus overlay prefab is not assigned.",
                this);
            return;
        }

        focusProperties = new MaterialPropertyBlock();
        focusOverlay = Instantiate(focusOverlayPrefab, transform);
        focusOverlay.name = focusOverlayPrefab.name;
        focusOverlay.enabled = false;
    }

    public void Apply(LocalAnomalyData anomaly)
    {
        Clear();
        IsIntroComplete = false;

        if (anomaly == null)
        {
            IsIntroComplete = true;
            return;
        }

        activeAnomaly = anomaly;
        visual?.Apply(anomaly);

        if (anomaly.ZonePrefab != null)
            SpawnLocalAnomalyZones(anomaly);

        IsIntroComplete = true;
    }

    public void BeginSiteLayout()
    {
        Clear();
        IsIntroComplete = true;
    }

    public LocalAnomalyZone SpawnSiteZone(
        LocalAnomalyData data,
        Vector2 position,
        Vector2 size)
    {
        if (data == null || data.ZonePrefab == null)
            return null;

        ResolveGameplayArea();

        Vector2 safeSize = new(
            Mathf.Max(0.1f, size.x),
            Mathf.Max(0.1f, size.y)
        );

        if (gameplayArea == null ||
            !IsRectangleInsidePlayableArea(position, safeSize))
        {
            Debug.LogWarning(
                $"[LevelAnomalyController] Site zone '{data.name}' " +
                "does not fit inside PlayableArea.",
                this
            );
            return null;
        }

        LocalAnomalyZone zone = Instantiate(
            data.ZonePrefab,
            position,
            Quaternion.identity
        );
        zone.Initialize(data, this, safeSize);
        spawnedZones.Add(zone);
        return zone;
    }

    public void CollapseSiteZone(LocalAnomalyZone zone)
    {
        if (zone == null || !spawnedZones.Remove(zone))
            return;

        RemoveActiveLocalZone(zone);
        RefreshLocalAnomalyCard();
        zone.Despawn();
    }

    public void Clear()
    {
        CleanupLocalAnomalyZones();
        activeAnomaly = null;
        displayedLocalAnomaly = null;
        localCardVisible = false;
        visual?.Clear();
        IsIntroComplete = true;
    }

    public void CollectActiveLocalZones(
        List<LocalAnomalyZoneGeometry> result)
    {
        if (result == null)
            return;

        result.Clear();

        for (int i = 0; i < spawnedZones.Count; i++)
        {
            LocalAnomalyZone zone = spawnedZones[i];

            if (zone == null || !zone.isActiveAndEnabled)
                continue;

            BoxCollider2D collider =
                zone.GetComponent<BoxCollider2D>();

            if (collider == null || !collider.enabled)
                continue;

            Vector2 center = collider.transform.TransformPoint(
                collider.offset
            );
            Vector3 scale = collider.transform.lossyScale;
            Vector2 size = Vector2.Scale(
                collider.size,
                new Vector2(Mathf.Abs(scale.x), Mathf.Abs(scale.y))
            );

            if (size.x <= Mathf.Epsilon || size.y <= Mathf.Epsilon)
                continue;

            result.Add(new LocalAnomalyZoneGeometry(
                zone.AnomalyType,
                center,
                size
            ));
        }
    }

    public void NotifyLocalZoneEntered(
        Object zone,
        LocalAnomalyData data)
    {
        if (zone == null || data == null)
            return;

        RemoveActiveLocalZone(zone);
        activeLocalZones.Add(new ActiveLocalZone(zone, data));
        RefreshLocalAnomalyCard();
    }

    public void NotifyLocalZoneExited(Object zone)
    {
        if (zone == null || !RemoveActiveLocalZone(zone))
            return;

        RefreshLocalAnomalyCard();
    }

    private bool RemoveActiveLocalZone(Object zone)
    {
        for (int i = activeLocalZones.Count - 1; i >= 0; i--)
        {
            if (!ReferenceEquals(activeLocalZones[i].Source, zone))
                continue;

            activeLocalZones.RemoveAt(i);
            return true;
        }

        return false;
    }

    private void RefreshLocalAnomalyCard()
    {
        for (int i = activeLocalZones.Count - 1; i >= 0; i--)
        {
            if (activeLocalZones[i].Source == null)
                activeLocalZones.RemoveAt(i);
        }

        if (activeLocalZones.Count == 0)
        {
            visual?.Hide();
            displayedLocalAnomaly = null;
            localCardVisible = false;
            return;
        }

        LocalAnomalyData data =
            activeLocalZones[activeLocalZones.Count - 1].Data;

        if (localCardVisible && displayedLocalAnomaly == data)
            return;

        displayedLocalAnomaly = data;
        localCardVisible = true;
        visual?.Show(data);
    }

    private void SpawnLocalAnomalyZones(LocalAnomalyData rootData)
    {
        ResolveGameplayArea();

        if (gameplayArea == null || gameplayArea.PlayableArea == null)
        {
            Debug.LogWarning(
                "[LevelAnomalyController] PlayableArea is missing. " +
                "Continuing without local anomalies.",
                this
            );
            return;
        }

        List<LocalAnomalyData> zoneData = BuildZoneData(rootData);

        if (zoneData.Count == 0)
            return;

        zoneData.Sort((left, right) =>
            left.AnomalyType.CompareTo(right.AnomalyType));

        int count = Mathf.Clamp(anomalyCount, 3, 5);
        List<LocalAnomalyData> regionData = new(count);
        float baseArea = 0f;

        for (int i = 0; i < count; i++)
        {
            LocalAnomalyData data = zoneData[i % zoneData.Count];
            regionData.Add(data);
            Vector2 baseSize = data.ZoneSize;
            baseArea += baseSize.x * baseSize.y;
        }

        Bounds bounds = gameplayArea.PlayableArea.bounds;
        float playableArea = bounds.size.x * bounds.size.y;

        if (baseArea <= Mathf.Epsilon || playableArea <= Mathf.Epsilon)
            return;

        float scale = Mathf.Sqrt(
            playableArea * Mathf.Clamp(targetCoverage, 0.65f, 0.8f) /
            baseArea
        );
        List<LocalAnomalyPlacement> placements = null;

        for (int attempt = 0; attempt < 16; attempt++)
        {
            if (TryBuildRegionPlacements(
                    regionData,
                    bounds,
                    scale,
                    out placements))
            {
                break;
            }

            scale *= 0.97f;
        }

        if (placements == null || placements.Count == 0)
        {
            Debug.LogWarning(
                "[LevelAnomalyController] Rectangular anomaly regions " +
                "do not fit inside PlayableArea.",
                this
            );
            return;
        }

        float coveredArea = 0f;

        for (int i = 0; i < placements.Count; i++)
        {
            LocalAnomalyPlacement placement = placements[i];

            LocalAnomalyZone zone = Instantiate(
                placement.Data.ZonePrefab,
                placement.Position,
                Quaternion.identity
            );
            zone.Initialize(
                placement.Data,
                this,
                placement.Size
            );
            spawnedZones.Add(zone);
            coveredArea += placement.Size.x * placement.Size.y;
        }

        CurrentCoverage = coveredArea / playableArea;
    }

    private static List<LocalAnomalyData> BuildZoneData(
        LocalAnomalyData rootData)
    {
        List<LocalAnomalyData> result = new();
        AddZoneData(result, rootData);

        LocalAnomalyData[] additional = rootData.AdditionalAnomalies;

        if (additional == null)
            return result;

        for (int i = 0; i < additional.Length; i++)
            AddZoneData(result, additional[i]);

        return result;
    }

    private static void AddZoneData(
        List<LocalAnomalyData> result,
        LocalAnomalyData data)
    {
        if (data == null || data.ZonePrefab == null || result.Contains(data))
            return;

        result.Add(data);
    }

    private bool TryBuildRegionPlacements(
        List<LocalAnomalyData> regionData,
        Bounds bounds,
        float scale,
        out List<LocalAnomalyPlacement> placements)
    {
        placements = null;
        float padding = Mathf.Max(0f, edgePadding);
        float availableWidth = bounds.size.x - padding * 2f;
        float availableHeight = bounds.size.y - padding * 2f;

        if (availableWidth <= 0f || availableHeight <= 0f)
            return false;

        List<RegionRequest> requests = new(regionData.Count);

        for (int i = 0; i < regionData.Count; i++)
        {
            LocalAnomalyData data = regionData[i];
            float zoneSizeMultiplier = RunStateManager.Instance != null
                ? RunStateManager.Instance.AnomalyModifiers.ZoneSizeMultiplier
                : 1f;
            requests.Add(new RegionRequest(
                data,
                data.ZoneSize * scale * zoneSizeMultiplier));
        }

        requests.Sort((left, right) =>
        {
            int heightOrder = right.Size.y.CompareTo(left.Size.y);
            return heightOrder != 0
                ? heightOrder
                : right.Size.x.CompareTo(left.Size.x);
        });

        List<RegionShelf> shelves = new();

        for (int i = 0; i < requests.Count; i++)
        {
            RegionRequest request = requests[i];
            bool added = false;

            for (int shelfIndex = 0;
                 shelfIndex < shelves.Count;
                 shelfIndex++)
            {
                RegionShelf shelf = shelves[shelfIndex];

                if (request.Size.y <= shelf.Height + 0.001f &&
                    shelf.UsedWidth + request.Size.x <=
                    availableWidth + 0.001f)
                {
                    shelf.Regions.Add(request);
                    shelf.UsedWidth += request.Size.x;
                    added = true;
                    break;
                }
            }

            if (added)
                continue;

            if (request.Size.x > availableWidth + 0.001f ||
                request.Size.y > availableHeight + 0.001f)
            {
                return false;
            }

            RegionShelf newShelf = new()
            {
                UsedWidth = request.Size.x,
                Height = request.Size.y
            };
            newShelf.Regions.Add(request);
            shelves.Add(newShelf);
        }

        float usedHeight = 0f;

        for (int i = 0; i < shelves.Count; i++)
            usedHeight += shelves[i].Height;

        if (usedHeight > availableHeight + 0.001f)
            return false;

        if (shelves.Count == 3)
        {
            RegionShelf middle = shelves[2];
            shelves.RemoveAt(2);
            shelves.Insert(1, middle);
        }

        placements = new List<LocalAnomalyPlacement>(regionData.Count);
        float top = bounds.max.y - padding;

        for (int shelfIndex = 0;
             shelfIndex < shelves.Count;
             shelfIndex++)
        {
            RegionShelf shelf = shelves[shelfIndex];
            float left = bounds.min.x + padding;
            float centerY = top - shelf.Height * 0.5f;

            for (int regionIndex = 0;
                 regionIndex < shelf.Regions.Count;
                 regionIndex++)
            {
                RegionRequest request = shelf.Regions[regionIndex];
                Vector2 center = new(
                    left + request.Size.x * 0.5f,
                    centerY
                );

                if (!IsRectangleInsidePlayableArea(center, request.Size))
                {
                    placements = null;
                    return false;
                }

                placements.Add(new LocalAnomalyPlacement(
                    center,
                    request.Size,
                    request.Data
                ));
                left += request.Size.x;
            }

            top -= shelf.Height;
        }

        return true;
    }

    private bool IsRectangleInsidePlayableArea(
        Vector2 center,
        Vector2 size)
    {
        Vector2 halfSize = size * 0.5f;

        return gameplayArea.IsInsidePlayableArea(
                center + new Vector2(-halfSize.x, -halfSize.y)) &&
            gameplayArea.IsInsidePlayableArea(
                center + new Vector2(-halfSize.x, halfSize.y)) &&
            gameplayArea.IsInsidePlayableArea(
                center + new Vector2(halfSize.x, -halfSize.y)) &&
            gameplayArea.IsInsidePlayableArea(
                center + new Vector2(halfSize.x, halfSize.y));
    }

    private void CleanupLocalAnomalyZones()
    {
        for (int i = 0; i < spawnedZones.Count; i++)
        {
            LocalAnomalyZone zone = spawnedZones[i];

            if (zone != null)
                zone.Despawn();
        }

        spawnedZones.Clear();
        activeLocalZones.Clear();
        claimedExplosiveDeaths.Clear();
        CurrentCoverage = 0f;
        visual?.Hide();
    }

    private void ResolveGameplayArea()
    {
        if (gameplayArea == null)
            gameplayArea = GameplayAreaService.Instance;

        if (gameplayArea == null)
            gameplayArea = FindFirstObjectByType<GameplayAreaService>();
    }

    private void OnDisable()
    {
        Clear();
        focusedZone = null;
        hasFocusGeometry = false;
        focusAmount = 0f;
        if (focusOverlay != null)
            focusOverlay.enabled = false;

        if (Instance == this)
            Instance = null;
    }

}
