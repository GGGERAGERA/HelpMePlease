using UnityEngine;

public class UICrosshairFollowMouse : MonoBehaviour
{
    [SerializeField] private RectTransform crosshairRect;

    private void Awake()
    {
        if (crosshairRect == null)
            crosshairRect = GetComponent<RectTransform>();

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