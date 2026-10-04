using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Drag bubbles up from nodes; EventSystem cancels their click when a drag starts.
public sealed class SurfaceMapNavigation : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IScrollHandler
{
    public RectTransform Content { get; private set; }
    private RectTransform viewport;
    private Vector2 lastPoint;
    public void Configure(RectTransform content) { Content = content; viewport = (RectTransform)transform; }
    public void OnBeginDrag(PointerEventData data)
    { RectTransformUtility.ScreenPointToLocalPointInRectangle(viewport, data.position, data.pressEventCamera, out lastPoint); }
    public void OnDrag(PointerEventData data)
    {
        if (Content == null) return;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(viewport, data.position, data.pressEventCamera, out var point);
        Pan(point - lastPoint); lastPoint = point;
    }
    public void OnEndDrag(PointerEventData data) { }
    public void Pan(Vector2 delta)
    {
        if (Content == null) return;
        Vector2 next = Content.anchoredPosition + delta;
        float limit = 600f * Content.localScale.x;
        Content.anchoredPosition = new Vector2(Mathf.Clamp(next.x, -limit, limit), Mathf.Clamp(next.y, -limit, limit));
    }
    public void OnScroll(PointerEventData data)
    {
        RectTransformUtility.ScreenPointToLocalPointInRectangle(viewport, data.position, data.enterEventCamera, out var point);
        Zoom(data.scrollDelta.y, point);
    }
    public void Zoom(float delta, Vector2 point)
    {
        if (Content == null) return;
        float oldScale = Content.localScale.x;
        float next = Mathf.Clamp(oldScale * Mathf.Pow(1.12f, delta), .65f, 2.2f);
        Content.anchoredPosition = point + (Content.anchoredPosition - point) * (next / oldScale);
        Content.localScale = Vector3.one * next;
    }
}

