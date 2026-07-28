using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

[RequireComponent(typeof(CircleCollider2D))]
public sealed class StasisZone : MonoBehaviour
{
    private const int OutlineSegments = 64;

    private static readonly Dictionary<CharacterMovement2D, int>
        activeZoneCounts = new();

    [SerializeField] private Material lineMaterial;

    private CircleCollider2D zoneCollider;
    private LineRenderer fill;
    private LineRenderer outline;
    private LevelAnomalyController anomalyController;
    private CharacterMovement2D affectedMovement;
    private float speedMultiplier = 0.65f;
    private int playerColliderCount;
    private bool initialized;
    private bool effectCleared;

    private void Awake()
    {
        zoneCollider = GetComponent<CircleCollider2D>();
        zoneCollider.isTrigger = true;
        BuildVisual();
    }

    public void Initialize(
        float radius,
        float playerSpeedMultiplier,
        LevelAnomalyController controller)
    {
        float safeRadius = Mathf.Max(0.1f, radius);
        speedMultiplier = Mathf.Clamp(
            playerSpeedMultiplier,
            0.1f,
            1f
        );
        anomalyController = controller;
        zoneCollider.radius = safeRadius;
        ConfigureVisual(safeRadius);
        effectCleared = false;
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
        anomalyController?.NotifyLocalZoneEntered(
            this,
            LocalAnomalyType.Stasis
        );
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
        anomalyController?.NotifyLocalZoneExited(this);
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
            anomalyController?.NotifyLocalZoneExited(this);

        affectedMovement = null;
        playerColliderCount = 0;

        if (zoneCollider != null)
            zoneCollider.enabled = false;
    }

    private void BuildVisual()
    {
        fill = CreateLineRenderer(
            "StasisZoneFill",
            0,
            new Color(0.06f, 0.1f, 0.3f, 0.3f)
        );
        fill.positionCount = 2;
        fill.numCapVertices = 32;

        outline = CreateLineRenderer(
            "StasisZoneOutline",
            3,
            new Color(0.38f, 0.45f, 1f, 0.98f)
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
        ClearEffect();
    }
}
