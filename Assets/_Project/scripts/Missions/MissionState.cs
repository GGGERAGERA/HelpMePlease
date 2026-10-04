using System;
using System.Collections.Generic;

public enum MissionState { Available, Active, ObjectiveCompleted, ReadyToTurnIn, Completed }
[Serializable]
public sealed class MissionObjectiveProgress
{
    public string objectiveId, data;
}
[Serializable]
public sealed class MissionProgressRecord
{
    public string missionId;
    public MissionState state;
    public List<string> committedObjectives=new();
    public List<MissionObjectiveProgress> objectiveProgress=new();
    public bool rewardClaimed;
}
[Serializable]
public sealed class MissionSaveState
{
    public int version=1;
    public List<MissionProgressRecord> missions=new();
}
