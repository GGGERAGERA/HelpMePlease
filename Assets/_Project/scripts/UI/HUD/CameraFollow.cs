using UnityEngine;
using System.Collections.Generic;
using UnityEngine.EventSystems;
using Subject42.Combat.OrbitalStation;

public class CameraFollow : MonoBehaviour
{
    public Transform target;
    public Vector3 offset = new Vector3(0, 0, -10);
    public float smoothSpeed = 0.125f;

    [Header("Temporary Focus")]
    [SerializeField] private Camera controlledCamera;

    [Header("Mouse Wheel Zoom")]
    [Tooltip("Orthographic size limits at the authored camera size. With adaptive limits, these scale with ORBITAL auto framing.")]
    [SerializeField, Min(.01f)] private float minZoom = 5.6f;
    [SerializeField, Min(.01f)] private float maxZoom = 10.5f;
    [SerializeField, Min(0f)] private float wheelSensitivity = .7f;
    [SerializeField, Min(.01f)] private float zoomSmoothTime = .18f;
    [SerializeField] private bool scaleZoomLimitsWithAutoFraming = true;
    [Tooltip("Maximum visible world width; zero disables. Keeps the bunker authored overview on wide displays.")]
    [SerializeField, Min(0f)] private float maxViewWidth;

    private float userZoomOffset;
    private float smoothedUserZoomOffset;
    private float userZoomVelocity;
    private float pendingWheel;
    private int orbitalRunId;
    private readonly List<RaycastResult> scrollRaycasts = new();
    private PointerEventData scrollPointer;
    private EventSystem scrollEventSystem;

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
    private OrbitalStationRuntime orbitalStation;
    private Transform orbitalTarget;
    private float framedSize;
    private float framingVelocity;
    public float FramedOrthographicSize => framedSize > 0f ? framedSize : normalOrthographicSize;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
    private Vector3 mouseLookAheadOffset;
    private Vector3 appliedMouseLookAheadOffset;
    public Vector3 DebugMouseLookAheadOffset => mouseLookAheadOffset;
#endif

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
            ApplyFocusZoom();
        }
    }

    private void LateUpdate()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        RemoveMouseLookAheadLayer();
#endif
        if (hasWorldBoundsFocus)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            mouseLookAheadOffset = Vector3.zero;
#endif
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

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        UpdateMouseLookAheadLayer();
#endif

        pendingWheel = Input.mouseScrollDelta.y;
        ApplyFocusZoom();
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    private void UpdateMouseLookAheadLayer()
    {
        ResolveCamera();
        bool enabled = PhysicalCombatFeedbackRuntime.GetLabValue(
            CombatFeelParameter.MouseLookAhead) >= .5f;
        // FEEL LAB is a live overlay: opening it must not disable camera
        // presentation. UI raycasts block clicks on the panel independently.
        bool neutralize = !enabled || target == null || controlledCamera == null;
        Vector3 targetOffset = Vector3.zero;
        if (!neutralize)
        {
            Vector2 screenSignal = EvaluateMouseLookAheadSignal(
                Input.mousePosition,
                new Vector2(Screen.width, Screen.height),
                PhysicalCombatFeedbackRuntime.GetLabValue(
                    CombatFeelParameter.LookAheadDeadZone),
                PhysicalCombatFeedbackRuntime.GetLabValue(
                    CombatFeelParameter.MaxScreenFraction),
                PhysicalCombatFeedbackRuntime.GetLabValue(
                    CombatFeelParameter.LookAheadCurve));
            float distance = PhysicalCombatFeedbackRuntime.GetLabValue(
                CombatFeelParameter.LookAheadDistance);
            float horizontal = PhysicalCombatFeedbackRuntime.GetLabValue(
                CombatFeelParameter.HorizontalStrength);
            float vertical = PhysicalCombatFeedbackRuntime.GetLabValue(
                CombatFeelParameter.VerticalStrength);
            targetOffset = controlledCamera.transform.right *
                    (screenSignal.x * distance * horizontal) +
                controlledCamera.transform.up *
                    (screenSignal.y * distance * vertical);
            targetOffset.z = 0f;
        }

        bool returning = targetOffset.sqrMagnitude < mouseLookAheadOffset.sqrMagnitude &&
            Vector3.Dot(targetOffset, mouseLookAheadOffset) >= 0f;
        float speed = PhysicalCombatFeedbackRuntime.GetLabValue(returning || neutralize
            ? CombatFeelParameter.LookAheadReturn
            : CombatFeelParameter.LookAheadResponse);
        float deltaTime = Time.unscaledDeltaTime;
        float blend = 1f - Mathf.Exp(-Mathf.Max(.01f, speed) * deltaTime);
        mouseLookAheadOffset += (targetOffset - mouseLookAheadOffset) * blend;
        if ((targetOffset - mouseLookAheadOffset).sqrMagnitude < .000001f)
            mouseLookAheadOffset = targetOffset;
        appliedMouseLookAheadOffset = mouseLookAheadOffset;
        transform.position += appliedMouseLookAheadOffset;
    }

    private void RemoveMouseLookAheadLayer()
    {
        if (appliedMouseLookAheadOffset == Vector3.zero) return;
        transform.position -= appliedMouseLookAheadOffset;
        appliedMouseLookAheadOffset = Vector3.zero;
    }

    public static Vector2 EvaluateMouseLookAheadSignal(
        Vector2 mousePosition, Vector2 screenSize, float deadZoneFraction,
        float maxScreenFraction, float exponent)
    {
        if (screenSize.x <= 1f || screenSize.y <= 1f) return Vector2.zero;
        Vector2 center = screenSize * .5f;
        Vector2 delta = mousePosition - center;
        float distance = delta.magnitude;
        float halfShortSide = Mathf.Min(screenSize.x, screenSize.y) * .5f;
        float deadPixels = Mathf.Clamp01(deadZoneFraction) * halfShortSide;
        if (distance <= deadPixels || distance <= .001f) return Vector2.zero;
        float saturationPixels = Mathf.Max(deadPixels + 1f,
            Mathf.Clamp(maxScreenFraction, .01f, 1f) * halfShortSide);
        float linear = Mathf.Clamp01((distance - deadPixels) /
            (saturationPixels - deadPixels));
        float shaped = Mathf.Pow(linear, Mathf.Clamp(exponent, .1f, 8f));
        return delta / distance * shaped;
    }
#endif

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
        hasFocusSession = true;

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
            }
            ApplyOrbitalFraming(normalOrthographicSize);
            return;
        }

        float focusedSize = normalOrthographicSize * focusZoomMultiplier;
        ApplyOrbitalFraming(Mathf.Lerp(normalOrthographicSize, focusedSize, Ease(focusBlend)));
    }

    private void ApplyOrbitalFraming(float baseSize)
    {
        if (orbitalTarget != target || orbitalStation == null)
        {
            orbitalTarget = target;
            orbitalStation = target != null ? target.GetComponentInChildren<OrbitalStationRuntime>() : null;
        }
        bool available = orbitalStation != null && orbitalStation.IsInitialized;
        if (available && orbitalRunId != orbitalStation.State.RunId)
        {
            orbitalRunId = orbitalStation.State.RunId;
            ResetUserZoom();
        }
        if (!available && framedSize <= 0f)
        {
            ApplyUserZoom(baseSize, Time.unscaledDeltaTime);
            return;
        }
        float desired = baseSize;
        if (available)
        {
            // Full orbit envelope avoids zoom breathing as modules rotate or turrets aim.
            Vector2 extents = orbitalStation.PresentationExtents + Vector2.one * .12f;
            float radius = extents.y;
            desired = Mathf.Max(desired, baseSize + Mathf.Max(0f, radius - baseSize * .25f) * .3f,
                radius / .67f); // Conservative orbit envelope leaves the visible silhouette near 60%.
            Rect pixels = controlledCamera.pixelRect;
            Rect safe = HUDManager.Instance != null
                ? HUDManager.Instance.GetOrbitalSafePixelRect(controlledCamera)
                : pixels;
            Vector3 delta = orbitalStation.transform.position - controlledCamera.transform.position;
            float dx = Vector3.Dot(delta, controlledCamera.transform.right);
            float dy = Vector3.Dot(delta, controlledCamera.transform.up);
            float left = (safe.xMin - pixels.xMin) / pixels.width;
            float right = (safe.xMax - pixels.xMin) / pixels.width;
            float bottom = (safe.yMin - pixels.yMin) / pixels.height;
            float top = (safe.yMax - pixels.yMin) / pixels.height;
            desired = Mathf.Max(desired,
                (extents.x - dx) / (2f * controlledCamera.aspect * Mathf.Max(.05f, .5f - left)),
                (extents.x + dx) / (2f * controlledCamera.aspect * Mathf.Max(.05f, right - .5f)),
                (radius - dy) / (2f * Mathf.Max(.05f, .5f - bottom)),
                (radius + dy) / (2f * Mathf.Max(.05f, top - .5f)));
        }
        if (framedSize <= 0f) framedSize = normalOrthographicSize;
        framedSize = Mathf.SmoothDamp(framedSize, desired, ref framingVelocity,
            .55f, Mathf.Infinity, Time.unscaledDeltaTime);
        ApplyUserZoom(framedSize, Time.unscaledDeltaTime);
    }

    private void ApplyUserZoom(float autoSize, float deltaTime)
    {
        float scale = scaleZoomLimitsWithAutoFraming
            ? Mathf.Max(1f, autoSize / ProductionOrthographicSize) : 1f;
        // Scripted bunker focus retains its authored magnification.
        if (!scaleZoomLimitsWithAutoFraming && hasFocusSession)
            scale *= Mathf.Lerp(1f, focusZoomMultiplier, Ease(focusBlend));
        float upper = maxZoom * scale;
        if (maxViewWidth > 0f)
            upper = Mathf.Min(upper, maxViewWidth / (2f * controlledCamera.aspect));
        float lower = Mathf.Min(minZoom * scale, upper);
        float minOffset = (lower - autoSize) / scale;
        float maxOffset = (upper - autoSize) / scale;

        float wheel = pendingWheel;
        pendingWheel = 0f;
        if (wheel != 0f && !IsPointerOverScrollHandler(Input.mousePosition))
            userZoomOffset -= wheel * wheelSensitivity;
        // Saturate the target, so reversing the wheel responds immediately at either limit.
        userZoomOffset = Mathf.Clamp(userZoomOffset, minOffset, maxOffset);
        smoothedUserZoomOffset = Mathf.SmoothDamp(smoothedUserZoomOffset, userZoomOffset,
            ref userZoomVelocity, zoomSmoothTime, Mathf.Infinity, deltaTime);
        controlledCamera.orthographicSize = Mathf.Clamp(
            autoSize + smoothedUserZoomOffset * scale, lower, upper);
    }

    private bool IsPointerOverScrollHandler(Vector2 pointerPosition)
    {
        EventSystem current = EventSystem.current;
        if (current == null) return false;
        if (scrollEventSystem != current)
        {
            scrollEventSystem = current;
            scrollPointer = new PointerEventData(current);
        }
        scrollPointer.Reset();
        scrollPointer.position = pointerPosition;
        scrollRaycasts.Clear();
        current.RaycastAll(scrollPointer, scrollRaycasts);
        // Match EventSystem dispatch: only the first hit and its parent chain receive scroll.
        return scrollRaycasts.Count > 0 &&
            ExecuteEvents.GetEventHandler<IScrollHandler>(scrollRaycasts[0].gameObject) != null;
    }

    private void ResetUserZoom()
    {
        userZoomOffset = 0f;
        smoothedUserZoomOffset = 0f;
        userZoomVelocity = 0f;
    }

    private void OnValidate()
    {
        minZoom = Mathf.Max(.01f, minZoom);
        maxZoom = Mathf.Max(minZoom, maxZoom);
        wheelSensitivity = Mathf.Max(0f, wheelSensitivity);
        zoomSmoothTime = Mathf.Max(.01f, zoomSmoothTime);
    }

    private void OnDisable()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        RemoveMouseLookAheadLayer();
        mouseLookAheadOffset = Vector3.zero;
#endif
        EndWorldBoundsFocus(worldBoundsOwner);
        ResetUserZoom();
        orbitalRunId = 0;
        framedSize = 0f;
        framingVelocity = 0f;
        orbitalStation = null;
        hasFocusSession = false;
        focusBlend = 0f;
        focusBlendTarget = 0f;
        focusOwner = null;
        focusTarget = null;
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
