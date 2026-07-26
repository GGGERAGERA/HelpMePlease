using UnityEngine;

[RequireComponent(typeof(CanvasGroup))]
public sealed class BoundaryHazardView : MonoBehaviour
{
    [SerializeField, Range(0f, 1f)] private float initialAlpha = 0.18f;
    [SerializeField, Range(0f, 1f)] private float maximumAlpha = 0.68f;
    [SerializeField, Min(0.1f)] private float fullIntensityTime = 8f;
    [SerializeField, Min(0.01f)] private float fadeSpeed = 3.5f;

    private CanvasGroup canvasGroup;
    private float targetAlpha;

    private void Awake()
    {
        canvasGroup = GetComponent<CanvasGroup>();
        canvasGroup.alpha = 0f;
        canvasGroup.blocksRaycasts = false;
        canvasGroup.interactable = false;
    }

    private void Update()
    {
        canvasGroup.alpha = Mathf.MoveTowards(
            canvasGroup.alpha,
            targetAlpha,
            fadeSpeed * Time.unscaledDeltaTime
        );
    }

    public void SetOutsideDuration(float duration)
    {
        float intensity = Mathf.Clamp01(
            duration / Mathf.Max(0.1f, fullIntensityTime)
        );
        targetAlpha = Mathf.Lerp(initialAlpha, maximumAlpha, intensity);
    }

    public void Hide()
    {
        targetAlpha = 0f;
    }
}
