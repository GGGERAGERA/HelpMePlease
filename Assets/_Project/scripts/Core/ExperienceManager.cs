using UnityEngine;
using UnityEngine.Events;

public class ExperienceManager : MonoBehaviour
{

    public static ExperienceManager Instance;

    public LevelData levelData;
    public int currentLevel = 1;
    public int currentExp;
    private int expToNextLevel;

    // События для UI
    public UnityEvent<int, int> OnExperienceChanged; // (currentExp, requieredExp)
    public UnityEvent<int> OnLevelUp; // (newLevel)

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
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
        currentExp += amount;
        // Проверяем, не хватило ли опыта для повышения уровня
        while (currentExp >= expToNextLevel && currentLevel < levelData.maxLevel)
        {
            currentExp -= expToNextLevel;
            LevelUp();
        }
        OnExperienceChanged?.Invoke(currentExp, expToNextLevel);
    }
    void LevelUp()
    {
        currentLevel++;
        expToNextLevel = GetRequiredExpForLevel(currentLevel);

        // Находим все компоненты оружия и увеличиваем урон
        PlayerStats stats = PlayerStats.Instance;
        if (stats != null)
        {
            stats.IncreaseDamage(5);  // +5 к базовому урону
        }

        OnLevelUp?.Invoke(currentLevel);
        Debug.Log($"Level up! Now level {currentLevel}");
    }
    public int GetRequiredExpForLevel(int level)
    {
        // формула: baseExp * (growth ^ (level-1))
        return Mathf.FloorToInt(levelData.baseExpToNextLevel * Mathf.Pow(levelData.expGrowthRate, level - 1));
    }

    public int GetRequiredExpForCurrentLevel()
    {
        return GetRequiredExpForLevel(currentLevel);
    }
}
