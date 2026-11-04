using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Paints the graph's connections as worn tracks, in the same coordinates as the fog.</summary>
[RequireComponent(typeof(CanvasRenderer))]
internal sealed class SurfaceRouteRoads : MaskableGraphic
{
    internal enum State { Open, Travelled, Missed, Ahead, Selected }

    private readonly List<(Vector2[] points, State state)> tracks = new();
    private int selectedTrack = -1;

    internal void Clear()
    {
        tracks.Clear();
        selectedTrack = -1;
    }

    internal int Add(Vector2 from, Vector2 to, State state, List<(Vector2 from, Vector2 to)> survey = null)
    {
        float bend = to.y - from.y;
        float offset = (Mathf.RoundToInt(to.x / 24f) % 2 == 0 ? -1f : 1f) * 16f;
        var points = new[]
        {
            from,
            Pixel(Vector2.Lerp(from, to, .22f) + Vector2.up * (-bend * .16f + offset)),
            Pixel(Vector2.Lerp(from, to, .51f) + Vector2.up * offset),
            Pixel(Vector2.Lerp(from, to, .78f) + Vector2.up * (bend * .12f + offset)),
            to
        };
        tracks.Add((points, state));
        if (survey != null)
            for (int i = 1; i < points.Length; i++) survey.Add((points[i - 1], points[i]));
        return tracks.Count - 1;
    }

    internal void Select(int index)
    {
        selectedTrack = index;
        SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper mesh)
    {
        mesh.Clear();
        for (int i = 0; i < tracks.Count; i++)
        {
            if (i == selectedTrack) continue;
            var (points, state) = tracks[i];
            Paint(mesh, points, state);
        }
        if (selectedTrack >= 0) Paint(mesh, tracks[selectedTrack].points, State.Selected);
    }

    private static void Paint(VertexHelper mesh, Vector2[] points, State state)
    {
        bool highlighted = state == State.Selected;
        var shoulder = highlighted ? new Color32(131, 75, 35, 255) : new Color32(28, 32, 29, 255);
        var soil = state switch
        {
            State.Selected => new Color32(255, 180, 69, 255),
            State.Open => new Color32(114, 168, 133, 255),
            State.Travelled => new Color32(61, 121, 94, 255),
            State.Ahead => new Color32(147, 138, 101, 255),
            _ => new Color32(72, 78, 69, 255)
        };
        var gravel = Color.Lerp(soil, new Color32(249, 232, 176, 255), highlighted ? .62f : .26f);
        for (int i = 1; i < points.Length; i++) Segment(mesh, points[i - 1], points[i], highlighted ? 30f : 21f, shoulder);
        for (int i = 1; i < points.Length; i++)
        {
            var a = points[i - 1];
            var b = points[i];
            Segment(mesh, a, b, highlighted ? 16f : 12f, soil);
            Vector2 tangent = (b - a).normalized;
            Vector2 normal = new(-tangent.y, tangent.x);
            float distance = Vector2.Distance(a, b);
            for (int step = 10; step < distance - 4f; step += 15)
            {
                float side = (step / 15 + i) % 2 == 0 ? 3f : -3f;
                var stone = Pixel(a + tangent * step + normal * side);
                Segment(mesh, stone, stone + tangent * 5f, 2f, gravel);
            }
        }
    }

    private static Vector2 Pixel(Vector2 point) => new(Mathf.Round(point.x / 4f) * 4f, Mathf.Round(point.y / 4f) * 4f);

    private static void Segment(VertexHelper mesh, Vector2 from, Vector2 to, float width, Color color)
    {
        Vector2 tangent = (to - from).normalized;
        Vector2 normal = new Vector2(-tangent.y, tangent.x) * (width * .5f);
        // Small overlaps at bends leave a continuous shoulder under the gravel.
        from -= tangent * width * .2f;
        to += tangent * width * .2f;
        int vertex = mesh.currentVertCount;
        mesh.AddVert(from - normal, color, Vector2.zero);
        mesh.AddVert(from + normal, color, Vector2.zero);
        mesh.AddVert(to + normal, color, Vector2.zero);
        mesh.AddVert(to - normal, color, Vector2.zero);
        mesh.AddTriangle(vertex, vertex + 1, vertex + 2);
        mesh.AddTriangle(vertex, vertex + 2, vertex + 3);
    }
}
