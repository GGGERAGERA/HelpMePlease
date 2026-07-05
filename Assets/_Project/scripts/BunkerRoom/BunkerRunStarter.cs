using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class BunkerRunStarter : MonoBehaviour
{
    [SerializeField] private string gameplaySceneName = "MVP";

    public void StartRun()
    {
        if (RunSelectionManager.Instance == null)
        {
            Debug.LogError("[BunkerRunStarter] RunSelectionManager is missing.");
            return;
        }

        if (!RunSelectionManager.Instance.IsReady)
        {
            Debug.LogWarning("[BunkerRunStarter] Select character and weapon first.");
            return;
        }

        SceneManager.LoadScene(gameplaySceneName);
    }
}