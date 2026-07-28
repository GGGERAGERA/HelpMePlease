using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

[RequireComponent(typeof(CircleCollider2D))]
public sealed class BerserkZone : MonoBehaviour
{
    private const int OutlineSegments = 64;

    private static readonly Dictionary<EnemyMovement, int>
        activeZoneCounts = new();

    [SerializeField] private Material lineMaterial;

    private readonly Dictionary<EnemyMovement, int>
        enemyColliderCounts = new();

    private CircleCollider2D zoneCollider;
    private LineRenderer fill;
    private LineRenderer outline;
    private LevelAnomalyController anomalyController;
    private float speedMultiplier = 1.5f;
    private int playerColliderCount;
    private bool initialized;
    private bool effectsCleared;

    private void Awake()
    {
        zoneCollider = GetComponent<CircleCollider2D>();
        zoneCollider.isTrigger = true;
        BuildVisual();
    }

    private void OnEnable()
    {
        EnemyHealth.Despawned += HandleEnemyDespawned;
    }

    public void Initialize(
        float radius,
        float enemySpeedMultiplier,
        LevelAnomalyController controller)
    {
        float safeRadius = Mathf.Max(0.1f, radius);
        speedMultiplier = Mathf.Max(1f, enemySpeedMultiplier);
        anomalyController = controller;
        zoneCollider.radius = safeRadius;
        ConfigureVisual(safeRadius);
        effectsCleared = false;
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
                anomalyController?.NotifyLocalZoneEntered(
                    this,
                    LocalAnomalyType.Berserk
                );

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
                anomalyController?.NotifyLocalZoneExited(this);

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
            anomalyController?.NotifyLocalZoneExited(this);

        playerColliderCount = 0;

        if (zoneCollider != null)
            zoneCollider.enabled = false;
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
        fill = CreateLineRenderer(
            "BerserkZoneFill",
            0,
            new Color(0.22f, 0.015f, 0.02f, 0.3f)
        );
        fill.positionCount = 2;
        fill.numCapVertices = 32;

        outline = CreateLineRenderer(
            "BerserkZoneOutline",
            3,
            new Color(1f, 0.08f, 0.1f, 0.98f)
        );
        outline.loop = true;
        outline.positionCount = OutlineSegments;
        outline.startWidth = 0.18f;
        outline.endWidth = 0.18f;
    }

    private void ConfigureVisual(float radius)
    {
        fill.startWidth = radius * 2f;
        fill.endWidth = radius * 2f;
        fill.SetPosition(0, new Vector3(-0.001f, 0f, 0f));
        fill.SetPosition(1, new Vector3(0.001f, 0f, 0f));

        for (int i = 0; i < OutlineSegments; i++)
        {
            float angle = i * Mathf.PI * 2f / OutlineSegments;
            outline.SetPosition(
                i,
                new Vector3(
                    Mathf.Cos(angle) * radius,
                    Mathf.Sin(angle) * radius,
                    0f
                )
            );
        }
    }

    private LineRenderer CreateLineRenderer(
        string objectName,
        int sortingOrder,
        Color color)
    {
        GameObject lineObject = new GameObject(objectName);
        lineObject.transform.SetParent(transform, false);

        LineRenderer line = lineObject.AddComponent<LineRenderer>();
        line.sharedMaterial = lineMaterial;
        line.useWorldSpace = false;
        line.alignment = LineAlignment.View;
        line.textureMode = LineTextureMode.Stretch;
        line.numCornerVertices = 4;
        line.shadowCastingMode = ShadowCastingMode.Off;
        line.receiveShadows = false;
        line.sortingLayerName = "Midground";
        line.sortingOrder = sortingOrder;
        line.startColor = color;
        line.endColor = color;
        return line;
    }

    private void OnDisable()
    {
        EnemyHealth.Despawned -= HandleEnemyDespawned;
        ClearEffects();
    }
}
