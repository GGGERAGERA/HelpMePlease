using UnityEngine;

public readonly struct WeaponHitContext
{
    public readonly BaseWeapon SourceWeapon;
    public readonly WeaponData WeaponData;
    public readonly EnemyHealth Target;
    public readonly Vector2 HitPoint;
    public readonly Vector2 Direction;
    public readonly float Damage;
    public readonly bool IsCritical;

    public WeaponHitContext(
        BaseWeapon sourceWeapon,
        WeaponData weaponData,
        EnemyHealth target,
        Vector2 hitPoint,
        Vector2 direction,
        float damage,
        bool isCritical)
    {
        SourceWeapon = sourceWeapon;
        WeaponData = weaponData;
        Target = target;
        HitPoint = hitPoint;
        Direction = direction.sqrMagnitude > 0.001f
            ? direction.normalized
            : Vector2.right;
        Damage = damage;
        IsCritical = isCritical;
    }
}
