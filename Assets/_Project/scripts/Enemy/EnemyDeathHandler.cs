using UnityEngine;


// Этот класс будет обрабатывать смерть врага, например, проигрывать анимацию смерти, спавнить предметы и т.д.
public class EnemyDeathHandler : MonoBehaviour
{
    public Animator anim;
    public float destroyDelay = 2f; // время до уничтожения объекта после смерти

    void Start()
    {
        // Ищем Animator на этом объекте или на любом дочернем
        anim = GetComponentInChildren<Animator>();
        if (anim == null)
            Debug.LogWarning("Animator not found in " + name);

        // Подписываемся на событие смерти
        var health = GetComponent<EnemyHealth>();
        if (health != null)
            health.onDeath.AddListener(HandleDeath);
        else
            Debug.LogError("EnemyHealth component missing on " + name);
    }

    void HandleDeath()
    {
        if (anim != null)
        {
            anim.SetTrigger("Die");
            Debug.Log("Death animation triggered for " + name);
        }
        else
            Debug.LogWarning("Animator is null, cannot play death animation");

        // Отключаем коллайдер и движение (опционально)
        var collider = GetComponent<Collider2D>();
        if (collider != null) collider.enabled = false;

        var movement = GetComponent<EnemyMovement>();
        if (movement != null) movement.enabled = false;

        Destroy(gameObject, destroyDelay);
    }
}
