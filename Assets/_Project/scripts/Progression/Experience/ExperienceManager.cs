using UnityEngine;
using UnityEngine.Events;

public class ExperienceManager : MonoBehaviour
{
    public static ExperienceManager Instance;

    [Header("Level Settings")]
    public LevelData levelData;
    public int currentLevel = 1;
    public int currentExp = 0;

    [Header("Rare enemy drops (independent probabilities)")]
    [SerializeField] private RarePickup medKitPrefab;
    [SerializeField, Range(0f, 1f)] private float medKitDropChance = 0.004f;
    [SerializeField] private RarePickup magnetPrefab;
    [SerializeField, Range(0f, 1f)] private float magnetDropChance = 0.0015f;

    [Header("Global XP magnet")]
    [SerializeField, Min(0.01f)] private float magnetDuration = 30f;
    [SerializeField, Min(0f)] private float globalMagnetSpeed = 20f;
    [Tooltip("Distant XP moves faster so it can cross the map in a few seconds.")]
    [SerializeField, Min(0.05f)] private float globalMagnetTravelTime = 1.5f;

    [Header("Healing feedback (existing DamagePopup prefab)")]
    [SerializeField] private DamagePopup healingPopupPrefab;
    private SimplePrefabPool healingPopupPool;
    private PlayerHealth magnetPlayer;
    private RunStateManager boundRun;
    private bool sceneReleased;
    public float MagnetSecondsRemaining { get; private set; }
    public bool IsGlobalMagnetActive => MagnetSecondsRemaining > 0f &&
        magnetPlayer != null && !magnetPlayer.IsDead && CanUseRarePickups(gameObject.scene);

    public bool CanUseRarePickups(UnityEngine.SceneManagement.Scene scene) =>
        !sceneReleased && isActiveAndEnabled && scene == gameObject.scene &&
        RunStateManager.Instance != null && !RunStateManager.Instance.IsRunEnded &&
        RunFlowController.Instance != null && RunFlowController.Instance.gameObject.scene == scene &&
        RunFlowController.Instance.Phase != RunPhase.Stopped &&
        RunFlowController.Instance.Phase != RunPhase.Victory;

    public float GetGlobalMagnetSpeed(float distance) =>
        Mathf.Max(globalMagnetSpeed, distance / Mathf.Max(0.05f, globalMagnetTravelTime));

    public void TryDropRareLoot(EnemyHealth enemy)
    {
        if (enemy == null || enemy.IsBoss || !CanUseRarePickups(enemy.gameObject.scene)) return;
        // Draw twice: either, both or neither can drop, independently of ordinary XP.
        bool dropMedKit = GameplayRandom.DropRandom.value < medKitDropChance;
        bool dropMagnet = GameplayRandom.DropRandom.value < magnetDropChance;
        if (dropMedKit) SpawnRarePickup(medKitPrefab, enemy.transform.position);
        if (dropMagnet) SpawnRarePickup(magnetPrefab, enemy.transform.position);
    }

    private RarePickup SpawnRarePickup(RarePickup prefab, Vector3 position) =>
        prefab != null ? Instantiate(prefab, position, Quaternion.identity) : null;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    public bool DebugHasRarePickup(RarePickup.PickupKind kind) =>
        (kind == RarePickup.PickupKind.MedKit ? medKitPrefab : magnetPrefab) != null;

    public RarePickup DebugSpawnRarePickup(RarePickup.PickupKind kind, Vector3 position) =>
        CanUseRarePickups(gameObject.scene)
            ? SpawnRarePickup(kind == RarePickup.PickupKind.MedKit ? medKitPrefab : magnetPrefab, position)
            : null;
#endif

    public void ActivateGlobalMagnet(PlayerHealth player)
    {
        if (player == null || player.IsDead || !CanUseRarePickups(player.gameObject.scene)) return;
        if (magnetPlayer != player)
        {
            ClearGlobalMagnet();
            magnetPlayer = player;
            magnetPlayer.Died += ClearGlobalMagnet;
        }
        MagnetSecondsRemaining += Mathf.Max(0f, magnetDuration);
    }

    public void ClearGlobalMagnet()
    {
        if (magnetPlayer != null) magnetPlayer.Died -= ClearGlobalMagnet;
        magnetPlayer = null;
        MagnetSecondsRemaining = 0f;
    }

    public void ShowHealing(PlayerHealth player, float amount)
    {
        if (healingPopupPrefab == null || amount <= 0f) return;
        healingPopupPool ??= new SimplePrefabPool(this, healingPopupPrefab.gameObject, 2, 4);
        var popup = healingPopupPool.Get(player.transform.position + Vector3.up * 2f, Quaternion.identity);
        popup.DamagePopup.SetHealing(amount);
    }

    private void Update()
    {
        if (MagnetSecondsRemaining <= 0f) return;
        if (!IsGlobalMagnetActive) { ClearGlobalMagnet(); return; }
        MagnetSecondsRemaining = Mathf.Max(0f, MagnetSecondsRemaining - Time.deltaTime);
        if (MagnetSecondsRemaining <= 0f) ClearGlobalMagnet();
    }

    private void ReleaseRunScene() { sceneReleased = true; ClearGlobalMagnet(); }
    private void OnDisable() => ClearGlobalMagnet();

    private int expToNextLevel;
    private float xpGainMultiplier = 1f;
    private float levelXpGainMultiplier = 1f;
    private float anomalyXpGainMultiplier = 1f;
    private float runUpgradeXpGainMultiplier = 1f;
    private SimplePrefabPool pickupEffectPool;
    private float nextPickupEffectTime;

    public UnityEvent<int, int> OnExperienceChanged;
    public UnityEvent<int> OnLevelUp;

    public int CurrentLevel => currentLevel;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
    public event System.Action<int> DebugExperienceAdded;
#endif

    public void PlayPickupEffect(ExperiencePickupEffect prefab, Vector3 position, Color color)
    {
        // Coalesce simultaneous absorption: at most ten short effects alive at once.
        if (Time.time < nextPickupEffectTime) return;
        nextPickupEffectTime = Time.time + 1f / 60f;
        pickupEffectPool ??= new SimplePrefabPool(this, prefab.gameObject, 10, 10);
        var item = pickupEffectPool.Get(position, Quaternion.identity);
        item.GetComponent<ExperiencePickupEffect>().Play(color);
        item.ReleaseAfter(ExperiencePickupEffect.Duration);
    }

    private void OnDestroy()
    {
        pickupEffectPool?.Dispose();
        healingPopupPool?.Dispose();
        ClearGlobalMagnet();
        if (boundRun != null) boundRun.UnregisterSceneCleanup(ReleaseRunScene);
        if (Instance == this) Instance = null;
    }
    public int CurrentExp => currentExp;
    public int ExpToNextLevel => expToNextLevel;

    private void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
            Destroy(gameObject);

    }

    private void Start()
    {
        boundRun = RunStateManager.Instance;
        boundRun?.RegisterSceneCleanup(ReleaseRunScene);
        expToNextLevel = GetRequiredExpForLevel(currentLevel);

        if (RunStateManager.Instance != null)
            RunStateManager.Instance.ApplyToExperienceManager(this);

        // A fresh run has no snapshot to restore, but still needs its initial XP display.
        UpdateExperienceHUD();
    }

    public void AddExperience(int amount)
    {
        if (levelData == null)
        {
            return;
        }

        int gained = Mathf.RoundToInt(
            amount *
            xpGainMultiplier *
            runUpgradeXpGainMultiplier *
            levelXpGainMultiplier *
            anomalyXpGainMultiplier
        );
        if (TutorialController.IsTutorialSector)
        {
            // Exactly one level-up, triggered by the first real pickup callback.
            // Extra loot must never queue a second reward or overflow into Sector 1.
            if (!TutorialController.IsActive || currentLevel != 1) return;
            gained = TutorialController.Active.Step == TutorialStep.FirstReward
                ? Mathf.Max(0, expToNextLevel - currentExp)
                : Mathf.Min(gained, Mathf.Max(0, expToNextLevel - currentExp - 1));
        }
        currentExp += gained;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        DebugExperienceAdded?.Invoke(gained);
#endif

        while (currentExp >= expToNextLevel && currentLevel < levelData.maxLevel)
        {
            currentExp -= expToNextLevel;
            LevelUp();
        }

        UpdateExperienceHUD();
        OnExperienceChanged?.Invoke(currentExp, expToNextLevel);


    }

    private void LevelUp()
    {
        currentLevel++;
        expToNextLevel = GetRequiredExpForLevel(currentLevel);
        OnLevelUp?.Invoke(currentLevel);

        if (UpgradeManager.Instance != null)
            UpgradeManager.Instance.ShowLevelUpChoices(currentLevel);
        else
            AudioService.Instance?.Play(AudioCueId.LevelUp);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log("Level up! Now level " + currentLevel);
#endif
    }

    private void UpdateExperienceHUD()
    {
        HUDManager.Instance?.SetExperience(
            currentExp,
            expToNextLevel,
            currentLevel
        );
    }

    private int GetRequiredExpForLevel(int level)
    {
        if (levelData == null)
            return 100;

        return Mathf.FloorToInt(
            levelData.baseExpToNextLevel * Mathf.Pow(levelData.expGrowth, level - 1)
        );
    }

    public int GetRequiredExpForCurrentLevel()
    {
        return GetRequiredExpForLevel(currentLevel);
    }

    public void AddXpGainPercent(float percent)
    {
        xpGainMultiplier *= 1f + percent;
    }
    public void SetLevelXpGainMultiplier(float multiplier)
    {
        levelXpGainMultiplier = Mathf.Max(0.1f, multiplier);
    }
    public void SetAnomalyXpGainMultiplier(float multiplier)
    {
        anomalyXpGainMultiplier = Mathf.Max(0.1f, multiplier);
    }
    public void SetRunUpgradeXpGainMultiplier(float multiplier)
    {
        runUpgradeXpGainMultiplier = Mathf.Max(0.1f, multiplier);
    }
    public float RunUpgradeXpGainMultiplier => runUpgradeXpGainMultiplier;
    public void RestoreRuntimeExperience(int level, int exp)
    {
        currentLevel = Mathf.Max(1, level);
        currentExp = Mathf.Max(0, exp);

        expToNextLevel = GetRequiredExpForLevel(currentLevel);

        UpdateExperienceHUD();
        OnExperienceChanged?.Invoke(currentExp, expToNextLevel);
    }
}
