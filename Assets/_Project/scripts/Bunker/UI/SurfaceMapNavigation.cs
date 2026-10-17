using UnityEngine;
using UnityEngine.EventSystems;

public sealed class SurfaceMapNavigation : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IScrollHandler
{
    public const float MinZoom = .65f, MaxZoom = 2.2f;
    public RectTransform Content { get; private set; }
    private RectTransform viewport;
    private Vector2 lastPoint, home;
    private Rect bounds = new(-300,-200,600,400);
    public void Configure(RectTransform content) { Content = content; viewport = (RectTransform)transform; }
    public void SetBounds(Rect mapBounds, Vector2 bunker) { bounds=mapBounds; home=bunker; Clamp(); }
    public void ResetView()
    {
        if(Content==null)return;
        Vector2 half=viewport.rect.size*.5f-new Vector2(20,20);
        float extentX=Mathf.Max(Mathf.Abs(bounds.xMin-home.x),Mathf.Abs(bounds.xMax-home.x));
        float extentY=Mathf.Max(Mathf.Abs(bounds.yMin-home.y),Mathf.Abs(bounds.yMax-home.y));
        float fit=Mathf.Clamp(Mathf.Min(half.x/Mathf.Max(1,extentX),half.y/Mathf.Max(1,extentY)),MinZoom,MaxZoom);
        Content.localScale=Vector3.one*fit; Content.anchoredPosition=-home*fit; Clamp();
    }
    public void OnBeginDrag(PointerEventData data)
    { RectTransformUtility.ScreenPointToLocalPointInRectangle(viewport,data.position,data.pressEventCamera,out lastPoint); }
    public void OnDrag(PointerEventData data)
    {
        if(Content==null)return;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(viewport,data.position,data.pressEventCamera,out var point);
        Pan(point-lastPoint); lastPoint=point;
    }
    public void OnEndDrag(PointerEventData data) { }
    public void Pan(Vector2 delta) { if(Content==null)return; Content.anchoredPosition+=delta; Clamp(); }
    public void OnScroll(PointerEventData data)
    {
        RectTransformUtility.ScreenPointToLocalPointInRectangle(viewport,data.position,data.enterEventCamera,out var point);
        Zoom(data.scrollDelta.y,point);
    }
    public void Zoom(float delta,Vector2 point)
    {
        if(Content==null)return;
        float old=Content.localScale.x, next=Mathf.Clamp(old*Mathf.Pow(1.12f,delta),MinZoom,MaxZoom);
        Content.anchoredPosition=point+(Content.anchoredPosition-point)*(next/old);
        Content.localScale=Vector3.one*next; Clamp();
    }
    private void Clamp()
    {
        if(Content==null||viewport==null)return;
        float scale=Content.localScale.x;
        // Keep the surveyed field nearby; zooming out cannot pan into an empty canvas.
        Vector2 half=viewport.rect.size*.5f, p=Content.anchoredPosition;
        Vector2 range=new(Mathf.Max(0,bounds.width*scale*.5f-half.x)+half.x*.35f,
            Mathf.Max(0,bounds.height*scale*.5f-half.y)+half.y*.35f);
        Vector2 center=-bounds.center*scale;
        p.x=Mathf.Clamp(p.x,center.x-range.x,center.x+range.x);
        p.y=Mathf.Clamp(p.y,center.y-range.y,center.y+range.y);
        Content.anchoredPosition=p;
    }
}
