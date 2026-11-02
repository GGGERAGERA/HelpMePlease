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
        public bool Install(TelekineticItem item, Vector2Int anchor, int turns = 0)
        {
            if (item == null || !reserve.Contains(item) || !CanPlace(item.Kind, anchor, turns)) return false;
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
