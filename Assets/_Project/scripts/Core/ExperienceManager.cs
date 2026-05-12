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

    [SerializeField] private AudioClip levelUpSound;
    [SerializeField] private float levelUpVolume = 0.5f;

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
        UpdateExperienceHUD();
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

        UpdateExperienceHUD();
        OnExperienceChanged?.Invoke(currentExp, expToNextLevel);
    }

    private void LevelUp()
    {
        currentLevel++;
        expToNextLevel = GetRequiredExpForLevel(currentLevel);

        OnLevelUp?.Invoke(currentLevel);

        if (UpgradeManager.Instance != null)
            UpgradeManager.Instance.ShowUpgradeChoices();

        if (levelUpSound != null && Camera.main != null)
            AudioSource.PlayClipAtPoint(levelUpSound, Camera.main.transform.position, levelUpVolume);

        Debug.Log("Level up! Now level " + currentLevel);
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
}