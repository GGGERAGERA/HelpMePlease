using UnityEngine;

public class PlayerCombatModifiers : MonoBehaviour
{
    private int attackCounter;

    [Header("Blue Upgrades")]
    public float hitExplosionChance;

    [Range(0f, 1f)]
    public float enemyDeathExplosionChance;
    public float deathExplosionDamageBonus;
    public float knockbackMultiplier = 1f;

    [Header("Purple Upgrades")]
    public bool stationaryFireRateRamp;
    public bool doubleDamageWithInaccuracy;
    public bool lowHpPower;

    [Header("Legendary Upgrades")]
    public float randomExtraShotsChance;
    public bool circularBurst;
    public bool nukeEveryTenKills;

    [Header("Masks")]
    public LayerMask enemyMask;

    [Header("Runtime Bonuses")]
    public float bonusDamageMultiplier = 1f;
    public float bonusFireRateMultiplier = 1f;
    public float accuracyPenaltyDegrees = 0f;

    [Header("Stationary Fire Rate Ramp")]
    public float stationaryFireTime;
    public float stationaryFireRateBonus;

    [Header("Movement Check")]
    public float stationaryMoveThreshold = 0.05f;

    [Header("Low HP Power")]
    public float lowHpDamageBonus;
    public float lowHpFireRateBonus;

    private PlayerHealth health;

    [SerializeField] private GameObject deathExplosionPrefab;
    [SerializeField] private float deathExplosionRadius = 2f;

    private void Awake()
    {
        health = GetComponent<PlayerHealth>();
    }
    private void Update()
    {
        if (health == null)
            return;

        UpdateLowHpPower(health.CurrentHealth / health.MaxHealth);
    }

    public bool ShouldFireExtraShot()
    {

        attackCounter++;

        return attackCounter % 5 == 0;
    }
    public void ResetStationaryFireRamp()
    {
        stationaryFireTime = 0f;
        stationaryFireRateBonus = 0f;
    }

    public void UpdateStationaryFireRateRamp(
    bool isShooting,
    bool isMoving,
    float deltaTime
)
    {
        if (!stationaryFireRateRamp)
            return;

        if (!isShooting || isMoving)
        {
            ResetStationaryFireRamp();
            return;
        }

        stationaryFireTime += deltaTime;

        stationaryFireRateBonus = Mathf.Min(
            stationaryFireTime * 0.15f,
            1.5f
        );
    }

    public void UpdateLowHpPower(float healthPercent)
    {
        if (!lowHpPower)
        {
            lowHpDamageBonus = 0f;
            lowHpFireRateBonus = 0f;
            return;
        }

        float missingHealthPercent = 1f - Mathf.Clamp01(healthPercent);

        lowHpDamageBonus = missingHealthPercent;
        lowHpFireRateBonus = missingHealthPercent;
    }
    public void TrySpawnDeathExplosion(Vector3 position)
    {
        if (enemyDeathExplosionChance <= 0f)
            return;

        if (Random.value > enemyDeathExplosionChance)
            return;

        SpawnDeathExplosion(position);
    }

    private void SpawnDeathExplosion(Vector3 position)
    {
        if (deathExplosionPrefab != null)
        {
            GameObject fx = Instantiate(deathExplosionPrefab, position, Quaternion.identity);
            Destroy(fx, 2f);
        }

        Collider2D[] enemies = Physics2D.OverlapCircleAll(
            position,
            deathExplosionRadius,
            enemyMask
        );

        foreach (Collider2D collider in enemies)
        {
            EnemyHealth health = collider.GetComponent<EnemyHealth>();

            if (health == null)
                continue;

            health.TakeDamage(deathExplosionDamageBonus, position);
        }
    }

}