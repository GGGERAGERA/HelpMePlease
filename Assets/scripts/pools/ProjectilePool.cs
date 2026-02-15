using UnityEngine;
using System.Collections.Generic;

public class ProjectilePool
{
    // Сегодня Герман пришёл сюда. 16 февраля 2026. Это уже что-то.
    //private Dictionary<GameObject, Queue<Projectile>> pools = new Dictionary<GameObject, Queue<Projectile>>();
    private GameObject prefab;
    private Transform poolParent;
    private Queue<Projectile> availableProjectiles; // ← очередь свободных снарядов

    public ProjectilePool(GameObject projectilePrefab, int initialSize, Transform parent)
    {
        this.prefab = projectilePrefab;
        this.poolParent = parent;
        this.availableProjectiles = new Queue<Projectile>();

        // Создаём начальные снаряды и кладём в очередь
        for (int i = 0; i < initialSize; i++)
        {
            GameObject obj = GameObject.Instantiate(prefab, poolParent);
            Projectile proj = obj.GetComponent<Projectile>();
            proj.Initialize(this); // ← говорим: "вот твой пул"
            availableProjectiles.Enqueue(proj);
        }
    }

    // Получить снаряд из пула
    public Projectile GetProjectile()
    {
        if (availableProjectiles.Count > 0)
        {
            Projectile proj = availableProjectiles.Dequeue(); // ← достаём из головы очереди
            return proj;
        }

        // ⚠️ КРИТИЧЕСКИЙ МОМЕНТ: не хватает — создаём новый!
        GameObject newObj = GameObject.Instantiate(prefab, poolParent);
        Projectile newProj = newObj.GetComponent<Projectile>();
        newProj.Initialize(this); // ← не забываем!
        Debug.Log("Пулл расширен: создан новый снаряд.");
        return newProj;
    }

    // Возврат снаряда в пул (вызывается из самого снаряда)
    public void ReturnProjectile(Projectile proj)
    {
        proj.gameObject.SetActive(false);
        availableProjectiles.Enqueue(proj); // ← кладём в конец очереди
    }
}