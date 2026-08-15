using System;
using System.Collections.Generic;
using UnityEngine;

public interface IEvolutionRuntime
{
    EvolutionRuntimeType Type { get; }
    bool IsActive { get; }
    void Activate(EvolutionDefinition definition, BaseWeapon weapon);
    void Deactivate();
}

public static class EvolutionRuntimeRegistry
{
    private static readonly Dictionary<
        EvolutionRuntimeType,
        Func<GameObject, IEvolutionRuntime>> factories = new();

    public static void Register(
        EvolutionRuntimeType type,
        Func<GameObject, IEvolutionRuntime> factory)
    {
        if (type == EvolutionRuntimeType.None || factory == null)
            return;

        factories[type] = factory;
    }

    public static bool TryCreate(
        GameObject owner,
        EvolutionRuntimeType type,
        out IEvolutionRuntime runtime)
    {
        runtime = null;
        return owner != null && factories.TryGetValue(type, out var factory) &&
            (runtime = factory(owner)) != null;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reset()
    {
        factories.Clear();
    }
}
