using UnityEngine;

public abstract class EnemyMovement : MonoBehaviour
{
    public abstract void SetSpeedMultiplier(float multiplier);
    public abstract void SetAnomalySpeedMultiplier(float multiplier);
    public abstract void ApplyKnockback(Vector2 direction, float force);
    public abstract void StopAfterHit();
}
