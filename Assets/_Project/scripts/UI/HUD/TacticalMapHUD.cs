using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public sealed class TacticalMapHUD : MonoBehaviour
{
    private sealed class MarkerVisual
    {
        public RectTransform Rect { get; }
        public Image Fill { get; }
        public Outline Border { get; }

        public MarkerVisual(
            RectTransform rect,
            Image fill,
            Outline border)
        {
            Rect = rect;
            Fill = fill;
            Border = border;
        }
    }

    private const float MaxMapSize = 220f;
    private const float LegendGap = 8f;
    private const float LegendRowHeight = 20f;
    private const float AnomalyRefreshInterval = 0.5f;
    private const float MarkerRefreshInterval = 0.1f;

    private static readonly Color Cyan =
        new(0.12f, 0.82f, 0.92f, 0.92f);
    private static readonly Color EventFill =
        new(0.1f, 0.75f, 0.86f, 0.95f);
    private static readonly Color BreakableFill =
        new(0.96f, 0.58f, 0.14f, 0.92f);
    private static readonly Color BreakableBorder =
        new(1f, 0.86f, 0.42f, 1f);
    private static readonly Color NormalSiteFill =
        new(0.08f, 0.68f, 0.9f, 0.2f);
    private static readonly Color NormalSiteBorder =
        new(0.18f, 0.88f, 1f, 0.95f);
    private static readonly Color SpecialSiteFill =
        new(0.72f, 0.12f, 0.95f, 0.26f);
    private static readonly Color SpecialSiteBorder =
        new(1f, 0.28f, 0.95f, 1f);
    private static readonly Color ExitFill =
        new(0.18f, 1f, 0.42f, 0.95f);
    private static readonly Color ExitBorder =
        new(0.62f, 1f, 0.72f, 1f);
    private static readonly Color BossFill =
        new(0.95f, 0.12f, 0.08f, 0.95f);
    private static readonly Color BossBorder =
        new(1f, 0.75f, 0.1f, 1f);

    [SerializeField] private bool visibleByDefault = true;

    [SerializeField] private RectTransform mapRoot;
    [SerializeField] private RectTransform mapFrame;
    [SerializeField] private RectTransform projectionRoot;
    [SerializeField] private RectTransform anomalyRoot;
    [SerializeField] private RectTransform eventRoot;
    [SerializeField] private RectTransform breakableRoot;
    [SerializeField] private RectTransform legendRoot;
    [SerializeField] private RectTransform playerLegendRow;
    [SerializeField] private RectTransform normalSiteLegendRow;
    [SerializeField] private RectTransform specialSiteLegendRow;
    [SerializeField] private RectTransform exitLegendRow;
    [SerializeField] private RectTransform eventLegendRow;
    [SerializeField] private RectTransform breakableLegendRow;
    [SerializeField] private RectTransform bossLegendRow;
    private MarkerVisual playerMarker;
    private MarkerVisual exitMarker;
    private MarkerVisual bossMarker;
    [SerializeField] private GameplayAreaService gameplayArea;
    [SerializeField] private LevelAnomalyController anomalyController;
    [SerializeField] private WorldEventSpawner eventSpawner;
    private Transform player;
    private Bounds worldBounds;
    private bool hasBounds;
    private bool isVisible;
    private bool hasVisibleEvents;
    private bool hasVisibleBreakables;
    private bool hasVisibleBoss;
    private bool hasVisibleNormalSite;
    private bool hasVisibleSpecialSite;
    private bool hasVisibleExit;
    private float currentMapHeight = MaxMapSize;
    private float nextAnomalyRefresh;
    private float nextMarkerRefresh;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
    private int lastLoggedBreakableMarkerCount = -1;
#endif

    private readonly List<LevelAnomalyController.LocalAnomalyZoneGeometry>
        anomalyZones = new();
    private readonly List<MarkerVisual> anomalyMarkers = new();
    private readonly List<MarkerVisual> eventMarkers = new();
    private readonly List<MarkerVisual> breakableMarkers = new();
    private readonly List<TacticalMapMarkerDescriptor> breakableDescriptors =
        new();

    public bool IsVisible => isVisible;

    private void Awake()
    {
        if (mapRoot == null || mapFrame == null || projectionRoot == null ||
            anomalyRoot == null || eventRoot == null || breakableRoot == null ||
            legendRoot == null || playerLegendRow == null || normalSiteLegendRow == null ||
            specialSiteLegendRow == null || exitLegendRow == null || eventLegendRow == null ||
            breakableLegendRow == null || bossLegendRow == null || gameplayArea == null ||
            anomalyController == null || eventSpawner == null)
        {
            Debug.LogError("[TacticalMapHUD] Authored shell or scene references are missing.", this);
            enabled = false;
            if (mapRoot != null) mapRoot.gameObject.SetActive(false);
            return;
        }
        CreateRuntimeMarkers();
        ApplyMapLayout(MaxMapSize, MaxMapSize);
        SetVisible(visibleByDefault);
    }

    private void OnEnable()
    {
        WorldBreakable.MarkerStateChanged += HandleBreakableMarkersChanged;
    }

    private void OnDisable()
    {
        WorldBreakable.MarkerStateChanged -= HandleBreakableMarkersChanged;
    }

    private void Start()
    {
        ResolvePlayer();
        RefreshBounds(true);
        RefreshAnomalies();
        RefreshMarkers();
    }

    private void Update()
    {
        if (!isVisible)
            return;

        if (player == null)
            ResolvePlayer();

        RefreshBounds(false);
        UpdatePlayerMarker();

        float now = Time.unscaledTime;

        if (now >= nextAnomalyRefresh)
        {
            nextAnomalyRefresh = now + AnomalyRefreshInterval;
            RefreshAnomalies();
        }

        if (now >= nextMarkerRefresh)
        {
            nextMarkerRefresh = now + MarkerRefreshInterval;
            RefreshMarkers();
        }
    }

    public void BindPlayer(Transform target)
    {
        player = target;
        UpdatePlayerMarker();
    }

    public void SetVisible(bool visible)
    {
        isVisible = visible;

        if (mapRoot != null)
            mapRoot.gameObject.SetActive(visible);

        if (visible)
        {
            nextAnomalyRefresh = 0f;
            nextMarkerRefresh = 0f;
        }
    }

    // Marker instances remain dynamic and are reused for the lifetime of this HUD.
    private void CreateRuntimeMarkers()
    {
        bossMarker = CreateMarker("Boss", projectionRoot);
        bossMarker.Rect.sizeDelta = new Vector2(10f, 10f);
        SetMarkerStyle(bossMarker, BossFill, BossBorder);
        bossMarker.Rect.gameObject.SetActive(false);

        exitMarker = CreateMarker("Sector Exit", projectionRoot);
        exitMarker.Rect.sizeDelta = new Vector2(10f, 10f);
        exitMarker.Rect.localRotation = Quaternion.Euler(0f, 0f, 45f);
        SetMarkerStyle(exitMarker, ExitFill, ExitBorder);
        exitMarker.Rect.gameObject.SetActive(false);

        playerMarker = CreateMarker("Player", projectionRoot);
        playerMarker.Rect.sizeDelta = new Vector2(8f, 8f);
        SetMarkerStyle(
            playerMarker,
            new Color(0.94f, 0.98f, 1f, 1f),
            Cyan
        );
        playerMarker.Rect.SetAsLastSibling();

    }

    private void ResolvePlayer()
    {
        GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
        player = playerObject != null ? playerObject.transform : null;
    }

    private void RefreshBounds(bool force)
    {
        Collider2D playable = gameplayArea != null
            ? gameplayArea.PlayableArea
            : null;

        if (playable == null || !playable.enabled)
        {
            hasBounds = false;
            return;
        }

        Bounds bounds = playable.bounds;

        if (!force && hasBounds && BoundsApproximatelyEqual(worldBounds, bounds))
            return;

        worldBounds = bounds;
        hasBounds = bounds.size.x > Mathf.Epsilon &&
            bounds.size.y > Mathf.Epsilon;

        if (!hasBounds)
            return;

        float aspect = bounds.size.x / bounds.size.y;
        Vector2 mapSize = aspect >= 1f
            ? new Vector2(MaxMapSize, MaxMapSize / aspect)
            : new Vector2(MaxMapSize * aspect, MaxMapSize);
        ApplyMapLayout(mapSize.x, mapSize.y);
        RefreshAnomalies();
        RefreshMarkers();
        UpdatePlayerMarker();
    }

    private void ApplyMapLayout(float mapWidth, float mapHeight)
    {
        currentMapHeight = mapHeight;
        mapFrame.sizeDelta = new Vector2(mapWidth, mapHeight);
        legendRoot.anchoredPosition = new Vector2(0f, -mapHeight - LegendGap);
        RefreshLegend();
    }

    private void UpdatePlayerMarker()
    {
        if (playerMarker == null)
            return;

        bool available = hasBounds && player != null;
        playerMarker.Rect.gameObject.SetActive(available);

        if (available)
            playerMarker.Rect.anchoredPosition = WorldToMap(player.position);
    }

    private void RefreshAnomalies()
    {
        hasVisibleNormalSite = false;
        hasVisibleSpecialSite = false;

        if (!hasBounds)
        {
            SetMarkerCount(anomalyMarkers, 0);
            return;
        }

        IReadOnlyList<ProductionAnomalySite> productionSites =
            ProductionAnomalySite.ActiveSites;
        int productionMarkerCount = 0;

        for (int i = 0; i < productionSites.Count; i++)
        {
            ProductionAnomalySite site = productionSites[i];

            if (site == null || !site.IsMapVisible)
                continue;

            EnsureMarkerCount(
                anomalyMarkers,
                productionMarkerCount + 1,
                "Production Site",
                anomalyRoot
            );
            MarkerVisual marker = anomalyMarkers[productionMarkerCount];
            marker.Rect.anchoredPosition = WorldToMap(
                site.transform.position
            );
            marker.Rect.sizeDelta = WorldSizeToMap(site.SiteSize);
            marker.Rect.localRotation = Quaternion.identity;

            if (site.IsSpecial)
            {
                SetMarkerStyle(
                    marker,
                    SpecialSiteFill,
                    SpecialSiteBorder
                );
                hasVisibleSpecialSite = true;
            }
            else
            {
                SetMarkerStyle(
                    marker,
                    NormalSiteFill,
                    NormalSiteBorder
                );
                hasVisibleNormalSite = true;
            }

            productionMarkerCount++;
        }

        if (productionMarkerCount > 0)
        {
            SetMarkerCount(anomalyMarkers, productionMarkerCount);
            return;
        }

        anomalyZones.Clear();
        anomalyController?.CollectActiveLocalZones(anomalyZones);
        EnsureMarkerCount(
            anomalyMarkers,
            anomalyZones.Count,
            "Anomaly",
            anomalyRoot
        );

        for (int i = 0; i < anomalyZones.Count; i++)
        {
            LevelAnomalyController.LocalAnomalyZoneGeometry zone =
                anomalyZones[i];
            MarkerVisual marker = anomalyMarkers[i];
            marker.Rect.anchoredPosition = WorldToMap(zone.Center);
            marker.Rect.sizeDelta = WorldSizeToMap(zone.Size);
            marker.Rect.localRotation = Quaternion.identity;
            GetAnomalyColors(zone.Type, out Color fill, out Color border);
            SetMarkerStyle(marker, fill, border);
        }

        hasVisibleNormalSite = anomalyZones.Count > 0;
    }

    private void RefreshMarkers()
    {
        int eventCount = 0;
        IReadOnlyList<WorldEvent> events = eventSpawner != null
            ? eventSpawner.SpawnedEvents
            : null;

        if (hasBounds && events != null)
        {
            for (int i = 0; i < events.Count; i++)
            {
                WorldEvent worldEvent = events[i];

                if (worldEvent == null || worldEvent.IsCompleted ||
                    !worldEvent.gameObject.activeInHierarchy)
                {
                    continue;
                }

                EnsureMarkerCount(
                    eventMarkers,
                    eventCount + 1,
                    "Event",
                    eventRoot
                );
                MarkerVisual marker = eventMarkers[eventCount];
                marker.Rect.anchoredPosition = WorldToMap(
                    worldEvent.transform.position
                );
                marker.Rect.sizeDelta = new Vector2(8f, 8f);
                marker.Rect.localRotation = Quaternion.identity;
                SetMarkerStyle(marker, EventFill, Cyan);
                eventCount++;
            }
        }

        SetMarkerCount(eventMarkers, eventCount);

        breakableDescriptors.Clear();
        if (hasBounds)
        {
            foreach (WorldBreakable breakable in
                WorldBreakable.ActiveInstances)
            {
                breakable?.CollectTacticalMapMarkers(breakableDescriptors);
            }
        }

        EnsureMarkerCount(
            breakableMarkers,
            breakableDescriptors.Count,
            "Breakable",
            breakableRoot
        );

        for (int i = 0; i < breakableDescriptors.Count; i++)
        {
            MarkerVisual marker = breakableMarkers[i];
            marker.Rect.anchoredPosition = WorldToMap(
                breakableDescriptors[i].Position
            );
            marker.Rect.sizeDelta = new Vector2(6f, 6f);
            marker.Rect.localRotation = Quaternion.identity;
            SetMarkerStyle(marker, BreakableFill, BreakableBorder);
        }

        hasVisibleExit = false;
        exitMarker.Rect.gameObject.SetActive(false);
        IReadOnlyList<ProductionSectorExit> exits =
            ProductionSectorExit.ActiveExits;

        if (hasBounds)
        {
            for (int i = 0; i < exits.Count; i++)
            {
                ProductionSectorExit sectorExit = exits[i];

                if (sectorExit == null || !sectorExit.IsMapVisible)
                    continue;

                exitMarker.Rect.anchoredPosition = WorldToMap(
                    sectorExit.transform.position
                );
                exitMarker.Rect.gameObject.SetActive(true);
                hasVisibleExit = true;
                break;
            }
        }

        EnemyHealth boss = FindAliveBoss();
        bool showBoss = hasBounds && boss != null;
        bossMarker.Rect.gameObject.SetActive(showBoss);

        if (showBoss)
        {
            bossMarker.Rect.anchoredPosition = WorldToMap(
                boss.transform.position
            );
        }

        hasVisibleEvents = eventCount > 0;
        hasVisibleBreakables = breakableDescriptors.Count > 0;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (lastLoggedBreakableMarkerCount != breakableDescriptors.Count)
        {
            lastLoggedBreakableMarkerCount = breakableDescriptors.Count;
            Debug.Log(
                $"[TacticalMap] breakableMarkers=" +
                $"{breakableDescriptors.Count}",
                this
            );
        }
#endif
        hasVisibleBoss = showBoss;
        RefreshLegend();
        playerMarker?.Rect.SetAsLastSibling();
    }

    private static EnemyHealth FindAliveBoss()
    {
        foreach (EnemyHealth enemy in EnemyHealth.ActiveInstances)
        {
            if (enemy != null && enemy.IsBoss && !enemy.IsDead &&
                enemy.isActiveAndEnabled && enemy.gameObject.activeInHierarchy)
            {
                return enemy;
            }
        }

        return null;
    }

    private void RefreshLegend()
    {
        if (legendRoot == null || mapRoot == null)
            return;

        bool showLegend = hasBounds && player != null ||
            hasVisibleNormalSite || hasVisibleSpecialSite || hasVisibleExit ||
            hasVisibleEvents || hasVisibleBreakables || hasVisibleBoss;
        legendRoot.gameObject.SetActive(showLegend);

        int rowIndex = 0;
        LayoutLegendRow(
            playerLegendRow,
            hasBounds && player != null,
            ref rowIndex
        );
        LayoutLegendRow(
            normalSiteLegendRow,
            hasVisibleNormalSite,
            ref rowIndex
        );
        LayoutLegendRow(
            specialSiteLegendRow,
            hasVisibleSpecialSite,
            ref rowIndex
        );
        LayoutLegendRow(exitLegendRow, hasVisibleExit, ref rowIndex);
        LayoutLegendRow(eventLegendRow, hasVisibleEvents, ref rowIndex);
        LayoutLegendRow(
            breakableLegendRow,
            hasVisibleBreakables,
            ref rowIndex
        );
        LayoutLegendRow(bossLegendRow, hasVisibleBoss, ref rowIndex);

        float legendHeight = rowIndex * LegendRowHeight;
        legendRoot.sizeDelta = new Vector2(MaxMapSize, legendHeight);
        mapRoot.sizeDelta = new Vector2(
            MaxMapSize,
            currentMapHeight + (showLegend ? LegendGap + legendHeight : 0f)
        );
    }

    private void HandleBreakableMarkersChanged()
    {
        nextMarkerRefresh = 0f;
    }

    private static void LayoutLegendRow(
        RectTransform row,
        bool visible,
        ref int rowIndex)
    {
        if (row == null)
            return;

        row.gameObject.SetActive(visible);

        if (!visible)
            return;

        row.anchoredPosition = new Vector2(
            0f,
            -rowIndex * LegendRowHeight
        );
        rowIndex++;
    }

    private Vector2 WorldToMap(Vector2 worldPosition)
    {
        Rect rect = projectionRoot.rect;
        float x = Mathf.InverseLerp(
            worldBounds.min.x,
            worldBounds.max.x,
            worldPosition.x
        );
        float y = Mathf.InverseLerp(
            worldBounds.min.y,
            worldBounds.max.y,
            worldPosition.y
        );
        return new Vector2(
            (x - 0.5f) * rect.width,
            (y - 0.5f) * rect.height
        );
    }

    private Vector2 WorldSizeToMap(Vector2 worldSize)
    {
        Rect rect = projectionRoot.rect;
        return new Vector2(
            Mathf.Max(2f, worldSize.x / worldBounds.size.x * rect.width),
            Mathf.Max(2f, worldSize.y / worldBounds.size.y * rect.height)
        );
    }

    private static void EnsureMarkerCount(
        List<MarkerVisual> markers,
        int required,
        string prefix,
        Transform parent)
    {
        while (markers.Count < required)
        {
            MarkerVisual marker = CreateMarker(
                prefix + " " + markers.Count,
                parent
            );
            marker.Rect.gameObject.SetActive(false);
            markers.Add(marker);
        }

        SetMarkerCount(markers, required);
    }

    private static void SetMarkerCount(
        List<MarkerVisual> markers,
        int activeCount)
    {
        for (int i = 0; i < markers.Count; i++)
            markers[i].Rect.gameObject.SetActive(i < activeCount);
    }

    private static void SetMarkerStyle(
        MarkerVisual marker,
        Color fill,
        Color border)
    {
        marker.Fill.color = fill;
        marker.Border.effectColor = border;
        marker.Border.enabled = border.a > 0f;
    }

    private static void GetAnomalyColors(
        LocalAnomalyType type,
        out Color fill,
        out Color border)
    {
        border = type switch
        {
            LocalAnomalyType.Berserk => new Color(1f, 0.12f, 0.1f, 0.9f),
            LocalAnomalyType.Stasis => new Color(0.12f, 0.48f, 1f, 0.9f),
            LocalAnomalyType.ExplosiveZone =>
                new Color(1f, 0.3f, 0.05f, 0.9f),
            LocalAnomalyType.Gravity => new Color(0.6f, 0.22f, 1f, 0.9f),
            LocalAnomalyType.Glitch => new Color(1f, 0.12f, 0.82f, 0.9f),
            _ => new Color(0.2f, 0.8f, 0.9f, 0.85f)
        };
        fill = new Color(border.r, border.g, border.b, 0.19f);
    }

    private static bool BoundsApproximatelyEqual(Bounds left, Bounds right) =>
        (left.center - right.center).sqrMagnitude < 0.0001f &&
        (left.size - right.size).sqrMagnitude < 0.0001f;

    private static MarkerVisual CreateMarker(
        string markerName,
        Transform parent)
    {
        RectTransform rect = CreateRect(markerName, parent);
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);

        Image image = rect.gameObject.AddComponent<Image>();
        image.raycastTarget = false;

        Outline outline = rect.gameObject.AddComponent<Outline>();
        outline.effectDistance = new Vector2(1f, -1f);
        outline.useGraphicAlpha = false;
        return new MarkerVisual(rect, image, outline);
    }

    private static RectTransform CreateRect(
        string objectName,
        Transform parent)
    {
        GameObject gameObject = new(objectName, typeof(RectTransform));
        gameObject.layer = 5;
        RectTransform rect = gameObject.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        return rect;
    }

}
