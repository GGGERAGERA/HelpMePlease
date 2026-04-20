using UnityEngine;

public class Pickup : MonoBehaviour
{
    public int value = 1;        // количество опыта/монет
    public float pickupRadius = 0.5f; // радиус подбора (опционально)

    void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            Debug.Log($"Loot triggered with: {other.name}, tag = {other.tag}");
            // Здесь можно добавить логику: увеличить счёт, опыт, здоровье и т.д.
            Debug.Log($"Loot picked up! Value: {value}");

            // Пример: увеличиваем счёт игрока
            ScoreManager.Instance.AddScore(value);
            // Можно добавить звук, эффект и т.д.
            Destroy(gameObject);
        }
    }
}
