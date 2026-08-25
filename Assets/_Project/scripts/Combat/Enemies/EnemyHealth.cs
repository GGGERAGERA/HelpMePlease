using UnityEngine;
using UnityEngine.Events;
using System.Collections;
using System.Collections.Generic;

public class EnemyHealth : MonoBehaviour
{
    private static readonly HashSet<EnemyHealth> activeInstances = new();

    public static event System.Action<EnemyHealth> Spawned;
    public static event System.Action<EnemyHealth> Despawned;
    public static IReadOnlyCollection<EnemyHealth> ActiveInstances => activeInstances;
    public static event System.Action<EnemyHealth> SpawnConfigured;

    public float maxHealth = 30f;
    private float currentHealth;
    private float baseMaxHealth;
    private bool spawnConfigured;

    public UnityEvent<float, float> OnHealthChanged;
    public UnityEvent onDeath;
    public UnityEvent OnDamageTaken; // новое событие для эффекта урона
    public System.Action<EnemyHealth> OnDied;

    public GameObject damagePopupPrefab;  // перетащите префаб DamagePopup
    public Vector3 popupOffset = new Vector3(0, 1f, 0); // смещение над врагом



    [SerializeField] private AudioClip critSound;

    
    [Header("Hit Sound")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip hitSound;
    [SerializeField] private Vector2 hitPitchRange = new Vector2(0.95f, 1.05f);
    [SerializeField] private float hitVolume = 0.35f;

    [Header("Loot")]
    [SerializeField] private GameObject lootPrefab;
    [SerializeField] private int lootAmount = 1;
    [SerializeField] private float lootScatterRadius = 0.4f;


    [Header("Hit FX")]
    [SerializeField] private ParticleSystem bloodHitPrefab;
    [SerializeField] private ParticleSystem deathFXPrefab;
    [Header("Boss")]
    [SerializeField] private bool isBoss;
    [Header("Boss UI")]
    [SerializeField] private string bossName = "BOSS";

    private bool isDead;
    public bool IsDead => isDead;
    public bool IsBoss => isBoss;
    public float CurrentHealth => currentHealth;
    public float MaxHealth => maxHealth;

    private EnemyWhiteFlash whiteFlash;
    private EnemyIdentity identity;
    private SimplePrefabPool damagePopupPool;
    private SimplePrefabPool bloodHitPool;
    private SimplePrefabPool deathFxPool;

    public GameObject DamagePopupPrefab => damagePopupPrefab;
    public GameObject BloodHitPrefab =>
        bloodHitPrefab != null ? bloodHitPrefab.gameObject : null;
    public GameObject DeathFxPrefab =>
        deathFXPrefab != null ? deathFXPrefab.gameObject : null;

    private static float lastCritSoundTime;
    private static bool missingUnlockServiceWasReported;
    void Start()
    {
        currentHealth = maxHealth;
        if (isBoss)
        {
            HUDManager.Instance?.ShowBossHp(bossName, currentHealth, maxHealth);
        }
        OnHealthChanged?.Invoke(currentHealth, maxHealth);

    }

    private void Awake()
    {
        baseMaxHealth = maxHealth;
        whiteFlash = GetComponent<EnemyWhiteFlash>();
        identity = GetComponent<EnemyIdentity>();

        if (hitSound == null)
            return;

        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();

        if (audioSource == null)
            audioSource = gameObject.AddComponent<AudioSource>();

        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0f;
        AudioService.Instance?.RouteExternalSource(
            audioSource,
            AudioCategory.SFX
        );
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

        if (isBoss)
            HUDManager.Instance?.UpdateBossHp(currentHealth, maxHealth);
    }

    public void SetRuntimeMaxHealth(float value)
    {
        maxHealth = Mathf.Max(0.01f, value);
        currentHealth = maxHealth;
        OnHealthChanged?.Invoke(currentHealth, maxHealth);

        if (isBoss)
            HUDManager.Instance?.UpdateBossHp(currentHealth, maxHealth);
    }

    public void NotifySpawnConfigured()
    {
        if (spawnConfigured)
            return;

        spawnConfigured = true;
        SpawnConfigured?.Invoke(this);
    }

    public void SetFeedbackPools(
        SimplePrefabPool popupPool,
        SimplePrefabPool hitPool,
        SimplePrefabPool deathPool)
    {
        damagePopupPool = popupPool;
        bloodHitPool = hitPool;
        deathFxPool = deathPool;
    }

    public void TakeDamage(float damage, Vector2 hitPoint, bool isCritical = false)
    {
        if (isDead) return;
        if (currentHealth <= 0) return;

        if (whiteFlash != null)
            whiteFlash.Flash();

        currentHealth -= damage;
        if (isBoss)
        {
            HUDManager.Instance?.UpdateBossHp(currentHealth, maxHealth);
        }
        SpawnBlood(hitPoint, isCritical);
        PlayHitSound();
        // Показать цифру урона
        ShowDamagePopup(Mathf.RoundToInt(damage), isCritical);
        OnHealthChanged?.Invoke(currentHealth, maxHealth);
        OnDamageTaken?.Invoke(); // вызываем эффект урона

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        CombatFeelTestDummy testDummy = GetComponent<CombatFeelTestDummy>();
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

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    public void DebugSimulateFeedback(
        float damage,
        Vector2 hitPoint,
        bool isCritical,
        bool lethal)
    {
        if (isDead)
            return;
        whiteFlash?.Flash();
        SpawnBlood(hitPoint, isCritical);
        PlayHitSound();
        ShowDamagePopup(Mathf.RoundToInt(damage), isCritical);
        OnDamageTaken?.Invoke();
        if (lethal)
            PlayDebugDeathPresentation();
    }
#endif
    private void SpawnBlood(Vector2 hitPoint, bool isCritical)
    {
        if (bloodHitPrefab == null)
            return;
        float bloodHitDestroyTime = bloodHitPrefab.main.duration;
        PooledGameObject pooledBlood = bloodHitPool?.Get(
            hitPoint,
            Quaternion.identity);
        ParticleSystem blood = pooledBlood != null
            ? pooledBlood.PrimaryParticleSystem
            : Instantiate(bloodHitPrefab, hitPoint, Quaternion.identity);
        if (isCritical)
        {
            if (critSound != null)
            {
                if (Time.time > lastCritSoundTime + 0.08f)
                {
                    AudioService.Instance?.PlayExternalOneShot(
                        critSound,
                        transform.position,
                        0.7f,
                        AudioCategory.SFX,
                        1f
                    );

                    lastCritSoundTime = Time.time;
                }
            }
            var main = blood.main;
            main.startSizeMultiplier *= 1.4f;
            main.startSpeedMultiplier *= 1.3f;

            var emission = blood.emission;
            emission.rateOverTimeMultiplier *= 1.5f;
        }

        blood.Play();

        if (pooledBlood != null)
            pooledBlood.ReleaseAfter(bloodHitDestroyTime);
        else
            Destroy(blood.gameObject, bloodHitDestroyTime);
    }
    private void Die()
    {
        if (isDead)
            return;

        isDead = true;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (GetComponent<CombatFeelTestDummy>() != null)
        {
            PlayDebugDeathPresentation();
            PhysicalCombatFeedbackRuntime.TryDetachDeathVisual(this);
            Destroy(gameObject);
            return;
        }
#endif

        AudioService.Instance?.PlayAt(
            isBoss ? AudioCueId.BossDeath : AudioCueId.CommonEnemyDeath,
            transform.position
        );

        if (UnlockProgressService.Instance != null &&
            identity != null &&
            !string.IsNullOrWhiteSpace(identity.EnemyId))
        {
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

    void ShowDamagePopup(int damage, bool isCritical)
    {
        if (damagePopupPrefab == null)
            return;

        Vector3 spawnPos = transform.position + popupOffset;
        PooledGameObject pooledPopup = damagePopupPool?.Get(
            spawnPos,
            Quaternion.identity);
        DamagePopup dp = pooledPopup != null
            ? pooledPopup.DamagePopup
            : Instantiate(damagePopupPrefab, spawnPos, Quaternion.identity)
                .GetComponent<DamagePopup>();
        if (dp != null)
            dp.SetDamage(damage, isCritical);
        else
            pooledPopup?.Release();
    }

    private void PlayHitSound()
    {
        if (hitSound == null || audioSource == null)
            return;

        audioSource.pitch = Random.Range(hitPitchRange.x, hitPitchRange.y);
        audioSource.PlayOneShot(hitSound, hitVolume);
    }

    private void Death()
    {
        if (isBoss)
        {
            HUDManager.Instance?.HideBossHp();

            if (RunFlowController.Instance != null)
            {
                RunFlowController.Instance.HandleBossDefeated();
            }
            else
            {
                Debug.LogError(
                    "[EnemyHealth] Boss defeated, but RunFlowController is missing."
                );
            }
        }

        if (deathFXPrefab != null)
        {
            PooledGameObject pooledBlood = deathFxPool?.Get(
                transform.position,
                Quaternion.identity);
            ParticleSystem blood = pooledBlood != null
                ? pooledBlood.PrimaryParticleSystem
                : Instantiate(
                    deathFXPrefab,
                    transform.position,
                    Quaternion.identity);

            float destroyTime = blood.main.duration;
            blood.Play();
            if (pooledBlood != null)
                pooledBlood.ReleaseAfter(destroyTime);
            else
                Destroy(blood.gameObject, destroyTime);
        }

        DropLoot();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        PhysicalCombatFeedbackRuntime.TryDetachDeathVisual(this);
#endif
        Destroy(gameObject);
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    private void PlayDebugDeathPresentation()
    {
        AudioService.Instance?.PlayAt(
            isBoss ? AudioCueId.BossDeath : AudioCueId.CommonEnemyDeath,
            transform.position);
        if (deathFXPrefab == null)
            return;
        PooledGameObject pooled = deathFxPool?.Get(
            transform.position, Quaternion.identity);
        ParticleSystem effect = pooled != null
            ? pooled.PrimaryParticleSystem
            : Instantiate(deathFXPrefab, transform.position, Quaternion.identity);
        float lifetime = effect.main.duration;
        effect.Play();
        if (pooled != null)
            pooled.ReleaseAfter(lifetime);
        else
            Destroy(effect.gameObject, lifetime);
    }
#endif

    private void DropLoot()
    {
        if (lootPrefab == null)
            return;

        for (int i = 0; i < lootAmount; i++)
        {
            Vector2 randomOffset = Random.insideUnitCircle * lootScatterRadius;
            Instantiate(
                lootPrefab,
                (Vector2)transform.position + randomOffset,
                Quaternion.identity
            );
        }
    }

}
