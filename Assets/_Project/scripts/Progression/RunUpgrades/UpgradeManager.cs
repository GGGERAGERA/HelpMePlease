using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(UpgradeApplier))]
public sealed class UpgradeManager : MonoBehaviour
{
    public static UpgradeManager Instance { get; private set; }

    [Header("UI")]
    [SerializeField] private UpgradePanelView upgradePanelView;

    [Header("Logic")]
    [SerializeField] private UpgradeApplier upgradeApplier;
    [SerializeField] private UpgradeData[] allUpgrades;
    [SerializeField] private int choicesCount = 3;

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
        if (isChoosingUpgrade)
            return;

        if (upgradePanelView == null)
        {
            Debug.LogError("[UpgradeManager] UpgradePanelView is not assigned.");
            return;
        }

        if (allUpgrades == null || allUpgrades.Length == 0)
        {
            Debug.LogWarning("[UpgradeManager] allUpgrades is empty.");
            return;
        }

        int playerLevel = ExperienceManager.Instance != null
            ? ExperienceManager.Instance.currentLevel
            : 1;

        List<UpgradeData> choices = upgradeRoller.RollChoices(playerLevel, choicesCount);

        if (choices.Count == 0)
        {
            Debug.LogWarning("[UpgradeManager] No upgrades available for current level.");
            return;
        }

        isChoosingUpgrade = true;
        previousTimeScale = Time.timeScale;
        Time.timeScale = 0f;

        upgradePanelView.Show(playerLevel, choices, SelectUpgrade);
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
        Time.timeScale = previousTimeScale <= 0f ? 1f : previousTimeScale;
    }
}
