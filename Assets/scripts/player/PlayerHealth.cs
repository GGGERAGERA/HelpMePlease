using UnityEngine;

public class PlayerHealth : MonoBehaviour, IDamageable
{
    [Header("Здоровье")]
    [Tooltip("Ссылка на ScriptableObject с параметрами игрока")]
    [SerializeField] private PlayerStatsSO playerStatsSO;
    //[SerializeField] private PlayerBuffs PleerBuffs;
    private int currentHealth;

    public void Initialize()
    {
        currentHealth = playerStatsSO.playerMaxHealth;
        //currentHealth = maxHealth;
    }

    public void TakeDamage(int damage, Vector2 attackDirection, GameObject attacker)
    {
        currentHealth -= damage;
        Debug.Log($"{name} получил {damage} урона. Осталось: {currentHealth}");

        // Можно добавить: отдачу (rb.AddForce), визуальные эффекты, звук

        if (currentHealth <= 0)
        {
            Die();
        }
    }

    public void Die()
    {
        Debug.Log($"{name} умер!");
        // Анимация смерти, спавн частиц, отключение управления...
        gameObject.SetActive(false); // или Destroy(gameObject);
    }
}
