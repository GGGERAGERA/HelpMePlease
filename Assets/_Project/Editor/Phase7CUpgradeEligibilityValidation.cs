using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public static class Phase7CUpgradeEligibilityValidation
{
    [UnityEditor.MenuItem(
        "Tools/Subject 42/Validate Phase 7C Upgrade Eligibility")]
    public static void Run()
    {
        UpgradeData[] upgrades = CreatePool();

        try
        {
            ValidateEmptySlots(upgrades);
            ValidateFiveSlots(upgrades);
            ValidateFullSlots(upgrades);
            ValidateFullSlotsWithMaxedItems(upgrades);
            ValidateAllMaxed(upgrades);
            ValidateNumericChest(upgrades);
            ValidateImprovedChestFallback(upgrades);
            ValidateReducedChoiceCounts(upgrades);
            Debug.Log("[Phase7CValidation] PASS: all eligibility scenarios passed.");
        }
        finally
        {
            for (int i = 0; i < upgrades.Length; i++)
                UnityEngine.Object.DestroyImmediate(upgrades[i]);
        }
    }

    private static void ValidateEmptySlots(UpgradeData[] upgrades)
    {
        RunItemSlots slots = new();
        List<UpgradeData> choices = RollAll(upgrades, slots);

        Require(slots.UsedSlotCount == 0, "Case A: expected 0/6 slots.");
        Require(slots.HasFreeUniqueSlot, "Case A: expected a free slot.");
        RequireSet(choices, upgrades, "Case A: empty inventory must allow the full pool.");
    }

    private static void ValidateFiveSlots(UpgradeData[] upgrades)
    {
        RunItemSlots slots = Fill(upgrades, 5);
        List<UpgradeData> choices = RollAll(upgrades, slots);

        Require(slots.UsedSlotCount == 5, "Case B: expected 5/6 slots.");
        Require(slots.HasFreeUniqueSlot, "Case B: sixth unique slot must be available.");
        Require(slots.CanAccept(upgrades[5]), "Case B: a new sixth item must be accepted.");
        Require(slots.CanAccept(upgrades[0]), "Case B: an owned non-maxed item must be accepted.");
        RequireSet(choices, upgrades, "Case B: all eligible new and owned items must remain rollable.");
    }

    private static void ValidateFullSlots(UpgradeData[] upgrades)
    {
        RunItemSlots slots = Fill(upgrades, RunItemSlots.SlotCount);
        UpgradeData[] owned = Slice(upgrades, 0, RunItemSlots.SlotCount);
        List<UpgradeData> choices = RollAll(upgrades, slots);

        Require(!slots.HasFreeUniqueSlot, "Case C: expected a full inventory.");
        Require(!slots.CanAccept(upgrades[6]), "Case C: a new item must be rejected.");
        Require(slots.CanAccept(upgrades[0]), "Case C: an owned level-I item must remain eligible.");
        RequireSet(choices, owned, "Case C: full inventory must roll owned items only.");
    }

    private static void ValidateFullSlotsWithMaxedItems(UpgradeData[] upgrades)
    {
        RunItemSlots slots = Fill(upgrades, RunItemSlots.SlotCount);
        Max(slots, upgrades[0]);
        Max(slots, upgrades[1]);
        UpgradeData[] expected = Slice(upgrades, 2, 4);
        List<UpgradeData> choices = RollAll(upgrades, slots);

        Require(!slots.CanAccept(upgrades[0]), "Case D: level-III item must be ineligible.");
        RequireSet(choices, expected, "Case D: maxed and new items must both be absent.");
    }

    private static void ValidateAllMaxed(UpgradeData[] upgrades)
    {
        RunItemSlots slots = Fill(upgrades, RunItemSlots.SlotCount);

        for (int i = 0; i < RunItemSlots.SlotCount; i++)
            Max(slots, upgrades[i]);

        UpgradeRoller roller = new(upgrades, slots);
        Require(roller.RollChoices(10, 3).Count == 0,
            "Case E: normal roll must be empty when all owned items are maxed.");
        Require(roller.RollNumericChoices(10, 3).Count == 0,
            "Case E: numeric roll must be empty when all owned items are maxed.");
        Require(roller.RollRewardChoices(10, 3).Count == 0,
            "Case E: improved roll must be empty when all owned items are maxed.");
    }

    private static void ValidateNumericChest(UpgradeData[] upgrades)
    {
        RunItemSlots slots = Fill(upgrades, RunItemSlots.SlotCount);
        List<UpgradeData> choices = new UpgradeRoller(upgrades, slots)
            .RollNumericChoices(10, 99);
        UpgradeData[] expected = Slice(upgrades, 0, RunItemSlots.SlotCount);

        RequireSet(choices, expected,
            "Case F: numeric chest must contain owned non-maxed Numeric only.");
    }

    private static void ValidateImprovedChestFallback(UpgradeData[] upgrades)
    {
        RunItemSlots slots = Fill(upgrades, RunItemSlots.SlotCount);
        List<UpgradeData> choices = new UpgradeRoller(upgrades, slots)
            .RollRewardChoices(10, 3);

        Require(choices.Count == 3,
            "Case G: improved chest must fall back to general eligible items.");

        for (int i = 0; i < choices.Count; i++)
        {
            Require(slots.Contains(choices[i]),
                "Case G: improved fallback returned a non-owned item.");
            Require(choices[i].category == UpgradeCategory.Numeric,
                "Case G: no owned Behavior exists, so fallback must be Numeric.");
        }
    }

    private static void ValidateReducedChoiceCounts(UpgradeData[] upgrades)
    {
        RunItemSlots slots = Fill(upgrades, RunItemSlots.SlotCount);

        for (int i = 0; i < 4; i++)
            Max(slots, upgrades[i]);

        UpgradeRoller roller = new(upgrades, slots);
        Require(roller.RollChoices(10, 3).Count == 2,
            "Two eligible items must produce two cards.");

        Max(slots, upgrades[4]);
        Require(roller.RollChoices(10, 3).Count == 1,
            "One eligible item must produce one card.");
    }

    private static UpgradeData[] CreatePool()
    {
        UpgradeData[] upgrades = new UpgradeData[8];

        for (int i = 0; i < upgrades.Length; i++)
        {
            UpgradeData upgrade = ScriptableObject.CreateInstance<UpgradeData>();
            upgrade.name = $"Phase7C_Test_{i}";
            upgrade.upgradeName = upgrade.name;
            upgrade.minPlayerLevel = 1;
            upgrade.category = i == upgrades.Length - 1
                ? UpgradeCategory.Behavior
                : UpgradeCategory.Numeric;
            upgrades[i] = upgrade;
        }

        return upgrades;
    }

    private static RunItemSlots Fill(UpgradeData[] upgrades, int count)
    {
        RunItemSlots slots = new();

        for (int i = 0; i < count; i++)
        {
            Require(slots.TryAdd(upgrades[i]) == ItemGrantResult.Added,
                $"Could not add test item {i}.");
        }

        return slots;
    }

    private static void Max(RunItemSlots slots, UpgradeData upgrade)
    {
        while (slots.GetLevel(upgrade) < RunItemSlots.MaxItemLevel)
        {
            Require(slots.TryAdd(upgrade) == ItemGrantResult.LeveledUp,
                $"Could not level {upgrade.name}.");
        }
    }

    private static List<UpgradeData> RollAll(
        UpgradeData[] upgrades,
        RunItemSlots slots)
    {
        return new UpgradeRoller(upgrades, slots).RollChoices(10, 99);
    }

    private static UpgradeData[] Slice(
        UpgradeData[] source,
        int start,
        int count)
    {
        UpgradeData[] result = new UpgradeData[count];
        Array.Copy(source, start, result, 0, count);
        return result;
    }

    private static void RequireSet(
        IReadOnlyList<UpgradeData> actual,
        IReadOnlyList<UpgradeData> expected,
        string message)
    {
        Require(actual.Count == expected.Count,
            $"{message} Expected {expected.Count}, got {actual.Count}.");

        for (int i = 0; i < expected.Count; i++)
            Require(actual.Contains(expected[i]), message);
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
