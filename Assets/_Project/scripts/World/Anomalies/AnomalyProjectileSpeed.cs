using System.Collections.Generic;
using UnityEngine;

public interface IAnomalySpeedProjectile
{
    Component ProjectileComponent { get; }
    float AnomalySpeedMultiplier { get; }
    void SetAnomalySpeedMultiplier(Object source, float multiplier);
    void RemoveAnomalySpeedMultiplier(Object source);
    void ClearAnomalySpeedMultipliers();
}

public static class AnomalyProjectileLifecycle
{
    public static event System.Action<Component> Disabled;

    public static void NotifyDisabled(Component projectile)
    {
        Disabled?.Invoke(projectile);
    }
}

public sealed class AnomalySpeedMultiplierStack
{
    private readonly Dictionary<Object, float> multipliers = new();

    public float Value { get; private set; } = 1f;

    public void Set(Object source, float multiplier)
    {
        if (source == null)
            return;

        multipliers[source] = Mathf.Max(0.1f, multiplier);
        Recalculate();
    }

    public void Remove(Object source)
    {
        if (ReferenceEquals(source, null))
            return;

        if (multipliers.Remove(source))
            Recalculate();
    }

    public void Clear()
    {
        multipliers.Clear();
        Value = 1f;
    }

    private void Recalculate()
    {
        float result = 1f;
        List<Object> staleSources = null;

        foreach (KeyValuePair<Object, float> pair in multipliers)
        {
            if (pair.Key == null)
            {
                staleSources ??= new List<Object>();
                staleSources.Add(pair.Key);
                continue;
            }

            result *= pair.Value;
        }

        if (staleSources != null)
        {
            for (int i = 0; i < staleSources.Count; i++)
                multipliers.Remove(staleSources[i]);
        }

        Value = result;
    }
}
