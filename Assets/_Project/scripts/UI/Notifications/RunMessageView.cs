using System.Collections;
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
    [SerializeField, Range(1f, 120f)]
    private float typewriterCharactersPerSecond = 45f;

    private Coroutine routine;
    private Image backgroundImage;
    private RectTransform panelRect;
    private RectTransform titleRect;
    private RectTransform descriptionRect;
    private bool feedbackLayoutApplied;
    private bool startupLayoutApplied;
    private bool defaultAutoSizing;
    private float defaultFontSize, defaultFontSizeMin, defaultFontSizeMax;
    public bool IsPanelVisible => canvasGroup != null && canvasGroup.alpha > .01f && !feedbackLayoutApplied;
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

    public void Show(
        string title,
        string description,
        float duration = 3f,
        bool useTypewriter = false)
    {
        StopActiveRoutine();

        routine = StartCoroutine(ShowRoutine(
            title,
            description,
            duration,
            useTypewriter
        ));
    }

    public void ShowWorldEventFeedback(
        string title,
        string description,
        Color accentColor,
        float duration)
    {
        StopActiveRoutine();
        routine = StartCoroutine(WorldEventFeedbackRoutine(
            title,
            description,
            accentColor,
            Mathf.Clamp(duration, 0.2f, 0.8f)
        ));
    }

    public void ShowStartupHint(string description, float duration)
    {
        Show(string.Empty, description, duration, true);
        startupLayoutApplied = true;
        defaultAutoSizing = descriptionText.enableAutoSizing;
        defaultFontSize = descriptionText.fontSize;
        defaultFontSizeMin = descriptionText.fontSizeMin;
        defaultFontSizeMax = descriptionText.fontSizeMax;
        descriptionText.enableAutoSizing = true;
        descriptionText.fontSizeMin = 18f;
        descriptionText.fontSizeMax = 25f;
        LayoutStartupHint();
    }

    private void LateUpdate()
    {
        if (startupLayoutApplied) LayoutStartupHint();
    }

    private void LayoutStartupHint()
    {
        Canvas canvas = GetComponentInParent<Canvas>();
        Camera camera = Camera.main;
        if (canvas == null || camera == null || panelRect.parent is not RectTransform parent) return;
        Rect safe = HUDManager.Instance != null
            ? HUDManager.Instance.GetOrbitalSafePixelRect(camera, false) : Screen.safeArea;
        Camera uiCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(parent,
            new Vector2(safe.center.x, safe.yMax - 8f), uiCamera, out Vector2 top);
        panelRect.anchorMin = panelRect.anchorMax = parent.pivot;
        panelRect.pivot = new Vector2(.5f, 1f);
        panelRect.anchoredPosition = top;
        panelRect.sizeDelta = new Vector2(Mathf.Min(1120f, parent.rect.width * .7f), 88f);
    }

    private void RestoreStartupLayout()
    {
        if (!startupLayoutApplied) return;
        startupLayoutApplied = false;
        panelRect.anchorMin = defaultPanelAnchorMin;
        panelRect.anchorMax = defaultPanelAnchorMax;
        panelRect.anchoredPosition = defaultPanelAnchoredPosition;
        panelRect.sizeDelta = defaultPanelSizeDelta;
        panelRect.pivot = defaultPanelPivot;
        descriptionText.enableAutoSizing = defaultAutoSizing;
        descriptionText.fontSize = defaultFontSize;
        descriptionText.fontSizeMin = defaultFontSizeMin;
        descriptionText.fontSizeMax = defaultFontSizeMax;
    }

    private IEnumerator ShowRoutine(
        string title,
        string description,
        float duration,
        bool useTypewriter)
    {
        PrepareText(titleText, title, useTypewriter);
        PrepareText(descriptionText, description, useTypewriter);
        ApplyDescriptionLayout(string.IsNullOrWhiteSpace(title));

        yield return FadeTo(1f, 0.15f);
        float visiblePhaseStartedAt = Time.unscaledTime;

        if (useTypewriter)
        {
            yield return RevealText(titleText);
            yield return RevealText(descriptionText);
        }

        float remainingVisibleTime = Mathf.Max(
            0f,
            duration - (Time.unscaledTime - visiblePhaseStartedAt)
        );
        if (remainingVisibleTime > 0f)
            yield return new WaitForSecondsRealtime(remainingVisibleTime);

        yield return FadeTo(0f, 0.25f);
        RestoreStartupLayout();
        ClearDisplayedText();
        routine = null;
    }

    private IEnumerator WorldEventFeedbackRoutine(
        string title,
        string description,
        Color accentColor,
        float duration)
    {
        ApplyFeedbackLayout(title, description, accentColor);
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
        RestoreFeedbackLayout();
        ClearDisplayedText();
    }

    private void ApplyFeedbackLayout(
        string title,
        string description,
        Color accentColor)
    {
        feedbackLayoutApplied = true;
        titleText.text = title;
        descriptionText.text = description;
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

        descriptionRect.anchorMin = new Vector2(0.15f, 0.22f);
        descriptionRect.anchorMax = new Vector2(0.85f, 0.48f);
        descriptionRect.anchoredPosition = Vector2.zero;
        descriptionRect.sizeDelta = Vector2.zero;
        descriptionRect.pivot = new Vector2(0.5f, 0.5f);

        if (backgroundImage != null)
        {
            backgroundImage.color = string.IsNullOrWhiteSpace(description)
                ? Color.clear
                : new Color(
                    accentColor.r * 0.18f,
                    accentColor.g * 0.18f,
                    accentColor.b * 0.18f,
                    0.34f
                );
            backgroundImage.raycastTarget = false;
        }
    }

    private void RestoreFeedbackLayout()
    {
        if (!feedbackLayoutApplied)
            return;

        feedbackLayoutApplied = false;
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
        descriptionRect.anchorMin = defaultDescriptionAnchorMin;
        descriptionRect.anchorMax = defaultDescriptionAnchorMax;
        descriptionRect.anchoredPosition = defaultDescriptionAnchoredPosition;
        descriptionRect.sizeDelta = defaultDescriptionSizeDelta;
        descriptionRect.pivot = defaultDescriptionPivot;

        if (backgroundImage != null)
        {
            backgroundImage.color = defaultBackgroundColor;
            backgroundImage.raycastTarget = defaultBackgroundRaycastTarget;
        }
    }

    private void StopActiveRoutine()
    {
        if (routine != null)
        {
            StopCoroutine(routine);
            routine = null;
        }

        RestoreFeedbackLayout();
        canvasGroup.alpha = 0f;
        RestoreStartupLayout();
        ClearDisplayedText();
    }

    private static int PrepareText(
        TextMeshProUGUI target,
        string value,
        bool hideCharacters)
    {
        if (target == null)
            return 0;

        target.text = value ?? string.Empty;
        target.maxVisibleCharacters = hideCharacters ? 0 : int.MaxValue;
        target.ForceMeshUpdate();
        return target.textInfo.characterCount;
    }

    private IEnumerator RevealText(TextMeshProUGUI target)
    {
        if (target == null)
            yield break;

        target.ForceMeshUpdate();
        int characterCount = target.textInfo.characterCount;
        float visibleCharacters = 0f;

        while (target.maxVisibleCharacters < characterCount)
        {
            visibleCharacters +=
                Mathf.Max(1f, typewriterCharactersPerSecond) *
                Time.unscaledDeltaTime;
            target.maxVisibleCharacters = Mathf.Min(
                characterCount,
                Mathf.FloorToInt(visibleCharacters)
            );
            yield return null;
        }

        target.maxVisibleCharacters = int.MaxValue;
    }

    private void ClearDisplayedText()
    {
        if (titleText != null)
        {
            titleText.maxVisibleCharacters = int.MaxValue;
            titleText.text = string.Empty;
        }

        if (descriptionText != null)
        {
            descriptionText.maxVisibleCharacters = int.MaxValue;
            descriptionText.text = string.Empty;
        }
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
        StopActiveRoutine();

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
