using Subject42.Combat.OrbitalStation;
using UnityEngine;
using UnityEngine.UI;

[CreateAssetMenu(menuName = "UI/Reward Card Visual Preset")]
public sealed class RewardCardVisualPreset : ScriptableObject
{
    [Tooltip("Presentation mapping only; gameplay categories are unchanged.")]
    public OrbitalRewardKind[] rewardKinds;
    public ColorBlock states = ColorBlock.defaultColorBlock;

    public bool Matches(OrbitalRewardKind kind) =>
        rewardKinds != null && System.Array.IndexOf(rewardKinds, kind) >= 0;
}
