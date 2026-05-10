using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class PlayerHealth : MonoBehaviour
{
    public float maxHealth = 100f;
    public float currentHealth;
    public Slider healthSlider; // Сюда перетащите HealthSlider из Canvas

    private bool isDead = false;
    public bool IsDead => isDead;
    private PlayerWhiteFlash whiteFlash;
    private PlayerHitSound hitSound;


    [SerializeField] private float invulnerabilityTime = 0.5f;

    private bool isInvulnerable;

    void Start()
    {
        whiteFlash = GetComponent<PlayerWhiteFlash>();
        hitSound = GetComponent<PlayerHitSound>();
        currentHealth = maxHealth;
        if (healthSlider != null)
        {
            healthSlider.maxValue = maxHealth;
            healthSlider.value = currentHealth;
        }
    }

    public void TakeDamage(float damage)
    {
        if (isInvulnerable)
            return;
        isInvulnerable = true;
        currentHealth -= damage;
        whiteFlash?.Flash();
        hitSound?.Play();
        // Сильная тряска при получении урона
        CameraShake.Instance?.Shake(0.12f, 0.08f);
        if (healthSlider != null) healthSlider.value = currentHealth;
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
        if (healthSlider != null)
            healthSlider.value = currentHealth;
    }
    public void Heal(int amount)
    {
        currentHealth += amount;
        if (currentHealth > maxHealth)
            currentHealth = maxHealth;

        if (healthSlider != null)
            healthSlider.value = currentHealth;
    }
    void Die()
    {
        Debug.Log("Player died");

        // Останавливаем тряску камеры
        if (CameraShake.Instance != null)
            CameraShake.Instance.StopAllShakes();

        // Отключаем движение игрока
        CharacterMovement2D movement = GetComponent<CharacterMovement2D>();
        if (movement != null) movement.enabled = false;

        // Отключаем стрельбу (все оружия)
        Shoot shoot = GetComponent<Shoot>();
        if (shoot != null) shoot.enabled = false;

        OrbitalWeapon orbital = GetComponentInChildren<OrbitalWeapon>();
        if (orbital != null) orbital.enabled = false;

        LaserSword sword = GetComponentInChildren<LaserSword>();
        if (sword != null) sword.enabled = false;

        if (isDead) return;
        isDead = true;

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
}