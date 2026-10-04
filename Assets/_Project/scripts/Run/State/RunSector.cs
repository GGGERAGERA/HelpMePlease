using UnityEngine;

public sealed class RunSector
{
    private RunConfig config = RunConfig.Default;
    internal void ApplyRunConfig(RunConfig value) => config = value ?? RunConfig.Default;
    public int SectorNumber { get; }
    public StageProfileData StageProfile { get; }
    private readonly WorldRuleData stageWorldRule;
    // Surface selection supplies the initial location rule; subsequent locations own their chosen rule.
    public WorldRuleData WorldRule => SectorNumber <= RunRoute.FirstSector && config.WorldRule != null
        ? config.WorldRule : stageWorldRule;
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
        LocalAnomalyData localAnomaly)
    {
        SectorNumber = sectorNumber;
        StageProfile = stageProfile;
        stageWorldRule = worldRule;
        LocalAnomaly = localAnomaly;
    }

}
