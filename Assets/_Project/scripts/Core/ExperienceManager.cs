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

    public UnityEvent<int, int> OnExperienceChanged;
    public UnityEvent<int> OnLevelUp;

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
        OnExperienceChanged?.Invoke(currentExp, expToNextLevel);
    }

    public void AddExperience(int amount)
    {
        if (levelData == null)
        {
            Debug.LogWarning("ExperienceManager: levelData is not assigned.");
            return;
        }

        currentExp += amount;

        while (currentExp >= expToNextLevel && currentLevel < levelData.maxLevel)
        {
            currentExp -= expToNextLevel;
            LevelUp();
        }

        OnExperienceChanged?.Invoke(currentExp, expToNextLevel);
    }

    private void LevelUp()
    {
        currentLevel++;
        expToNextLevel = GetRequiredExpForLevel(currentLevel);

        OnLevelUp?.Invoke(currentLevel);

        if (UpgradeManager.Instance != null)
        {
            UpgradeManager.Instance.ShowUpgradeChoices();
        }
        else
        {
            Debug.LogWarning("ExperienceManager: UpgradeManager not found.");
        }

        Debug.Log("Level up! Now level " + currentLevel);
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
}