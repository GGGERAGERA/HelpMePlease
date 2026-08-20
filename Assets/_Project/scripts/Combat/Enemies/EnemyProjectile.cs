using UnityEngine;

public class EnemyProjectile : MonoBehaviour, IAnomalySpeedProjectile,
    IAnomalyExternalVelocity
{
    [SerializeField] private float speed = 7f;
    [SerializeField] private int damage = 10;
    [SerializeField] private float lifetime = 5f;

    private Vector2 direction;
    private float remainingLifetime;
    private PooledGameObject pooledObject;
    private readonly AnomalySpeedMultiplierStack anomalySpeed = new();
    private readonly AnomalyExternalVelocityStack
        anomalyExternalVelocity = new();

    public void Initialize(Vector2 shootDirection)
    {
        direction = shootDirection.normalized;
        remainingLifetime = lifetime;
        anomalySpeed.Clear();
        anomalyExternalVelocity.Clear();
        pooledObject ??= GetComponent<PooledGameObject>();
    }

    private void Update()
    {
        remainingLifetime -= Time.deltaTime;
        if (remainingLifetime <= 0f)
        {
            Despawn();
            return;
        }

        Vector2 windVelocity = WorldRuleController.Instance != null
            ? WorldRuleController.Instance.ProjectileWindVelocity
            : Vector2.zero;
        transform.position += (Vector3)(
            (direction * speed * anomalySpeed.Value + windVelocity +
             anomalyExternalVelocity.Value) *
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

        Despawn();
    }

    public Component ProjectileComponent => this;
    public Component ExternalVelocityComponent => this;
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

    public void SetAnomalyExternalVelocity(
        Object source,
        Vector2 velocity)
    {
        anomalyExternalVelocity.Set(source, velocity);
    }

    public void RemoveAnomalyExternalVelocity(Object source)
    {
        anomalyExternalVelocity.Remove(source);
    }

    private void OnDisable()
    {
        AnomalyProjectileLifecycle.NotifyDisabled(this);
        anomalySpeed.Clear();
        anomalyExternalVelocity.Clear();
    }

    private void Despawn()
    {
        if (pooledObject != null && pooledObject.Release())
            return;

        Destroy(gameObject);
    }
}
