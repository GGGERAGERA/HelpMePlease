using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class LevelChoicePanelView : MonoBehaviour
{
    [Header("Texts")]
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private TextMeshProUGUI subtitleText;

    [Header("Cards")]
    [SerializeField] private LevelChoiceCardView[] cardViews;

    [Header("Confirm")]
    [SerializeField] private Button confirmButton;

    private LevelNodeData selectedNode;
    private Action<LevelNodeData> onChoiceConfirmed;

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
        IReadOnlyList<LevelNodeData> choices,
        Action<LevelNodeData> onChoiceSelected
    )
    {
        gameObject.SetActive(true);

        SetText(titleText, "ВЫБЕРИТЕ СЛЕДУЮЩИЙ УРОВЕНЬ");

        selectedNode = null;
        onChoiceConfirmed = onChoiceSelected;

        if (confirmButton != null)
            confirmButton.interactable = false;

        int cardCount = cardViews != null ? cardViews.Length : 0;

        for (int i = 0; i < cardCount; i++)
        {
            LevelChoiceCardView cardView = cardViews[i];

            if (cardView == null)
                continue;

            LevelNodeData data =
                choices != null && i < choices.Count
                    ? choices[i]
                    : null;

            cardView.Setup(data, SelectCard);
            cardView.SetSelected(false);
        }
    }

    public void Hide()
    {
        selectedNode = null;
        onChoiceConfirmed = null;

        if (confirmButton != null)
            confirmButton.interactable = false;

        gameObject.SetActive(false);
    }

    private void SelectCard(LevelNodeData node)
    {
        if (node == null)
            return;

        selectedNode = node;

        int cardCount = cardViews != null ? cardViews.Length : 0;

        for (int i = 0; i < cardCount; i++)
        {
            LevelChoiceCardView cardView = cardViews[i];

            if (cardView != null)
                cardView.SetSelected(cardView.Data == node);
        }

        if (confirmButton != null)
            confirmButton.interactable = true;
    }

    private void ConfirmSelection()
    {
        if (selectedNode == null)
            return;

        Action<LevelNodeData> callback = onChoiceConfirmed;

        // Защита от повторного клика до смены сцены.
        if (confirmButton != null)
            confirmButton.interactable = false;

        callback?.Invoke(selectedNode);
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
