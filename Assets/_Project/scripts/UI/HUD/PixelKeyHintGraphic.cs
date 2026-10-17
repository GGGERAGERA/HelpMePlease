using UnityEngine;
using UnityEngine.UI;

// A keyboard spacebar pictogram, using the same pixel grid as the orbit hints.
public sealed class PixelKeyHintGraphic : MaskableGraphic
{
    private static readonly string[] Space =
    {
        ".OOOOOOOOOOOOOOOOOOOOOO.", "OddddddddddddddddddddddO",
        "OddddddddddddddddddddddO", "Oddd#dddddddddddddd#dddO",
        "Oddd#dddddddddddddd#dddO", "Oddd################dddO",
        "OddddddddddddddddddddddO", "OddddddddddddddddddddddO",
        ".OOOOOOOOOOOOOOOOOOOOOO."
    };

    protected override void OnPopulateMesh(VertexHelper mesh)
    {
        mesh.Clear();
        Rect rect = GetPixelAdjustedRect();
        float pixel = Mathf.Floor(Mathf.Min(rect.width / 24f, rect.height / 9f));
        if (pixel < 1f) return;
        var origin = new Vector2(rect.center.x - 12f * pixel, rect.center.y + 4.5f * pixel);
        HudPixelGlyph.Draw(mesh, Space, 0, 0, origin, pixel, new Color(.18f, .8f, .88f), Color.white, color);
    }
}

internal static class HudPixelGlyph
{
    internal static void Draw(VertexHelper mesh, string[] rows, int xOffset, int yOffset,
        Vector2 origin, float pixel, Color inkColor, Color accentColor, Color tint)
    {
        for (int y = 0; y < rows.Length; y++)
        for (int x = 0; x < rows[y].Length;)
        {
            char ink = rows[y][x];
            int start = x++;
            while (x < rows[y].Length && rows[y][x] == ink) x++;
            if (ink == '.') continue;
            Color shade = ink switch
            {
                'R' => accentColor,
                'O' => new Color(.57f, .72f, .76f, 1f),
                'W' => new Color(.78f, .9f, .94f, 1f),
                'd' => new Color(.035f, .075f, .09f, 1f),
                _ => inkColor
            };
            float left = origin.x + (xOffset + start) * pixel;
            float top = origin.y - (yOffset + y) * pixel;
            int index = mesh.currentVertCount;
            var vertex = UIVertex.simpleVert;
            vertex.color = shade * tint;
            vertex.position = new Vector3(left, top - pixel); mesh.AddVert(vertex);
            vertex.position = new Vector3(left, top); mesh.AddVert(vertex);
            vertex.position = new Vector3(left + (x - start) * pixel, top); mesh.AddVert(vertex);
            vertex.position = new Vector3(left + (x - start) * pixel, top - pixel); mesh.AddVert(vertex);
            mesh.AddTriangle(index, index + 1, index + 2);
            mesh.AddTriangle(index + 2, index + 3, index);
        }
    }
}
