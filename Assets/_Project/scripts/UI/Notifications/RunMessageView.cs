using System.Collections;
using TMPro;
using UnityEngine;

public sealed class RunMessageView : MonoBehaviour
{
    private const float TitlelessHorizontalPadding = 48f;
    private const float TitlelessVerticalPadding = 7f;

    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private TextMeshProUGUI descriptionText;

    private Coroutine routine;
    private RectTransform descriptionRect;
    private Vector2 defaultDescriptionAnchorMin;
    private Vector2 defaultDescriptionAnchorMax;
    private Vector2 defaultDescriptionAnchoredPosition;
    private Vector2 defaultDescriptionSizeDelta;
    private Vector2 defaultDescriptionPivot;

    private void Awake()
    {
        if (canvasGroup == null)
            canvasGroup = GetComponent<CanvasGroup>();

        descriptionRect = descriptionText.rectTransform;
        defaultDescriptionAnchorMin = descriptionRect.anchorMin;
        defaultDescriptionAnchorMax = descriptionRect.anchorMax;
        defaultDescriptionAnchoredPosition = descriptionRect.anchoredPosition;
        defaultDescriptionSizeDelta = descriptionRect.sizeDelta;
        defaultDescriptionPivot = descriptionRect.pivot;

        HideInstant();
    }

    public void Show(string title, string description, float duration = 3f)
    {
        if (routine != null)
            StopCoroutine(routine);

        routine = StartCoroutine(ShowRoutine(title, description, duration));
    }

    private IEnumerator ShowRoutine(string title, string description, float duration)
    {
        titleText.text = title;
        descriptionText.text = description;
        ApplyDescriptionLayout(string.IsNullOrWhiteSpace(title));

        yield return FadeTo(1f, 0.15f);
        yield return new WaitForSecondsRealtime(duration);
        yield return FadeTo(0f, 0.25f);
        routine = null;
    }

    private IEnumerator FadeTo(float targetAlpha, float duration)
    {
        float startAlpha = canvasGroup.alpha;
        float time = 0f;

        while (time < duration)
        {
            time += Time.unscaledDeltaTime;
            canvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, time / duration);
            yield return null;
        }

        canvasGroup.alpha = targetAlpha;
    }

    private void ApplyDescriptionLayout(bool isTitleless)
    {
        if (isTitleless)
        {
            descriptionRect.anchorMin = Vector2.zero;
            descriptionRect.anchorMax = Vector2.one;
            descriptionRect.anchoredPosition = Vector2.zero;
            descriptionRect.sizeDelta = new Vector2(
                -TitlelessHorizontalPadding * 2f,
                -TitlelessVerticalPadding * 2f
            );
            descriptionRect.pivot = new Vector2(0.5f, 0.5f);
            return;
        }

        descriptionRect.anchorMin = defaultDescriptionAnchorMin;
        descriptionRect.anchorMax = defaultDescriptionAnchorMax;
        descriptionRect.anchoredPosition = defaultDescriptionAnchoredPosition;
        descriptionRect.sizeDelta = defaultDescriptionSizeDelta;
        descriptionRect.pivot = defaultDescriptionPivot;
    }

    public void HideInstant()
    {
        if (routine != null)
        {
            StopCoroutine(routine);
            routine = null;
        }

        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
            canvasGroup.blocksRaycasts = false;
            canvasGroup.interactable = false;
        }
    }
}
