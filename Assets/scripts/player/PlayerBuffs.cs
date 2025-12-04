
// PlayerBuffs.cs
public class PlayerBuffs
{
    public int AttackBonus { get; private set; }
    public float ProjectileSpeedBonus { get; private set; }
    public float Duration { get; private set; }

    public PlayerBuffs(PlayerBuffsSO so)
    {
        AttackBonus = so.PlayerBuffsPower;
        ProjectileSpeedBonus = so.PlayerBuffsProjectileSpeed;
        Duration = so.Duration;
    }
}