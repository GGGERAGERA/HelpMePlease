using UnityEngine;

public class WeaponAim : MonoBehaviour
{
    private Transform firePoint; // точка вылета пули (обычно дочерний объект)
    private Transform target;    // текущий враг

    void Start()
    {
        firePoint = GetComponent<Transform>(); // или назначьте вручную
    }

    void Update()
    {
        // Находим ближайшего врага
        target = FindClosestEnemy();
        if (target != null)
        {
            // Направление от оружия к врагу
            Vector2 direction = target.position - transform.position;
            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.Euler(0, 0, angle);
        }

    }

    Transform FindClosestEnemy()
    {
        GameObject[] enemies = GameObject.FindGameObjectsWithTag("Enemy");
        Transform closest = null;
        float minDist = Mathf.Infinity;
        foreach (GameObject enemy in enemies)
        {
            float dist = Vector2.Distance(transform.position, enemy.transform.position);
            if (dist < minDist)
            {
                minDist = dist;
                closest = enemy.transform;
            }
        }
        return closest;
    }
}
