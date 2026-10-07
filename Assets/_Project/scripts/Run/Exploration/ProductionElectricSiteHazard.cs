using UnityEngine;

internal static class ProductionElectricSiteDefinition
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void RegisterDefinition()
    {
        ProductionAnomalySiteDefinitionRegistry.Register(
            new ProductionAnomalySiteDefinition(
                AnomalyPowerType.ArcNode,
                "ELECTRIC",
                CreateEnvironment
            )
        );
    }

    private static IProductionAnomalySiteEnvironment CreateEnvironment(
        ProductionAnomalySiteContext context)
    {
        ProductionElectricSiteHazard electric =
            context.SiteObject.AddComponent<ProductionElectricSiteHazard>();
        electric.Initialize(
            context.Position,
            context.Size,
            context.Config.ElectricEnemyDamage,
            context.Config.ElectricPlayerDamage,
            context.Config.ElectricVisualPrefab
        );
        Debug.Log(
            "[ExplorationSector] Special Site: ELECTRIC " +
            "(ProductionElectricSiteHazard)."
        );
        return electric;
    }
}

internal sealed class ProductionElectricSiteHazard :
    ProductionSpecialSiteHazard,
    IAnomalyVisualTunable
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
    private AnomalyBeamView view;

    public void Initialize(
        Vector2 center,
        Vector2 size,
        float configuredEnemyDamage,
        float configuredPlayerDamage,
        AnomalyBeamView visualPrefab)
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

        view = Instantiate(visualPrefab, transform);
        view.SetNodes(nodes);
        state = HazardState.Waiting;
        stateUntil = Time.time + 0.35f;
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
                FireDischarge();
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
        Vector2Int pair = NodePairs[pairIndex % NodePairs.Length];
        pairIndex++;
        dischargeStart = nodes[pair.x];
        dischargeEnd = nodes[pair.y];
        view.SetSegment(dischargeStart, dischargeEnd, DamageHalfWidth);
        view.SetState(AnomalyBeamView.BeamState.Telegraph);
        state = HazardState.Telegraph;
        stateUntil = Time.time + TelegraphSeconds;
    }

    private void FireDischarge()
    {
        view.SetState(AnomalyBeamView.BeamState.Active);
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

    public string VisualTypeName => "ELECTRIC";
    public AnomalyVisualTuningCapabilities VisualCapabilities => AnomalyBeamView.Capabilities;
    public AnomalyVisualTuningValues VisualValues => view.VisualValues;
    public void ApplyVisualValues(AnomalyVisualTuningValues values) => view.ApplyVisualValues(values);
    public void ResetVisualValues() => view.ResetVisualValues();

    private void OnDestroy()
    {
        if (view != null) Destroy(view.gameObject);
    }
}
