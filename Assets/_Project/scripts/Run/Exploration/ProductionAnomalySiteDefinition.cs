using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

[Flags]
public enum AnomalyVisualTuningCapabilities
{
    None = 0,
    PrimaryColor = 1 << 0,
    SecondaryColor = 1 << 1,
    FillColor = 1 << 2,
    FillAlpha = 1 << 3,
    BoundaryWidth = 1 << 4,
    InnerLineWidth = 1 << 5,
    VisualScale = 1 << 6,
    EdgeGlow = 1 << 7,
    PulseSpeed = 1 << 8,
    PulseStrength = 1 << 9,
    PatternSpeed = 1 << 10,
    BoundaryAlpha = 1 << 11,
    PatternStrength = 1 << 12
}

public struct AnomalyVisualTuningValues
{
    public Color PrimaryColor;
    public Color SecondaryColor;
    public Color FillColor;
    public float FillAlpha;
    public float BoundaryWidth;
    public float InnerLineWidth;
    public float VisualScale;
    public float EdgeGlow;
    public float PulseSpeed;
    public float PulseStrength;
    public float PatternSpeed;
    public float BoundaryAlpha;
    public float PatternStrength;
}

internal interface IAnomalyVisualTunable
{
    string VisualTypeName { get; }
    AnomalyVisualTuningCapabilities VisualCapabilities { get; }
    AnomalyVisualTuningValues VisualValues { get; }
    void ApplyVisualValues(AnomalyVisualTuningValues values);
    void ResetVisualValues();
}

internal static class AnomalyVisualTuningFormatter
{
    public static string Format(
        string name,
        AnomalyVisualTuningCapabilities capabilities,
        AnomalyVisualTuningValues values)
    {
        StringBuilder text = new();
        text.AppendLine($"{name} Visual");
        AppendColor(text, "PrimaryColor", values.PrimaryColor,
            capabilities, AnomalyVisualTuningCapabilities.PrimaryColor);
        AppendColor(text, "SecondaryColor", values.SecondaryColor,
            capabilities, AnomalyVisualTuningCapabilities.SecondaryColor);
        AppendColor(text, "FillColor", values.FillColor,
            capabilities, AnomalyVisualTuningCapabilities.FillColor);
        AppendFloat(text, "FillAlpha", values.FillAlpha,
            capabilities, AnomalyVisualTuningCapabilities.FillAlpha);
        AppendFloat(text, "BoundaryWidth", values.BoundaryWidth,
            capabilities, AnomalyVisualTuningCapabilities.BoundaryWidth);
        AppendFloat(text, "InnerLineWidth", values.InnerLineWidth,
            capabilities, AnomalyVisualTuningCapabilities.InnerLineWidth);
        AppendFloat(text, "VisualScale", values.VisualScale,
            capabilities, AnomalyVisualTuningCapabilities.VisualScale);
        AppendFloat(text, "EdgeGlow", values.EdgeGlow,
            capabilities, AnomalyVisualTuningCapabilities.EdgeGlow);
        AppendFloat(text, "PulseSpeed", values.PulseSpeed,
            capabilities, AnomalyVisualTuningCapabilities.PulseSpeed);
        AppendFloat(text, "PulseStrength", values.PulseStrength,
            capabilities, AnomalyVisualTuningCapabilities.PulseStrength);
        AppendFloat(text, "PatternSpeed", values.PatternSpeed,
            capabilities, AnomalyVisualTuningCapabilities.PatternSpeed);
        AppendFloat(text, "BoundaryAlpha", values.BoundaryAlpha,
            capabilities, AnomalyVisualTuningCapabilities.BoundaryAlpha);
        AppendFloat(text, "PatternStrength", values.PatternStrength,
            capabilities, AnomalyVisualTuningCapabilities.PatternStrength);
        return text.ToString().TrimEnd();
    }

    private static void AppendColor(
        StringBuilder text,
        string label,
        Color value,
        AnomalyVisualTuningCapabilities capabilities,
        AnomalyVisualTuningCapabilities required)
    {
        if ((capabilities & required) == 0)
            return;

        text.AppendLine(
            $"{label} = ({value.r:0.###}, {value.g:0.###}, " +
            $"{value.b:0.###}, {value.a:0.###})"
        );
    }

    private static void AppendFloat(
        StringBuilder text,
        string label,
        float value,
        AnomalyVisualTuningCapabilities capabilities,
        AnomalyVisualTuningCapabilities required)
    {
        if ((capabilities & required) != 0)
            text.AppendLine($"{label} = {value:0.###}");
    }
}

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
