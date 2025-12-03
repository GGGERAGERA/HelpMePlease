// ProjectilePool.cs
using UnityEngine;
using System.Collections.Generic;

public class ProjectilePool : MonoBehaviour
{
    public static ProjectilePool Instance;

    [System.Serializable]
    public struct PoolConfig
    {
        //public ProjectileSO.EProjectileType type;
        public ProjectileSO.EProjectileType type;
        public Projectile prefab;
        //public int initialSize;
        public int initialSize;
    }

    [Header("Настройка пулов по типам")]
    public List<PoolConfig> poolConfigs;

    // Словарь: тип → очередь снарядов
    private Dictionary<ProjectileSO.EProjectileType, Queue<Projectile>> pools = new Dictionary<ProjectileSO.EProjectileType, Queue<Projectile>>();

    // Кэш префабов для быстрого доступа
    private Dictionary<ProjectileSO.EProjectileType, Projectile> prefabCache = new Dictionary<ProjectileSO.EProjectileType, Projectile>();

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
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
        foreach (var config in poolConfigs)
        {
            Queue<Projectile> pool = new Queue<Projectile>();
            //var projectileType = ProjectileSO ;
            
            pools[config.type] = pool;
            prefabCache[config.type] = config.prefab; // Кэшируем префаб

            //pools[config.prefab.GetComponent<ProjectileSO.EProjectileType>()] = pool;
            //prefabCache[config.prefab.GetComponent<ProjectileSO.EProjectileType>()] = config.prefab; // Кэшируем префаб

            for (int i = 0; i < config.initialSize; i++)
            //for (int i = 0; i < config.prefab.GetComponent<ProjectileSO>().initialSize; i++)
            {
                Projectile proj = Instantiate(config.prefab, transform);
                proj.gameObject.SetActive(false);
                pool.Enqueue(proj);
            }
        }
    }


    /// Получить снаряд указанного типа
    public Projectile GetProjectile(ProjectileSO.EProjectileType type)
    {
        if (!pools.ContainsKey(type))
        {
            Debug.LogError($"Пул для типа {type} не настроен!");
            return null;
        }

        Queue<Projectile> pool = pools[type];
        if (pool.Count > 0)
        {
            Projectile proj = pool.Dequeue();
            proj.gameObject.SetActive(true);
            return proj;
        }
        else
        {
            // Если пул исчерпан — создаём новый
            if (prefabCache.TryGetValue(type, out Projectile prefab))
            {
                Debug.LogWarning($"Пул для {type} исчерпан. Создаём новый.");
                return Instantiate(prefab, transform);
            }
            return null;
        }
    }


    /// Вернуть снаряд в пул
    public void ReturnProjectile(Projectile proj)
    {
        if (proj == null) return;

        ProjectileSO.EProjectileType type = proj.Type; // ← Берём тип из самого снаряда

        if (pools.ContainsKey(type))
        {
            proj.gameObject.SetActive(false);
            proj.transform.SetParent(transform);
            pools[type].Enqueue(proj);
        }
        else
        {
            Destroy(proj.gameObject);
        }
    }
}