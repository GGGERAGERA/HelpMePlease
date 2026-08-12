using UnityEngine;

internal static class ProductionGravitySiteDefinition
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void RegisterDefinition()
    {
        ProductionAnomalySiteDefinitionRegistry.Register(
            new ProductionAnomalySiteDefinition(
                AnomalyPowerType.GravityOrb,
                AnomalyPowerType.GravityOrb,
                "GRAVITY",
                "GRAVITY ORB",
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
            context.Size
        );

        GameObject servicesHost = context.ServicesHost != null
            ? context.ServicesHost
            : context.SiteObject;
        GravityTrajectoryService trajectoryService =
            servicesHost.GetComponent<GravityTrajectoryService>();

        if (trajectoryService == null)
        {
            trajectoryService =
                servicesHost.AddComponent<GravityTrajectoryService>();
        }

        trajectoryService.Disable();

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
            trajectoryService.SetGravityZone(gravityZone);
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

        anomalyController?.CollapseSiteZone(anomalyZone);
        anomalyZone = null;
    }

    public void SetDebugVisualEmphasis(float multiplier)
    {
        if (anomalyZone is GravityZone gravityZone)
            gravityZone.SetDebugVisualEmphasis(multiplier);
    }
}
