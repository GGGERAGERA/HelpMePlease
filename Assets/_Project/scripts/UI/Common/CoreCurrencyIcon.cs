using UnityEngine;
using UnityEngine.UI;

/// <summary>A small pixel-cut core, rendered as UI geometry with no texture filtering.</summary>
[RequireComponent(typeof(CanvasRenderer))]
public sealed class CoreCurrencyIcon : MaskableGraphic
{
    private static readonly string[] Pixels =
    {
        "0001111000", "0012222100", "0123333210", "1232112321", "1231441321",
        "1231441321", "1232112321", "0123333210", "0012222100", "0001111000"
    };
    protected override void OnPopulateMesh(VertexHelper helper)
    {
        helper.Clear();
        Rect rect = GetPixelAdjustedRect();
        float unit = Mathf.Min(rect.width, rect.height) / 10f;
        Vector2 origin = rect.center - Vector2.one * unit * 5f;
        Color[] palette = { Color.clear, new(.12f,.36f,.36f), new(.18f,.62f,.59f), new(.43f,.88f,.8f), new(.75f,1f,.91f) };
        for (int y = 0; y < 10; y++)
            for (int x = 0; x < 10; x++)
            {
                int index = Pixels[9-y][x] - '0';
                if (index == 0) continue;
                var tint = palette[index] * color;
                Vector2 p = origin + new Vector2(x,y) * unit;
                int start = helper.currentVertCount;
                helper.AddVert(new Vector3(p.x,p.y),tint,Vector2.zero);
                helper.AddVert(new Vector3(p.x,p.y+unit),tint,Vector2.zero);
                helper.AddVert(new Vector3(p.x+unit,p.y+unit),tint,Vector2.zero);
                helper.AddVert(new Vector3(p.x+unit,p.y),tint,Vector2.zero);
                helper.AddTriangle(start,start+1,start+2); helper.AddTriangle(start,start+2,start+3);
            }
    }
}
