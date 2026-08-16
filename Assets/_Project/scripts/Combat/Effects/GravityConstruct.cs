using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class GravityConstruct :
    AnomalyCoreConstruct,
    IAnomalyWeaponPayload,
    IAnomalyEvolutionPower,
    IAnomalyPowerRuntime
{
    private const float VisualRadius = 0.78f;
    private const float CoreRadius = 0.38f;
    private const float GlowRadius = 0.96f;
    private const float TrailHistorySeconds = 0.32f;

    [Header("Source")]
    [SerializeField] private BaseWeapon sourceWeapon;
    [SerializeField] private Transform anchor;

    [Header("Base Gravity")]
    [Min(0f)]
    [SerializeField] private float orbitRadius = 3f;
    [SerializeField] private float orbitSpeed = 145f;
    [Min(0f)]
    [SerializeField] private float damageRadius = VisualRadius;
    [Min(0f)]
    [SerializeField] private float baseDamage = 65f;
    [Min(0.05f)]
    [SerializeField] private float damageTickInterval = 0.42f;

    [Header("Optional Weapon Payload")]
    [SerializeField] private bool weaponPayloadEnabled;
    [Range(0.05f, 2f)]
    [SerializeField] private float payloadFireRateMultiplier = 0.7f;
    [SerializeField] private float angleOffset;

    private EvolutionDefinition evolutionDefinition;

    private readonly List<EnemyHealth> enemySnapshot = new();
    private Transform gravityEntity;
    private Material visualMaterial;
    private float orbitAngle;
    private float damageTickTimer;
    private float fireTimer;
    private float overdriveAngle;
    private int level = 1;

    public AnomalyPowerType Type => AnomalyPowerType.GravityOrb;
    public int Level => level;
    public BaseWeapon SourceWeapon => sourceWeapon;
    public Transform Anchor => anchor;
    public bool WeaponPayloadEnabled => weaponPayloadEnabled;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void RegisterRuntime()
    {
        AnomalyPowerRuntimeRegistry.Register(
            AnomalyPowerType.GravityOrb,
            owner => owner.AddComponent<GravityConstruct>());
    }

    public void SetLevel(int value)
    {
        level = AnomalyPowerLevelProfiles.ClampLevel(value);
    }

    public void Activate()
    {
        enabled = true;

        if (gravityEntity != null)
            gravityEntity.gameObject.SetActive(true);
    }

    public void Deactivate()
    {
        enabled = false;

        if (gravityEntity != null)
            gravityEntity.gameObject.SetActive(false);
    }

    public override void Configure(
        Transform anchorTransform,
        BaseWeapon optionalWeapon)
    {
        sourceWeapon = optionalWeapon;
        anchor = anchorTransform != null ? anchorTransform : transform;
    }

    public void SetWeaponPayloadEnabled(bool enabled)
    {
        weaponPayloadEnabled = enabled;

        if (!weaponPayloadEnabled)
            fireTimer = 0f;
    }

    public void ConfigureWeaponPayload(
        BaseWeapon weapon,
        float fireRateMultiplier)
    {
        sourceWeapon = weapon;
        payloadFireRateMultiplier = Mathf.Clamp(
            fireRateMultiplier, 0.05f, 2f);
    }

    public void ConfigureEvolutionPayload(
        BaseWeapon weapon,
        EvolutionDefinition definition,
        int anomalyLevel)
    {
        evolutionDefinition = definition;
        SetLevel(anomalyLevel);
        ConfigureWeaponPayload(
            weapon,
            definition != null
                ? definition.PayloadFireRateMultiplier
                : payloadFireRateMultiplier);
        SetWeaponPayloadEnabled(definition != null && weapon != null &&
            level >= 2);
    }

    public void DisableEvolutionPayload()
    {
        evolutionDefinition = null;
        SetWeaponPayloadEnabled(false);
    }

    public override void Shutdown()
    {
        base.Shutdown();
        Deactivate();
    }

    private void Awake()
    {
        CreateGravityEntity();
    }

    private void OnEnable()
    {
        if (gravityEntity != null)
            gravityEntity.gameObject.SetActive(true);
    }

    private void Update()
    {
        UpdateOrbit();
        ApplyBaseGravityDamage();
        EmitWeaponPayload();
    }

    private void UpdateOrbit()
    {
        orbitAngle = Mathf.Repeat(
            orbitAngle + orbitSpeed * Time.deltaTime,
            360f
        );

        if (gravityEntity == null)
            return;

        float angle = orbitAngle + angleOffset;
        Vector2 direction = DirectionFromAngle(angle);
        gravityEntity.position = GetCenter() +
            direction * Mathf.Max(0f, orbitRadius);
        float size = OffensiveAttackContext.GetAttackSize(gameObject) *
            AnomalyPowerLevelProfiles.GravitySize(level);
        gravityEntity.localScale = Vector3.one * size;
    }

    private void ApplyBaseGravityDamage()
    {
        damageTickTimer += Time.deltaTime;
        float interval = Mathf.Max(0.05f, damageTickInterval);

        if (damageTickTimer < interval || gravityEntity == null)
            return;

        damageTickTimer = 0f;
        enemySnapshot.Clear();

        foreach (EnemyHealth enemy in EnemyHealth.ActiveInstances)
            enemySnapshot.Add(enemy);

        Vector2 hitPoint = gravityEntity.position;
        OffensiveAttackContext attack = OffensiveAttackContext.Resolve(
            gameObject,
            baseDamage * AnomalyPowerLevelProfiles.GravityDamage(level));
        float scaledRadius = damageRadius *
            AnomalyPowerLevelProfiles.GravitySize(level) *
            attack.AttackSizeMultiplier;
        float radiusSquared = scaledRadius * scaledRadius;
        foreach (EnemyHealth enemy in enemySnapshot)
        {
            if (enemy == null || enemy.IsDead)
                continue;

            Vector2 damagePoint = GetClosestDamagePoint(enemy, hitPoint);
            Vector2 offset = damagePoint - hitPoint;

            if (offset.sqrMagnitude > radiusSquared)
                continue;

            enemy.TakeDamage(attack.Damage, damagePoint, attack.IsCritical);
        }
    }

    private void EmitWeaponPayload()
    {
        if (!weaponPayloadEnabled || sourceWeapon == null ||
            gravityEntity == null)
        {
            return;
        }

        fireTimer += Time.deltaTime;

        bool overdrive = level >= 3 && evolutionDefinition != null;
        float interval = overdrive
            ? sourceWeapon.GetBaseAttackCooldown() /
                evolutionDefinition.OverdriveFireRateMultiplier
            : sourceWeapon.GetBaseAttackCooldown() /
                Mathf.Max(0.05f, payloadFireRateMultiplier);
        if (fireTimer < Mathf.Max(0.05f, interval))
            return;

        fireTimer = 0f;
        Vector2 origin = gravityEntity.position;
        if (overdrive)
        {
            EmitRotatingOverdriveShot(origin);
            return;
        }

        if (!TryFindNearestEnemy(
                origin,
                sourceWeapon.GetRange(),
                out EnemyHealth target))
            return;

        Vector2 direction = (Vector2)target.transform.position - origin;
        if (direction.sqrMagnitude <= Mathf.Epsilon)
            return;

        direction.Normalize();

        sourceWeapon.EmitAttack(origin, direction);
    }

    private void EmitRotatingOverdriveShot(Vector2 origin)
    {
        float jitter = Random.Range(
            -evolutionDefinition.OverdriveDirectionJitter,
            evolutionDefinition.OverdriveDirectionJitter);
        Vector2 direction = DirectionFromAngle(overdriveAngle + jitter);
        sourceWeapon.EmitAttack(
            origin,
            direction,
            evolutionDefinition.PayloadRangeMultiplier);
        overdriveAngle = Mathf.Repeat(
            overdriveAngle + evolutionDefinition.OverdriveAngularStep,
            360f);
    }

    private static Vector2 GetClosestDamagePoint(
        EnemyHealth enemy,
        Vector2 origin)
    {
        Collider2D collider = enemy != null
            ? enemy.GetComponentInChildren<Collider2D>()
            : null;
        return collider != null
            ? collider.ClosestPoint(origin)
            : (Vector2)enemy.transform.position;
    }

    private static bool TryFindNearestEnemy(
        Vector2 origin,
        float range,
        out EnemyHealth nearest)
    {
        nearest = null;
        float nearestDistanceSquared = float.PositiveInfinity;
        float rangeSquared = Mathf.Max(0.1f, range) *
            Mathf.Max(0.1f, range);

        foreach (EnemyHealth enemy in EnemyHealth.ActiveInstances)
        {
            if (enemy == null || enemy.IsDead ||
                !enemy.gameObject.activeInHierarchy)
            {
                continue;
            }

            float distanceSquared =
                ((Vector2)enemy.transform.position - origin).sqrMagnitude;
            if (distanceSquared > rangeSquared ||
                distanceSquared >= nearestDistanceSquared)
                continue;

            nearest = enemy;
            nearestDistanceSquared = distanceSquared;
        }

        return nearest != null;
    }

    private void CreateGravityEntity()
    {
        GameObject entityObject = new("Gravity Construct Orb");
        entityObject.transform.SetParent(transform, false);
        gravityEntity = entityObject.transform;
        gravityEntity.position = GetCenter() + Vector2.right * orbitRadius;

        visualMaterial = AnomalyPowerVisuals.CreateMaterial(
            "Gravity Construct Runtime Material"
        );

        LineRenderer glow = AnomalyPowerVisuals.CreateLine(
            gravityEntity,
            "Gravity Construct Glow",
            new Color(0.45f, 0.18f, 1f, 0.25f),
            0.22f,
            33,
            visualMaterial
        );
        ConfigureRing(glow, GlowRadius);
        glow.sortingOrder = 34;

        LineRenderer ring = AnomalyPowerVisuals.CreateLine(
            gravityEntity,
            "Gravity Construct Outer Ring",
            new Color(0.55f, 0.15f, 1f, 1f),
            0.16f,
            33,
            visualMaterial
        );
        ring.endColor = new Color(0.1f, 0.85f, 1f, 1f);
        ConfigureRing(ring, VisualRadius);
        ring.sortingOrder = 36;

        LineRenderer core = AnomalyPowerVisuals.CreateLine(
            gravityEntity,
            "Gravity Construct Core",
            new Color(0.72f, 0.4f, 1f, 1f),
            CoreRadius * 2f,
            2,
            visualMaterial
        );
        core.useWorldSpace = false;
        core.numCapVertices = 10;
        core.SetPosition(0, new Vector3(-0.025f, 0f, 0f));
        core.SetPosition(1, new Vector3(0.025f, 0f, 0f));
        core.sortingOrder = 37;

        GameObject trailObject = new("Gravity Construct Trail");
        trailObject.transform.SetParent(gravityEntity, false);
        TrailRenderer trail = trailObject.AddComponent<TrailRenderer>();
        trail.time = TrailHistorySeconds;
        trail.minVertexDistance = 0.08f;
        trail.startWidth = 0.34f;
        trail.endWidth = 0.02f;
        trail.numCapVertices = 5;
        trail.numCornerVertices = 4;
        trail.startColor = new Color(0.7f, 0.2f, 1f, 0.75f);
        trail.endColor = new Color(0.05f, 0.7f, 1f, 0f);
        trail.sortingLayerName = "Effects";
        trail.sortingOrder = 35;

        if (visualMaterial != null)
            trail.sharedMaterial = visualMaterial;

        trail.Clear();
    }

    private Vector2 GetCenter()
    {
        return anchor != null
            ? (Vector2)anchor.position
            : (Vector2)transform.position;
    }

    private static void ConfigureRing(LineRenderer ring, float radius)
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

    private static Vector2 DirectionFromAngle(float angle)
    {
        float radians = angle * Mathf.Deg2Rad;
        return new Vector2(Mathf.Cos(radians), Mathf.Sin(radians));
    }

    private void OnValidate()
    {
        orbitRadius = Mathf.Max(0f, orbitRadius);
        damageRadius = Mathf.Max(0f, damageRadius);
        baseDamage = Mathf.Max(0f, baseDamage);
        damageTickInterval = Mathf.Max(0.05f, damageTickInterval);
        payloadFireRateMultiplier = Mathf.Clamp(
            payloadFireRateMultiplier, 0.05f, 2f);
    }

    private void OnDestroy()
    {
        if (gravityEntity != null)
            Destroy(gravityEntity.gameObject);

        if (visualMaterial != null)
            Destroy(visualMaterial);
    }
}
