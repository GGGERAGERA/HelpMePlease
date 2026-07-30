using System;
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

    [Header("Animation")]
    [SerializeField] private UpgradePanelAnimation panelAnimation;

    private void Awake()
    {
        if (panelAnimation == null)
            panelAnimation = GetComponent<UpgradePanelAnimation>();
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

    public void Hide()
    {
        gameObject.SetActive(false);
    }

    public void ShowWorldEventModeChoices(
        Action onStandardSelected,
        Action onRiskSelected
    )
    {
        gameObject.SetActive(true);
        transform.SetAsLastSibling();

        SetText(titleText, "WORLD EVENT");
        SetText(subtitleText, "Выберите режим события");

        if (cardViews != null && cardViews.Length > 0 && cardViews[0] != null)
        {
            cardViews[0].SetupChoice(
                "STANDARD",
                "Обычная сложность и обычная награда.",
                UpgradeCategory.Numeric,
                onStandardSelected
            );
        }

        if (cardViews != null && cardViews.Length > 1 && cardViews[1] != null)
        {
            cardViews[1].SetupChoice(
                "RISK",
                "Повышенная сложность и улучшенная награда. Поражение оставит без награды.",
                UpgradeCategory.Behavior,
                onRiskSelected
            );
        }

        for (int i = 2; cardViews != null && i < cardViews.Length; i++)
        {
            if (cardViews[i] != null)
                cardViews[i].gameObject.SetActive(false);
        }

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
