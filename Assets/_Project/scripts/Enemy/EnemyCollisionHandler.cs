using UnityEditor.Experimental.GraphView;
using UnityEngine;

public class EnemyCollisionHandler : MonoBehaviour
{
    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.CompareTag("Player"))
        {
         collision.GetComponent<PlayerHealth>()?.TakeDamage(10); // наносим урон игроку, если он столкнулся с врагом
        }
    }

    void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision.collider.CompareTag("Player"))
        {
            PlayerHealth playerHealth = collision.gameObject.GetComponent<PlayerHealth>();
            if (playerHealth != null) {
                    playerHealth.TakeDamage(20f); // наносим урон игроку, если он столкнулся с врагом
            }
        }
    }

}
