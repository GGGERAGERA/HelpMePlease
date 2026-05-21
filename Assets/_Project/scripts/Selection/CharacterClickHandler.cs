using UnityEngine;
using UnityEngine.UI;

public class CharacterClickHandler : MonoBehaviour
{
    public CharacterData character;

    void Start()
    {
        Button btn = GetComponent<Button>();
        if (btn == null)
            btn = gameObject.AddComponent<Button>();

        btn.onClick.AddListener(OnCharacterClick);
    }
    public void OnCharacterClick()
    {
        if (character == null)
        {
            Debug.LogWarning("CharacterClickHandler: characterData is null.");
            return;
        }

        CharactersSelectionManager.Instance.SelectCharacter(character);

        Debug.Log("Клик по персонажу: " + character.characterName);
    }
}

