using UnityEngine;

[DisallowMultipleComponent]
public sealed class RiftConstruct : AnomalyCoreConstruct
{
    private const float ImpactAngleStep = 137.5f;

    [Header("Source")]
    [SerializeField] private BaseWeapon sourceWeapon;
    [SerializeField] private Transform anchor;

    [Header("Impact")]
    [Min(0.05f)]
    [SerializeField] private float interval = 2.5f;
    [Min(0f)]
    [SerializeField] private float impactDelay = 0.35f;
    [Min(0f)]
    [SerializeField] private float distanceFromAnchor = 3f;
    [Min(1)]
    [SerializeField] private int burstCount = 8;
    [SerializeField] private float rotationOffset;

    private float intervalTimer;
    private float impactTimer;
    private float impactSequenceAngle;
    private Vector2 pendingImpactPoint;
    private bool impactPending;
    private GameObject telegraphVisual;

    public BaseWeapon SourceWeapon => sourceWeapon;

    public override void Configure(
        BaseWeapon weapon,
        Transform anchorTransform)
    {
        sourceWeapon = weapon;
        anchor = anchorTransform != null ? anchorTransform : transform;
    }

    private void Update()
    {
        if (sourceWeapon == null)
            return;

        float deltaTime = Time.deltaTime;
        intervalTimer += deltaTime;

        if (impactPending)
        {
            impactTimer -= deltaTime;
            if (impactTimer <= 0f)
                ResolveImpact();
        }

        if (!impactPending && intervalTimer >= interval)
        {
            intervalTimer = 0f;
            BeginImpact();
        }
    }

    private void BeginImpact()
    {
        Vector2 center = anchor != null
            ? (Vector2)anchor.position
            : (Vector2)transform.position;
        Vector2 offsetDirection = DirectionFromAngle(
            impactSequenceAngle + rotationOffset
        );

        pendingImpactPoint = center + offsetDirection *
            Mathf.Max(0f, distanceFromAnchor);
        impactSequenceAngle = Mathf.Repeat(
            impactSequenceAngle + ImpactAngleStep,
            360f
        );
        impactTimer = Mathf.Max(0f, impactDelay);
        impactPending = true;

        telegraphVisual = RiftRuntimeVisual.CreateRing(
            "Rift Impact Telegraph",
            pendingImpactPoint,
            0.35f,
            new Color(1f, 0.35f, 0.8f, 0.9f),
            Mathf.Max(0.05f, impactDelay)
        );
    }

    private void ResolveImpact()
    {
        impactPending = false;

        if (telegraphVisual != null)
            Destroy(telegraphVisual);

        telegraphVisual = null;
        RiftRuntimeVisual.CreateRing(
            "Rift Impact",
            pendingImpactPoint,
            0.75f,
            new Color(0.45f, 0.9f, 1f, 1f),
            0.25f
        );

        EmitRadialBurst(pendingImpactPoint);
    }

    private void EmitRadialBurst(Vector2 origin)
    {
        int count = Mathf.Max(1, burstCount);
        float angleStep = 360f / count;

        for (int i = 0; i < count; i++)
        {
            Vector2 direction = DirectionFromAngle(
                rotationOffset + angleStep * i
            );
            sourceWeapon.EmitAttack(origin, direction);
        }
    }

    private static Vector2 DirectionFromAngle(float angle)
    {
        float radians = angle * Mathf.Deg2Rad;
        return new Vector2(Mathf.Cos(radians), Mathf.Sin(radians));
    }

    private void OnDisable()
    {
        intervalTimer = 0f;
        impactTimer = 0f;
        impactPending = false;

        if (telegraphVisual != null)
            Destroy(telegraphVisual);

        telegraphVisual = null;
    }

    private void OnValidate()
    {
        interval = Mathf.Max(0.05f, interval);
        impactDelay = Mathf.Max(0f, impactDelay);
        distanceFromAnchor = Mathf.Max(0f, distanceFromAnchor);
        burstCount = Mathf.Max(1, burstCount);
    }
}

internal static class RiftRuntimeVisual
{
    private const int Segments = 32;
    private static Material lineMaterial;

    public static GameObject CreateRing(
        string objectName,
        Vector2 center,
        float radius,
        Color color,
        float duration)
    {
        Material material = GetLineMaterial();
        if (material == null)
            return null;

        GameObject visual = new(objectName);
        LineRenderer line = visual.AddComponent<LineRenderer>();
        line.useWorldSpace = true;
        line.loop = true;
        line.positionCount = Segments;
        line.sharedMaterial = material;
        line.startColor = color;
        line.endColor = color;
        line.startWidth = 0.075f;
        line.endWidth = 0.075f;
        line.numCornerVertices = 2;
        line.sortingLayerName = "Effects";
        line.sortingOrder = 24;

        for (int i = 0; i < Segments; i++)
        {
            float angle = Mathf.PI * 2f * i / Segments;
            line.SetPosition(i, center + new Vector2(
                Mathf.Cos(angle),
                Mathf.Sin(angle)
            ) * radius);
        }

        Object.Destroy(visual, Mathf.Max(0.05f, duration));
        return visual;
    }

    private static Material GetLineMaterial()
    {
        if (lineMaterial != null)
            return lineMaterial;

        Shader shader = Shader.Find("Sprites/Default");
        shader ??= Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null)
            return null;

        lineMaterial = new Material(shader)
        {
            name = "Rift Runtime Line Material",
            hideFlags = HideFlags.HideAndDontSave
        };
        return lineMaterial;
    }
}
