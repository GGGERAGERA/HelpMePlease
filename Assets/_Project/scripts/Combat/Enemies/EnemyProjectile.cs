using UnityEngine;

public class EnemyProjectile : MonoBehaviour, IAnomalySpeedProjectile
{
    [SerializeField] private float speed = 7f;
    [SerializeField] private int damage = 10;
    [SerializeField] private float lifetime = 5f;

    private Vector2 direction;
    private readonly AnomalySpeedMultiplierStack anomalySpeed = new();

    public void Initialize(Vector2 shootDirection)
    {
        direction = shootDirection.normalized;
        Destroy(gameObject, lifetime);
    }

    private void Update()
    {
        Vector2 windVelocity = WorldRuleController.Instance != null
            ? WorldRuleController.Instance.ProjectileWindVelocity
            : Vector2.zero;
        transform.position += (Vector3)(
            (direction * speed * anomalySpeed.Value + windVelocity) *
            Time.deltaTime
        );
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Player"))
            return;

        PlayerHealth health = other.GetComponent<PlayerHealth>();

        if (health != null)
        {
            Vector2 hitDirection = other.transform.position - transform.position;
            health.TakeDamage(damage, hitDirection);
        }

        Destroy(gameObject);
    }

    public Component ProjectileComponent => this;
    public float AnomalySpeedMultiplier => anomalySpeed.Value;

    public void SetAnomalySpeedMultiplier(Object source, float multiplier)
    {
        anomalySpeed.Set(source, multiplier);
    }

    public void RemoveAnomalySpeedMultiplier(Object source)
    {
        anomalySpeed.Remove(source);
    }

    public void ClearAnomalySpeedMultipliers()
    {
        anomalySpeed.Clear();
    }

    private void OnDisable()
    {
        AnomalyProjectileLifecycle.NotifyDisabled(this);
        anomalySpeed.Clear();
    }
}
