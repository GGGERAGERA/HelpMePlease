using UnityEngine;

public class EnemyCollisionHandler : MonoBehaviour
{
    [Header("Combat Settings")]
    public int damage = 15;
    public float damageCooldown = 1f;

    private float lastDamageTime;

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.CompareTag("Player"))
            TryDamagePlayer(collision.GetComponent<PlayerHealth>());
    }

    private void OnTriggerStay2D(Collider2D collision)
    {
        if (collision.CompareTag("Player"))
            TryDamagePlayer(collision.GetComponent<PlayerHealth>());
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision.collider.CompareTag("Player"))
            TryDamagePlayer(collision.gameObject.GetComponent<PlayerHealth>());
    }

    private void OnCollisionStay2D(Collision2D collision)
    {
        if (collision.collider.CompareTag("Player"))
            TryDamagePlayer(collision.gameObject.GetComponent<PlayerHealth>());
    }

    private void TryDamagePlayer(PlayerHealth playerHealth)
    {
        if (playerHealth == null)
            return;

        if (Time.time < lastDamageTime + damageCooldown)
            return;

        Vector2 hitDirection = playerHealth.transform.position - transform.position;

        bool damageApplied = playerHealth.TakeDamage(damage, hitDirection);

        if (damageApplied)
        {
            EnemyMovement movement = GetComponent<EnemyMovement>();

            if (movement != null)
                movement.StopAfterHit();
        }

        lastDamageTime = Time.time;
    }
}