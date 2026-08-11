using System;

public static class WeaponHitResolver
{
    // Weapon-only extension point. Low-level damage does not raise this event.
    public static event Action<WeaponHitContext> HitResolved;

    public static bool Resolve(in WeaponHitContext context)
    {
        if (context.Target == null || context.Target.IsDead)
            return false;

        context.Target.TakeDamage(
            context.Damage,
            context.HitPoint,
            context.IsCritical
        );

        HitResolved?.Invoke(context);
        return true;
    }
}
