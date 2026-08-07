using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class CharacterSelectionUI : MonoBehaviour
{
    [Header("Cards")]
    [SerializeField] private CharacterCardView[] cards;

    [Header("Details")]
    [SerializeField] private TextMeshProUGUI characterNameText;
    [SerializeField] private TextMeshProUGUI statusText;
    [SerializeField] private Image portraitImage;
    [SerializeField] private TextMeshProUGUI hpText;
    [SerializeField] private TextMeshProUGUI speedText;
    [SerializeField] private TextMeshProUGUI specialText;
    [SerializeField] private TextMeshProUGUI descriptionText;

    [Header("Buttons")]
    [SerializeField] private Button selectButton;
    [SerializeField] private Button backButton;

    [Header("Navigation")]
    [SerializeField] private BunkerPanelManager panelManager;

    [Header("Button Visual")]
    [SerializeField] private Color enabledColor = Color.white;
    [SerializeField] private Color disabledColor = new(0.45f, 0.45f, 0.45f, 1f);

    private CharacterData selectedCharacter;
    private CharacterStationEmbeddedView stationView;
    private RectTransform detailPanel;

    private void Awake()
    {
        if (selectButton != null)
            selectButton.onClick.AddListener(ConfirmSelection);

        if (backButton != null)
            backButton.onClick.AddListener(Close);
        BindCards();
        ApplyTwoColumnLayout();
        ConfigureEmbeddedStationView();
        ClearSelection();
    }

    private void OnEnable()
    {
        SubscribeToProgression();
        ApplyTwoColumnLayout();
        stationView?.Refresh();
        ClearSelection();

        if (cards == null)
            return;

        foreach (CharacterCardView card in cards)
        {
            if (card != null)
                card.Refresh();
        }
    }

    private void OnDestroy()
    {
        UnsubscribeFromProgression();
        if (selectButton != null)
            selectButton.onClick.RemoveListener(ConfirmSelection);

        if (backButton != null)
            backButton.onClick.RemoveListener(Close);

        if (cards != null)
        {
            foreach (CharacterCardView card in cards)
            {
                if (card != null)
                    card.Clicked -= SelectCharacter;
            }
        }
    }

    private void OnDisable()
    {
        UnsubscribeFromProgression();
    }

    private void SubscribeToProgression()
    {
        if (BunkerStationProgressionService.Instance == null)
            return;

        BunkerStationProgressionService.Instance.StationLevelChanged -= HandleStationLevelChanged;
        BunkerStationProgressionService.Instance.StationLevelChanged += HandleStationLevelChanged;
    }

    private void UnsubscribeFromProgression()
    {
        if (BunkerStationProgressionService.Instance != null)
            BunkerStationProgressionService.Instance.StationLevelChanged -= HandleStationLevelChanged;
    }

    private void HandleStationLevelChanged(BunkerStationId stationId, int level)
    {
        if (stationId != BunkerStationId.Character)
            return;

        if (cards != null)
        {
            foreach (CharacterCardView card in cards)
                card?.Refresh();
        }

        if (selectedCharacter != null)
        {
            RefreshDetails(selectedCharacter);
            SetSelectButton(IsCharacterUnlocked(selectedCharacter));
        }
    }

    public void SelectCharacter(CharacterData character)
    {
        if (character == null)
            return;

        selectedCharacter = character;

        RefreshDetails(character);
        RefreshCards(character);

        bool isUnlocked = IsCharacterUnlocked(character);
        SetSelectButton(isUnlocked);
    }

    private void ConfirmSelection()
    {
        if (selectedCharacter == null)
            return;

        if (RunSelectionManager.Instance == null)
        {
            Debug.LogError("[CharacterSelectionUI] RunSelectionManager is missing.");
            return;
        }

        RunSelectionManager.Instance.SelectCharacter(selectedCharacter);
        AudioService.Instance?.Play(AudioCueId.UIConfirm);
        Close(false);
    }

    private void Close()
    {
        Close(true);
    }

    private void Close(bool playSound)
    {
        if (panelManager != null)
            panelManager.CloseAll(playSound);
    }

    private void RefreshDetails(CharacterData character)
    {
        bool isUnlocked = IsCharacterUnlocked(character);

        SetText(characterNameText, character.characterName);

        if (!isUnlocked && character.unlockData != null)
        {
            SetText(statusText, "LOCKED");
            SetText(hpText, string.Empty);
            SetText(speedText, string.Empty);
            SetText(specialText, string.Empty);
            SetText(descriptionText, character.unlockData.lockedDescription);

            SetPortrait(character.portrait, Color.gray);
            return;
        }

        SetText(statusText, "CHARACTERISTICS");
        SetText(hpText, $"HP      {character.maxHealth}");
        SetText(speedText, $"SPEED   {character.moveSpeed:0.##}");
        SetText(specialText, $"SPECIAL {GetSpecialText(character.specialDescription)}");
        SetText(descriptionText, $"DESCRIPTION\n{character.description}");

        SetPortrait(character.portrait, Color.white);
    }

    private void RefreshCards(CharacterData character)
    {
        if (cards == null)
            return;

        foreach (CharacterCardView card in cards)
        {
            if (card == null)
                continue;

            card.SetSelected(card.Character == character);
        }
    }

    private void ClearSelection()
    {
        selectedCharacter = null;

        SetText(characterNameText, "SELECT CHARACTER");
        SetText(statusText, string.Empty);
        SetText(hpText, "HP: -");
        SetText(speedText, "Speed: -");
        SetText(specialText, "Special: -");
        SetText(descriptionText, "DESCRIPTION\nChoose a survivor.");

        SetPortrait(null, Color.white);
        RefreshCards(null);
        SetSelectButton(false);
    }

    private void SetPortrait(Sprite portrait, Color color)
    {
        if (portraitImage == null)
            return;

        // The selected portrait is already the dominant visual on the card.
        portraitImage.enabled = false;
    }

    private void SetSelectButton(bool active)
    {
        if (selectButton == null)
            return;

        selectButton.interactable = active;
    }

    private void SetText(TextMeshProUGUI text, string value)
    {
        if (text != null)
            text.text = value;
    }

    private string GetSpecialText(string special)
    {
        return string.IsNullOrWhiteSpace(special) ||
               string.Equals(special.Trim(), "No", System.StringComparison.OrdinalIgnoreCase) ||
               string.Equals(special.Trim(), "No special ability yet.", System.StringComparison.OrdinalIgnoreCase)
            ? "-"
            : special;
    }
    private bool IsCharacterUnlocked(CharacterData character)
    {
        if (character == null || character.unlockData == null)
            return true;

        if (UnlockProgressService.Instance == null)
            return character.unlockData.unlockedByDefault;

        return UnlockProgressService.Instance.IsUnlocked(character.unlockData);
    }
    private void BindCards()
    {
        if (cards == null)
            return;

        foreach (CharacterCardView card in cards)
        {
            if (card == null)
                continue;

            card.Clicked -= SelectCharacter;
            card.Clicked += SelectCharacter;
            card.Refresh();
        }
    }

    private void ConfigureEmbeddedStationView()
    {
        stationView = GetComponent<CharacterStationEmbeddedView>();
        if (stationView == null)
            stationView = gameObject.AddComponent<CharacterStationEmbeddedView>();

        stationView.Configure(characterNameText != null ? characterNameText.font : null, detailPanel);
    }

    private void ApplyTwoColumnLayout()
    {
        if (cards != null && cards.Length > 0 && cards[0] != null)
        {
            Transform gridTransform = cards[0].transform.parent;
            GridLayoutGroup grid = gridTransform != null ? gridTransform.GetComponent<GridLayoutGroup>() : null;
            RectTransform cardsArea = gridTransform != null ? gridTransform.parent as RectTransform : null;
            if (cardsArea != null)
            {
                cardsArea.anchorMin = new Vector2(0.025f, 0.14f);
                cardsArea.anchorMax = new Vector2(0.615f, 0.84f);
                cardsArea.pivot = new Vector2(0.5f, 0.5f);
                cardsArea.anchoredPosition = Vector2.zero;
                cardsArea.sizeDelta = Vector2.zero;

                DisableOwnFrame(cardsArea.gameObject);
                Transform cardsAreaOutline = FindDescendant(cardsArea, "CardsAreaPanelOutline");
                if (cardsAreaOutline != null)
                    cardsAreaOutline.gameObject.SetActive(false);

                if (grid != null)
                {
                    grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
                    grid.constraintCount = 3;
                    grid.spacing = new Vector2(24f, 20f);
                    grid.padding = new RectOffset(8, 8, 18, 8);

                    float width = ((RectTransform)transform).rect.width * 0.59f;
                    float availableWidth = Mathf.Max(900f, width - grid.padding.horizontal);
                    float cardWidth = (availableWidth - grid.spacing.x * 2f) / 3f;
                    float cardHeight = Mathf.Min(cardWidth / 0.75f, 480f);
                    grid.cellSize = new Vector2(cardWidth, cardHeight);
                    grid.childAlignment = TextAnchor.UpperCenter;
                }
            }
        }

        detailPanel = FindAncestorRect(portraitImage != null ? portraitImage.transform : null, "DetailPanel");
        if (detailPanel != null)
        {
            detailPanel.anchorMin = new Vector2(0.635f, 0.14f);
            detailPanel.anchorMax = new Vector2(0.975f, 0.84f);
            detailPanel.pivot = new Vector2(0.5f, 0.5f);
            detailPanel.anchoredPosition = Vector2.zero;
            detailPanel.sizeDelta = Vector2.zero;

            ReparentInfoText(characterNameText, detailPanel, 28f, 42f, 30f, Color.white, FontStyles.Bold);
            ReparentInfoText(statusText, detailPanel, 78f, 30f, 19f, new Color(0.2f, 0.78f, 0.82f), FontStyles.Bold);
            ReparentInfoText(hpText, detailPanel, 118f, 28f, 18f, Color.white, FontStyles.Normal);
            ReparentInfoText(speedText, detailPanel, 150f, 28f, 18f, Color.white, FontStyles.Normal);
            ReparentInfoText(specialText, detailPanel, 182f, 38f, 18f, Color.white, FontStyles.Normal);
            ReparentInfoText(descriptionText, detailPanel, 232f, 142f, 17f, new Color(0.82f, 0.86f, 0.88f), FontStyles.Normal);
            descriptionText.textWrappingMode = TextWrappingModes.Normal;
            descriptionText.overflowMode = TextOverflowModes.Ellipsis;
            descriptionText.maxVisibleLines = 5;

            Transform portraitFrame = FindDescendant(detailPanel, "PortraitImageOutline");
            if (portraitFrame != null)
                portraitFrame.gameObject.SetActive(false);

            Transform oldInfoContainer = FindDescendant(detailPanel, "LeftInfo");
            if (oldInfoContainer != null)
                oldInfoContainer.gameObject.SetActive(false);

            Transform oldDescriptionPanel = FindDescendant(detailPanel, "DescriptionPanel");
            if (oldDescriptionPanel != null)
                oldDescriptionPanel.gameObject.SetActive(false);
        }
    }

    private void OnRectTransformDimensionsChange()
    {
        if (isActiveAndEnabled && cards != null)
            ApplyTwoColumnLayout();
    }

    private static void ReparentInfoText(
        TextMeshProUGUI text,
        RectTransform parent,
        float top,
        float height,
        float fontSize,
        Color color,
        FontStyles style)
    {
        if (text == null)
            return;

        text.transform.SetParent(parent, false);
        RectTransform rect = text.rectTransform;
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = new Vector2(0f, -top);
        rect.sizeDelta = new Vector2(-56f, height);
        text.fontSize = fontSize;
        text.fontStyle = style;
        text.color = color;
        text.alignment = TextAlignmentOptions.MidlineLeft;
        text.raycastTarget = false;
    }

    private static void DisableOwnFrame(GameObject target)
    {
        foreach (Graphic graphic in target.GetComponents<Graphic>())
            graphic.enabled = false;
        foreach (Shadow effect in target.GetComponents<Shadow>())
            effect.enabled = false;
    }

    private static Transform FindDescendant(Transform root, string objectName)
    {
        if (root == null)
            return null;

        foreach (Transform child in root)
        {
            if (child.name == objectName)
                return child;

            Transform nested = FindDescendant(child, objectName);
            if (nested != null)
                return nested;
        }

        return null;
    }

    private static RectTransform FindAncestorRect(Transform start, string objectName)
    {
        Transform current = start;
        while (current != null)
        {
            if (current.name == objectName)
                return current as RectTransform;
            current = current.parent;
        }

        return null;
    }

}
