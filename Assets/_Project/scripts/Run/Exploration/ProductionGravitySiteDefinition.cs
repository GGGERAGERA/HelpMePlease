using UnityEngine;

internal static class ProductionGravitySiteDefinition
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void RegisterDefinition()
    {
        ProductionAnomalySiteDefinitionRegistry.Register(
            new ProductionAnomalySiteDefinition(
                AnomalyPowerType.GravityOrb,
                "GRAVITY",
                CreateEnvironment
            )
        );
    }

    private static IProductionAnomalySiteEnvironment CreateEnvironment(
        ProductionAnomalySiteContext context)
    {
        LocalAnomalyZone zone = context.AnomalyController?.SpawnSiteZone(
            context.Config.GravityAnomaly,
            context.Position,
            context.Size,
            context.SiteObject.transform
        );

        if (zone is GravityZone gravityZone)
        {
            gravityZone.ConfigureOrbit(
                7f,
                3.5f,
                2.5f,
                1f,
                0.7f,
                0.35f
            );
        }

        Debug.Log(
            "[ExplorationSector] Special Site: GRAVITY " +
            "(GravityZone)."
        );
        return new ProductionGravitySiteEnvironment(
            context.AnomalyController,
            zone
        );
    }
}

internal sealed class ProductionGravitySiteEnvironment :
    IProductionAnomalySiteEnvironment
{
    private readonly LevelAnomalyController anomalyController;
    private LocalAnomalyZone anomalyZone;

    public LocalAnomalyZone AnomalyZone => anomalyZone;

    public ProductionGravitySiteEnvironment(
        LevelAnomalyController controller,
        LocalAnomalyZone zone)
    {
        anomalyController = controller;
        anomalyZone = zone;
    }

    public void Collapse()
    {
        if (anomalyZone == null)
            return;

        if (anomalyController != null) anomalyController.CollapseSiteZone(anomalyZone);
        anomalyZone = null;
    }

    public void SetDebugVisualEmphasis(float multiplier)
    {
        if (anomalyZone is GravityZone gravityZone)
            gravityZone.SetDebugVisualEmphasis(multiplier);
    }
}
