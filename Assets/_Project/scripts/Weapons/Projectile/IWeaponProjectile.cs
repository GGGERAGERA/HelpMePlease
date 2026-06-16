using UnityEngine;

public interface IWeaponProjectile
{
    void Initialize(
        float damage,
        float speed,
        float range,
        Vector2 direction,
        int pierce = 0,
        bool isCritical = false,
        int ricochet = 0
    );
}