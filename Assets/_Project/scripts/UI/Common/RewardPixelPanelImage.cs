using UnityEngine;
using UnityEngine.UI;

// Presentation-only replacement for the old neon panel sprites. Keeps Image references,
// RectTransforms and raycast bounds intact; Button tint drives the outline state.
public sealed class RewardPixelPanelImage : Image
{
    [SerializeField] private bool outlineOnly;
    [SerializeField] private Color edgeColor = new(0.29f, 0.33f, 0.32f, 1f);
    [SerializeField] private float corner = 8f;
    [SerializeField] private float stroke = 2f;
    [SerializeField] private bool details = true;

    protected override void OnPopulateMesh(VertexHelper mesh)
    {
        mesh.Clear();
        Rect r = GetPixelAdjustedRect();
        float c = Mathf.Min(corner, Mathf.Min(r.width, r.height) / 4f);
        float s = Mathf.Min(stroke, c / 2f);
        if (!outlineOnly)
        {
            Quad(mesh, new Rect(r.xMin + c, r.yMin, r.width - c * 2, c / 2), color);
            Quad(mesh, new Rect(r.xMin + c / 2, r.yMin + c / 2, r.width - c, c / 2), color);
            Quad(mesh, new Rect(r.xMin, r.yMin + c, r.width, r.height - c * 2), color);
            Quad(mesh, new Rect(r.xMin + c / 2, r.yMax - c, r.width - c, c / 2), color);
            Quad(mesh, new Rect(r.xMin + c, r.yMax - c / 2, r.width - c * 2, c / 2), color);
        }
        Color edge = outlineOnly ? color : edgeColor;
        Quad(mesh, new Rect(r.xMin + c, r.yMin, r.width - c * 2, s), edge);
        Quad(mesh, new Rect(r.xMin + c, r.yMax - s, r.width - c * 2, s), edge);
        Quad(mesh, new Rect(r.xMin, r.yMin + c, s, r.height - c * 2), edge);
        Quad(mesh, new Rect(r.xMax - s, r.yMin + c, s, r.height - c * 2), edge);
        for (int x = 0; x < 2; x++)
            for (int y = 0; y < 2; y++)
            {
                float px = x == 0 ? r.xMin : r.xMax;
                float py = y == 0 ? r.yMin : r.yMax;
                CornerQuad(mesh, px, py, x, y, c / 2, c / 2, c / 2 + s, s, edge);
                CornerQuad(mesh, px, py, x, y, c / 2, c / 2, s, c / 2 + s, edge);
                CornerQuad(mesh, px, py, x, y, c - s, 0, s, c / 2, edge);
                CornerQuad(mesh, px, py, x, y, 0, c - s, c / 2, s, edge);
            }
        if (!details) return;
        // Small mechanical fasteners, no text or icon overlays.
        Color muted = edge; muted.a *= .5f;
        for (int i = 0; i < 3; i++)
            Quad(mesh, new Rect(r.center.x - 12 + i * 9, r.yMin + 7, 5, 2), muted);
        Quad(mesh, new Rect(r.xMin + c + 5, r.yMax - 8, 12, 2), muted);
        Quad(mesh, new Rect(r.xMax - c - 17, r.yMax - 8, 12, 2), muted);
    }

    private static void CornerQuad(VertexHelper mesh, float x, float y, int right, int top,
        float dx, float dy, float w, float h, Color tint)
        => Quad(mesh, new Rect(right == 0 ? x + dx : x - dx - w,
            top == 0 ? y + dy : y - dy - h, w, h), tint);

    private static void Quad(VertexHelper mesh, Rect r, Color tint)
    {
        int first = mesh.currentVertCount;
        mesh.AddVert(new Vector3(r.xMin, r.yMin), tint, Vector2.zero);
        mesh.AddVert(new Vector3(r.xMin, r.yMax), tint, Vector2.zero);
        mesh.AddVert(new Vector3(r.xMax, r.yMax), tint, Vector2.zero);
        mesh.AddVert(new Vector3(r.xMax, r.yMin), tint, Vector2.zero);
        mesh.AddTriangle(first, first + 1, first + 2);
        mesh.AddTriangle(first + 2, first + 3, first);
    }
}
