using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class CharacterSelectionUI : MonoBehaviour
{
    [Header("Navigation")]
    [SerializeField] private Button nextButton;

    [Header("Top Text")]
    [SerializeField] private TextMeshProUGUI selectedPlayerText;

    [Header("Right Info Panel")]
    [SerializeField] private Image portraitImage;
    [SerializeField] private TextMeshProUGUI characterNameText;
    [SerializeField] private TextMeshProUGUI descriptionText;

    [Header("Left Base Stats Panel")]
    [SerializeField] private TextMeshProUGUI hpText;
    [SerializeField] private TextMeshProUGUI speedText;
    [SerializeField] private TextMeshProUGUI specialText;

    [Header("Next Button Visual")]
    [SerializeField] private Color enabledColor = Color.white;
    [SerializeField] private Color disabledColor = new Color(0.45f, 0.45f, 0.45f, 1f);

    private CharacterData selectedCharacter;
    private CharactersSelectionManager selectionManager;

    private void Awake()
    {
        selectionManager = CharactersSelectionManager.Instance;
    }

    private void Start()
    {
        ClearSelection();
    }

    public void SelectCharacter(CharacterData character)
    {
        if (character == null)
        {
            Debug.LogWarning("CharacterSelectionUI: selected character is null.");
            return;
        }

        selectedCharacter = character;

        if (selectionManager != null)
            selectionManager.SelectCharacter(character);

        UpdateTopText(character);
        UpdateRightPanel(character);
        UpdateStatsPanel(character);
        SetNextButtonState(true);
    }

    private void ClearSelection()
    {
        selectedCharacter = null;

        if (selectedPlayerText != null)
            selectedPlayerText.text = "Selected Player: none";

        if (portraitImage != null)
        {
            portraitImage.sprite = null;
            portraitImage.enabled = false;
        }

        if (characterNameText != null)
            characterNameText.text = "";

        if (descriptionText != null)
            descriptionText.text = "Select a character to see details.";

        SetText(hpText, "HP: -");
        SetText(speedText, "Speed: -");
        SetText(specialText, "Special: -");

        SetNextButtonState(false);
    }

    private void UpdateTopText(CharacterData character)
    {
        if (selectedPlayerText != null)
            selectedPlayerText.text = "Selected Player: " + character.characterName;
    }

    private void UpdateRightPanel(CharacterData character)
    {
        if (portraitImage != null)
        {
            portraitImage.sprite = character.portrait;
            portraitImage.enabled = character.portrait != null;
        }

        if (characterNameText != null)
            characterNameText.text = character.characterName;

        if (descriptionText != null)
            descriptionText.text = character.description;
    }

    private void UpdateStatsPanel(CharacterData character)
    {
        SetText(hpText, "HP: " + character.maxHealth);
        SetText(speedText, "Speed: " + character.moveSpeed);
        SetText(specialText, "Special: " + character.specialDescription);
    }

    private void SetNextButtonState(bool isEnabled)
    {
        if (nextButton == null)
            return;

        nextButton.interactable = isEnabled;

        Image buttonImage = nextButton.GetComponent<Image>();
        if (buttonImage != null)
            buttonImage.color = isEnabled ? enabledColor : disabledColor;
    }

    private void SetText(TextMeshProUGUI text, string value)
    {
        if (text != null)
            text.text = value;
    }
}