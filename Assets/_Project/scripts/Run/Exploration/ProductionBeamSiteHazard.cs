using UnityEngine;

internal static class ProductionBeamSiteDefinition
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void RegisterDefinition()
    {
        ProductionAnomalySiteDefinitionRegistry.Register(
            new ProductionAnomalySiteDefinition(
                AnomalyPowerType.RedBeam,
                AnomalyPowerType.RedBeam,
                "BEAM",
                "RED BEAM",
                CreateEnvironment
            )
        );
    }

    private static IProductionAnomalySiteEnvironment CreateEnvironment(
        ProductionAnomalySiteContext context)
    {
        ProductionBeamSiteHazard beam =
            context.SiteObject.AddComponent<ProductionBeamSiteHazard>();
        beam.Initialize(
            context.Position,
            context.Size,
            context.Config.BeamEnemyDamage,
            context.Config.BeamPlayerDamage
        );
        Debug.Log(
            "[ExplorationSector] Special Site: BEAM " +
            "(ProductionBeamSiteHazard)."
        );
        return beam;
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
