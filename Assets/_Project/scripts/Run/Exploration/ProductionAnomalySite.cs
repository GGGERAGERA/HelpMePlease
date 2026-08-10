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
    private ProductionSpecialSiteHazard hazard;
    private AnomalyPowerType specialPower;
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
    public LocalAnomalyZone AnomalyZone => anomalyZone;
    public string DebugZoneName => isSpecial
        ? $"SPECIAL {GetPowerDebugName(specialPower)}"
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
        LocalAnomalyData gravityAnomaly,
        WorldEvent prefab,
        WorldEventSpawner events,
        LevelAnomalyController anomalies,
        GravityTrajectoryService trajectoryService,
        ExplorationSectorConfig config,
        Vector2 sectorExitPosition,
        float sectorExitRadius)
    {
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
        specialPower = power;
        siteSize = size;
        initialized = true;
        BuildBoundary(size, new Color(1f, 0.35f, 0.1f, 0.9f));

        if (power == AnomalyPowerType.GravityOrb)
        {
            anomalyZone = anomalyController?.SpawnSiteZone(
                gravityAnomaly,
                position,
                size
            );

            if (anomalyZone is GravityZone gravityZone)
            {
                gravityZone.ConfigureOrbit(
                    7f,
                    3.5f,
                    2.5f,
                    1f,
                    0.7f,
                    0.35f
                );
                trajectoryService?.SetGravityZone(gravityZone);
            }

            Debug.Log(
                "[ExplorationSector] Special Site: GRAVITY " +
                "(GravityZone)."
            );
        }
        else if (power == AnomalyPowerType.ArcNode)
        {
            ProductionElectricSiteHazard electric =
                gameObject.AddComponent<ProductionElectricSiteHazard>();
            electric.Initialize(
                position,
                size,
                config.ElectricEnemyDamage,
                config.ElectricPlayerDamage
            );
            hazard = electric;
            Debug.Log(
                "[ExplorationSector] Special Site: ELECTRIC " +
                "(ProductionElectricSiteHazard)."
            );
        }
        else
        {
            ProductionBeamSiteHazard beam =
                gameObject.AddComponent<ProductionBeamSiteHazard>();
            beam.Initialize(
                position,
                size,
                config.BeamEnemyDamage,
                config.BeamPlayerDamage
            );
            hazard = beam;
            Debug.Log(
                "[ExplorationSector] Special Site: BEAM " +
                "(ProductionBeamSiteHazard)."
            );
        }

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
        if (anomalyZone is GravityZone gravityZone)
            gravityZone.SetDebugVisualEmphasis(multiplier);
    }

    private static string GetPowerDebugName(AnomalyPowerType power) =>
        power switch
        {
            AnomalyPowerType.GravityOrb => "GRAVITY",
            AnomalyPowerType.ArcNode => "ELECTRIC",
            _ => "BEAM"
        };
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
        RunStateManager runState = RunStateManager.Instance;
        bool added = runState != null &&
            runState.TryAddAnomalyPower(specialPower);

        if (added)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            AnomalyPowerRuntime.EnsurePower(player, specialPower);
            RunMessageService.Instance?.ShowCustom(
                "ANOMALY POWER ACQUIRED",
                $"{GetPowerDisplayName(specialPower)}  " +
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

        if (hazard != null)
        {
            hazard.StopHazard();
            hazard = null;
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

    private static string GetPowerDisplayName(AnomalyPowerType power)
    {
        return power switch
        {
            AnomalyPowerType.GravityOrb => "GRAVITY ORB",
            AnomalyPowerType.ArcNode => "ARC NODE",
            _ => "RED BEAM"
        };
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

internal abstract class ProductionSpecialSiteHazard : MonoBehaviour
{
    public abstract void StopHazard();
}

internal sealed class ProductionElectricSiteHazard :
    ProductionSpecialSiteHazard
{
    private enum HazardState
    {
        Waiting,
        Telegraph,
        Firing
    }

    private const float WaitSeconds = 0.75f;
    private const float TelegraphSeconds = 0.55f;
    private const float DischargeSeconds = 0.24f;
    private const float DamageHalfWidth = 0.8f;

    private static readonly Vector2[] NormalizedNodePositions =
    {
        new(-0.667f, -0.333f), new(-0.524f, 0.429f),
        new(-0.048f, -0.619f), new(0.143f, 0.590f),
        new(0.619f, -0.362f), new(0.686f, 0.324f)
    };

    private static readonly Vector2Int[] NodePairs =
    {
        new(0, 5), new(1, 4), new(2, 3),
        new(0, 3), new(1, 5), new(2, 4)
    };

    private readonly Vector2[] nodes = new Vector2[6];
    private float enemyDamage;
    private float playerDamage;
    private HazardState state;
    private float stateUntil;
    private int pairIndex;
    private Vector2 dischargeStart;
    private Vector2 dischargeEnd;
    private GameObject visualRoot;
    private Material material;
    private LineRenderer telegraph;
    private LineRenderer glow;
    private LineRenderer core;

    public void Initialize(
        Vector2 center,
        Vector2 size,
        float configuredEnemyDamage,
        float configuredPlayerDamage)
    {
        enemyDamage = Mathf.Max(0f, configuredEnemyDamage);
        playerDamage = Mathf.Max(0f, configuredPlayerDamage);
        Vector2 halfSize = size * 0.5f;

        for (int i = 0; i < nodes.Length; i++)
        {
            nodes[i] = center + Vector2.Scale(
                NormalizedNodePositions[i],
                halfSize
            );
        }

        BuildVisuals();
        state = HazardState.Waiting;
        stateUntil = Time.time + 0.35f;
    }

    public override void StopHazard()
    {
        enabled = false;
        HideHazardLines();

        if (visualRoot != null)
            Destroy(visualRoot);

        Destroy(this);
    }

    private void Update()
    {
        if (Time.time < stateUntil)
            return;

        switch (state)
        {
            case HazardState.Waiting:
                BeginTelegraph();
                break;
            case HazardState.Telegraph:
                FireDischarge();
                break;
            default:
                HideHazardLines();
                state = HazardState.Waiting;
                stateUntil = Time.time + WaitSeconds;
                break;
        }
    }

    private void BeginTelegraph()
    {
        Vector2Int pair = NodePairs[pairIndex % NodePairs.Length];
        pairIndex++;
        dischargeStart = nodes[pair.x];
        dischargeEnd = nodes[pair.y];
        SetLine(telegraph, dischargeStart, dischargeEnd);
        telegraph.enabled = true;
        glow.enabled = false;
        core.enabled = false;
        state = HazardState.Telegraph;
        stateUntil = Time.time + TelegraphSeconds;
    }

    private void FireDischarge()
    {
        telegraph.enabled = false;
        SetLine(glow, dischargeStart, dischargeEnd);
        SetLine(core, dischargeStart, dischargeEnd);
        glow.enabled = true;
        core.enabled = true;
        ProductionSiteHazardUtility.ApplyLineDamage(
            dischargeStart,
            dischargeEnd,
            DamageHalfWidth,
            enemyDamage,
            playerDamage
        );
        state = HazardState.Firing;
        stateUntil = Time.time + DischargeSeconds;
    }

    private void BuildVisuals()
    {
        visualRoot = new GameObject("Electric Site Hazard Visual");
        visualRoot.transform.SetParent(transform, false);
        material = AnomalyPowerVisuals.CreateMaterial(
            "Electric Site Hazard Runtime Material"
        );

        for (int i = 0; i < nodes.Length; i++)
        {
            LineRenderer ring = CreateLine(
                $"Electric Node {i + 1}",
                new Color(0.2f, 0.9f, 1f, 1f),
                0.13f,
                21,
                31
            );
            ring.loop = true;

            for (int point = 0; point < ring.positionCount; point++)
            {
                float angle = point / (float)(ring.positionCount - 1) *
                    Mathf.PI * 2f;
                ring.SetPosition(point, nodes[i] + new Vector2(
                    Mathf.Cos(angle),
                    Mathf.Sin(angle)
                ) * 0.52f);
            }
        }

        telegraph = CreateLine(
            "Electric Telegraph",
            new Color(1f, 0.85f, 0.2f, 0.8f),
            0.16f,
            2,
            34
        );
        glow = CreateLine(
            "Electric Discharge Glow",
            new Color(0.1f, 0.55f, 1f, 0.35f),
            1.6f,
            2,
            35
        );
        core = CreateLine(
            "Electric Discharge Core",
            new Color(0.75f, 0.95f, 1f, 1f),
            0.3f,
            2,
            36
        );
        HideHazardLines();
    }

    private LineRenderer CreateLine(
        string lineName,
        Color color,
        float width,
        int positions,
        int sortingOrder)
    {
        LineRenderer line = AnomalyPowerVisuals.CreateLine(
            visualRoot.transform,
            lineName,
            color,
            width,
            positions,
            material
        );
        line.sortingOrder = sortingOrder;
        return line;
    }

    private static void SetLine(LineRenderer line, Vector2 start, Vector2 end)
    {
        line.SetPosition(0, start);
        line.SetPosition(1, end);
    }

    private void HideHazardLines()
    {
        if (telegraph != null)
            telegraph.enabled = false;
        if (glow != null)
            glow.enabled = false;
        if (core != null)
            core.enabled = false;
    }

    private void OnDestroy()
    {
        if (visualRoot != null)
            Destroy(visualRoot);
        if (material != null)
            Destroy(material);
    }
}

internal sealed class ProductionBeamSiteHazard :
    ProductionSpecialSiteHazard
{
    private enum HazardState
    {
        Waiting,
        Telegraph,
        Firing
    }

    private const float WaitSeconds = 2f;
    private const float TelegraphSeconds = 0.68f;
    private const float BeamSeconds = 0.3f;
    private const float DamageHalfWidth = 1.45f;

    private static readonly Vector2[] Directions =
    {
        Vector2.right,
        Vector2.up,
        new Vector2(1f, 1f).normalized,
        new Vector2(1f, -1f).normalized
    };

    private static readonly float[] NormalizedOffsets =
    {
        -0.238f,
        0.190f,
        0f,
        -0.286f
    };

    private Vector2 center;
    private Vector2 halfSize;
    private float enemyDamage;
    private float playerDamage;
    private HazardState state;
    private float stateUntil;
    private int patternIndex;
    private Vector2 beamStart;
    private Vector2 beamEnd;
    private GameObject visualRoot;
    private Material material;
    private LineRenderer telegraph;
    private LineRenderer glow;
    private LineRenderer core;

    public void Initialize(
        Vector2 siteCenter,
        Vector2 size,
        float configuredEnemyDamage,
        float configuredPlayerDamage)
    {
        center = siteCenter;
        halfSize = new Vector2(
            Mathf.Max(2f, size.x * 0.5f - 0.5f),
            Mathf.Max(2f, size.y * 0.5f - 0.5f)
        );
        enemyDamage = Mathf.Max(0f, configuredEnemyDamage);
        playerDamage = Mathf.Max(0f, configuredPlayerDamage);
        BuildVisuals();
        state = HazardState.Waiting;
        stateUntil = Time.time + 0.4f;
    }

    public override void StopHazard()
    {
        enabled = false;
        HideHazardLines();

        if (visualRoot != null)
            Destroy(visualRoot);

        Destroy(this);
    }

    private void Update()
    {
        if (Time.time < stateUntil)
            return;

        switch (state)
        {
            case HazardState.Waiting:
                BeginTelegraph();
                break;
            case HazardState.Telegraph:
                FireBeam();
                break;
            default:
                HideHazardLines();
                state = HazardState.Waiting;
                stateUntil = Time.time + WaitSeconds;
                break;
        }
    }

    private void BeginTelegraph()
    {
        int index = patternIndex % Directions.Length;
        patternIndex++;
        Vector2 direction = Directions[index];
        Vector2 normal = new(-direction.y, direction.x);
        float normalExtent = Mathf.Abs(normal.x) * halfSize.x +
            Mathf.Abs(normal.y) * halfSize.y;
        Vector2 point = center + normal *
            (NormalizedOffsets[index] * normalExtent);

        if (!TryBuildSegment(point, direction, out beamStart, out beamEnd))
        {
            beamStart = center - direction * Mathf.Min(halfSize.x, halfSize.y);
            beamEnd = center + direction * Mathf.Min(halfSize.x, halfSize.y);
        }

        SetLine(telegraph, beamStart, beamEnd);
        telegraph.enabled = true;
        glow.enabled = false;
        core.enabled = false;
        state = HazardState.Telegraph;
        stateUntil = Time.time + TelegraphSeconds;
    }

    private void FireBeam()
    {
        telegraph.enabled = false;
        SetLine(glow, beamStart, beamEnd);
        SetLine(core, beamStart, beamEnd);
        glow.enabled = true;
        core.enabled = true;
        ProductionSiteHazardUtility.ApplyLineDamage(
            beamStart,
            beamEnd,
            DamageHalfWidth,
            enemyDamage,
            playerDamage
        );
        state = HazardState.Firing;
        stateUntil = Time.time + BeamSeconds;
    }

    private bool TryBuildSegment(
        Vector2 point,
        Vector2 direction,
        out Vector2 start,
        out Vector2 end)
    {
        float minimum = float.NegativeInfinity;
        float maximum = float.PositiveInfinity;
        bool valid = ClipAxis(
            point.x,
            direction.x,
            center.x - halfSize.x,
            center.x + halfSize.x,
            ref minimum,
            ref maximum
        ) && ClipAxis(
            point.y,
            direction.y,
            center.y - halfSize.y,
            center.y + halfSize.y,
            ref minimum,
            ref maximum
        );

        start = point + direction * minimum;
        end = point + direction * maximum;
        return valid && maximum > minimum;
    }

    private static bool ClipAxis(
        float origin,
        float direction,
        float minimumBound,
        float maximumBound,
        ref float minimum,
        ref float maximum)
    {
        if (Mathf.Abs(direction) < 0.0001f)
            return origin >= minimumBound && origin <= maximumBound;

        float first = (minimumBound - origin) / direction;
        float second = (maximumBound - origin) / direction;

        if (first > second)
            (first, second) = (second, first);

        minimum = Mathf.Max(minimum, first);
        maximum = Mathf.Min(maximum, second);
        return maximum >= minimum;
    }

    private void BuildVisuals()
    {
        visualRoot = new GameObject("Beam Site Hazard Visual");
        visualRoot.transform.SetParent(transform, false);
        material = AnomalyPowerVisuals.CreateMaterial(
            "Beam Site Hazard Runtime Material"
        );
        telegraph = CreateLine(
            "Environmental Beam Telegraph",
            new Color(1f, 0.12f, 0.08f, 0.75f),
            0.24f,
            34
        );
        glow = CreateLine(
            "Environmental Beam Glow",
            new Color(1f, 0.01f, 0.01f, 0.3f),
            3f,
            35
        );
        core = CreateLine(
            "Environmental Beam Core",
            new Color(1f, 0.32f, 0.12f, 1f),
            1.25f,
            36
        );
        HideHazardLines();
    }

    private LineRenderer CreateLine(
        string lineName,
        Color color,
        float width,
        int sortingOrder)
    {
        LineRenderer line = AnomalyPowerVisuals.CreateLine(
            visualRoot.transform,
            lineName,
            color,
            width,
            2,
            material
        );
        line.sortingOrder = sortingOrder;
        return line;
    }

    private static void SetLine(LineRenderer line, Vector2 start, Vector2 end)
    {
        line.SetPosition(0, start);
        line.SetPosition(1, end);
    }

    private void HideHazardLines()
    {
        if (telegraph != null)
            telegraph.enabled = false;
        if (glow != null)
            glow.enabled = false;
        if (core != null)
            core.enabled = false;
    }

    private void OnDestroy()
    {
        if (visualRoot != null)
            Destroy(visualRoot);
        if (material != null)
            Destroy(material);
    }
}

internal static class ProductionSiteHazardUtility
{
    public static void ApplyLineDamage(
        Vector2 start,
        Vector2 end,
        float halfWidth,
        float enemyDamage,
        float playerDamage)
    {
        List<EnemyHealth> enemies = new(EnemyHealth.ActiveInstances);

        for (int i = 0; i < enemies.Count; i++)
        {
            EnemyHealth enemy = enemies[i];

            if (enemy == null || enemy.IsDead ||
                DistanceToSegment(enemy.transform.position, start, end) >
                halfWidth)
            {
                continue;
            }

            enemy.TakeDamage(enemyDamage, enemy.transform.position, false);
        }

        GameObject player = GameObject.FindGameObjectWithTag("Player");
        PlayerHealth playerHealth = player != null
            ? player.GetComponent<PlayerHealth>()
            : null;

        if (playerHealth == null || playerHealth.IsDead ||
            DistanceToSegment(player.transform.position, start, end) >
            halfWidth)
        {
            return;
        }

        Vector2 nearest = ClosestPoint(player.transform.position, start, end);
        Vector2 knockback = (Vector2)player.transform.position - nearest;

        if (knockback.sqrMagnitude < 0.001f)
            knockback = Vector2.up;

        playerHealth.TakeDamage(playerDamage, knockback.normalized);
    }

    private static Vector2 ClosestPoint(Vector2 point, Vector2 start, Vector2 end)
    {
        Vector2 segment = end - start;
        float denominator = segment.sqrMagnitude;

        if (denominator <= 0.0001f)
            return start;

        float amount = Mathf.Clamp01(
            Vector2.Dot(point - start, segment) / denominator
        );
        return start + segment * amount;
    }

    private static float DistanceToSegment(
        Vector2 point,
        Vector2 start,
        Vector2 end)
    {
        return Vector2.Distance(point, ClosestPoint(point, start, end));
    }
}
