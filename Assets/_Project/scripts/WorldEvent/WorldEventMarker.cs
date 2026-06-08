using TMPro;
using UnityEngine;

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
        target = newTarget;
        targetLabel = label;

        if (labelText != null)
            labelText.text = targetLabel;

        if (markerRoot != null)
            markerRoot.gameObject.SetActive(true);
    }

    public void Hide()
    {
        target = null;

        if (markerRoot != null)
            markerRoot.gameObject.SetActive(false);
    }

    private void UpdateMarker()
    {
        Vector3 screenPos = targetCamera.WorldToScreenPoint(target.position);

        bool isBehindCamera = screenPos.z < 0f;

        if (isBehindCamera)
            screenPos *= -1f;

        float clampedX = Mathf.Clamp(screenPos.x, screenPadding, Screen.width - screenPadding);
        float clampedY = Mathf.Clamp(screenPos.y, screenPadding, Screen.height - screenPadding);

        markerRoot.position = new Vector3(clampedX, clampedY, 0f);

        Vector2 screenCenter = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
        Vector2 direction = ((Vector2)screenPos - screenCenter).normalized;

        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;

        if (arrowTransform != null)
            arrowTransform.rotation = Quaternion.Euler(0f, 0f, angle - 90f);
    }
}