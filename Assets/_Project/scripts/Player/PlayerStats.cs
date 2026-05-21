using UnityEngine;

public class PlayerStats : MonoBehaviour
{
    public static PlayerStats Instance;

    [Header("Combat")]
    public int baseDamage = 10;
    private float damageMultiplier = 1f;

    [Header("Health")]
    public float maxHealth = 100f;
    public float currentHealth;

    [Header("Movement")]
    public float moveSpeed = 5f;

    void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
            Destroy(gameObject);
    }

    void Start()
    {
        currentHealth = maxHealth;
    }

    public int GetDamage()
    {
        return Mathf.RoundToInt(baseDamage * damageMultiplier);
    }

    public void IncreaseDamage(int amount)
    {
        baseDamage += amount;
        Debug.Log($"Damage increased to {GetDamage()}");
    }

    public void SetHealth(float health)
    {
        currentHealth = health;
        maxHealth = health;
    }

    public void TakeDamage(float damage)
    {
        currentHealth -= damage;
        if (currentHealth <= 0)
            Die();
    }

    public void IncreaseDamageMultiplier(float amount)
    {
        damageMultiplier += amount;
    }

    void Die()
    {
        Debug.Log("Player died from stats");
        // Здесь можно вызвать GameOver или отключить игрока
    }
}