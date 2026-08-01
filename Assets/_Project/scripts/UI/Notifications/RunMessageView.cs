using System.Collections;
using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class RunMessageView : MonoBehaviour
{
    private const float TitlelessHorizontalPadding = 48f;
    private const float TitlelessVerticalPadding = 7f;

    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private TextMeshProUGUI descriptionText;

    private Coroutine routine;
    private Image backgroundImage;
    private RectTransform panelRect;
    private RectTransform titleRect;
    private RectTransform descriptionRect;
    private UnityEngine.Object eventPresentationOwner;
    private Action eventPresentationComplete;
    private bool eventPresentationLayoutApplied;
    private Vector2 defaultPanelAnchorMin;
    private Vector2 defaultPanelAnchorMax;
    private Vector2 defaultPanelAnchoredPosition;
    private Vector2 defaultPanelSizeDelta;
    private Vector2 defaultPanelPivot;
    private Vector2 defaultTitleAnchorMin;
    private Vector2 defaultTitleAnchorMax;
    private Vector2 defaultTitleAnchoredPosition;
    private Vector2 defaultTitleSizeDelta;
    private Vector2 defaultTitlePivot;
    private Color defaultBackgroundColor;
    private bool defaultBackgroundRaycastTarget;
    private Color defaultTitleColor;
    private Vector2 defaultDescriptionAnchorMin;
    private Vector2 defaultDescriptionAnchorMax;
    private Vector2 defaultDescriptionAnchoredPosition;
    private Vector2 defaultDescriptionSizeDelta;
    private Vector2 defaultDescriptionPivot;

    private void Awake()
    {
        if (canvasGroup == null)
            canvasGroup = GetComponent<CanvasGroup>();

        backgroundImage = GetComponent<Image>();
        panelRect = (RectTransform)transform;
        titleRect = titleText.rectTransform;
        descriptionRect = descriptionText.rectTransform;
        defaultPanelAnchorMin = panelRect.anchorMin;
        defaultPanelAnchorMax = panelRect.anchorMax;
        defaultPanelAnchoredPosition = panelRect.anchoredPosition;
        defaultPanelSizeDelta = panelRect.sizeDelta;
        defaultPanelPivot = panelRect.pivot;
        defaultTitleAnchorMin = titleRect.anchorMin;
        defaultTitleAnchorMax = titleRect.anchorMax;
        defaultTitleAnchoredPosition = titleRect.anchoredPosition;
        defaultTitleSizeDelta = titleRect.sizeDelta;
        defaultTitlePivot = titleRect.pivot;
        defaultDescriptionAnchorMin = descriptionRect.anchorMin;
        defaultDescriptionAnchorMax = descriptionRect.anchorMax;
        defaultDescriptionAnchoredPosition = descriptionRect.anchoredPosition;
        defaultDescriptionSizeDelta = descriptionRect.sizeDelta;
        defaultDescriptionPivot = descriptionRect.pivot;
        defaultBackgroundColor = backgroundImage != null
            ? backgroundImage.color
            : Color.white;
        defaultBackgroundRaycastTarget =
            backgroundImage != null && backgroundImage.raycastTarget;
        defaultTitleColor = titleText.color;

        HideInstant();
    }

    public void Show(string title, string description, float duration = 3f)
    {
        StopActiveRoutine(false);

        routine = StartCoroutine(ShowRoutine(title, description, duration));
    }

    public void ShowWorldEventStart(
        UnityEngine.Object owner,
        string title,
        Color accentColor,
        float duration,
        Action onComplete)
    {
        StopActiveRoutine(false);
        eventPresentationOwner = owner;
        eventPresentationComplete = onComplete;
        routine = StartCoroutine(WorldEventStartRoutine(
            title,
            accentColor,
            Mathf.Clamp(duration, 0.5f, 0.8f)
        ));
    }

    public void CancelWorldEventStart(UnityEngine.Object owner)
    {
        if (owner == null || eventPresentationOwner != owner)
            return;

        StopActiveRoutine(false);
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

    private IEnumerator WorldEventStartRoutine(
        string title,
        Color accentColor,
        float duration)
    {
        ApplyEventPresentationLayout(title, accentColor);
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float normalized = Mathf.Clamp01(elapsed / duration);
            float pulse = Mathf.Sin(normalized * Mathf.PI);
            canvasGroup.alpha = Mathf.SmoothStep(0f, 1f, pulse);
            yield return null;
        }

        canvasGroup.alpha = 0f;
        routine = null;
        RestoreEventPresentationLayout();
        eventPresentationOwner = null;
        Action completion = eventPresentationComplete;
        eventPresentationComplete = null;
        completion?.Invoke();
    }

    private void ApplyEventPresentationLayout(
        string title,
        Color accentColor)
    {
        eventPresentationLayoutApplied = true;
        titleText.text = title;
        descriptionText.text = string.Empty;
        titleText.color = Color.Lerp(Color.white, accentColor, 0.6f);

        panelRect.anchorMin = Vector2.zero;
        panelRect.anchorMax = Vector2.one;
        panelRect.anchoredPosition = Vector2.zero;
        panelRect.sizeDelta = Vector2.zero;
        panelRect.pivot = new Vector2(0.5f, 0.5f);

        titleRect.anchorMin = Vector2.zero;
        titleRect.anchorMax = Vector2.one;
        titleRect.anchoredPosition = Vector2.zero;
        titleRect.sizeDelta = new Vector2(-160f, -80f);
        titleRect.pivot = new Vector2(0.5f, 0.5f);

        if (backgroundImage != null)
        {
            Color pulseColor = Color.Lerp(Color.black, accentColor, 0.3f);
            pulseColor.a = 0.42f;
            backgroundImage.color = pulseColor;
            backgroundImage.raycastTarget = false;
        }
    }

    private void RestoreEventPresentationLayout()
    {
        if (!eventPresentationLayoutApplied)
            return;

        eventPresentationLayoutApplied = false;
        panelRect.anchorMin = defaultPanelAnchorMin;
        panelRect.anchorMax = defaultPanelAnchorMax;
        panelRect.anchoredPosition = defaultPanelAnchoredPosition;
        panelRect.sizeDelta = defaultPanelSizeDelta;
        panelRect.pivot = defaultPanelPivot;
        titleRect.anchorMin = defaultTitleAnchorMin;
        titleRect.anchorMax = defaultTitleAnchorMax;
        titleRect.anchoredPosition = defaultTitleAnchoredPosition;
        titleRect.sizeDelta = defaultTitleSizeDelta;
        titleRect.pivot = defaultTitlePivot;
        titleText.color = defaultTitleColor;

        if (backgroundImage != null)
        {
            backgroundImage.color = defaultBackgroundColor;
            backgroundImage.raycastTarget = defaultBackgroundRaycastTarget;
        }
    }

    private void StopActiveRoutine(bool completePresentation)
    {
        if (routine != null)
        {
            StopCoroutine(routine);
            routine = null;
        }

        Action completion = completePresentation
            ? eventPresentationComplete
            : null;
        eventPresentationOwner = null;
        eventPresentationComplete = null;
        RestoreEventPresentationLayout();
        completion?.Invoke();
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
        StopActiveRoutine(false);

        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
            canvasGroup.blocksRaycasts = false;
            canvasGroup.interactable = false;
        }
    }

    private void OnDisable()
    {
        HideInstant();
    }
}
