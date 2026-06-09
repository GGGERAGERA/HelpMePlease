using UnityEngine;
using UnityEngine.Rendering.Universal;

public class RunLevelManager : MonoBehaviour
{
    public static RunLevelManager Instance { get; private set; }

    [Header("Current Level")]
    [SerializeField] private int currentLevel = 1;

    [Header("Player Reset")]
    [SerializeField] private Transform levelStartPoint;
    [SerializeField] private bool healPlayerOnNextLevel = false;
    [SerializeField] private float healPercentOnNextLevel = 0.35f;

    [Header("Scaling")]
    [SerializeField] private float enemyHealthMultiplierPerLevel = 1.35f;
    [SerializeField] private float enemySpeedMultiplierPerLevel = 1.12f;
    [SerializeField] private float spawnRateMultiplierPerLevel = 0.85f;

    [Header("Level Lighting")]
    [SerializeField] private Light2D globalLight;
    [SerializeField] private int darkLevel = 2;
    [SerializeField] private float darkLevelIntensity = 0.1f;

    public int CurrentLevel => currentLevel;

    private bool isChangingLevel;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    public void GoToNextLevel()
    {
        Debug.Log("Before level up: " + currentLevel);


        
        if (isChangingLevel)
            return;

        isChangingLevel = true;

        currentLevel++;
        Debug.Log("After level up: " + currentLevel);
        ApplyLevelLighting();
        ClearLevelObjects();
        MovePlayerToStart();
        OptionallyHealPlayer();

        EnemySpawner spawner = FindAnyObjectByType<EnemySpawner>();

        if (spawner != null)
        {
            spawner.ResetForNewLevel();

            int levelIndex = currentLevel - 1;

            float healthMultiplier = Mathf.Pow(enemyHealthMultiplierPerLevel, levelIndex);
            float speedMultiplier = Mathf.Pow(enemySpeedMultiplierPerLevel, levelIndex);
            float spawnRateMultiplier = Mathf.Pow(spawnRateMultiplierPerLevel, levelIndex);

            spawner.SetLevelScaling(
                healthMultiplier,
                speedMultiplier,
                spawnRateMultiplier
            );
        }

        RunTimer timer = FindAnyObjectByType<RunTimer>();

        if (timer != null)
            timer.RestartBossTimer();

        HUDManager.Instance?.ShowBossText($"LEVEL {currentLevel}", 3f);

        isChangingLevel = false;
    }

    private void ClearLevelObjects()
    {
        DestroyAllWithTag("Enemy");
        DestroyAllWithTag("Loot");
        DestroyAllWithTag("WorldEvent");
        DestroyAllPortals();

        HUDManager.Instance?.HideBossHp();
        HUDManager.Instance?.HideWorldEventMarker();
    }

    private void DestroyAllWithTag(string tag)
    {
        GameObject[] objects = GameObject.FindGameObjectsWithTag(tag);

        foreach (GameObject obj in objects)
        {
            if (obj != null)
                Destroy(obj);
        }
    }

    private void DestroyAllPortals()
    {
        ExitPortal[] portals = FindObjectsByType<ExitPortal>(FindObjectsSortMode.None);

        foreach (ExitPortal portal in portals)
        {
            if (portal != null)
                Destroy(portal.gameObject);
        }
    }

    private void MovePlayerToStart()
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");

        if (player == null || levelStartPoint == null)
            return;

        player.transform.position = levelStartPoint.position;
    }

    private void OptionallyHealPlayer()
    {
        if (!healPlayerOnNextLevel)
            return;

        GameObject player = GameObject.FindGameObjectWithTag("Player");

        if (player == null)
            return;

        PlayerHealth health = player.GetComponent<PlayerHealth>();

        if (health == null)
            return;

        float healAmount = health.maxHealth * healPercentOnNextLevel;
        health.Heal(healAmount);
    }
    public int GetNextLevelNumber()
    {
        return currentLevel + 1;
    }
    private void ApplyLevelLighting()
    {
        if (globalLight == null)
            return;

        if (currentLevel >= darkLevel)
            globalLight.intensity = darkLevelIntensity;
    }
}