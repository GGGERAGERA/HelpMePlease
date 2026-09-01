using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(BoxCollider2D))]
public sealed class GlitchZone : LocalAnomalyZone
#if UNITY_EDITOR || DEVELOPMENT_BUILD
    , IAnomalyVisualTunable
#endif
{
    private sealed class AffectedObject
    {
        public readonly Component Component;
        public readonly List<Collider2D> Colliders = new();

        public AffectedObject(Component component, Collider2D collider)
        {
            Component = component;
            Colliders.Add(collider);
        }
    }

    private static readonly int FadeId = Shader.PropertyToID("_Fade");
    private static readonly int EdgeWidthId =
        Shader.PropertyToID("_EdgeWidth");
    private static readonly int RegionSizeId =
        Shader.PropertyToID("_RegionSize");
    private static readonly int VisualTimeId =
        Shader.PropertyToID("_VisualTime");
    private static readonly int PulseId = Shader.PropertyToID("_Pulse");
    private static readonly int InnerColorId =
        Shader.PropertyToID("_InnerColor");
    private static readonly int EdgeColorId =
        Shader.PropertyToID("_EdgeColor");

    private const int PositionAttempts = 8;
    private const int OverlapCapacity = 32;
    private const float PulseVisualDuration = 0.16f;

    [Header("Visual")]
    [SerializeField] private Material visualMaterial;
    [SerializeField] private Transform visualRoot;
    [SerializeField] private MeshRenderer visualRenderer;
    [Tooltip("Editor-only prefab preview. Runtime Initialize size still wins.")]
    [SerializeField] private Vector2 previewSize = new(4.5f, 3.2f);
    [SerializeField, Range(0.1f, 0.75f)] private float edgeWidth = 0.3f;
    [SerializeField, Range(0.1f, 1f)] private float fadeDuration = 0.55f;

    private readonly List<AffectedObject> affectedObjects = new();
    private readonly Collider2D[] overlapResults =
        new Collider2D[OverlapCapacity];

    private MaterialPropertyBlock visualProperties;
    private GameplayAreaService gameplayArea;
    private float glitchInterval;
    private float glitchDistance;
    private float pulseTimer;
    private float pulseVisual;
    private float visualFade;
    private float targetVisualFade;
    private bool effectsCleared;
    private bool despawning;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
    private AnomalyVisualTuningValues originalVisualValues;
    private AnomalyVisualTuningValues debugVisualValues;
    private bool visualValuesCaptured;
#endif

    private void Awake()
    {
        ResolveVisual();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (Application.isPlaying || visualRoot == null)
            return;

        previewSize = new Vector2(
            Mathf.Max(0.1f, previewSize.x),
            Mathf.Max(0.1f, previewSize.y));
        if (TryGetComponent(out BoxCollider2D previewCollider))
            previewCollider.size = previewSize;
        ConfigureVisual(previewSize);

        if (visualRenderer == null)
            visualRenderer = visualRoot.GetComponentInChildren<MeshRenderer>(true);

        if (visualRenderer == null)
            return;

        MaterialPropertyBlock previewProperties = new();
        visualRenderer.GetPropertyBlock(previewProperties);
        previewProperties.SetFloat(FadeId, 1f);
        previewProperties.SetFloat(EdgeWidthId, edgeWidth);
        previewProperties.SetVector(RegionSizeId, previewSize);
        previewProperties.SetFloat(PulseId, 0f);
        visualRenderer.SetPropertyBlock(previewProperties);
    }
#endif

    private void OnEnable()
    {
        EnemyHealth.Despawned += HandleEnemyDespawned;
        AnomalyProjectileLifecycle.Disabled += HandleProjectileDisabled;
        AnomalySpeedPickupLifecycle.Disabled += HandlePickupDisabled;
    }

    private void Update()
    {
        float deltaTime = Time.deltaTime;

        visualFade = Mathf.MoveTowards(
            visualFade,
            targetVisualFade,
            Time.unscaledDeltaTime / Mathf.Max(0.01f, fadeDuration)
        );
        pulseVisual = Mathf.MoveTowards(
            pulseVisual,
            0f,
            deltaTime / PulseVisualDuration
        );

        if (!effectsCleared && deltaTime > 0f)
        {
            pulseTimer -= deltaTime;

            if (pulseTimer <= 0f)
            {
                Pulse();
                pulseTimer = glitchInterval;
            }
        }

        ApplyVisualProperties();

        if (despawning && visualFade <= 0f)
            Destroy(gameObject);
    }

    protected override void InitializeFromData(
        LocalAnomalyData data,
        Vector2 areaSize)
    {
        glitchInterval = data.GlitchInterval;
        glitchDistance = data.GlitchDistance;
        pulseTimer = glitchInterval;
        gameplayArea = GameplayAreaService.Instance;

        if (gameplayArea == null)
            gameplayArea = FindFirstObjectByType<GameplayAreaService>();

        ConfigureVisual(areaSize);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        CaptureOriginalVisualValues();
#endif
        effectsCleared = false;
        despawning = false;
        pulseVisual = 0f;
        visualFade = 0f;
        targetVisualFade = 1f;
        ApplyVisualProperties();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (effectsCleared)
            return;

        Component component = FindSupportedComponent(other);

        if (component == null)
            return;

        AffectedObject affected = FindAffected(component);

        if (affected != null)
        {
            if (!affected.Colliders.Contains(other))
                affected.Colliders.Add(other);

            return;
        }

        affectedObjects.Add(new AffectedObject(component, other));

        if (component is CharacterMovement2D)
            Controller?.NotifyLocalZoneEntered(this, Data);
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        Component component = FindSupportedComponent(other);

        if (component == null)
            return;

        AffectedObject affected = FindAffected(component);

        if (affected == null)
            return;

        affected.Colliders.Remove(other);

        if (affected.Colliders.Count == 0)
            RemoveAffected(affected);
    }

    private void Pulse()
    {
        pulseVisual = 1f;
        bool movedAny = false;
        Physics2D.SyncTransforms();

        for (int i = affectedObjects.Count - 1; i >= 0; i--)
        {
            AffectedObject affected = affectedObjects[i];

            if (!IsActive(affected.Component))
            {
                RemoveAffected(affected);
                continue;
            }

            RemoveMissingColliders(affected);

            if (affected.Colliders.Count == 0)
            {
                RemoveAffected(affected);
                continue;
            }

            if (!TryFindSafeOffset(affected, out Vector2 offset))
                continue;

            Teleport(affected.Component, offset);
            movedAny = true;
        }

        if (movedAny)
            Physics2D.SyncTransforms();
    }

    private bool TryFindSafeOffset(
        AffectedObject affected,
        out Vector2 offset)
    {
        offset = Vector2.zero;

        if (glitchDistance <= Mathf.Epsilon || gameplayArea == null ||
            !TryGetBounds(affected, out Bounds bounds))
        {
            return false;
        }

        float edgePadding = new Vector2(
            bounds.extents.x,
            bounds.extents.y
        ).magnitude;

        for (int i = 0; i < PositionAttempts; i++)
        {
            Vector2 direction = Random.insideUnitCircle;

            if (direction.sqrMagnitude <= 0.0001f)
                continue;

            Vector2 candidateOffset =
                direction.normalized * glitchDistance;
            Vector2 candidateCenter =
                (Vector2)bounds.center + candidateOffset;

            if (!gameplayArea.IsInsidePlayableArea(
                    candidateCenter,
                    edgePadding))
            {
                continue;
            }

            if (OverlapsObstacle(affected, bounds, candidateOffset))
                continue;

            offset = candidateOffset;
            return true;
        }

        return false;
    }

    private bool OverlapsObstacle(
        AffectedObject affected,
        Bounds bounds,
        Vector2 offset)
    {
        int collisionMask = 0;

        for (int i = 0; i < affected.Colliders.Count; i++)
        {
            Collider2D collider = affected.Colliders[i];

            if (collider != null)
            {
                collisionMask |= Physics2D.GetLayerCollisionMask(
                    collider.gameObject.layer
                );
            }
        }

        ContactFilter2D filter = new()
        {
            useTriggers = false,
            useLayerMask = true,
            layerMask = collisionMask
        };
        int hitCount = Physics2D.OverlapBox(
            (Vector2)bounds.center + offset,
            bounds.size,
            0f,
            filter,
            overlapResults
        );

        if (hitCount >= overlapResults.Length)
            return true;

        for (int i = 0; i < hitCount; i++)
        {
            Collider2D hit = overlapResults[i];

            if (hit == null || IsOwnCollider(affected, hit) ||
                FindSupportedComponent(hit) != null)
            {
                continue;
            }

            return true;
        }

        return false;
    }

    private static void Teleport(Component component, Vector2 offset)
    {
        Rigidbody2D body = component.GetComponent<Rigidbody2D>();

        if (body == null)
            body = component.GetComponentInParent<Rigidbody2D>();

        if (body != null)
        {
            body.position += offset;
            return;
        }

        component.transform.position += (Vector3)offset;
    }

    private static bool TryGetBounds(
        AffectedObject affected,
        out Bounds bounds)
    {
        bounds = default;
        bool hasBounds = false;

        for (int i = 0; i < affected.Colliders.Count; i++)
        {
            Collider2D collider = affected.Colliders[i];

            if (collider == null || !collider.enabled)
                continue;

            if (!hasBounds)
            {
                bounds = collider.bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(collider.bounds);
            }
        }

        return hasBounds;
    }

    private static bool IsOwnCollider(
        AffectedObject affected,
        Collider2D collider)
    {
        for (int i = 0; i < affected.Colliders.Count; i++)
        {
            if (affected.Colliders[i] == collider)
                return true;
        }

        return false;
    }

    private static void RemoveMissingColliders(AffectedObject affected)
    {
        for (int i = affected.Colliders.Count - 1; i >= 0; i--)
        {
            Collider2D collider = affected.Colliders[i];

            if (collider == null || !collider.enabled ||
                !collider.gameObject.activeInHierarchy)
            {
                affected.Colliders.RemoveAt(i);
            }
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
        affectedObjects.Remove(affected);

        if (affected.Component is CharacterMovement2D)
            Controller?.NotifyLocalZoneExited(this);
    }

    private void RemoveAffected(Component component)
    {
        AffectedObject affected = FindAffected(component);

        if (affected != null)
            RemoveAffected(affected);
    }

    private static bool IsActive(Component component)
    {
        return component != null && component.gameObject.activeInHierarchy;
    }

    private static Component FindSupportedComponent(Collider2D other)
    {
        CharacterMovement2D player =
            other.GetComponentInParent<CharacterMovement2D>();

        if (player != null)
            return player;

        EnemyMovement enemy = other.GetComponentInParent<EnemyMovement>();

        if (enemy != null)
            return enemy;

        Bullet bullet = other.GetComponentInParent<Bullet>();

        if (bullet != null)
            return bullet;

        ExplosiveProjectile explosive =
            other.GetComponentInParent<ExplosiveProjectile>();

        if (explosive != null)
            return explosive;

        EnemyProjectile enemyProjectile =
            other.GetComponentInParent<EnemyProjectile>();

        if (enemyProjectile != null)
            return enemyProjectile;

        ExperiencePickup experience =
            other.GetComponentInParent<ExperiencePickup>();

        if (experience != null)
            return experience;

        return other.GetComponentInParent<GoldenCoinPickup>();
    }

    private void HandleEnemyDespawned(EnemyHealth enemy)
    {
        if (enemy == null)
            return;

        EnemyMovement movement = enemy.GetComponentInChildren<EnemyMovement>();

        if (movement != null)
            RemoveAffected(movement);
    }

    private void HandleProjectileDisabled(Component projectile)
    {
        if (!ReferenceEquals(projectile, null))
            RemoveAffected(projectile);
    }

    private void HandlePickupDisabled(Component pickup)
    {
        if (!ReferenceEquals(pickup, null))
            RemoveAffected(pickup);
    }

    private void ClearEffects()
    {
        if (effectsCleared)
            return;

        effectsCleared = true;

        for (int i = affectedObjects.Count - 1; i >= 0; i--)
        {
            if (affectedObjects[i].Component is CharacterMovement2D)
                Controller?.NotifyLocalZoneExited(this);
        }

        affectedObjects.Clear();

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

    private void ResolveVisual()
    {
        if (visualRoot == null)
        {
            Debug.LogWarning(
                "[GlitchZone] Serialized VisualRoot is missing.",
                this);
            return;
        }

        if (visualRenderer == null)
            visualRenderer = visualRoot.GetComponentInChildren<MeshRenderer>(true);

        if (visualRenderer == null)
        {
            Debug.LogWarning(
                "[GlitchZone] Serialized VisualRoot has no MeshRenderer.",
                this);
            return;
        }

        visualProperties = new MaterialPropertyBlock();
        ApplyVisualProperties();
    }

    private void ConfigureVisual(Vector2 areaSize)
    {
        if (visualRoot == null)
            return;

        visualRoot.localScale =
            new Vector3(areaSize.x, areaSize.y, 1f);
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    public string VisualTypeName => "GLITCH";

    public AnomalyVisualTuningCapabilities VisualCapabilities =>
        AnomalyVisualTuningCapabilities.PrimaryColor |
        AnomalyVisualTuningCapabilities.FillColor |
        AnomalyVisualTuningCapabilities.FillAlpha |
        AnomalyVisualTuningCapabilities.BoundaryWidth |
        AnomalyVisualTuningCapabilities.BoundaryAlpha |
        AnomalyVisualTuningCapabilities.VisualScale;

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
        edgeWidth = debugVisualValues.BoundaryWidth;
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

        Material sourceMaterial = visualRenderer != null
            ? visualRenderer.sharedMaterial
            : visualMaterial;
        Color inner = sourceMaterial != null &&
            sourceMaterial.HasProperty(InnerColorId)
                ? sourceMaterial.GetColor(InnerColorId)
                : Color.clear;
        Color edge = sourceMaterial != null &&
            sourceMaterial.HasProperty(EdgeColorId)
                ? sourceMaterial.GetColor(EdgeColorId)
                : Color.white;
        debugVisualValues = new AnomalyVisualTuningValues
        {
            PrimaryColor = edge,
            FillColor = inner,
            FillAlpha = inner.a,
            BoundaryWidth = edgeWidth,
            BoundaryAlpha = 1f,
            VisualScale = 1f
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

        visualProperties.SetFloat(FadeId, visualFade);
        visualProperties.SetFloat(EdgeWidthId, edgeWidth);
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
        visualProperties.SetFloat(PulseId, pulseVisual);
        visualRenderer.SetPropertyBlock(visualProperties);
    }

    private void OnDisable()
    {
        EnemyHealth.Despawned -= HandleEnemyDespawned;
        AnomalyProjectileLifecycle.Disabled -= HandleProjectileDisabled;
        AnomalySpeedPickupLifecycle.Disabled -= HandlePickupDisabled;
        ClearEffects();
    }
}
