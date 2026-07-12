using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public sealed class LevelChoicePanelView : MonoBehaviour
{
    [Header("Texts")]
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private TextMeshProUGUI subtitleText;

    [Header("Cards")]
    [SerializeField] private LevelChoiceCardView[] cardViews;

    public void Show(
        IReadOnlyList<LevelNodeData> choices,
        Action<LevelNodeData> onChoiceSelected
    )
    {
        gameObject.SetActive(true);

        SetText(titleText, "ВЫБЕРИТЕ СЛЕДУЮЩИЙ УРОВЕНЬ");
        SetText(subtitleText, "Маршрут влияет на врагов, погоду и награды.");

        for (int i = 0; i < cardViews.Length; i++)
        {
            if (cardViews[i] == null)
                continue;

            LevelNodeData data = choices != null && i < choices.Count
                ? choices[i]
                : null;

            cardViews[i].Setup(data, onChoiceSelected);
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