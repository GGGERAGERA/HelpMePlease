using UnityEngine;

[CreateAssetMenu(menuName="Subject42/Missions/Reward")]
public sealed class MissionRewardDefinition : ScriptableObject
{
    [SerializeField,Min(0)] private int gold=100;
    public int Gold => Mathf.Max(0,gold);
}
