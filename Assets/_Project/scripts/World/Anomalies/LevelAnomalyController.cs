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

    public static LevelAnomalyController Instance { get; private set; }

    [Header("Data")]
    [SerializeField] private LocalAnomalyData[] availableAnomalies;

    [Header("View")]
    [SerializeField] private LevelAnomalyView view;

    [Header("Local Anomaly Zones")]
    [SerializeField] private BerserkZone berserkZonePrefab;
    [SerializeField] private StasisZone stasisZonePrefab;
    [SerializeField, Range(1, 2)] private int anomalyCount = 1;
    [SerializeField, Min(0.1f)] private float anomalyRadius = 4f;
    [SerializeField, Min(0.1f)] private float stasisRadius = 4f;
    [SerializeField, Min(1f)] private float enemySpeedMultiplier = 1.5f;
    [SerializeField, Range(0.1f, 1f)] private float playerSpeedMultiplier = 0.65f;
    [SerializeField, Min(0f)] private float edgePadding = 1f;
    [SerializeField, Min(0f)] private float minimumDistanceFromPlayerStart = 5f;
    [SerializeField, Min(0f)] private float minimumDistanceBetweenAnomalies = 2f;
    [SerializeField] private GameplayAreaService gameplayArea;

    private readonly List<BerserkZone> berserkZones = new();
    private readonly List<StasisZone> stasisZones = new();
    private readonly List<ActiveLocalZone> activeLocalZones = new();

    private enum LocalAnomalyKind
    {
        Berserk,
        Stasis
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
        public readonly LocalAnomalyType Type;

        public ActiveLocalZone(
            Object source,
            LocalAnomalyType type)
        {
            Source = source;
            Type = type;
        }
    }

    private LocalAnomalyData activeAnomaly;
    private bool levelStarted;
    private bool localCardVisible;
    private LocalAnomalyType displayedLocalAnomalyType;

    public LocalAnomalyData ActiveAnomaly => activeAnomaly;
    public bool IsIntroComplete { get; private set; }

    public void CollectActiveLocalZones(
        List<LocalAnomalyZoneGeometry> result)
    {
        if (result == null)
            return;

        result.Clear();

        for (int i = 0; i < berserkZones.Count; i++)
        {
            AddLocalZoneGeometry(
                result,
                berserkZones[i],
                LocalAnomalyType.Berserk
            );
        }

        for (int i = 0; i < stasisZones.Count; i++)
        {
            AddLocalZoneGeometry(
                result,
                stasisZones[i],
                LocalAnomalyType.Stasis
            );
        }
    }

    private static void AddLocalZoneGeometry(
        List<LocalAnomalyZoneGeometry> result,
        MonoBehaviour zone,
        LocalAnomalyType type)
    {
        if (zone == null || !zone.isActiveAndEnabled)
            return;

        CircleCollider2D collider =
            zone.GetComponent<CircleCollider2D>();

        if (collider == null || !collider.enabled)
            return;

        Vector2 center = collider.transform.TransformPoint(
            collider.offset
        );
        Vector3 scale = collider.transform.lossyScale;
        float radius = collider.radius * Mathf.Max(
            Mathf.Abs(scale.x),
            Mathf.Abs(scale.y)
        );

        if (radius <= Mathf.Epsilon)
            return;

        result.Add(
            new LocalAnomalyZoneGeometry(type, center, radius)
        );
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    public void BeginLevel(LevelNodeData level)
    {
        if (levelStarted)
            return;

        levelStarted = true;
        activeAnomaly = SelectLocalAnomaly();

        if (activeAnomaly == null)
        {
            IsIntroComplete = true;
            return;
        }

        if (TryGetLocalAnomalyKind(
                activeAnomaly.AnomalyType,
                out LocalAnomalyKind localKind))
        {
            if (!HasZonePrefab(localKind))
            {
                Debug.LogWarning(
                    "[LevelAnomalyController] Selected local anomaly " +
                    $"'{activeAnomaly.name}' " +
                    $"({activeAnomaly.AnomalyType}), but its zone " +
                    "prefab is not assigned. Continuing without an " +
                    "anomaly.",
                    this
                );
                IsIntroComplete = true;
                return;
            }

            SpawnLocalAnomalyZones();
            IsIntroComplete = true;
            return;
        }

        IsIntroComplete = true;
    }

    private LocalAnomalyData SelectLocalAnomaly()
    {
        if (availableAnomalies == null || availableAnomalies.Length == 0)
            return null;

        float totalWeight = 0f;

        for (int i = 0; i < availableAnomalies.Length; i++)
        {
            LocalAnomalyData anomaly = availableAnomalies[i];

            if (anomaly != null &&
                IsLocalAnomalyType(anomaly.AnomalyType))
            {
                totalWeight += anomaly.SelectionWeight;
            }
        }

        if (totalWeight <= 0f)
            return null;

        float roll = Random.value * totalWeight;
        LocalAnomalyData lastEligible = null;

        for (int i = 0; i < availableAnomalies.Length; i++)
        {
            LocalAnomalyData anomaly = availableAnomalies[i];

            if (anomaly == null ||
                !IsLocalAnomalyType(anomaly.AnomalyType))
            {
                continue;
            }

            lastEligible = anomaly;
            roll -= anomaly.SelectionWeight;

            if (roll <= 0f)
                return anomaly;
        }

        return lastEligible;
    }

    public void NotifyLocalZoneEntered(
        Object zone,
        LocalAnomalyType type)
    {
        if (zone == null || !IsLocalAnomalyType(type))
            return;

        RemoveActiveLocalZone(zone);
        activeLocalZones.Add(new ActiveLocalZone(zone, type));
        RefreshLocalAnomalyCard();
    }

    public void NotifyLocalZoneExited(Object zone)
    {
        if (zone == null)
            return;

        if (!RemoveActiveLocalZone(zone))
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
            if (activeLocalZones[i].Source != null)
                continue;

            activeLocalZones.RemoveAt(i);
        }

        if (activeLocalZones.Count == 0)
        {
            view?.HideLocalAnomaly();
            localCardVisible = false;
            return;
        }

        LocalAnomalyType type =
            activeLocalZones[activeLocalZones.Count - 1].Type;

        if (localCardVisible &&
            displayedLocalAnomalyType == type)
        {
            return;
        }

        LocalAnomalyData data = FindAnomalyData(type);

        if (data == null)
        {
            Debug.LogWarning(
                "[LevelAnomalyController] Missing data for local " +
                $"anomaly {type}.",
                this
            );
            view?.HideLocalAnomaly();
            localCardVisible = false;
            return;
        }

        displayedLocalAnomalyType = type;
        localCardVisible = true;
        view?.ShowLocalAnomaly(data.Presentation);
    }

    private LocalAnomalyData FindAnomalyData(LocalAnomalyType type)
    {
        if (availableAnomalies == null)
            return null;

        for (int i = 0; i < availableAnomalies.Length; i++)
        {
            LocalAnomalyData data = availableAnomalies[i];

            if (data != null && data.AnomalyType == type)
                return data;
        }

        return null;
    }

    private static bool IsLocalAnomalyType(LocalAnomalyType type)
    {
        return TryGetLocalAnomalyKind(type, out _);
    }

    private static bool TryGetLocalAnomalyKind(
        LocalAnomalyType type,
        out LocalAnomalyKind kind)
    {
        switch (type)
        {
            case LocalAnomalyType.Berserk:
                kind = LocalAnomalyKind.Berserk;
                return true;

            case LocalAnomalyType.Stasis:
                kind = LocalAnomalyKind.Stasis;
                return true;

            default:
                kind = default;
                return false;
        }
    }

    private bool HasZonePrefab(LocalAnomalyKind kind)
    {
        switch (kind)
        {
            case LocalAnomalyKind.Berserk:
                return berserkZonePrefab != null;

            case LocalAnomalyKind.Stasis:
                return stasisZonePrefab != null;

            default:
                return false;
        }
    }

    private void SpawnLocalAnomalyZones()
    {
        CleanupLocalAnomalyZones();
        ResolveGameplayArea();

        List<LocalAnomalyKind> availableKinds =
            GetAvailableLocalAnomalyKinds();

        if (availableKinds.Count == 0 || gameplayArea == null)
        {
            Debug.LogWarning(
                "[LevelAnomalyController] Local anomalies cannot spawn: " +
                "zone prefabs or GameplayAreaService are missing. " +
                "Continuing without an anomaly.",
                this
            );
            return;
        }

        GameObject playerObject =
            GameObject.FindGameObjectWithTag("Player");

        if (playerObject == null)
        {
            Debug.LogWarning(
                "[LevelAnomalyController] Local anomalies cannot spawn: " +
                "the Player object was not found. Continuing without " +
                "an anomaly.",
                this
            );
            return;
        }

        Vector3 playerStart = playerObject.transform.position;
        List<LocalAnomalyPlacement> placements = new();
        int count = Mathf.Clamp(anomalyCount, 1, 2);
        LocalAnomalyKind firstKind =
            GetFirstLocalAnomalyKind(availableKinds);

        for (int i = 0; i < count; i++)
        {
            LocalAnomalyKind kind = i == 0
                ? firstKind
                : GetNextLocalAnomalyKind(
                    firstKind,
                    availableKinds
                );
            float radius = GetLocalAnomalyRadius(kind);

            if (!TryGetLocalAnomalyPosition(
                    playerStart,
                    radius,
                    placements,
                    out Vector3 position))
            {
                Debug.LogWarning(
                    "[LevelAnomalyController] No valid position was " +
                    $"found for local anomaly {i + 1}. Continuing " +
                    "without that zone.",
                    this
                );
                break;
            }

            SpawnLocalAnomaly(kind, position, radius);
            placements.Add(
                new LocalAnomalyPlacement(position, radius)
            );
        }
    }

    private List<LocalAnomalyKind> GetAvailableLocalAnomalyKinds()
    {
        List<LocalAnomalyKind> result = new();

        if (berserkZonePrefab != null)
            result.Add(LocalAnomalyKind.Berserk);
        if (stasisZonePrefab != null)
            result.Add(LocalAnomalyKind.Stasis);

        return result;
    }

    private LocalAnomalyKind GetFirstLocalAnomalyKind(
        List<LocalAnomalyKind> availableKinds)
    {
        LocalAnomalyKind preferred =
            activeAnomaly.AnomalyType == LocalAnomalyType.Stasis
                ? LocalAnomalyKind.Stasis
                : LocalAnomalyKind.Berserk;

        if (availableKinds.Contains(preferred))
            return preferred;

        return availableKinds[Random.Range(0, availableKinds.Count)];
    }

    private static LocalAnomalyKind GetNextLocalAnomalyKind(
        LocalAnomalyKind firstKind,
        List<LocalAnomalyKind> availableKinds)
    {
        for (int i = 0; i < availableKinds.Count; i++)
        {
            if (availableKinds[i] != firstKind)
                return availableKinds[i];
        }

        return firstKind;
    }

    private float GetLocalAnomalyRadius(LocalAnomalyKind kind)
    {
        return kind == LocalAnomalyKind.Stasis
            ? Mathf.Max(0.1f, stasisRadius)
            : Mathf.Max(0.1f, anomalyRadius);
    }

    private void SpawnLocalAnomaly(
        LocalAnomalyKind kind,
        Vector3 position,
        float radius)
    {
        if (kind == LocalAnomalyKind.Stasis)
        {
            StasisZone zone = Instantiate(
                stasisZonePrefab,
                position,
                Quaternion.identity
            );
            zone.Initialize(
                radius,
                playerSpeedMultiplier,
                this
            );
            stasisZones.Add(zone);
            return;
        }

        BerserkZone berserkZone = Instantiate(
            berserkZonePrefab,
            position,
            Quaternion.identity
        );
        berserkZone.Initialize(
            radius,
            enemySpeedMultiplier,
            this
        );
        berserkZones.Add(berserkZone);
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
        float placementPadding =
            radius + Mathf.Max(0f, edgePadding);
        float maximumDistance =
            gameplayArea.SpawnArea.bounds.size.magnitude;

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

            if (!IsCircleInsidePlayableArea(
                    candidate,
                    placementPadding))
            {
                continue;
            }

            bool separated = true;

            for (int i = 0; i < existingPlacements.Count; i++)
            {
                LocalAnomalyPlacement existing =
                    existingPlacements[i];
                float requiredZoneDistance =
                    radius +
                    existing.Radius +
                    Mathf.Max(
                        0f,
                        minimumDistanceBetweenAnomalies
                    );

                if (Vector2.Distance(
                        candidate,
                        existing.Position) <
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

    private bool IsCircleInsidePlayableArea(
        Vector2 center,
        float radius)
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
        for (int i = 0; i < berserkZones.Count; i++)
        {
            BerserkZone zone = berserkZones[i];

            if (zone == null)
                continue;

            zone.ClearEffects();
            Destroy(zone.gameObject);
        }

        berserkZones.Clear();

        for (int i = 0; i < stasisZones.Count; i++)
        {
            StasisZone zone = stasisZones[i];

            if (zone == null)
                continue;

            zone.ClearEffect();
            Destroy(zone.gameObject);
        }

        stasisZones.Clear();
        activeLocalZones.Clear();
        localCardVisible = false;
        view?.HideLocalAnomaly();
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
        CleanupLocalAnomalyZones();

        if (Instance == this)
            Instance = null;
    }
}
