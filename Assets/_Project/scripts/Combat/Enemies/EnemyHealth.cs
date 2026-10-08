using UnityEngine;
using UnityEngine.Events;
using DropRandom = GameplayRandom.DropRandom;
using System.Collections;
using System.Collections.Generic;

public class EnemyHealth : MonoBehaviour
{
    private static readonly HashSet<EnemyHealth> activeInstances = new();

    public static event System.Action<EnemyHealth> Spawned;
    public static event System.Action<EnemyHealth> Despawned;
    public static IReadOnlyCollection<EnemyHealth> ActiveInstances => activeInstances;
    internal static HashSet<EnemyHealth>.Enumerator GetActiveEnumerator() => activeInstances.GetEnumerator();
    public static event System.Action<EnemyHealth> SpawnConfigured;

    public float maxHealth = 30f;
    private float currentHealth;
    private float baseMaxHealth;
    private bool spawnConfigured;

    public UnityEvent<float, float> OnHealthChanged = new();
    public UnityEvent onDeath;
    public UnityEvent OnDamageTaken; // новое событие для эффекта урона
    public System.Action<EnemyHealth> OnDied;
    public event System.Action<float, Vector2, bool> HitFeedback;
    public event System.Action DeathFeedback;




    [Header("Loot")]
    [SerializeField] private GameObject lootPrefab;
    [SerializeField] private int lootAmount = 1;
    [SerializeField] private float lootScatterRadius = 0.4f;


    [Header("Boss")]
    [SerializeField] private bool isBoss;
    [Header("Boss Identity")]
    [SerializeField] private string bossName = "BOSS";

    private bool isDead;
    public bool IsDead => isDead;
    public bool IsBoss => isBoss;
    public string BossName => bossName;
    public float CurrentHealth => currentHealth;
    public float MaxHealth => maxHealth;

    private EnemyIdentity identity;
    private static bool missingUnlockServiceWasReported;
    void Start()
    {
        currentHealth = maxHealth;
        OnHealthChanged?.Invoke(currentHealth, maxHealth);

    }

    private void Awake()
    {
        baseMaxHealth = maxHealth;
        identity = GetComponent<EnemyIdentity>();


    }

    private void OnEnable()
    {
        isDead = false;
        spawnConfigured = false;
        maxHealth = baseMaxHealth;
        currentHealth = maxHealth;
        activeInstances.Add(this);
        Spawned?.Invoke(this);
    }

    private void OnDisable()
    {
        activeInstances.Remove(this);
        Despawned?.Invoke(this);
    }

    public void SetMaxHealthMultiplier(float multiplier)
    {
        maxHealth *= multiplier;
        currentHealth = maxHealth;

        OnHealthChanged?.Invoke(currentHealth, maxHealth);

    }

    public void SetRuntimeMaxHealth(float value)
    {
        maxHealth = Mathf.Max(0.01f, value);
        currentHealth = maxHealth;
        OnHealthChanged?.Invoke(currentHealth, maxHealth);

    }

    public void NotifySpawnConfigured()
    {
        if (spawnConfigured)
            return;

        spawnConfigured = true;
        SpawnConfigured?.Invoke(this);
    }



    public void TakeDamage(float damage, Vector2 hitPoint, bool isCritical = false)
    {
        if (isDead) return;
        if (currentHealth <= 0) return;

        currentHealth -= damage;
        HitFeedback?.Invoke(damage, hitPoint, isCritical);
        OnHealthChanged?.Invoke(currentHealth, maxHealth);
        OnDamageTaken?.Invoke(); // вызываем эффект урона

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        EnemyDebugDamagePolicy testDummy = GetComponent<EnemyDebugDamagePolicy>();
        if (testDummy != null && testDummy.Invulnerable)
        {
            currentHealth = maxHealth;
            OnHealthChanged?.Invoke(currentHealth, maxHealth);
            return;
        }
#endif

        if (currentHealth <= 0)
            Die();
    }

    private void Die()
    {
        if (isDead)
            return;

        isDead = true;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (GetComponent<EnemyDebugDamagePolicy>() is { SuppressesProductionRewards: true })
        {
            DeathFeedback?.Invoke();
            Destroy(gameObject);
            return;
        }
#endif

        if (UnlockProgressService.Instance != null &&
            identity != null &&
            !string.IsNullOrWhiteSpace(identity.EnemyId))
        {
            if (RunStateManager.Instance?.IsDevelopmentRun != true)
                UnlockProgressService.Instance.AddProgressByCondition(
                UnlockConditionType.KillEnemyType,
                identity.EnemyId,
                1
            );
        }
        else
        {
            if (identity == null)
            {
                Debug.LogWarning(
                    $"[EnemyHealth] EnemyIdentity is missing on '{name}'. Kill progress was not registered.",
                    this
                );
            }

            if (UnlockProgressService.Instance == null &&
                !missingUnlockServiceWasReported)
            {
                missingUnlockServiceWasReported = true;
                Debug.LogWarning(
                    "[EnemyHealth] UnlockProgressService is missing. Kill progress was not registered.",
                    this
                );
            }
        }

        GoldenEnemyModifier goldenModifier =
            GetComponent<GoldenEnemyModifier>();
        float killRewardMultiplier = goldenModifier != null
            ? goldenModifier.RewardMultiplier
            : 1f;

        if (!isBoss && LevelAnomalyController.Instance != null &&
            LevelAnomalyController.Instance.IsPositionInsideActiveZone(
                transform.position) &&
            RunStateManager.Instance != null)
        {
            killRewardMultiplier *= RunStateManager.Instance.AnomalyModifiers
                .GoldInsideAnomalyMultiplier;
        }

        KillManager.Instance?.AddKill(killRewardMultiplier);

        OnDied?.Invoke(this);

        Death();
    }

    private void Death()
    {
        if (isBoss)
        {

            if (RunFlowController.Instance != null)
            {
                RunFlowController.Instance.HandleBossDefeated(this);
            }
            else
            {
                Debug.LogError(
                    "[EnemyHealth] Boss defeated, but RunFlowController is missing."
                );
            }
        }

        DeathFeedback?.Invoke();
        DropLoot();
        Destroy(gameObject);
    }

    private void DropLoot()
    {
        if (lootPrefab == null)
            return;

        for (int i = 0; i < lootAmount; i++)
        {
            Vector2 randomOffset = DropRandom.insideUnitCircle * lootScatterRadius;
            SpawnLootPickup((Vector2)transform.position + randomOffset);
        }
    }

    // Shared by enemy death and development tools; retains the authored loot prefab.
    public GameObject SpawnLootPickup(Vector3 position)
    {
        return lootPrefab != null
            ? Instantiate(lootPrefab, position, Quaternion.identity)
            : null;
    }

    public bool HasExperienceLoot => lootPrefab != null &&
        lootPrefab.GetComponent<ExperiencePickup>() != null;

}
