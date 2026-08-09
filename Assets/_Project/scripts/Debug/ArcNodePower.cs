using System.Collections.Generic;
using UnityEngine;

public sealed class ArcNodePower : MonoBehaviour
{
    // Prototype tuning.
    public const float DischargeInterval = 0.65f;
    public const float AcquisitionRadius = 9f;
    public const float JumpRadius = 4.2f;
    public const int MaxTargets = 4;
    public const float DamagePerTarget = 70f;

    private static readonly Vector2 NodeOffset = new(1.25f, 1.05f);
    private readonly List<EnemyHealth> dischargeTargets = new(MaxTargets);

    private GameObject visualRoot;
    private LineRenderer nodeRing;
    private float dischargeTimer;

    private void OnEnable()
    {
        EnsureVisual();
        visualRoot.SetActive(true);
        dischargeTimer = 0.2f;
    }

    private void Update()
    {
        Vector2 bob = Vector2.up * (Mathf.Sin(Time.time * 3f) * 0.16f);
        visualRoot.transform.position = (Vector2)transform.position +
            NodeOffset + bob;
        visualRoot.transform.Rotate(0f, 0f, 75f * Time.deltaTime);

        dischargeTimer += Time.deltaTime;
        if (dischargeTimer < DischargeInterval)
            return;

        dischargeTimer = 0f;
        Discharge();
    }

    private void Discharge()
    {
        dischargeTargets.Clear();
        Vector2 nodePosition = visualRoot.transform.position;
        EnemyHealth first = FindNearest(
            nodePosition,
            AcquisitionRadius,
            dischargeTargets
        );

        if (first == null)
            return;

        dischargeTargets.Add(first);
        Vector2 searchCenter = first.transform.position;

        while (dischargeTargets.Count < MaxTargets)
        {
            EnemyHealth next = FindNearest(
                searchCenter,
                JumpRadius,
                dischargeTargets
            );
            if (next == null)
                break;

            dischargeTargets.Add(next);
            searchCenter = next.transform.position;
        }

        Vector2 start = nodePosition;
        Color arcColor = new(1f, 0.72f, 0.12f, 1f);

        for (int i = 0; i < dischargeTargets.Count; i++)
        {
            EnemyHealth target = dischargeTargets[i];
            if (target == null || target.IsDead)
                continue;

            Vector2 end = target.transform.position;
            WeaponCoreDebugVisual.DrawLine(
                start,
                end,
                arcColor,
                0.12f,
                0.18f
            );
            target.TakeDamage(DamagePerTarget, end, false);
            start = end;
        }
    }

    private static EnemyHealth FindNearest(
        Vector2 center,
        float radius,
        List<EnemyHealth> ignored)
    {
        EnemyHealth nearest = null;
        float nearestDistance = radius * radius;

        foreach (EnemyHealth enemy in EnemyHealth.ActiveInstances)
        {
            if (enemy == null || enemy.IsDead || ignored.Contains(enemy))
                continue;

            float distance = ((Vector2)enemy.transform.position - center)
                .sqrMagnitude;
            if (distance >= nearestDistance)
                continue;

            nearest = enemy;
            nearestDistance = distance;
        }

        return nearest;
    }

    private void EnsureVisual()
    {
        if (visualRoot != null)
            return;

        visualRoot = new GameObject("Arc Node Power Visual");
        visualRoot.transform.SetParent(transform, true);
        nodeRing = visualRoot.AddComponent<LineRenderer>();
        nodeRing.useWorldSpace = false;
        nodeRing.loop = true;
        nodeRing.positionCount = 12;
        nodeRing.startWidth = 0.1f;
        nodeRing.endWidth = 0.1f;
        nodeRing.startColor = new Color(1f, 0.8f, 0.15f, 1f);
        nodeRing.endColor = new Color(0.65f, 0.15f, 1f, 1f);
        nodeRing.sharedMaterial = WeaponCoreDebugVisual.SharedLineMaterial;
        nodeRing.sortingLayerName = "Effects";
        nodeRing.sortingOrder = 37;

        for (int i = 0; i < nodeRing.positionCount; i++)
        {
            float angle = Mathf.PI * 2f * i / nodeRing.positionCount;
            float radius = i % 2 == 0 ? 0.38f : 0.24f;
            nodeRing.SetPosition(i, new Vector3(
                Mathf.Cos(angle) * radius,
                Mathf.Sin(angle) * radius,
                0f
            ));
        }
    }

    private void OnDisable()
    {
        if (visualRoot != null)
            visualRoot.SetActive(false);
    }

    private void OnDestroy()
    {
        if (visualRoot != null)
            Destroy(visualRoot);
    }
}
