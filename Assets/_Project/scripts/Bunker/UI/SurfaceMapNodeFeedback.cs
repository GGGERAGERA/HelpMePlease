using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>Node feedback only. Selection and inspection remain in the map view.</summary>
public sealed class SurfaceMapNodeFeedback : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    public bool Selected, Available;
    public Action DoubleClick;
    private bool hovered;
    private float clickUntil;
    private Graphic rim;
    public void Configure(Graphic border) { rim=border; }
    public void OnPointerEnter(PointerEventData e) { hovered=true; }
    public void OnPointerExit(PointerEventData e) { hovered=false; }
    public void OnPointerClick(PointerEventData e)
    { if(e.button!=PointerEventData.InputButton.Left)return; clickUntil=Time.unscaledTime+.12f; if(e.clickCount==2)DoubleClick?.Invoke(); }
    private void OnDisable() { hovered=false; }
    private void Update()
    {
        float scale=Selected?1.05f:1f;
        if(Time.unscaledTime<clickUntil)scale*=.97f;
        transform.localScale=Vector3.one*scale;
        if(rim!=null)rim.canvasRenderer.SetColor(hovered&&Available?new Color(1.3f,1.3f,1.3f):Color.white);
    }
}
