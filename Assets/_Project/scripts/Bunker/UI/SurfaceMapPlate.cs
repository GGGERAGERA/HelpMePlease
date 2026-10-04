using UnityEngine;
using UnityEngine.UI;

/// <summary>Small pixel-stepped silhouette used only by the expedition terminal UI.</summary>
[RequireComponent(typeof(CanvasRenderer))]
public sealed class SurfaceMapPlate : MaskableGraphic
{
    public float Cut = 8f;
    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear(); var r = rectTransform.rect;
        float c = Mathf.Min(Cut, Mathf.Min(r.width, r.height) * .25f);
        var p = new[] {
            new Vector2(r.xMin+c,r.yMin), new Vector2(r.xMax-c,r.yMin),
            new Vector2(r.xMax-c,r.yMin+c*.5f), new Vector2(r.xMax,r.yMin+c*.5f),
            new Vector2(r.xMax,r.yMax-c), new Vector2(r.xMax-c*.5f,r.yMax-c),
            new Vector2(r.xMax-c*.5f,r.yMax), new Vector2(r.xMin+c,r.yMax),
            new Vector2(r.xMin+c,r.yMax-c*.5f), new Vector2(r.xMin,r.yMax-c*.5f),
            new Vector2(r.xMin,r.yMin+c), new Vector2(r.xMin+c*.5f,r.yMin+c),
            new Vector2(r.xMin+c*.5f,r.yMin)
        };
        vh.AddVert(r.center, color, Vector2.zero);
        foreach (var v in p) vh.AddVert(v, color, Vector2.zero);
        for (int i=0;i<p.Length;i++) vh.AddTriangle(0, i+1, (i+1)%p.Length+1);
    }
}

