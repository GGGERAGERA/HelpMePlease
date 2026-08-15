using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class RunItemSlotsHUD : MonoBehaviour
{
    [SerializeField] private Image[] iconImages;
    [SerializeField] private TextMeshProUGUI[] levelTexts;

    private RunStateManager runState;

    private void OnEnable()
    {
        runState = RunStateManager.Instance != null
            ? RunStateManager.Instance
            : RunStateManager.EnsureExists();

        runState.ItemSlots.SlotsChanged += Refresh;
        Refresh();
    }

    private void OnDisable()
    {
        if (runState != null)
            runState.ItemSlots.SlotsChanged -= Refresh;

        runState = null;
    }

    private void Refresh()
    {
        IReadOnlyList<RunItemSlot> slots = runState.ItemSlots.Slots;
        int visibleSlotCount = slots.Count;

        for (int i = 0; i < visibleSlotCount; i++)
        {
            RunItemSlot slot = slots[i];
            UpgradeData item = slot.Item;
            Sprite icon = item != null ? item.icon : null;

            if (iconImages != null && i < iconImages.Length && iconImages[i] != null)
            {
                iconImages[i].sprite = icon;
                iconImages[i].enabled = icon != null;
            }

            if (levelTexts != null && i < levelTexts.Length && levelTexts[i] != null)
            {
                string level = item != null ? GetRomanLevel(slot.Level) : string.Empty;
                levelTexts[i].text = level;
                levelTexts[i].enabled = level.Length > 0;
            }
        }

        for (int i = visibleSlotCount; iconImages != null && i < iconImages.Length; i++)
        {
            if (iconImages[i] != null)
                iconImages[i].enabled = false;
        }

        for (int i = visibleSlotCount; levelTexts != null && i < levelTexts.Length; i++)
        {
            if (levelTexts[i] != null)
                levelTexts[i].enabled = false;
        }
    }

    private static string GetRomanLevel(int level)
    {
        return level switch
        {
            1 => "I",
            2 => "II",
            3 => "III",
            _ => string.Empty
        };
    }
}
