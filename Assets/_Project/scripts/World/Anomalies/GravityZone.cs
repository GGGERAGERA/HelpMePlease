using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(BoxCollider2D))]
public sealed class GravityZone : LocalAnomalyZone
{
    private sealed class AffectedObject
    {
        public readonly Component Component;
        public readonly IAnomalyExternalVelocity VelocityTarget;
        public int ColliderCount;

        public AffectedObject(
            Component component,
            IAnomalyExternalVelocity velocityTarget)
        {
            Component = component;
            VelocityTarget = velocityTarget;
            ColliderCount = 1;
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

    private const float DefaultProjectileForceMultiplier = 0.5f;

    [Header("Visual")]
    [SerializeField] private Material visualMaterial;
    [SerializeField, Range(0.1f, 0.75f)] private float edgeWidth = 0.35f;
    [SerializeField, Min(0f)] private float flowSpeed = 0.65f;
    [SerializeField, Min(0f)] private float centerPulseSpeed = 1.1f;
    [SerializeField, Range(0.1f, 1f)] private float fadeDuration = 0.8f;

    private readonly List<AffectedObject> affectedObjects = new();
    private MeshRenderer visualRenderer;
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

        IAnomalyExternalVelocity target =
            other.GetComponentInParent<IAnomalyExternalVelocity>();

        if (target == null || target.ExternalVelocityComponent == null)
            return;

        Component component = target.ExternalVelocityComponent;
        AffectedObject affected = FindAffected(component);

        if (affected != null)
        {
            affected.ColliderCount++;
            return;
        }

        affectedObjects.Add(new AffectedObject(component, target));

        if (component is CharacterMovement2D)
            Controller?.NotifyLocalZoneEntered(this, Data);
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        IAnomalyExternalVelocity target =
            other.GetComponentInParent<IAnomalyExternalVelocity>();
        Component component = target?.ExternalVelocityComponent;

        if (component == null)
            return;

        AffectedObject affected = FindAffected(component);

        if (affected == null)
            return;

        affected.ColliderCount--;

        if (affected.ColliderCount > 0)
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
        visualProperties.SetVector(RegionSizeId, AreaSize);
        visualProperties.SetFloat(VisualTimeId, Time.unscaledTime);
        visualRenderer.SetPropertyBlock(visualProperties);
    }

    private void OnDisable()
    {
        ClearEffects();
    }
}
