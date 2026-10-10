using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>Authored equipment face; native Button owns input, navigation and interaction state.</summary>
public sealed class PixelMenuButton : Button
{
    [Header("Pixel presentation")]
    [SerializeField] private RectTransform face;
    [SerializeField] private TMP_Text label;
    [SerializeField] private ColorBlock labelColors = ColorBlock.defaultColorBlock;
    [SerializeField] private Vector2 pressedOffset = new Vector2(0f, -3f);

    protected override void DoStateTransition(SelectionState state, bool instant)
    {
        base.DoStateTransition(state, instant);
        // Selectable can initialize before serialized references are assigned in Editor.
        if (face == null || label == null) return;
        face.anchoredPosition = state == SelectionState.Pressed ? pressedOffset : Vector2.zero;
        Color tint = state switch
        {
            SelectionState.Highlighted => labelColors.highlightedColor,
            SelectionState.Selected => labelColors.selectedColor,
            SelectionState.Pressed => labelColors.pressedColor,
            SelectionState.Disabled => labelColors.disabledColor,
            _ => labelColors.normalColor
        };
        label.CrossFadeColor(tint * labelColors.colorMultiplier,
            instant ? 0f : labelColors.fadeDuration, true, true);
    }

    public override void OnPointerEnter(PointerEventData eventData)
    {
        base.OnPointerEnter(eventData);
        if (IsActive() && IsInteractable()) AudioService.Instance?.Play(AudioCueId.UIHover);
    }

    public override void OnSelect(BaseEventData eventData)
    {
        base.OnSelect(eventData);
        if (IsActive() && IsInteractable()) AudioService.Instance?.Play(AudioCueId.UIHover);
    }

    public override void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Left && IsActive() && IsInteractable())
            AudioService.Instance?.Play(AudioCueId.UIConfirm);
        base.OnPointerClick(eventData);
    }

    public override void OnSubmit(BaseEventData eventData)
    {
        if (IsActive() && IsInteractable()) AudioService.Instance?.Play(AudioCueId.UIConfirm);
        base.OnSubmit(eventData);
    }
}
