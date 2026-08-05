using UnityEngine;

public sealed class RunSector
{
    public int SectorNumber { get; }
    public StageProfileData StageProfile { get; }
    public WorldRuleData WorldRule { get; }
    public LocalAnomalyData LocalAnomaly { get; }

    public float Duration => StageProfile != null
        ? StageProfile.Duration
        : 0f;
    public EnemySpawnProfile SpawnProfile => StageProfile != null
        ? StageProfile.SpawnProfile
        : null;
    public GameObject BossPrefab => StageProfile != null
        ? StageProfile.BossPrefab
        : null;
    public float EnemyHealthMultiplier => StageProfile != null
        ? StageProfile.EnemyHealthMultiplier
        : 1f;
    public float EnemySpeedMultiplier => StageProfile != null
        ? StageProfile.EnemySpeedMultiplier
        : 1f;
    public float SpawnPressureMultiplier => StageProfile != null
        ? StageProfile.SpawnPressureMultiplier
        : 1f;
    public float ExperienceGainMultiplier => StageProfile != null
        ? StageProfile.ExperienceGainMultiplier *
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
        WorldRule = worldRule;
        LocalAnomaly = localAnomaly;
    }

}
