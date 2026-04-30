using UnityEngine;

public class EnemyCollisionHandler : MonoBehaviour
{
    [Header("Combat Settings")]
    public int damage = 15;               // урон этого врага (настраивается в инспекторе)
    public float damageCooldown = 1f;     // задержка между ударами (настраивается в инспекторе)

    private float lastDamageTime;

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.CompareTag("Player"))
        {
            TryDamagePlayer(collision.GetComponent<PlayerHealth>());
        }
    }

    private void OnTriggerStay2D(Collider2D collision)
    {
        if (collision.CompareTag("Player"))
        {
            TryDamagePlayer(collision.GetComponent<PlayerHealth>());
        }
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision.collider.CompareTag("Player"))
        {
            TryDamagePlayer(collision.gameObject.GetComponent<PlayerHealth>());
        }
    }

    private void OnCollisionStay2D(Collision2D collision)
    {
        if (collision.collider.CompareTag("Player"))
        {
            TryDamagePlayer(collision.gameObject.GetComponent<PlayerHealth>());
        }
    }

    private void TryDamagePlayer(PlayerHealth playerHealth)
    {
        if (playerHealth == null) return;

        if (Time.time >= lastDamageTime + damageCooldown)
        {
            playerHealth.TakeDamage(damage);
            lastDamageTime = Time.time;
        }
    }
}