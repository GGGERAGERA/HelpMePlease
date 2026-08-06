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

    private const float ProjectileForceMultiplier = 0.5f;

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
    private bool effectsCleared;
    private bool despawning;

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

            Vector2 offset = center -
                (Vector2)affected.Component.transform.position;
            Vector2 velocity = offset.sqrMagnitude > 0.0001f
                ? offset.normalized * gravityForce
                : Vector2.zero;

            if (affected.Component is IAnomalySpeedProjectile)
                velocity *= ProjectileForceMultiplier;

            affected.VelocityTarget.SetAnomalyExternalVelocity(
                this,
                velocity
            );
        }
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

        visualProperties.SetFloat(FadeId, visualFade);
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
