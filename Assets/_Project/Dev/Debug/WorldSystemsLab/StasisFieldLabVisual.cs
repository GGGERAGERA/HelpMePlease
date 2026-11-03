#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections.Generic;
using UnityEngine;

// Presentation experiment confined to WorldSystemsLab. The original zone owns all gameplay.
public sealed class StasisFieldLabVisual : MonoBehaviour
{
    const int PixelsPerUnit = 16;
    const float InsideRate = .16f;
    sealed class Fragment
    {
        public Transform Body, Shadow;
        public Vector2 Anchor, Orbit;
        public float Phase, Clock, Rate, Speed, Spin;
        public bool CrossesEdge, WasInside, SawSlow;
    }
    readonly List<Fragment> fragments = new();
    readonly List<Renderer> retired = new();
    readonly List<bool> originalVisibility = new();
    Texture2D ink;
    Sprite sprite;
    Material material;
    SpriteRenderer surface;
    BoxCollider2D area;
    Color32[] pixels;
    int width, height;
    Vector2 half, centre;
    float nextVisibilityCheck;
    public int DebrisCount => fragments.Count;
    // Face the interior of the lab, rather than the orange outer-map limit.
    float DriftSide => centre.x >= 0 ? -1f : 1f;
    public Vector2 PreviewPosition => centre + Vector2.right * ((half.x-4.3f)*DriftSide);
    public int BoundaryCrossings { get; private set; }
    public bool ObservedEntrySlowdown { get; private set; }
    public bool ObservedExitRecovery { get; private set; }
    static readonly Color Shadow = new(.025f,.045f,.06f,.42f);
    static readonly Color Metal = new(.10f,.17f,.20f,.65f);
    static readonly Color Edge = new(.25f,.39f,.44f,.55f);

    public static void Attach(ProductionAnomalySite site, Sprite[] debrisSprites)
    {
        if (site.gameObject.scene.path != WorldSystemsLabController.ScenePath ||
            site.AnomalyZone == null || site.AnomalyZone.AnomalyType != LocalAnomalyType.Stasis) return;
        if (site.GetComponent<StasisFieldLabVisual>() != null) return;
        site.gameObject.AddComponent<StasisFieldLabVisual>().Build(site, debrisSprites);
    }

    void Build(ProductionAnomalySite site, Sprite[] debrisSprites)
    {
        area = (BoxCollider2D)site.AnomalyZone.FocusArea;
        Vector2 size = Vector2.Scale(area.size, area.transform.lossyScale);
        centre = area.transform.TransformPoint(area.offset);
        half = size * .5f;
        width = Mathf.CeilToInt(size.x * PixelsPerUnit);
        height = Mathf.CeilToInt(size.y * PixelsPerUnit);
        pixels = new Color32[width * height];
        DrawField();
        ink = new Texture2D(width,height,TextureFormat.RGBA32,false)
        { name = "Stasis / quiet boundary", filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
        ink.SetPixels32(pixels); ink.Apply(false,true); pixels = null;
        material = new Material(Shader.Find("Subject42/Lab/StasisField")) { name = "Stasis / matte pixel art (lab only)" };
        var art = new GameObject("STASIS / quiet boundary and suspended fragments — visual only");
        art.transform.SetParent(transform,false);
        art.transform.position = centre;
        surface = art.AddComponent<SpriteRenderer>();
        sprite = Sprite.Create(ink,new Rect(0,0,width,height),new Vector2(.5f,.5f),PixelsPerUnit,0,SpriteMeshType.FullRect);
        surface.sprite = sprite; surface.sharedMaterial = material;
        surface.sortingLayerName = "Midground"; surface.sortingOrder = -15;
        art.transform.localScale = new Vector3(size.x*PixelsPerUnit/width,size.y*PixelsPerUnit/height,1);
        BuildFragments(debrisSprites);
        RetireDebugArt();
    }

    void BuildFragments(Sprite[] sources)
    {
        if (sources == null || sources.Length == 0) return;
        for (int i=0;i<8;i++)
        {
            Sprite source = sources[i % sources.Length];
            if (source == null) continue;
            bool crossing = i >= 4;
            Vector2 anchor = i switch
            {
                0 => new Vector2(-half.x*.47f, half.y*.31f),
                1 => new Vector2(half.x*.62f, -Mathf.Min(3.5f,half.y*.40f)),
                2 => new Vector2(-half.x*.25f, -half.y*.47f),
                3 => new Vector2(half.x*.72f, Mathf.Min(4f,half.y*.38f)),
                4 => new Vector2(-half.x, -half.y*.15f),
                5 => new Vector2(half.x, half.y*.10f),
                6 => new Vector2(half.x, -half.y*.12f),
                _ => new Vector2(half.x*.35f, -half.y)
            };
            Vector2 orbit = crossing ? (i<7 ? new Vector2(1.25f,.55f) : new Vector2(.65f,1.25f)) : new Vector2(.70f,.42f);
            anchor.x *= DriftSide; orbit.x *= DriftSide;
            // One fragment enters immediately, another leaves: no respawns or hidden speed jumps.
            float phase = i switch { 4 => 4.45f, 5 => 4.45f, 6 => 1.40f, 7 => 3.0f, _ => i*1.73f };
            float size = .34f + (i%3)*.065f;
            float scale = size / Mathf.Max(source.bounds.size.x, source.bounds.size.y);
            var body = FragmentRenderer("Suspended fragment " + i, source, new Color(.59f,.69f,.77f,.86f), -13);
            var shadow = FragmentRenderer("Soft fragment shadow " + i, source, new Color(.015f,.025f,.035f,.18f), -16);
            body.localScale = Vector3.one*scale; shadow.localScale = Vector3.one*scale*.85f;
            var f = new Fragment { Body=body, Shadow=shadow, Anchor=anchor, Orbit=orbit, Phase=phase,
                Clock=i*2.31f, Speed=crossing?1.12f:.55f, Spin=(i%2==0?1:-1)*(9+i), CrossesEdge=crossing };
            Vector2 start = Path(f);
            f.Rate = SpeedAt(centre+start); f.WasInside = Contains(centre+start);
            f.SawSlow = f.Rate < .25f;
            fragments.Add(f); Pose(f);
        }
    }
    Transform FragmentRenderer(string label, Sprite source, Color color, int order)
    {
        var go = new GameObject(label); go.transform.SetParent(surface.transform,false);
        var renderer = go.AddComponent<SpriteRenderer>();
        renderer.sprite=source; renderer.sharedMaterial=material; renderer.color=color;
        renderer.sortingLayerName="Midground"; renderer.sortingOrder=order;
        return go.transform;
    }
    Vector2 Path(Fragment f) => f.Anchor + new Vector2(Mathf.Cos(f.Phase)*f.Orbit.x, Mathf.Sin(f.Phase)*f.Orbit.y);
    bool Contains(Vector2 point)
    {
        Vector2 local = area.transform.InverseTransformPoint(point); local -= area.offset;
        return Mathf.Abs(local.x)<=area.size.x*.5f && Mathf.Abs(local.y)<=area.size.y*.5f;
    }
    public float SpeedAt(Vector2 point)
    {
        Vector2 local = area.transform.InverseTransformPoint(point); local -= area.offset;
        Vector2 distance = area.size*.5f - new Vector2(Mathf.Abs(local.x),Mathf.Abs(local.y));
        float inset = Mathf.Min(distance.x*area.transform.lossyScale.x, distance.y*area.transform.lossyScale.y);
        // A narrow, smooth transition centred on the actual gameplay box, without touching physics.
        return Mathf.Lerp(1f,InsideRate,Mathf.SmoothStep(0,1,Mathf.InverseLerp(-.35f,.35f,inset)));
    }
    void Drift(float dt)
    {
        foreach (var f in fragments)
        {
            float target = SpeedAt(centre+Path(f));
            f.Rate = Mathf.Lerp(f.Rate,target,1-Mathf.Exp(-dt/.32f));
            float localDt = dt*f.Rate;
            float tangent = new Vector2(Mathf.Sin(f.Phase)*f.Orbit.x,Mathf.Cos(f.Phase)*f.Orbit.y).magnitude;
            f.Phase = Mathf.Repeat(f.Phase+localDt*f.Speed/Mathf.Max(.1f,tangent),Mathf.PI*2);
            f.Clock += localDt;
            if (f.CrossesEdge)
            {
                bool inside = Contains(centre+Path(f));
                if (inside!=f.WasInside) BoundaryCrossings++;
                f.WasInside=inside;
                if (inside && f.Rate < .25f) { ObservedEntrySlowdown=true; f.SawSlow=true; }
                if (!inside && f.SawSlow && f.Rate > .85f) ObservedExitRecovery=true;
            }
            Pose(f);
        }
    }
    void Pose(Fragment f)
    {
        Vector2 p=centre+Path(f);
        float bob=.035f*Mathf.Sin(f.Clock*.70f);
        float angle=f.Clock*f.Spin+4*Mathf.Sin(f.Clock*.42f);
        f.Body.position=new Vector3(p.x,p.y+.15f+bob,0);
        f.Shadow.position=new Vector3(p.x+.035f,p.y-.12f,0);
        f.Body.rotation=f.Shadow.rotation=Quaternion.Euler(0,0,angle);
    }
    void RetireDebugArt()
    {
        foreach (var r in GetComponentsInChildren<Renderer>(true))
        {
            if (r.transform.IsChildOf(surface.transform) || retired.Contains(r)) continue;
            retired.Add(r); originalVisibility.Add(r.forceRenderingOff); r.forceRenderingOff=true;
        }
    }
    void LateUpdate()
    {
        if (surface==null) return;
        // Substeps preserve a continuous path even when the editor has a slow frame.
        float remaining=Mathf.Min(Time.deltaTime,.20f);
        while (remaining>0) { float dt=Mathf.Min(remaining,1f/60f); Drift(dt); remaining-=dt; }
        if (Time.unscaledTime>=nextVisibilityCheck)
        { RetireDebugArt(); nextVisibilityCheck=Time.unscaledTime+1f; }
    }
    void OnDestroy()
    {
        for (int i=0;i<retired.Count;i++) if(retired[i]!=null) retired[i].forceRenderingOff=originalVisibility[i];
        if(surface!=null) Destroy(surface.gameObject);
        if(sprite!=null) Destroy(sprite); if(ink!=null) Destroy(ink); if(material!=null) Destroy(material);
    }

    void Pixel(int x,int y,Color color)
    { if(x>=0 && x<width && y>=0 && y<height) pixels[y*width+x]=color; }
    Vector2Int P(Vector2 p) => new(Mathf.RoundToInt((p.x+half.x)*PixelsPerUnit),Mathf.RoundToInt((p.y+half.y)*PixelsPerUnit));
    void Line(Vector2 a,Vector2 b,Color color,int thickness=1)
    {
        var from=P(a); var to=P(b); int steps=Mathf.Max(Mathf.Abs(to.x-from.x),Mathf.Abs(to.y-from.y));
        for(int s=0;s<=steps;s++)
        {
            float t=steps==0?0:s/(float)steps;
            int x=Mathf.RoundToInt(Mathf.Lerp(from.x,to.x,t)),y=Mathf.RoundToInt(Mathf.Lerp(from.y,to.y,t));
            for(int j=-(thickness/2);j<thickness-thickness/2;j++) for(int i=-(thickness/2);i<thickness-thickness/2;i++) Pixel(x+i,y+j,color);
        }
    }
    void Rect(Vector2 c,Vector2 s,Color color)
    {
        var a=P(c-s*.5f);var b=P(c+s*.5f);
        for(int y=a.y;y<=b.y;y++)for(int x=a.x;x<=b.x;x++)Pixel(x,y,color);
    }
    void DrawField()
    {
        // All concentric arcs, radial guides, calibration ticks, inscriptions and energy traces removed.
        Vector2 edge=half-Vector2.one/ PixelsPerUnit;
        Color boundary=new(.19f,.39f,.51f,.58f);
        Line(new(-edge.x,-edge.y),new(edge.x,-edge.y),boundary);
        Line(new(-edge.x,edge.y),new(edge.x,edge.y),boundary);
        Line(new(-edge.x,-edge.y),new(-edge.x,edge.y),boundary);
        Line(new(edge.x,-edge.y),new(edge.x,edge.y),boundary);
        foreach(int sx in new[]{-1,1}) foreach(int sy in new[]{-1,1})
        {
            Vector2 node=new(sx*(half.x-.46f),sy*(half.y-.44f));
            Rect(node+new Vector2(.035f,-.045f),new Vector2(.64f,.44f),Shadow);
            Rect(node,new Vector2(.57f,.34f),Metal);
            Line(node+new Vector2(-.25f,.15f),node+new Vector2(.25f,.15f),Edge);
            Line(node+new Vector2(-.25f,-.13f),node+new Vector2(.25f,-.13f),Edge);
            Rect(node,new Vector2(.25f,.09f),new Color(.12f,.24f,.30f,.68f));
        }
    }
}
#endif
