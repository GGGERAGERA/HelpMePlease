using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
internal sealed class RedBeamRuntime :
    MonoBehaviour,
    IAnomalyPowerRuntime
{
    private const int MaximumOverlapHits = 64;

    [Header("Beam Construct Geometry")]
    [SerializeField, Min(0f)] private float orbitRadius = 1.1f;
    [SerializeField, Min(0.1f)] private float segmentLength = 5.5f;
    [SerializeField, Min(0.02f)] private float segmentWidth = 0.32f;
    [SerializeField, Min(1f)] private float rotationSpeed = 105f;

    [Header("Beam Contact")]
    [SerializeField, Min(0f)] private float contactDamage = 35f;
    [SerializeField, Min(0.05f)] private float perEnemyHitCooldown = 0.65f;

    [Header("Beam III Rotation Overdrive")]
    [SerializeField, Min(1f)] private float overdriveRotationMultiplier = 1.5f;

    private readonly Dictionary<EnemyHealth, float> nextEnemyHitAt = new();
    private readonly Collider2D[] overlapHits = new Collider2D[MaximumOverlapHits];
    private Material material;
    private Transform constructVisual;
    private LineRenderer glowLine;
    private LineRenderer coreLine;
    private float rotationAngle;
    private int level = 1;

    public AnomalyPowerType Type => AnomalyPowerType.RedBeam;
    public int Level => level;

    public void SetLevel(int value)
    {
        level = AnomalyPowerLevelProfiles.ClampLevel(value);
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void RegisterRuntime()
    {
        AnomalyPowerRuntimeRegistry.Register(
            AnomalyPowerType.RedBeam,
            owner => owner.AddComponent<RedBeamRuntime>());
    }

    public void Activate()
    {
        enabled = true;
        rotationAngle = 0f;
        nextEnemyHitAt.Clear();

        if (constructVisual != null)
            constructVisual.gameObject.SetActive(true);
    }

    public void Deactivate()
    {
        enabled = false;
        nextEnemyHitAt.Clear();

        if (constructVisual != null)
            constructVisual.gameObject.SetActive(false);
    }

    private void Awake()
    {
        material = AnomalyPowerVisuals.CreateMaterial(
            "Beam Construct Runtime Material");
        BuildConstructVisual();
        UpdateConstructGeometry(1f);
    }

    private void OnEnable()
    {
        EnemyHealth.Despawned += HandleEnemyDespawned;
    }

    private void OnDisable()
    {
        EnemyHealth.Despawned -= HandleEnemyDespawned;
    }

    private void Update()
    {
        float attackSize = Mathf.Max(
            0.01f,
            OffensiveAttackContext.GetAttackSize(gameObject));
        float speedMultiplier = level >= 3
            ? overdriveRotationMultiplier
            : 1f;
        rotationAngle = Mathf.Repeat(
            rotationAngle + rotationSpeed * speedMultiplier * Time.deltaTime,
            360f);

        UpdateConstructGeometry(attackSize);
        ApplyContactDamage(attackSize);
    }

    private void UpdateConstructGeometry(float attackSize)
    {
        Vector2 direction = DirectionFromAngle(rotationAngle);
        Vector2 center = GetAnchorPosition() + direction * orbitRadius;
        float finalLength = segmentLength * attackSize;
        Vector2 halfSegment = direction * (finalLength * 0.5f);
        Vector2 endpointA = center + halfSegment;
        Vector2 endpointB = center - halfSegment;

        constructVisual.position = center;
        constructVisual.rotation = Quaternion.Euler(0f, 0f, rotationAngle);
        SetLineGeometry(glowLine, endpointB, endpointA, segmentWidth * 2.1f * attackSize);
        SetLineGeometry(coreLine, endpointB, endpointA, segmentWidth * attackSize);
    }

    private void ApplyContactDamage(float attackSize)
    {
        Vector2 direction = DirectionFromAngle(rotationAngle);
        Vector2 center = GetAnchorPosition() + direction * orbitRadius;
        Vector2 size = new(
            segmentLength * attackSize,
            segmentWidth * attackSize);
        int overlapCount = Physics2D.OverlapBox(
            center,
            size,
            rotationAngle,
            ContactFilter2D.noFilter,
            overlapHits);

        for (int i = 0; i < overlapCount; i++)
        {
            Collider2D collider = overlapHits[i];
            EnemyHealth enemy = collider != null
                ? collider.GetComponentInParent<EnemyHealth>()
                : null;
            if (!IsValidTarget(enemy) ||
                nextEnemyHitAt.TryGetValue(enemy, out float nextHit) &&
                Time.time < nextHit)
            {
                continue;
            }

            nextEnemyHitAt[enemy] = Time.time + perEnemyHitCooldown;
            Vector2 hitPoint = collider.ClosestPoint(center);
            OffensiveAttackContext attack = OffensiveAttackContext.Resolve(
                gameObject,
                contactDamage);
            enemy.TakeDamage(attack.Damage, hitPoint, attack.IsCritical);
        }
    }

    private void BuildConstructVisual()
    {
        GameObject constructObject = new("Beam Construct");
        constructObject.transform.SetParent(transform, false);
        constructVisual = constructObject.transform;

        glowLine = AnomalyPowerVisuals.CreateLine(
            constructVisual,
            "Beam Construct Glow",
            new Color(1f, 0.04f, 0.02f, 0.28f),
            segmentWidth * 2.1f,
            2,
            material);
        glowLine.sortingOrder = 35;

        coreLine = AnomalyPowerVisuals.CreateLine(
            constructVisual,
            "Beam Construct Core",
            new Color(1f, 0.18f, 0.08f, 0.95f),
            segmentWidth,
            2,
            material);
        coreLine.sortingOrder = 38;
    }

    private static void SetLineGeometry(
        LineRenderer line,
        Vector2 start,
        Vector2 end,
        float width)
    {
        if (line == null)
            return;

        line.startWidth = width;
        line.endWidth = width;
        line.SetPosition(0, start);
        line.SetPosition(1, end);
    }

    private Vector2 GetAnchorPosition()
    {
        return transform.position;
    }

    private static Vector2 DirectionFromAngle(float angle)
    {
        float radians = angle * Mathf.Deg2Rad;
        return new Vector2(Mathf.Cos(radians), Mathf.Sin(radians));
    }

    private static bool IsValidTarget(EnemyHealth enemy)
    {
        return enemy != null && !enemy.IsDead &&
            enemy.gameObject.activeInHierarchy;
    }

    private void HandleEnemyDespawned(EnemyHealth enemy)
    {
        if (enemy != null)
            nextEnemyHitAt.Remove(enemy);
    }

    private void OnValidate()
    {
        orbitRadius = Mathf.Max(0f, orbitRadius);
        segmentLength = Mathf.Max(0.1f, segmentLength);
        segmentWidth = Mathf.Max(0.02f, segmentWidth);
        rotationSpeed = Mathf.Max(1f, rotationSpeed);
        contactDamage = Mathf.Max(0f, contactDamage);
        perEnemyHitCooldown = Mathf.Max(0.05f, perEnemyHitCooldown);
        overdriveRotationMultiplier = Mathf.Max(
            1f,
            overdriveRotationMultiplier);
    }

    private void OnDestroy()
    {
        EnemyHealth.Despawned -= HandleEnemyDespawned;

        if (material != null)
            Destroy(material);
    }
}
