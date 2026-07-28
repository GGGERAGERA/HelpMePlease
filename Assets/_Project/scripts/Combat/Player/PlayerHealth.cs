using UnityEngine;
using System.Collections;

public class PlayerHealth : MonoBehaviour
{
    public event System.Action DamageTaken;

    public float maxHealth = 100f;
    public float currentHealth;
    private bool isDead = false;
    public bool IsDead => isDead;
    public float CurrentHealth => currentHealth;
    public float MaxHealth => maxHealth;

    private PlayerWhiteFlash whiteFlash;
    private PlayerHitSound hitSound;
    private CharacterMovement2D movement;


    [SerializeField] private float invulnerabilityTime = 0.6f;
    [SerializeField, Min(0f)] private float incomingDamageMultiplier = 1f;

    private bool isInvulnerable;

    private void Awake()
    {
        whiteFlash = GetComponent<PlayerWhiteFlash>();
        hitSound = GetComponent<PlayerHitSound>();
        movement = GetComponent<CharacterMovement2D>();
    }

    void Start()
    {
        // CharacterSpawner can restore a run snapshot before this Start runs.
        // Only initialize health when no valid runtime value was supplied.
        if (currentHealth <= 0f)
            currentHealth = maxHealth;

        HUDManager.Instance?.SetHealth(currentHealth, maxHealth);
    }

    public bool TakeDamage(float damage, Vector2 hitDirection)
    {
        if (isDead)
            return false;
        if (isInvulnerable)
            return false;
        isInvulnerable = true;
        float finalDamage = Mathf.Max(0f, damage) * incomingDamageMultiplier;
        currentHealth -= finalDamage;

        movement?.ApplyKnockback(hitDirection);
        whiteFlash?.Flash();

        if (currentHealth > 0f)
        {
            if (AudioService.Instance != null)
                AudioService.Instance.PlayAt(AudioCueId.PlayerHurt, transform.position);
            else
                hitSound?.Play();
        }

        //     
        CameraShake.Instance?.Shake(0.12f, 0.08f);
        HUDManager.Instance?.SetHealth(currentHealth, maxHealth);
        if (currentHealth <= 0) Die();

        DamageTaken?.Invoke();

        StartCoroutine(InvulnerabilityRoutine());

        return true;
    }
    public void SetIncomingDamageMultiplier(float multiplier)
    {
        incomingDamageMultiplier = Mathf.Max(0f, multiplier);
    }

    public float IncomingDamageMultiplier => incomingDamageMultiplier;
    private IEnumerator InvulnerabilityRoutine()
    {
        yield return new WaitForSeconds(invulnerabilityTime);
        isInvulnerable = false;
    }
    public void SetCurrentHealth(int value)
    {
        currentHealth = Mathf.Clamp(value, 0, maxHealth);
        HUDManager.Instance?.SetHealth(currentHealth, maxHealth);
    }
    public void Heal(float amount)
    {
        currentHealth += amount;
        if (currentHealth > maxHealth)
            currentHealth = maxHealth;

        HUDManager.Instance?.SetHealth(currentHealth, maxHealth);
    }
    void Die()
    {
        Debug.Log("Player died");
        if (isDead) return;
        isDead = true;

        AudioService.Instance?.PlayAt(
            AudioCueId.PlayerDeath,
            transform.position
        );

        //   
        if (CameraShake.Instance != null)
            CameraShake.Instance.StopAllShakes();

        //   
        CharacterMovement2D movement = GetComponent<CharacterMovement2D>();
        if (movement != null) movement.enabled = false;



        //    (   !)
        SpriteRenderer[] renderers = GetComponentsInChildren<SpriteRenderer>();
        foreach (SpriteRenderer sr in renderers)
        {
            sr.enabled = false;
        }

        //      (  )
        // gameObject.SetActive(false);

        //   GameOver
        if (GameOverManager.Instance != null)
            GameOverManager.Instance.GameOver();
    }

    public void AddMaxHealth(float amount)
    {
        maxHealth += amount;
        currentHealth += amount;

        HUDManager.Instance?.SetHealth(currentHealth, maxHealth);
    }
    public void SetRuntimeHealth(float maxHealthValue, float currentHealthValue)
    {
        maxHealth = Mathf.Max(1f, maxHealthValue);
        currentHealth = Mathf.Clamp(currentHealthValue, 1f, maxHealth);
        isDead = false;

        HUDManager.Instance?.SetHealth(currentHealth, maxHealth);
    }
}
