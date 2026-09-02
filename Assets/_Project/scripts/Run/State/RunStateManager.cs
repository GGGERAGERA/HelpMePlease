using System.Collections.Generic;
using UnityEngine;
using Subject42.Combat.OrbitalStation;

/// <summary>
/// Persistent state of the current run. Stores data, not scene objects.
/// New gameplay scenes can be reloaded safely, then this state is applied to the newly spawned player.
/// </summary>
public sealed class RunStateManager : MonoBehaviour
{
    public event System.Action EvolutionChanged;
    public static RunStateManager Instance { get; private set; }
    public event System.Action CurrentRewardChanged;

    public CharacterData SelectedCharacter { get; private set; }
    public WeaponData SelectedWeapon { get; private set; }
    public AnomalyStabilizerData CurrentAnomalyStabilizer { get; private set; }
    public AnomalyRunModifiers AnomalyModifiers { get; private set; } =
        AnomalyRunModifiers.None;
    public int CurrentLevel { get; private set; } = 1;

    public RunSector CurrentSector { get; private set; }

    private readonly List<UpgradeData> pickedUpgrades = new();
    private readonly RunItemSlots itemSlots = new();
    private readonly AnomalyInventory anomalyInventory = new();
    private readonly EvolutionState evolutionState = new();

    private float threatValue;
    private float threatElapsedTime;

    private bool hasHealthSnapshot;
    private float savedCurrentHealth;
    private float savedMaxHealth;

    private bool hasExperienceSnapshot;
    private int savedXpLevel = 1;
    private int savedCurrentExp;

    private bool upgradesAppliedToCurrentScene;

    private int accumulatedKills;
    private float accumulatedKillRewardUnits;
    private float accumulatedRunTime;
    private int completedLevels;
    private float completedLevelRewardMultiplierTotal;
    private int lastCompletedSectorNumber;

    private RunStatsManager lastCommittedStats;
    private bool runEnded;

    private RunSummary lastRunSummary;
    private int orbitalRunSequence;
    private bool warnedInvalidOrbitalState;

    public OrbitalRunState OrbitalStationState { get; private set; }

    private StageProfileData startingStageProfile;
    private WorldRuleData startingWorldRule;
    private LocalAnomalyData startingLocalAnomaly;

    public IReadOnlyList<UpgradeData> PickedUpgrades => pickedUpgrades;
    public RunItemSlots ItemSlots => itemSlots;
    public AnomalyInventory AnomalyInventory => anomalyInventory;
    public EvolutionState EvolutionState => evolutionState;
    public EvolutionDefinition CurrentEvolution =>
        evolutionState.CurrentEvolution;
    public bool HasEvolution => evolutionState.HasEvolution;
    public float ThreatValue => threatValue;
    public float ThreatElapsedTime => threatElapsedTime;

    public int AccumulatedKills => accumulatedKills;
    public float AccumulatedRunTime => accumulatedRunTime;
    public int CompletedLevels => completedLevels;
    public bool IsRunEnded => runEnded;

    public int GetCurrentRunKills()
    {
        int kills = accumulatedKills;

        if (TryGetUncommittedCurrentSceneStats(out RunStatsManager stats))
            kills += stats.Kills;

        return kills;
    }

    public float GetCurrentRunTime()
    {
        float runTime = accumulatedRunTime;

        if (TryGetUncommittedCurrentSceneStats(out RunStatsManager stats))
            runTime += stats.RunTime;

        return runTime;
    }

    public int GetCurrentGoldReward(RunEndReason endReason)
    {
        float killRewardUnits = accumulatedKillRewardUnits;
        float runTime = accumulatedRunTime;

        if (TryGetUncommittedCurrentSceneStats(out RunStatsManager stats))
        {
            killRewardUnits += stats.KillRewardUnits;
            runTime += stats.RunTime;
        }

        return RunRewardCalculator.CalculateGold(
            killRewardUnits,
            runTime,
            completedLevelRewardMultiplierTotal,
            endReason
        );
    }

    private bool TryGetUncommittedCurrentSceneStats(
        out RunStatsManager stats)
    {
        stats = RunStatsManager.Instance;
        return stats != null &&
            !ReferenceEquals(stats, lastCommittedStats);
    }

    public static RunStateManager EnsureExists()
    {
        if (Instance != null)
            return Instance;

        GameObject go = new GameObject("RunStateManager");
        return go.AddComponent<RunStateManager>();
    }

    public void SetCurrentSector(RunSector sector)
    {
        CurrentSector = sector;

        if (sector != null)
            CurrentLevel = Mathf.Max(1, sector.SectorNumber);
    }

    public void ClearCurrentSector()
    {
        CurrentSector = null;
    }

    public OrbitalRunState EnsureOrbitalRunState()
    {
        if (OrbitalStationState != null &&
            OrbitalStationState.Validate(out _))
        {
            return OrbitalStationState;
        }

        if (!warnedInvalidOrbitalState)
        {
            warnedInvalidOrbitalState = true;
            string error = "state is missing";
            OrbitalStationState?.Validate(out error);
            Debug.LogWarning(
                $"[OrbitalRunState] Invalid state ({error}); rebuilding default.");
        }

        orbitalRunSequence++;
        OrbitalStationState = OrbitalRunState.CreateDefault(orbitalRunSequence);
        return OrbitalStationState;
    }

    public OrbitalRunState ResetOrbitalRunState()
    {
        orbitalRunSequence++;
        warnedInvalidOrbitalState = false;
        OrbitalStationState = OrbitalRunState.CreateDefault(orbitalRunSequence);
        return OrbitalStationState;
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public void BeginNewRun(CharacterData character, WeaponData weapon)
    {
        BeginNewRunInternal(character, weapon, null);
    }

    private void BeginNewRunInternal(
        CharacterData character,
        WeaponData weapon,
        AnomalyStabilizerData anomalyStabilizer)
    {
        CombatModeState.LatchForNewRun();
        if (CombatModeState.ActiveRunMode == CombatMode.Orbital)
            ResetOrbitalRunState();
        else
        {
            OrbitalStationState = null;
            warnedInvalidOrbitalState = false;
        }
        FindFirstObjectByType<DoubleOrLeave>()?.ResetState();

        ClearCurrentSector();
        SelectedCharacter = character;
        SelectedWeapon = weapon;
        CurrentAnomalyStabilizer = anomalyStabilizer;
        AnomalyModifiers = AnomalyRunModifiers.From(anomalyStabilizer);
        CurrentLevel = 1;

        pickedUpgrades.Clear();
        itemSlots.Clear();
        anomalyInventory.Clear();
        evolutionState.Clear();
        threatValue = 0f;
        threatElapsedTime = 0f;

        ClearExperienceSnapshot();
        ClearHealthSnapshot();

        accumulatedKills = 0;
        accumulatedKillRewardUnits = 0f;
        accumulatedRunTime = 0f;
        completedLevels = 0;
        completedLevelRewardMultiplierTotal = 0f;
        lastCompletedSectorNumber = 0;

        // Restart can begin before the current gameplay scene is unloaded.
        // Treat its scene-local stats as belonging to the previous run until
        // the replacement scene publishes a new RunStatsManager instance.
        lastCommittedStats = RunStatsManager.Instance;
        runEnded = false;
        lastRunSummary = null;

        upgradesAppliedToCurrentScene = false;
        CreateStartingSector();
        CurrentRewardChanged?.Invoke();

        Debug.Log(
            $"[RunState] New run: " +
            $"character={GetName(character)}, " +
            $"weapon={GetName(weapon)}, " +
            $"stabilizer={GetName(anomalyStabilizer)}"
        );
    }

    public void SavePlayerState(GameObject player)
    {
        upgradesAppliedToCurrentScene = false;
        if (player == null)
        {
            Debug.LogWarning("[RunState] Save skipped: player is null.");
            return;
        }

        PlayerHealth health = player.GetComponent<PlayerHealth>();

        if (health == null)
        {
            Debug.LogWarning("[RunState] Save skipped: PlayerHealth not found.");
            return;
        }

        savedCurrentHealth = health.CurrentHealth;
        savedMaxHealth = health.MaxHealth;
        hasHealthSnapshot = true;

        Debug.Log($"[RunState] Saved player health: {savedCurrentHealth}/{savedMaxHealth}");
    }

    public void AdvanceLevel()
    {
        CurrentLevel++;
        Debug.Log($"[RunState] Advanced to level {CurrentLevel}");
    }

    public void ApplyToSpawnedPlayer(GameObject player, UpgradeApplier upgradeApplier)
    {
        if (player == null)
            return;

        if (!upgradesAppliedToCurrentScene)
        {
            if (upgradeApplier != null)
            {
                Dictionary<UpgradeData, int> restoredLevels = new();
                foreach (UpgradeData upgrade in pickedUpgrades)
                {
                    restoredLevels.TryGetValue(upgrade, out int level);
                    level++;
                    restoredLevels[upgrade] = level;
                    upgradeApplier.Apply(upgrade, level);
                }
            }
            else if (pickedUpgrades.Count > 0)
            {
                Debug.LogWarning("[RunState] UpgradeApplier not assigned. Upgrades were not restored.");
            }

            upgradesAppliedToCurrentScene = true;
        }

        if (hasHealthSnapshot)
        {
            PlayerHealth health = player.GetComponent<PlayerHealth>();

            if (health != null)
                health.SetRuntimeHealth(savedMaxHealth, savedCurrentHealth);
        }

        Debug.Log($"[RunState] Applied to spawned player. Upgrades: {pickedUpgrades.Count}, hasHealth: {hasHealthSnapshot}");
    }

    private void ClearHealthSnapshot()
    {
        hasHealthSnapshot = false;
        savedCurrentHealth = 0f;
        savedMaxHealth = 0f;
    }

    private string GetName(Object obj)
    {
        return obj != null ? obj.name : "NULL";
    }

    public void SaveExperienceState()
    {
        ExperienceManager experience = ExperienceManager.Instance;

        if (experience == null)
        {
            Debug.LogWarning("[RunState] XP save skipped: ExperienceManager not found.");
            return;
        }

        savedXpLevel = experience.CurrentLevel;
        savedCurrentExp = experience.CurrentExp;
        hasExperienceSnapshot = true;

        Debug.Log($"[RunState] Saved XP: level={savedXpLevel}, exp={savedCurrentExp}");
    }

    public void ApplyToExperienceManager(ExperienceManager experience)
    {
        if (experience == null)
            return;

        int xpGainLevel = itemSlots.GetLevel(UpgradeType.XpGainPercent);
        experience.SetRunUpgradeXpGainMultiplier(
            xpGainLevel > 0
                ? ProductionUpgradeProfiles.XpGainMultiplier(xpGainLevel)
                : 1f);

        if (!hasExperienceSnapshot)
            return;

        experience.RestoreRuntimeExperience(savedXpLevel, savedCurrentExp);

        Debug.Log($"[RunState] Applied XP: level={savedXpLevel}, exp={savedCurrentExp}");
    }

    private void ClearExperienceSnapshot()
    {
        hasExperienceSnapshot = false;
        savedXpLevel = 1;
        savedCurrentExp = 0;
    }
    public void RegisterUpgrade(UpgradeData upgrade)
    {
        if (upgrade == null)
            return;

        pickedUpgrades.Add(upgrade);

        Debug.Log($"[RunState] Registered upgrade: {upgrade.upgradeName}. Total: {pickedUpgrades.Count}");
    }

    public void AdvanceThreat(float deltaTime, float valuePerSecond)
    {
        if (runEnded || deltaTime <= 0f)
            return;

        threatElapsedTime += deltaTime;
        threatValue = Mathf.Clamp(
            threatValue + deltaTime * Mathf.Max(0f, valuePerSecond),
            0f,
            100f
        );
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    public void SetSelectedWeaponForDebug(WeaponData weapon)
    {
        if (!runEnded && weapon != null)
            SelectedWeapon = weapon;
    }

    public bool TrySetUpgradeLevelForDebug(
        UpgradeData upgrade,
        int targetLevel,
        UpgradeApplier upgradeApplier)
    {
        if (runEnded || upgrade == null || upgradeApplier == null ||
            !itemSlots.TrySetLevelForDebug(upgrade, targetLevel))
        {
            return false;
        }

        pickedUpgrades.RemoveAll(item => item == upgrade);
        for (int i = 0; i < targetLevel; i++)
            pickedUpgrades.Add(upgrade);

        upgradeApplier.ApplyDebugProductionBuild(itemSlots);
        return true;
    }

    public void ClearUpgradesForDebug(UpgradeApplier upgradeApplier)
    {
        pickedUpgrades.Clear();
        itemSlots.Clear();
        upgradeApplier?.ApplyDebugProductionBuild(itemSlots);
    }

    public void SetThreatForDebug(float value)
    {
        if (runEnded)
            return;

        threatValue = Mathf.Clamp(value, 0f, 100f);
    }
#endif

    public AnomalyGrantResult TryGrantAnomalyItem(AnomalyItemData item)
    {
        AnomalyGrantResult result = runEnded
            ? AnomalyGrantResult.Invalid
            : anomalyInventory.TryGrant(item);

        Debug.Log(
            $"[RunState] Anomaly grant '{(item != null ? item.Id : "NULL")}' " +
            $"=> {result}. Level: {anomalyInventory.Level}."
        );

        if (result == AnomalyGrantResult.Accepted ||
            result == AnomalyGrantResult.Upgraded)
        {
            AnomalyPowerRuntime.EnsurePower(
                GameObject.FindGameObjectWithTag("Player"),
                anomalyInventory.CurrentItem.PowerType,
                anomalyInventory.Level);
        }

        return result;
    }

    public void ClearAnomalyItem()
    {
        if (runEnded || anomalyInventory.IsEmpty)
            return;

        AnomalyPowerRuntime.DeactivatePower(
            GameObject.FindGameObjectWithTag("Player"),
            anomalyInventory.CurrentItem.PowerType);
        anomalyInventory.Clear();
    }

    public void ResolveCurrentEvolution(
        IReadOnlyList<EvolutionRecipe> recipes)
    {
        ResolveCurrentEvolution(SelectedWeapon, recipes);
    }

    public void ResolveCurrentEvolution(
        WeaponData activeWeapon,
        IReadOnlyList<EvolutionRecipe> recipes)
    {
        EvolutionDefinition previous = evolutionState.CurrentEvolution;
        evolutionState.Refresh(activeWeapon, anomalyInventory, recipes);

        if (previous != evolutionState.CurrentEvolution)
            EvolutionChanged?.Invoke();
    }

    [ContextMenu("Debug/Clear Item Slots")]
    private void DebugClearItemSlots()
    {
        itemSlots.Clear();
    }

    [ContextMenu("Debug/Log Item Slots")]
    private void DebugLogItemSlots()
    {
        IReadOnlyList<RunItemSlot> slots = itemSlots.Slots;

        for (int i = 0; i < slots.Count; i++)
        {
            RunItemSlot slot = slots[i];
            string itemName = slot.Item != null ? slot.Item.upgradeName : "Empty";
            Debug.Log($"[RunState] Item slot {i}: {itemName}, level {slot.Level}", this);
        }
    }

    public void RegisterCompletedLevel()
    {
        if (runEnded)
            return;

        if (CurrentSector == null)
        {
            Debug.LogError(
                "[RunState] Cannot register sector completion: " +
                "CurrentSector is missing."
            );
            return;
        }

        int sectorNumber = CurrentSector.SectorNumber;

        if (lastCompletedSectorNumber == sectorNumber)
        {
            Debug.LogWarning(
                $"[RunState] Sector {sectorNumber} completion was already registered."
            );
            return;
        }

        completedLevels++;
        completedLevelRewardMultiplierTotal +=
            CurrentSector.CompletionGoldMultiplier;
        lastCompletedSectorNumber = sectorNumber;
        CurrentRewardChanged?.Invoke();

        Debug.Log(
            $"[RunState] Completed sector {sectorNumber}. " +
            $"Completed levels: {completedLevels}, " +
            $"reward total x{completedLevelRewardMultiplierTotal:F2}"
        );
    }

    public void CommitCurrentSceneStats()
    {
        if (runEnded)
            return;

        RunStatsManager stats = RunStatsManager.Instance;

        if (stats == null)
        {
            Debug.LogWarning(
                "[RunState] RunStatsManager is missing. " +
                "Current scene stats were not committed."
            );

            return;
        }

        if (ReferenceEquals(lastCommittedStats, stats))
        {
            Debug.Log(
                "[RunState] Current scene stats were already committed."
            );

            return;
        }

        accumulatedKills += stats.Kills;
        accumulatedKillRewardUnits += stats.KillRewardUnits;
        accumulatedRunTime += stats.RunTime;
        lastCommittedStats = stats;
        CurrentRewardChanged?.Invoke();

        Debug.Log(
            $"[RunState] Scene stats committed. " +
            $"Total kills={accumulatedKills}, " +
            $"time={accumulatedRunTime:F1}, " +
            $"levels={completedLevels}"
        );
    }

    public RunSummary EndRun(RunEndReason reason)
    {
        if (runEnded)
            return lastRunSummary;

        CommitCurrentSceneStats();

        int goldEarned = GetCurrentGoldReward(reason);

        CurrencyManager.Instance?.AddGold(goldEarned);

        lastRunSummary = new RunSummary(
            reason,
            completedLevels,
            accumulatedKills,
            accumulatedRunTime,
            goldEarned
        );

        runEnded = true;
        CurrentAnomalyStabilizer = null;
        AnomalyModifiers = AnomalyRunModifiers.None;
        anomalyInventory.Clear();
        evolutionState.Clear();
        threatValue = 0f;
        threatElapsedTime = 0f;
        OrbitalStationState = null;
        warnedInvalidOrbitalState = false;

        Debug.Log(
            $"[RunState] Run ended. " +
            $"Reason={reason}, " +
            $"levels={completedLevels}, " +
            $"kills={accumulatedKills}, " +
            $"time={accumulatedRunTime:F1}, " +
            $"gold={goldEarned}"
        );

        return lastRunSummary;
    }

    public bool TryConsumeLastRunSummary(out RunSummary summary)
    {
        summary = lastRunSummary;

        if (summary == null)
            return false;

        lastRunSummary = null;
        return true;
    }

    public void BeginNewRun(
        CharacterData character,
        WeaponData weapon,
        StageProfileData stageProfile,
        WorldRuleData worldRule,
        LocalAnomalyData localAnomaly)
    {
        startingStageProfile = stageProfile;
        startingWorldRule = worldRule;
        startingLocalAnomaly = localAnomaly;
        BeginNewRun(character, weapon);
    }

    public void BeginNewRun(
        CharacterData character,
        WeaponData weapon,
        StageProfileData stageProfile,
        WorldRuleData worldRule,
        LocalAnomalyData localAnomaly,
        AnomalyStabilizerData anomalyStabilizer)
    {
        startingStageProfile = stageProfile;
        startingWorldRule = worldRule;
        startingLocalAnomaly = localAnomaly;
        BeginNewRunInternal(character, weapon, anomalyStabilizer);
    }

    private void CreateStartingSector()
    {
        if (startingStageProfile == null ||
            startingWorldRule == null ||
            startingLocalAnomaly == null)
        {
            Debug.LogError(
                "[RunState] Starting sector configuration is missing. " +
                "CurrentSector was not created."
            );
            return;
        }

        if (startingStageProfile.SectorNumber != 1)
        {
            Debug.LogError(
                $"[RunState] Starting StageProfile " +
                $"'{startingStageProfile.name}' is not sector 1."
            );
            return;
        }

        SetCurrentSector(new RunSector(
            1,
            startingStageProfile,
            startingWorldRule,
            startingLocalAnomaly
        ));
    }

    public void ClearFinishedRunCompatibilityState()
    {
        if (!runEnded)
            return;

        ClearCurrentSector();
        CurrentLevel = 1;
    }
}
