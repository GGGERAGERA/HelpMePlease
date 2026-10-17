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
    private RectTransform selection;
    private Vector2 selectionHome;
    private Graphic rim;
    public void Configure(Graphic border, RectTransform marker) { rim=border; selection=marker; selectionHome=marker.anchoredPosition; }
    public void OnPointerEnter(PointerEventData e) { hovered=true; }
    public void OnPointerExit(PointerEventData e) { hovered=false; }
    public void OnPointerClick(PointerEventData e)
    { if(e.button!=PointerEventData.InputButton.Left)return; if(e.clickCount==2)DoubleClick?.Invoke(); }
    private void OnDisable() { hovered=false; }
    private void Update()
    {
        if(selection!=null && Selected)
            selection.anchoredPosition=selectionHome+Vector2.up*(Mathf.Round(Mathf.Sin(Time.unscaledTime*3f)+1)*2);
        if(rim!=null)rim.canvasRenderer.SetColor(hovered&&Available?new Color(1.3f,1.3f,1.3f):Color.white);
    }
}
