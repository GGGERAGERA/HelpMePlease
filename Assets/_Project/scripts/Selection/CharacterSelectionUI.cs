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

    private void Awake()
    {
        if (selectButton != null)
            selectButton.onClick.AddListener(ConfirmSelection);

        if (backButton != null)
            backButton.onClick.AddListener(Close);

        ClearSelection();
    }

    private void OnEnable()
    {
        ClearSelection();
    }

    private void OnDestroy()
    {
        if (selectButton != null)
            selectButton.onClick.RemoveListener(ConfirmSelection);

        if (backButton != null)
            backButton.onClick.RemoveListener(Close);
    }

    public void SelectCharacter(CharacterData character)
    {
        if (character == null)
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
        Close();
    }

    private void Close()
    {
        if (panelManager != null)
            panelManager.CloseAll();
    }

    private void RefreshDetails(CharacterData character)
    {
        SetText(characterNameText, character.characterName);
        SetText(statusText, "ВЫЖИВШИЙ");
        SetText(hpText, $"HP: {character.maxHealth}");
        SetText(speedText, $"Speed: {character.moveSpeed}");
        SetText(specialText, $"Special: {GetSpecialText(character.specialDescription)}");
        SetText(descriptionText, character.description);
        SetPortrait(character.portrait);
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
        SetText(descriptionText, "Choose a survivor.");

        SetPortrait(null);
        RefreshCards(null);
        SetSelectButton(false);
    }

    private void SetPortrait(Sprite portrait)
    {
        if (portraitImage == null)
            return;

        portraitImage.sprite = portrait;
        portraitImage.enabled = portrait != null;
    }

    private void SetSelectButton(bool active)
    {
        if (selectButton == null)
            return;

        selectButton.interactable = active;

        Image image = selectButton.GetComponent<Image>();
        if (image != null)
            image.color = active ? enabledColor : disabledColor;
    }

    private void SetText(TextMeshProUGUI text, string value)
    {
        if (text != null)
            text.text = value;
    }

    private string GetSpecialText(string special)
    {
        return string.IsNullOrWhiteSpace(special) ? "No" : special;
    }
}