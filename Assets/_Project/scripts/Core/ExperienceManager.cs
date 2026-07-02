using UnityEngine;
using UnityEngine.Events;
using System.Collections; // 👈 Добавь только эту строку в начало

public class ExperienceManager : MonoBehaviour
{
    public static ExperienceManager Instance;

    [Header("Level Settings")]
    public LevelData levelData;
    public int currentLevel = 1;
    public int currentExp = 0;

    private int expToNextLevel;
    private float xpGainMultiplier = 1f;

    public UnityEvent<int, int> OnExperienceChanged;
    public UnityEvent<int> OnLevelUp;

    [SerializeField] private AudioClip levelUpSound;
    [SerializeField] private float levelUpVolume = 0.5f;
    [SerializeField] private GameObject levelUpFX;
    [SerializeField] private float FXPauseTime = 1.2f;
    [SerializeField] private Transform playerTransform;

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

        currentExp += Mathf.RoundToInt(amount * xpGainMultiplier);

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
        StartCoroutine(LevelUpSequence()); // 👈 Запускаем корутину вместо прямого вызова
    }

    private IEnumerator LevelUpSequence()
    {
        // 1. Логика уровня
        currentLevel++;
        expToNextLevel = GetRequiredExpForLevel(currentLevel);
        OnLevelUp?.Invoke(currentLevel);

        // 2. Пауза
        Time.timeScale = 0f;

        // 3. Звук (играет всегда)
        if (levelUpSound != null && Camera.main != null)
            AudioSource.PlayClipAtPoint(levelUpSound, Camera.main.transform.position, levelUpVolume);

        // 4. Поиск игрока
        if (playerTransform == null)
        {
            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
            playerTransform = playerObj?.transform;
        }

        // 5. Эффект
        if (levelUpFX != null && playerTransform != null)
        {
            Vector3 spawnPosition = playerTransform.position; // 👈 Исправлена ошибка playerTransform.transform.position
            GameObject FXSpawn = Instantiate(levelUpFX, spawnPosition, Quaternion.identity, playerTransform);

            //  Чтобы эффект не завис на паузе
            foreach (var ps in FXSpawn.GetComponentsInChildren<ParticleSystem>())
            {
                var mainModule = ps.main;
                mainModule.useUnscaledTime = true;
            }

            Destroy(FXSpawn, 10f);

            // 👇 Ждём завершения эффекта (реальное время, игнорирует паузу)
            // ⚠️ Подставь реальную длительность твоего эффекта в секундах
            yield return new WaitForSecondsRealtime(FXPauseTime);
        }

        // 6. UI появляется ТОЛЬКО после эффекта
        if (UpgradeManager.Instance != null)
            UpgradeManager.Instance.ShowUpgradeChoices();

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

    public void AddXpGainPercent(float percent)
    {
        xpGainMultiplier *= 1f + percent;
    }
    public void RestoreRuntimeExperience(int level, int exp)
    {
        currentLevel = Mathf.Max(1, level);
        currentExp = Mathf.Max(0, exp);

        expToNextLevel = GetRequiredExpForLevel(currentLevel);

        UpdateExperienceHUD();
        OnExperienceChanged?.Invoke(currentExp, expToNextLevel);
    }
}