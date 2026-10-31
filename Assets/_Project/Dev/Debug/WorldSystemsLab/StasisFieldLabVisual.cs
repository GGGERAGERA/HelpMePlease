#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections.Generic;
using UnityEngine;

// Presentation experiment confined to WorldSystemsLab. The original zone owns all gameplay.
public sealed class StasisFieldLabVisual : MonoBehaviour
{
    const int PixelsPerUnit = 16;
    Texture2D ink, energy;
    Sprite sprite;
    Material material;
    SpriteRenderer surface;
    readonly List<Renderer> retired = new();
    readonly List<bool> originalVisibility = new();
    Color32[] pixels;
    float[] mask;
    int width, height;
    Vector2 half;
    float nextVisibilityCheck;
    static readonly Color Shadow = new(.025f,.045f,.06f,.64f);
    static readonly Color Metal = new(.10f,.21f,.24f,.82f);
    static readonly Color Edge = new(.30f,.50f,.51f,.76f);
    static readonly Color Etch = new(.19f,.43f,.47f,.38f);

    public static void Attach(ProductionAnomalySite site)
    {
        if (site.gameObject.scene.path != WorldSystemsLabController.ScenePath ||
            site.AnomalyZone == null || site.AnomalyZone.AnomalyType != LocalAnomalyType.Stasis) return;
        if (site.GetComponent<StasisFieldLabVisual>() != null) return;
        site.gameObject.AddComponent<StasisFieldLabVisual>().Build(site);
    }

    void Build(ProductionAnomalySite site)
    {
        // Use the existing focus box, including its offset. Never touch its size or enabled state.
        var area = (BoxCollider2D)site.AnomalyZone.FocusArea;
        Vector2 size = Vector2.Scale(area.size, area.transform.lossyScale);
        half = size * .5f;
        width = Mathf.CeilToInt(size.x * PixelsPerUnit);
        height = Mathf.CeilToInt(size.y * PixelsPerUnit);
        pixels = new Color32[width * height]; mask = new float[pixels.Length];
        DrawField();
        ink = Texture("Stasis • etched floor", pixels);
        var glow = new Color32[pixels.Length];
        for (int y = 0; y < height; y++) for (int x = 0; x < width; x++)
        {
            float core = mask[y * width + x], halo = 0;
            for (int j = -3; j <= 3; j++) for (int i = -3; i <= 3; i++)
                if (x+i >= 0 && x+i < width && y+j >= 0 && y+j < height)
                    halo = Mathf.Max(halo, mask[(y+j)*width+x+i] * Mathf.Exp(-(i*i+j*j)*.38f) * .22f);
            glow[y*width+x] = new Color(core, halo, 0, 1);
        }
        energy = Texture("Stasis • embedded circuits",glow,true);
        material = new Material(Shader.Find("Subject42/Lab/StasisField")) { name = "Stasis field (lab only)" };
        material.SetTexture("_MainTex",ink); material.SetTexture("_GlowTex",energy);
        material.SetVector("_FieldSize", new Vector4(size.x,size.y,0,0));
        var art = new GameObject("STASIS / etched technological field — visual only");
        art.transform.SetParent(transform,false);
        art.transform.position = area.transform.TransformPoint(area.offset);
        surface = art.AddComponent<SpriteRenderer>();
        sprite = Sprite.Create(ink,new Rect(0,0,width,height),new Vector2(.5f,.5f),PixelsPerUnit,0,SpriteMeshType.FullRect);
        surface.sprite = sprite; surface.sharedMaterial = material;
        surface.sortingLayerName = "Midground"; surface.sortingOrder = -15;
        art.transform.localScale = new Vector3(size.x*PixelsPerUnit/width,size.y*PixelsPerUnit/height,1);
        RetireDebugArt();
        pixels = null; mask = null;
    }
    Texture2D Texture(string label, Color32[] data,bool linear=false)
    {
        var result = new Texture2D(width,height,TextureFormat.RGBA32,false,linear) { name = label, filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
        result.SetPixels32(data); result.Apply(false,true); return result;
    }
    void RetireDebugArt()
    {
        foreach (var r in GetComponentsInChildren<Renderer>(true))
        {
            if (r == surface || retired.Contains(r)) continue;
            retired.Add(r); originalVisibility.Add(r.forceRenderingOff); r.forceRenderingOff = true;
        }
    }
    void LateUpdate()
    {
        if (surface == null || Time.unscaledTime < nextVisibilityCheck) return;
        RetireDebugArt(); nextVisibilityCheck = Time.unscaledTime + 1f;
    }
    void OnDestroy()
    {
        for (int i=0;i<retired.Count;i++) if(retired[i]!=null) retired[i].forceRenderingOff=originalVisibility[i];
        if (surface != null) Destroy(surface.gameObject);
        if(sprite!=null) Destroy(sprite); if(ink!=null) Destroy(ink); if(energy!=null) Destroy(energy); if(material!=null) Destroy(material);
    }

    void Pixel(int x,int y,Color color,float power=0)
    {
        if(x<0 || x>=width || y<0 || y>=height) return;
        int n=y*width+x;
        if(color.a>0) pixels[n]=color;
        mask[n]=Mathf.Max(mask[n],power);
    }
    Vector2Int P(Vector2 p) => new(Mathf.RoundToInt((p.x+half.x)*PixelsPerUnit),Mathf.RoundToInt((p.y+half.y)*PixelsPerUnit));
    void Line(Vector2 a,Vector2 b,Color color,int thickness=1,float power=0)
    {
        var from=P(a); var to=P(b); int steps=Mathf.Max(Mathf.Abs(to.x-from.x),Mathf.Abs(to.y-from.y));
        for(int s=0;s<=steps;s++)
        {
            float t=steps==0?0:s/(float)steps;
            int x=Mathf.RoundToInt(Mathf.Lerp(from.x,to.x,t)),y=Mathf.RoundToInt(Mathf.Lerp(from.y,to.y,t));
            for(int j=-(thickness/2);j<thickness-thickness/2;j++) for(int i=-(thickness/2);i<thickness-thickness/2;i++) Pixel(x+i,y+j,color,power);
        }
    }
    Vector2 Polar(float r,float degrees) => new Vector2(Mathf.Cos(degrees*Mathf.Deg2Rad),Mathf.Sin(degrees*Mathf.Deg2Rad))*r;
    void Arc(float r,float start,float end,Color color,int thickness=1,float power=0)
    {
        int steps=Mathf.CeilToInt((end-start)*Mathf.Deg2Rad*r*PixelsPerUnit*1.25f);
        Vector2 last=Polar(r,start);
        for(int s=1;s<=steps;s++) { Vector2 p=Polar(r,Mathf.Lerp(start,end,s/(float)steps)); Line(last,p,color,thickness,power); last=p; }
    }
    void Rect(Vector2 c,Vector2 s,Color color)
    {
        var a=P(c-s*.5f);var b=P(c+s*.5f);
        for(int y=a.y;y<=b.y;y++)for(int x=a.x;x<=b.x;x++)Pixel(x,y,color);
    }
    void Frame(Vector2 c,Vector2 s,Color color,int thickness=1,float power=0)
    {
        var d=s*.5f; Line(c+new Vector2(-d.x,-d.y),c+new Vector2(d.x,-d.y),color,thickness,power);
        Line(c+new Vector2(d.x,-d.y),c+d,color,thickness,power); Line(c+d,c+new Vector2(-d.x,d.y),color,thickness,power);
        Line(c+new Vector2(-d.x,d.y),c-d,color,thickness,power);
    }
    void DrawField()
    {
        float radius=Mathf.Min(half.x,half.y)*.78f;
        // Broken structural arcs; most of the texture remains fully transparent.
        for(int ring=0;ring<4;ring++)
        {
            float r=radius*(1f-ring*.185f);
            int count=ring==3?4:8;
            for(int s=0;s<count;s++)
            {
                float angle=s*360f/count+(ring%2)*13f;
                float span=360f/count-(s%3==0?15:8);
                int band=ring==0?10:ring==2?7:3;
                float rim=band/(2f*PixelsPerUnit);
                Arc(r,angle,angle+span,Shadow,band+4);
                Arc(r,angle,angle+span,Metal,band);
                Arc(r+rim,angle+1,angle+span-1,Edge);
                Arc(r-rim,angle+2,angle+span-3,Etch);
                Arc(r-rim*.3f,angle+3,angle+span-3,new Color(.025f,.10f,.13f,.85f),2);
                Arc(r-rim*.3f,angle+4,angle+span*.65f,Color.clear,1,ring%2==0?.82f:.48f);
                Line(Polar(r-rim,angle+span),Polar(r+rim,angle+span),Edge,2);
                if(band>4)
                {
                    // Engraved vents and aged fasteners break the broad inset bands into machinery.
                    for(float a=angle+4;a<angle+span-3;a+=2.4f)
                        Line(Polar(r+rim*.30f,a),Polar(r+rim*.75f,a+.2f),new Color(.33f,.45f,.43f,.55f));
                    for(int screw=0;screw<2;screw++)
                    {
                        Vector2 p=Polar(r,angle+(screw==0?1.2f:span-1.2f));
                        Rect(p,new Vector2(.13f,.13f),new Color(.46f,.44f,.31f,.78f));
                    }
                    Arc(r+rim*.50f,angle+span*.65f,angle+span*.84f,new Color(.44f,.39f,.24f,.48f),2);
                }
            }
        }
        // Sparse calibration ticks, worn radial guides and circuit traces.
        for(int i=0;i<72;i++)
        {
            float a=i*5f; if(i%9==0)continue;
            float r=radius+ .22f;
            Line(Polar(r,a),Polar(r+(i%3==0?.18f:.07f),a),Etch,1,i%9==2?.20f:0);
        }
        for(int i=0;i<8;i++)
        {
            float a=22.5f+i*45;
            Line(Polar(radius*.23f,a),Polar(radius*.91f,a),new Color(.19f,.38f,.40f,.21f));
            Vector2 p=Polar(radius*.88f,a);
            Frame(p,new Vector2(.16f,.16f),Edge);
        }
        // Four flat, recessed coils bridge the large broken outer arcs.
        for(int i=0;i<4;i++)
        {
            float a=41+i*90;
            Vector2 radial=Polar(1,a), tangent=Polar(1,a+90), c=radial*radius;
            Line(c-tangent*.65f,c+tangent*.65f,Shadow,20);
            Line(c-tangent*.58f,c+tangent*.58f,Metal,15);
            Line(c-tangent*.52f-radial*.42f,c+tangent*.52f-radial*.42f,Edge,2);
            Line(c-tangent*.52f+radial*.42f,c+tangent*.52f+radial*.42f,Edge,2);
            for(int j=-3;j<=3;j++)
            {
                Vector2 p=c+tangent*(j*.14f);
                Line(p-radial*.27f,p+radial*.27f,new Color(.02f,.10f,.13f,.95f),2);
                Line(p-radial*.16f,p+radial*.16f,Color.clear,1,.78f);
            }
            for(int sign=-1;sign<=1;sign+=2)
            {
                Vector2 p=c+tangent*(sign*.68f);
                Line(p-radial*.40f,p+radial*.40f,Edge,3);
                Rect(p+radial*.33f,new Vector2(.13f,.13f),new Color(.47f,.43f,.29f,.9f));
            }
        }
        // Angular floor traces are sparse enough to preserve the terrain between the rings.
        for(int i=0;i<4;i++)
        {
            float a=i*90;
            Vector2 n=Polar(1,a),t=Polar(1,a+90);
            Vector2 start=n*(radius*.40f)+t*(radius*.19f), elbow=n*(radius*.65f)+t*(radius*.19f);
            Line(start,elbow,Shadow,4);Line(elbow,n*(radius*.81f)+t*(radius*.35f),Shadow,4);
            Line(start,elbow,Etch,1,.24f);Line(elbow,n*(radius*.81f)+t*(radius*.35f),Etch,1,.24f);
            Vector2 stamp=n*(radius*.57f)+t*(radius*.43f);
            for(int j=0;j<3;j++)Line(stamp+t*j*.20f,stamp+t*j*.20f+n*.35f,Edge,2);
            Frame(n*(radius*.71f)-t*(radius*.24f),new Vector2(.50f,.50f),Etch);
        }
        Arc(radius*.19f,0,360,Etch);
        for(int i=0;i<4;i++)
        {
            float a=i*90f;
            Line(Polar(.22f,a),Polar(.48f,a),Edge,1,.35f);
            Arc(radius*.24f,a+12,a+52,Color.clear,1,.40f);
        }
        // Exact original box edge: hairlines with breathing gaps, never a raised rim.
        for(int side=0;side<4;side++)
        {
            bool horizontal=side<2; float sign=side%2==0?-1:1;
            float length=(horizontal?half.x:half.y)*2;
            float edge=(horizontal?half.y:half.x)-.035f;
            int segments=Mathf.Max(3,Mathf.FloorToInt(length/1.8f));
            for(int i=0;i<segments;i++)
            {
                float start=-length*.5f+i*length/segments+.18f;
                float end=-length*.5f+(i+1)*length/segments-.20f;
                Vector2 A(float t,float inset=0)=>horizontal?new Vector2(t,sign*(edge-inset)):new Vector2(sign*(edge-inset),t);
                Line(A(start,.06f),A(end,.06f),Shadow,3);
                Line(A(start),A(end),Color.clear,1,.58f);
                Line(A(start+.08f,.16f),A(Mathf.Min(end,start+.45f),.16f),Etch);
            }
        }
        // Four flat relic emitters. Small footprints, no colliders, no tall silhouettes.
        foreach(int sx in new[]{-1,1}) foreach(int sy in new[]{-1,1})
        {
            Vector2 node=new(sx*(half.x-.64f),sy*(half.y-.74f));
            Node(node,sx,sy);
            Vector2 target=Polar(radius+ .28f,Mathf.Atan2(node.y,node.x)*Mathf.Rad2Deg);
            Vector2 elbow=new(node.x,target.y);
            Line(node,elbow,Shadow,4); Line(elbow,target,Shadow,4);
            Line(node,elbow,Etch,1,.20f); Line(elbow,target,Etch,1,.20f);
            Vector2 corner=new(sx*(half.x-.10f),sy*(half.y-.10f));
            Line(corner,corner-new Vector2(sx*.48f,0),Color.clear,2,.85f);
            Line(corner,corner-new Vector2(0,sy*.48f),Color.clear,2,.85f);
            Glyph(node+new Vector2(-.40f,-sy*.53f),sx==sy?"S42":"T07");
        }
        // Coordinate inscriptions and a quiet centre marker belong to the floor.
        Glyph(new Vector2(-.66f,radius+.63f),"STASIS");
        Glyph(new Vector2(-.50f,-radius-.74f),"S42-07");
        // Deterministic missing pixels / scratches make the technology feel excavated.
        for(int i=0;i<pixels.Length;i++)
        {
            uint hash=unchecked((uint)i*747796405u+2891336453u); hash=((hash>>((int)(hash>>28)+4))^hash)*277803737u;
            if((hash&127)==0){ pixels[i].a=(byte)(pixels[i].a*.35f);mask[i]*=.25f; }
        }
    }
    void Node(Vector2 c,int sx,int sy)
    {
        Rect(c+new Vector2(.04f,-.07f),new Vector2(1.33f,.93f),Shadow);
        Rect(c,new Vector2(1.24f,.83f),Metal); Frame(c,new Vector2(1.24f,.83f),Edge,2);
        Rect(c,new Vector2(.86f,.51f),new Color(.025f,.08f,.10f,.93f));
        for(int i=-3;i<=3;i++)
        {
            float x=i*.105f; Line(c+new Vector2(x,-.19f),c+new Vector2(x,.19f),Color.clear,1,.90f);
        }
        foreach(int sign in new[]{-1,1})
        {
            Rect(c+new Vector2(sign*.51f,.30f),new Vector2(.10f,.10f),Edge);
            Rect(c+new Vector2(sign*.51f,-.30f),new Vector2(.10f,.10f),Edge);
            Line(c+new Vector2(sign*.70f,-.23f),c+new Vector2(sign*.70f,.23f),Etch,2);
        }
    }
    // 3x5 technical stencil, deliberately small and diegetic.
    void Glyph(Vector2 at,string value)
    {
        const string chars="STAI4207-";
        string[] bits={"111100111001111","111010010010010","010101111101101","111010010010111","101101111001001","111001111100111","111101101101111","111001010010010","000000111000000"};
        Vector2Int p=P(at);
        for(int n=0;n<value.Length;n++)
        {
            int index=chars.IndexOf(value[n]);if(index<0)continue;
            for(int y=0;y<5;y++)for(int x=0;x<3;x++)if(bits[index][y*3+x]=='1')
                for(int j=0;j<2;j++)for(int i=0;i<2;i++)Pixel(p.x+n*8+x*2+i,p.y+(4-y)*2+j,Etch,.10f);
        }
    }
}
#endif
