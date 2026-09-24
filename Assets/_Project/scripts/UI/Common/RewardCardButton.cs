using UnityEngine;
using UnityEngine.UI;

// Preserve the authored Animator (including its exact scale curves), while tinting
// the new frame through the same Button state. Selection and click handling are inherited.
public sealed class RewardCardButton : Button
{
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
    }
}
