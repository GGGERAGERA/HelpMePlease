using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    public Transform target;
    public Vector3 offset = new Vector3(0, 0, -10);
    public float smoothSpeed = 0.125f;

    [Header("Temporary Focus")]
    [SerializeField] private Camera controlledCamera;

    private Object focusOwner;
    private Transform focusTarget;
    private float focusZoomMultiplier = 1f;
    private float focusPositionStrength;
    private float focusBlend;
    private float focusBlendTarget;
    private float focusInDuration = 0.4f;
    private float focusOutDuration = 0.5f;
    private float normalOrthographicSize;
    private bool hasFocusSession;
    private float productionOrthographicSize;
    private float debugOrthographicSize = -1f;
    private bool productionOrthographicSizeCaptured;

    private Object worldBoundsOwner;
    private Vector3 worldBoundsRootPosition;
    private Vector3 savedWorldBoundsRootPosition;
    private float worldBoundsOrthographicSize;
    private float savedWorldBoundsOrthographicSize;
    private bool hasWorldBoundsFocus;

    public Camera ControlledCamera
    {
        get
        {
            ResolveCamera();
            return controlledCamera;
        }
    }

    private void Awake()
    {
        ResolveCamera();
        CaptureProductionOrthographicSize();
    }

    public bool OrthographicZoomAvailable
    {
        get
        {
            ResolveCamera();
            return controlledCamera != null && controlledCamera.orthographic;
        }
    }

    public float ProductionOrthographicSize
    {
        get
        {
            CaptureProductionOrthographicSize();
            return productionOrthographicSize;
        }
    }

    public float DebugOrthographicSize => debugOrthographicSize > 0f
        ? debugOrthographicSize
        : ProductionOrthographicSize;

    public void SetDebugOrthographicSize(float value)
    {
        CaptureProductionOrthographicSize();
        debugOrthographicSize = Mathf.Clamp(value, 2f, 16f);
        normalOrthographicSize = debugOrthographicSize;

        if (!hasWorldBoundsFocus && controlledCamera != null &&
            controlledCamera.orthographic)
        {
            ApplyFocusZoom();
        }
    }

    public void ResetDebugOrthographicSize()
    {
        CaptureProductionOrthographicSize();
        debugOrthographicSize = -1f;
        normalOrthographicSize = productionOrthographicSize;
        if (!hasWorldBoundsFocus && controlledCamera != null &&
            controlledCamera.orthographic)
        {
            if (hasFocusSession)
                ApplyFocusZoom();
            else
                controlledCamera.orthographicSize = productionOrthographicSize;
        }
    }

    private void LateUpdate()
    {
        if (hasWorldBoundsFocus)
        {
            transform.position = worldBoundsRootPosition;
            if (controlledCamera != null && controlledCamera.orthographic)
                controlledCamera.orthographicSize = worldBoundsOrthographicSize;
            return;
        }

        ResolveTarget();
        UpdateFocusBlend();

        if (target != null)
        {
            Vector3 desiredPosition = target.position + offset;

            if (focusTarget != null && focusBlend > 0f)
            {
                Vector3 focusPosition = focusTarget.position + offset;
                float positionBlend = Ease(focusBlend) * focusPositionStrength;
                desiredPosition = Vector3.Lerp(desiredPosition, focusPosition, positionBlend);
            }

            transform.position = Vector3.Lerp(transform.position, desiredPosition, smoothSpeed);
        }

        ApplyFocusZoom();
    }

    public void BeginTemporaryFocus(
        Object owner,
        Transform newFocusTarget,
        float zoomMultiplier,
        float positionStrength,
        float duration)
    {
        if (newFocusTarget == null)
            return;

        ResolveCamera();
        if (!hasFocusSession)
        {
            if (controlledCamera != null && controlledCamera.orthographic)
                normalOrthographicSize = controlledCamera.orthographicSize;

            hasFocusSession = true;
        }

        focusOwner = owner;
        focusTarget = newFocusTarget;
        focusZoomMultiplier = Mathf.Clamp(zoomMultiplier, 0.5f, 1f);
        focusPositionStrength = Mathf.Clamp01(positionStrength);
        focusInDuration = Mathf.Max(0.01f, duration);
        focusBlendTarget = 1f;
    }

    public void EndTemporaryFocus(Object owner, float duration)
    {
        if (!hasFocusSession || focusOwner != owner)
            return;

        focusOutDuration = Mathf.Max(0.01f, duration);
        focusBlendTarget = 0f;
    }

    public bool BeginWorldBoundsFocus(
        Object owner,
        Vector2 worldCenter,
        float orthographicSize)
    {
        ResolveCamera();
        if (owner == null || controlledCamera == null ||
            !controlledCamera.orthographic || orthographicSize <= 0f)
        {
            return false;
        }

        if (!hasWorldBoundsFocus)
        {
            savedWorldBoundsRootPosition = transform.position;
            savedWorldBoundsOrthographicSize = controlledCamera.orthographicSize;
        }

        Vector3 cameraWorldOffset = controlledCamera.transform.position - transform.position;
        Vector3 framedCameraPosition = new(
            worldCenter.x,
            worldCenter.y,
            controlledCamera.transform.position.z);

        worldBoundsOwner = owner;
        worldBoundsRootPosition = framedCameraPosition - cameraWorldOffset;
        worldBoundsOrthographicSize = orthographicSize;
        hasWorldBoundsFocus = true;
        transform.position = worldBoundsRootPosition;
        controlledCamera.orthographicSize = worldBoundsOrthographicSize;
        return true;
    }

    public void EndWorldBoundsFocus(Object owner)
    {
        if (!hasWorldBoundsFocus || worldBoundsOwner != owner)
            return;

        transform.position = savedWorldBoundsRootPosition;
        if (controlledCamera != null && controlledCamera.orthographic &&
            savedWorldBoundsOrthographicSize > 0f)
        {
            controlledCamera.orthographicSize = savedWorldBoundsOrthographicSize;
        }

        hasWorldBoundsFocus = false;
        worldBoundsOwner = null;
    }

    private void ResolveTarget()
    {
        if (target != null)
            return;

        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
            target = player.transform;
    }

    private void ResolveCamera()
    {
        if (controlledCamera == null)
            controlledCamera = GetComponentInChildren<Camera>(true);
    }

    private void UpdateFocusBlend()
    {
        if (!hasFocusSession)
            return;

        float duration = focusBlendTarget > focusBlend ? focusInDuration : focusOutDuration;
        focusBlend = Mathf.MoveTowards(focusBlend, focusBlendTarget, Time.deltaTime / duration);

        if (focusBlendTarget <= 0f && focusBlend <= 0f)
        {
            focusBlend = 0f;
            RestoreNormalZoom();
            hasFocusSession = false;
            focusOwner = null;
            focusTarget = null;
        }
    }

    private void ApplyFocusZoom()
    {
        ResolveCamera();
        if (controlledCamera == null || !controlledCamera.orthographic)
            return;

        if (!hasFocusSession)
        {
            if (debugOrthographicSize > 0f)
            {
                normalOrthographicSize = debugOrthographicSize;
                controlledCamera.orthographicSize = debugOrthographicSize;
            }
            else
            {
                normalOrthographicSize = controlledCamera.orthographicSize;
            }
            return;
        }

        float focusedSize = normalOrthographicSize * focusZoomMultiplier;
        controlledCamera.orthographicSize = Mathf.Lerp(normalOrthographicSize, focusedSize, Ease(focusBlend));
    }

    private void OnDisable()
    {
        EndWorldBoundsFocus(worldBoundsOwner);
        RestoreNormalZoom();
        hasFocusSession = false;
        focusBlend = 0f;
        focusBlendTarget = 0f;
        focusOwner = null;
        focusTarget = null;
    }

    private void RestoreNormalZoom()
    {
        if (controlledCamera != null && controlledCamera.orthographic && normalOrthographicSize > 0f)
            controlledCamera.orthographicSize = normalOrthographicSize;
    }

    private void CaptureProductionOrthographicSize()
    {
        ResolveCamera();
        if (productionOrthographicSizeCaptured || controlledCamera == null ||
            !controlledCamera.orthographic)
        {
            return;
        }

        productionOrthographicSize = controlledCamera.orthographicSize;
        productionOrthographicSizeCaptured = true;
        normalOrthographicSize = productionOrthographicSize;
    }

    private static float Ease(float value)
    {
        return value * value * (3f - 2f * value);
    }
}
