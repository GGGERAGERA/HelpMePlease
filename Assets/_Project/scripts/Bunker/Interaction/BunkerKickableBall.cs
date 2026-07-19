using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public sealed class BunkerKickableBall : MonoBehaviour
{
    [SerializeField] private float kickForce = 6f;

    private Rigidbody2D rb;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (!collision.collider.CompareTag("Player"))
            return;

        Vector2 direction = ((Vector2)transform.position - (Vector2)collision.transform.position).normalized;

        if (direction.sqrMagnitude <= 0.001f)
            direction = Random.insideUnitCircle.normalized;

        rb.AddForce(direction * kickForce, ForceMode2D.Impulse);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log("[BunkerKickableBall] Kicked.");
#endif
    }
}
