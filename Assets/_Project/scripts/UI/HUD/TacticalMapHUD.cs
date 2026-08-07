using System.Collections.Generic;
using TMPro;
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
    private const int GridCellCount = 8;
    private const float AnomalyRefreshInterval = 0.5f;
    private const float MarkerRefreshInterval = 0.1f;

    private static readonly Color FrameColor =
        new(0.008f, 0.025f, 0.032f, 0.84f);
    private static readonly Color Cyan =
        new(0.12f, 0.82f, 0.92f, 0.92f);
    private static readonly Color GridColor =
        new(0.08f, 0.38f, 0.43f, 0.2f);
    private static readonly Color LegendTextColor =
        new(0.78f, 0.84f, 0.86f, 0.95f);
    private static readonly Color EventFill =
        new(0.1f, 0.75f, 0.86f, 0.95f);
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

    private RectTransform mapRoot;
    private RectTransform mapFrame;
    private RectTransform projectionRoot;
    private RectTransform anomalyRoot;
    private RectTransform eventRoot;
    private RectTransform legendRoot;
    private RectTransform playerLegendRow;
    private RectTransform normalSiteLegendRow;
    private RectTransform specialSiteLegendRow;
    private RectTransform exitLegendRow;
    private RectTransform eventLegendRow;
    private RectTransform bossLegendRow;
    private MarkerVisual playerMarker;
    private MarkerVisual exitMarker;
    private MarkerVisual bossMarker;
    private GameplayAreaService gameplayArea;
    private LevelAnomalyController anomalyController;
    private WorldEventSpawner eventSpawner;
    private Transform player;
    private Bounds worldBounds;
    private bool hasBounds;
    private bool isVisible;
    private bool hasVisibleEvents;
    private bool hasVisibleBoss;
    private bool hasVisibleNormalSite;
    private bool hasVisibleSpecialSite;
    private bool hasVisibleExit;
    private float currentMapHeight = MaxMapSize;
    private float nextAnomalyRefresh;
    private float nextMarkerRefresh;

    private readonly List<LevelAnomalyController.LocalAnomalyZoneGeometry>
        anomalyZones = new();
    private readonly List<MarkerVisual> anomalyMarkers = new();
    private readonly List<MarkerVisual> eventMarkers = new();

    public bool IsVisible => isVisible;

    private void Awake()
    {
        BuildUI();
        SetVisible(visibleByDefault);
    }

    private void Start()
    {
        ResolveSceneReferences();
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

    private void BuildUI()
    {
        Canvas canvas = GetComponentInParent<Canvas>();
        Transform parent = canvas != null ? canvas.transform : transform;

        mapRoot = CreateRect("TacticalMapRoot", parent);
        mapRoot.anchorMin = mapRoot.anchorMax = new Vector2(1f, 1f);
        mapRoot.pivot = new Vector2(1f, 1f);
        mapRoot.anchoredPosition = new Vector2(-24f, -24f);

        mapFrame = CreateRect("MapFrame", mapRoot);
        mapFrame.anchorMin = mapFrame.anchorMax = new Vector2(1f, 1f);
        mapFrame.pivot = new Vector2(1f, 1f);
        mapFrame.anchoredPosition = Vector2.zero;

        Image background = mapFrame.gameObject.AddComponent<Image>();
        background.color = FrameColor;
        background.raycastTarget = false;

        Outline frameBorder = mapFrame.gameObject.AddComponent<Outline>();
        frameBorder.effectColor = Cyan;
        frameBorder.effectDistance = new Vector2(1f, -1f);
        frameBorder.useGraphicAlpha = true;

        projectionRoot = CreateRect("ProjectionRoot", mapFrame);
        Stretch(projectionRoot);
        BuildGrid(projectionRoot);

        anomalyRoot = CreateRect("Anomaly Zones", projectionRoot);
        Stretch(anomalyRoot);

        eventRoot = CreateRect("Events", projectionRoot);
        Stretch(eventRoot);

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

        legendRoot = CreateRect("LegendRoot", mapRoot);
        legendRoot.anchorMin = legendRoot.anchorMax = new Vector2(1f, 1f);
        legendRoot.pivot = new Vector2(1f, 1f);

        playerLegendRow = CreateLegendRow(
            "ИГРОК",
            new Color(0.94f, 0.98f, 1f, 1f),
            Cyan
        );
        normalSiteLegendRow = CreateLegendRow(
            "ОБЫЧНАЯ АНОМАЛИЯ",
            NormalSiteFill,
            NormalSiteBorder
        );
        specialSiteLegendRow = CreateLegendRow(
            "ОСОБАЯ АНОМАЛИЯ",
            SpecialSiteFill,
            SpecialSiteBorder
        );
        exitLegendRow = CreateLegendRow(
            "ВЫХОД",
            ExitFill,
            ExitBorder,
            45f
        );
        eventLegendRow = CreateLegendRow("ИСПЫТАНИЕ", EventFill, Cyan);
        bossLegendRow = CreateLegendRow("БОСС", BossFill, BossBorder);

        ApplyMapLayout(MaxMapSize, MaxMapSize);
        RefreshLegend();
    }

    private void BuildGrid(Transform parent)
    {
        RectTransform gridRoot = CreateRect("Grid", parent);
        Stretch(gridRoot);

        for (int i = 1; i < GridCellCount; i++)
        {
            float normalized = (float)i / GridCellCount;

            RectTransform vertical = CreateRect("Grid V " + i, gridRoot);
            vertical.anchorMin = new Vector2(normalized, 0f);
            vertical.anchorMax = new Vector2(normalized, 1f);
            vertical.pivot = new Vector2(0.5f, 0.5f);
            vertical.anchoredPosition = Vector2.zero;
            vertical.sizeDelta = new Vector2(1f, 0f);
            AddDecorativeImage(vertical.gameObject, GridColor);

            RectTransform horizontal = CreateRect("Grid H " + i, gridRoot);
            horizontal.anchorMin = new Vector2(0f, normalized);
            horizontal.anchorMax = new Vector2(1f, normalized);
            horizontal.pivot = new Vector2(0.5f, 0.5f);
            horizontal.anchoredPosition = Vector2.zero;
            horizontal.sizeDelta = new Vector2(0f, 1f);
            AddDecorativeImage(horizontal.gameObject, GridColor);
        }
    }

    private RectTransform CreateLegendRow(
        string label,
        Color fill,
        Color border,
        float markerRotation = 0f)
    {
        RectTransform row = CreateRect("Legend " + label, legendRoot);
        row.anchorMin = new Vector2(0f, 1f);
        row.anchorMax = new Vector2(1f, 1f);
        row.pivot = new Vector2(0f, 1f);
        row.sizeDelta = new Vector2(0f, LegendRowHeight);

        MarkerVisual swatch = CreateMarker("Swatch", row);
        swatch.Rect.anchorMin = swatch.Rect.anchorMax = new Vector2(0f, 1f);
        swatch.Rect.pivot = new Vector2(0f, 1f);
        swatch.Rect.anchoredPosition = new Vector2(3f, -6f);
        swatch.Rect.sizeDelta = new Vector2(8f, 8f);
        swatch.Rect.localRotation = Quaternion.Euler(
            0f,
            0f,
            markerRotation
        );
        SetMarkerStyle(swatch, fill, border);

        RectTransform textRect = CreateRect("Label", row);
        textRect.anchorMin = new Vector2(0f, 0f);
        textRect.anchorMax = new Vector2(1f, 1f);
        textRect.offsetMin = new Vector2(19f, 0f);
        textRect.offsetMax = Vector2.zero;

        TextMeshProUGUI text = textRect.gameObject.AddComponent<TextMeshProUGUI>();
        text.font = TMP_Settings.defaultFontAsset;
        text.fontSize = 13f;
        text.fontStyle = FontStyles.Normal;
        text.alignment = TextAlignmentOptions.MidlineLeft;
        text.color = LegendTextColor;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.overflowMode = TextOverflowModes.Truncate;
        text.raycastTarget = false;
        text.text = label;
        return row;
    }

    private void ResolveSceneReferences()
    {
        gameplayArea ??= GameplayAreaService.Instance;
        gameplayArea ??= FindFirstObjectByType<GameplayAreaService>();
        anomalyController ??= LevelAnomalyController.Instance;
        anomalyController ??= FindFirstObjectByType<LevelAnomalyController>();
        eventSpawner ??= FindFirstObjectByType<WorldEventSpawner>();
    }

    private void ResolvePlayer()
    {
        GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
        player = playerObject != null ? playerObject.transform : null;
    }

    private void RefreshBounds(bool force)
    {
        if (gameplayArea == null)
            ResolveSceneReferences();

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

        if (anomalyController == null)
            ResolveSceneReferences();

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
        if (eventSpawner == null)
            ResolveSceneReferences();

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
            hasVisibleEvents || hasVisibleBoss;
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
        LayoutLegendRow(bossLegendRow, hasVisibleBoss, ref rowIndex);

        float legendHeight = rowIndex * LegendRowHeight;
        legendRoot.sizeDelta = new Vector2(MaxMapSize, legendHeight);
        mapRoot.sizeDelta = new Vector2(
            MaxMapSize,
            currentMapHeight + (showLegend ? LegendGap + legendHeight : 0f)
        );
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

    private static Image AddDecorativeImage(
        GameObject target,
        Color color)
    {
        Image image = target.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return image;
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

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }
}
