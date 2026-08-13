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

    private void Awake()
    {
        ResolveCamera();
    }

    private void LateUpdate()
    {
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
            normalOrthographicSize = controlledCamera.orthographicSize;
            return;
        }

        float focusedSize = normalOrthographicSize * focusZoomMultiplier;
        controlledCamera.orthographicSize = Mathf.Lerp(normalOrthographicSize, focusedSize, Ease(focusBlend));
    }

    private void OnDisable()
    {
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

    private static float Ease(float value)
    {
        return value * value * (3f - 2f * value);
    }
}
