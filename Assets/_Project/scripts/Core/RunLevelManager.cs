using UnityEngine;

public class RunLevelManager : MonoBehaviour
{
    public static RunLevelManager Instance { get; private set; }

    [Header("Current Level")]
    [SerializeField] private int currentLevel = 1;

    [Header("Scaling")]
    [SerializeField] private float enemyHealthMultiplierPerLevel = 1.35f;
    [SerializeField] private float enemySpeedMultiplierPerLevel = 1.12f;
    [SerializeField] private float spawnRateMultiplierPerLevel = 0.85f;

    public int CurrentLevel => currentLevel;

    private void Awake()
    {
        Instance = this;
    }

    public void GoToNextLevel()
    {
        currentLevel++;

        HUDManager.Instance?.ShowBossText(
            $"LEVEL {currentLevel}",
            3f
        );

        EnemySpawner spawner = FindAnyObjectByType<EnemySpawner>();

        if (spawner != null)
        {
            spawner.ApplyLevelScaling(
                enemyHealthMultiplierPerLevel,
                enemySpeedMultiplierPerLevel,
                spawnRateMultiplierPerLevel
            );
        }

        RunTimer timer = FindAnyObjectByType<RunTimer>();

        if (timer != null)
            timer.RestartBossTimer();
    }
}
