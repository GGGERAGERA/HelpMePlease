using UnityEngine;

public readonly struct WeaponFireContext
{
    public readonly BaseWeapon Weapon;
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

    public readonly float KnockbackForce;

    public readonly PlayerCombatModifiers Modifiers;
    public readonly WeaponFxPlayer FxPlayer;
    public readonly WeaponShotKind ShotKind;
    public readonly WeaponCoreType Core;

    public WeaponFireContext(
        BaseWeapon weapon,
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
        float knockbackForce,
        PlayerCombatModifiers modifiers,
        WeaponFxPlayer fxPlayer,
        WeaponShotKind shotKind,
        WeaponCoreType core
    )
    {
        Weapon = weapon;
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
        KnockbackForce = knockbackForce;
        Modifiers = modifiers;
        FxPlayer = fxPlayer;
        ShotKind = shotKind;
        Core = core;
    }

    public WeaponFireContext WithKnockback(float knockbackForce)
    {
        return new WeaponFireContext(
            Weapon,
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
            knockbackForce,
            Modifiers,
            FxPlayer,
            ShotKind,
            Core
        );
    }
}
