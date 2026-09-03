using System;

[Serializable]
public sealed class RunSummary
{
    public RunEndReason EndReason;
    public int CompletedLevels;
    public int Kills;
    public float RunTime;
    public int GoldEarned;
    public int SectorNumber;
    public int PlayerLevel = 1;
    public int OrbitalRingCount;
    public int OrbitalModuleCount;
    public int OrbitalCoreLevel;

    public RunSummary(
        RunEndReason endReason,
        int completedLevels,
        int kills,
        float runTime,
        int goldEarned)
    {
        EndReason = endReason;
        CompletedLevels = completedLevels;
        Kills = kills;
        RunTime = runTime;
        GoldEarned = goldEarned;
    }
}
