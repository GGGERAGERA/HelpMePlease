using UnityEngine;

public static class RunRewardCalculator
{
    public static int CalculateGold(bool victory)
    {
        int kills = RunStatsManager.Instance != null ? RunStatsManager.Instance.Kills : 0;
        float runTime = RunStatsManager.Instance != null ? RunStatsManager.Instance.RunTime : 0f;

        int minutes = Mathf.FloorToInt(runTime / 60f);

        int gold = kills + minutes * 10;

        if (victory)
            gold = Mathf.RoundToInt(gold * 1.25f);
        else
            gold = Mathf.RoundToInt(gold * 0.8f);

        return Mathf.Max(0, gold);
    }
}
