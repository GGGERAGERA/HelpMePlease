using System;
using System.Collections.Generic;
using UnityEngine;

namespace Subject42.Combat.Telekinetic
{
    public sealed partial class TelekineticBuild
    {
        [SerializeField] private List<TelekineticItem> reserve = new();
        public IReadOnlyList<TelekineticItem> Reserve => reserve;
        public bool ValidateInventory()
        {
            if (AvailableRadius<1 || AvailableRadius>Radius) return false;
            var ids=new HashSet<int>();
            foreach (var item in items)
                if (item==null || item.Id<=0 || item.Id>nextId || !ids.Add(item.Id) || !Enum.IsDefined(typeof(TelekineticPart),item.Kind) || item.Level<1) return false;
            foreach (var item in reserve)
                if (item==null || item.Id<=0 || item.Id>nextId || !ids.Add(item.Id) || !Enum.IsDefined(typeof(TelekineticPart),item.Kind) || item.Level<1) return false;
            return Validate();
        }
        public TelekineticItem Grant(TelekineticPart kind)
        {
            var item = new TelekineticItem(++nextId, kind, Vector2Int.zero);
            reserve.Add(item);
            return item;
        }
        internal void AwardRouteLevel(bool weapon)
        {
            TelekineticItem target = null;
            FindTarget(items);
            FindTarget(reserve);
            target ??= Grant(weapon ? TelekineticPart.Pistol : TelekineticPart.DMG);
            target.Level++;

            void FindTarget(List<TelekineticItem> inventory)
            {
                foreach (var item in inventory)
                    if ((weapon ? item.IsWeapon : item.CanMerge) && item.Level < 3 &&
                        (target == null || item.Level < target.Level)) target = item;
            }
        }

        public bool Install(TelekineticItem item, Vector2Int anchor, int turns = 0)
        {
            if (item == null || !reserve.Contains(item)) return false;
            if (!CanPlace(item.Kind, anchor, turns, out string reason)) return Reject(reason);
            item.Position = anchor; item.QuarterTurns = (turns % 4 + 4) % 4;
            reserve.Remove(item); items.Add(item); Disconnected.Clear();
            Message = "Предмет установлен из запаса.";
            return true;
        }
        public bool Stow(TelekineticItem item)
        {
            if (!Remove(item)) return false;
            reserve.Add(item);
            Message = "Предмет возвращён в запас.";
            return true;
        }
        internal bool RemoveLegacyFrames()
        {
            bool removedFromReserve = reserve.RemoveAll(item => item.Kind == TelekineticPart.Frame) > 0;
            if (items.RemoveAll(item => item.Kind == TelekineticPart.Frame) == 0) return removedFromReserve;

            StowDisconnectedItems();
            return true;
        }

        internal bool UpgradeSniperFootprints()
        {
            if (!items.Exists(item => item.Kind == TelekineticPart.Sniper)) return false;

            // Reserve every old cell before trying to add any sniper's new second cell.
            var occupied = new HashSet<Vector2Int>();
            foreach (var item in items)
            {
                int oldCellCount = item.Kind == TelekineticPart.Sniper ? 1 : item.CellCount;
                for (int i = 0; i < oldCellCount; i++) occupied.Add(item.Cell(i));
            }

            items.RemoveAll(item =>
            {
                if (item.Kind != TelekineticPart.Sniper) return false;
                var extra = item.Cell(1);
                if (InBounds(extra) && Mathf.Abs(extra.x) <= AvailableRadius &&
                    Mathf.Abs(extra.y) <= AvailableRadius && occupied.Add(extra)) return false;
                reserve.Add(item);
                return true;
            });

            StowDisconnectedItems();
            return true;
        }

        private void StowDisconnectedItems()
        {
            // Whole footprints are internally connected, so the anchor represents the entire item.
            if (!ValidateLayout(out _, out var disconnected))
                items.RemoveAll(item =>
                {
                    if (!disconnected.Contains(item.Position)) return false;
                    reserve.Add(item);
                    return true;
                });
            Disconnected.Clear();
        }

        public bool Expand()
        {
            if (AvailableRadius >= Radius) return false;
            AvailableRadius++; return true;
        }
        public bool CanSwap(TelekineticItem a, TelekineticItem b) => Swap(a, b, false);
        public bool TrySwap(TelekineticItem a, TelekineticItem b) => Swap(a, b, true);
        private bool Swap(TelekineticItem a, TelekineticItem b, bool commit)
        {
            if (a == null || b == null || a == b || !items.Contains(a) || !items.Contains(b) ||
                a.IsWeapon != b.IsWeapon || a.IsModule != b.IsModule ||
                (!a.IsWeapon && !a.IsModule && a.Kind != b.Kind) ||
                a.Kind == b.Kind && a.Level == b.Level && a.QuarterTurns == b.QuarterTurns) return false;
            var first = a.Position; var second = b.Position;
            a.Position = second; b.Position = first;
            bool valid = ValidateLayout(out _, out _);
            if (!valid || !commit) { a.Position = first; b.Position = second; }
            return valid;
        }
        public bool TryGlitch(System.Random random)
        {
            // Reservoir sampling across complete validated pairs. Zero candidates means no mutation.
            TelekineticItem first = null, second = null; int candidates = 0;
            for (int i = 0; i < items.Count; i++) for (int j = i + 1; j < items.Count; j++)
                if (CanSwap(items[i], items[j]) && random.Next(++candidates) == 0)
                { first = items[i]; second = items[j]; }
            return first != null && TrySwap(first, second);
        }
    }
    public interface ITelekineticStructureOwner
    {
        TelekineticBuild Build { get; }
        Material SpriteMaterial { get; }
        float CellSpacing { get; }
        bool Editing { get; }
    }
}
