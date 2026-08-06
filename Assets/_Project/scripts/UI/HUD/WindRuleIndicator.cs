using UnityEngine;

[DisallowMultipleComponent]
public sealed class WindRuleIndicator : MonoBehaviour
{
    [SerializeField] private RectTransform arrowTransform;
    [SerializeField, Min(0.01f)] private float fadeDuration = 0.35f;
    [SerializeField, Min(0.1f)] private float warningBlinkSpeed = 5f;
    [SerializeField, Min(0f)] private float appliedHoldDuration = 0.2f;

    private Vector3 baseArrowScale = Vector3.one;
    private CanvasGroup canvasGroup;
    private float visibility;
    private float appliedHoldRemaining;
    private bool warning;

    private void Awake()
    {
        if (arrowTransform != null)
            baseArrowScale = arrowTransform.localScale;

        canvasGroup = GetComponent<CanvasGroup>();

        if (canvasGroup == null)
            canvasGroup = gameObject.AddComponent<CanvasGroup>();

        canvasGroup.alpha = 0f;
    }

    private void Update()
    {
        float targetVisibility = warning || appliedHoldRemaining > 0f
            ? 1f
            : 0f;

        if (appliedHoldRemaining > 0f)
        {
            appliedHoldRemaining = Mathf.Max(
                0f,
                appliedHoldRemaining - Time.unscaledDeltaTime
            );
        }

        visibility = Mathf.MoveTowards(
            visibility,
            targetVisibility,
            Time.unscaledDeltaTime / Mathf.Max(0.01f, fadeDuration)
        );

        if (canvasGroup != null)
        {
            float blink = warning
                ? Mathf.Lerp(
                    0.35f,
                    1f,
                    0.5f + 0.5f * Mathf.Sin(
                        Time.unscaledTime * warningBlinkSpeed
                    )
                )
                : 1f;
            canvasGroup.alpha = visibility * blink;
        }

        if (!warning &&
            appliedHoldRemaining <= 0f &&
            visibility <= 0f)
        {
            gameObject.SetActive(false);
        }
    }

    public void ShowWarning(Vector2 direction)
    {
        if (!PrepareDirection(direction))
            return;

        warning = true;
        appliedHoldRemaining = 0f;
    }

    public void ShowApplied(Vector2 direction)
    {
        if (!PrepareDirection(direction))
            return;

        warning = false;
        visibility = 1f;

        if (canvasGroup != null)
            canvasGroup.alpha = 1f;

        appliedHoldRemaining = appliedHoldDuration;
    }

    private bool PrepareDirection(Vector2 direction)
    {
        if (direction.sqrMagnitude <= 0.0001f)
        {
            Hide();
            return false;
        }

        if (!gameObject.activeSelf)
            gameObject.SetActive(true);

        float angle = Mathf.Atan2(direction.y, direction.x) *
            Mathf.Rad2Deg;

        if (arrowTransform != null)
        {
            arrowTransform.localRotation =
                Quaternion.Euler(0f, 0f, angle - 90f);
        }

        return true;
    }

    public void Hide()
    {
        warning = false;
        appliedHoldRemaining = 0f;
        visibility = 0f;

        if (canvasGroup != null)
            canvasGroup.alpha = 0f;

        if (arrowTransform != null)
            arrowTransform.localScale = baseArrowScale;

        if (gameObject.activeSelf)
            gameObject.SetActive(false);
    }
}
