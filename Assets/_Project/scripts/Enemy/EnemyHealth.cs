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

    void Start()
    {
        currentHealth = maxHealth;
        OnHealthChanged?.Invoke(currentHealth, maxHealth);
    }

    public void TakeDamage(float damage)
    {
        if (isDead) return;
        if (currentHealth <= 0) return;

        currentHealth -= damage;
        // Показать цифру урона
        ShowDamagePopup(Mathf.RoundToInt(damage));

        OnHealthChanged?.Invoke(currentHealth, maxHealth);
        OnDamageTaken?.Invoke(); // вызываем эффект урона

        if (currentHealth <= 0)
            Die();
    }

    void Die()
    {
        if (isDead) return;
        isDead = true;
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

}