using System;
using System.Collections.Generic;
using UnityEngine;

public interface IAnomalyPowerRuntime
{
    AnomalyPowerType Type { get; }
    int Level { get; }
    void SetLevel(int level);
    void Activate();
    void Deactivate();
}

public static class AnomalyPowerRuntimeRegistry
{
    private static readonly Dictionary<
        AnomalyPowerType,
        Func<GameObject, IAnomalyPowerRuntime>> factories = new();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reset()
    {
        factories.Clear();
    }

    public static void Register(
        AnomalyPowerType type,
        Func<GameObject, IAnomalyPowerRuntime> factory)
    {
        if (factory == null)
            throw new ArgumentNullException(nameof(factory));

        if (factories.ContainsKey(type))
        {
            Debug.LogError(
                $"[AnomalyPowerRuntimeRegistry] Duplicate registration for " +
                $"'{type}'."
            );
            return;
        }

        factories.Add(type, factory);
    }

    public static bool TryCreate(
        GameObject owner,
        AnomalyPowerType type,
        out IAnomalyPowerRuntime runtime)
    {
        runtime = null;

        if (owner == null || !factories.TryGetValue(type, out var factory))
            return false;

        runtime = factory(owner);
        return runtime != null;
    }
}
