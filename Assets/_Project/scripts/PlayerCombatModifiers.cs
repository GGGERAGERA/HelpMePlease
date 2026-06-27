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
    public bool doubleDamageWithInaccuracy;

    [Header("Low HP Power")]
    public bool lowHpPower;
    public float lowHpPowerMultiplier;
    public float lowHpDamageBonus;
    public float lowHpFireRateBonus;

    [Header("Stationary Fire Rate Ramp")]
    public bool stationaryFireRateRamp;
    public float stationaryFireRateRampMaxBonus;
    public float stationaryFireRateBonus;
    [SerializeField] private float stationaryRampDuration = 2f;

    [Header("Legendary Upgrades")]
    public float randomExtraShotsChance;
    [Header("Circular Burst")]
    public bool circularBurst;
    public float circularBurstCooldown = 8f;
    [SerializeField] private float circularBurstMinCooldown = 3f;
    [Header("Nuke")]
    public bool nukeEveryKills;
    public int nukeKillsRequired = 10;
    [SerializeField] private int nukeMinKillsRequired = 3;

    [Header("Masks")]
    public LayerMask enemyMask;

    [Header("Runtime Bonuses")]
    public float bonusDamageMultiplier = 1f;
    public float bonusFireRateMultiplier = 1f;
    public float accuracyPenaltyDegrees = 0f;



    [Header("Movement Check")]
    public float stationaryMoveThreshold = 0.05f;





    private PlayerHealth health;

    [SerializeField] private GameObject deathExplosionPrefab;
    [SerializeField] private float deathExplosionRadius = 2f;
    [Header("Hit Explosion")]
    [SerializeField] private GameObject hitExplosionPrefab;
    [SerializeField] private float hitExplosionRadius = 1.5f;
    [SerializeField] private float hitExplosionDamage = 10f;

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
        stationaryFireRateBonus = 0f;
    }

    public void UpdateStationaryFireRateRamp(
        bool isShooting,
        bool isMoving,
        float deltaTime
    )
    {
        if (!stationaryFireRateRamp || stationaryFireRateRampMaxBonus <= 0f)
        {
            stationaryFireRateBonus = 0f;
            return;
        }

        if (!isShooting || isMoving)
        {
            stationaryFireRateBonus = 0f;
            return;
        }

        float rampSpeed = stationaryFireRateRampMaxBonus / Mathf.Max(0.1f, stationaryRampDuration);

        stationaryFireRateBonus = Mathf.MoveTowards(
            stationaryFireRateBonus,
            stationaryFireRateRampMaxBonus,
            rampSpeed * deltaTime
        );
    }

    public void UpdateLowHpPower(float healthPercent)
    {
        if (!lowHpPower || lowHpPowerMultiplier <= 0f)
        {
            lowHpDamageBonus = 0f;
            lowHpFireRateBonus = 0f;
            return;
        }

        float missingHealthPercent = 1f - Mathf.Clamp01(healthPercent);

        lowHpDamageBonus = missingHealthPercent * lowHpPowerMultiplier;
        lowHpFireRateBonus = missingHealthPercent * lowHpPowerMultiplier;
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
    public void TrySpawnHitExplosion(Vector3 position)
    {
        if (hitExplosionChance <= 0f)
            return;

        if (Random.value > hitExplosionChance)
            return;

        SpawnHitExplosion(position);
    }

    private void SpawnHitExplosion(Vector3 position)
    {
        if (hitExplosionPrefab != null)
        {
            GameObject fx = Instantiate(hitExplosionPrefab, position, Quaternion.identity);
            Destroy(fx, 2f);
        }

        Collider2D[] enemies = Physics2D.OverlapCircleAll(
            position,
            hitExplosionRadius,
            enemyMask
        );

        foreach (Collider2D collider in enemies)
        {
            EnemyHealth enemyHealth = collider.GetComponent<EnemyHealth>();

            if (enemyHealth == null)
                continue;

            enemyHealth.TakeDamage(hitExplosionDamage, position);
        }
    }
    public void AddCircularBurstCooldownReduction(float reduction)
    {
        circularBurst = true;

        circularBurstCooldown = Mathf.Max(
            circularBurstMinCooldown,
            circularBurstCooldown - reduction
        );
    }
    public void AddNukeKillRequirementReduction(float reduction)
    {
        nukeEveryKills = true;

        int reductionInt = Mathf.RoundToInt(reduction);

        nukeKillsRequired = Mathf.Max(
            nukeMinKillsRequired,
            nukeKillsRequired - reductionInt
        );
    }
}