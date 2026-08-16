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


    public WeaponFireContext WithRangeMultiplier(float multiplier)
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
            Range * Mathf.Max(0.1f, multiplier),
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

public static class WeaponShotSpread
{
    private const float SpreadDegreesPerShot = 15f;
    private const float MaximumTotalSpread = 72f;

    public static float GetAngleOffset(
        int index,
        int shotCount,
        float minimumTotalSpread = 0f)
    {
        int count = Mathf.Max(1, shotCount);
        if (count <= 1)
            return 0f;

        float automaticSpread = Mathf.Min(
            MaximumTotalSpread,
            SpreadDegreesPerShot * count);
        float totalSpread = Mathf.Max(
            automaticSpread,
            Mathf.Max(0f, minimumTotalSpread));
        float step = totalSpread / (count - 1);
        return -totalSpread * 0.5f + step * Mathf.Clamp(index, 0, count - 1);
    }

    public static Vector2 RotateDirection(Vector2 direction, float angle)
    {
        float radians = angle * Mathf.Deg2Rad;
        float cos = Mathf.Cos(radians);
        float sin = Mathf.Sin(radians);
        return new Vector2(
            direction.x * cos - direction.y * sin,
            direction.x * sin + direction.y * cos).normalized;
    }
}
