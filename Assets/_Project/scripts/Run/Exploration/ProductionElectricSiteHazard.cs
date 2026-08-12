using UnityEngine;

internal static class ProductionElectricSiteDefinition
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void RegisterDefinition()
    {
        ProductionAnomalySiteDefinitionRegistry.Register(
            new ProductionAnomalySiteDefinition(
                AnomalyPowerType.ArcNode,
                AnomalyPowerType.ArcNode,
                "ELECTRIC",
                "ARC NODE",
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
            context.Config.ElectricPlayerDamage
        );
        Debug.Log(
            "[ExplorationSector] Special Site: ELECTRIC " +
            "(ProductionElectricSiteHazard)."
        );
        return electric;
    }
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
