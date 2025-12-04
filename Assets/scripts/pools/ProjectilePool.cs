// ProjectilePool.cs
using UnityEngine;
using System.Collections.Generic;

public class ProjectilePool : MonoBehaviour
{
    public static ProjectilePool InstancePoolParent;

    [Header("Префабы снарядов")]
    public List<GameObject> projectilePrefabs; // ← сюда перетаскиваешь все префабы

    // Пул: тип → очередь снарядов
    private Dictionary<ProjectileSO.EProjectileType, Queue<Projectile>> pools = new();

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
            Projectile projectileComponent = prefab.GetComponent<Projectile>();
            if (projectileComponent == null || projectileComponent.projectileSO1 == null)
            {
                Debug.LogError($"Префаб {prefab.name} не имеет Projectile или projectileSO1!");
                continue;
            }

            ProjectileSO data = projectileComponent.projectileSO1;
            ProjectileSO.EProjectileType type = data.projectileType;

            if (!pools.ContainsKey(type))
                pools[type] = new Queue<Projectile>();

            for (int i = 0; i < data.ProjectileSpawnPoolCount; i++)
            {
                GameObject obj = Instantiate(prefab, transform);
                obj.SetActive(false);
                Projectile proj = obj.GetComponent<Projectile>();
                pools[type].Enqueue(proj);
            }

            Debug.Log($"Пул для {type}: {data.ProjectileSpawnPoolCount} снарядов");
        }
    }

    public Projectile GetProjectile(ProjectileSO.EProjectileType type)
    {
        if (!pools.TryGetValue(type, out var pool))
        {
            Debug.LogError($"Пул для {type} не найден!");
            return null;
        }

        if (pool.Count > 0)
        {
            var proj = pool.Dequeue();
            proj.gameObject.SetActive(true);
            return proj;
        }
        else
        {
            Debug.LogWarning($"Пул для {type} пуст. Создаём новый (но это плохо для производительности!).");
            // Лучше не создавать новые! Но на крайний случай:
            return Instantiate(GetPrefabByType(type), transform).GetComponent<Projectile>();
        }
    }

    public void ReturnProjectile(Projectile projectile)
    {
        if (projectile == null) return;
        projectile.gameObject.SetActive(false);
        ProjectileSO.EProjectileType type = projectile.projectileSO1.projectileType;
        if (pools.TryGetValue(type, out var pool))
        {
            pool.Enqueue(projectile);
        }
        else
        {
            Debug.LogWarning($"Невозможно вернуть снаряд типа {type} — пул не найден!");
        }
    }

    // Вспомогательный метод: найти префаб по типу (нужен только для emergency-создания)
    private GameObject GetPrefabByType(ProjectileSO.EProjectileType type)
    {
        foreach (var prefab in projectilePrefabs)
        {
            if (prefab.GetComponent<Projectile>()?.projectileSO1?.projectileType == type)
                return prefab;
        }
        return null;
    }
}