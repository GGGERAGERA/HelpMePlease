using System.Collections.Generic;
using UnityEngine;

internal sealed class ArcNodeRuntime :
    MonoBehaviour,
    IAnomalyPowerRuntime,
    IAnomalyEvolutionPower
{
    private const float DischargeInterval = 0.65f;
    private const float AcquisitionRadius = 9f;
    private const float JumpRadius = 4.2f;
    private const int MaximumVisualTargets = 12;
    private const float DamagePerTarget = 70f;
    private const float DischargeVisualWidth = 0.11f;
    private static readonly Vector2 NodeOffset = new(-1.45f, 1.05f);

    private readonly List<EnemyHealth> primaryTargets = new();
    private readonly List<EnemyHealth> secondaryTargets = new();
    private readonly HashSet<EnemyHealth> claimedTargets = new();
    private Material material;
    private LineRenderer primaryLine;
    private LineRenderer secondaryLine;
    private Transform nodeVisual;
    private BaseWeapon payloadWeapon;
    private EvolutionDefinition evolutionDefinition;
    private float nextDischarge;
    private float hideLinesAt;
    private int level = 1;

    public AnomalyPowerType Type => AnomalyPowerType.ArcNode;
    public int Level => level;

    public void SetLevel(int value)
    {
        level = AnomalyPowerLevelProfiles.ClampLevel(value);
    }

    public void ConfigureEvolutionPayload(
        BaseWeapon weapon,
        EvolutionDefinition definition,
        int anomalyLevel)
    {
        payloadWeapon = weapon;
        evolutionDefinition = definition;
        SetLevel(anomalyLevel);
    }

    public void DisableEvolutionPayload()
    {
        payloadWeapon = null;
        evolutionDefinition = null;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void RegisterRuntime()
    {
        AnomalyPowerRuntimeRegistry.Register(
            AnomalyPowerType.ArcNode,
            owner => owner.AddComponent<ArcNodeRuntime>());
    }

    public void Activate()
    {
        enabled = true;
        if (nodeVisual != null)
            nodeVisual.gameObject.SetActive(true);
    }

    public void Deactivate()
    {
        enabled = false;
        DisableEvolutionPayload();
        if (nodeVisual != null)
            nodeVisual.gameObject.SetActive(false);
        HideLines();
    }

    private void Awake()
    {
        material = AnomalyPowerVisuals.CreateMaterial(
            "Arc Node Runtime Material");
        BuildNodeVisual();
        primaryLine = CreateDischargeLine("Arc Primary Discharge");
        secondaryLine = CreateDischargeLine("Arc Fork Discharge");
    }

    private LineRenderer CreateDischargeLine(string lineName)
    {
        LineRenderer result = AnomalyPowerVisuals.CreateLine(
            transform,
            lineName,
            new Color(0.3f, 0.8f, 1f, 1f),
            DischargeVisualWidth,
            MaximumVisualTargets + 1,
            material);
        result.enabled = false;
        return result;
    }

    private void Update()
    {
        UpdateNodeVisual();
        float attackSize = OffensiveAttackContext.GetAttackSize(gameObject);
        if (nodeVisual != null)
            nodeVisual.localScale = Vector3.one * attackSize;
        UpdateLineWidth(primaryLine, attackSize);
        UpdateLineWidth(secondaryLine, attackSize);

        if (Time.time >= hideLinesAt)
            HideLines();
        else
            UpdateVisibleLineOrigins();

        if (Time.time < nextDischarge)
            return;

        nextDischarge = Time.time + DischargeInterval;
        Discharge(attackSize);
    }

    private void Discharge(float attackSize)
    {
        BuildTopology(attackSize);
        if (primaryTargets.Count == 0 && secondaryTargets.Count == 0)
            return;

        Vector2 nodeOrigin = GetNodePosition();
        OffensiveAttackContext attack = OffensiveAttackContext.Resolve(
            gameObject,
            DamagePerTarget * AnomalyPowerLevelProfiles.ArcDamage(level));
        int payloadBudget = evolutionDefinition != null
            ? evolutionDefinition.MaxPayloadAttacksPerTick
            : 0;

        DrawAndApplyBranch(
            primaryLine,
            nodeOrigin,
            primaryTargets,
            attack,
            ref payloadBudget);
        DrawAndApplyBranch(
            secondaryLine,
            nodeOrigin,
            secondaryTargets,
            attack,
            ref payloadBudget);
        hideLinesAt = Time.time + 0.12f;
    }

    private void BuildTopology(float attackSize)
    {
        primaryTargets.Clear();
        secondaryTargets.Clear();
        claimedTargets.Clear();

        int primaryCount = level == 1
            ? 1
            : evolutionDefinition != null
                ? evolutionDefinition.BranchTargets
                : AnomalyPowerLevelProfiles.ArcTargets(level);
        int segmentCap = evolutionDefinition != null
            ? evolutionDefinition.MaxPayloadSegments
            : primaryCount;
        primaryCount = Mathf.Min(primaryCount, segmentCap);

        BuildBranch(
            GetNodePosition(),
            primaryCount,
            attackSize,
            primaryTargets);

        if (level < 3 || evolutionDefinition == null ||
            evolutionDefinition.OverdriveBranchCount < 2)
        {
            return;
        }

        int remaining = Mathf.Max(0, segmentCap - primaryTargets.Count);
        BuildBranch(
            GetNodePosition(),
            Mathf.Min(evolutionDefinition.BranchTargets, remaining),
            attackSize,
            secondaryTargets);
    }

    private void BuildBranch(
        Vector2 origin,
        int targetCount,
        float attackSize,
        List<EnemyHealth> output)
    {
        for (int targetIndex = 0; targetIndex < targetCount; targetIndex++)
        {
            float radius = (targetIndex == 0
                ? AcquisitionRadius
                : JumpRadius) * attackSize;
            EnemyHealth best = FindNearestUnclaimed(origin, radius);
            if (best == null)
                break;

            output.Add(best);
            claimedTargets.Add(best);
            origin = best.transform.position;
        }
    }

    private EnemyHealth FindNearestUnclaimed(Vector2 origin, float radius)
    {
        EnemyHealth best = null;
        float bestDistanceSquared = radius * radius;
        foreach (EnemyHealth enemy in EnemyHealth.ActiveInstances)
        {
            if (enemy == null || enemy.IsDead ||
                !enemy.gameObject.activeInHierarchy ||
                claimedTargets.Contains(enemy))
            {
                continue;
            }

            float distanceSquared =
                ((Vector2)enemy.transform.position - origin).sqrMagnitude;
            if (distanceSquared > bestDistanceSquared)
                continue;

            best = enemy;
            bestDistanceSquared = distanceSquared;
        }

        return best;
    }

    private void DrawAndApplyBranch(
        LineRenderer branchLine,
        Vector2 origin,
        List<EnemyHealth> branch,
        OffensiveAttackContext attack,
        ref int payloadBudget)
    {
        if (branchLine == null || branch.Count == 0)
            return;

        branchLine.positionCount = branch.Count + 1;
        branchLine.SetPosition(0, origin);
        Vector2 segmentOrigin = origin;

        for (int i = 0; i < branch.Count; i++)
        {
            EnemyHealth enemy = branch[i];
            Vector2 targetPosition = enemy.transform.position;
            branchLine.SetPosition(i + 1, targetPosition);
            enemy.TakeDamage(
                attack.Damage,
                targetPosition,
                attack.IsCritical);

            Vector2 direction = targetPosition - segmentOrigin;
            if (level >= 2 && payloadWeapon != null &&
                payloadBudget > 0 && direction.sqrMagnitude > 0.001f)
            {
                payloadWeapon.EmitAttack(segmentOrigin, direction.normalized);
                payloadBudget--;
            }

            segmentOrigin = targetPosition;
        }

        branchLine.enabled = true;
    }

    private void UpdateVisibleLineOrigins()
    {
        Vector2 origin = GetNodePosition();
        if (primaryLine != null && primaryLine.enabled)
            primaryLine.SetPosition(0, origin);
        if (secondaryLine != null && secondaryLine.enabled)
            secondaryLine.SetPosition(0, origin);
    }

    private static void UpdateLineWidth(LineRenderer target, float scale)
    {
        if (target == null)
            return;
        target.startWidth = DischargeVisualWidth * scale;
        target.endWidth = DischargeVisualWidth * scale;
    }

    private void HideLines()
    {
        if (primaryLine != null)
            primaryLine.enabled = false;
        if (secondaryLine != null)
            secondaryLine.enabled = false;
    }

    private Vector2 GetNodePosition()
    {
        return nodeVisual != null
            ? (Vector2)nodeVisual.position
            : (Vector2)transform.position;
    }

    private void BuildNodeVisual()
    {
        GameObject nodeObject = new("Arc Node Satellite");
        nodeObject.transform.SetParent(transform, false);
        nodeVisual = nodeObject.transform;

        LineRenderer glow = AnomalyPowerVisuals.CreateLine(
            nodeVisual,
            "Arc Node Glow",
            new Color(0.12f, 0.65f, 1f, 0.28f),
            0.18f,
            25,
            material);
        ConfigureNodeRing(glow, 0.53f);
        glow.sortingOrder = 35;

        LineRenderer star = AnomalyPowerVisuals.CreateLine(
            nodeVisual,
            "Arc Node Star",
            new Color(0.2f, 0.92f, 1f, 1f),
            0.11f,
            12,
            material);
        star.useWorldSpace = false;
        star.loop = true;
        star.sortingOrder = 38;
        star.endColor = new Color(0.18f, 0.48f, 1f, 1f);

        for (int i = 0; i < star.positionCount; i++)
        {
            float radians = Mathf.PI * 2f * i / star.positionCount;
            float radius = i % 2 == 0 ? 0.42f : 0.22f;
            star.SetPosition(i, new Vector3(
                Mathf.Cos(radians) * radius,
                Mathf.Sin(radians) * radius,
                0f));
        }

        LineRenderer core = AnomalyPowerVisuals.CreateLine(
            nodeVisual,
            "Arc Node Core",
            new Color(0.72f, 0.96f, 1f, 1f),
            0.3f,
            2,
            material);
        core.useWorldSpace = false;
        core.numCapVertices = 8;
        core.SetPosition(0, new Vector3(-0.02f, 0f, 0f));
        core.SetPosition(1, new Vector3(0.02f, 0f, 0f));
        core.sortingOrder = 39;
        UpdateNodeVisual();
    }

    private static void ConfigureNodeRing(LineRenderer ring, float radius)
    {
        ring.useWorldSpace = false;
        ring.loop = true;
        for (int i = 0; i < ring.positionCount; i++)
        {
            float radians = Mathf.PI * 2f * i / ring.positionCount;
            ring.SetPosition(i, new Vector3(
                Mathf.Cos(radians) * radius,
                Mathf.Sin(radians) * radius,
                0f));
        }
    }

    private void UpdateNodeVisual()
    {
        if (nodeVisual == null)
            return;

        float phase = Time.time * 2.2f;
        Vector2 drift = new(
            Mathf.Cos(phase) * 0.1f,
            Mathf.Sin(phase * 1.15f) * 0.14f);
        nodeVisual.position = (Vector2)transform.position +
            NodeOffset + drift;
        nodeVisual.Rotate(0f, 0f, 72f * Time.deltaTime);
    }

    private void OnDestroy()
    {
        if (material != null)
            Destroy(material);
    }
}
