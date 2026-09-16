using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class BunkerRunSummaryPresenter : MonoBehaviour
{
    [SerializeField, Min(0f)] private float showDelay = 0.25f;
    [SerializeField] private RectTransform notificationParent;
    [SerializeField] private CanvasScaler sourceScaler;
    [SerializeField] private RectTransform panelTemplate;
    [SerializeField] private TextMeshProUGUI goldTextTemplate;

    private RectTransform notification;
    private CanvasGroup notificationGroup;
    private GameObject notificationCanvas;
    private TextMeshProUGUI displayedTitle;
    private TextMeshProUGUI displayedGold;
    private TextMeshProUGUI displayedExtraGold;
    private RunSummary displayedSummary;
    private bool displayedEscapeUpdate;

    private void OnEnable() => LocalizationService.Instance.LanguageChanged += HandleLanguageChanged;
    private void HandleLanguageChanged(GameLanguage language)
    {
        if (notification == null) return;
        string reasonKey = displayedSummary?.EndReason switch
        {
            RunEndReason.PlayerDied => "bunker.summary_interrupted",
            RunEndReason.Victory => "bunker.summary_completed",
            _ => "bunker.summary_return"
        };
        displayedTitle.text = LocalizationService.Instance.Get(displayedEscapeUpdate ? "bunker.summary_protocol" : reasonKey);
        displayedGold.text = displayedEscapeUpdate ? LocalizationService.Instance.Get("bunker.summary_access")
            : string.Format(LocalizationService.Instance.Get("bunker.summary_gold"), displayedSummary.GoldEarned);
        if (displayedExtraGold != null)
            displayedExtraGold.text = string.Format(LocalizationService.Instance.Get("bunker.summary_gold"), displayedSummary.GoldEarned);
    }

    private const float VisibleDuration = 3f;
    private const float FadeDuration = 0.2f;

    private IEnumerator Start()
    {
        while (SceneTransitionOverlay.IsTransitioning) yield return null;
        yield return new WaitForSecondsRealtime(showDelay);

        MetaProgressionManager meta = MetaProgressionManager.EnsureExists();
        bool escapeUpdated = meta.HasEscapeUpdate;
        RunSummary summary = null;
        if (RunStateManager.Instance != null)
            RunStateManager.Instance.TryConsumeLastRunSummary(out summary);
        if (summary == null && !escapeUpdated) yield break;

        if (notificationParent == null || panelTemplate == null || goldTextTemplate == null || sourceScaler == null)
        {
            Debug.LogError("[BunkerRunSummaryPresenter] Notification UI references are missing.", this);
            yield break;
        }

        // Use the same framed header and typography as BunkerSelectionWindow's
        // StationWindow base. Do not route post-run data into the old banner.
        // The station-window canvas can be hidden by the bunker panel manager.
        // Keep the transient notification independent, using its Canvas Scaler.
        notificationCanvas = new GameObject("PostRunNotificationCanvas", typeof(RectTransform));
        notificationCanvas.transform.SetParent(transform, false);
        Canvas canvas = notificationCanvas.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 1100;
        CanvasScaler scaler = notificationCanvas.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = sourceScaler.uiScaleMode;
        scaler.referenceResolution = sourceScaler.referenceResolution;
        scaler.screenMatchMode = sourceScaler.screenMatchMode;
        scaler.matchWidthOrHeight = sourceScaler.matchWidthOrHeight;
        scaler.referencePixelsPerUnit = sourceScaler.referencePixelsPerUnit;
        notification = Instantiate(panelTemplate, notificationCanvas.transform);
        notification.name = "PostRunNotification";
        notification.gameObject.SetActive(true);
        notification.anchorMin = notification.anchorMax = new Vector2(0.5f, 1f);
        notification.pivot = new Vector2(0.5f, 1f);
        notification.anchoredPosition = new Vector2(0f, -24f);
        notification.sizeDelta = new Vector2(520f, escapeUpdated && summary != null ? 140f : 100f);
        notification.localScale = Vector3.one;
        LayoutElement layout = notification.GetComponent<LayoutElement>();
        if (layout != null) layout.ignoreLayout = true;

        notificationGroup = notification.gameObject.AddComponent<CanvasGroup>();
        notificationGroup.interactable = false;
        notificationGroup.blocksRaycasts = false;
        foreach (Graphic graphic in notification.GetComponentsInChildren<Graphic>(true))
            graphic.raycastTarget = false;

        TextMeshProUGUI title = notification.GetComponentInChildren<TextMeshProUGUI>(true);
        displayedTitle = title;
        displayedSummary = summary;
        displayedEscapeUpdate = escapeUpdated;
        string reason = summary?.EndReason switch
        {
            RunEndReason.PlayerDied => LocalizationService.Instance.Get("bunker.summary_interrupted"),
            RunEndReason.Victory => LocalizationService.Instance.Get("bunker.summary_completed"),
            _ => LocalizationService.Instance.Get("bunker.summary_return")
        };
        SetLine(title, escapeUpdated ? LocalizationService.Instance.Get("bunker.summary_protocol") : reason, escapeUpdated && summary != null ? 0.8f : 0.7f);
        TextMeshProUGUI gold = Instantiate(goldTextTemplate, notification);
        displayedGold = gold;
        gold.name = "GoldEarned";
        gold.gameObject.SetActive(true);
        SetLine(gold, escapeUpdated ? LocalizationService.Instance.Get("bunker.summary_access") : string.Format(LocalizationService.Instance.Get("bunker.summary_gold"), summary.GoldEarned),
            escapeUpdated && summary != null ? 0.5f : 0.3f);
        if (escapeUpdated)
        {
            if (summary != null)
            {
                TextMeshProUGUI earned = Instantiate(goldTextTemplate, notification);
                displayedExtraGold = earned;
                earned.gameObject.SetActive(true);
                SetLine(earned, string.Format(LocalizationService.Instance.Get("bunker.summary_gold"), summary.GoldEarned), 0.2f);
            }
            meta.AcknowledgeEscapeUpdate();
        }

        yield return new WaitForSecondsRealtime(VisibleDuration);
        float elapsed = 0f;
        while (elapsed < FadeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            notificationGroup.alpha = 1f - Mathf.Clamp01(elapsed / FadeDuration);
            yield return null;
        }
        Hide();
    }

    private static void SetLine(TextMeshProUGUI text, string content, float anchorY)
    {
        text.text = content;
        text.fontSize = 20f;
        text.enableAutoSizing = true;
        text.fontSizeMin = 16f;
        text.fontSizeMax = 20f;
        text.alignment = TextAlignmentOptions.Center;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.overflowMode = TextOverflowModes.Ellipsis;
        text.raycastTarget = false;
        RectTransform rect = text.rectTransform;
        rect.anchorMin = new Vector2(0f, anchorY);
        rect.anchorMax = new Vector2(1f, anchorY);
        rect.pivot = Vector2.one * 0.5f;
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = new Vector2(-32f, 32f);
        rect.localScale = Vector3.one;
    }

    private void Hide()
    {
        if (notification != null) Destroy(notification.gameObject);
        if (notificationCanvas != null) Destroy(notificationCanvas);
        notification = null;
        notificationCanvas = null;
    }

    private void OnDisable()
    {
        if (LocalizationService.Instance != null)
            LocalizationService.Instance.LanguageChanged -= HandleLanguageChanged;
        Hide();
    }
}
