using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class UpgradePanelView : MonoBehaviour
{
    [Header("Texts")]
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private TextMeshProUGUI subtitleText;

    [Header("Cards")]
    [SerializeField] private UpgradeCardView[] cardViews;

    public void Show(int level, IReadOnlyList<UpgradeData> upgrades, Action<UpgradeData> onUpgradeSelected)
    {
        gameObject.SetActive(true);

        SetText(titleText, $"УРОВЕНЬ {level}");
        SetText(subtitleText, "Выберите улучшение");

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

    public void Hide()
    {
        gameObject.SetActive(false);
    }

    private void SetText(TextMeshProUGUI text, string value)
    {
        if (text != null)
            text.text = value;
    }
}