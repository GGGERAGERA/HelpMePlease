using UnityEngine;
using System.Collections;

public class PlayerHealth : MonoBehaviour
{

    public float maxHealth = 100f;
    public float currentHealth;
    private bool isDead = false;
    public bool IsDead => isDead;
    public float CurrentHealth => currentHealth;
    public float MaxHealth => maxHealth;

    private PlayerWhiteFlash whiteFlash;
    private PlayerHitSound hitSound;


    [SerializeField] private float invulnerabilityTime = 0.5f;

    private bool isInvulnerable;

    void Start()
    {
        whiteFlash = GetComponent<PlayerWhiteFlash>();
        hitSound = GetComponent<PlayerHitSound>();
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
        currentHealth -= damage;

        whiteFlash?.Flash();
        hitSound?.Play();
        //     
        CameraShake.Instance?.Shake(0.12f, 0.08f);
        HUDManager.Instance?.SetHealth(currentHealth, maxHealth);
        if (currentHealth <= 0) Die();

        StartCoroutine(InvulnerabilityRoutine());

        return true;
    }
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