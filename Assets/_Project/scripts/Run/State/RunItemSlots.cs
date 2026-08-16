using System;
using System.Collections.Generic;

public enum ItemGrantResult
{
    Added,
    LeveledUp,
    MaxLevel,
    RequiresReplacement,
    Invalid,
    IncompatibleWeapon,
    ExclusiveConflict
}

[Serializable]
public sealed class RunItemSlot
{
    public UpgradeData Item { get; private set; }
    public int Level { get; private set; }

    internal void Set(UpgradeData item, int level)
    {
        Item = item;
        Level = level;
    }

    internal void Clear()
    {
        Item = null;
        Level = 0;
    }
}

/// <summary>
/// Runtime-only item slots for the current run.
/// </summary>
public sealed class RunItemSlots
{
    public const int SlotCount = 4;
    public const int ProductionTargetSlotCount = 4;
    public const int MaxItemLevel = 3;

    private readonly RunItemSlot[] slots;
    private readonly IReadOnlyList<RunItemSlot> readOnlySlots;

    public event Action SlotsChanged;

    public IReadOnlyList<RunItemSlot> Slots => readOnlySlots;
    public int Capacity => slots.Length;
    public int UsedSlotCount
    {
        get
        {
            int count = 0;

            for (int i = 0; i < slots.Length; i++)
            {
                if (slots[i].Item != null)
                    count++;
            }

            return count;
        }
    }
    public bool HasFreeUniqueSlot => UsedSlotCount < Capacity;

    public RunItemSlots(int capacity = SlotCount)
    {
        if (capacity <= 0)
            throw new ArgumentOutOfRangeException(nameof(capacity));

        slots = new RunItemSlot[capacity];

        for (int i = 0; i < slots.Length; i++)
            slots[i] = new RunItemSlot();

        readOnlySlots = Array.AsReadOnly(slots);
    }

    public ItemGrantResult TryAdd(UpgradeData item)
    {
        if (item == null)
            return ItemGrantResult.Invalid;

        int existingIndex = FindItemIndex(item);

        if (existingIndex >= 0)
        {
            RunItemSlot existingSlot = slots[existingIndex];

            if (existingSlot.Level >= MaxItemLevel)
                return ItemGrantResult.MaxLevel;

            existingSlot.Set(item, existingSlot.Level + 1);
            SlotsChanged?.Invoke();
            return ItemGrantResult.LeveledUp;
        }

        int emptyIndex = FindEmptySlotIndex();

        if (emptyIndex < 0)
            return ItemGrantResult.RequiresReplacement;

        slots[emptyIndex].Set(item, 1);
        SlotsChanged?.Invoke();
        return ItemGrantResult.Added;
    }

    public bool Contains(UpgradeData item)
    {
        return item != null && FindItemIndex(item) >= 0;
    }

    public int GetLevel(UpgradeData item)
    {
        int index = item != null ? FindItemIndex(item) : -1;
        return index >= 0 ? slots[index].Level : 0;
    }

    public int GetLevel(UpgradeType type)
    {
        for (int i = 0; i < slots.Length; i++)
        {
            UpgradeData item = slots[i].Item;
            if (item != null && item.upgradeType == type)
                return slots[i].Level;
        }

        return 0;
    }

    public bool CanAccept(UpgradeData item)
    {
        if (item == null)
            return false;

        int level = GetLevel(item);

        if (level >= MaxItemLevel)
            return false;

        return level > 0 || HasFreeUniqueSlot;
    }

    public bool TryReplace(int slotIndex, UpgradeData newItem)
    {
        if (slotIndex < 0 || slotIndex >= slots.Length || newItem == null)
            return false;

        int existingIndex = FindItemIndex(newItem);

        if (existingIndex >= 0 && existingIndex != slotIndex)
            return false;

        slots[slotIndex].Set(newItem, 1);
        SlotsChanged?.Invoke();
        return true;
    }

    public void Clear()
    {
        bool changed = false;

        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i].Item != null || slots[i].Level != 0)
                changed = true;

            slots[i].Clear();
        }

        if (changed)
            SlotsChanged?.Invoke();
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    public bool TrySetLevelForDebug(UpgradeData item, int targetLevel)
    {
        if (item == null || targetLevel < 0 || targetLevel > MaxItemLevel)
            return false;

        int index = FindItemIndex(item);
        if (targetLevel == 0)
        {
            if (index < 0)
                return true;

            slots[index].Clear();
            SlotsChanged?.Invoke();
            return true;
        }

        if (index < 0)
        {
            index = FindEmptySlotIndex();
            if (index < 0)
                return false;
        }

        slots[index].Set(item, targetLevel);
        SlotsChanged?.Invoke();
        return true;
    }
#endif

    private int FindItemIndex(UpgradeData item)
    {
        for (int i = 0; i < slots.Length; i++)
        {
            if (ReferenceEquals(slots[i].Item, item))
                return i;
        }

        return -1;
    }

    private int FindEmptySlotIndex()
    {
        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i].Item == null)
                return i;
        }

        return -1;
    }
}
