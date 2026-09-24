using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public sealed class TacticalMapHUD : MonoBehaviour
{
    [SerializeField] private float MaxMapSize = 220f;
    private const float AnomalyRefreshInterval = 0.5f;
    private const float MarkerRefreshInterval = 0.1f;

    [SerializeField] private Color Cyan =
        new(0.12f, 0.82f, 0.92f, 0.92f);
    [SerializeField] private Color EventFill =
        new(0.1f, 0.75f, 0.86f, 0.95f);
    [SerializeField] private Color BreakableFill =
        new(0.96f, 0.58f, 0.14f, 0.92f);
    [SerializeField] private Color BreakableBorder =
        new(1f, 0.86f, 0.42f, 1f);
    [SerializeField] private Color NormalSiteFill =
        new(0.08f, 0.68f, 0.9f, 0.2f);
    [SerializeField] private Color NormalSiteBorder =
        new(0.18f, 0.88f, 1f, 0.95f);
    [SerializeField] private Color SpecialSiteFill =
        new(0.72f, 0.12f, 0.95f, 0.26f);
    [SerializeField] private Color SpecialSiteBorder =
        new(1f, 0.28f, 0.95f, 1f);
    [SerializeField] private Color ExitFill =
        new(0.18f, 1f, 0.42f, 0.95f);
    [SerializeField] private Color ExitBorder =
        new(0.62f, 1f, 0.72f, 1f);
    [SerializeField] private Color BossFill =
        new(0.95f, 0.12f, 0.08f, 0.95f);
    [SerializeField] private Color BossBorder =
        new(1f, 0.75f, 0.1f, 1f);

    [SerializeField] private bool visibleByDefault = true;

    [SerializeField] private RectTransform mapRoot;
    public RectTransform LayoutRoot => mapRoot;
    [SerializeField] private RectTransform mapFrame;
    [SerializeField] private RectTransform projectionRoot;
    [SerializeField] private RectTransform anomalyRoot;
    [SerializeField] private RectTransform eventRoot;
    [SerializeField] private RectTransform breakableRoot;
    [SerializeField] private TacticalMapMarker playerMarker;
    [SerializeField] private TacticalMapMarker exitMarker;
    [SerializeField] private TacticalMapMarker bossMarker;
    [SerializeField] private GameplayAreaService gameplayArea;
    [SerializeField] private LevelAnomalyController anomalyController;
    [SerializeField] private WorldEventSpawner eventSpawner;
    [Header("Authored territory layer")]
    [SerializeField] private Image[] territoryAreas;
    [SerializeField] private Color normalAreaColor = new(.16f,.48f,.62f,.22f);
    [SerializeField] private Color specialAreaColor = new(.48f,.28f,.65f,.25f);
    private Transform player;
    private Bounds worldBounds;
    private bool hasBounds;
    private bool isVisible;

    private float nextAnomalyRefresh;
    private float nextMarkerRefresh;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
    private int lastLoggedBreakableMarkerCount = -1;
#endif

    private readonly List<LevelAnomalyController.LocalAnomalyZoneGeometry>
        anomalyZones = new();
    private readonly List<TacticalMapMarker> eventMarkers = new();
    private readonly List<TacticalMapMarker> breakableMarkers = new();
    private readonly List<TacticalMapMarkerDescriptor> breakableDescriptors =
        new();

    public bool IsVisible => isVisible;

    private void Awake()
    {
        if (mapRoot == null || mapFrame == null || projectionRoot == null ||
            anomalyRoot == null || eventRoot == null || breakableRoot == null ||
            playerMarker == null || exitMarker == null || bossMarker == null || gameplayArea == null ||
            anomalyController == null || eventSpawner == null)
        {
            Debug.LogError("[TacticalMapHUD] Authored shell or scene references are missing.", this);
            enabled = false;
            if (mapRoot != null) mapRoot.gameObject.SetActive(false);
            return;
        }
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

    private void HandleBreakableMarkersChanged() { nextMarkerRefresh = 0f; }

    private void ResolvePlayer()
    {
        player = PlayerRuntimeReference.ResolvePlayerTransform(forceLookup: true);
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
        mapFrame.sizeDelta = new Vector2(mapWidth, mapHeight);
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
        foreach (var area in territoryAreas) area.gameObject.SetActive(false);
        if (!hasBounds) return;
        int count = 0;
        foreach (var site in ProductionAnomalySite.ActiveSites)
        {
            if (site == null || !site.IsMapVisible) continue;
            ShowTerritory(count++, site.transform.position, site.SiteSize, site.IsSpecial);
        }
        if (count > 0) return;
        anomalyZones.Clear();
        anomalyController?.CollectActiveLocalZones(anomalyZones);
        for (int i = 0; i < anomalyZones.Count; i++)
        {
            var zone = anomalyZones[i];
            ShowTerritory(i, zone.Center, zone.Size, false);
        }
    }

    private void ShowTerritory(int index, Vector2 center, Vector2 size, bool special)
    {
        if (index >= territoryAreas.Length) return;
        var area = territoryAreas[index];
        area.gameObject.SetActive(true);
        area.color = special ? specialAreaColor : normalAreaColor;
        area.rectTransform.anchoredPosition = WorldToMap(center);
        var mapSize = projectionRoot.rect.size;
        area.rectTransform.sizeDelta = new Vector2(
            size.x / worldBounds.size.x * mapSize.x,
            size.y / worldBounds.size.y * mapSize.y);
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
                TacticalMapMarker marker = eventMarkers[eventCount];
                marker.Rect.anchoredPosition = WorldToMap(
                    worldEvent.transform.position
                );
                marker.Rect.sizeDelta = eventSize;
                marker.Rect.localRotation = Quaternion.identity;
                SetMarkerStyle(marker, EventFill, Cyan);
                eventCount++;
            }
        }

        var portals = ProductionExplorationSectorController.ActiveInstance?.PortalPair;
        if (hasBounds && portals != null && portals.isActiveAndEnabled)
        {
            for (int i = 0; i < 2; i++)
            {
                EnsureMarkerCount(eventMarkers, eventCount + 1, "Portal", eventRoot);
                TacticalMapMarker marker = eventMarkers[eventCount++];
                marker.Rect.anchoredPosition = WorldToMap(i == 0 ? portals.PositionA : portals.PositionB);
                marker.Rect.sizeDelta = portalSize;
                marker.Rect.localRotation = Quaternion.Euler(0f, 0f, 45f);
                Color color = i == 0 ? ProductionPortalPair.ColorA : ProductionPortalPair.ColorB;
                SetMarkerStyle(marker, color * new Color(1f, 1f, 1f, 0.45f), color);
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
            foreach (WorldLootChest chest in WorldLootChest.ActiveInstances)
                chest?.CollectTacticalMapMarkers(breakableDescriptors);
        }

        EnsureMarkerCount(
            breakableMarkers,
            breakableDescriptors.Count,
            "Breakable",
            breakableRoot
        );

        for (int i = 0; i < breakableDescriptors.Count; i++)
        {
            TacticalMapMarker marker = breakableMarkers[i];
            marker.Rect.anchoredPosition = WorldToMap(
                breakableDescriptors[i].Position
            );
            marker.Rect.sizeDelta = containerSize;
            marker.Rect.localRotation = Quaternion.identity;
            SetMarkerStyle(marker, BreakableFill, BreakableBorder);
        }
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
                SetMarkerStyle(exitMarker,
                    sectorExit.IsAvailable ? ExitFill : lockedExitFill,
                    sectorExit.IsAvailable ? ExitBorder : lockedExitBorder);
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

    private void EnsureMarkerCount(
        List<TacticalMapMarker> markers,
        int required,
        string prefix,
        Transform parent)
    {
        while (markers.Count < required)
        {
            TacticalMapMarker marker = CreateMarker(
                prefix + " " + markers.Count,
                parent
            );
            marker.Rect.gameObject.SetActive(false);
            markers.Add(marker);
        }

        SetMarkerCount(markers, required);
    }

    private static void SetMarkerCount(
        List<TacticalMapMarker> markers,
        int activeCount)
    {
        for (int i = 0; i < markers.Count; i++)
            markers[i].Rect.gameObject.SetActive(i < activeCount);
    }

    private static void SetMarkerStyle(
        TacticalMapMarker marker,
        Color fill,
        Color border)
    {
        marker.Fill.color = fill;
        marker.Border.effectColor = border;
        marker.Border.enabled = border.a > 0f;
    }

    private void GetAnomalyColors(LocalAnomalyType type, out Color fill, out Color border)
    {
        int index = (int)type;
        border = index >= 0 && index < anomalyColors.Length ? anomalyColors[index] : defaultAnomalyColor;
        fill = border;
        fill.a = anomalyOpacity;
    }

    private static bool BoundsApproximatelyEqual(Bounds left, Bounds right) =>
        (left.center - right.center).sqrMagnitude < 0.0001f &&
        (left.size - right.size).sqrMagnitude < 0.0001f;

    [SerializeField] private TacticalMapMarker anomalyPrefab;
    [SerializeField] private TacticalMapMarker eventPrefab;
    [SerializeField] private TacticalMapMarker containerPrefab;
    [SerializeField] private Vector2 anomalySize = new(16f, 16f);
    [SerializeField] private Vector2 eventSize = new(16f, 16f);
    [SerializeField] private Vector2 portalSize = new(16f, 16f);
    [SerializeField] private Vector2 containerSize = new(12f, 12f);
    [SerializeField] private Color lockedExitFill = new(.25f, .18f, .1f, .8f);
    [SerializeField] private Color lockedExitBorder = new(.6f, .34f, .12f, .8f);
    [SerializeField] private Color[] anomalyColors;
    [SerializeField] private Color defaultAnomalyColor = new(.2f, .8f, .9f, .85f);
    [SerializeField] private float anomalyOpacity = .19f;
    private TacticalMapMarker CreateMarker(string markerName, Transform parent)
    {
        var prefab = parent == anomalyRoot ? anomalyPrefab : parent == breakableRoot ? containerPrefab : eventPrefab;
        return Instantiate(prefab, parent);
    }
}
