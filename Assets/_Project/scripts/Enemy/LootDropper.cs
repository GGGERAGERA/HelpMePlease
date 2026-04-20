using UnityEngine;

public class LootDropper : MonoBehaviour
{
    [Header("Loot Settings")]
    public GameObject lootPrefab;      // префаб лута (монета, опыт и т.д.)
    public float dropChance = 1f;      // 1 = 100%, 0.5 = 50%
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
        // Проверяем шанс выпадения
        if (Random.value > dropChance) return;

        if (lootPrefab == null)
        {
            Debug.LogWarning("LootDropper: lootPrefab is null on " + name);
            return;
        }

        // Создаём лут в позиции врага + смещение
        Vector3 dropPosition = transform.position + dropOffset;
        Instantiate(lootPrefab, dropPosition, Quaternion.identity);
    }
}
