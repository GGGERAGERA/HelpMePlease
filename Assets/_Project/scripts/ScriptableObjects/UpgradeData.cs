using UnityEngine;
public enum UpgradeType
{
    MaxHealth,
    Heal,
    MoveSpeed,
    BaseDamage,
    DamageMultiplier,
    WeaponDamage,
    FireRatePercent,
    WeaponRange,
    OrbitRadius,
    ProjectileCount
}

[CreateAssetMenu(fileName = "New UpgradeData", menuName = "Game/Upgrade Data")]

public class UpgradeData : ScriptableObject
{
    [Header("UI")]
    public string upgradeName;
    [TextArea] public string description;
    public Sprite icon;
    [Header("Effect")]
    public UpgradeType upgradeType;

    [Tooltip("Число улучшения. Например: 20 здоровья, 0.5f к урону, 0.2f к скорости и т.д.")]
    public float value = 1f;

}
