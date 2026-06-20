using UnityEngine;

public class PlayerCombatModifiers : MonoBehaviour
{
    private int attackCounter;

    [Header("Blue Upgrades")]
    public bool everyFifthAttackExtraShot;
    public float hitExplosionChance;
    public bool enemyDeathExplosion;
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
        if (!everyFifthAttackExtraShot)
            return false;

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

}