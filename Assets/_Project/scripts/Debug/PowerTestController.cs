using System.Collections.Generic;
using UnityEngine;

public sealed class PowerTestController : MonoBehaviour
{
    // Stable low-TTK sandbox population.
    public const int TargetAlive = 42;
    public const float RefillInterval = 0.3f;
    public const int RefillBatchSize = 6;
    public const float MinimumSpawnDistance = 5f;
    public const float MaximumSpawnDistance = 9f;

    private const float RollingKillWindow = 5f;

    private readonly HashSet<EnemyHealth> testEnemies = new();
    private readonly Queue<float> recentKillTimes = new();
    private EnemySpawner enemySpawner;
    private GameplayAreaService gameplayArea;
    private GameObject[] enemyPrefabs;
    private ExitMassTestController exitMassTest;
    private Transform player;
    private PlayerHealth playerHealth;
    private float originalIncomingDamageMultiplier = 1f;
    private float refillTimer;
    private int kills;
    private bool active;
    private bool damageOverrideApplied;

    public bool IsActive => active;
    public int EnemiesAlive => testEnemies.Count;
    public int Kills => kills;
    public float KillsPerSecond
    {
        get
        {
            TrimRecentKills();
            return recentKillTimes.Count / RollingKillWindow;
        }
    }

    public void Configure(
        EnemySpawner spawner,
        GameplayAreaService area,
        GameObject[] prefabs,
        ExitMassTestController oldMassTest)
    {
        enemySpawner = spawner;
        gameplayArea = area;
        enemyPrefabs = prefabs;
        exitMassTest = oldMassTest;
    }

    private void Update()
    {
        ResolvePlayer();

        if (Input.GetKeyDown(KeyCode.F4))
        {
            if (Input.GetKey(KeyCode.LeftShift) ||
                Input.GetKey(KeyCode.RightShift))
            {
                StopTest();
            }
            else
            {
                ResetTest();
            }

            return;
        }

        if (!active || player == null)
            return;

        TrimRecentKills();
        refillTimer += Time.deltaTime;

        if (refillTimer < RefillInterval ||
            testEnemies.Count >= TargetAlive)
        {
            return;
        }

        refillTimer = 0f;
        SpawnBatch(Mathf.Min(
            RefillBatchSize,
            TargetAlive - testEnemies.Count
        ));
    }

    private void ResetTest()
    {
        ResolvePlayer();
        if (player == null || enemySpawner == null ||
            enemyPrefabs == null || enemyPrefabs.Length == 0)
        {
            Debug.LogWarning(
                "[PowerTest] Player, EnemySpawner, or enemy prefabs are missing."
            );
            return;
        }

        exitMassTest?.StopForOtherDebugTest();
        ClearEnemies();
        ApplyDamageOverride();
        MovePlayerToCenter();
        kills = 0;
        recentKillTimes.Clear();
        refillTimer = 0f;
        active = true;
        SpawnBatch(18);
        Debug.Log(
            "[PowerTest] POWER TEST STARTED. " +
            "F4 resets; Shift+F4 stops. Powers 4/5/6."
        );
    }

    public void StopTest()
    {
        if (!active && testEnemies.Count == 0)
            return;

        ClearEnemies();
        RestoreDamageOverride();
        active = false;
        Debug.Log("[PowerTest] POWER TEST STOPPED.");
    }

    private void SpawnBatch(int count)
    {
        if (player == null || count <= 0)
            return;

        for (int i = 0; i < count; i++)
        {
            GameObject prefab = enemyPrefabs[
                Random.Range(0, enemyPrefabs.Length)
            ];
            GameObject instance = enemySpawner.SpawnSpecificEnemyAround(
                prefab,
                player.position,
                MinimumSpawnDistance,
                MaximumSpawnDistance,
                4f,
                false,
                0f
            );

            if (instance == null)
                continue;

            EnemyHealth health = instance.GetComponent<EnemyHealth>();
            if (health == null)
            {
                Destroy(instance);
                continue;
            }

            testEnemies.Add(health);
            health.OnDied += HandleEnemyDied;
        }
    }

    private void HandleEnemyDied(EnemyHealth enemy)
    {
        if (enemy != null)
            enemy.OnDied -= HandleEnemyDied;

        if (!testEnemies.Remove(enemy))
            return;

        kills++;
        recentKillTimes.Enqueue(Time.time);
    }

    private void ClearEnemies()
    {
        foreach (EnemyHealth enemy in testEnemies)
        {
            if (enemy == null)
                continue;

            enemy.OnDied -= HandleEnemyDied;
            Destroy(enemy.gameObject);
        }

        testEnemies.Clear();
        recentKillTimes.Clear();
    }

    private void ResolvePlayer()
    {
        if (player != null)
            return;

        GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
        if (playerObject == null)
            return;

        player = playerObject.transform;
        playerHealth = playerObject.GetComponent<PlayerHealth>();
    }

    private void MovePlayerToCenter()
    {
        Vector2 center = gameplayArea != null &&
            gameplayArea.PlayableArea != null
            ? gameplayArea.PlayableArea.bounds.center
            : Vector2.zero;
        player.position = center;

        Rigidbody2D body = player.GetComponent<Rigidbody2D>();
        if (body != null)
        {
            body.position = center;
            body.linearVelocity = Vector2.zero;
        }

        CharacterMovement2D movement = player.GetComponent<CharacterMovement2D>();
        if (movement != null)
            movement.enabled = true;

        if (playerHealth != null)
            playerHealth.SetRuntimeHealth(playerHealth.MaxHealth, playerHealth.MaxHealth);
    }

    private void ApplyDamageOverride()
    {
        if (playerHealth == null || damageOverrideApplied)
            return;

        originalIncomingDamageMultiplier = playerHealth.IncomingDamageMultiplier;
        playerHealth.SetIncomingDamageMultiplier(0f);
        damageOverrideApplied = true;
    }

    private void RestoreDamageOverride()
    {
        if (playerHealth == null || !damageOverrideApplied)
            return;

        playerHealth.SetIncomingDamageMultiplier(
            originalIncomingDamageMultiplier
        );
        damageOverrideApplied = false;
    }

    private void TrimRecentKills()
    {
        float cutoff = Time.time - RollingKillWindow;
        while (recentKillTimes.Count > 0 &&
            recentKillTimes.Peek() < cutoff)
        {
            recentKillTimes.Dequeue();
        }
    }

    private void OnDestroy()
    {
        ClearEnemies();
        RestoreDamageOverride();
    }
}
