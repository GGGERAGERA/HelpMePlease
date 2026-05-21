using UnityEngine;
using UnityEngine.SceneManagement;

public class CharactersSelectionManager : MonoBehaviour
{
    public static CharactersSelectionManager Instance;

    [Header("All Characters")]
    public CharacterData[] allCharacters; // массив всех персонажей

    // Храним ВЕСЬ объект CharacterData, а не индекс
    private CharacterData selectedCharacterData;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    // Новый метод: выбираем персонажа по объекту CharacterData
    public void SelectCharacter(CharacterData character)
    {
        selectedCharacterData = character;

        // Сохраняем индекс в PlayerPrefs (чтобы загрузить в следующей сцене)
        int index = System.Array.IndexOf(allCharacters, character);
        PlayerPrefs.SetInt("SelectedCharacter", index);
        PlayerPrefs.Save();

        Debug.Log($"Выбран персонаж: {character.characterName}");
    }

    // Получить выбранного персонажа
    public CharacterData GetSelectedCharacter()
    {
        return selectedCharacterData;
    }
}