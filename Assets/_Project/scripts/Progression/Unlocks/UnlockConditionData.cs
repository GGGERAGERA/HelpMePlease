using UnityEngine;

[System.Serializable]
public class UnlockConditionData
{
    public UnlockConditionType type;

    [Tooltip("Tupik, Bomber, Darkness, Rain")]
    public string targetId;

    public int requiredAmount = 1;

    [Tooltip("Used only by StationLevelRequirement.")]
    public BunkerStationId stationId;
}
