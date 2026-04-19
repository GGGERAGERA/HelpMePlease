using UnityEngine;
using UnityEngine.Events; // обязательно добавить

public class EnemyHealth : MonoBehaviour
{
    public float maxHealth = 30f;
    private float currentHealth;

    public UnityEvent<float, float> OnHealthChanged; // (current, max)
    public UnityEvent onDeath;

    void Start()
    {
        currentHealth = maxHealth;
        OnHealthChanged?.Invoke(currentHealth, maxHealth);
    }

    public void TakeDamage(float damage)
    {
        if (currentHealth <= 0) return;
        currentHealth -= damage;
        OnHealthChanged?.Invoke(currentHealth, maxHealth);
        if (currentHealth <= 0)
            Die();
    }

    void Die()
    {
        onDeath?.Invoke();
    }
}