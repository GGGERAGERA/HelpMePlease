using UnityEngine;
using UnityEngine.Events;

public class ExperienceManager : MonoBehaviour
{
    public static ExperienceManager Instance;

    [Header("Level Settings")]
    public LevelData levelData;
    public int currentLevel = 1;
    public int currentExp = 0;

    private int expToNextLevel;
    private float xpGainMultiplier = 1f;
    private float levelXpGainMultiplier = 1f;
    private float anomalyXpGainMultiplier = 1f;
    private float runUpgradeXpGainMultiplier = 1f;

    public UnityEvent<int, int> OnExperienceChanged;
    public UnityEvent<int> OnLevelUp;

    public int CurrentLevel => currentLevel;
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
        expToNextLevel = GetRequiredExpForLevel(currentLevel);

        if (RunStateManager.Instance != null)
            RunStateManager.Instance.ApplyToExperienceManager(this);
        else
            UpdateExperienceHUD();
    }

    public void AddExperience(int amount)
    {
        if (levelData == null)
        {
            return;
        }

        currentExp += Mathf.RoundToInt(
            amount *
            xpGainMultiplier *
            runUpgradeXpGainMultiplier *
            levelXpGainMultiplier *
            anomalyXpGainMultiplier
        );

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
