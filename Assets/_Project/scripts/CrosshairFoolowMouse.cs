using UnityEngine;

public class CrosshairFollowMouse : MonoBehaviour
{
    [SerializeField] private Camera targetCamera;

    private void Awake()
    {
        if (targetCamera == null)
            targetCamera = Camera.main;

        Cursor.visible = false;
    }

    private void Update()
    {
        if (targetCamera == null)
            return;

        Vector3 mousePosition = targetCamera.ScreenToWorldPoint(Input.mousePosition);
        mousePosition.z = 0f;

        transform.position = mousePosition;
    }

    private void OnDisable()
    {
        Cursor.visible = true;
    }
}