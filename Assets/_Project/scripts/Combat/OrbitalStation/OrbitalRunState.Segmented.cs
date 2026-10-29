using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Subject42.Combat.OrbitalStation
{
    public enum SegmentedLinkKind { Damage, FireRate, ExtraShot, Ricochet }

    [Serializable]
    public sealed class SegmentedLinkState
    {
        public int RingId, MountIndex;
        public SegmentedLinkKind Kind;
        [NonSerialized] public float NextExtraShot;
    }

    public sealed partial class OrbitalRunState
    {
        public const int MaxSegmentedWeapons = 12;
        public int UnlockedSegmentedLevels = 1;
        public int SegmentedWeaponLimit = 3;
        // Installed weapons and this reserve are disjoint; stowing retains identity and upgrades.
        public List<OrbitalModuleState> SegmentedWeapons = new();
        public List<SegmentedLinkKind> SegmentedLinkStock = new();
        public List<SegmentedLinkState> SegmentedLinks = new();

        public static bool IsSegmentedWeapon(OrbitalModuleKind kind) => kind is
            OrbitalModuleKind.Pistol or OrbitalModuleKind.Shotgun or OrbitalModuleKind.Sniper or OrbitalModuleKind.Saw;

        public static float SegmentedMountAngle(int level, int position) =>
            90f + (level == 1 ? 15f : 0f) + position * 30f;

        public static bool IsSegmentedWeaponSlot(int level, int position) => level > 0 || position % 2 == 0;

        public SegmentedLinkState SegmentedModuleAt(int ringId, int position) =>
            SegmentedLinks.FirstOrDefault(module => module.RingId == ringId && module.MountIndex == position);

        public IEnumerable<OrbitalModuleState> SegmentedModuleTargets(SegmentedLinkState module) =>
            SegmentedModuleTargets(module.RingId, module.MountIndex);

        private IEnumerable<OrbitalModuleState> SegmentedModuleTargets(int ringId, int position)
        {
            foreach (var weapon in Modules)
            {
                if (weapon.StableRingId != ringId || weapon.ModuleType is not
                    (OrbitalModuleKind.Pistol or OrbitalModuleKind.Shotgun or OrbitalModuleKind.Sniper)) continue;
                int gap = Mathf.Abs(weapon.MountIndex - position);
                if (gap == 1 || gap == SegmentedPositions - 1) yield return weapon;
            }
        }

        public IEnumerable<SegmentedLinkState> ActiveSegmentedLinks() =>
            SegmentedLinks.Where(module => SegmentedModuleTargets(module).Any());

        public int SegmentedBonusCount(int id, SegmentedLinkKind kind) => UsesSegmentedOrbit
            ? SegmentedLinks.Count(module => module.Kind == kind && SegmentedModuleTargets(module).Any(weapon => weapon.StableModuleId == id)) : 0;

        public bool TryGrantSegmentedLink(SegmentedLinkKind kind)
        {
            if (!UsesSegmentedOrbit || !Enum.IsDefined(typeof(SegmentedLinkKind), kind) || !CanCommit(out _)) return false;
            SegmentedLinkStock.Add(kind);
            Revision++;
            return true;
        }

        public bool TryGrantSegmentedWeapon(OrbitalModuleKind kind)
        {
            if (!UsesSegmentedOrbit || !IsSegmentedWeapon(kind) || !CanCommit(out _) || NextStableModuleId == int.MaxValue) return false;
            SegmentedWeapons.Add(new OrbitalModuleState
            {
                StableModuleId = NextStableModuleId++, ModuleType = kind, StableRingId = 0, MountIndex = -1
            });
            Revision++;
            return true;
        }

        private bool CanUseSegmentedModuleSlot(int ringId, int position, out string error)
        {
            var ring = FindRing(ringId);
            return Rule(UsesSegmentedOrbit && ring != null && IsRingActive(ring) && position >= 0 && position < SegmentedPositions &&
                !IsSegmentedWeaponSlot(ring.Order, position), "Выберите открытую зелёную ячейку M.", out error) &&
                Rule(SegmentedModuleTargets(ringId, position).Any(), "Рядом с M нужна хотя бы одна стреляющая пушка.", out error);
        }

        public bool CanInstallSegmentedModule(SegmentedLinkKind kind, int ringId, int position, out string error) =>
            CanUseSegmentedModuleSlot(ringId, position, out error) &&
            Rule(SegmentedLinkStock.Contains(kind), "Такого модуля нет в запасе.", out error) &&
            Rule(SegmentedModuleAt(ringId, position)?.Kind != kind, "Этот модуль уже установлен в M.", out error);

        public bool TryInstallSegmentedModule(SegmentedLinkKind kind, int ringId, int position, out string error)
        {
            if (!CanCommit(out error) || !CanInstallSegmentedModule(kind, ringId, position, out error)) return false;
            SegmentedLinkStock.Remove(kind);
            var installed = SegmentedModuleAt(ringId, position);
            if (installed != null)
            {
                SegmentedLinkStock.Add(installed.Kind);
                installed.Kind = kind;
            }
            else SegmentedLinks.Add(new SegmentedLinkState { RingId = ringId, MountIndex = position, Kind = kind });
            Revision++;
            return true;
        }

        public bool CanMoveSegmentedModule(int index, int ringId, int position, out string error) =>
            Rule(index >= 0 && index < SegmentedLinks.Count, "Модуль не выбран.", out error) &&
            CanUseSegmentedModuleSlot(ringId, position, out error) &&
            Rule(SegmentedModuleAt(ringId, position) == null, "Ячейка M занята.", out error);

        public bool TryMoveSegmentedModule(int index, int ringId, int position, out string error)
        {
            if (!CanCommit(out error) || !CanMoveSegmentedModule(index, ringId, position, out error)) return false;
            SegmentedLinks[index].RingId = ringId; SegmentedLinks[index].MountIndex = position;
            Revision++;
            return true;
        }

        public bool TryDisconnectSegmented(int index)
        {
            if (!CanCommit(out _) || index < 0 || index >= SegmentedLinks.Count) return false;
            SegmentedLinkStock.Add(SegmentedLinks[index].Kind);
            SegmentedLinks.RemoveAt(index);
            Revision++;
            return true;
        }

        private void ReturnSegmentedRingModules(int ringId)
        {
            for (int i = SegmentedLinks.Count - 1; i >= 0; i--)
                if (SegmentedLinks[i].RingId == ringId)
                {
                    SegmentedLinkStock.Add(SegmentedLinks[i].Kind);
                    SegmentedLinks.RemoveAt(i);
                }
        }

        public bool TryInstallSegmentedWeapon(OrbitalModuleKind kind, int ringId, int mount, out OrbitalModuleState weapon, out string error)
        {
            weapon = null;
            if (!UsesSegmentedOrbit || !CanOccupy(ringId, mount, 0, out error))
            { error = "Нужна свободная точка установки."; return false; }
            weapon = SegmentedWeapons.FirstOrDefault(gun => gun.ModuleType == kind);
            if (weapon == null) return Rule(false, "Нет такого оружия в запасе.", out error);
            SegmentedWeapons.Remove(weapon);
            weapon.StableRingId = ringId; weapon.MountIndex = mount;
            Modules.Add(weapon);
            Revision++;
            error = null;
            return true;
        }

        public bool TryStowSegmentedWeapon(int id)
        {
            if (!UsesSegmentedOrbit || !CanCommit(out _)) return false;
            var weapon = FindModule(id);
            if (weapon == null) return false;
            Modules.Remove(weapon);
            weapon.StableRingId = 0; weapon.MountIndex = -1;
            SegmentedWeapons.Add(weapon);
            Revision++;
            return true;
        }

        private bool ValidateSegmentedInventory(HashSet<int> installedIds, out string error)
        {
            if (UsesSegmentedOrbit && (UnlockedSegmentedLevels < 1 || UnlockedSegmentedLevels > SegmentedLevels ||
                SegmentedWeaponLimit < 3 || SegmentedWeaponLimit > MaxSegmentedWeapons || Modules.Count > SegmentedWeaponLimit ||
                Modules.Any(gun => FindRing(gun.StableRingId)?.Order >= UnlockedSegmentedLevels ||
                    !IsSegmentedWeaponSlot(FindRing(gun.StableRingId).Order, gun.MountIndex))))
                return Rule(false, "invalid segmented progression", out error);
            foreach (var gun in SegmentedWeapons)
                if (gun == null || gun.StableModuleId <= 0 || gun.StableModuleId >= NextStableModuleId ||
                    !installedIds.Add(gun.StableModuleId) || gun.DamageLevel < 0 || !Enum.IsDefined(typeof(OrbitalModuleKind), gun.ModuleType))
                    return Rule(false, "invalid segmented weapon reserve", out error);
            var slots = new HashSet<(int, int)>();
            foreach (var link in SegmentedLinks)
                if (FindRing(link.RingId) == null || link.MountIndex < 0 || link.MountIndex >= SegmentedPositions ||
                    IsSegmentedWeaponSlot(FindRing(link.RingId).Order, link.MountIndex) ||
                    !slots.Add((link.RingId, link.MountIndex)) || !Enum.IsDefined(typeof(SegmentedLinkKind), link.Kind))
                    return Rule(false, "invalid segmented link", out error);
            return Rule(SegmentedLinkStock.All(kind => Enum.IsDefined(typeof(SegmentedLinkKind), kind)),
                "invalid segmented link reserve", out error);
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        public void DebugGrantSegmentedKit()
        {
            if (!UsesSegmentedOrbit) return;
            foreach (var kind in new[] { OrbitalModuleKind.Pistol, OrbitalModuleKind.Pistol, OrbitalModuleKind.Shotgun, OrbitalModuleKind.Sniper, OrbitalModuleKind.Saw })
                SegmentedWeapons.Add(new OrbitalModuleState { StableModuleId = NextStableModuleId++, ModuleType = kind });
            foreach (SegmentedLinkKind kind in Enum.GetValues(typeof(SegmentedLinkKind)))
            { SegmentedLinkStock.Add(kind); SegmentedLinkStock.Add(kind); }
            Revision++;
        }
#endif
    }
}
