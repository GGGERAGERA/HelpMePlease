using UnityEngine;
using System.Collections.Generic;

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
            context.Config.ElectricPlayerDamage,
            context.Config.ElectricArtHooks
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
    private readonly List<LineRenderer> nodeRings = new();
    private Vector2 center;
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
    private AnomalyArtHooks artHookRuntime;
    private AnomalyVisualTuningValues originalVisualValues;
    private AnomalyVisualTuningValues debugVisualValues;
    private bool visualValuesCaptured;
    private Color originalNodeColor;
    private Color originalTelegraphColor;
    private Color originalGlowColor;
    private Color originalCoreColor;
    private float originalNodeWidth;
    private float originalTelegraphWidth;
    private float originalGlowWidth;
    private float originalCoreWidth;

    public void Initialize(
        Vector2 center,
        Vector2 size,
        float configuredEnemyDamage,
        float configuredPlayerDamage,
        AnomalyArtHookSet artHooks)
    {
        this.center = center;
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
        artHookRuntime = AnomalyArtHooks.Create(
            visualRoot.transform, artHooks, "ELECTRIC");
        artHookRuntime?.SetBoundarySize(size);
        CaptureOriginalVisualValues();
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
        artHookRuntime?.AlignPatternToWorldSegment(
            dischargeStart, dischargeEnd);
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
                0.1f,
                21,
                31
            );
            ring.loop = true;
            nodeRings.Add(ring);
            SetNodeRing(ring, nodes[i], 1f);
        }

        telegraph = CreateLine(
            "Electric Telegraph",
            new Color(1f, 0.85f, 0.2f, 0.8f),
            0.12f,
            2,
            34
        );
        glow = CreateLine(
            "Electric Discharge Glow",
            new Color(0.1f, 0.55f, 1f, 0.35f),
            1.15f,
            2,
            35
        );
        core = CreateLine(
            "Electric Discharge Core",
            new Color(0.75f, 0.95f, 1f, 1f),
            0.24f,
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

    private void SetLine(LineRenderer line, Vector2 start, Vector2 end)
    {
        float scale = visualValuesCaptured
            ? debugVisualValues.VisualScale
            : 1f;
        line.SetPosition(0, ScalePoint(start, scale));
        line.SetPosition(1, ScalePoint(end, scale));
    }

    public string VisualTypeName => "ELECTRIC / ARC";

    public AnomalyVisualTuningCapabilities VisualCapabilities =>
        AnomalyVisualTuningCapabilities.PrimaryColor |
        AnomalyVisualTuningCapabilities.SecondaryColor |
        AnomalyVisualTuningCapabilities.BoundaryWidth |
        AnomalyVisualTuningCapabilities.InnerLineWidth |
        AnomalyVisualTuningCapabilities.VisualScale |
        AnomalyVisualTuningCapabilities.EdgeGlow;

    public AnomalyVisualTuningValues VisualValues => debugVisualValues;

    public void ApplyVisualValues(AnomalyVisualTuningValues values)
    {
        debugVisualValues = values;
        debugVisualValues.PrimaryColor = ClampColor(values.PrimaryColor);
        debugVisualValues.SecondaryColor = ClampColor(values.SecondaryColor);
        debugVisualValues.BoundaryWidth = Mathf.Clamp(
            values.BoundaryWidth, 0.01f, 3f);
        debugVisualValues.InnerLineWidth = Mathf.Clamp(
            values.InnerLineWidth, 0.01f, 3f);
        debugVisualValues.VisualScale = Mathf.Clamp(
            values.VisualScale, 0.25f, 3f);
        debugVisualValues.EdgeGlow = Mathf.Clamp(
            values.EdgeGlow, 0.01f, 10f);

        for (int i = 0; i < nodeRings.Count; i++)
        {
            LineRenderer ring = nodeRings[i];

            if (ring == null)
                continue;

            ring.startColor = debugVisualValues.PrimaryColor;
            ring.endColor = debugVisualValues.PrimaryColor;
            ring.startWidth = debugVisualValues.BoundaryWidth;
            ring.endWidth = debugVisualValues.BoundaryWidth;
            SetNodeRing(
                ring,
                nodes[i],
                debugVisualValues.VisualScale
            );
        }

        SetLineStyle(
            telegraph,
            debugVisualValues.SecondaryColor,
            debugVisualValues.BoundaryWidth
        );
        Color glowColor = debugVisualValues.SecondaryColor;
        glowColor.a = Mathf.Min(glowColor.a, 0.45f);
        SetLineStyle(glow, glowColor, debugVisualValues.EdgeGlow);
        SetLineStyle(
            core,
            debugVisualValues.PrimaryColor,
            debugVisualValues.InnerLineWidth
        );
        SetLine(telegraph, dischargeStart, dischargeEnd);
        SetLine(glow, dischargeStart, dischargeEnd);
        SetLine(core, dischargeStart, dischargeEnd);
    }

    public void ResetVisualValues()
    {
        if (!visualValuesCaptured)
            return;

        debugVisualValues = originalVisualValues;

        for (int i = 0; i < nodeRings.Count; i++)
        {
            LineRenderer ring = nodeRings[i];

            if (ring == null)
                continue;

            SetLineStyle(ring, originalNodeColor, originalNodeWidth);
            SetNodeRing(ring, nodes[i], 1f);
        }

        SetLineStyle(
            telegraph,
            originalTelegraphColor,
            originalTelegraphWidth
        );
        SetLineStyle(glow, originalGlowColor, originalGlowWidth);
        SetLineStyle(core, originalCoreColor, originalCoreWidth);
        SetLine(telegraph, dischargeStart, dischargeEnd);
        SetLine(glow, dischargeStart, dischargeEnd);
        SetLine(core, dischargeStart, dischargeEnd);
    }

    private void CaptureOriginalVisualValues()
    {
        if (visualValuesCaptured)
            return;

        originalNodeColor = nodeRings.Count > 0
            ? nodeRings[0].startColor
            : Color.cyan;
        originalNodeWidth = nodeRings.Count > 0
            ? nodeRings[0].startWidth
            : 0.13f;
        originalTelegraphColor = telegraph.startColor;
        originalTelegraphWidth = telegraph.startWidth;
        originalGlowColor = glow.startColor;
        originalGlowWidth = glow.startWidth;
        originalCoreColor = core.startColor;
        originalCoreWidth = core.startWidth;
        debugVisualValues = new AnomalyVisualTuningValues
        {
            PrimaryColor = originalCoreColor,
            SecondaryColor = originalTelegraphColor,
            BoundaryWidth = originalNodeWidth,
            InnerLineWidth = originalCoreWidth,
            VisualScale = 1f,
            EdgeGlow = originalGlowWidth
        };
        originalVisualValues = debugVisualValues;
        visualValuesCaptured = true;
    }

    private Vector2 ScalePoint(Vector2 point, float scale)
    {
        return center + (point - center) * scale;
    }

    private void SetNodeRing(
        LineRenderer ring,
        Vector2 node,
        float scale)
    {
        Vector2 visualNode = ScalePoint(node, scale);

        for (int point = 0; point < ring.positionCount; point++)
        {
            float angle = point / (float)(ring.positionCount - 1) *
                Mathf.PI * 2f;
            ring.SetPosition(point, visualNode + new Vector2(
                Mathf.Cos(angle),
                Mathf.Sin(angle)
            ) * (0.52f * scale));
        }
    }

    private static void SetLineStyle(
        LineRenderer line,
        Color color,
        float width)
    {
        if (line == null)
            return;

        line.startColor = color;
        line.endColor = color;
        line.startWidth = width;
        line.endWidth = width;
    }

    private static Color ClampColor(Color value)
    {
        return new Color(
            Mathf.Clamp01(value.r),
            Mathf.Clamp01(value.g),
            Mathf.Clamp01(value.b),
            Mathf.Clamp01(value.a)
        );
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
        nodeRings.Clear();
        if (visualRoot != null)
            Destroy(visualRoot);
        if (material != null)
            Destroy(material);
    }
}
