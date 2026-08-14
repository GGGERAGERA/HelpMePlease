using System;
using UnityEngine;

[Flags]
public enum WeaponUpgradeCapability
{
    None = 0,
    Pierce = 1 << 0,
    Ricochet = 1 << 1,
    MultiProjectile = 1 << 2,
    Knockback = 1 << 3
}

public static class WeaponUpgradeCapabilityResolver
{
    public static WeaponUpgradeCapability GetCurrentCapabilities()
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            WeaponUpgradeCapability current = WeaponUpgradeCapability.None;
            BaseWeapon[] weapons = player.GetComponentsInChildren<BaseWeapon>(false);
            for (int i = 0; i < weapons.Length; i++)
            {
                if (weapons[i] != null)
                    current |= weapons[i].UpgradeCapabilities;
            }

            return current;
        }

        WeaponData selected = RunStateManager.Instance != null
            ? RunStateManager.Instance.SelectedWeapon
            : null;
        if (selected == null || selected.weaponPrefab == null)
            return WeaponUpgradeCapability.None;

        BaseWeapon prefabWeapon =
            selected.weaponPrefab.GetComponentInChildren<BaseWeapon>(true);
        return prefabWeapon != null
            ? prefabWeapon.UpgradeCapabilities
            : WeaponUpgradeCapability.None;
    }
}

public static class UpgradeEligibilityRules
{
    public static bool IsWeaponCompatible(
        UpgradeData upgrade,
        WeaponUpgradeCapability capabilities)
    {
        if (upgrade == null)
            return false;

        WeaponUpgradeCapability required = upgrade.requiredWeaponCapabilities;
        return required == WeaponUpgradeCapability.None ||
               (capabilities & required) == required;
    }

    public static bool HasExclusiveConflict(
        UpgradeData upgrade,
        RunItemSlots itemSlots)
    {
        if (upgrade == null || itemSlots == null ||
            string.IsNullOrWhiteSpace(upgrade.exclusiveGroup))
        {
            return false;
        }

        System.Collections.Generic.IReadOnlyList<RunItemSlot> slots =
            itemSlots.Slots;
        for (int i = 0; i < slots.Count; i++)
        {
            UpgradeData owned = slots[i].Item;
            if (owned == null || ReferenceEquals(owned, upgrade))
                continue;

            if (string.Equals(
                    owned.exclusiveGroup,
                    upgrade.exclusiveGroup,
                    StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }
}
