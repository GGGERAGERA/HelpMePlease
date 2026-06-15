using UnityEngine;
using UnityEngine.UI;

public class CharacterClickHandler : MonoBehaviour
{
    [SerializeField] private CharacterData character;
    [SerializeField] private CharacterSelectionUI selectionUI;

    private void Awake()
    {
        Button button = GetComponent<Button>();

        if (button == null)
            button = gameObject.AddComponent<Button>();

        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(OnCharacterClick);
    }

    private void OnCharacterClick()
    {
        Debug.Log("UI SELECT CHARACTER: " + character.characterName);
        Debug.Log("Character CARD CLICKED: " + gameObject.name);
        if (character == null)
        {
            Debug.LogWarning("CharacterClickHandler: character is null.");
            return;
        }

        if (selectionUI == null)
        {
            Debug.LogWarning("CharacterClickHandler: selectionUI is null.");
            return;
        }

        selectionUI.SelectCharacter(character);
    }
}