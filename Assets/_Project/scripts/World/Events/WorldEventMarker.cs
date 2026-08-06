using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class WorldEventMarker : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private RectTransform markerRoot;
    [SerializeField] private RectTransform arrowTransform;
    [SerializeField] private TextMeshProUGUI labelText;

    [Header("Settings")]
    [SerializeField] private Camera targetCamera;
    [SerializeField] private float screenPadding = 80f;

    private Transform target;
    private string targetLabel;
    private bool showOnlyOffscreen;
    private bool suppressed;
    private bool pulseArrow;
    private float arrowPulseTime;
    private Vector3 arrowBaseScale = Vector3.one;

    private void Awake()
    {
        if (targetCamera == null)
            targetCamera = Camera.main;

        Hide();
    }

    private void Update()
    {
        if (target == null || targetCamera == null)
        {
            Hide();
            return;
        }

        UpdateMarker();
    }

    public void Show(Transform newTarget, string label)
    {
        if (!gameObject.activeSelf)
            gameObject.SetActive(true);

        target = newTarget;
        targetLabel = label;

        if (labelText != null)
            labelText.text = targetLabel;

        SetMarkerVisible(!suppressed);
    }

    public void SetShowOnlyOffscreen(bool value)
    {
        showOnlyOffscreen = value;

        if (target != null && targetCamera != null)
            UpdateMarker();
    }

    public void ConfigureAsCarrierIndicator()
    {
        showOnlyOffscreen = true;
        pulseArrow = true;
        arrowPulseTime = 0f;

        if (labelText != null)
        {
            labelText.raycastTarget = false;
            labelText.gameObject.SetActive(false);
        }

        if (arrowTransform != null)
        {
            arrowBaseScale = arrowTransform.localScale * 0.9f;
            Image arrowImage = arrowTransform.GetComponent<Image>();

            if (arrowImage != null)
            {
                arrowImage.color = new Color(0.1f, 1f, 1f, 0.92f);
                arrowImage.raycastTarget = false;
                Outline glow = arrowTransform.GetComponent<Outline>();

                if (glow == null)
                    glow = arrowTransform.gameObject.AddComponent<Outline>();

                glow.effectColor = new Color(1f, 0.9f, 0.1f, 0.55f);
                glow.effectDistance = new Vector2(2f, -2f);
                glow.useGraphicAlpha = true;
            }
        }

        if (target != null && targetCamera != null)
            UpdateMarker();
    }

    public void SetSuppressed(bool value)
    {
        suppressed = value;

        if (value)
            SetMarkerVisible(false);
        else if (target != null && targetCamera != null)
            UpdateMarker();
    }

    public void Hide()
    {
        target = null;

        SetMarkerVisible(false);
    }

    private void UpdateMarker()
    {
        if (suppressed)
        {
            SetMarkerVisible(false);
            return;
        }

        if (!showOnlyOffscreen)
        {
            UpdateDefaultMarker();
            return;
        }

        if (Time.timeScale == 0f)
        {
            SetMarkerVisible(false);
            return;
        }

        Vector3 viewportPos =
            targetCamera.WorldToViewportPoint(target.position);
        bool isBehindCamera = viewportPos.z < 0f;
        bool isInsideViewport = !isBehindCamera &&
            viewportPos.x >= 0f && viewportPos.x <= 1f &&
            viewportPos.y >= 0f && viewportPos.y <= 1f;

        if (showOnlyOffscreen && isInsideViewport)
        {
            SetMarkerVisible(false);
            return;
        }

        SetMarkerVisible(true);
        Vector2 screenCenter = new(
            Screen.width * 0.5f,
            Screen.height * 0.5f
        );
        Vector2 screenPos = new(
            viewportPos.x * Screen.width,
            viewportPos.y * Screen.height
        );
        Vector2 direction = screenPos - screenCenter;

        if (isBehindCamera)
            direction = -direction;

        if (direction.sqrMagnitude < 0.0001f)
            direction = Vector2.up;

        Vector2 safeHalfSize = new(
            Mathf.Max(1f, screenCenter.x - screenPadding),
            Mathf.Max(1f, screenCenter.y - screenPadding)
        );
        float xIntersection = Mathf.Abs(direction.x) > 0.0001f
            ? safeHalfSize.x / Mathf.Abs(direction.x)
            : float.PositiveInfinity;
        float yIntersection = Mathf.Abs(direction.y) > 0.0001f
            ? safeHalfSize.y / Mathf.Abs(direction.y)
            : float.PositiveInfinity;
        Vector2 markerPosition = screenCenter + direction * Mathf.Min(
            xIntersection,
            yIntersection
        );

        markerRoot.position = new Vector3(
            markerPosition.x,
            markerPosition.y,
            0f
        );

        float angle = Mathf.Atan2(direction.y, direction.x) *
            Mathf.Rad2Deg;

        if (arrowTransform != null)
        {
            arrowTransform.rotation = Quaternion.Euler(0f, 0f, angle - 90f);

            if (pulseArrow)
            {
                arrowPulseTime += Time.unscaledDeltaTime;
                float pulse = 1f + Mathf.Sin(arrowPulseTime * 4.5f) *
                    0.06f;
                arrowTransform.localScale = arrowBaseScale * pulse;
            }
        }
    }

    private void UpdateDefaultMarker()
    {
        SetMarkerVisible(true);
        Vector3 screenPos = targetCamera.WorldToScreenPoint(target.position);

        if (screenPos.z < 0f)
            screenPos *= -1f;

        float clampedX = Mathf.Clamp(
            screenPos.x,
            screenPadding,
            Screen.width - screenPadding
        );
        float clampedY = Mathf.Clamp(
            screenPos.y,
            screenPadding,
            Screen.height - screenPadding
        );
        markerRoot.position = new Vector3(clampedX, clampedY, 0f);

        Vector2 screenCenter = new(
            Screen.width * 0.5f,
            Screen.height * 0.5f
        );
        Vector2 direction = ((Vector2)screenPos - screenCenter).normalized;
        float angle = Mathf.Atan2(direction.y, direction.x) *
            Mathf.Rad2Deg;

        if (arrowTransform != null)
            arrowTransform.rotation = Quaternion.Euler(0f, 0f, angle - 90f);
    }

    private void SetMarkerVisible(bool visible)
    {
        if (markerRoot != null && markerRoot.gameObject.activeSelf != visible)
            markerRoot.gameObject.SetActive(visible);
    }
}
