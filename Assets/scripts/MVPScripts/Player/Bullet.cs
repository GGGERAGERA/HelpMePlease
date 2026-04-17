using UnityEngine;

public class Bullet : MonoBehaviour
{
    private Transform target;
    private float speed;
    private bool hasTarget = false;

    // ���� ����� ����� �������� ������ Shoot
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

        // ���� ���� ����������, ����������������
        if (target == null)
        {
            Destroy(gameObject);
            return;
        }

        // ����������� � ����
        Vector3 direction = (target.position - transform.position).normalized;
        transform.Translate(direction * speed * Time.deltaTime, Space.World);
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Enemy"))
        {
            Destroy(gameObject);
        }
        // ����� ��������, ����� ���� �������� ��� ��������� � ����� ��� ������
    }
    void Start()
    {
        // ��������������� ����� 2 �������, ����� ���� �� �������������
        Destroy(gameObject, 2f);
    }
}
