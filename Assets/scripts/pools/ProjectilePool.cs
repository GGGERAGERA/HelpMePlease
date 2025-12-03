// ProjectilePool.cs
using UnityEngine;
using System.Collections.Generic;
using UnityEditor;

public class ProjectilePool : MonoBehaviour
{
    public static ProjectilePool InstancePoolParent;
    //public GameObject ProjectilePrefabForPool;

    //System.Serializable для стракта
    [System.Serializable]
    public struct PoolConfig
    {
        public ProjectileSO.EProjectileType type;
        public Projectile prefab;
        //public int initialSize;
        public int initialSize;
    }

    [Header("Настройка пулов по типам")]
    
    public List<PoolConfig> poolConfigs;
    public List<GameObject> ProjectilePrefabsForPool;
    public List<GameObject> ProjectilePrefabsPooled;
    private ProjectileSO SpawnProjectileCountFromSO;
    private int SpawnProjectileCount;
    //public int ProjectilePrefabForPoolPlace = 0;

    // Словарь: тип → очередь снарядов
    private Dictionary<ProjectileSO.EProjectileType, Queue<Projectile>> pools = new Dictionary<ProjectileSO.EProjectileType, Queue<Projectile>>();

    // Кэш префабов для быстрого доступа
    private Dictionary<ProjectileSO.EProjectileType, Projectile> prefabCache = new Dictionary<ProjectileSO.EProjectileType, Projectile>();

    //private Dictionary<GameObject, ProjectileSO.EProjectileType> ProjectilePrefabsForPool2 = new Dictionary<GameObject, ProjectileSO.EProjectileType>();
    //Если уже есть пулл объект на сцене- уничтожаем его
    private void Awake()
    {
        
        if (InstancePoolParent == null)
        {
            InstancePoolParent = this;
            DontDestroyOnLoad(InstancePoolParent);
            InitializePools2();
        }
        else
        {
            Destroy(InstancePoolParent);
        }
    }

    //Инициализация пула через стракт
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

    //Инициализация пула через префаб пули (projectile)
    public void InitializePools2()
    {
        
        //if(ProjectilePrefabsForPool == null) return;
        Debug.LogWarning($"Попытка инициализировать пулл2");
        GameObject ObjectPoolContainer;
        for(int i = 0; i < ProjectilePrefabsForPool.Count; i++)
        {
            //Пипец.. Забираем данные SO из ячеек префабов в пулле
            SpawnProjectileCountFromSO = ProjectilePrefabsForPool[i].GetComponent<Projectile>().projectileSO1;
            //Пипец.. Какой бред, считать SO не можем, и типа забиваем всё в конкретную переменную...
            SpawnProjectileCount = SpawnProjectileCountFromSO.ProjectileSpawnPoolCount;
            int k = SpawnProjectileCount;

            Debug.LogWarning($"Попытка инициализировать пулл3 {i }, {k }");
            //if (InstancePoolParent.GetComponentInChildren<Projectile>() == ProjectilePrefabsForPool[i]) return;
                for (int j = 0; j < SpawnProjectileCount; j++)
            //ProjectilePrefabsForPool[i].GetComponent<ProjectileConfig>().projectileData.ProjectileSpawnPoolCount
                {
                ObjectPoolContainer = Instantiate(ProjectilePrefabsForPool[i]);
                ObjectPoolContainer.SetActive(false);
                ProjectilePrefabsPooled.Add(ObjectPoolContainer);
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

    /// Получить снаряд указанного типа через префаб снаряда!
    public GameObject GetProjectile2(GameObject GetProjectilePrefab)
    {

        // Если пул исчерпан — создаём новый    
                for(int j = 0; j < ProjectilePrefabsForPool[j].GetComponent<Projectile>().CurrentPrefabsForSpawn; j++)
                {
                    if(!ProjectilePrefabsPooled[j].activeInHierarchy)
                    {
                    return ProjectilePrefabsPooled[j];
                    }

                    if(ProjectilePrefabsPooled[j] == null)
                    {
                    Debug.LogWarning($"Пул для {GetProjectilePrefab} исчерпан. Создаём новый.");
                    return Instantiate(GetProjectilePrefab, transform);
                    }

                }
                return null;
        
    }

    /// Вернуть снаряд в пул
    public void ReturnProjectile(Projectile Returnproj)
    {
        if (Returnproj == null) return;

        ProjectileSO.EProjectileType type = Returnproj.Type; // ← Берём тип из самого снаряда

        if (pools.ContainsKey(type))
        {
            Returnproj.gameObject.SetActive(false);
            Returnproj.transform.SetParent(transform);
            pools[type].Enqueue(Returnproj);
        }
        else
        {
            Destroy(Returnproj.gameObject);
        }
    }

    /// Вернуть снаряд в пул через префаб!
    public void ReturnProjectile2(Projectile ReturnProjectile1)
    {
        if (ReturnProjectile1 == null) return;

        ProjectileSO.EProjectileType type = ReturnProjectile1.Type; // ← Берём тип из самого снаряда

        if (pools.ContainsKey(type))
        {
            ReturnProjectile1.gameObject.SetActive(false);
            ReturnProjectile1.transform.SetParent(transform);
            pools[type].Enqueue(ReturnProjectile1);
        }
        else
        {
            Destroy(ReturnProjectile1.gameObject);
        }
    }

}