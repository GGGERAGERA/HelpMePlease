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

    void OnCharacterClick()
    {
        if (CharactersSelectionManager.Instance != null)
        {
            CharactersSelectionManager.Instance.SelectCharacter(character);
            Debug.Log($"Клик по персонажу: {character.characterName}");
        }
        else
        {
            Debug.LogError("CharactersSelectionManager.Instance не найден!");
        }
    }
}