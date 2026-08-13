using UnityEngine;

public static class EnemyDebugAiFreeze
{
    public static bool IsFrozen { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetSessionState()
    {
        IsFrozen = false;
    }

    public static void SetFrozen(bool frozen)
    {
        IsFrozen = frozen;
    }
}

public abstract class EnemyMovement : MonoBehaviour, IAnomalyExternalVelocity
{
    private readonly AnomalyExternalVelocityStack
        anomalyExternalVelocity = new();

    protected Vector2 AnomalyExternalVelocity =>
        anomalyExternalVelocity.Value;
    public Component ExternalVelocityComponent => this;

    public abstract void SetSpeedMultiplier(float multiplier);
    public abstract void SetAnomalySpeedMultiplier(float multiplier);
    public abstract void SetWorldRuleSpeedMultiplier(float multiplier);
    public abstract void SetWorldRuleExternalVelocity(Vector2 velocity);
    public abstract void ApplyKnockback(Vector2 direction, float force);
    public abstract void StopAfterHit();

    public void SetAnomalyExternalVelocity(
        Object source,
        Vector2 velocity)
    {
        anomalyExternalVelocity.Set(source, velocity);
    }

    public void RemoveAnomalyExternalVelocity(Object source)
    {
        anomalyExternalVelocity.Remove(source);
    }

    protected void ClearAnomalyExternalVelocities()
    {
        anomalyExternalVelocity.Clear();
    }
}
