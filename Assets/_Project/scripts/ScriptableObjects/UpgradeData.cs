using UnityEngine;
public enum UpgradeType
{
    MaxHealth,
    Heal,
    MoveSpeed,
    WeaponDamage,
    FireRatePercent,
    WeaponRange,
    OrbitRadius,
    ProjectileCount,
    ProjectilePierce,
    ProjectileRicochet,
    CritChance,
    CritDamage,
    Knockback
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


    [Tooltip("����� ���������. ��������: 20 ��������, 0.5f � �����, 0.2f � �������� � �.�.")]
    public float value = 1f;

}
