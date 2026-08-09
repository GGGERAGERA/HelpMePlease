using UnityEngine;

public enum WeaponCoreType
{
    None = 0,
    Rupture = 1,
    Chain = 2,
    Void = 3
}

public readonly struct WeaponHitContext
{
    public readonly BaseWeapon Weapon;
    public readonly Transform Owner;
    public readonly Object Source;
    public readonly WeaponShotKind ShotKind;
    public readonly WeaponCoreType Core;
    public readonly EnemyHealth Target;
    public readonly Vector2 HitPoint;
    public readonly Vector2 Direction;
    public readonly float Damage;
    public readonly bool IsCritical;
    public readonly int PropagationDepth;

    public WeaponHitContext(
        BaseWeapon weapon,
        Transform owner,
        Object source,
        WeaponShotKind shotKind,
        WeaponCoreType core,
        EnemyHealth target,
        Vector2 hitPoint,
        Vector2 direction,
        float damage,
        bool isCritical,
        int propagationDepth = 0)
    {
        Weapon = weapon;
        Owner = owner;
        Source = source;
        ShotKind = shotKind;
        Core = core;
        Target = target;
        HitPoint = hitPoint;
        Direction = direction.sqrMagnitude > 0.001f
            ? direction.normalized
            : Vector2.right;
        Damage = damage;
        IsCritical = isCritical;
        PropagationDepth = Mathf.Max(0, propagationDepth);
    }

    public WeaponHitContext CreateSecondary(
        EnemyHealth target,
        Vector2 hitPoint,
        Vector2 direction,
        float damage)
    {
        return new WeaponHitContext(
            Weapon,
            Owner,
            Source,
            ShotKind,
            Core,
            target,
            hitPoint,
            direction,
            damage,
            false,
            PropagationDepth + 1
        );
    }
}
