using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class BunkerRunStarter : MonoBehaviour
{
    [SerializeField] private string gameplaySceneName = "MVP";
    private BunkerNotificationManager Notifications =>
    BunkerContext.Instance != null ? BunkerContext.Instance.Notifications : null;

    public void StartRun()
    {
        if (RunSelectionManager.Instance == null)
        {
            Notifications?.ShowError("Система выбора не найдена.");
            return;
        }

        if (RunSelectionManager.Instance.SelectedCharacter == null)
        {
            Notifications?.ShowWarning("Сначала выбери персонажа.");
            return;
        }

        if (RunSelectionManager.Instance.SelectedWeapon == null)
        {
            Notifications?.ShowWarning("Сначала выбери оружие.");
            return;
        }

        CharacterData character = RunSelectionManager.Instance.SelectedCharacter;
        WeaponData weapon = RunSelectionManager.Instance.SelectedWeapon;

        RunStateManager.EnsureExists().BeginNewRun(character, weapon);

        Time.timeScale = 1f;
        SceneManager.LoadScene(gameplaySceneName);
    }
}