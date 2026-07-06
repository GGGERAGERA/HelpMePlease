using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class BunkerRunStarter : MonoBehaviour
{
    [SerializeField] private string gameplaySceneName = "MVP";

    public void StartRun()
    {
        if (RunSelectionManager.Instance == null)
        {
            BunkerNotificationManager.Instance?.ShowError("Система выбора не найдена.");
            return;
        }

        if (RunSelectionManager.Instance.SelectedCharacter == null)
        {
            BunkerNotificationManager.Instance?.ShowWarning("Сначала выбери персонажа.");
            return;
        }

        if (RunSelectionManager.Instance.SelectedWeapon == null)
        {
            BunkerNotificationManager.Instance?.ShowWarning("Сначала выбери оружие.");
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