using UnityEngine;

[System.Serializable]
public class UnlockConditionData
{
    public UnlockConditionType type;

    [Tooltip("Tupik, Bomber, Darkness, Rain")]
    public string targetId;

    public int requiredAmount = 1;
}