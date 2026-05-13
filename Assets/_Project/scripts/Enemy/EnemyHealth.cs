using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.Events;

public class EnemyHealth : MonoBehaviour
{
    public float maxHealth = 30f;
    private float currentHealth;
    private bool isDead = false;

    public UnityEvent<float, float> OnHealthChanged;
    public UnityEvent onDeath;
    public UnityEvent OnDamageTaken; // новое событие для эффекта урона

    public GameObject damagePopupPrefab;  // перетащите префаб DamagePopup
    public Vector3 popupOffset = new Vector3(0, 1f, 0); // смещение над врагом

    [Header("Hit Sound")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip hitSound;
    [SerializeField] private Vector2 hitPitchRange = new Vector2(0.95f, 1.05f);
    [SerializeField] private float hitVolume = 0.35f;


    [Header("Hit FX")]
    [SerializeField] private ParticleSystem bloodHitPrefab;
    void Start()
    {
        currentHealth = maxHealth;
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

    public void TakeDamage(float damage, Vector2 hitPoint)
    {
        if (isDead) return;
        if (currentHealth <= 0) return;

        currentHealth -= damage;
        SpawnBlood(hitPoint);
        PlayHitSound();
        // Показать цифру урона
        ShowDamagePopup(Mathf.RoundToInt(damage));

        OnHealthChanged?.Invoke(currentHealth, maxHealth);
        OnDamageTaken?.Invoke(); // вызываем эффект урона

        if (currentHealth <= 0)
            Die();
    }
    private void SpawnBlood(Vector2 hitPoint)
    {
        if (bloodHitPrefab == null)
            return;

        ParticleSystem blood = Instantiate(
            bloodHitPrefab,
            hitPoint,
            Quaternion.identity
        );

        blood.Play();

        Destroy(blood.gameObject, 2f);
    }
    void Die()
    {
        if (isDead) return;
        isDead = true;
        KillManager.Instance?.AddKill();
        onDeath?.Invoke();
    }

    void ShowDamagePopup(int damage)
    {
        Debug.Log($"ShowDamagePopup called with damage = {damage}");

        if (damagePopupPrefab == null)
        {
            Debug.LogError("damagePopupPrefab is null!");
            return;
        }

        Vector3 spawnPos = transform.position + popupOffset;
        GameObject popup = Instantiate(damagePopupPrefab, spawnPos, Quaternion.identity);

        DamagePopup dp = popup.GetComponent<DamagePopup>();
        if (dp != null)
        {
            dp.SetDamage(damage);
            Debug.Log($"DamagePopup created and SetDamage({damage}) called");
        }
        else
        {
            Debug.LogError("DamagePopup component not found on prefab!");
        }
    }

    private void PlayHitSound()
    {
        if (hitSound == null || audioSource == null)
            return;

        audioSource.pitch = Random.Range(hitPitchRange.x, hitPitchRange.y);
        audioSource.PlayOneShot(hitSound, hitVolume);
    }

}