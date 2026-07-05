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

        CharacterData character = RunSelectionManager.Instance.SelectedCharacter;
        WeaponData weapon = RunSelectionManager.Instance.SelectedWeapon;

        RunStateManager.EnsureExists().BeginNewRun(character, weapon);

        Debug.Log($"[BunkerRunStarter] Start run: character={character.name}, weapon={weapon.name}");

        Time.timeScale = 1f;
        SceneManager.LoadScene(gameplaySceneName);
    }
}