using System;
using System.Collections.Generic;
using UnityEngine;

internal readonly struct ProductionAnomalySiteContext
{
    public GameObject SiteObject { get; }
    public GameObject ServicesHost { get; }
    public Vector2 Position { get; }
    public Vector2 Size { get; }
    public LevelAnomalyController AnomalyController { get; }
    public ExplorationSectorConfig Config { get; }

    public ProductionAnomalySiteContext(
        GameObject siteObject,
        GameObject servicesHost,
        Vector2 position,
        Vector2 size,
        LevelAnomalyController anomalyController,
        ExplorationSectorConfig config)
    {
        SiteObject = siteObject;
        ServicesHost = servicesHost;
        Position = position;
        Size = size;
        AnomalyController = anomalyController;
        Config = config;
    }
}

internal interface IProductionAnomalySiteEnvironment
{
    LocalAnomalyZone AnomalyZone { get; }
    void Collapse();
    void SetDebugVisualEmphasis(float multiplier);
}

internal sealed class ProductionAnomalySiteDefinition
{
    private readonly Func<
        ProductionAnomalySiteContext,
        IProductionAnomalySiteEnvironment> createEnvironment;

    public AnomalyPowerType Type { get; }
    public AnomalyPowerType PowerReward { get; }
    public string SiteDisplayName { get; }
    public string PowerDisplayName { get; }

    public ProductionAnomalySiteDefinition(
        AnomalyPowerType type,
        AnomalyPowerType powerReward,
        string siteDisplayName,
        string powerDisplayName,
        Func<ProductionAnomalySiteContext,
            IProductionAnomalySiteEnvironment> environmentFactory)
    {
        Type = type;
        PowerReward = powerReward;
        SiteDisplayName = siteDisplayName;
        PowerDisplayName = powerDisplayName;
        createEnvironment = environmentFactory ??
            throw new ArgumentNullException(nameof(environmentFactory));
    }

    public IProductionAnomalySiteEnvironment CreateEnvironment(
        ProductionAnomalySiteContext context)
    {
        return createEnvironment(context);
    }
}

internal static class ProductionAnomalySiteDefinitionRegistry
{
    private static readonly Dictionary<
        AnomalyPowerType,
        ProductionAnomalySiteDefinition> definitions = new();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reset()
    {
        definitions.Clear();
    }

    public static void Register(ProductionAnomalySiteDefinition definition)
    {
        if (definition == null)
            throw new ArgumentNullException(nameof(definition));

        if (definitions.ContainsKey(definition.Type))
        {
            Debug.LogError(
                $"[AnomalySiteDefinitionRegistry] Duplicate definition for " +
                $"'{definition.Type}'."
            );
            return;
        }

        definitions.Add(definition.Type, definition);
    }

    public static bool TryGet(
        AnomalyPowerType type,
        out ProductionAnomalySiteDefinition definition)
    {
        return definitions.TryGetValue(type, out definition);
    }
}

internal abstract class ProductionSpecialSiteHazard :
    MonoBehaviour,
    IProductionAnomalySiteEnvironment
{
    public LocalAnomalyZone AnomalyZone => null;

    public void Collapse()
    {
        StopHazard();
    }

    public virtual void SetDebugVisualEmphasis(float multiplier)
    {
    }

    public abstract void StopHazard();
}
