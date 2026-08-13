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
    private BunkerStationProgressionService boundProgressionService;

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

    private void Update()
    {
        if (boundProgressionService != BunkerStationProgressionService.Instance)
            SubscribeToProgression();
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
        if (boundProgressionService != null)
            boundProgressionService.StationLevelChanged -= HandleStationLevelChanged;

        boundProgressionService = BunkerStationProgressionService.Instance;
        if (boundProgressionService != null)
            boundProgressionService.StationLevelChanged += HandleStationLevelChanged;
    }

    private void UnsubscribeFromProgression()
    {
        if (boundProgressionService != null)
            boundProgressionService.StationLevelChanged -= HandleStationLevelChanged;
        boundProgressionService = null;
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
        if (character == null || !IsCharacterUnlocked(character))
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
        SetText(characterNameText, character.characterName);
        SetText(statusText, "COMBAT TYPE");
        SetText(descriptionText, GetCharacterDetails(character));

        SetPortrait(character.portrait, Color.white);
    }

    private static string GetCharacterDetails(CharacterData character)
    {
        string combatName = string.IsNullOrWhiteSpace(
            character.combatTypeDisplayName)
            ? character.combatType.ToString()
            : character.combatTypeDisplayName;
        string combatDescription = character.combatTypeDescription ?? string.Empty;
        string characterDescription = character.description ?? string.Empty;

        return $"{combatName}\n{combatDescription}\n\n{characterDescription}";
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
        SetText(statusText, "DESCRIPTION");
        SetText(descriptionText, "Choose a survivor.");

        SetPortrait(null, Color.white);
        RefreshCards(null);
        SetSelectButton(false);
    }

    private void SetPortrait(Sprite portrait, Color color)
    {
        if (portraitImage == null)
            return;

        portraitImage.sprite = portrait;
        portraitImage.color = color;
        portraitImage.preserveAspect = true;
        portraitImage.raycastTarget = false;
        portraitImage.enabled = portrait != null;
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

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    public void CollectDebugCharacters(
        System.Collections.Generic.List<CharacterData> destination)
    {
        if (destination == null || cards == null)
            return;

        foreach (CharacterCardView card in cards)
        {
            CharacterData character = card != null ? card.Character : null;

            if (character != null && !destination.Contains(character))
                destination.Add(character);
        }
    }

    public void DebugRefresh()
    {
        stationView?.Refresh();
        if (cards != null)
        {
            foreach (CharacterCardView card in cards)
                card?.Refresh();
        }

        if (selectedCharacter != null)
        {
            if (IsCharacterUnlocked(selectedCharacter))
                RefreshDetails(selectedCharacter);
            else
                ClearSelection();
        }
    }

    public bool CanDebugSelectCharacter(CharacterData character)
    {
        return character != null && IsCharacterUnlocked(character);
    }

    public bool DebugSelectCharacter(CharacterData character)
    {
        if (character == null || !IsCharacterUnlocked(character))
            return false;

        SelectCharacter(character);
        return selectedCharacter == character;
    }
#endif
    private bool IsCharacterUnlocked(CharacterData character)
    {
        if (character == null || character.unlockData == null)
            return true;

        return UnlockProgressService.IsUnlockedNow(character.unlockData);
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

            ApplyDetailPanelStyle(detailPanel);
            ConfigurePortrait(detailPanel);
            ReparentInfoText(characterNameText, detailPanel, 28f, 44f, 30f,
                Color.white, FontStyles.Bold, 208f, 28f);
            ReparentInfoText(statusText, detailPanel, 82f, 24f, 15f,
                new Color(0.2f, 0.78f, 0.82f), FontStyles.Bold, 208f, 28f);
            ReparentInfoText(descriptionText, detailPanel, 112f, 116f, 17f,
                new Color(0.82f, 0.86f, 0.88f), FontStyles.Normal, 208f, 28f);
            descriptionText.textWrappingMode = TextWrappingModes.Normal;
            descriptionText.overflowMode = TextOverflowModes.Overflow;
            descriptionText.maxVisibleLines = int.MaxValue;

            SetProductionStatsVisible(false);

            Transform portraitFrame = FindDescendant(detailPanel, "PortraitImageOutline");
            if (portraitFrame != null)
                portraitFrame.gameObject.SetActive(false);

            Transform portraitMask = FindDescendant(detailPanel, "PortraitImageMask");
            if (portraitMask != null && portraitMask != portraitImage.transform)
                portraitMask.gameObject.SetActive(false);

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
        FontStyles style,
        float left = 28f,
        float right = 28f)
    {
        if (text == null)
            return;

        text.transform.SetParent(parent, false);
        RectTransform rect = text.rectTransform;
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = new Vector2(0f, -top);
        rect.offsetMin = new Vector2(left, rect.offsetMin.y);
        rect.offsetMax = new Vector2(-right, rect.offsetMax.y);
        rect.sizeDelta = new Vector2(-(left + right), height);
        text.fontSize = fontSize;
        text.fontStyle = style;
        text.color = color;
        text.alignment = TextAlignmentOptions.MidlineLeft;
        text.raycastTarget = false;
    }

    private void ConfigurePortrait(RectTransform parent)
    {
        if (portraitImage == null)
            return;

        portraitImage.transform.SetParent(parent, false);
        RectTransform rect = portraitImage.rectTransform;
        rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(28f, -28f);
        rect.sizeDelta = new Vector2(156f, 156f);
        portraitImage.preserveAspect = true;
        portraitImage.raycastTarget = false;
    }

    private void SetProductionStatsVisible(bool visible)
    {
        if (hpText != null) hpText.gameObject.SetActive(visible);
        if (speedText != null) speedText.gameObject.SetActive(visible);
        if (specialText != null) specialText.gameObject.SetActive(visible);
    }

    private static void ApplyDetailPanelStyle(RectTransform panel)
    {
        Image background = panel.GetComponent<Image>();
        if (background != null)
        {
            background.sprite = null;
            background.type = Image.Type.Simple;
            background.color = new Color(0.018f, 0.035f, 0.05f, 0.98f);
            background.raycastTarget = true;
        }

        foreach (Shadow shadow in panel.GetComponents<Shadow>())
            shadow.enabled = false;

        Outline outline = panel.GetComponent<Outline>();
        if (outline == null)
            outline = panel.gameObject.AddComponent<Outline>();
        outline.enabled = true;
        outline.effectColor = new Color(0.08f, 0.78f, 0.82f, 0.9f);
        outline.effectDistance = new Vector2(1f, -1f);
        outline.useGraphicAlpha = true;

        Transform oldOutline = FindDescendant(panel, "DetailPanelOutline");
        if (oldOutline != null)
            oldOutline.gameObject.SetActive(false);
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
