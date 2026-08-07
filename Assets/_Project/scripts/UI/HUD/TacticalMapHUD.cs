using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public sealed class TacticalMapHUD : MonoBehaviour
{
    private const float MaxMapSize = 220f;
    private const float StaticRefreshInterval = 0.5f;
    private const float EventRefreshInterval = 0.1f;

    [SerializeField] private bool visibleByDefault = true;

    private RectTransform mapRoot;
    private RectTransform contentRoot;
    private RectTransform playerMarker;
    private GameplayAreaService gameplayArea;
    private LevelAnomalyController anomalyController;
    private WorldEventSpawner eventSpawner;
    private Transform player;
    private Bounds worldBounds;
    private bool hasBounds;
    private bool isVisible;
    private float nextStaticRefresh;
    private float nextEventRefresh;

    private readonly List<LevelAnomalyController.LocalAnomalyZoneGeometry>
        anomalyZones = new();
    private readonly List<TacticalMapMarkerDescriptor> eventDescriptors = new();
    private readonly List<RectTransform> anomalyMarkers = new();
    private readonly List<RectTransform> eventMarkers = new();

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
        RefreshEvents();
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

        if (now >= nextStaticRefresh)
        {
            nextStaticRefresh = now + StaticRefreshInterval;
            RefreshAnomalies();
        }

        if (now >= nextEventRefresh)
        {
            nextEventRefresh = now + EventRefreshInterval;
            RefreshEvents();
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
            nextStaticRefresh = 0f;
            nextEventRefresh = 0f;
        }
    }

    private void BuildUI()
    {
        Canvas canvas = GetComponentInParent<Canvas>();
        Transform parent = canvas != null ? canvas.transform : transform;

        GameObject rootObject = new(
            "TacticalMap",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image),
            typeof(Outline)
        );
        mapRoot = rootObject.GetComponent<RectTransform>();
        mapRoot.SetParent(parent, false);
        mapRoot.anchorMin = mapRoot.anchorMax = new Vector2(1f, 1f);
        mapRoot.pivot = new Vector2(1f, 1f);
        mapRoot.anchoredPosition = new Vector2(-24f, -112f);
        mapRoot.sizeDelta = new Vector2(MaxMapSize, MaxMapSize);

        Image background = rootObject.GetComponent<Image>();
        background.color = new Color(0.015f, 0.035f, 0.05f, 0.68f);
        background.raycastTarget = false;
        Outline outline = rootObject.GetComponent<Outline>();
        outline.effectColor = new Color(0.15f, 0.82f, 0.92f, 0.8f);
        outline.effectDistance = new Vector2(1.25f, -1.25f);

        contentRoot = CreateRect("Map Content", mapRoot);
        Stretch(contentRoot, 0f);

        playerMarker = CreateMarker(
            "Player",
            contentRoot,
            new Color(0.75f, 1f, 1f, 1f)
        );
        playerMarker.sizeDelta = new Vector2(8f, 8f);
        playerMarker.SetAsLastSibling();
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
        mapRoot.sizeDelta = aspect >= 1f
            ? new Vector2(MaxMapSize, MaxMapSize / aspect)
            : new Vector2(MaxMapSize * aspect, MaxMapSize);
        RefreshAnomalies();
        RefreshEvents();
        UpdatePlayerMarker();
    }

    private void UpdatePlayerMarker()
    {
        if (playerMarker == null)
            return;

        bool available = hasBounds && player != null;
        playerMarker.gameObject.SetActive(available);

        if (available)
            playerMarker.anchoredPosition = WorldToMap(player.position);
    }

    private void RefreshAnomalies()
    {
        if (!hasBounds)
        {
            SetMarkerCount(anomalyMarkers, 0);
            return;
        }

        if (anomalyController == null)
            ResolveSceneReferences();

        anomalyZones.Clear();
        anomalyController?.CollectActiveLocalZones(anomalyZones);
        EnsureMarkerCount(anomalyMarkers, anomalyZones.Count, "Anomaly");

        for (int i = 0; i < anomalyZones.Count; i++)
        {
            LevelAnomalyController.LocalAnomalyZoneGeometry zone =
                anomalyZones[i];
            RectTransform marker = anomalyMarkers[i];
            marker.anchoredPosition = WorldToMap(zone.Center);
            marker.sizeDelta = WorldSizeToMap(zone.Size);
            marker.localRotation = Quaternion.identity;
            marker.GetComponent<Image>().color = GetAnomalyColor(zone.Type);
        }
    }

    private void RefreshEvents()
    {
        if (!hasBounds)
        {
            SetMarkerCount(eventMarkers, 0);
            return;
        }

        if (eventSpawner == null)
            ResolveSceneReferences();

        eventDescriptors.Clear();
        IReadOnlyList<WorldEvent> events = eventSpawner != null
            ? eventSpawner.SpawnedEvents
            : null;

        if (events != null)
        {
            for (int i = 0; i < events.Count; i++)
            {
                WorldEvent worldEvent = events[i];

                if (worldEvent is ITacticalMapMarkerProvider provider)
                    provider.CollectTacticalMapMarkers(eventDescriptors);
            }
        }

        EnsureMarkerCount(eventMarkers, eventDescriptors.Count, "Event");

        for (int i = 0; i < eventDescriptors.Count; i++)
        {
            TacticalMapMarkerDescriptor descriptor = eventDescriptors[i];
            RectTransform marker = eventMarkers[i];
            marker.anchoredPosition = WorldToMap(descriptor.Position);
            marker.sizeDelta = descriptor.IsArea
                ? WorldSizeToMap(descriptor.Size)
                : GetMarkerSize(descriptor.Kind);
            marker.localRotation = Quaternion.Euler(0f, 0f, descriptor.Rotation);
            marker.GetComponent<Image>().color = GetEventColor(descriptor.Kind);
        }

        playerMarker?.SetAsLastSibling();
    }

    private Vector2 WorldToMap(Vector2 worldPosition)
    {
        Rect rect = contentRoot.rect;
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
        Rect rect = contentRoot.rect;
        return new Vector2(
            Mathf.Max(2f, worldSize.x / worldBounds.size.x * rect.width),
            Mathf.Max(2f, worldSize.y / worldBounds.size.y * rect.height)
        );
    }

    private void EnsureMarkerCount(
        List<RectTransform> markers,
        int required,
        string prefix)
    {
        while (markers.Count < required)
        {
            RectTransform marker = CreateMarker(
                prefix + " " + markers.Count,
                contentRoot,
                Color.white
            );
            markers.Add(marker);
        }

        SetMarkerCount(markers, required);
    }

    private static void SetMarkerCount(
        List<RectTransform> markers,
        int activeCount)
    {
        for (int i = 0; i < markers.Count; i++)
            markers[i].gameObject.SetActive(i < activeCount);
    }

    private static Color GetAnomalyColor(LocalAnomalyType type) => type switch
    {
        LocalAnomalyType.Berserk => new Color(1f, 0.12f, 0.1f, 0.3f),
        LocalAnomalyType.Stasis => new Color(0.1f, 0.45f, 1f, 0.3f),
        LocalAnomalyType.ExplosiveZone => new Color(1f, 0.3f, 0.05f, 0.3f),
        LocalAnomalyType.Gravity => new Color(0.55f, 0.2f, 1f, 0.3f),
        LocalAnomalyType.Glitch => new Color(1f, 0.1f, 0.85f, 0.3f),
        _ => new Color(0.2f, 0.8f, 0.9f, 0.25f)
    };

    private static Color GetEventColor(TacticalMapMarkerKind kind) => kind switch
    {
        TacticalMapMarkerKind.Target => new Color(1f, 0.2f, 0.2f, 0.95f),
        TacticalMapMarkerKind.Objective => new Color(1f, 0.85f, 0.15f, 0.9f),
        TacticalMapMarkerKind.Corridor => new Color(0.15f, 0.9f, 1f, 0.28f),
        _ => new Color(0.2f, 0.9f, 1f, 0.9f)
    };

    private static Vector2 GetMarkerSize(TacticalMapMarkerKind kind) =>
        kind == TacticalMapMarkerKind.Target
            ? new Vector2(9f, 9f)
            : new Vector2(7f, 7f);

    private static bool BoundsApproximatelyEqual(Bounds left, Bounds right) =>
        (left.center - right.center).sqrMagnitude < 0.0001f &&
        (left.size - right.size).sqrMagnitude < 0.0001f;

    private static RectTransform CreateMarker(
        string markerName,
        Transform parent,
        Color color)
    {
        RectTransform marker = CreateRect(markerName, parent);
        marker.anchorMin = marker.anchorMax = new Vector2(0.5f, 0.5f);
        marker.pivot = new Vector2(0.5f, 0.5f);
        Image image = marker.gameObject.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return marker;
    }

    private static RectTransform CreateRect(string objectName, Transform parent)
    {
        GameObject gameObject = new(objectName, typeof(RectTransform));
        RectTransform rect = gameObject.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        return rect;
    }

    private static void Stretch(RectTransform rect, float padding)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(padding, padding);
        rect.offsetMax = new Vector2(-padding, -padding);
    }
}
