using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Viewport-wide pixel fog. Receives already-discovered points only; owns no exploration or save state.</summary>
[RequireComponent(typeof(CanvasRenderer))]
public sealed class SurfaceMapFog : MaskableGraphic
{
    private RectTransform graph;
    private readonly List<Vector2> revealed = new();
    private readonly List<(Vector2 from,Vector2 to)> roads = new();
    private Matrix4x4 previousMatrix;
    private Rect previousRect;
    public void Configure(RectTransform content) { graph=content; raycastTarget=false; SetVerticesDirty(); }
    public void Reveal(IReadOnlyList<Vector2> points,IReadOnlyList<(Vector2 from,Vector2 to)> connections)
    {
        revealed.Clear(); roads.Clear();
        for(int i=0;i<points.Count;i++)revealed.Add(points[i]);
        for(int i=0;i<connections.Count;i++)roads.Add(connections[i]);
        SetVerticesDirty();
    }
    private void LateUpdate()
    {
        if(graph==null)return;
        var matrix=graph.worldToLocalMatrix*transform.localToWorldMatrix;
        if(matrix==previousMatrix && rectTransform.rect==previousRect)return;
        previousMatrix=matrix; previousRect=rectTransform.rect; SetVerticesDirty();
    }
    // Zero in surveyed areas, one in unknown territory. This never uses locked sector coordinates.
    public float CoverageAt(Vector2 point)
    {
        float distance=float.PositiveInfinity;
        foreach(var center in revealed)
            distance=Mathf.Min(distance,new Vector2((point.x-center.x)/154f,(point.y-center.y)/170f).magnitude);
        foreach(var road in roads)
        {
            var delta=road.to-road.from;
            float along=delta.sqrMagnitude>0?Mathf.Clamp01(Vector2.Dot(point-road.from,delta)/delta.sqrMagnitude):0;
            distance=Mathf.Min(distance,Vector2.Distance(point,road.from+along*delta)/62f);
        }
        return Mathf.Clamp01((distance-.92f)/.22f);
    }
    protected override void OnPopulateMesh(VertexHelper mesh)
    {
        mesh.Clear(); if(graph==null)return;
        var rect=GetPixelAdjustedRect(); var matrix=graph.worldToLocalMatrix*transform.localToWorldMatrix;
        // Keep the mesh below the uGUI vertex limit even on large/ultrawide canvases.
        float cell=Mathf.Max(12,Mathf.Ceil(Mathf.Sqrt(rect.width*rect.height/14000f)));
        for(float y=rect.yMin;y<rect.yMax;y+=cell) for(float x=rect.xMin;x<rect.xMax;x+=cell)
        {
            Vector2 point=matrix.MultiplyPoint3x4(new Vector3(x+cell*.5f,y+cell*.5f));
            float coverage=CoverageAt(point);
            if(coverage<=0)continue;
            // Four deliberately stepped bands: a quiet ash edge, then opaque blue charcoal banks.
            int band=Mathf.Clamp(Mathf.CeilToInt(coverage*4),1,4);
            float cloud=Mathf.PerlinNoise((point.x+4000)*.008f,(point.y+4000)*.008f);
            byte tone=(byte)(cloud<.37f?0:cloud>.64f?8:4);
            Color32 ink=band==1?new Color32(72,83,82,88):band==2?new Color32(49,64,68,175):
                band==3?new Color32(31,44,51,235):new Color32((byte)(20+tone),(byte)(29+tone),(byte)(36+tone),255);
            int start=mesh.currentVertCount; float right=Mathf.Min(x+cell,rect.xMax),top=Mathf.Min(y+cell,rect.yMax);
            mesh.AddVert(new Vector3(x,y),ink,Vector2.zero); mesh.AddVert(new Vector3(x,top),ink,Vector2.zero);
            mesh.AddVert(new Vector3(right,top),ink,Vector2.zero); mesh.AddVert(new Vector3(right,y),ink,Vector2.zero);
            mesh.AddTriangle(start,start+1,start+2); mesh.AddTriangle(start,start+2,start+3);
        }
    }
}
