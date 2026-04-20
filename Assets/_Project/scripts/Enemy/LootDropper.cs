using UnityEngine;

public class LootDropper : MonoBehaviour
{
    [Header("Loot Settings")]
    public GameObject[] lootPrefabs;   // массив префабов (два или больше)
    public Vector3 dropOffset = Vector3.zero; // смещение от позиции врага

    private EnemyHealth enemyHealth;

    void Start()
    {
        enemyHealth = GetComponent<EnemyHealth>();
        if (enemyHealth != null)
            enemyHealth.onDeath.AddListener(DropLoot);
        else
            Debug.LogError("LootDropper: EnemyHealth component not found!");
    }

    void DropLoot()
    {
        if (lootPrefabs == null || lootPrefabs.Length == 0)
        {
            Debug.LogWarning("LootDropper: no loot prefabs assigned!");
            return;
        }

        // Выбираем случайный префаб из массива (равная вероятность для каждого)
        int randomIndex = Random.Range(0, lootPrefabs.Length);
        GameObject selectedLoot = lootPrefabs[randomIndex];

        // Создаём выбранный лут
        Vector3 dropPosition = transform.position + dropOffset;
        Instantiate(selectedLoot, dropPosition, Quaternion.identity);
    }
}