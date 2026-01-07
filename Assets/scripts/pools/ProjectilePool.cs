using UnityEngine;
using System.Collections.Generic;

public class ProjectilePool : MonoBehaviour
{
    private Dictionary<GameObject, Queue<Projectile>> pools = new Dictionary<GameObject, Queue<Projectile>>();

    public void AddProjectileType(GameObject prefab)
    {
        if (prefab == null || pools.ContainsKey(prefab)) return;

        Projectile proj = prefab.GetComponent<Projectile>();
        if (proj == null)
        {
            Debug.LogError($"Префаб {prefab.name} не имеет компонента Projectile!");
            return;
        }

        // Берём количество из самого компонента (можно добавить поле, см. ниже)
        int count = 20; // или proj.poolSize, если добавишь

        var queue = new Queue<Projectile>();
        for (int i = 0; i < count; i++)
        {
            GameObject go = Instantiate(prefab, transform);
            go.SetActive(false);
            queue.Enqueue(go.GetComponent<Projectile>());
        }

        pools[prefab] = queue;
        Debug.Log($"Пул для {prefab.name}: {count} снарядов");
    }

    public Projectile GetProjectile(GameObject prefab)
    {
        if (prefab == null) return null;
        if (!pools.ContainsKey(prefab)) AddProjectileType(prefab);

        var pool = pools[prefab];
        return pool.Count > 0 
            ? pool.Dequeue().Also(p => p.gameObject.SetActive(true)) 
            : EmergencyCreate(prefab);
    }

    private Projectile EmergencyCreate(GameObject prefab)
    {
        Debug.LogWarning($"Пул исчерпан для {prefab.name}");
        GameObject go = Instantiate(prefab, transform);
        go.SetActive(true);
        return go.GetComponent<Projectile>();
    }

    public void ReturnProjectile(Projectile proj)
    {
        if (proj == null) return;
        GameObject source = proj.GetSourcePrefab();
        if (source == null) { proj.gameObject.SetActive(false); return; }

        if (pools.TryGetValue(source, out var pool))
        {
            proj.gameObject.SetActive(false);
            pool.Enqueue(proj);
        }
        else
        {
            AddProjectileType(source);
            pools[source].Enqueue(proj);
            proj.gameObject.SetActive(false);
        }
    }
}

// Маленький helper, чтобы не писать лишнее
public static class GameObjectExtensions
{
    public static T Also<T>(this T obj, System.Action<T> action)
    {
        action(obj);
        return obj;
    }
}