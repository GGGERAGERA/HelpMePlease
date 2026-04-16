using UnityEngine;

public class Bullet : MonoBehaviour
{
    private Transform target;
    private float speed;
    private bool hasTarget = false;

    // Этот метод будет вызывать скрипт Shoot
    public void SetTarget(Transform enemyTarget, float bulletSpeed)
    {
        target = enemyTarget;
        speed = bulletSpeed;
        hasTarget = true;
    }

    void Update()
    {
        if (!hasTarget)
        {
            Destroy(gameObject);
            return;
        }

        // Если цель уничтожена, самоуничтожаемся
        if (target == null)
        {
            Destroy(gameObject);
            return;
        }

        // Направление к цели
        Vector3 direction = (target.position - transform.position).normalized;
        transform.Translate(direction * speed * Time.deltaTime, Space.World);
    }

    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Enemy"))
        {
            Destroy(other.gameObject);
            Destroy(gameObject);
        }
        // Можно добавить, чтобы пуля исчезала при попадании в стену или игрока
    }
    void Start()
    {
        // Самоуничтожение через 2 секунды, чтобы пули не накапливались
        Destroy(gameObject, 2f);
    }
}
