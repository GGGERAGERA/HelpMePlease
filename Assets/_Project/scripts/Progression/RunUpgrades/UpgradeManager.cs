using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Subject42.Combat.OrbitalStation;

[RequireComponent(typeof(UpgradeApplier))]
public sealed class UpgradeManager : MonoBehaviour
{
    private readonly struct UpgradeChoiceRequest
    {
        public readonly int PlayerLevel;
        public readonly bool PlayLevelUpSound;
        public readonly int ChoiceCount;
        public readonly bool GuaranteeBehavior;
        public readonly bool NumericOnly;
        public readonly bool IsChestReward;
        public readonly System.Action OnClosed;

        public UpgradeChoiceRequest(
            int playerLevel,
            bool playLevelUpSound,
            int choiceCount,
            bool guaranteeBehavior,
            bool isChestReward,
            System.Action onClosed = null,
            bool numericOnly = false
        )
        {
            PlayerLevel = playerLevel;
            PlayLevelUpSound = playLevelUpSound;
            ChoiceCount = choiceCount;
            GuaranteeBehavior = guaranteeBehavior;
            NumericOnly = numericOnly;
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
    private readonly Queue<UpgradeChoiceRequest> milestoneChoices = new();
    private readonly Queue<int> milestoneLevels = new();
    private readonly Queue<System.Action> idleCallbacks = new();
    private OrbitalRewardProvider orbitalRewardProvider;
    private OrbitalRewardFlowController orbitalRewardFlow;
    private List<UpgradeData> currentChoices;
    private bool milestoneSequenceRunning;
    private bool shuttingDown;
    private bool isChoosingUpgrade;
    private float previousTimeScale = 1f;
    private System.Action currentOnClosed;
    private UpgradeChoiceRequest currentRequest;
    private bool hasCurrentRequest;

    public float TimeScaleAfterRewards => previousTimeScale;

    public bool IsRewardQueueIdle => !isChoosingUpgrade && !hasCurrentRequest &&
        pendingChoices.Count == 0 && !milestoneSequenceRunning && milestoneChoices.Count == 0;

    public bool IsChoosingUpgrade => isChoosingUpgrade ||
        milestoneSequenceRunning || milestoneChoices.Count > 0;

    public void RunWhenRewardQueueIsIdle(System.Action callback)
    {
        if (callback == null)
            return;
        if (isChoosingUpgrade || pendingChoices.Count > 0 ||
            milestoneSequenceRunning || milestoneChoices.Count > 0)
        {
            idleCallbacks.Enqueue(callback);
            return;
        }
        callback();
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    public IReadOnlyList<UpgradeData> AllUpgrades => allUpgrades;

    public void ConfigureDebugUpgradePool(
        UpgradeData[] upgrades,
        UpgradeApplier applier)
    {
        allUpgrades = upgrades ?? System.Array.Empty<UpgradeData>();
        upgradeApplier = applier != null ? applier : GetComponent<UpgradeApplier>();
        orbitalRewardProvider?.Dispose();
        orbitalRewardProvider = new OrbitalRewardProvider(allUpgrades);
    }

    public bool TryApplyDebugUpgrade(
        UpgradeData upgrade,
        out ItemGrantResult grantResult)
    {
        return TryGrantUpgrade(upgrade, out grantResult);
    }

    public int GetEligibleProductionUpgradeCount(int playerLevel)
    {
        return orbitalRewardProvider?.GetEligibleKinds().Count ?? 0;
    }

    public int GetStationAvailableProductionUpgradeCount()
    {
        if (allUpgrades == null)
            return 0;

        int count = 0;
        for (int i = 0; i < allUpgrades.Length; i++)
        {
            UpgradeData upgrade = allUpgrades[i];
            if (upgrade != null &&
                UnlockProgressService.IsUnlockedNow(upgrade.unlockData))
            {
                count++;
            }
        }

        return count;
    }

    public string GetOrbitalEligibilitySummary() =>
        orbitalRewardProvider?.GetEligibilitySummary() ?? "provider unavailable";

    public bool DebugForceOrbitalReward(OrbitalRewardKind kind)
    {
        if (isChoosingUpgrade || orbitalRewardProvider == null ||
            !orbitalRewardProvider.IsEligible(kind))
            return false;
        OrbitalRewardData reward = orbitalRewardProvider.GetDefinition(kind);
        if (reward == null)
            return false;
        int level = ExperienceManager.Instance?.CurrentLevel ?? 1;
        UpgradeChoiceRequest request = new(level, false, 1, false, false);
        BeginChoiceRequest(request, new List<UpgradeData> { reward });
        return true;
    }

    public IReadOnlyList<UpgradeData> DebugCurrentChoices => currentChoices;

    public bool DebugSelectCurrentChoice(int index)
    {
        if (!isChoosingUpgrade || currentChoices == null || index < 0 ||
            index >= currentChoices.Count)
            return false;
        SelectUpgrade(currentChoices[index]);
        return true;
    }
#endif

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

        orbitalRewardProvider = new OrbitalRewardProvider(allUpgrades);

        if (upgradePanelView != null)
            upgradePanelView.Hide();
    }

    private void OnDisable()
    {
        if (!IsChoosingUpgrade)
            return;

        shuttingDown = true;
        if (orbitalRewardFlow != null)
            orbitalRewardFlow.CancelForSceneTransition();
        isChoosingUpgrade = false;
        hasCurrentRequest = false;
        if (upgradePanelView != null)
            upgradePanelView.Hide();

        System.Action onClosed = currentOnClosed;
        currentOnClosed = null;
        onClosed?.Invoke();

        while (pendingChoices.Count > 0)
            pendingChoices.Dequeue().OnClosed?.Invoke();

        milestoneChoices.Clear();
        milestoneLevels.Clear();
        milestoneSequenceRunning = false;
        idleCallbacks.Clear();

        RestoreRewardTimeScale();
        while (idleCallbacks.Count > 0)
            idleCallbacks.Dequeue()?.Invoke();
    }

    private void OnDestroy()
    {
        orbitalRewardProvider?.Dispose();
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
        FindFirstObjectByType<OrbitalInteractionController>()?.PrepareForExternalPause();
        UpgradeChoiceRequest request = new(
                playerLevel,
                playLevelUpSound: true,
                choicesCount,
                guaranteeBehavior: false,
                isChestReward: false
            );
        OrbitalStationRuntime station =
            FindFirstObjectByType<OrbitalStationRuntime>();
        bool requiresSequence = milestoneSequenceRunning ||
            OrbitalProgressionConfig.Default.IsRingMilestone(playerLevel);
        if (requiresSequence)
        {
            milestoneChoices.Enqueue(request);
            milestoneLevels.Enqueue(playerLevel);
            if (!milestoneSequenceRunning)
            {
                // One XP pickup can grant several levels before the first
                // coroutine receives a frame. Reserve the sequence now so
                // every milestone is handled by the same queue runner.
                milestoneSequenceRunning = true;
                StartCoroutine(FlushMilestoneChoices());
            }
            return;
        }
        station?.ProcessPlayerLevelMilestone(playerLevel);
        RequestUpgradeChoices(request);
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

    public void ShowNumericChestRewardChoices(
        int choiceCount,
        System.Action onClosed)
    {
        int playerLevel = ExperienceManager.Instance != null
            ? ExperienceManager.Instance.currentLevel
            : 1;

        RequestUpgradeChoices(
            new UpgradeChoiceRequest(
                playerLevel,
                playLevelUpSound: false,
                choiceCount,
                guaranteeBehavior: false,
                isChestReward: true,
                onClosed: onClosed,
                numericOnly: true
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

        BeginChoiceRequest(request, choices);
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

        choices = orbitalRewardProvider?.BuildChoices(request.ChoiceCount) ??
            new List<UpgradeData>();

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
            upgradePanelView.ShowWorldEventReward(
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

        if (upgrade is OrbitalRewardData orbitalReward)
        {
            SelectOrbitalReward(orbitalReward);
            return;
        }

        bool applied = TryGrantUpgrade(upgrade, out ItemGrantResult grantResult);

        if (!applied)
        {
            Debug.LogWarning(
                $"[UpgradeManager] Could not grant " +
                $"'{upgrade?.upgradeName ?? "NULL"}' ({grantResult}). " +
                "Refreshing eligible choices."
            );
            RefreshChoicesAfterGrantFailure();
            return;
        }

        CloseUpgradeSelection();
    }

    private void SelectOrbitalReward(OrbitalRewardData reward)
    {
        if (reward.BodyUpgrade != null)
        {
            if (TryGrantUpgrade(reward.BodyUpgrade,
                    out ItemGrantResult bodyResult))
                CloseUpgradeSelection();
            else
            {
                Debug.LogWarning($"[OrbitalRewards] Subject reward failed: {bodyResult}.");
                RefreshChoicesAfterGrantFailure();
            }
            return;
        }

        OrbitalStationRuntime station =
            FindFirstObjectByType<OrbitalStationRuntime>();
        if (station == null || station.RewardFlow == null)
        {
            RefreshChoicesAfterGrantFailure();
            return;
        }
        upgradePanelView.Hide();
        orbitalRewardFlow = station.RewardFlow;
        bool started = orbitalRewardFlow.Begin(reward,
            CloseUpgradeSelection, ReturnToCurrentChoices);
        if (!started && isChoosingUpgrade)
            RefreshChoicesAfterGrantFailure();
    }

    private void ReturnToCurrentChoices()
    {
        orbitalRewardFlow = null;
        if (shuttingDown || !isChoosingUpgrade)
            return;
        if (currentChoices != null && currentChoices.Count > 0)
            ShowChoiceRequest(currentRequest, currentChoices);
        else
            RefreshChoicesAfterGrantFailure();
    }

    private void RefreshChoicesAfterGrantFailure()
    {
        if (hasCurrentRequest &&
            TryBuildChoices(currentRequest, out List<UpgradeData> choices))
        {
            currentChoices = choices;
            ShowChoiceRequest(currentRequest, choices);
            return;
        }

        Debug.LogWarning(
            "[UpgradeManager] No eligible upgrades remain after grant failure. " +
            "Closing the selection without leaving the game paused."
        );
        CloseUpgradeSelection();
    }

    private bool TryGrantUpgrade(
        UpgradeData upgrade,
        out ItemGrantResult grantResult)
    {
        grantResult = ItemGrantResult.Invalid;

        if (upgradeApplier == null)
        {
            Debug.LogError("[UpgradeManager] UpgradeApplier is not assigned.");
            return false;
        }

        RunStateManager runState = RunStateManager.EnsureExists();

        if (!UpgradeEligibilityRules.IsWeaponCompatible(
                upgrade,
                WeaponUpgradeCapabilityResolver.GetCurrentCapabilities()))
        {
            grantResult = ItemGrantResult.IncompatibleWeapon;
            return false;
        }

        if (UpgradeEligibilityRules.HasExclusiveConflict(
                upgrade,
                runState.ItemSlots))
        {
            grantResult = ItemGrantResult.ExclusiveConflict;
            return false;
        }

        if (!runState.ItemSlots.CanAccept(upgrade))
        {
            grantResult = runState.ItemSlots.GetLevel(upgrade) >=
                RunItemSlots.MaxItemLevel
                ? ItemGrantResult.MaxLevel
                : ItemGrantResult.RequiresReplacement;
            return false;
        }

        int nextLevel = runState.ItemSlots.GetLevel(upgrade) + 1;
        if (!upgradeApplier.Apply(upgrade, nextLevel))
            return false;

        grantResult = runState.ItemSlots.TryAdd(upgrade);

        if (grantResult != ItemGrantResult.Added &&
            grantResult != ItemGrantResult.LeveledUp)
        {
            Debug.LogError(
                $"[UpgradeManager] Slot state changed while granting " +
                $"'{upgrade.upgradeName}' ({grantResult})."
            );
            return false;
        }

        runState.RegisterUpgrade(upgrade);
        return true;
    }

    private void CloseUpgradeSelection()
    {
        if (upgradePanelView != null)
            upgradePanelView.Hide();

        isChoosingUpgrade = false;
        orbitalRewardFlow = null;
        hasCurrentRequest = false;
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
            currentRequest = nextRequest;
            hasCurrentRequest = true;
            currentOnClosed = nextRequest.OnClosed;
            currentChoices = choices;
            ShowChoiceRequest(nextRequest, choices);
            return;
        }

        RestoreRewardTimeScale();
        InvokeIdleCallbacksIfReady();
    }

    private void RestoreRewardTimeScale()
    {
        PauseMenuUI pause = FindFirstObjectByType<PauseMenuUI>();
        if (pause == null || !pause.IsPaused) Time.timeScale = previousTimeScale;
    }

    private void BeginChoiceRequest(UpgradeChoiceRequest request,
        List<UpgradeData> choices)
    {
        FindFirstObjectByType<OrbitalInteractionController>()?.PrepareForExternalPause();
        shuttingDown = false;
        isChoosingUpgrade = true;
        previousTimeScale = Time.timeScale;
        Time.timeScale = 0f;
        currentRequest = request;
        hasCurrentRequest = true;
        currentOnClosed = request.OnClosed;
        currentChoices = choices;
        ShowChoiceRequest(request, choices);
    }

    private IEnumerator FlushMilestoneChoices()
    {
        // Let ExperienceManager finish a possible multi-level while-loop first.
        yield return null;
        while (milestoneLevels.Count > 0)
        {
            int level = milestoneLevels.Dequeue();
            OrbitalStationRuntime station =
                FindFirstObjectByType<OrbitalStationRuntime>();
            bool addedRing = station != null &&
                station.ProcessPlayerLevelMilestone(level);
            if (!addedRing)
                continue;
            float elapsed = 0f;
            while (elapsed < 0.45f)
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
        }
        milestoneSequenceRunning = false;
        while (milestoneChoices.Count > 0)
            RequestUpgradeChoices(milestoneChoices.Dequeue());
        InvokeIdleCallbacksIfReady();
    }

    private void InvokeIdleCallbacksIfReady()
    {
        if (isChoosingUpgrade || pendingChoices.Count > 0 ||
            milestoneSequenceRunning || milestoneChoices.Count > 0)
            return;

        while (idleCallbacks.Count > 0)
            idleCallbacks.Dequeue()?.Invoke();
    }

}
