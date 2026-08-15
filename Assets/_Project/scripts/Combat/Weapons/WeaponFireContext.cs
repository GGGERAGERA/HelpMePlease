using UnityEngine;

public readonly struct WeaponFireContext
{
    public readonly BaseWeapon Weapon;
    public readonly WeaponData WeaponData;
    public readonly Transform Owner;
    public readonly Transform FirePoint;

    public readonly Vector2 Origin;
    public readonly Vector2 Direction;

    public readonly int Damage;
    public readonly bool IsCritical;

    public readonly float Range;
    public readonly float ProjectileSpeed;

    public readonly int ProjectileCount;
    public readonly int Pierce;
    public readonly int Ricochet;
    public readonly float ShotVisualScale;

    public readonly float KnockbackForce;

    public readonly PlayerCombatModifiers Modifiers;
    public readonly WeaponFxPlayer FxPlayer;

    public WeaponFireContext(
        BaseWeapon weapon,
        WeaponData weaponData,
        Transform owner,
        Transform firePoint,
        Vector2 origin,
        Vector2 direction,
        int damage,
        bool isCritical,
        float range,
        float projectileSpeed,
        int projectileCount,
        int pierce,
        int ricochet,
        float shotVisualScale,
        float knockbackForce,
        PlayerCombatModifiers modifiers,
        WeaponFxPlayer fxPlayer)
    {
        Weapon = weapon;
        WeaponData = weaponData;
        Owner = owner;
        FirePoint = firePoint;
        Origin = origin;
        Direction = direction;
        Damage = damage;
        IsCritical = isCritical;
        Range = range;
        ProjectileSpeed = projectileSpeed;
        ProjectileCount = projectileCount;
        Pierce = pierce;
        Ricochet = ricochet;
        ShotVisualScale = Mathf.Max(0.1f, shotVisualScale);
        KnockbackForce = knockbackForce;
        Modifiers = modifiers;
        FxPlayer = fxPlayer;
    }

    public WeaponFireContext WithKnockback(float knockbackForce)
    {
        return new WeaponFireContext(
            Weapon,
            WeaponData,
            Owner,
            FirePoint,
            Origin,
            Direction,
            Damage,
            IsCritical,
            Range,
            ProjectileSpeed,
            ProjectileCount,
            Pierce,
            Ricochet,
            ShotVisualScale,
            knockbackForce,
            Modifiers,
            FxPlayer
        );
    }

    public WeaponFireContext WithOriginAndDirection(
        Vector2 origin,
        Vector2 direction)
    {
        return new WeaponFireContext(
            Weapon,
            WeaponData,
            Owner,
            FirePoint,
            origin,
            direction,
            Damage,
            IsCritical,
            Range,
            ProjectileSpeed,
            ProjectileCount,
            Pierce,
            Ricochet,
            ShotVisualScale,
            KnockbackForce,
            Modifiers,
            FxPlayer
        );
    }

    public WeaponHitContext CreateHitContext(
        EnemyHealth target,
        Vector2 hitPoint)
    {
        return new WeaponHitContext(
            Weapon,
            WeaponData,
            target,
            hitPoint,
            Direction,
            Damage,
            IsCritical
        );
    }
}
