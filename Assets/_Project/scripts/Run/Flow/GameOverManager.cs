using UnityEngine;
using UnityEngine.SceneManagement;

public class GameOverManager : MonoBehaviour
{
    public static GameOverManager Instance;

    [SerializeField] private RunResultView runResultView;
    private bool isRestarting;

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
        if (isRestarting)
            return;

        isRestarting = true;
        RunStateManager runState = RunStateManager.Instance;

        if (runState != null)
        {
            CharacterData character = runState.SelectedCharacter;
            // The result panel already presents this reward. Finalize the dead
            // run before clearing its state so Restart cannot silently discard
            // earned gold.
            runState.EndRun(RunEndReason.PlayerDied);
            runState.BeginNewRun(character, null);
        }

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
