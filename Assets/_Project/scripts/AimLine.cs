using UnityEngine;

public class AimLine : MonoBehaviour
{
    [SerializeField] private Transform firePoint;
    [SerializeField] private Camera targetCamera;
    [SerializeField] private LineRenderer lineRenderer;

    private void Awake()
    {
        if (targetCamera == null)
            targetCamera = Camera.main;

        if (lineRenderer == null)
            lineRenderer = GetComponent<LineRenderer>();

        lineRenderer.positionCount = 2;
    }

    private void Update()
    {
        if (firePoint == null || targetCamera == null || lineRenderer == null)
            return;

        Vector3 mouseWorldPosition = targetCamera.ScreenToWorldPoint(Input.mousePosition);
        mouseWorldPosition.z = 0f;

        Vector3 start = firePoint.position;
        Vector3 end = mouseWorldPosition;

        start.z = 0f;
        end.z = 0f;

        lineRenderer.SetPosition(0, start);
        lineRenderer.SetPosition(1, end);
    }
}