using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(BoxCollider2D))]
public sealed class GravityZone : LocalAnomalyZone
#if UNITY_EDITOR || DEVELOPMENT_BUILD
    , IAnomalyVisualTunable
#endif
{
    private sealed class AffectedObject
    {
        public readonly Component Component;
        public readonly IAnomalyExternalVelocity VelocityTarget;
        public readonly HashSet<Collider2D> Colliders = new();

        public AffectedObject(
            Component component,
            IAnomalyExternalVelocity velocityTarget,
            Collider2D collider)
        {
            Component = component;
            VelocityTarget = velocityTarget;
            Colliders.Add(collider);
        }
    }

    private static readonly int FadeId = Shader.PropertyToID("_Fade");
    private static readonly int EdgeWidthId =
        Shader.PropertyToID("_EdgeWidth");
    private static readonly int FlowSpeedId =
        Shader.PropertyToID("_FlowSpeed");
    private static readonly int CenterPulseSpeedId =
        Shader.PropertyToID("_CenterPulseSpeed");
    private static readonly int RegionSizeId =
        Shader.PropertyToID("_RegionSize");
    private static readonly int VisualTimeId =
        Shader.PropertyToID("_VisualTime");
    private static readonly int InnerColorId =
        Shader.PropertyToID("_InnerColor");
    private static readonly int EdgeColorId =
        Shader.PropertyToID("_EdgeColor");

    private const float DefaultProjectileForceMultiplier = 0.5f;

    [Header("Visual")]
    [SerializeField] private Material visualMaterial;
    [SerializeField, Range(0.1f, 0.75f)] private float edgeWidth = 0.3f;
    [SerializeField, Min(0f)] private float flowSpeed = 0.65f;
    [SerializeField, Min(0f)] private float centerPulseSpeed = 1.1f;
    [SerializeField, Range(0.1f, 1f)] private float fadeDuration = 0.8f;

    [Header("Optional Art Hooks")]
    [SerializeField] private AnomalyArtHookSet artHooks;

    private readonly List<AffectedObject> affectedObjects = new();
    private MeshRenderer visualRenderer;
    private AnomalyArtHooks artHookRuntime;
    private MaterialPropertyBlock visualProperties;
    private float gravityForce;
    private float visualFade;
    private float targetVisualFade;
    private float debugVisualEmphasis = 1f;
    private bool effectsCleared;
    private bool despawning;
    private bool orbitMode;
    private float orbitForceEnemies;
    private float orbitForcePlayer;
    private float orbitForceProjectiles;
    private float inwardForceEnemies;
    private float inwardForcePlayer;
    private float inwardForceProjectiles;
    private System.Predicate<Collider2D> affectedColliderFilter;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
    private AnomalyVisualTuningValues originalVisualValues;
    private AnomalyVisualTuningValues debugVisualValues;
    private bool visualValuesCaptured;
#endif

    public void ConfigureOrbit(
        float enemyOrbitForce,
        float playerOrbitForce,
        float projectileOrbitForce,
        float enemyInwardForce,
        float playerInwardForce,
        float projectileInwardForce)
    {
        orbitMode = true;
        orbitForceEnemies = Mathf.Max(0f, enemyOrbitForce);
        orbitForcePlayer = Mathf.Max(0f, playerOrbitForce);
        orbitForceProjectiles = Mathf.Max(0f, projectileOrbitForce);
        inwardForceEnemies = Mathf.Max(0f, enemyInwardForce);
        inwardForcePlayer = Mathf.Max(0f, playerInwardForce);
        inwardForceProjectiles = Mathf.Max(0f, projectileInwardForce);
    }

    public void ConfigureForce(float force)
    {
        gravityForce = Mathf.Max(0f, force);
    }

    public void ConfigureAffectedColliderFilter(
        System.Predicate<Collider2D> filter)
    {
        affectedColliderFilter = filter;
    }

    public bool ContainsWorldPosition(Vector2 worldPosition)
    {
        return !effectsCleared && AreaCollider != null &&
            AreaCollider.enabled && AreaCollider.OverlapPoint(worldPosition);
    }

    public Vector2 GetPredictedExternalVelocity(
        Vector2 worldPosition,
        Component affectedComponent)
    {
        if (!orbitMode || effectsCleared || affectedComponent == null)
            return Vector2.zero;

        Vector2 center = AreaCollider != null
            ? AreaCollider.bounds.center
            : transform.position;
        return CalculateOrbitVelocity(
            affectedComponent,
            center,
            worldPosition
        );
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    public void ConfigureDebugOrbit(
        float orbitForceEnemies,
        float orbitForcePlayer,
        float orbitForceProjectiles,
        float inwardForceEnemies,
        float inwardForcePlayer,
        float inwardForceProjectiles)
    {
        ConfigureOrbit(
            orbitForceEnemies,
            orbitForcePlayer,
            orbitForceProjectiles,
            inwardForceEnemies,
            inwardForcePlayer,
            inwardForceProjectiles
        );
    }

    public bool DebugContainsWorldPosition(Vector2 worldPosition)
    {
        return ContainsWorldPosition(worldPosition);
    }

    public Vector2 GetDebugPredictedExternalVelocity(
        Vector2 worldPosition,
        Component affectedComponent)
    {
        return GetPredictedExternalVelocity(
            worldPosition,
            affectedComponent
        );
    }

#endif

    public void SetDebugVisualEmphasis(float multiplier)
    {
        debugVisualEmphasis = Mathf.Clamp(multiplier, 1f, 1.75f);
        ApplyVisualProperties();
    }

    private void Awake()
    {
        BuildVisual();
        artHookRuntime = AnomalyArtHooks.Create(
            transform, artHooks, "GRAVITY");
    }

    private void Update()
    {
        visualFade = Mathf.MoveTowards(
            visualFade,
            targetVisualFade,
            Time.unscaledDeltaTime / Mathf.Max(0.01f, fadeDuration)
        );
        ApplyVisualProperties();

        if (!effectsCleared)
            ApplyGravity();

        if (despawning && visualFade <= 0f)
            Destroy(gameObject);
    }

    protected override void InitializeFromData(
        LocalAnomalyData data,
        Vector2 areaSize)
    {
        gravityForce = data.GravityForce;
        ConfigureVisual(areaSize);
        artHookRuntime?.SetBoundarySize(areaSize);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        CaptureOriginalVisualValues();
#endif
        effectsCleared = false;
        despawning = false;
        visualFade = 0f;
        targetVisualFade = 1f;
        ApplyVisualProperties();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (effectsCleared)
            return;

        if (affectedColliderFilter != null && !affectedColliderFilter(other))
            return;

        IAnomalyExternalVelocity target =
            other.GetComponentInParent<IAnomalyExternalVelocity>();

        if (target == null || target.ExternalVelocityComponent == null)
            return;

        Component component = target.ExternalVelocityComponent;
        AffectedObject affected = FindAffected(component);

        if (affected != null)
        {
            affected.Colliders.Add(other);
            return;
        }

        affectedObjects.Add(new AffectedObject(component, target, other));

        if (component is CharacterMovement2D)
            Controller?.NotifyLocalZoneEntered(this, Data);
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (affectedColliderFilter != null && !affectedColliderFilter(other))
            return;

        IAnomalyExternalVelocity target =
            other.GetComponentInParent<IAnomalyExternalVelocity>();
        Component component = target?.ExternalVelocityComponent;

        if (component == null)
            return;

        AffectedObject affected = FindAffected(component);

        if (affected == null)
            return;

        affected.Colliders.Remove(other);

        if (affected.Colliders.Count > 0 && IsActuallyOverlapping(affected))
            return;

        RemoveAffected(affected);
    }

    private void ApplyGravity()
    {
        Vector2 center = AreaCollider != null
            ? AreaCollider.bounds.center
            : transform.position;

        for (int i = affectedObjects.Count - 1; i >= 0; i--)
        {
            AffectedObject affected = affectedObjects[i];

            if (affected.Component == null)
            {
                affectedObjects.RemoveAt(i);
                continue;
            }

            if (!affected.Component.gameObject.activeInHierarchy ||
                affected.Component is Behaviour behaviour &&
                !behaviour.isActiveAndEnabled)
            {
                RemoveAffected(affected);
                continue;
            }

            if (!IsActuallyOverlapping(affected))
            {
                RemoveAffected(affected);
                continue;
            }

            Vector2 position = affected.Component.transform.position;
            Vector2 offset = center - position;
            Vector2 velocity;

            if (orbitMode)
            {
                velocity = CalculateOrbitVelocity(
                    affected.Component,
                    center,
                    position
                );
            }
            else
            {
                velocity = offset.sqrMagnitude > 0.0001f
                    ? offset.normalized * gravityForce
                    : Vector2.zero;

                if (affected.Component is IAnomalySpeedProjectile)
                {
                    velocity *= DefaultProjectileForceMultiplier;
                }
                else if (affected.Component is CharacterMovement2D &&
                    RunStateManager.Instance != null)
                {
                    velocity *= RunStateManager.Instance.AnomalyModifiers
                        .GravityPlayerForceMultiplier;
                }
            }

            affected.VelocityTarget.SetAnomalyExternalVelocity(
                this,
                velocity
            );
        }
    }

    private Vector2 CalculateOrbitVelocity(
        Component component,
        Vector2 center,
        Vector2 position)
    {
        Vector2 radial = position - center;
        float distance = radial.magnitude;

        if (distance <= 0.0001f)
            return Vector2.zero;

        radial /= distance;
        Vector2 tangent = new(-radial.y, radial.x);
        bool projectile = component is IAnomalySpeedProjectile ||
            component is EnemyProjectile;
        float orbitForce;
        float inwardForce;

        if (component is CharacterMovement2D)
        {
            orbitForce = orbitForcePlayer;
            inwardForce = inwardForcePlayer;

            if (RunStateManager.Instance != null)
            {
                float modifier = RunStateManager.Instance.AnomalyModifiers
                    .GravityPlayerForceMultiplier;
                orbitForce *= modifier;
                inwardForce *= modifier;
            }
        }
        else if (projectile)
        {
            orbitForce = orbitForceProjectiles;
            inwardForce = inwardForceProjectiles;
        }
        else
        {
            orbitForce = orbitForceEnemies;
            inwardForce = inwardForceEnemies;
        }

        float radius = Mathf.Max(
            0.1f,
            Mathf.Min(AreaSize.x, AreaSize.y) * 0.5f
        );
        float normalizedDistance = Mathf.Clamp01(distance / radius);
        float distanceMultiplier = Mathf.Sin(
            normalizedDistance * Mathf.PI
        );

        return (
            tangent * orbitForce - radial * inwardForce
        ) * distanceMultiplier;
    }
    private AffectedObject FindAffected(Component component)
    {
        for (int i = 0; i < affectedObjects.Count; i++)
        {
            if (affectedObjects[i].Component == component)
                return affectedObjects[i];
        }

        return null;
    }

    private bool IsActuallyOverlapping(AffectedObject affected)
    {
        if (AreaCollider == null || !AreaCollider.enabled)
            return false;

        affected.Colliders.RemoveWhere(collider => collider == null);
        foreach (Collider2D collider in affected.Colliders)
        {
            if (!collider.enabled || !collider.gameObject.activeInHierarchy)
                continue;

            ColliderDistance2D distance = AreaCollider.Distance(collider);
            if (distance.isOverlapped)
                return true;
        }
        return false;
    }

    private void RemoveAffected(AffectedObject affected)
    {
        if (affected.Component != null)
        {
            affected.VelocityTarget.RemoveAnomalyExternalVelocity(this);

            if (affected.Component is CharacterMovement2D)
                Controller?.NotifyLocalZoneExited(this);
        }

        affectedObjects.Remove(affected);
    }

    private void ClearEffects()
    {
        if (effectsCleared)
            return;

        effectsCleared = true;

        for (int i = affectedObjects.Count - 1; i >= 0; i--)
            RemoveAffected(affectedObjects[i]);

        if (AreaCollider != null)
            AreaCollider.enabled = false;
    }

    public override void Despawn()
    {
        if (despawning)
            return;

        ClearEffects();
        despawning = true;
        targetVisualFade = 0f;

        if (visualRenderer == null)
            Destroy(gameObject);
    }

    private void BuildVisual()
    {
        if (visualMaterial == null)
            return;

        Mesh quad = Resources.GetBuiltinResource<Mesh>("Quad.fbx");

        if (quad == null)
        {
            Debug.LogWarning(
                "[GravityZone] Built-in Quad mesh is unavailable.",
                this
            );
            return;
        }

        GameObject visualObject = new("GravityZoneVisual");
        visualObject.transform.SetParent(transform, false);

        MeshFilter meshFilter = visualObject.AddComponent<MeshFilter>();
        meshFilter.sharedMesh = quad;

        visualRenderer = visualObject.AddComponent<MeshRenderer>();
        visualRenderer.sharedMaterial = visualMaterial;
        visualRenderer.shadowCastingMode =
            UnityEngine.Rendering.ShadowCastingMode.Off;
        visualRenderer.receiveShadows = false;
        visualRenderer.sortingLayerName = "Midground";
        visualRenderer.sortingOrder = 1;

        visualProperties = new MaterialPropertyBlock();
        ApplyVisualProperties();
    }

    private void ConfigureVisual(Vector2 areaSize)
    {
        if (visualRenderer == null)
            return;

        visualRenderer.transform.localScale =
            new Vector3(areaSize.x, areaSize.y, 1f);
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    public string VisualTypeName => "GRAVITY";

    public AnomalyVisualTuningCapabilities VisualCapabilities =>
        AnomalyVisualTuningCapabilities.PrimaryColor |
        AnomalyVisualTuningCapabilities.FillColor |
        AnomalyVisualTuningCapabilities.FillAlpha |
        AnomalyVisualTuningCapabilities.BoundaryWidth |
        AnomalyVisualTuningCapabilities.BoundaryAlpha |
        AnomalyVisualTuningCapabilities.VisualScale |
        AnomalyVisualTuningCapabilities.PulseSpeed |
        AnomalyVisualTuningCapabilities.PatternSpeed;

    public AnomalyVisualTuningValues VisualValues => debugVisualValues;

    public void ApplyVisualValues(AnomalyVisualTuningValues values)
    {
        debugVisualValues = values;
        debugVisualValues.PrimaryColor = ClampColor(values.PrimaryColor);
        debugVisualValues.FillColor = ClampColor(values.FillColor);
        debugVisualValues.FillAlpha = Mathf.Clamp01(values.FillAlpha);
        debugVisualValues.FillColor.a = debugVisualValues.FillAlpha;
        debugVisualValues.BoundaryWidth = Mathf.Clamp(
            values.BoundaryWidth, 0.01f, 3f);
        debugVisualValues.BoundaryAlpha = Mathf.Clamp01(
            values.BoundaryAlpha);
        debugVisualValues.VisualScale = Mathf.Clamp(
            values.VisualScale, 0.25f, 3f);
        debugVisualValues.PulseSpeed = Mathf.Clamp(
            values.PulseSpeed, 0f, 10f);
        debugVisualValues.PatternSpeed = Mathf.Clamp(
            values.PatternSpeed, 0f, 10f);
        edgeWidth = debugVisualValues.BoundaryWidth;
        centerPulseSpeed = debugVisualValues.PulseSpeed;
        flowSpeed = debugVisualValues.PatternSpeed;
        ConfigureVisual(AreaSize * debugVisualValues.VisualScale);
        ApplyVisualProperties();
    }

    public void ResetVisualValues()
    {
        if (visualValuesCaptured)
            ApplyVisualValues(originalVisualValues);
    }

    private void CaptureOriginalVisualValues()
    {
        if (visualValuesCaptured)
            return;

        Color inner = visualMaterial != null &&
            visualMaterial.HasProperty(InnerColorId)
                ? visualMaterial.GetColor(InnerColorId)
                : Color.clear;
        Color edge = visualMaterial != null &&
            visualMaterial.HasProperty(EdgeColorId)
                ? visualMaterial.GetColor(EdgeColorId)
                : Color.white;
        debugVisualValues = new AnomalyVisualTuningValues
        {
            PrimaryColor = edge,
            FillColor = inner,
            FillAlpha = inner.a,
            BoundaryWidth = edgeWidth,
            BoundaryAlpha = 1f,
            VisualScale = 1f,
            PulseSpeed = centerPulseSpeed,
            PatternSpeed = flowSpeed
        };
        originalVisualValues = debugVisualValues;
        visualValuesCaptured = true;
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
#endif

    private void ApplyVisualProperties()
    {
        if (visualRenderer == null || visualProperties == null)
            return;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        visualProperties.SetFloat(FadeId, visualFade * debugVisualEmphasis);
#else
        visualProperties.SetFloat(FadeId, visualFade);
#endif
        visualProperties.SetFloat(EdgeWidthId, edgeWidth);
        visualProperties.SetFloat(FlowSpeedId, flowSpeed);
        visualProperties.SetFloat(CenterPulseSpeedId, centerPulseSpeed);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (visualValuesCaptured)
        {
            Color fill = debugVisualValues.FillColor;
            fill.a = debugVisualValues.FillAlpha;
            Color edge = debugVisualValues.PrimaryColor;
            edge.a *= debugVisualValues.BoundaryAlpha;
            visualProperties.SetColor(InnerColorId, fill);
            visualProperties.SetColor(EdgeColorId, edge);
            visualProperties.SetVector(
                RegionSizeId,
                AreaSize * debugVisualValues.VisualScale
            );
        }
        else
#endif
            visualProperties.SetVector(RegionSizeId, AreaSize);
        visualProperties.SetFloat(VisualTimeId, Time.unscaledTime);
        visualRenderer.SetPropertyBlock(visualProperties);
    }

    private void OnDisable()
    {
        ClearEffects();
    }
}
