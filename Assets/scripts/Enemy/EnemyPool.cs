

using System.Collections;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using System.Collections.Generic;
public class EnemyPool : MonoBehaviour
{
    public static EnemyPool InstanceEnemyPoolParent;

    [Header("Префабы врагов")]
    public List<GameObject> enemyPrefabs;

    private Dictionary<EnemySO.EEnemyType, Queue<EnemyBehavior>> pools = new();

    private void Awake()
    {
        if (InstanceEnemyPoolParent == null)
        {
            InstanceEnemyPoolParent = this;
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
        foreach (var prefab in enemyPrefabs)
        {
            EnemyBehavior enemyComponent = prefab.GetComponent<EnemyBehavior>();
            if (enemyComponent == null || enemyComponent.EnemySO1 == null)
            {
                Debug.LogError($"Префаб {prefab.name} не имеет Enemy или enemySO!");
                continue;
            }

            EnemySO data = enemyComponent.EnemySO1;
            EnemySO.EEnemyType type = data.enemyType;

            if (!pools.ContainsKey(type))
                pools[type] = new Queue<EnemyBehavior>();

            for (int i = 0; i < data.EnemySpawnPoolCount; i++)
            {
                GameObject obj = Instantiate(prefab, transform);
                obj.SetActive(false);
                EnemyBehavior enemy = obj.GetComponent<EnemyBehavior>();
                pools[type].Enqueue(enemy);
            }

            Debug.Log($"Пул для {type}: {data.EnemySpawnPoolCount} врагов");
        }
    }

    public EnemyBehavior GetEnemy(EnemySO.EEnemyType type)
    {
        if (!pools.TryGetValue(type, out var pool))
        {
            Debug.LogError($"Пул для {type} не найден!");
            return null;
        }

        if (pool.Count > 0)
        {
            var enemy = pool.Dequeue();
            enemy.gameObject.SetActive(true);
            return enemy;
        }
        else
        {
            Debug.LogWarning($"Пул для {type} пуст.");
            return null;
        }
    }

    public void ReturnEnemy(EnemyBehavior enemy)
    {
        if (enemy == null) return;
        enemy.gameObject.SetActive(false);
        EnemySO.EEnemyType type = enemy.EnemySO1.enemyType;
        if (pools.TryGetValue(type, out var pool))
        {
            pool.Enqueue(enemy);
        }
    }
}