using UnityEngine;
using UnityEngine.SceneManagement;

public class RunResultButtons : MonoBehaviour
{
    [SerializeField] private string mainMenuSceneName = "MainMenu";

    public void Retry()
    {
        RunStateManager runState = RunStateManager.Instance;

        if (runState != null)
        {
            CharacterData character = runState.SelectedCharacter;
            WeaponData weapon = runState.SelectedWeapon;
            runState.BeginNewRun(character, weapon);
        }

        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    public void MainMenu()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(mainMenuSceneName);
    }
}
