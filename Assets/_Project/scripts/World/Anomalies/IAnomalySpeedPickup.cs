using UnityEngine;

public interface IAnomalySpeedPickup
{
    Component PickupComponent { get; }
    float AnomalySpeedMultiplier { get; }
    void SetAnomalySpeedMultiplier(Object source, float multiplier);
    void RemoveAnomalySpeedMultiplier(Object source);
}

public static class AnomalySpeedPickupLifecycle
{
    public static event System.Action<Component> Disabled;

    public static void NotifyDisabled(Component pickup)
    {
        Disabled?.Invoke(pickup);
    }
}
