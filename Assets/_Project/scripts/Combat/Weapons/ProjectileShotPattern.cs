using UnityEngine;

public sealed class ProjectileShotPattern : MonoBehaviour
{
    [SerializeField] private float fallbackSpreadAngle = 0f;

    public bool FirePattern(
        WeaponFireContext baseContext,
        IWeaponFireBehaviour fireBehaviour,
        float spreadAngle
    )
    {
        if (fireBehaviour == null)
            return false;

        int count = Mathf.Max(1, baseContext.ProjectileCount);
        float finalSpread = spreadAngle > 0f ? spreadAngle : fallbackSpreadAngle;
        bool fired = false;

        for (int i = 0; i < count; i++)
        {
            float angleOffset = GetSpreadOffset(i, count, finalSpread);
            Vector2 direction = RotateVector(baseContext.Direction, angleOffset);

            WeaponFireContext shotContext = new WeaponFireContext(
                baseContext.Weapon,
                baseContext.Owner,
                baseContext.FirePoint,
                baseContext.Origin,
                direction,
                baseContext.Damage,
                baseContext.IsCritical,
                baseContext.Range,
                baseContext.ProjectileSpeed,
                baseContext.ProjectileCount,
                baseContext.Pierce,
                baseContext.Ricochet,
                baseContext.KnockbackForce,
                baseContext.Modifiers,
                baseContext.FxPlayer,
                baseContext.ShotKind,
                baseContext.Core
            );

            fired |= fireBehaviour.Fire(shotContext);
        }

        return fired;
    }

    private float GetSpreadOffset(int index, int count, float totalSpread)
    {
        if (count <= 1)
            return 0f;

        float step = totalSpread / (count - 1);
        return -totalSpread * 0.5f + step * index;
    }

    private Vector2 RotateVector(Vector2 vector, float angle)
    {
        float radians = angle * Mathf.Deg2Rad;

        float cos = Mathf.Cos(radians);
        float sin = Mathf.Sin(radians);

        return new Vector2(
            vector.x * cos - vector.y * sin,
            vector.x * sin + vector.y * cos
        ).normalized;
    }
}
