using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(CanvasRenderer))]
public sealed class TutorialOverlay : MaskableGraphic
{
    private readonly List<Rect> holes = new();
    private readonly List<float> xs = new(), ys = new();
    private readonly Vector3[] quad = new Vector3[4];
    private Vector2 arrowTip, arrowDirection;
    private float scale = 1f, dimAlpha, arrowLength;
    private bool visible;
    private static readonly Color Highlight = new(.4f, 1f, .86f, 1f);
    public void SetState(bool visible, List<Rect> focusHoles, Vector2 tip, Vector2 direction, float scale, float dimAlpha, float arrowLength)
    {
        this.visible = visible; holes.Clear(); holes.AddRange(focusHoles);
        arrowTip = tip; arrowDirection = direction; this.scale = scale; this.dimAlpha = dimAlpha; this.arrowLength = arrowLength;
        SetVerticesDirty();
    }
    protected override void OnPopulateMesh(VertexHelper mesh)
    {
        mesh.Clear();
        if (!visible) return;
        xs.Clear(); ys.Clear();
        xs.Add(0); xs.Add(Screen.width); ys.Add(0); ys.Add(Screen.height);
        foreach (var hole in holes) { xs.Add(hole.xMin); xs.Add(hole.xMax); ys.Add(hole.yMin); ys.Add(hole.yMax); }
        xs.Sort(); ys.Sort();
        var dim = new Color(0.015f, 0.025f, 0.035f, dimAlpha);
        for (int x = 1; x < xs.Count; x++)
            for (int y = 1; y < ys.Count; y++)
            {
                var rect = Rect.MinMaxRect(xs[x - 1], ys[y - 1], xs[x], ys[y]);
                if (rect.width <= 0 || rect.height <= 0) continue;
                bool cutout = false;
                foreach (var hole in holes) if (hole.Contains(rect.center)) { cutout = true; break; }
                if (!cutout) Rectangle(mesh, rect, dim);
            }
        float border = 3f * scale;
        foreach (var hole in holes)
        {
            Rectangle(mesh, new Rect(hole.xMin, hole.yMin, hole.width, border), Highlight);
            Rectangle(mesh, new Rect(hole.xMin, hole.yMax - border, hole.width, border), Highlight);
            Rectangle(mesh, new Rect(hole.xMin, hole.yMin, border, hole.height), Highlight);
            Rectangle(mesh, new Rect(hole.xMax - border, hole.yMin, border, hole.height), Highlight);
        }
        Rectangle(mesh, new Rect(16f * scale, 18f * scale, Screen.width - 32f * scale, 98f * scale), new Color(0.02f, 0.04f, 0.05f, 0.94f));
        if (arrowDirection.sqrMagnitude > 0f)
        {
            float length = arrowLength;
            Vector2 back = arrowTip - arrowDirection * length;
            Vector2 perpendicular = new(-arrowDirection.y, arrowDirection.x);
            Line(mesh, back, arrowTip, 5f * scale);
            Line(mesh, arrowTip - arrowDirection * 18f * scale + perpendicular * 13f * scale, arrowTip, 5f * scale);
            Line(mesh, arrowTip - arrowDirection * 18f * scale - perpendicular * 13f * scale, arrowTip, 5f * scale);
        }
    }

    private void Rectangle(VertexHelper mesh, Rect rect, Color tint)
    {
        quad[0] = new Vector2(rect.xMin, rect.yMin); quad[1] = new Vector2(rect.xMin, rect.yMax);
        quad[2] = new Vector2(rect.xMax, rect.yMax); quad[3] = new Vector2(rect.xMax, rect.yMin);
        AddQuad(mesh, tint);
    }
    private void Line(VertexHelper mesh, Vector2 from, Vector2 to, float width)
    {
        Vector2 direction = (to - from).normalized;
        Vector2 side = new Vector2(-direction.y, direction.x) * width * 0.5f;
        quad[0] = from - side; quad[1] = from + side; quad[2] = to + side; quad[3] = to - side;
        AddQuad(mesh, Highlight);
    }
    private void AddQuad(VertexHelper mesh, Color tint)
    {
        int start = mesh.currentVertCount;
        Vector2 offset = rectTransform.rect.min;
        for (int i = 0; i < 4; i++) mesh.AddVert((Vector2)quad[i] + offset, tint, Vector2.zero);
        mesh.AddTriangle(start, start + 1, start + 2); mesh.AddTriangle(start, start + 2, start + 3);
    }
}
