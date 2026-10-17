using System;
using UnityEngine;

// A receipt-backed breakdown, never a request to award money from a result screen.
[Serializable]
public sealed class ExpeditionCoreRewards
{
    public string RunToken;
    public string TaskTitle;
    public bool TaskCompleted;
    public int TaskCores;
    public int SectorCores;
    public int FirstClearCores;
    public int Total => TaskCores + SectorCores + FirstClearCores;
    public ExpeditionCoreRewards Copy() => (ExpeditionCoreRewards)MemberwiseClone();
}

/// <summary>One optional repeatable objective and its payouts, owned by one real run.</summary>
public sealed class ExpeditionTaskRun
{
    public const string LastRewardsKey = "META_EXPEDITION_CORE_REWARDS";
    public event Action Changed;
    public event Action Completed;
    public ExpeditionTaskDefinition Task { get; }
    public int Progress { get; private set; }
    public bool IsCompleted => rewards.TaskCompleted;
    public ExpeditionCoreRewards Snapshot => rewards.Copy();
    private readonly RunConfig config;
    private readonly CurrencyManager currency;
    private readonly SurfaceMapService map;
    private readonly ExpeditionCoreRewards rewards;
    private readonly int target, taskReward;
    private bool finished;

    public ExpeditionTaskRun(RunConfig config, CurrencyManager currency, SurfaceMapService map)
    {
        this.config = config;
        this.currency = currency;
        this.map = map;
        Task = config.ExpeditionTask != null && config.ExpeditionTask.IsAvailable(config) ? config.ExpeditionTask : null;
        target = Task != null ? Task.Target : 0;
        taskReward = Task != null ? Task.RewardCores : 0;
        rewards = new ExpeditionCoreRewards { RunToken = Guid.NewGuid().ToString("N"), TaskTitle = Task != null ? Task.Title : null };
    }

    public void Observe(int kills, float seconds, int locations)
    {
        if (finished || Task == null || IsCompleted) return;
        int next = Mathf.Clamp(Task.Measure(kills, seconds, locations), Progress, target);
        bool changed = Progress != next;
        Progress = next;
        if (Progress >= target && currency != null && currency.TryGrantCoresOnce(
            rewards.RunToken + "/task", taskReward, () =>
            {
                rewards.TaskCompleted = true;
                rewards.TaskCores = taskReward;
                StageRewards();
            }))
        {
            Changed?.Invoke();
            Completed?.Invoke();
        }
        else if (changed) Changed?.Invoke();
    }

    public void Finish(RunEndReason reason, bool confirmedProductionVictory)
    {
        if (finished) return;
        finished = true;
        if (reason != RunEndReason.Victory || !confirmedProductionVictory || currency == null || map == null ||
            config.MapId != map.Definition.Id || map.GetStatus(config.SectorId) == SurfaceSectorStatus.Locked) return;

        currency.TryGrantCoresOnce(rewards.RunToken + "/sector", config.VictoryCores, () =>
        {
            rewards.SectorCores = config.VictoryCores;
            StageRewards();
        });
        // Existing saved completions are authoritative: never backfill old clears.
        // A stable first-clear receipt also protects the gap before map progression saves.
        if (map.GetStatus(config.SectorId) != SurfaceSectorStatus.Completed)
            currency.TryGrantCoresOnce("first/" + config.MapId + "/" + config.SectorId, config.FirstClearCores, () =>
            {
                rewards.FirstClearCores = config.FirstClearCores;
                StageRewards();
            });
        Changed?.Invoke();
    }

    private void StageRewards() => PlayerPrefs.SetString(LastRewardsKey, JsonUtility.ToJson(rewards));
}
