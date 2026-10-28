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
        public int First, Second;
        public SegmentedLinkKind Kind;
        [NonSerialized] public float NextExtraShot;
        public bool Touches(int id) => First == id || Second == id;
        public int Other(int id) => First == id ? Second : First;
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

        public static float SegmentedMountAngle(int level, int position) =>
            90f + (level == 1 ? 15f : 0f) + position * 30f;

        public bool AreSegmentedNeighbours(int first, int second)
        {
            var a = FindModule(first); var b = FindModule(second);
            if (a == null || b == null || first == second) return false;
            var ar = FindRing(a.StableRingId); var br = FindRing(b.StableRingId);
            if (ar == null || br == null) return false;
            if (ar == br)
            {
                int gap = Mathf.Abs(a.MountIndex - b.MountIndex);
                return gap == 1 || gap == SegmentedPositions - 1;
            }
            return Mathf.Abs(ar.Order - br.Order) == 1 && Mathf.Abs(Mathf.DeltaAngle(
                SegmentedMountAngle(ar.Order, a.MountIndex), SegmentedMountAngle(br.Order, b.MountIndex))) <= 15.001f;
        }

        public IEnumerable<SegmentedLinkState> ActiveSegmentedLinks()
        {
            var degree = new Dictionary<int, int>();
            foreach (var link in SegmentedLinks)
            {
                if (!AreSegmentedNeighbours(link.First, link.Second)) continue;
                degree.TryGetValue(link.First, out int a); degree.TryGetValue(link.Second, out int b);
                if (a >= 2 || b >= 2) continue;
                degree[link.First] = a + 1; degree[link.Second] = b + 1;
                yield return link;
            }
        }

        public int SegmentedBonusCount(int id, SegmentedLinkKind kind) => UsesSegmentedOrbit
            ? ActiveSegmentedLinks().Count(link => link.Kind == kind && link.Touches(id)) : 0;

        public bool TryGrantSegmentedLink(SegmentedLinkKind kind)
        {
            if (!UsesSegmentedOrbit || !Enum.IsDefined(typeof(SegmentedLinkKind), kind) || !CanCommit(out _)) return false;
            SegmentedLinkStock.Add(kind);
            Revision++;
            return true;
        }

        public bool CanConnectSegmented(int first, int second, out string error)
        {
            if (!UsesSegmentedOrbit || !AreSegmentedNeighbours(first, second))
                return Rule(false, "Нужны пушки в соседних точках одного уровня или ближайших точках соседних уровней.", out error);
            if (SegmentedLinks.Any(link => link.Touches(first) && link.Touches(second)))
                return Rule(false, "Эта пара уже соединена.", out error);
            var active = ActiveSegmentedLinks().ToArray();
            return Rule(active.Count(link => link.Touches(first)) < 2 && active.Count(link => link.Touches(second)) < 2,
                "Максимум две активные связи на пушку.", out error);
        }

        public bool TryConnectSegmented(int first, int second, SegmentedLinkKind kind, out string error)
        {
            if (!CanCommit(out error) || !CanConnectSegmented(first, second, out error)) return false;
            if (!SegmentedLinkStock.Remove(kind)) return Rule(false, "Нет такого модуля в запасе.", out error);
            SegmentedLinks.Add(new SegmentedLinkState { First = Mathf.Min(first, second), Second = Mathf.Max(first, second), Kind = kind });
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

        private void ReturnSegmentedLinks(int id)
        {
            for (int i = SegmentedLinks.Count - 1; i >= 0; i--)
                if (SegmentedLinks[i].Touches(id))
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
            ReturnSegmentedLinks(id);
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
                Modules.Any(gun => FindRing(gun.StableRingId)?.Order >= UnlockedSegmentedLevels)))
                return Rule(false, "invalid segmented progression", out error);
            foreach (var gun in SegmentedWeapons)
                if (gun == null || gun.StableModuleId <= 0 || gun.StableModuleId >= NextStableModuleId ||
                    !installedIds.Add(gun.StableModuleId) || gun.DamageLevel < 0 || !Enum.IsDefined(typeof(OrbitalModuleKind), gun.ModuleType))
                    return Rule(false, "invalid segmented weapon reserve", out error);
            var pairs = new HashSet<(int, int)>();
            foreach (var link in SegmentedLinks)
                if (link.First >= link.Second || FindModule(link.First) == null || FindModule(link.Second) == null ||
                    !pairs.Add((link.First, link.Second)) || !Enum.IsDefined(typeof(SegmentedLinkKind), link.Kind))
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
