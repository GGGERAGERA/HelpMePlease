using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Opt in a sprite-only decoration to circular anomaly movement.
/// Place on the prop root, never on a room, event, or gameplay object.
/// Physics, animated and scripted props (including containers) are excluded.
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("World/Anomalies/Anomaly Movable Prop")]
public sealed class AnomalyMovableProp : MonoBehaviour
{
    private static readonly List<AnomalyMovableProp> activeProps = new(256);
    internal static IReadOnlyList<AnomalyMovableProp> ActiveProps => activeProps;
    private int lastMovedFrame = -1;

    private void OnEnable()
    {
        // Allocation is confined to registration, never the movement loop.
        if (IsSafeDecoration() && !activeProps.Contains(this)) activeProps.Add(this);
    }

    private bool IsSafeDecoration()
    {
        foreach (Component component in GetComponentsInChildren<Component>(true))
            if (!(component is Transform) && !(component is SpriteRenderer) && component != this)
                return false;

        for (Transform ancestor = transform.parent; ancestor != null; ancestor = ancestor.parent)
            if (ancestor.GetComponent<Collider2D>() != null || ancestor.GetComponent<Rigidbody2D>() != null ||
                ancestor.GetComponent<Interactable>() != null || ancestor.GetComponent<LocalAnomalyZone>() != null ||
                ancestor.GetComponent<GravityConstruct>() != null ||
                ancestor.GetComponent<WorldBreakable>() != null || ancestor.GetComponent<ResourceNode>() != null ||
                ancestor.GetComponent<Animator>() != null)
                return false;
        return true;
    }

    private void OnDisable() => activeProps.Remove(this);

    private void OnTransformParentChanged()
    {
        activeProps.Remove(this);
        if (isActiveAndEnabled) OnEnable();
    }

    internal void Orbit(Vector2 center, float cosine, float sine)
    {
        Vector2 position = transform.position;
        Vector2 offset = position - center;
        MoveBy(center + new Vector2(offset.x * cosine - offset.y * sine,
            offset.x * sine + offset.y * cosine) - position);
    }

    internal void MoveBy(Vector2 displacement)
    {
        if (lastMovedFrame == Time.frameCount) return;
        lastMovedFrame = Time.frameCount;
        transform.position += (Vector3)displacement;
    }
}
