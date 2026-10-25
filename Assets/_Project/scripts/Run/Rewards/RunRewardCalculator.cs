using UnityEngine;

public static class RunRewardCalculator
{
    private const int KillsPerGold = 5;
    private const int GoldPerCompletedLevel = 100;
    private const int GoldPerMinute = 5;

    public static int CalculateGold(
        int kills,
        float runTime,
        float completedLevelRewardMultiplierTotal,
        RunEndReason endReason)
    {
        return CalculateGold(
            (float)Mathf.Max(0, kills),
            runTime,
            completedLevelRewardMultiplierTotal,
            endReason
        );
    }

    public static int CalculateGold(
        float killRewardUnits,
        float runTime,
        float completedLevelRewardMultiplierTotal,
        RunEndReason endReason)
    {
        int killReward = Mathf.FloorToInt(killRewardUnits / KillsPerGold);
        int timeReward = Mathf.FloorToInt(runTime / 60f) * GoldPerMinute;
        int levelReward = Mathf.RoundToInt(
            Mathf.Max(0f, completedLevelRewardMultiplierTotal) *
            GoldPerCompletedLevel
        );

        int total = killReward + timeReward + levelReward;

        return Mathf.Max(0, total);
    }
}
