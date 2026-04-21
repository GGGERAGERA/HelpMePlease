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

        // Применяем улучшения (находим компоненты игрока)
        PlayerHealth playerHealth = FindFirstObjectByType<PlayerHealth>();
        Shoot shoot = FindFirstObjectByType<Shoot>();
        CharacterMovement2D movement = FindFirstObjectByType<CharacterMovement2D>();
        if (shoot != null)
        {
            shoot.bulletDamage += 2f;
            shoot.shootInterval = Mathf.Max(0.2f, shoot.shootInterval - 0.05f);
            // Если используете InvokeRepeating, нужно перезапустить его (сложно), для простоты оставьте так
        }
        if (movement != null)
            movement.speed += 0.2f;

        OnLevelUp?.Invoke(currentLevel);
        Debug.Log($"Level up! Now level {currentLevel}. Damage: {shoot?.bulletDamage}, Speed: {movement?.speed}");
    }
    public int GetRequiredExpForLevel(int level)
    {
        // формула: baseExp * (growth ^ (level-1))
        return Mathf.FloorToInt(levelData.baseExpToNextLevel * Mathf.Pow(levelData.expGrowthRate, level - 1));
    }
}
