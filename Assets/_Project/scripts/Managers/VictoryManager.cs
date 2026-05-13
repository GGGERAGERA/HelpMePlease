using UnityEngine;
using UnityEngine.SceneManagement;

public class VictoryManager : MonoBehaviour
{
    public static VictoryManager Instance { get; private set; }

    [SerializeField] private GameObject victoryPanel;

    private bool isVictory;

    private void Awake()
    {
        Instance = this;

        if (victoryPanel != null)
            victoryPanel.SetActive(false);
    }

    public void Victory()
    {
        if (isVictory)
            return;

        isVictory = true;

        if (victoryPanel != null)
            victoryPanel.SetActive(true);

        RunResultView resultView = victoryPanel.GetComponent<RunResultView>();

        if (resultView != null)
            resultView.Show();
        Time.timeScale = 0f;
    }

    public void RestartGame()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    public void MainMenu()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene("MainMenu");
    }
}
