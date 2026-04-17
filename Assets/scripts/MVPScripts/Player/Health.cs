using UnityEngine;
using UnityEngine.UI;

public class Health : MonoBehaviour
{
    public float maxHealth = 100f;
    private float currentHealth;

    public GameObject healthBarPrefab; // Префаб полоски здоровья
    public GameObject healthBarInstance; // Экземпляр полоски здоровья
    private Image fillImage; // Компонент Image для изменения заполнения полоски здоровья
    private Canvas healthBarCanvas; // Канвас для отображения полоски здоровья
    void Start()
    {
        currentHealth = maxHealth;
        if (healthBarPrefab != null)
        {
            healthBarInstance = Instantiate(healthBarPrefab, transform.position, Quaternion.identity);
            healthBarCanvas = healthBarInstance.GetComponentInParent<Canvas>();
            fillImage = healthBarInstance.GetComponentInChildren<Image>();
            if (fillImage == null)
                Debug.LogError("Health bar prefab must have an Image component for the fill.");
            UpdateHealthBar();
        }
    }

    private void LateUpdate()
    {
        if (healthBarInstance != null)
        {
            healthBarInstance.transform.position = transform.position + new Vector3(0, 1.5f, 0); // Позиция над игроком
            healthBarInstance.transform.rotation = healthBarCanvas.transform.rotation; // Сохранение ориентации канваса
        }
    }

    public void TakeDamage(float damage)
    {
        currentHealth -= damage;
        if (currentHealth <= 0)
        {
            currentHealth = 0;
            Die();
        }
    }

    void UpdateHealthBar()
    {
        if (fillImage != null)
        {
            fillImage.fillAmount = currentHealth / maxHealth;
        }
    }

    private void Die()
    {
        if (healthBarInstance != null)
            Destroy(healthBarInstance);
        Destroy(gameObject);
    }

    private void OnDestroy()
    {
        // Удаляем хп-бар, если объект уничтожен
        if (healthBarInstance != null)
            Destroy(healthBarInstance);
    }
}
