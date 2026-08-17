using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(BoxCollider2D))]
public sealed class FootballPlayerBoundary : MonoBehaviour
{
    [Header("Production visual")]
    [SerializeField] private SpriteRenderer boundaryVisual;
    [SerializeField] private Color visualColor = new(0.25f, 0.9f, 1f, 0.9f);
    [SerializeField, Min(0.05f)] private float visualThickness = 0.12f;

    private BoxCollider2D boundary;

    public BoxCollider2D Collider => boundary != null ? boundary : GetComponent<BoxCollider2D>();
    public SpriteRenderer Visual => boundaryVisual;

    public void ConfigureVisual(SpriteRenderer visual)
    {
        boundaryVisual = visual;
        SynchronizeVisual();
    }

    public void Configure(Vector2 worldCenter, float worldWidth, float thickness)
    {
        boundary ??= GetComponent<BoxCollider2D>();
        transform.position = new Vector3(worldCenter.x, worldCenter.y, transform.position.z);
        Vector3 scale = transform.lossyScale;
        boundary.offset = Vector2.zero;
        boundary.size = new Vector2(
            worldWidth / Mathf.Max(0.0001f, Mathf.Abs(scale.x)),
            Mathf.Max(0.05f, thickness) / Mathf.Max(0.0001f, Mathf.Abs(scale.y)));
        boundary.isTrigger = false;
        SynchronizeVisual();
        if (Application.isPlaying)
            RefreshCollisionExceptions();
    }

    private void SynchronizeVisual()
    {
        if (boundaryVisual == null)
            return;

        boundaryVisual.color = visualColor;
        boundaryVisual.drawMode = SpriteDrawMode.Simple;
        Vector2 spriteSize = boundaryVisual.sprite != null
            ? boundaryVisual.sprite.bounds.size
            : Vector2.one;
        boundaryVisual.transform.localScale = new Vector3(
            (boundary != null ? boundary.size.x : 1f) / Mathf.Max(0.0001f, spriteSize.x),
            visualThickness / Mathf.Max(0.0001f, spriteSize.y),
            1f);
        boundaryVisual.sortingLayerName = "Midground";
        boundaryVisual.sortingOrder = 50;
    }

    public void RefreshCollisionExceptions()
    {
        boundary ??= GetComponent<BoxCollider2D>();
        Collider2D[] colliders = FindObjectsByType<Collider2D>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        foreach (Collider2D other in colliders)
        {
            if (other == null || other == boundary)
                continue;

            Physics2D.IgnoreCollision(boundary, other, !BelongsToPlayer(other));
        }
    }

    private static bool BelongsToPlayer(Collider2D other)
    {
        Transform current = other.transform;
        while (current != null)
        {
            if (current.CompareTag("Player"))
                return true;
            current = current.parent;
        }
        return false;
    }
}
