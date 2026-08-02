using System.Collections.Generic;
using UnityEngine;

public sealed class StasisZone : LocalAnomalyZone
{
    private static readonly int FadeId = Shader.PropertyToID("_Fade");
    private static readonly int EdgeWidthId =
        Shader.PropertyToID("_EdgeWidth");
    private static readonly int PulseSpeedId =
        Shader.PropertyToID("_PulseSpeed");
    private static readonly int VisualTimeId =
        Shader.PropertyToID("_VisualTime");

    private static readonly Dictionary<CharacterMovement2D, int>
        activeZoneCounts = new();

    [Header("Visual")]
    [SerializeField] private Material visualMaterial;
    [SerializeField, Min(1f)] private float visualRadiusMultiplier = 1.08f;
    [SerializeField, Range(0.01f, 0.4f)] private float edgeWidth = 0.14f;
    [SerializeField, Min(0f)] private float pulseSpeed = 0.28f;
    [SerializeField, Range(0.6f, 1f)] private float fadeDuration = 0.8f;

    private CircleCollider2D zoneCollider;
    private MeshRenderer visualRenderer;
    private MaterialPropertyBlock visualProperties;
    private CharacterMovement2D affectedMovement;
    private float speedMultiplier = 0.65f;
    private float visualFade;
    private float targetVisualFade;
    private int playerColliderCount;
    private bool initialized;
    private bool effectCleared;
    private bool despawning;

    private void Awake()
    {
        zoneCollider = GetComponent<CircleCollider2D>();
        zoneCollider.isTrigger = true;
        BuildVisual();
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

    protected override void InitializeFromData(LocalAnomalyData data)
    {
        float safeRadius = data.ZoneRadius;
        speedMultiplier = data.PlayerSpeedMultiplier;
        zoneCollider.radius = safeRadius;
        ConfigureVisual(safeRadius);
        effectCleared = false;
        despawning = false;
        visualFade = 0f;
        targetVisualFade = 1f;
        ApplyVisualProperties();
        initialized = true;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!initialized || effectCleared)
            return;

        CharacterMovement2D movement =
            other.GetComponentInParent<CharacterMovement2D>();

        if (movement == null)
            return;

        if (affectedMovement != null &&
            affectedMovement != movement)
        {
            return;
        }

        affectedMovement = movement;
        playerColliderCount++;

        if (playerColliderCount > 1)
            return;

        ApplyEffect(movement);
        Controller?.NotifyLocalZoneEntered(this, Data);
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        CharacterMovement2D movement =
            other.GetComponentInParent<CharacterMovement2D>();

        if (movement == null || movement != affectedMovement)
            return;

        playerColliderCount = Mathf.Max(
            0,
            playerColliderCount - 1
        );

        if (playerColliderCount > 0)
            return;

        RemoveEffect(movement);
        affectedMovement = null;
        Controller?.NotifyLocalZoneExited(this);
    }

    private void ApplyEffect(CharacterMovement2D movement)
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

    private static void RemoveEffect(CharacterMovement2D movement)
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

    public void ClearEffect()
    {
        if (effectCleared)
            return;

        effectCleared = true;

        if (!ReferenceEquals(affectedMovement, null) &&
            playerColliderCount > 0)
        {
            RemoveEffect(affectedMovement);
        }

        if (playerColliderCount > 0)
            Controller?.NotifyLocalZoneExited(this);

        affectedMovement = null;
        playerColliderCount = 0;

        if (zoneCollider != null)
            zoneCollider.enabled = false;
    }

    public override void Despawn()
    {
        if (despawning)
            return;

        ClearEffect();
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
                "[StasisZone] Built-in Quad mesh is unavailable.",
                this
            );
            return;
        }

        GameObject visualObject = new("StasisZoneVisual");
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

    private void ConfigureVisual(float radius)
    {
        if (visualRenderer == null)
            return;

        float diameter =
            radius * 2f * Mathf.Max(1f, visualRadiusMultiplier);
        visualRenderer.transform.localScale =
            new Vector3(diameter, diameter, 1f);
    }

    private void ApplyVisualProperties()
    {
        if (visualRenderer == null || visualProperties == null)
            return;

        visualProperties.SetFloat(FadeId, visualFade);
        visualProperties.SetFloat(EdgeWidthId, edgeWidth);
        visualProperties.SetFloat(PulseSpeedId, pulseSpeed);
        visualProperties.SetFloat(VisualTimeId, Time.unscaledTime);
        visualRenderer.SetPropertyBlock(visualProperties);
    }

    private void OnDisable()
    {
        ClearEffect();
    }
}
