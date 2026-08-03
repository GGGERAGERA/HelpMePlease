using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class LevelChoicePanelView : MonoBehaviour
{
    private const int TotalRouteSectors = 10;

    [Header("Texts")]
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private TextMeshProUGUI subtitleText;

    [Header("Route Progress")]
    [SerializeField] private RunRouteProgressView routeProgressView;

    [Header("Cards")]
    [SerializeField] private LevelChoiceCardView[] cardViews;

    [Header("Confirm")]
    [SerializeField] private Button confirmButton;

    private WorldRuleData selectedRule;
    private Action<WorldRuleData> onChoiceConfirmed;

    private void Awake()
    {
        if (subtitleText != null)
            subtitleText.gameObject.SetActive(false);

        if (confirmButton == null)
            confirmButton = FindButton("NextButton");

        if (confirmButton != null)
        {
            confirmButton.onClick.RemoveListener(ConfirmSelection);
            confirmButton.onClick.AddListener(ConfirmSelection);
            confirmButton.interactable = false;
        }
        else
        {
            Debug.LogError(
                "[LevelChoicePanelView] Confirm button is not assigned and NextButton was not found.",
                this
            );
        }
    }

    private void OnDestroy()
    {
        if (confirmButton != null)
            confirmButton.onClick.RemoveListener(ConfirmSelection);
    }

    public void Show(
        IReadOnlyList<WorldRuleData> choices,
        IReadOnlyDictionary<WorldRuleData, RunSector> sectorOptions,
        int nextSectorNumber,
        Action<WorldRuleData> onChoiceSelected
    )
    {
        gameObject.SetActive(true);

        SetText(
            titleText,
            "\u0412\u042b\u0411\u0415\u0420\u0418\u0422\u0415 " +
            "\u0423\u0421\u041b\u041e\u0412\u0418\u042f " +
            "\u0421\u0415\u041a\u0422\u041e\u0420\u0410"
        );

        selectedRule = null;
        onChoiceConfirmed = onChoiceSelected;
        routeProgressView?.ShowNext(
            nextSectorNumber,
            TotalRouteSectors
        );

        if (confirmButton != null)
            confirmButton.interactable = false;

        int cardCount = cardViews != null ? cardViews.Length : 0;

        for (int i = 0; i < cardCount; i++)
        {
            LevelChoiceCardView cardView = cardViews[i];

            if (cardView == null)
                continue;

            WorldRuleData rule =
                choices != null && i < choices.Count
                    ? choices[i]
                    : null;
            RunSector sector = null;

            if (rule != null && sectorOptions != null)
                sectorOptions.TryGetValue(rule, out sector);

            cardView.SetupSectorChoice(rule, sector, SelectCard);
            cardView.SetSelected(false);
        }
    }

    public void Hide()
    {
        selectedRule = null;
        onChoiceConfirmed = null;
        routeProgressView?.Hide();

        if (confirmButton != null)
            confirmButton.interactable = false;

        gameObject.SetActive(false);
    }

    private void SelectCard(WorldRuleData rule)
    {
        if (rule == null)
            return;

        selectedRule = rule;

        int cardCount = cardViews != null ? cardViews.Length : 0;

        for (int i = 0; i < cardCount; i++)
        {
            LevelChoiceCardView cardView = cardViews[i];

            if (cardView != null)
                cardView.SetSelected(cardView.Rule == rule);
        }

        if (confirmButton != null)
            confirmButton.interactable = true;
    }

    private void ConfirmSelection()
    {
        if (selectedRule == null)
            return;

        Action<WorldRuleData> callback = onChoiceConfirmed;

        if (confirmButton != null)
            confirmButton.interactable = false;

        AudioService.Instance?.Play(AudioCueId.UIConfirm);
        callback?.Invoke(selectedRule);
    }

    private Button FindButton(string objectName)
    {
        Button[] buttons = GetComponentsInChildren<Button>(true);

        for (int i = 0; i < buttons.Length; i++)
        {
            Button button = buttons[i];

            if (button != null && button.name == objectName)
                return button;
        }

        return null;
    }

    private static void SetText(TextMeshProUGUI text, string value)
    {
        if (text != null)
            text.text = value;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (cardViews == null || cardViews.Length != 3)
        {
            Debug.LogWarning(
                "[LevelChoicePanelView] Exactly three card views should be assigned.",
                this
            );
        }
    }
#endif
}
