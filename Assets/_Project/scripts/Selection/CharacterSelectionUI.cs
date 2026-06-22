using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class CharacterSelectionUI : MonoBehaviour
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

    [Header("Navigation")]
    [SerializeField] private MainMenuController mainMenuController;

    private CharacterData selectedCharacter;

    private void Awake()
    {
        if (selectButton != null)
            selectButton.onClick.AddListener(ConfirmSelection);

        ClearDetails();
    }

    public void SelectCharacter(CharacterData character)
    {
        if (character == null)
            return;

        selectedCharacter = character;

        UpdateDetails(character);
        UpdateCardsSelection(character);

        if (selectButton != null)
            selectButton.interactable = true;
    }

    private void ConfirmSelection()
    {
        if (selectedCharacter == null)
            return;

        if (RunSelectionManager.Instance != null)
            RunSelectionManager.Instance.SelectCharacter(selectedCharacter);

        if (mainMenuController != null)
            mainMenuController.OpenWeaponSelection();

        Debug.Log("Confirmed character: " + selectedCharacter.characterName);
    }

    private void UpdateDetails(CharacterData character)
    {
        if (characterNameText != null)
            characterNameText.text = character.characterName;

        if (statusText != null)
            statusText.text = "¬€∆»¬ÿ»…";

        if (portraitImage != null)
        {
            portraitImage.sprite = character.portrait;
            portraitImage.enabled = character.portrait != null;
        }

        if (hpText != null)
            hpText.text = $"HP: {character.maxHealth}";

        if (speedText != null)
            speedText.text = $"Speed: {character.moveSpeed}";

        if (specialText != null)
            specialText.text = $"Special: {character.specialDescription}";

        if (descriptionText != null)
            descriptionText.text = character.description;
    }

    private void UpdateCardsSelection(CharacterData character)
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

    private void ClearDetails()
    {
        selectedCharacter = null;

        if (characterNameText != null)
            characterNameText.text = "SELECT CHARACTER";

        if (statusText != null)
            statusText.text = "";

        if (portraitImage != null)
            portraitImage.enabled = false;

        if (hpText != null)
            hpText.text = "HP: -";

        if (speedText != null)
            speedText.text = "Speed: -";

        if (specialText != null)
            specialText.text = "Special: -";

        if (descriptionText != null)
            descriptionText.text = "Choose a survivor.";

        if (selectButton != null)
            selectButton.interactable = false;
    }
}