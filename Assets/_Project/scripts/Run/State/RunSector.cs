using UnityEngine;

public sealed class RunSector
{
    private RunConfig config = RunConfig.Default;
    internal void ApplyRunConfig(RunConfig value) => config = value ?? RunConfig.Default;
    public int SectorNumber { get; }
    public RouteNode RouteNode { get; }
    public LocationType LocationType => RouteNode?.LocationType ?? LocationType.Clearance;
    public float Duration => SectorNumber == RunRoute.TutorialSector
        ? StageProfile.ExitActivationTime : RunRoute.SectorDuration;
    public StageProfileData StageProfile { get; }
    private readonly WorldRuleData stageWorldRule;
    public WorldRuleData WorldRule => RouteNode != null ? RouteNode.WorldRule :
        SectorNumber <= RunRoute.FirstSector && config.WorldRule != null ? config.WorldRule : stageWorldRule;
    public LocalAnomalyData LocalAnomaly { get; }

    public EnemySpawnProfile SpawnProfile => config.SpawnProfile != null ? config.SpawnProfile : StageProfile != null
        ? StageProfile.SpawnProfile
        : null;
    public GameObject BossPrefab => StageProfile != null
        ? StageProfile.BossPrefab
        : null;
    public float EnemyHealthMultiplier => StageProfile != null
        ? StageProfile.EnemyHealthMultiplier * config.EnemyHealth
        : 1f;
    public float EnemySpeedMultiplier => StageProfile != null
        ? StageProfile.EnemySpeedMultiplier
        : 1f;
    public float SpawnPressureMultiplier => StageProfile != null
        ? StageProfile.SpawnPressureMultiplier * config.SpawnPressure
        : 1f;
    public float ExperienceGainMultiplier => StageProfile != null
        ? StageProfile.ExperienceGainMultiplier * config.Experience *
            (WorldRule != null
                ? WorldRule.SectorExperienceMultiplier
                : 1f)
        : 1f;
    public float CompletionGoldMultiplier => StageProfile != null
        ? StageProfile.CompletionGoldMultiplier *
            (WorldRule != null
                ? WorldRule.SectorCompletionGoldMultiplier
                : 1f)
        : 1f;

    public RunSector(
        int sectorNumber,
        StageProfileData stageProfile,
        WorldRuleData worldRule,
        LocalAnomalyData localAnomaly,
        RouteNode routeNode = null)
    {
        SectorNumber = sectorNumber;
        RouteNode = routeNode;
        StageProfile = stageProfile;
        stageWorldRule = worldRule;
        LocalAnomaly = localAnomaly;
    }

}
