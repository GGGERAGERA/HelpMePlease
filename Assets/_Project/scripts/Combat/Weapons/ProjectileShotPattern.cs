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
            float angleOffset = WeaponShotSpread.GetAngleOffset(
                i,
                count,
                finalSpread);
            Vector2 direction = WeaponShotSpread.RotateDirection(
                baseContext.Direction,
                angleOffset);
            WeaponFireContext shotContext =
                baseContext.WithOriginAndDirection(
                    baseContext.Origin,
                    direction);

            fired |= fireBehaviour.Fire(shotContext);
        }

        return fired;
    }
}
