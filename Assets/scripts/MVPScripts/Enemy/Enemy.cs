using UnityEngine;

public class Enemy : MonoBehaviour
{
    public float speed = 2f;
    public float destroyDelay = 3f; // Время до полного удаления после смерти
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
        if (isDead) return; // Не двигаемся, если умираем
        if (player == null) return;

        Vector3 direction = (player.position - transform.position).normalized;

        transform.Translate(direction * speed * Time.deltaTime, Space.World);
    }

    void Die()
    {
        if (isDead) return;
        isDead = true;
        // Отключаем движение, коллайдер, чтобы враг не мешал
        GetComponent<Collider>().enabled = false;
        // Запускаем анимацию смерти
        if (anim != null)
        {
            anim.SetTrigger("Die");
            Debug.Log("Enemy DIE! Animation triggered.");
        }
        else
            Destroy(gameObject, 0.01f); // если аниматора нет — удаляем почти сразу

        // Удаляем объект после окончания анимации (длина анимации + задержка)
        Destroy(gameObject, destroyDelay);
    }

    void OnTriggerEnter(Collider other)
    {
        if (isDead) return;
        if (other.CompareTag("Player"))
        {
            Debug.Log("Enemy DIE!");
            Die(); // вместо Destroy(gameObject)
            // Дополнительно: нанести урон игроку
        }
        else if (other.CompareTag("Bullet"))
        {
            Die(); // вместо Destroy
            Destroy(other.gameObject); // пуля тоже исчезает
        }
    }
}