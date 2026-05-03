using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class CharacterSelectionUI : MonoBehaviour
{
    [Header("Buttons")]
    public Button nextButton;
    public Button previousButton;
    public Button selectButton;

    [Header("UI Elements")]
    public TextMeshProUGUI nameText;
    public TextMeshProUGUI descriptionText;
    public Image portraitImage;
    public TextMeshProUGUI statsText;

    private CharactersSelectionManager selectionManager;
    private int currentIndex = 0;

    void Start()
    {
        selectionManager = CharactersSelectionManager.Instance;
        if (selectionManager == null)
        {
            Debug.LogError("CharacterSelectionManager not found!");
            return;
        }

        // Подписываемся на кнопки
        if (nextButton != null)
            nextButton.onClick.AddListener(NextCharacter);
        if (previousButton != null)
            previousButton.onClick.AddListener(PreviousCharacter);
        if (selectButton != null)
            selectButton.onClick.AddListener(SelectCharacter);

        // Показываем первого персонажа
        UpdateUI();
    }

    void NextCharacter()
    {
        currentIndex++;
        if (currentIndex >= selectionManager.allCharacters.Length)
            currentIndex = 0;
        UpdateUI();
    }

    void PreviousCharacter()
    {
        currentIndex--;
        if (currentIndex < 0)
            currentIndex = selectionManager.allCharacters.Length - 1;
        UpdateUI();
    }

    void UpdateUI()
    {
        CharacterData character = selectionManager.allCharacters[currentIndex];
        if (character == null) return;

        if (nameText != null)
            nameText.text = character.characterName;
        if (descriptionText != null)
            descriptionText.text = character.description;
        if (portraitImage != null && character.portrait != null)
            portraitImage.sprite = character.portrait;
        if (statsText != null)
            statsText.text = $"Damage: {character.damage}\nHealth: {character.maxHealth}\nSpeed: {character.moveSpeed}";
    }

    void SelectCharacter()
    {
        CharacterData selected = selectionManager.allCharacters[currentIndex];
        selectionManager.SelectCharacter(selected);
        //selectionManager.LoadGameScene();
        Debug.Log($"Выбран персонаж: {selected.characterName}");
    }
}