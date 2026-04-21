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
        currentHealth -= damage;
        if (healthSlider != null) healthSlider.value = currentHealth;
        if (currentHealth <= 0) Die();
    }

    void Die()
    {
        Debug.Log("Player died");

        GameOverManager gameOverManager = FindFirstObjectByType<GameOverManager>();
        if (gameOverManager != null)
            gameOverManager.GameOver();
        else
            Debug.LogError("GameOverManager not found!");

        gameObject.SetActive(false);
    }
}