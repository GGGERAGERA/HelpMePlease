using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class CharacterSelectionUI : MonoBehaviour
{
    [Header("Cards")]
    [SerializeField] private CharacterCardView[] cards;

    [Header("Character Info")]
    [SerializeField] private GameObject characterInfoRoot;
    [SerializeField] private TextMeshProUGUI emptyStateText;
    [SerializeField] private Image portraitImage;
    [SerializeField] private TextMeshProUGUI characterNameText;
    [SerializeField] private TextMeshProUGUI combatTypeText;
    [SerializeField] private TextMeshProUGUI featureText;
    [SerializeField] private TextMeshProUGUI statsText;
    [SerializeField] private TextMeshProUGUI descriptionText;

    [Header("Buttons")]
    [SerializeField] private Button selectButton;
    [SerializeField] private Button backButton;

    [Header("Station")]
    [SerializeField] private CharacterStationEmbeddedView stationView;

    [Header("Navigation")]
    [SerializeField] private BunkerPanelManager panelManager;

    private LocalizationService localization;
    private CharacterData selectedCharacter;
    private BunkerStationProgressionService boundProgressionService;

    private void Awake()
    {
        if (stationView == null)
            stationView = GetComponent<CharacterStationEmbeddedView>();

        if (selectButton != null)
            selectButton.onClick.AddListener(ConfirmSelection);
        if (backButton != null)
            backButton.onClick.AddListener(Close);

        BindCards();
        ClearSelection();
    }

    private void OnEnable()
    {
        localization = LocalizationService.EnsureExists();
        localization.LanguageChanged += HandleLanguageChanged;
        SubscribeToProgression();
        stationView?.Refresh();
        RefreshAllCards();
        RestoreCurrentSelection();
    }

    private void Update()
    {
        if (boundProgressionService != BunkerStationProgressionService.Instance)
            SubscribeToProgression();
    }

    private void OnDisable()
    {
        if (localization != null)
            localization.LanguageChanged -= HandleLanguageChanged;
        localization = null;
        UnsubscribeFromProgression();
    }

    private void OnDestroy()
    {
        UnsubscribeFromProgression();
        if (selectButton != null)
            selectButton.onClick.RemoveListener(ConfirmSelection);
        if (backButton != null)
            backButton.onClick.RemoveListener(Close);

        if (cards == null)
            return;
        foreach (CharacterCardView card in cards)
        {
            if (card != null)
                card.Clicked -= SelectCharacter;
        }
    }

    public void SelectCharacter(CharacterData character)
    {
        if (character == null || !IsCharacterUnlocked(character))
            return;

        selectedCharacter = character;
        RefreshDetails(character);
        RefreshCards(character);
        SetSelectButton(true);
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

    private void Close() => Close(true);

    private void Close(bool playSound)
    {
        if (panelManager != null)
            panelManager.CloseAll(playSound);
    }

    private void RefreshDetails(CharacterData character)
    {
        if (characterInfoRoot != null)
            characterInfoRoot.SetActive(true);
        if (emptyStateText != null)
            emptyStateText.gameObject.SetActive(false);
        if (portraitImage != null)
        {
            portraitImage.sprite = character.portrait;
            portraitImage.enabled = character.portrait != null;
            portraitImage.preserveAspect = true;
            portraitImage.color = Color.white;
        }

        SetText(characterNameText, character.LocalizedName);
        SetText(combatTypeText, character.LocalizedCombatTypeDisplayName);
        SetText(featureText,
            string.Format(LocalizationService.EnsureExists().Get("character.details.feature"), character.LocalizedCombatTypeDescription));
        SetText(statsText, GetStatsText(character));
        SetText(descriptionText,
            string.Format(LocalizationService.EnsureExists().Get("character.details.description"), character.LocalizedDescription));
    }

    private static string GetStatsText(CharacterData character)
    {
        string healthBar = BuildPixelBar(
            Mathf.Clamp(Mathf.RoundToInt(character.maxHealth / 25f), 1, 8));
        string speedBar = BuildPixelBar(
            Mathf.Clamp(Mathf.RoundToInt(character.moveSpeed), 1, 8));

        return string.Format(LocalizationService.EnsureExists().Get("character.details.stats"),
            character.maxHealth, healthBar, character.moveSpeed, speedBar);
    }

    private static string BuildPixelBar(int filled)
    {
        const int width = 8;
        int safeFilled = Mathf.Clamp(filled, 0, width);
        return $"<color=#14D1DB>{new string('■', safeFilled)}</color>" +
               $"<color=#52666A>{new string('□', width - safeFilled)}</color>";
    }

    private void ClearSelection()
    {
        selectedCharacter = null;
        if (characterInfoRoot != null)
            characterInfoRoot.SetActive(false);
        if (portraitImage != null)
        {
            portraitImage.sprite = null;
            portraitImage.enabled = false;
        }
        if (emptyStateText != null)
        {
            emptyStateText.gameObject.SetActive(true);
            emptyStateText.text = LocalizationService.EnsureExists().Get("character.choose");
        }

        RefreshCards(null);
        SetSelectButton(false);
    }

    private void HandleLanguageChanged(GameLanguage language)
    {
        RefreshAllCards();
        if (selectedCharacter != null)
            RefreshDetails(selectedCharacter);
        else if (emptyStateText != null)
            emptyStateText.text = localization.Get("character.choose");
    }

    private void RestoreCurrentSelection()
    {
        CharacterData current = RunSelectionManager.Instance != null
            ? RunSelectionManager.Instance.SelectedCharacter
            : null;

        if (IsCharacterUnlocked(current))
            SelectCharacter(current);
        else
            ClearSelection();
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

    private void RefreshAllCards()
    {
        if (cards == null)
            return;
        foreach (CharacterCardView card in cards)
            card?.Refresh();
    }

    private void RefreshCards(CharacterData character)
    {
        if (cards == null)
            return;
        foreach (CharacterCardView card in cards)
        {
            if (card != null)
                card.SetSelected(card.Character == character);
        }
    }

    private void SetSelectButton(bool active)
    {
        if (selectButton != null)
            selectButton.interactable = active;
    }

    private static void SetText(TextMeshProUGUI text, string value)
    {
        if (text != null)
            text.text = value;
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

        RefreshAllCards();
        if (selectedCharacter == null)
            return;

        if (IsCharacterUnlocked(selectedCharacter))
        {
            RefreshDetails(selectedCharacter);
            SetSelectButton(true);
        }
        else
        {
            ClearSelection();
        }
    }

    private static bool IsCharacterUnlocked(CharacterData character)
    {
        return character != null &&
               (character.unlockData == null || UnlockProgressService.IsUnlockedNow(character.unlockData));
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    public void CollectDebugCharacters(System.Collections.Generic.List<CharacterData> destination)
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
        RefreshAllCards();
        if (selectedCharacter != null)
        {
            if (IsCharacterUnlocked(selectedCharacter))
                RefreshDetails(selectedCharacter);
            else
                ClearSelection();
        }
    }

    public bool CanDebugSelectCharacter(CharacterData character) => IsCharacterUnlocked(character);

    public bool DebugSelectCharacter(CharacterData character)
    {
        if (!IsCharacterUnlocked(character))
            return false;
        SelectCharacter(character);
        return selectedCharacter == character;
    }
#endif
}
