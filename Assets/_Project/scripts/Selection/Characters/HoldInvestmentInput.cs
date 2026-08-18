using UnityEngine;
using UnityEngine.EventSystems;

public sealed class HoldInvestmentInput : MonoBehaviour,
    IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
{
    public System.Action PointerDown { private get; set; }
    public System.Action PointerUp { private get; set; }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Left)
            PointerDown?.Invoke();
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Left)
            PointerUp?.Invoke();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (eventData.pointerPress == gameObject)
            PointerUp?.Invoke();
    }
}
