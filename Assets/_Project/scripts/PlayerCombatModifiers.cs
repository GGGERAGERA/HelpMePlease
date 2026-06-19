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


    public bool ShouldFireExtraShot()
    {
        if (!everyFifthAttackExtraShot)
            return false;

        attackCounter++;

        return attackCounter % 5 == 0;
    }

}