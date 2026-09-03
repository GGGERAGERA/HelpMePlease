using UnityEngine;
using UnityEngine.UI;

public class UICrosshairFollowMouse : MonoBehaviour
{
    [SerializeField] private RectTransform crosshairRect;

    private void Awake()
    {
        if (crosshairRect == null)
            crosshairRect = GetComponent<RectTransform>();

        // The software cursor must render above modal windows, while pointer
        // events continue through it to the buttons underneath.
        Canvas cursorCanvas = crosshairRect.GetComponent<Canvas>();
        if (cursorCanvas == null)
            cursorCanvas = crosshairRect.gameObject.AddComponent<Canvas>();
        cursorCanvas.overrideSorting = true;
        cursorCanvas.sortingOrder = short.MaxValue;
        foreach (Graphic graphic in crosshairRect.GetComponentsInChildren<Graphic>(true))
            graphic.raycastTarget = false;

        Cursor.visible = false;
    }

    private void Update()
    {
        crosshairRect.position = Input.mousePosition;
    }

    private void OnDisable()
    {
        Cursor.visible = true;
    }
}
