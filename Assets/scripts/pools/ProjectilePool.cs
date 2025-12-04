// ProjectilePool.cs
using UnityEngine;
using System.Collections.Generic;

public class ProjectilePool : MonoBehaviour
{
    public static ProjectilePool InstancePoolParent;

    [Header("Префабы снарядов")]
    public List<GameObject> projectilePrefabs; // ← сюда перетаскиваешь все префабы

    // Пул: тип → очередь снарядов
    private Dictionary<GameObject, Queue<Projectile>> pools = new();

    private void Awake()
    {
        if (InstancePoolParent == null)
        {
            InstancePoolParent = this;
            DontDestroyOnLoad(gameObject);
            InitializePools();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void InitializePools()
    {
        foreach (var prefab in projectilePrefabs)
        {
            if (prefab == null)
            {
                Debug.LogWarning("Пустой префаб в списке!");
                continue;
            }

            Projectile projComp = prefab.GetComponent<Projectile>();
            if (projComp == null || projComp.projectileSO1 == null)
            {
                Debug.LogError($"Префаб {prefab.name} не имеет Projectile или projectileSO1!");
                continue;
            }

            // Получаем количество объектов из SO
            int count = projComp.projectileSO1.ProjectileSpawnPoolCount;

            // Создаём пул для этого префаба
            pools[prefab] = new Queue<Projectile>();

            for (int i = 0; i < count; i++)
            {
                GameObject instance = Instantiate(prefab, transform);
                instance.SetActive(false);
                Projectile proj = instance.GetComponent<Projectile>();
                pools[prefab].Enqueue(proj);
            }

            Debug.Log($"✅ Пул для {prefab.name}: {count} снарядов");
        }
    }

    /// Получить снаряд из пула для указанного префаба.
    /// Если пул исчерпан — создаёт новый (динамический пул).
    public Projectile GetProjectile(GameObject prefab)
    {
        if (prefab == null)
        {
            Debug.LogError("GetProjectile: передан null-префаб!");
            return null;
        }

        // Проверяем, есть ли пул для этого префаба
        if (!pools.TryGetValue(prefab, out var pool))
        {
            Debug.LogError($"Пул для префаба {prefab.name} не инициализирован! Добавь его в projectilePrefabs.");
            return null;
        }

        // Если есть свободные снаряды — берём
        if (pool.Count > 0)
        {
            var proj = pool.Dequeue();
            proj.gameObject.SetActive(true);
            return proj;
        }

        // 🔥 Экстренный случай: создаём новый снаряд
        Debug.LogWarning($"Пул для {prefab.name} пуст. Создаём дополнительный снаряд (динамический пул).");
        GameObject newProjGO = Instantiate(prefab, transform);
        newProjGO.SetActive(true);
        return newProjGO.GetComponent<Projectile>();
    }

    /// Вернуть снаряд в пул. Если снаряд создан динамически — уничтожаем или игнорируем.
    public void ReturnProjectile(Projectile projectile)
    {
        if (projectile == null) return;

        // 💡 Определяем, из какого префаба этот снаряд
        // Поскольку у нас нет прямой ссылки — ищем вручную
        // Но у нас есть projectileSO1 → а у SO есть ссылка на свой префаб? Нет.
        // Поэтому — храним префаб в самом Projectile!

        // 👇 ЭТО КЛЮЧ КО ВСЕМУ!
        GameObject sourcePrefab = projectile.GetSourcePrefab();
        if (sourcePrefab == null)
        {
            Debug.LogWarning($"Не могу вернуть снаряд — неизвестный префаб.");
            projectile.gameObject.SetActive(false);
            return;
        }

        if (pools.TryGetValue(sourcePrefab, out var pool))
        {
            projectile.gameObject.SetActive(false);
            pool.Enqueue(projectile);
        }
        else
        {
            // Если префаб не в пуле — это динамический снаряд → просто деактивируем
            projectile.gameObject.SetActive(false);
            // Можно уничтожить через пару секунд, но лучше вернуть в пул.
        }
    }
}