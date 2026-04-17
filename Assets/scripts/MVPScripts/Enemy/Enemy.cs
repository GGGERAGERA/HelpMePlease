using UnityEngine;

public class Enemy : MonoBehaviour
{
    public float speed = 2f;
    public float destroyDelay = 3f; // ����� �� ������� �������� ����� ������
    private Transform player;
    private Animator anim;
    private bool isDead = false;

    void Start()
    {
        player = GameObject.FindGameObjectWithTag("Player").transform;
        anim = GetComponentInChildren<Animator>();
    }

    void Update()
    {
        if (isDead) return; // �� ���������, ���� �������
        if (player == null) return;

        Vector3 direction = (player.position - transform.position).normalized;

        transform.Translate(direction * speed * Time.deltaTime, Space.World);
    }

    void Die()
    {
        if (isDead) return;
        isDead = true;
        // ��������� ��������, ���������, ����� ���� �� �����
        GetComponent<Collider2D>().enabled = false;
        // ��������� �������� ������
        if (anim != null)
        {
            anim.SetTrigger("Die");
            Debug.Log("Enemy DIE! Animation triggered.");
        }
        else
            Destroy(gameObject, 0.01f); // ���� ��������� ��� � ������� ����� �����

        // ������� ������ ����� ��������� �������� (����� �������� + ��������)
        Destroy(gameObject, destroyDelay);
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (isDead) return;
        if (other.CompareTag("Player"))
        {
            Debug.Log("Enemy DIE!");
            Die(); // ������ Destroy(gameObject)
            // �������������: ������� ���� ������
        }
        else if (other.CompareTag("Bullet"))
        {
            Die(); // ������ Destroy
            Destroy(other.gameObject); // ���� ���� ��������
        }
    }
}