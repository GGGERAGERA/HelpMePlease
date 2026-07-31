using System.Collections.Generic;
using UnityEngine;

public sealed class LevelAnomalyController : MonoBehaviour
{
    private const int LocalAnomalyPositionAttempts = 64;

    public readonly struct LocalAnomalyZoneGeometry
    {
        public LocalAnomalyType Type { get; }
        public Vector2 Center { get; }
        public float Radius { get; }

        public LocalAnomalyZoneGeometry(
            LocalAnomalyType type,
            Vector2 center,
            float radius)
        {
            Type = type;
            Center = center;
            Radius = radius;
        }
    }

    private readonly struct LocalAnomalyPlacement
    {
        public readonly Vector3 Position;
        public readonly float Radius;

        public LocalAnomalyPlacement(Vector3 position, float radius)
        {
            Position = position;
            Radius = radius;
        }
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
    [SerializeField, Range(1, 2)] private int anomalyCount = 1;
    [SerializeField, Min(0f)] private float edgePadding = 1f;
    [SerializeField, Min(0f)] private float minimumDistanceFromPlayerStart = 5f;
    [SerializeField, Min(0f)] private float minimumDistanceBetweenAnomalies = 2f;
    [SerializeField] private GameplayAreaService gameplayArea;

    private readonly List<LocalAnomalyZone> spawnedZones = new();
    private readonly List<ActiveLocalZone> activeLocalZones = new();

    private LocalAnomalyData activeAnomaly;
    private bool localCardVisible;
    private LocalAnomalyData displayedLocalAnomaly;

    public LocalAnomalyData ActiveAnomaly => activeAnomaly;
    public bool IsIntroComplete { get; private set; } = true;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
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

            CircleCollider2D collider =
                zone.GetComponent<CircleCollider2D>();

            if (collider == null || !collider.enabled)
                continue;

            Vector2 center = collider.transform.TransformPoint(
                collider.offset
            );
            Vector3 scale = collider.transform.lossyScale;
            float radius = collider.radius * Mathf.Max(
                Mathf.Abs(scale.x),
                Mathf.Abs(scale.y)
            );

            if (radius <= Mathf.Epsilon)
                continue;

            result.Add(new LocalAnomalyZoneGeometry(
                zone.AnomalyType,
                center,
                radius
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

        if (gameplayArea == null)
        {
            Debug.LogWarning(
                "[LevelAnomalyController] GameplayAreaService is " +
                "missing. Continuing without local anomalies.",
                this
            );
            return;
        }

        GameObject playerObject =
            GameObject.FindGameObjectWithTag("Player");

        if (playerObject == null)
        {
            Debug.LogWarning(
                "[LevelAnomalyController] Player was not found. " +
                "Continuing without local anomalies.",
                this
            );
            return;
        }

        List<LocalAnomalyData> zoneData = BuildZoneData(rootData);

        if (zoneData.Count == 0)
            return;

        Vector3 playerStart = playerObject.transform.position;
        List<LocalAnomalyPlacement> placements = new();
        int count = Mathf.Clamp(anomalyCount, 1, 2);

        for (int i = 0; i < count; i++)
        {
            LocalAnomalyData data = zoneData[i % zoneData.Count];
            float radius = data.ZoneRadius;

            if (!TryGetLocalAnomalyPosition(
                    playerStart,
                    radius,
                    placements,
                    out Vector3 position))
            {
                Debug.LogWarning(
                    "[LevelAnomalyController] No valid position was " +
                    $"found for local anomaly {i + 1}.",
                    this
                );
                break;
            }

            LocalAnomalyZone zone = Instantiate(
                data.ZonePrefab,
                position,
                Quaternion.identity
            );
            zone.Initialize(data, this);
            spawnedZones.Add(zone);
            placements.Add(new LocalAnomalyPlacement(position, radius));
        }
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

    private bool TryGetLocalAnomalyPosition(
        Vector3 playerStart,
        float radius,
        List<LocalAnomalyPlacement> existingPlacements,
        out Vector3 position)
    {
        position = default;

        if (gameplayArea == null || gameplayArea.SpawnArea == null)
            return false;

        float requiredPlayerDistance =
            radius + Mathf.Max(0f, minimumDistanceFromPlayerStart);
        float placementPadding = radius + Mathf.Max(0f, edgePadding);
        float maximumDistance = gameplayArea.SpawnArea.bounds.size.magnitude;

        for (int attempt = 0;
             attempt < LocalAnomalyPositionAttempts;
             attempt++)
        {
            if (!gameplayArea.TryGetSpawnPosition(
                    playerStart,
                    requiredPlayerDistance,
                    maximumDistance,
                    1,
                    placementPadding,
                    out Vector3 candidate))
            {
                continue;
            }

            if (Vector2.Distance(candidate, playerStart) <
                requiredPlayerDistance)
            {
                continue;
            }

            if (!IsCircleInsidePlayableArea(candidate, placementPadding))
                continue;

            bool separated = true;

            for (int i = 0; i < existingPlacements.Count; i++)
            {
                LocalAnomalyPlacement existing = existingPlacements[i];
                float requiredZoneDistance =
                    radius +
                    existing.Radius +
                    Mathf.Max(0f, minimumDistanceBetweenAnomalies);

                if (Vector2.Distance(candidate, existing.Position) <
                    requiredZoneDistance)
                {
                    separated = false;
                    break;
                }
            }

            if (!separated)
                continue;

            position = candidate;
            return true;
        }

        return false;
    }

    private bool IsCircleInsidePlayableArea(Vector2 center, float radius)
    {
        const int Samples = 16;

        if (!gameplayArea.IsInsidePlayableArea(center))
            return false;

        for (int i = 0; i < Samples; i++)
        {
            float angle = i * Mathf.PI * 2f / Samples;
            Vector2 sample = center + new Vector2(
                Mathf.Cos(angle),
                Mathf.Sin(angle)
            ) * radius;

            if (!gameplayArea.IsInsidePlayableArea(sample))
                return false;
        }

        return true;
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

        if (Instance == this)
            Instance = null;
    }
}
