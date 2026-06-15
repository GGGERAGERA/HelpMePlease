using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class EnemyBomberMovement : EnemyMovement
{
    [Header("Target")]
    [SerializeField] private string playerTag = "Player";

    [Header("Movement")]
    [SerializeField] private float moveSpeed = 4.5f;

    [Header("Explosion")]
    [SerializeField] private float explosionRadius = 2f;
    [SerializeField] private int explosionDamage = 25;
    [SerializeField] private ParticleSystem explosionFxPrefab;

    private Rigidbody2D rb;
    private Transform player;
    private bool exploded;
    private float speedMultiplier = 1f;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    private void Start()
    {
        FindPlayer();
    }

    private void FixedUpdate()
    {
        if (Time.timeScale == 0f || exploded)
            return;

        if (player == null)
            FindPlayer();

        if (player == null)
            return;

        Vector2 direction = ((Vector2)player.position - rb.position).normalized;
        rb.MovePosition(rb.position + direction * moveSpeed * speedMultiplier * Time.fixedDeltaTime);

        float distance = Vector2.Distance(rb.position, player.position);

        if (distance <= explosionRadius)
            Explode();
    }

    private void FindPlayer()
    {
        GameObject playerObject = GameObject.FindGameObjectWithTag(playerTag);

        if (playerObject != null)
            player = playerObject.transform;
    }

    private void Explode()
    {
        if (exploded)
            return;

        exploded = true;

        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, explosionRadius);

        foreach (Collider2D hit in hits)
        {
            if (!hit.CompareTag("Player"))
                continue;

            PlayerHealth health = hit.GetComponent<PlayerHealth>();

            if (health != null)
            {
                Vector2 hitDirection = hit.transform.position - transform.position;
                health.TakeDamage(explosionDamage, hitDirection);
            }
        }

        if (explosionFxPrefab != null)
        {
            ParticleSystem fx = Instantiate(explosionFxPrefab, transform.position, Quaternion.identity);
            fx.Play();
            Destroy(fx.gameObject, fx.main.duration);
        }

        Destroy(gameObject);
    }

    public override void SetSpeedMultiplier(float multiplier)
    {
        speedMultiplier = Mathf.Max(0.1f, multiplier);
    }

    public override void ApplyKnockback(Vector2 direction, float force)
    {
        // Подрывник пока не отлетает, чтобы надёжно исполнял роль.
    }

    public override void StopAfterHit()
    {
        // Подрывник не останавливается после контакта.
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, explosionRadius);
    }
}