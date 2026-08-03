using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class BunkerRunStarter : MonoBehaviour
{
    [SerializeField] private string gameplaySceneName = "MVP";

    [Header("Starting Sector")]
    [SerializeField] private StageProfileData startingStageProfile;
    [SerializeField] private WorldRuleData startingWorldRule;
    [SerializeField] private LocalAnomalyData startingLocalAnomaly;

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

        if (startingStageProfile == null ||
            startingWorldRule == null ||
            startingLocalAnomaly == null)
        {
            Debug.LogError(
                "[BunkerRunStarter] Starting sector configuration is incomplete.",
                this
            );
            return;
        }

        if (startingStageProfile.SectorNumber != 1)
        {
            Debug.LogError(
                "[BunkerRunStarter] Starting StageProfile must be sector 1.",
                this
            );
            return;
        }

        RunStateManager.EnsureExists().BeginNewRun(
            character,
            weapon,
            startingStageProfile,
            startingWorldRule,
            startingLocalAnomaly
        );

        AudioService.Instance?.Play(AudioCueId.StartRun);
        Time.timeScale = 1f;
        SceneManager.LoadScene(gameplaySceneName);
    }
}
