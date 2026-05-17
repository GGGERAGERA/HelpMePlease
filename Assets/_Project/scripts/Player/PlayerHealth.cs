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

    public void TakeDamage(float damage, Vector2 hitDirection)
    {
        if (isInvulnerable)
            return;
        isInvulnerable = true;
        currentHealth -= damage;

        whiteFlash?.Flash();
        hitSound?.Play();
        // Сильная тряска при получении урона
        CameraShake.Instance?.Shake(0.12f, 0.08f);
        HUDManager.Instance?.SetHealth(currentHealth, maxHealth);
        if (currentHealth <= 0) Die();

        StartCoroutine(InvulnerabilityRoutine());
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
    public void Heal(int amount)
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
        // Останавливаем тряску камеры
        if (CameraShake.Instance != null)
            CameraShake.Instance.StopAllShakes();

        // Отключаем движение игрока
        CharacterMovement2D movement = GetComponent<CharacterMovement2D>();
        if (movement != null) movement.enabled = false;

        OrbitalWeapon orbital = GetComponentInChildren<OrbitalWeapon>();
        if (orbital != null) orbital.enabled = false;

        LaserSword sword = GetComponentInChildren<LaserSword>();
        if (sword != null) sword.enabled = false;



        // Скрываем спрайт игрока (ищем на дочерних объектах!)
        SpriteRenderer[] renderers = GetComponentsInChildren<SpriteRenderer>();
        foreach (SpriteRenderer sr in renderers)
        {
            sr.enabled = false;
        }

        // ИЛИ просто деактивируем весь объект (проще и надёжнее)
        // gameObject.SetActive(false);

        // Показываем панель GameOver
        if (GameOverManager.Instance != null)
            GameOverManager.Instance.GameOver();
    }

    public void AddMaxHealth(float amount)
    {
        maxHealth += amount;
        currentHealth += amount;

        HUDManager.Instance?.SetHealth(currentHealth, maxHealth);
    }
}