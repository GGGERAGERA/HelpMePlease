using UnityEngine;
using UnityEngine.SceneManagement;

public class GameOverManager : MonoBehaviour
{
    public static GameOverManager Instance;

    [SerializeField] private RunResultView runResultView;

    void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
            Destroy(gameObject);

    }

    public void GameOver()
    {
        HUDManager.Instance?.HideLowHpVignette();

        if (runResultView != null)
            runResultView.Show(false);

        Time.timeScale = 0f;
    }

    public void RestartGame()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }
    public void MainMenu()
    {
        if (RunEndService.Instance == null)
        {
            Debug.LogError(
                "[GameOverManager] RunEndService is missing."
            );

            return;
        }

        RunEndService.Instance.EndRunAfterDeath();
    }
}
