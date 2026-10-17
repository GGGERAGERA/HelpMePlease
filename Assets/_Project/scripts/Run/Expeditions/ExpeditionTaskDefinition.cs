using System;
using UnityEngine;

public enum ExpeditionTaskCondition { SurviveMinutes, Kills, CompletedLocations }

[CreateAssetMenu(menuName = "Subject42/Expeditions/Repeatable Task")]
public sealed class ExpeditionTaskDefinition : ScriptableObject
{
    [SerializeField] private string id;
    [SerializeField] private string title;
    [SerializeField] private string description;
    [SerializeField] private string progressLabel;
    [SerializeField] private ExpeditionTaskCondition condition;
    [SerializeField, Min(1)] private int target = 1;
    [SerializeField, Min(1)] private int rewardCores = 1;
    [Tooltip("Empty means every sector whose task list includes this asset.")]
    [SerializeField] private string[] allowedSectors = Array.Empty<string>();
    public string Id => id;
    public string Title => title;
    public string Description => description;
    public string ProgressLabel => progressLabel;
    public ExpeditionTaskCondition Condition => condition;
    public int Target => target;
    public int RewardCores => rewardCores;
    public bool IsAvailable(RunConfig config) => config != null && !string.IsNullOrEmpty(id) &&
        target > 0 && rewardCores > 0 && Enum.IsDefined(typeof(ExpeditionTaskCondition), condition) &&
        (condition != ExpeditionTaskCondition.CompletedLocations || target <= RunRoute.ExplorationSectorCount) &&
        (allowedSectors == null || allowedSectors.Length == 0 || Array.IndexOf(allowedSectors, config.SectorId) >= 0);

    public int Measure(int kills, float seconds, int locations) => condition switch
    {
        ExpeditionTaskCondition.SurviveMinutes => Mathf.FloorToInt(seconds / 60f),
        ExpeditionTaskCondition.Kills => kills,
        ExpeditionTaskCondition.CompletedLocations => locations,
        _ => 0
    };
}
