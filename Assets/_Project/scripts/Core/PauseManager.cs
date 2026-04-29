using UnityEngine;
using UnityEngine.SceneManagement;

public class PauseManager : MonoBehaviour
{
    public GameObject pauseCanvas; // перетащите PauseCanvas в инспекторе
    private bool isPaused = false;

    void Update()
    {
        // Нажатие на Escape
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (isPaused)
                ResumeGame();
            else
                PauseGame();
        }
    }

    public void PauseGame()
    {
        isPaused = true;
        Time.timeScale = 0f;      // останавливаем время
        pauseCanvas.SetActive(true);
    }

    public void ResumeGame()
    {
        isPaused = false;
        Time.timeScale = 1f;
        pauseCanvas.SetActive(false);
    }

    public void RestartGame()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    public void MenuGame()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene("MainMenu"); // Убедитесь, что у вас есть сцена с именем "MainMenu"
    }
}

