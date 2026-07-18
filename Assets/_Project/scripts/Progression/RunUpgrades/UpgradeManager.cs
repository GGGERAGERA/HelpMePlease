using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(UpgradeApplier))]
public sealed class UpgradeManager : MonoBehaviour
{
    private readonly struct UpgradeChoiceRequest
    {
        public readonly int PlayerLevel;
        public readonly bool PlayLevelUpSound;

        public UpgradeChoiceRequest(int playerLevel, bool playLevelUpSound)
        {
            PlayerLevel = playerLevel;
            PlayLevelUpSound = playLevelUpSound;
        }
    }

    public static UpgradeManager Instance { get; private set; }

    [Header("UI")]
    [SerializeField] private UpgradePanelView upgradePanelView;

    [Header("Logic")]
    [SerializeField] private UpgradeApplier upgradeApplier;
    [SerializeField] private UpgradeData[] allUpgrades;
    [SerializeField] private int choicesCount = 3;

    private readonly Queue<UpgradeChoiceRequest> pendingChoices = new();
    private UpgradeRoller upgradeRoller;
    private bool isChoosingUpgrade;
    private float previousTimeScale = 1f;

    public bool IsChoosingUpgrade => isChoosingUpgrade;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        if (upgradeApplier == null)
            upgradeApplier = GetComponent<UpgradeApplier>();

        upgradeRoller = new UpgradeRoller(allUpgrades);

        if (upgradePanelView != null)
            upgradePanelView.Hide();
    }

    public void ShowUpgradeChoices()
    {
        int playerLevel = ExperienceManager.Instance != null
            ? ExperienceManager.Instance.currentLevel
            : 1;

        RequestUpgradeChoices(
            new UpgradeChoiceRequest(playerLevel, playLevelUpSound: false)
        );
    }

    public void ShowLevelUpChoices(int playerLevel)
    {
        RequestUpgradeChoices(
            new UpgradeChoiceRequest(playerLevel, playLevelUpSound: true)
        );
    }

    private void RequestUpgradeChoices(UpgradeChoiceRequest request)
    {
        if (isChoosingUpgrade)
        {
            pendingChoices.Enqueue(request);
            return;
        }

        if (!TryBuildChoices(request.PlayerLevel, out List<UpgradeData> choices))
            return;

        isChoosingUpgrade = true;
        previousTimeScale = Time.timeScale;
        Time.timeScale = 0f;

        ShowChoiceRequest(request, choices);
    }

    private bool TryBuildChoices(
        int playerLevel,
        out List<UpgradeData> choices
    )
    {
        choices = null;

        if (upgradePanelView == null)
        {
            Debug.LogError("[UpgradeManager] UpgradePanelView is not assigned.");
            return false;
        }

        if (allUpgrades == null || allUpgrades.Length == 0)
        {
            Debug.LogWarning("[UpgradeManager] allUpgrades is empty.");
            return false;
        }

        choices = upgradeRoller.RollChoices(playerLevel, choicesCount);

        if (choices.Count > 0)
            return true;

        Debug.LogWarning(
            $"[UpgradeManager] No upgrades available for level {playerLevel}."
        );
        return false;
    }

    private void ShowChoiceRequest(
        UpgradeChoiceRequest request,
        IReadOnlyList<UpgradeData> choices
    )
    {
        if (request.PlayLevelUpSound)
            AudioService.Instance?.Play(AudioCueId.LevelUp);

        upgradePanelView.Show(request.PlayerLevel, choices, SelectUpgrade);
    }

    private void SelectUpgrade(UpgradeData upgrade)
    {
        if (!isChoosingUpgrade)
            return;

        if (upgradeApplier == null)
        {
            Debug.LogError("[UpgradeManager] UpgradeApplier is not assigned.");
            return;
        }

        bool applied = upgradeApplier.Apply(upgrade);

        if (!applied)
            return;

        RunStateManager.EnsureExists().RegisterUpgrade(upgrade);

        CloseUpgradeSelection();
    }

    private void CloseUpgradeSelection()
    {
        if (upgradePanelView != null)
            upgradePanelView.Hide();

        isChoosingUpgrade = false;

        while (pendingChoices.Count > 0)
        {
            UpgradeChoiceRequest nextRequest = pendingChoices.Dequeue();

            if (!TryBuildChoices(
                    nextRequest.PlayerLevel,
                    out List<UpgradeData> choices))
            {
                continue;
            }

            isChoosingUpgrade = true;
            ShowChoiceRequest(nextRequest, choices);
            return;
        }

        Time.timeScale = previousTimeScale;
    }
}
