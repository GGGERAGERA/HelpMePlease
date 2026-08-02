using System.Collections.Generic;
using UnityEngine;

public sealed class BerserkZone : LocalAnomalyZone
{
    private static readonly int FadeId = Shader.PropertyToID("_Fade");
    private static readonly int EdgeWidthId =
        Shader.PropertyToID("_EdgeWidth");
    private static readonly int PulseSpeedId =
        Shader.PropertyToID("_PulseSpeed");
    private static readonly int PulseStrengthId =
        Shader.PropertyToID("_PulseStrength");
    private static readonly int DistortionStrengthId =
        Shader.PropertyToID("_DistortionStrength");
    private static readonly int VisualTimeId =
        Shader.PropertyToID("_VisualTime");

    private static readonly Dictionary<EnemyMovement, int>
        activeZoneCounts = new();

    [Header("Visual")]
    [SerializeField] private Material visualMaterial;
    [SerializeField, Range(0.01f, 0.4f)] private float edgeWidth = 0.12f;
    [SerializeField, Min(0f)] private float pulseSpeed = 0.45f;
    [SerializeField, Range(0f, 1f)] private float pulseStrength = 0.12f;
    [SerializeField, Range(0f, 0.25f)]
    private float distortionStrength = 0.008f;
    [SerializeField, Range(0.6f, 1f)] private float fadeDuration = 0.8f;

    private readonly Dictionary<EnemyMovement, int>
        enemyColliderCounts = new();

    private MeshRenderer visualRenderer;
    private MaterialPropertyBlock visualProperties;
    private float speedMultiplier = 1.5f;
    private float visualFade;
    private float targetVisualFade;
    private int playerColliderCount;
    private bool initialized;
    private bool effectsCleared;
    private bool despawning;

    private void Awake()
    {
        BuildVisual();
    }

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

        if (activeZoneCounts.TryGetValue(movement, out int zoneCount))
        {
            activeZoneCounts[movement] = zoneCount + 1;
            return;
        }

        activeZoneCounts[movement] = 1;
        movement.SetAnomalySpeedMultiplier(speedMultiplier);
    }

    private static void RemoveZoneEffect(EnemyMovement movement)
    {
        if (ReferenceEquals(movement, null) ||
            !activeZoneCounts.TryGetValue(
                movement,
                out int zoneCount))
        {
            return;
        }

        zoneCount--;

        if (zoneCount > 0)
        {
            activeZoneCounts[movement] = zoneCount;
            return;
        }

        activeZoneCounts.Remove(movement);

        if (movement != null)
            movement.SetAnomalySpeedMultiplier(1f);
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

        RemoveZoneEffect(movement);
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

    private void BuildVisual()
    {
        if (visualMaterial == null)
            return;

        Mesh quad = Resources.GetBuiltinResource<Mesh>("Quad.fbx");

        if (quad == null)
        {
            Debug.LogWarning(
                "[BerserkZone] Built-in Quad mesh is unavailable.",
                this
            );
            return;
        }

        GameObject visualObject = new("BerserkZoneVisual");
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
            new Vector3(areaSize.x / 0.9f, areaSize.y / 0.9f, 1f);
    }

    private void ApplyVisualProperties()
    {
        if (visualRenderer == null || visualProperties == null)
            return;

        visualProperties.SetFloat(FadeId, visualFade);
        visualProperties.SetFloat(EdgeWidthId, edgeWidth);
        visualProperties.SetFloat(PulseSpeedId, pulseSpeed);
        visualProperties.SetFloat(PulseStrengthId, pulseStrength);
        visualProperties.SetFloat(
            DistortionStrengthId,
            distortionStrength
        );
        visualProperties.SetFloat(VisualTimeId, Time.unscaledTime);
        visualRenderer.SetPropertyBlock(visualProperties);
    }

    private void OnDisable()
    {
        EnemyHealth.Despawned -= HandleEnemyDespawned;
        ClearEffects();
    }
}
