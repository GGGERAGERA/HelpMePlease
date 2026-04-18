using UnityEngine;
using UnityEngine.Events;

public class EnemyHealth : MonoBehaviour
{
    public float maxHealth = 100f;
    private float currentHealth;
    public UnityEvent<float> onDamageTaken; // событие при уроне для обновления UI
    public UnityEvent onDeath; // событие при смерти для уничтожения врага
    void Start()
    {
        currentHealth = maxHealth;
    }

  
    public void TakeDamage(float damage)
    {
        currentHealth -= damage;
        onDamageTaken.Invoke(currentHealth); // вызываем событие при уроне
        if (currentHealth <= 0)
        {
            Die();
        }
    }
    void Die()
    {
        onDeath.Invoke(); // вызываем событие при смерти
    }
}
