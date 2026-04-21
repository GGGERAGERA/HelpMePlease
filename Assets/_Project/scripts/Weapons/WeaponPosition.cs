using System.Diagnostics;
using UnityEngine;
using Debug = UnityEngine.Debug;

public class WeaponPosition : MonoBehaviour
{
    public Transform player;          // ссылка на игрока (перетащить в инспекторе)
    public Vector2 offsetRight = new Vector2(0.6f, 0.2f); // положение справа
    public Vector2 offsetLeft = new Vector2(-0.6f, 0.2f); // положение слева
    private Vector2 currentOffset;
    private Transform targetEnemy;
    private bool isFacingRight = true;


    void Update()
    {
        targetEnemy = FindClosestEnemy();
        if (targetEnemy != null)
        {
            float direction = targetEnemy.position.x - player.position.x;
            currentOffset = (direction > 0) ? offsetRight : offsetLeft;
        }
        else
        {
            currentOffset = offsetRight;
        }

        // Позиционируем оружие относительно игрока
        transform.position = player.position + (Vector3)currentOffset;

        // Определяем, нужно ли смотреть вправо (если offsetRight - смотрим вправо)
        bool shouldFaceRight = (currentOffset == offsetRight);
        if (shouldFaceRight != isFacingRight)
        {
            isFacingRight = shouldFaceRight;
            Vector3 scale = transform.localScale;
            scale.x = Mathf.Abs(scale.x) * (isFacingRight ? 1 : -1);
            transform.localScale = scale;
        }
    }

    Transform FindClosestEnemy()
    {
        GameObject[] enemies = GameObject.FindGameObjectsWithTag("Enemy");
        Transform closest = null;
        float minDist = Mathf.Infinity;
        foreach (GameObject enemy in enemies)
        {
            float dist = Vector2.Distance(player.position, enemy.transform.position);
            if (dist < minDist)
            {
                minDist = dist;
                closest = enemy.transform;
            }
        }
        return closest;
    }
}