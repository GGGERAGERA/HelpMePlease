using UnityEngine;
using UnityEngine.UI;

/// <summary>Outline glyphs on the combat HUD's 16-pixel icon grid.</summary>
[RequireComponent(typeof(CanvasRenderer))]
internal sealed class CombatHudIcon : MaskableGraphic
{
    internal enum Kind { Heart, Bolt, Coins, Core, Signal }
    [SerializeField] private Kind kind;

    private static readonly float[][][] Paths =
    {
        new[] { new[] { 8f, 14, 5, 12, 2, 9, 1.5f, 6, 2.5f, 3.5f, 5.5f, 2.5f, 8, 4,
            10.5f, 2.5f, 13.5f, 3.5f, 14.5f, 6, 14, 9, 11, 12, 8, 14 } },
        new[] { new[] { 9f, 1, 2, 9, 7, 9, 6, 15, 14, 6.5f, 9, 6.5f, 9, 1 } },
        new[]
        {
            new[] { 6f, 1.5f, 9, 3, 10, 6, 8.5f, 9, 6, 10, 3, 9, 1.5f, 6, 2.5f, 3, 6, 1.5f },
            new[] { 11.5f, 6, 14, 7.5f, 15, 11, 13.5f, 14, 10.5f, 15, 8, 13.5f, 7, 12 },
            new[] { 6f, 4, 6, 7.5f },
            new[] { 11.5f, 9, 10, 12.5f }
        },
        new[]
        {
            new[] { 1f, 6, 4.5f, 2, 11.5f, 2, 15, 6, 8, 14.5f, 1, 6, 15, 6 },
            new[] { 6.5f, 2, 5.5f, 6, 8, 14.5f, 10.5f, 6, 9.5f, 2 }
        },
        new[]
        {
            new[] { 5f, 15, 8, 7, 11, 15 },
            new[] { 6f, 12, 10, 12 },
            new[] { 4f, 4, 2.5f, 6, 2.5f, 8, 4, 10 },
            new[] { 12f, 4, 13.5f, 6, 13.5f, 8, 12, 10 },
            new[] { 2f, 2, .75f, 4, .75f, 9, 2, 11 },
            new[] { 14f, 2, 15.25f, 4, 15.25f, 9, 14, 11 }
        }
    };

    protected override void OnPopulateMesh(VertexHelper mesh)
    {
        mesh.Clear();
        var bounds = GetPixelAdjustedRect();
        float unit = Mathf.Min(bounds.width, bounds.height) / 16f;
        Vector2 origin = bounds.center + new Vector2(-8f, 8f) * unit;
        foreach (var path in Paths[(int)kind])
        for (int i = 2; i < path.Length; i += 2)
        {
            Vector2 start = origin + new Vector2(path[i - 2], -path[i - 1]) * unit;
            Vector2 end = origin + new Vector2(path[i], -path[i + 1]) * unit;
            Vector2 tangent = (end - start).normalized * (.65f * unit);
            Vector2 side = new(-tangent.y, tangent.x);
            start -= tangent * .45f;
            end += tangent * .45f;
            int at = mesh.currentVertCount;
            mesh.AddVert(start - side, color, Vector2.zero);
            mesh.AddVert(start + side, color, Vector2.zero);
            mesh.AddVert(end + side, color, Vector2.zero);
            mesh.AddVert(end - side, color, Vector2.zero);
            mesh.AddTriangle(at, at + 1, at + 2);
            mesh.AddTriangle(at, at + 2, at + 3);
        }
    }
}
