using UnityEngine;
// PlayerBuffs.cs
public class PlayerBuffs : MonoBehaviour
{
    public int AttackBonus { get; private set; }
    public float ProjectileSpeedBonus { get; private set; }
    public float Duration { get; private set; }

    /*public PlayerBuffs(PlayerBuffsSO so)
    {
        AttackBonus = so.PlayerBuffsPower;
        ProjectileSpeedBonus = so.PlayerBuffsProjectileSpeed;
        Duration = so.Duration;
    }*/
    private PlayerContext _context;

    public void Initialize(PlayerContext context)
    {
        _context = context;
        // Можно подписаться на события, обновить UI и т.д.
    }
    
}