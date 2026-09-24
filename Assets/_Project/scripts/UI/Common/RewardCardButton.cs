using UnityEngine;
using UnityEngine.UI;

// Preserve the authored Animator (including its exact scale curves), while tinting
// the new frame through the same Button state. Selection and click handling are inherited.
public sealed class RewardCardButton : Button
{
    [Header("Reward presentation")]
    [SerializeField] private RewardCardVisualPreset[] visualPresets;
    [SerializeField] private RewardCardVisualPreset fallbackPreset;
    [SerializeField] private Image headerAccent;

    public void SetReward(UpgradeData reward)
    {
        var preset = fallbackPreset;
        if (reward is Subject42.Combat.OrbitalStation.OrbitalRewardData orbital && visualPresets != null)
            foreach (var candidate in visualPresets)
                if (candidate != null && candidate.Matches(orbital.RewardKind))
                {
                    preset = candidate;
                    break;
                }
        if (preset != null) colors = preset.states;
        DoStateTransition(currentSelectionState, true);
    }

    protected override void DoStateTransition(SelectionState state, bool instant)
    {
        base.DoStateTransition(state, instant);
        if (targetGraphic == null || !gameObject.activeInHierarchy) return;
        Color tint = state switch
        {
            SelectionState.Highlighted => colors.highlightedColor,
            SelectionState.Pressed => colors.pressedColor,
            SelectionState.Selected => colors.selectedColor,
            SelectionState.Disabled => colors.disabledColor,
            _ => colors.normalColor
        };
        targetGraphic.CrossFadeColor(tint * colors.colorMultiplier,
            instant ? 0f : colors.fadeDuration, true, true);
        if (headerAccent != null)
            headerAccent.CrossFadeColor(tint * colors.colorMultiplier,
                instant ? 0f : colors.fadeDuration, true, true);
    }
}
