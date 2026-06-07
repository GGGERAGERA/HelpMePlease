using UnityEngine;
using UnityEngine.Events;
using System.Collections;

public class EnemyHealth : MonoBehaviour
{
    public float maxHealth = 30f;
    private float currentHealth;

    public UnityEvent<float, float> OnHealthChanged;
    public UnityEvent onDeath;
    public UnityEvent OnDamageTaken; // новое событие для эффекта урона

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

    private static float lastCritSoundTime;
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
        currentHealth = maxHealth;
        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();

        if (audioSource == null)
            audioSource = gameObject.AddComponent<AudioSource>();

        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0f;
    }

    public void SetMaxHealthMultiplier(float multiplier)
    {
        maxHealth *= multiplier;
        currentHealth = maxHealth;
    }

    public void TakeDamage(float damage, Vector2 hitPoint, bool isCritical = false)
    {
        if (isDead) return;
        if (currentHealth <= 0) return;

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

        if (currentHealth <= 0)
            Die();
    }
    private void SpawnBlood(Vector2 hitPoint, bool isCritical)
    {
        if (bloodHitPrefab == null)
            return;
        float bloodHitDestroyTime = bloodHitPrefab.main.duration;
        ParticleSystem blood = Instantiate(
            bloodHitPrefab,
            hitPoint,
            Quaternion.identity
        );
        if (isCritical)
        {
            if (critSound != null)
            {
                if (Time.time > lastCritSoundTime + 0.08f)
                {
                    AudioSource.PlayClipAtPoint(
                        critSound,
                        transform.position,
                        0.7f
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

        Destroy(blood.gameObject, bloodHitDestroyTime);
    }
    private void Die()
    {
        if (isDead) return;

        isDead = true;

        KillManager.Instance?.AddKill();

        Death();
    }

    void ShowDamagePopup(int damage, bool isCritical)
    {
        if (damagePopupPrefab == null)
            return;

        Vector3 spawnPos = transform.position + popupOffset;
        GameObject popup = Instantiate(damagePopupPrefab, spawnPos, Quaternion.identity);

        DamagePopup dp = popup.GetComponent<DamagePopup>();
        if (dp != null)
            dp.SetDamage(damage, isCritical);
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

            RunTimer runTimer = FindAnyObjectByType<RunTimer>();
            if (runTimer != null)
                runTimer.StartSurvivalPhase();
        }

        if (deathFXPrefab != null)
        {
            ParticleSystem blood = Instantiate(
                deathFXPrefab,
                transform.position,
                Quaternion.identity
            );

            float destroyTime = blood.main.duration;
            blood.Play();
            Destroy(blood.gameObject, destroyTime);
        }

        DropLoot();
        Destroy(gameObject);
    }

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