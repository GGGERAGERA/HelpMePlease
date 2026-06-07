using UnityEngine;

public class CharactersSelectionManager : MonoBehaviour
{
    public static CharactersSelectionManager Instance { get; private set; }

    [Header("All Characters")]
    public CharacterData[] allCharacters;

    private CharacterData selectedCharacterData;

    private const string SelectedCharacterKey = "SelectedCharacter";

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public void SelectCharacter(CharacterData character)
    {
        if (character == null)
            return;

        selectedCharacterData = character;

        int index = System.Array.IndexOf(allCharacters, character);

        if (index >= 0)
        {
            PlayerPrefs.SetInt(SelectedCharacterKey, index);
            PlayerPrefs.Save();
        }
        else
        {
            Debug.LogWarning("CharactersSelectionManager: selected character is not in allCharacters array: " + character.characterName);
        }

        Debug.Log("Selected character: " + character.characterName);
    }

    public CharacterData GetSelectedCharacter()
    {
        if (selectedCharacterData != null)
            return selectedCharacterData;

        int index = PlayerPrefs.GetInt(SelectedCharacterKey, -1);

        if (allCharacters != null && index >= 0 && index < allCharacters.Length)
            selectedCharacterData = allCharacters[index];

        return selectedCharacterData;
    }
}