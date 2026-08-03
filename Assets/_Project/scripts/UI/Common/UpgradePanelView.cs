using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UpgradePanelView : MonoBehaviour
{
    [Header("Texts")]
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private TextMeshProUGUI subtitleText;

    [Header("Cards")]
    [SerializeField] private UpgradeCardView[] cardViews;

    [Header("World Event Choice")]
    [SerializeField] private Sprite standardChoiceIcon;
    [SerializeField] private Sprite riskChoiceIcon;

    [Header("Animation")]
    [SerializeField] private UpgradePanelAnimation panelAnimation;

    [Header("World Event Reward")]
    [SerializeField, Min(0f)] private float rewardCardInitialDelay = 0.16f;
    [SerializeField, Min(0f)] private float rewardCardRevealDuration = 0.16f;
    [SerializeField, Min(0f)] private float rewardCardDelay = 0.06f;
    [SerializeField, Range(0.1f, 1f)]
    private float rewardCardStartScale = 0.92f;

    private RectTransform cardsRoot;
    private RectTransform panelContent;
    private HorizontalLayoutGroup cardsLayout;
    private Vector2 defaultCardsRootSize;
    private Vector2 defaultPanelContentSize;
    private float defaultCardsSpacing;
    private readonly List<Vector2> defaultCardSizes = new();
    private readonly List<CanvasGroup> rewardCardGroups = new();
    private bool layoutDefaultsCaptured;
    private Coroutine rewardRevealRoutine;
    private bool textDefaultsCaptured;
    private bool defaultTitleAutoSizing;
    private bool defaultSubtitleAutoSizing;
    private float defaultTitleFontSizeMin;
    private float defaultTitleFontSizeMax;
    private float defaultSubtitleFontSizeMin;
    private float defaultSubtitleFontSizeMax;
    private float defaultTitleFontSize;
    private float defaultSubtitleFontSize;

    private void Awake()
    {
        if (panelAnimation == null)
            panelAnimation = GetComponent<UpgradePanelAnimation>();

        CaptureLayoutDefaults();
        CaptureTextDefaults();
    }

    public void Show(int level, IReadOnlyList<UpgradeData> upgrades, Action<UpgradeData> onUpgradeSelected)
    {
        Show(
            $"УРОВЕНЬ {level}",
            "Выберите улучшение",
            upgrades,
            onUpgradeSelected
        );
    }

    public void Show(
        string title,
        string subtitle,
        IReadOnlyList<UpgradeData> upgrades,
        Action<UpgradeData> onUpgradeSelected
    )
    {
        StopRewardReveal();
        RestoreDefaultLayout();
        RestoreDefaultTextLayout();
        gameObject.SetActive(true);
        transform.SetAsLastSibling();

        SetText(titleText, title);
        SetText(subtitleText, subtitle);

        for (int i = 0; i < cardViews.Length; i++)
        {
            if (cardViews[i] == null)
                continue;

            if (upgrades != null && i < upgrades.Count)
                cardViews[i].Setup(upgrades[i], onUpgradeSelected);
            else
                cardViews[i].gameObject.SetActive(false);
        }

        RebuildCardsLayout();
        panelAnimation?.PlayShow();
    }

    public void ShowWorldEventReward(
        string title,
        string subtitle,
        IReadOnlyList<UpgradeData> upgrades,
        Action<UpgradeData> onUpgradeSelected
    )
    {
        StopRewardReveal();
        RestoreDefaultLayout();
        RestoreDefaultTextLayout();
        gameObject.SetActive(true);
        transform.SetAsLastSibling();

        SetText(titleText, title);
        SetText(subtitleText, subtitle);
        SetupUpgradeCards(upgrades, onUpgradeSelected);
        PrepareRewardCards();
        RebuildCardsLayout();
        panelAnimation?.PlayShow();
        rewardRevealRoutine = StartCoroutine(RevealRewardCards());
    }

    public void Hide()
    {
        StopRewardReveal();
        gameObject.SetActive(false);
    }

    public void ShowWorldEventModeChoices(
        string eventDisplayName,
        string eventDescription,
        Action onStandardSelected,
        Action onRiskSelected
    )
    {
        StopRewardReveal();
        RestoreDefaultLayout();
        gameObject.SetActive(true);
        transform.SetAsLastSibling();

        ApplyWorldEventChoiceTextLayout();
        SetText(titleText, eventDisplayName);
        SetText(subtitleText, eventDescription);

        if (cardViews != null && cardViews.Length > 0 && cardViews[0] != null)
        {
            cardViews[0].SetupChoice(
                "STANDARD",
                "Обычная сложность\n3 варианта награды",
                UpgradeCategory.Numeric,
                standardChoiceIcon,
                new Color(0.16f, 0.42f, 0.72f, 1f),
                onStandardSelected
            );
        }

        if (cardViews != null && cardViews.Length > 1 && cardViews[1] != null)
        {
            cardViews[1].SetupChoice(
                "RISK",
                "Повышенная сложность\nУлучшенная награда\nПоражение — без награды",
                UpgradeCategory.Behavior,
                riskChoiceIcon,
                new Color(0.82f, 0.25f, 0.12f, 1f),
                onRiskSelected
            );
        }

        for (int i = 2; cardViews != null && i < cardViews.Length; i++)
        {
            if (cardViews[i] != null)
                cardViews[i].gameObject.SetActive(false);
        }

        ApplyWorldEventChoiceLayout();
        RebuildCardsLayout();
        panelAnimation?.PlayShow();
    }

    public void ClearWorldEventModeChoices()
    {
        if (cardViews != null)
        {
            for (int i = 0; i < cardViews.Length; i++)
                cardViews[i]?.ClearChoiceCallback();
        }

        Hide();
        RestoreDefaultLayout();
        RestoreDefaultTextLayout();
    }

    private void CaptureLayoutDefaults()
    {
        if (layoutDefaultsCaptured || cardViews == null ||
            cardViews.Length == 0 || cardViews[0] == null)
        {
            return;
        }

        RectTransform firstCard = cardViews[0].transform as RectTransform;
        cardsRoot = firstCard != null
            ? firstCard.parent as RectTransform
            : null;
        panelContent = cardsRoot != null
            ? cardsRoot.parent as RectTransform
            : null;
        cardsLayout = cardsRoot != null
            ? cardsRoot.GetComponent<HorizontalLayoutGroup>()
            : null;

        if (cardsRoot == null || panelContent == null || cardsLayout == null)
            return;

        defaultCardsRootSize = cardsRoot.sizeDelta;
        defaultPanelContentSize = panelContent.sizeDelta;
        defaultCardsSpacing = cardsLayout.spacing;
        defaultCardSizes.Clear();

        for (int i = 0; i < cardViews.Length; i++)
        {
            RectTransform card = cardViews[i] != null
                ? cardViews[i].transform as RectTransform
                : null;
            defaultCardSizes.Add(card != null ? card.sizeDelta : Vector2.zero);
        }

        layoutDefaultsCaptured = true;
    }

    private void ApplyWorldEventChoiceLayout()
    {
        CaptureLayoutDefaults();

        if (!layoutDefaultsCaptured)
            return;

        panelContent.sizeDelta = new Vector2(
            860f,
            defaultPanelContentSize.y
        );
        cardsRoot.sizeDelta = new Vector2(780f, defaultCardsRootSize.y);
        cardsLayout.spacing = 40f;

        for (int i = 0; i < 2 && i < cardViews.Length; i++)
        {
            RectTransform card = cardViews[i] != null
                ? cardViews[i].transform as RectTransform
                : null;

            if (card != null)
                card.sizeDelta = new Vector2(350f, defaultCardSizes[i].y);
        }
    }

    private void RestoreDefaultLayout()
    {
        CaptureLayoutDefaults();

        if (!layoutDefaultsCaptured)
            return;

        cardsRoot.sizeDelta = defaultCardsRootSize;
        panelContent.sizeDelta = defaultPanelContentSize;
        cardsLayout.spacing = defaultCardsSpacing;

        for (int i = 0; i < cardViews.Length; i++)
        {
            RectTransform card = cardViews[i] != null
                ? cardViews[i].transform as RectTransform
                : null;

            if (card != null)
                card.sizeDelta = defaultCardSizes[i];
        }
    }

    private void CaptureTextDefaults()
    {
        if (textDefaultsCaptured || titleText == null || subtitleText == null)
            return;

        defaultTitleAutoSizing = titleText.enableAutoSizing;
        defaultSubtitleAutoSizing = subtitleText.enableAutoSizing;
        defaultTitleFontSizeMin = titleText.fontSizeMin;
        defaultTitleFontSizeMax = titleText.fontSizeMax;
        defaultSubtitleFontSizeMin = subtitleText.fontSizeMin;
        defaultSubtitleFontSizeMax = subtitleText.fontSizeMax;
        defaultTitleFontSize = titleText.fontSize;
        defaultSubtitleFontSize = subtitleText.fontSize;
        textDefaultsCaptured = true;
    }

    private void ApplyWorldEventChoiceTextLayout()
    {
        CaptureTextDefaults();

        if (!textDefaultsCaptured)
            return;

        titleText.enableAutoSizing = true;
        titleText.fontSizeMin = 28f;
        titleText.fontSizeMax = defaultTitleFontSize;
        subtitleText.enableAutoSizing = true;
        subtitleText.fontSizeMin = 22f;
        subtitleText.fontSizeMax = defaultSubtitleFontSize;
    }

    private void RestoreDefaultTextLayout()
    {
        CaptureTextDefaults();

        if (!textDefaultsCaptured)
            return;

        titleText.enableAutoSizing = defaultTitleAutoSizing;
        titleText.fontSizeMin = defaultTitleFontSizeMin;
        titleText.fontSizeMax = defaultTitleFontSizeMax;
        titleText.fontSize = defaultTitleFontSize;
        subtitleText.enableAutoSizing = defaultSubtitleAutoSizing;
        subtitleText.fontSizeMin = defaultSubtitleFontSizeMin;
        subtitleText.fontSizeMax = defaultSubtitleFontSizeMax;
        subtitleText.fontSize = defaultSubtitleFontSize;
    }

    private void SetupUpgradeCards(
        IReadOnlyList<UpgradeData> upgrades,
        Action<UpgradeData> onUpgradeSelected)
    {
        for (int i = 0; i < cardViews.Length; i++)
        {
            if (cardViews[i] == null)
                continue;

            if (upgrades != null && i < upgrades.Count)
                cardViews[i].Setup(upgrades[i], onUpgradeSelected);
            else
                cardViews[i].gameObject.SetActive(false);
        }
    }

    private void PrepareRewardCards()
    {
        rewardCardGroups.Clear();

        for (int i = 0; i < cardViews.Length; i++)
        {
            UpgradeCardView cardView = cardViews[i];

            if (cardView == null || !cardView.gameObject.activeSelf)
                continue;

            CanvasGroup group = cardView.GetComponent<CanvasGroup>();

            if (group == null)
                group = cardView.gameObject.AddComponent<CanvasGroup>();

            group.alpha = 0f;
            group.interactable = false;
            group.blocksRaycasts = false;
            cardView.transform.localScale =
                Vector3.one * rewardCardStartScale;
            rewardCardGroups.Add(group);
        }
    }

    private IEnumerator RevealRewardCards()
    {
        yield return WaitUnscaled(rewardCardInitialDelay);

        for (int i = 0; i < rewardCardGroups.Count; i++)
        {
            CanvasGroup group = rewardCardGroups[i];

            if (group == null)
                continue;

            yield return RevealRewardCard(group);

            if (i + 1 < rewardCardGroups.Count)
                yield return WaitUnscaled(rewardCardDelay);
        }

        rewardRevealRoutine = null;
    }

    private IEnumerator RevealRewardCard(CanvasGroup group)
    {
        RectTransform card = group.transform as RectTransform;
        float duration = Mathf.Max(0f, rewardCardRevealDuration);

        if (duration <= 0f)
        {
            CompleteRewardCard(group, card);
            yield break;
        }

        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float eased = 1f - Mathf.Pow(1f - t, 3f);
            group.alpha = eased;

            if (card != null)
            {
                card.localScale = Vector3.LerpUnclamped(
                    Vector3.one * rewardCardStartScale,
                    Vector3.one,
                    eased
                );
            }

            yield return null;
        }

        CompleteRewardCard(group, card);
    }

    private IEnumerator WaitUnscaled(float duration)
    {
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }
    }

    private void CompleteRewardCard(
        CanvasGroup group,
        RectTransform card)
    {
        group.alpha = 1f;
        group.interactable = true;
        group.blocksRaycasts = true;

        if (card != null)
            card.localScale = Vector3.one;
    }

    private void StopRewardReveal()
    {
        if (rewardRevealRoutine != null)
        {
            StopCoroutine(rewardRevealRoutine);
            rewardRevealRoutine = null;
        }

        for (int i = 0; i < rewardCardGroups.Count; i++)
        {
            CanvasGroup group = rewardCardGroups[i];

            if (group == null)
                continue;

            CompleteRewardCard(group, group.transform as RectTransform);
        }

        rewardCardGroups.Clear();
    }

    private void OnDisable()
    {
        StopRewardReveal();
    }

    private void RebuildCardsLayout()
    {
        if (cardViews == null)
            return;

        for (int i = 0; i < cardViews.Length; i++)
        {
            RectTransform card = cardViews[i] != null
                ? cardViews[i].transform as RectTransform
                : null;
            RectTransform cardsRoot = card != null
                ? card.parent as RectTransform
                : null;

            if (cardsRoot == null)
                continue;

            LayoutRebuilder.ForceRebuildLayoutImmediate(cardsRoot);
            return;
        }
    }

    private void SetText(TextMeshProUGUI text, string value)
    {
        if (text != null)
            text.text = value;
    }
}
