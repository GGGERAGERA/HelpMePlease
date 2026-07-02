using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class MenuPortal : MonoBehaviour
{
    [SerializeField] private string menuSceneName = "MainMenu";

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Player"))
            return;

        Time.timeScale = 1f;
        SceneManager.LoadScene(menuSceneName);
    }
}