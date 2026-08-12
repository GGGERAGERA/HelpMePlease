using System.Collections.Generic;
using UnityEngine;

internal sealed class ArcNodeRuntime : MonoBehaviour, IAnomalyPowerRuntime
{
    private const float DischargeInterval = 0.65f;
    private const float AcquisitionRadius = 9f;
    private const float JumpRadius = 4.2f;
    private const int MaxTargets = 4;
    private const float DamagePerTarget = 70f;

    private readonly List<EnemyHealth> targets = new(MaxTargets);
    private static readonly Vector2 NodeOffset = new(-1.45f, 1.05f);
    private Material material;
    private LineRenderer line;
    private Transform nodeVisual;
    private float nextDischarge;
    private float hideLineAt;

    public AnomalyPowerType Type => AnomalyPowerType.ArcNode;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void RegisterRuntime()
    {
        AnomalyPowerRuntimeRegistry.Register(
            AnomalyPowerType.ArcNode,
            owner => owner.AddComponent<ArcNodeRuntime>()
        );
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

        if (nodeVisual != null)
            nodeVisual.gameObject.SetActive(false);

        if (line != null)
            line.enabled = false;
    }

    private void Awake()
    {
        material = AnomalyPowerVisuals.CreateMaterial(
            "Arc Node Runtime Material"
        );
        BuildNodeVisual();
        line = AnomalyPowerVisuals.CreateLine(
            transform,
            "Arc Node Discharge",
            new Color(0.3f, 0.8f, 1f, 1f),
            0.11f,
            MaxTargets + 1,
            material
        );
        line.enabled = false;
    }

    private void Update()
    {
        UpdateNodeVisual();

        if (line.enabled && Time.time >= hideLineAt)
            line.enabled = false;
        else if (line.enabled && nodeVisual != null)
            line.SetPosition(0, nodeVisual.position);

        if (Time.time < nextDischarge)
            return;

        nextDischarge = Time.time + DischargeInterval;
        BuildChain();

        if (targets.Count == 0)
            return;

        line.positionCount = targets.Count + 1;
        line.SetPosition(0, nodeVisual != null
            ? nodeVisual.position
            : transform.position);

        for (int i = 0; i < targets.Count; i++)
        {
            EnemyHealth enemy = targets[i];
            Vector3 hitPoint = enemy.transform.position;
            line.SetPosition(i + 1, hitPoint);
            enemy.TakeDamage(DamagePerTarget, hitPoint, false);
        }

        line.enabled = true;
        hideLineAt = Time.time + 0.12f;
    }

    private void BuildChain()
    {
        targets.Clear();
        Vector2 origin = transform.position;

        for (int targetIndex = 0; targetIndex < MaxTargets; targetIndex++)
        {
            EnemyHealth best = null;
            float bestDistance = float.PositiveInfinity;
            float radius = targetIndex == 0
                ? AcquisitionRadius
                : JumpRadius;

            foreach (EnemyHealth enemy in EnemyHealth.ActiveInstances)
            {
                if (enemy == null || enemy.IsDead || targets.Contains(enemy))
                    continue;

                float distance = Vector2.Distance(
                    origin,
                    enemy.transform.position
                );

                if (distance <= radius && distance < bestDistance)
                {
                    best = enemy;
                    bestDistance = distance;
                }
            }

            if (best == null)
                break;

            targets.Add(best);
            origin = best.transform.position;
        }
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
            material
        );
        ConfigureNodeRing(glow, 0.53f);
        glow.sortingOrder = 35;

        LineRenderer star = AnomalyPowerVisuals.CreateLine(
            nodeVisual,
            "Arc Node Star",
            new Color(0.2f, 0.92f, 1f, 1f),
            0.11f,
            12,
            material
        );
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
                0f
            ));
        }

        LineRenderer core = AnomalyPowerVisuals.CreateLine(
            nodeVisual,
            "Arc Node Core",
            new Color(0.72f, 0.96f, 1f, 1f),
            0.3f,
            2,
            material
        );
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
                0f
            ));
        }
    }

    private void UpdateNodeVisual()
    {
        if (nodeVisual == null)
            return;

        float phase = Time.time * 2.2f;
        Vector2 drift = new(
            Mathf.Cos(phase) * 0.1f,
            Mathf.Sin(phase * 1.15f) * 0.14f
        );
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
