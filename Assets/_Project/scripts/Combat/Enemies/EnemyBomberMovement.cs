using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class EnemyBomberMovement : EnemyMovement
{
    [Header("Target")]
    [SerializeField] private string playerTag = "Player";

    [Header("Movement")]
    [SerializeField] private float moveSpeed = 4.5f;

    [Header("Knockback")]
    [SerializeField] private float knockbackDecay = 18f;

    [Header("Explosion")]
    [SerializeField] private float triggerRadius = 1.4f;
    [SerializeField] private float explosionRadius = 2f;
    [SerializeField] private float explosionDelay = 1f;
    [SerializeField] private int explosionDamage = 25;

    [Header("FX")]
    [SerializeField] private GameObject explosionRadiusPrefab;
    [SerializeField] private ParticleSystem explosionFxPrefab;
    [SerializeField] private GameObject shockwaveFxPrefab;

    private Rigidbody2D rb;
    private Transform player;
    private bool isExploding;
    private bool exploded;
    private float speedMultiplier = 1f;
    private Vector2 knockbackVelocity;

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
        if (Time.timeScale == 0f || exploded || isExploding)
            return;

        if (player == null)
            FindPlayer();

        if (player == null)
            return;

        Vector2 direction = ((Vector2)player.position - rb.position).normalized;

        knockbackVelocity = Vector2.MoveTowards(
            knockbackVelocity,
            Vector2.zero,
            knockbackDecay * Time.fixedDeltaTime
        );

        Vector2 movement = direction * moveSpeed * speedMultiplier + knockbackVelocity;

        rb.MovePosition(rb.position + movement * Time.fixedDeltaTime);

        float distance = Vector2.Distance(rb.position, player.position);

        if (distance <= triggerRadius)
            StartExplosionSequence();
    }

    private void FindPlayer()
    {
        GameObject playerObject = GameObject.FindGameObjectWithTag(playerTag);

        if (playerObject != null)
            player = playerObject.transform;
    }

    private void StartExplosionSequence()
    {
        if (isExploding || exploded)
            return;

        isExploding = true;
        rb.linearVelocity = Vector2.zero;

        StartCoroutine(ExplosionSequence());
    }

    private IEnumerator ExplosionSequence()
    {
        GameObject radiusVisual = null;

        if (explosionRadiusPrefab != null)
        {
            radiusVisual = Instantiate(
                explosionRadiusPrefab,
                transform.position,
                Quaternion.identity
            );

            float diameter = explosionRadius * 2f;
            radiusVisual.transform.localScale = new Vector3(diameter, diameter, 1f);
        }

        yield return new WaitForSeconds(explosionDelay);

        if (radiusVisual != null)
            Destroy(radiusVisual);

        Explode();
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
            ParticleSystem fx = Instantiate(
                explosionFxPrefab,
                transform.position,
                Quaternion.identity
            );

            fx.Play();
            Destroy(fx.gameObject, fx.main.duration);
        }

        if (shockwaveFxPrefab != null)
            Instantiate(shockwaveFxPrefab, transform.position, Quaternion.identity);

        Destroy(gameObject);
    }

    public override void SetSpeedMultiplier(float multiplier)
    {
        speedMultiplier = Mathf.Max(0.1f, multiplier);
    }

    public override void ApplyKnockback(Vector2 direction, float force)
    {
        if (isExploding || exploded)
            return;

        knockbackVelocity = direction.normalized * force;
    }

    public override void StopAfterHit()
    {
        // Подрывник не останавливается от контактного удара.
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, triggerRadius);

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, explosionRadius);
    }
}