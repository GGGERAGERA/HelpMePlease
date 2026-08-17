using System.Collections.Generic;
using UnityEngine;

public sealed class BerserkZone : LocalAnomalyZone
#if UNITY_EDITOR || DEVELOPMENT_BUILD
    , IAnomalyVisualTunable
#endif
{
    private static readonly int FadeId = Shader.PropertyToID("_Fade");
    private static readonly int EdgeWidthId =
        Shader.PropertyToID("_EdgeWidth");
    private static readonly int PulseSpeedId =
        Shader.PropertyToID("_PulseSpeed");
    private static readonly int PulseStrengthId =
        Shader.PropertyToID("_PulseStrength");
    private static readonly int PulseSharpnessId =
        Shader.PropertyToID("_PulseSharpness");
    private static readonly int RegionSizeId =
        Shader.PropertyToID("_RegionSize");
    private static readonly int InnerPatternIntensityId =
        Shader.PropertyToID("_InnerPatternIntensity");
    private static readonly int InnerPatternSpeedId =
        Shader.PropertyToID("_InnerPatternSpeed");
    private static readonly int InnerPatternScaleId =
        Shader.PropertyToID("_InnerPatternScale");
    private static readonly int WarningPulseFrequencyId =
        Shader.PropertyToID("_WarningPulseFrequency");
    private static readonly int VisualTimeId =
        Shader.PropertyToID("_VisualTime");
    private static readonly int InnerColorId =
        Shader.PropertyToID("_InnerColor");
    private static readonly int EdgeColorId =
        Shader.PropertyToID("_EdgeColor");

    [Header("Visual")]
    [SerializeField] private Material visualMaterial;
    [SerializeField] private Transform visualRoot;
    [SerializeField] private MeshRenderer visualRenderer;
    [Tooltip("Editor-only prefab preview. Runtime Initialize size still wins.")]
    [SerializeField] private Vector2 previewSize = new(4.5f, 3.2f);
    [SerializeField, Range(0.1f, 0.75f)] private float edgeWidth = 0.35f;
    [SerializeField, Min(0f)] private float pulseSpeed = 0.35f;
    [SerializeField, Range(0f, 1f)] private float pulseStrength = 0.08f;
    [SerializeField, Min(1f)] private float pulseSharpness = 1f;
    [SerializeField, Range(0f, 0.25f)]
    private float innerPatternIntensity = 0.14f;
    [SerializeField, Min(0f)] private float innerPatternSpeed = 1.8f;
    [SerializeField, Min(0.5f)] private float innerPatternScale = 3.5f;
    [SerializeField, Min(0f)] private float warningPulseFrequency = 0.22f;
    [SerializeField, Range(0.6f, 1f)] private float fadeDuration = 0.8f;

    [Header("Enemy Tint")]
    [SerializeField] private Color enemyTint =
        new(1f, 0.42f, 0.38f, 1f);

    private readonly Dictionary<EnemyMovement, int>
        enemyColliderCounts = new();

    private MaterialPropertyBlock visualProperties;
    private float speedMultiplier = 1.5f;
    private float visualFade;
    private float targetVisualFade;
    private int playerColliderCount;
    private bool initialized;
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
        previewProperties.SetFloat(PulseSpeedId, pulseSpeed);
        previewProperties.SetFloat(PulseStrengthId, pulseStrength);
        previewProperties.SetFloat(PulseSharpnessId, pulseSharpness);
        previewProperties.SetVector(RegionSizeId, previewSize);
        previewProperties.SetFloat(
            InnerPatternIntensityId, innerPatternIntensity);
        previewProperties.SetFloat(InnerPatternSpeedId, innerPatternSpeed);
        previewProperties.SetFloat(InnerPatternScaleId, innerPatternScale);
        previewProperties.SetFloat(
            WarningPulseFrequencyId, warningPulseFrequency);
        visualRenderer.SetPropertyBlock(previewProperties);
    }
#endif

    private void OnEnable()
    {
        EnemyHealth.Despawned += HandleEnemyDespawned;
    }

    private void Update()
    {
        if (visualRenderer == null)
            return;

        visualFade = Mathf.MoveTowards(
            visualFade,
            targetVisualFade,
            Time.unscaledDeltaTime / Mathf.Max(0.01f, fadeDuration)
        );
        ApplyVisualProperties();

        if (despawning && visualFade <= 0f)
            Destroy(gameObject);
    }

    protected override void InitializeFromData(
        LocalAnomalyData data,
        Vector2 areaSize)
    {
        speedMultiplier = data.EnemySpeedMultiplier;
        ConfigureVisual(areaSize);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        CaptureOriginalVisualValues();
#endif
        effectsCleared = false;
        despawning = false;
        visualFade = 0f;
        targetVisualFade = 1f;
        ApplyVisualProperties();
        initialized = true;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!initialized || effectsCleared)
            return;

        PlayerHealth enteringPlayer =
            other.GetComponentInParent<PlayerHealth>();

        if (enteringPlayer != null)
        {
            playerColliderCount++;

            if (playerColliderCount == 1)
                Controller?.NotifyLocalZoneEntered(this, Data);

            return;
        }

        EnemyMovement movement =
            other.GetComponentInParent<EnemyMovement>();

        if (movement == null)
            return;

        if (enemyColliderCounts.TryGetValue(
                movement,
                out int colliderCount))
        {
            enemyColliderCounts[movement] = colliderCount + 1;
            return;
        }

        enemyColliderCounts[movement] = 1;
        ApplyZoneEffect(movement);
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        PlayerHealth exitingPlayer =
            other.GetComponentInParent<PlayerHealth>();

        if (exitingPlayer != null)
        {
            bool wasInside = playerColliderCount > 0;
            playerColliderCount = Mathf.Max(
                0,
                playerColliderCount - 1
            );

            if (wasInside && playerColliderCount == 0)
                Controller?.NotifyLocalZoneExited(this);

            return;
        }

        EnemyMovement movement =
            other.GetComponentInParent<EnemyMovement>();

        if (movement == null ||
            !enemyColliderCounts.TryGetValue(
                movement,
                out int colliderCount))
        {
            return;
        }

        colliderCount--;

        if (colliderCount > 0)
        {
            enemyColliderCounts[movement] = colliderCount;
            return;
        }

        enemyColliderCounts.Remove(movement);
        RemoveZoneEffect(movement);
    }

    private void ApplyZoneEffect(EnemyMovement movement)
    {
        if (movement == null)
            return;

        EnemyHealth enemy = movement.GetComponentInParent<EnemyHealth>();
        EnemyAnomalyEffects.GetOrCreate(enemy)?.EnterZone(
            this,
            speedMultiplier,
            enemyTint
        );
    }

    private void RemoveZoneEffect(EnemyMovement movement)
    {
        if (ReferenceEquals(movement, null))
            return;

        EnemyHealth enemy = movement != null
            ? movement.GetComponentInParent<EnemyHealth>()
            : null;
        enemy?.GetComponent<EnemyAnomalyEffects>()?.ExitZone(this);
    }

    private void HandleEnemyDespawned(EnemyHealth enemy)
    {
        if (enemy == null)
            return;

        EnemyMovement movement = GetEnemyMovement(enemy);

        if (movement == null ||
            !enemyColliderCounts.Remove(movement))
        {
            return;
        }

        enemy.GetComponent<EnemyAnomalyEffects>()?.ExitZone(this);
    }

    public void ClearEffects()
    {
        if (effectsCleared)
            return;

        effectsCleared = true;

        foreach (EnemyMovement movement in enemyColliderCounts.Keys)
            RemoveZoneEffect(movement);

        enemyColliderCounts.Clear();

        if (playerColliderCount > 0)
            Controller?.NotifyLocalZoneExited(this);

        playerColliderCount = 0;

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

    private static EnemyMovement GetEnemyMovement(EnemyHealth enemy)
    {
        EnemyMovement movement = enemy.GetComponent<EnemyMovement>();

        if (movement == null)
            movement = enemy.GetComponentInParent<EnemyMovement>();

        if (movement == null)
            movement = enemy.GetComponentInChildren<EnemyMovement>();

        return movement;
    }

    private void ResolveVisual()
    {
        if (visualRoot == null)
        {
            Debug.LogWarning(
                "[BerserkZone] Serialized VisualRoot is missing.",
                this);
            return;
        }

        if (visualRenderer == null)
            visualRenderer = visualRoot.GetComponentInChildren<MeshRenderer>(true);

        if (visualRenderer == null)
        {
            Debug.LogWarning(
                "[BerserkZone] Serialized VisualRoot has no MeshRenderer.",
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
    public string VisualTypeName => "BERSERK";

    public AnomalyVisualTuningCapabilities VisualCapabilities =>
        AnomalyVisualTuningCapabilities.PrimaryColor |
        AnomalyVisualTuningCapabilities.FillColor |
        AnomalyVisualTuningCapabilities.FillAlpha |
        AnomalyVisualTuningCapabilities.BoundaryWidth |
        AnomalyVisualTuningCapabilities.BoundaryAlpha |
        AnomalyVisualTuningCapabilities.VisualScale |
        AnomalyVisualTuningCapabilities.PulseSpeed |
        AnomalyVisualTuningCapabilities.PulseStrength |
        AnomalyVisualTuningCapabilities.PatternSpeed |
        AnomalyVisualTuningCapabilities.PatternStrength;

    public AnomalyVisualTuningValues VisualValues => debugVisualValues;

    public void ApplyVisualValues(AnomalyVisualTuningValues values)
    {
        debugVisualValues = values;
        debugVisualValues.PrimaryColor = ClampColor(values.PrimaryColor);
        debugVisualValues.FillColor = ClampColor(values.FillColor);
        debugVisualValues.FillAlpha = Mathf.Clamp01(values.FillAlpha);
        debugVisualValues.BoundaryWidth = Mathf.Clamp(
            values.BoundaryWidth, 0.01f, 3f);
        debugVisualValues.BoundaryAlpha = Mathf.Clamp01(
            values.BoundaryAlpha);
        debugVisualValues.VisualScale = Mathf.Clamp(
            values.VisualScale, 0.25f, 3f);
        debugVisualValues.PulseSpeed = Mathf.Clamp(
            values.PulseSpeed, 0f, 10f);
        debugVisualValues.PulseStrength = Mathf.Clamp01(
            values.PulseStrength);
        debugVisualValues.PatternSpeed = Mathf.Clamp(
            values.PatternSpeed, 0f, 10f);
        debugVisualValues.PatternStrength = Mathf.Clamp01(
            values.PatternStrength);

        edgeWidth = debugVisualValues.BoundaryWidth;
        pulseSpeed = debugVisualValues.PulseSpeed;
        pulseStrength = debugVisualValues.PulseStrength;
        innerPatternSpeed = debugVisualValues.PatternSpeed;
        innerPatternIntensity = debugVisualValues.PatternStrength * 0.25f;
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
        Color fill = sourceMaterial != null &&
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
            FillColor = fill,
            FillAlpha = fill.a,
            BoundaryWidth = edgeWidth,
            BoundaryAlpha = 1f,
            VisualScale = 1f,
            PulseSpeed = pulseSpeed,
            PulseStrength = pulseStrength,
            PatternSpeed = innerPatternSpeed,
            PatternStrength = innerPatternIntensity / 0.25f
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
            Mathf.Clamp01(value.a));
    }
#endif

    private void ApplyVisualProperties()
    {
        if (visualRenderer == null || visualProperties == null)
            return;

        visualProperties.SetFloat(FadeId, visualFade);
        visualProperties.SetFloat(EdgeWidthId, edgeWidth);
        visualProperties.SetFloat(PulseSpeedId, pulseSpeed);
        visualProperties.SetFloat(PulseStrengthId, pulseStrength);
        visualProperties.SetFloat(PulseSharpnessId, pulseSharpness);
        visualProperties.SetVector(RegionSizeId, AreaSize);
        visualProperties.SetFloat(
            InnerPatternIntensityId,
            innerPatternIntensity
        );
        visualProperties.SetFloat(InnerPatternSpeedId, innerPatternSpeed);
        visualProperties.SetFloat(InnerPatternScaleId, innerPatternScale);
        visualProperties.SetFloat(
            WarningPulseFrequencyId,
            warningPulseFrequency
        );
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
                AreaSize * debugVisualValues.VisualScale);
        }
#endif
        visualProperties.SetFloat(VisualTimeId, Time.unscaledTime);
        visualRenderer.SetPropertyBlock(visualProperties);
    }

    private void OnDisable()
    {
        EnemyHealth.Despawned -= HandleEnemyDespawned;
        ClearEffects();
    }
}
