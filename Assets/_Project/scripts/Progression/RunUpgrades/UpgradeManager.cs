using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(UpgradeApplier))]
public sealed class UpgradeManager : MonoBehaviour
{
    private readonly struct UpgradeChoiceRequest
    {
        public readonly int PlayerLevel;
        public readonly bool PlayLevelUpSound;
        public readonly int ChoiceCount;
        public readonly bool GuaranteeBehavior;
        public readonly bool IsChestReward;
        public readonly System.Action OnClosed;

        public UpgradeChoiceRequest(
            int playerLevel,
            bool playLevelUpSound,
            int choiceCount,
            bool guaranteeBehavior,
            bool isChestReward,
            System.Action onClosed = null
        )
        {
            PlayerLevel = playerLevel;
            PlayLevelUpSound = playLevelUpSound;
            ChoiceCount = choiceCount;
            GuaranteeBehavior = guaranteeBehavior;
            IsChestReward = isChestReward;
            OnClosed = onClosed;
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
    private System.Action currentOnClosed;

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
            new UpgradeChoiceRequest(
                playerLevel,
                playLevelUpSound: false,
                choicesCount,
                guaranteeBehavior: false,
                isChestReward: false
            )
        );
    }

    public void ShowLevelUpChoices(int playerLevel)
    {
        RequestUpgradeChoices(
            new UpgradeChoiceRequest(
                playerLevel,
                playLevelUpSound: true,
                choicesCount,
                guaranteeBehavior: false,
                isChestReward: false
            )
        );
    }

    public void ShowChestRewardChoices(
        int choiceCount,
        bool guaranteeBehavior,
        System.Action onClosed
    )
    {
        int playerLevel = ExperienceManager.Instance != null
            ? ExperienceManager.Instance.currentLevel
            : 1;

        RequestUpgradeChoices(
            new UpgradeChoiceRequest(
                playerLevel,
                playLevelUpSound: false,
                choiceCount,
                guaranteeBehavior,
                isChestReward: true,
                onClosed
            )
        );
    }

    private void RequestUpgradeChoices(UpgradeChoiceRequest request)
    {
        if (isChoosingUpgrade)
        {
            pendingChoices.Enqueue(request);
            return;
        }

        if (!TryBuildChoices(request, out List<UpgradeData> choices))
        {
            request.OnClosed?.Invoke();
            return;
        }

        isChoosingUpgrade = true;
        previousTimeScale = Time.timeScale;
        Time.timeScale = 0f;
        currentOnClosed = request.OnClosed;

        ShowChoiceRequest(request, choices);
    }

    private bool TryBuildChoices(
        UpgradeChoiceRequest request,
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

        choices = request.GuaranteeBehavior
            ? upgradeRoller.RollRewardChoices(
                request.PlayerLevel,
                request.ChoiceCount
            )
            : upgradeRoller.RollChoices(
                request.PlayerLevel,
                request.ChoiceCount
            );

        if (choices.Count > 0)
            return true;

        if (request.IsChestReward)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogWarning(
                $"[UpgradeManager] No chest rewards available for level " +
                $"{request.PlayerLevel}."
            );
#endif
        }
        else
        {
            Debug.LogWarning(
                $"[UpgradeManager] No upgrades available for level " +
                $"{request.PlayerLevel}."
            );
        }

        return false;
    }

    private void ShowChoiceRequest(
        UpgradeChoiceRequest request,
        IReadOnlyList<UpgradeData> choices
    )
    {
        if (request.PlayLevelUpSound)
            AudioService.Instance?.Play(AudioCueId.LevelUp);

        if (request.IsChestReward)
        {
            upgradePanelView.Show(
                "НАГРАДА",
                "Выберите предмет",
                choices,
                SelectUpgrade
            );
        }
        else
        {
            upgradePanelView.Show(
                request.PlayerLevel,
                choices,
                SelectUpgrade
            );
        }
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

        RunStateManager runState = RunStateManager.EnsureExists();
        ItemGrantResult grantResult = runState.ItemSlots.TryAdd(upgrade);

        if (grantResult == ItemGrantResult.Added ||
            grantResult == ItemGrantResult.LeveledUp)
        {
            bool applied = upgradeApplier.Apply(upgrade);

            if (!applied)
                return;

            runState.RegisterUpgrade(upgrade);
        }
        else if (grantResult == ItemGrantResult.RequiresReplacement)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogWarning(
                $"[UpgradeManager] No free item slot for {upgrade.upgradeName}. " +
                "Replacement selection is not implemented yet."
            );
#endif
        }
        else if (grantResult == ItemGrantResult.Invalid)
        {
            Debug.LogWarning("[UpgradeManager] Cannot grant an invalid upgrade.");
            return;
        }

        CloseUpgradeSelection();
    }

    private void CloseUpgradeSelection()
    {
        if (upgradePanelView != null)
            upgradePanelView.Hide();

        isChoosingUpgrade = false;
        System.Action onClosed = currentOnClosed;
        currentOnClosed = null;
        onClosed?.Invoke();

        while (pendingChoices.Count > 0)
        {
            UpgradeChoiceRequest nextRequest = pendingChoices.Dequeue();

            if (!TryBuildChoices(nextRequest, out List<UpgradeData> choices))
            {
                nextRequest.OnClosed?.Invoke();
                continue;
            }

            isChoosingUpgrade = true;
            currentOnClosed = nextRequest.OnClosed;
            ShowChoiceRequest(nextRequest, choices);
            return;
        }

        Time.timeScale = previousTimeScale;
    }
}
