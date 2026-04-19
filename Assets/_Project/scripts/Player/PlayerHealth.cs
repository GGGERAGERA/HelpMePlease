using UnityEngine;
using UnityEngine.UI;

public class PlayerHealth : MonoBehaviour
{
    public float maxHealth = 100f;
    private float currentHealth;
    public Slider healthSlider; // —юда перетащите HealthSlider из Canvas

    void Start()
    {
        currentHealth = maxHealth;
        if (healthSlider != null)
        {
            healthSlider.maxValue = maxHealth;
            healthSlider.value = currentHealth;
        }
    }

    public void TakeDamage(float damage)
    {
        Debug.Log("Slider value now: " + healthSlider.value);
        currentHealth -= damage;
        Debug.Log($"TakeDamage: {damage}, currentHealth={currentHealth}, healthSlider={(healthSlider == null ? "null" : "ok")}");
        if (healthSlider != null) healthSlider.value = currentHealth;
        if (currentHealth <= 0) Die();
    }

    void Die()
    {
        Debug.Log("Player died");
        gameObject.SetActive(false);
    }
}