using UnityEngine;

public class PlayerCombatModifiers : MonoBehaviour
{

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
