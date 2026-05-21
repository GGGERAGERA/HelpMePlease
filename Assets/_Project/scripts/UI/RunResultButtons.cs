using UnityEngine;
using UnityEngine.SceneManagement;

public class RunResultButtons : MonoBehaviour
{
    [SerializeField] private string mainMenuSceneName = "MainMenu";

    public void Retry()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    public void MainMenu()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(mainMenuSceneName);
    }
}
