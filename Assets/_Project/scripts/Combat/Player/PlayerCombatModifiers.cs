using UnityEngine;

public class PlayerCombatModifiers : MonoBehaviour
{
    [Header("Production Offensive Upgrades")]
    [SerializeField] private float runDamageMultiplier = 1f;
    [SerializeField] private float runCritChanceBonus;
    [SerializeField] private float runAttackSizeMultiplier = 1f;

    public float RunDamageMultiplier => Mathf.Max(0.01f, runDamageMultiplier);
    public float RunCritChanceBonus => Mathf.Clamp01(runCritChanceBonus);
    public float RunAttackSizeMultiplier =>
        Mathf.Max(0.1f, runAttackSizeMultiplier);
    public float TotalDamageMultiplier =>
        RunDamageMultiplier * Mathf.Max(0.01f, bonusDamageMultiplier);

    public void SetRunDamageMultiplier(float value)
    {
        runDamageMultiplier = Mathf.Max(0.01f, value);
    }

    public void SetRunCritChanceBonus(float value)
    {
        runCritChanceBonus = Mathf.Clamp01(value);
    }

    public void SetRunAttackSizeMultiplier(float value)
    {
        runAttackSizeMultiplier = Mathf.Max(0.1f, value);
    }

    [Header("Blue Upgrades")]
    public float hitExplosionChance;
    public float knockbackMultiplier = 1f;

    [Header("Stationary Fire Rate Ramp")]
    public bool stationaryFireRateRamp;
    public float stationaryFireRateRampMaxBonus;
    public float stationaryFireRateBonus;
    [SerializeField] private float stationaryRampDuration = 2f;

    [Header("Masks")]
    public LayerMask enemyMask;

    [Header("Runtime Bonuses")]
    public float bonusDamageMultiplier = 1f;
    public float bonusFireRateMultiplier = 1f;
    public float accuracyPenaltyDegrees = 0f;



    [Header("Movement Check")]
    public float stationaryMoveThreshold = 0.05f;

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

}
