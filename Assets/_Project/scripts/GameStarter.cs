using UnityEngine;
using UnityEngine.SceneManagement;

public class GameStarter : MonoBehaviour
{
    public void StartGame()
    {
        // Проверяем, выбран ли персонаж
        if (CharactersSelectionManager.Instance == null ||
            CharactersSelectionManager.Instance.GetSelectedCharacter() == null)
        {
            Debug.LogWarning("Сначала выбери персонажа!");
            return;
        }

        // Уровень уже сохранится в PlayerPrefs через слайдер
        Time.timeScale = 1f; // Убедимся, что время не заморожено
        SceneManager.LoadScene("MVP");  // имя твоей игровой сцены
    }
}