using UnityEngine;

internal static class ProductionBeamSiteDefinition
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void RegisterDefinition()
    {
        ProductionAnomalySiteDefinitionRegistry.Register(
            new ProductionAnomalySiteDefinition(
                AnomalyPowerType.RedBeam,
                "BEAM",
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
            context.Config.BeamPlayerDamage,
            context.Config.BeamVisualPrefab
        );
        Debug.Log(
            "[ExplorationSector] Special Site: BEAM " +
            "(ProductionBeamSiteHazard)."
        );
        return beam;
    }
}

internal sealed class ProductionBeamSiteHazard :
    ProductionSpecialSiteHazard,
    IAnomalyVisualTunable
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
    private AnomalyBeamView view;

    public void Initialize(
        Vector2 siteCenter,
        Vector2 size,
        float configuredEnemyDamage,
        float configuredPlayerDamage,
        AnomalyBeamView visualPrefab)
    {
        center = siteCenter;
        halfSize = new Vector2(
            Mathf.Max(2f, size.x * 0.5f - 0.5f),
            Mathf.Max(2f, size.y * 0.5f - 0.5f)
        );
        enemyDamage = Mathf.Max(0f, configuredEnemyDamage);
        playerDamage = Mathf.Max(0f, configuredPlayerDamage);
        view = Instantiate(visualPrefab, transform);
        state = HazardState.Waiting;
        stateUntil = Time.time + 0.4f;
    }

    public override void StopHazard()
    {
        enabled = false;
        Destroy(view.gameObject);

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
                view.SetState(AnomalyBeamView.BeamState.Ending);
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

        view.SetSegment(beamStart, beamEnd, DamageHalfWidth);
        view.SetState(AnomalyBeamView.BeamState.Telegraph);
        state = HazardState.Telegraph;
        stateUntil = Time.time + TelegraphSeconds;
    }

    private void FireBeam()
    {
        view.SetState(AnomalyBeamView.BeamState.Active);
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

    public string VisualTypeName => "BEAM";
    public AnomalyVisualTuningCapabilities VisualCapabilities => AnomalyBeamView.Capabilities;
    public AnomalyVisualTuningValues VisualValues => view.VisualValues;
    public void ApplyVisualValues(AnomalyVisualTuningValues values) => view.ApplyVisualValues(values);
    public void ResetVisualValues() => view.ResetVisualValues();

    private void OnDestroy()
    {
        if (view != null) Destroy(view.gameObject);
    }
}
