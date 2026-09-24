using UnityEngine;
using UnityEngine.UI;

public class UICrosshairFollowMouse : MonoBehaviour
{
    [SerializeField] private RectTransform crosshairRect;
    [SerializeField] private Canvas cursorCanvas;

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

    private void OnEnable() => RunStateManager.EnsureExists().RegisterSceneCleanup(ReleaseRunScene);
    private void ReleaseRunScene()
    {
        enabled = false;
        Cursor.lockState = CursorLockMode.None;
    }

    private void OnDisable()
    {
        RunStateManager.Instance?.UnregisterSceneCleanup(ReleaseRunScene);
        Cursor.visible = true;
    }
}
