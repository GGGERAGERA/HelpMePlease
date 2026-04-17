using UnityEngine;

public class Enemy : MonoBehaviour
{
    public float speed = 2f;
    public float destroyDelay = 3f; // Время до уничтожения объекта
    private Transform player;
    private Animator anim;
    private bool isDead = false;

    public float damage = 20f; // урон от врага игроку
    private Health health;

    void Start()
    {
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null)
            player = playerObj.transform;
        else
            Debug.LogWarning("Enemy: Player not found!");
        anim = GetComponentInChildren<Animator>();
        health = player.GetComponent<Health>();
            if (health == null)
                health = player.gameObject.AddComponent<Health>(); // Добавляем компонент Health, если его нет
        health.maxHealth = 50f; 
    }

    void Update()
    {
        if (isDead) return; // Если мертв, ничего не делаем
        if (player == null) return;

        Vector3 direction = (player.position - transform.position).normalized;

        transform.Translate(direction * speed * Time.deltaTime, Space.World);
    }

    void Die()
    {
        if (isDead) return;
        isDead = true;
        // Отключаем коллайдер, чтобы враг не взаимодействовал с другими объектами
        GetComponent<Collider2D>().enabled = false;
        // Запускаем анимацию смерти
        if (anim != null)
        {
            anim.SetTrigger("Die");
        }
        else
            Destroy(gameObject, 0.01f); // Уничтожение объекта, например.

        // Уничтожение объекта через заданное время
        Destroy(gameObject, destroyDelay);
    }

    void OnTriggerEnter2D(Collider2D other)
    {

        if (isDead) return;
        if (other.CompareTag("Player"))
        {
            PlayerHealth playerHealth = other.GetComponent<PlayerHealth>();
            if (playerHealth != null)
                playerHealth.TakeDamage(20f);
            Die(); // Уничтожаем врага
            // TODO: Добавить эффекты смерти
        }
        else if (other.CompareTag("Bullet"))
        {
            Die(); // Уничтожаем врага
            Destroy(other.gameObject); // Уничтожаем пулю
        }
    }
}