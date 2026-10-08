using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class BunkerRunSummaryPresenter : MonoBehaviour
{
    [SerializeField, Min(0f)] private float showDelay = 0.25f;
    [SerializeField] private RectTransform notification;
    [SerializeField] private CanvasGroup notificationGroup;
    [SerializeField] private TextMeshProUGUI displayedTitle;
    [SerializeField] private TextMeshProUGUI displayedGold;
    [SerializeField] private TextMeshProUGUI displayedExtraGold;
    private RunSummary displayedSummary;
    private bool displayedEscapeUpdate;

    private void OnEnable() => LocalizationService.Instance.LanguageChanged += HandleLanguageChanged;
    private void HandleLanguageChanged(GameLanguage language)
    {
        if (!notification.gameObject.activeSelf) return;
        string reasonKey = displayedSummary?.EndReason switch
        {
            RunEndReason.PlayerDied => "bunker.summary_interrupted",
            RunEndReason.Victory => "bunker.summary_completed",
            _ => "bunker.summary_return"
        };
        displayedTitle.text = LocalizationService.Instance.Get(displayedEscapeUpdate ? "bunker.summary_protocol" : reasonKey);
        displayedGold.text = displayedEscapeUpdate ? LocalizationService.Instance.Get("bunker.summary_access")
            : string.Format(LocalizationService.Instance.Get("bunker.summary_gold"), displayedSummary.GoldEarned);
        if (displayedExtraGold.gameObject.activeSelf)
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

        notification.gameObject.SetActive(true);
        notificationGroup.alpha = 1f;
        notification.sizeDelta = new Vector2(520f, escapeUpdated && summary != null ? 140f : 100f);
        TextMeshProUGUI title = displayedTitle;
        displayedSummary = summary;
        displayedEscapeUpdate = escapeUpdated;
        string reason = summary?.EndReason switch
        {
            RunEndReason.PlayerDied => LocalizationService.Instance.Get("bunker.summary_interrupted"),
            RunEndReason.Victory => LocalizationService.Instance.Get("bunker.summary_completed"),
            _ => LocalizationService.Instance.Get("bunker.summary_return")
        };
        SetLine(title, escapeUpdated ? LocalizationService.Instance.Get("bunker.summary_protocol") : reason, escapeUpdated && summary != null ? 0.8f : 0.7f);
        TextMeshProUGUI gold = displayedGold;
        gold.name = "GoldEarned";
        gold.gameObject.SetActive(true);
        SetLine(gold, escapeUpdated ? LocalizationService.Instance.Get("bunker.summary_access") : string.Format(LocalizationService.Instance.Get("bunker.summary_gold"), summary.GoldEarned),
            escapeUpdated && summary != null ? 0.5f : 0.3f);
        displayedExtraGold.gameObject.SetActive(escapeUpdated && summary != null);
        if (escapeUpdated)
        {
            if (summary != null)
            {
                TextMeshProUGUI earned = displayedExtraGold;
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

    private void Hide() => notification.gameObject.SetActive(false);

    private void OnDisable()
    {
        if (LocalizationService.Instance != null)
            LocalizationService.Instance.LanguageChanged -= HandleLanguageChanged;
        Hide();
    }
}
