using UnityEngine;
using UnityEngine.Events;

public class ExperienceManager : MonoBehaviour
{
    public static ExperienceManager Instance;

    [Header("Level Settings")]
    public LevelData levelData;          // ScriptableObject с настройками опыта
    public int currentLevel = 1;
    public int currentExp = 0;

    [Header("Upgrade Settings")]
    public int healthPerLevel = 10;      // +10 к макс. здоровью за уровень
    public float speedPerLevel = 0.2f;   // +0.2 к скорости за уровень
    public float damageMultiplierPerLevel = 0.1f; // +10% к урону за уровень

    private int expToNextLevel;

    public UnityEvent<int, int> OnExperienceChanged; // (currentExp, requiredExp)
    public UnityEvent<int> OnLevelUp;                 // (newLevel)

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

        // Применяем улучшения к игроку
        ApplyUpgrades();

        OnLevelUp?.Invoke(currentLevel);
        Debug.Log($"Level up! Now level {currentLevel}");
    }

    private void ApplyUpgrades()
{
    // Находим компоненты
    PlayerHealth health = FindFirstObjectByType<PlayerHealth>();
    CharacterMovement2D movement = FindFirstObjectByType<CharacterMovement2D>();
    PlayerStats stats = PlayerStats.Instance;
    
    // 1. Увеличиваем здоровье
    if (health != null)
    {
        health.maxHealth += healthPerLevel;
        health.currentHealth += healthPerLevel; // теперь должно работать
        // или health.Heal(healthPerLevel);
    }
    
    // 2. Увеличиваем скорость
    if (movement != null)
    {
        movement.speed += speedPerLevel;
    }
    
    // 3. Увеличиваем множитель урона
    if (stats != null)
    {
            stats.IncreaseDamageMultiplier(damageMultiplierPerLevel);
        }
    
    // 4. Обновляем UI
    if (health != null && health.healthSlider != null)
    {
        health.healthSlider.maxValue = health.maxHealth;
        health.healthSlider.value = health.currentHealth;
    }
}

    private int GetRequiredExpForLevel(int level)
    {
        return Mathf.FloorToInt(levelData.baseExpToNextLevel * Mathf.Pow(levelData.expGrowth, level - 1));
    }

    public int GetRequiredExpForCurrentLevel()
    {
        return GetRequiredExpForLevel(currentLevel);
    }
}