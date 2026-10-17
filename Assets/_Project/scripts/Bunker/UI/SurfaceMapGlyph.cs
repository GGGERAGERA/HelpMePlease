using UnityEngine;
using UnityEngine.UI;

public enum SurfaceMapMeaning { Danger, Reward, Anomaly, Event, Rule, Expedition }

/// <summary>Shared condition symbols plus a neutral expedition marker; owns no gameplay profile.</summary>
[RequireComponent(typeof(CanvasRenderer))]
public sealed class SurfaceMapGlyph : MaskableGraphic
{
    [SerializeField] private SurfaceMapMeaning meaning;
    private static readonly string[][] Pixels =
    {
        new[]{"000010000","000111000","001010100","001010100","010010010","010000010","100010001","111111111","000000000"},
        new[]{"001111100","011000110","110010011","110111011","110010011","110111011","110010011","011000110","001111100"},
        new[]{"000010000","000101000","001001100","010011010","100110001","010100010","001101100","000101000","000010000"},
        new[]{"010000000","011111110","010111100","011111110","010000000","010000000","010000000","111100000","000000000"},
        new[]{"001111100","011000110","110010011","100111001","100010001","100111001","110010011","011000110","001111100"},
        new[]{"000111000","011000110","110000011","100000001","100000001","110000011","011000110","001111100","000000000"}
    };
    public static Color Tint(SurfaceMapMeaning value) => value switch
    {
        SurfaceMapMeaning.Danger => new Color32(220,111,97,255),
        SurfaceMapMeaning.Reward => new Color32(222,192,106,255),
        SurfaceMapMeaning.Anomaly => new Color32(178,149,203,255),
        SurfaceMapMeaning.Event => new Color32(214,158,91,255),
        SurfaceMapMeaning.Rule => new Color32(126,184,187,255),
        _ => new Color32(162,174,163,255)
    };
    public void Show(SurfaceMapMeaning value)
    {
        meaning=value; color=Tint(value); raycastTarget=false; SetVerticesDirty();
    }
    protected override void OnPopulateMesh(VertexHelper mesh)
    {
        mesh.Clear(); var rect=GetPixelAdjustedRect();
        float pixel=Mathf.Max(1,Mathf.Round(Mathf.Min(rect.width,rect.height)/9));
        Vector2 origin=rect.center-new Vector2(pixel*4.5f,pixel*4.5f);
        var rows=Pixels[(int)meaning];
        for(int y=0;y<9;y++) for(int x=0;x<9;x++)
        {
            if(rows[y][x]!='1')continue;
            float left=origin.x+x*pixel,bottom=origin.y+(8-y)*pixel; int start=mesh.currentVertCount;
            mesh.AddVert(new Vector3(left,bottom),color,Vector2.zero);
            mesh.AddVert(new Vector3(left,bottom+pixel),color,Vector2.zero);
            mesh.AddVert(new Vector3(left+pixel,bottom+pixel),color,Vector2.zero);
            mesh.AddVert(new Vector3(left+pixel,bottom),color,Vector2.zero);
            mesh.AddTriangle(start,start+1,start+2); mesh.AddTriangle(start,start+2,start+3);
        }
    }
}
