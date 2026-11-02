#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Subject42.DebugLabs.Telekinetic
{
    public enum TelekineticPart { Frame, Pistol, Shotgun, Saw, DMG, FR, Double, RIC, Energy, Pierce, Splash, Chain, Crit, Slow, Shred, Knock, Echo }

    public readonly struct TelekineticCharge
    {
        public readonly TelekineticPart Kind;
        public readonly float Strength;
        public TelekineticCharge(TelekineticPart kind,float strength) { Kind=kind; Strength=strength; }
    }

    public sealed class TelekineticItem
    {
        public readonly int Id;
        public readonly TelekineticPart Kind;
        public Vector2Int Position { get; internal set; }
        public int QuarterTurns { get; internal set; }
        public int Level { get; internal set; } = 1;
        public float Remaining, ShotRemaining;
        internal float LastCooldown;
        public int Activations, Shots;
        // One pending charge per source: repeated pulses refresh rather than bank infinite shots.
        internal readonly Dictionary<int, float> Damage = new(), Rapid = new();
        internal readonly Dictionary<int, int> Extra = new(), Ricochet = new();
        internal readonly Dictionary<int, float> RapidUntil = new();
        internal readonly Dictionary<int,TelekineticCharge> Effects=new();
        public bool IsWeapon => Kind >= TelekineticPart.Pistol && Kind <= TelekineticPart.Saw;
        public bool IsModule => Kind >= TelekineticPart.DMG && Kind <= TelekineticPart.RIC || Kind>=TelekineticPart.Pierce && Kind<=TelekineticPart.Echo;
        public bool CanMerge => IsModule || Kind == TelekineticPart.Energy;
        private static readonly Vector2Int[] Single={new(0,0)}, Bar={new(0,0),new(0,1)},
            Square={new(0,0),new(1,0),new(0,1),new(1,1)}, Elbow={new(0,0),new(0,1),new(1,0)};
        public IReadOnlyList<Vector2Int> Footprint => Shape(Kind);
        public static IReadOnlyList<Vector2Int> Shape(TelekineticPart kind) => kind switch
        {
            TelekineticPart.Shotgun or TelekineticPart.DMG or TelekineticPart.Energy or TelekineticPart.Pierce or TelekineticPart.Splash or TelekineticPart.Shred => Bar,
            TelekineticPart.Saw or TelekineticPart.Double or TelekineticPart.Chain or TelekineticPart.Echo => Square,
            TelekineticPart.RIC => Elbow, _ => Single
        };
        public int CellCount => Footprint.Count;
        public static Vector2Int RotateOffset(Vector2Int p,int turns)
        {
            for (int i=0;i<((turns%4+4)%4);i++) p=new Vector2Int(p.y,-p.x);
            return p;
        }
        public Vector2Int Cell(int index) => Position+RotateOffset(Footprint[index],QuarterTurns);
        public bool Contains(Vector2Int p)
        { for (int i=0;i<CellCount;i++) if (Cell(i)==p) return true; return false; }
        public Vector2 Center
        {
            get { Vector2 sum=Vector2.zero; for (int i=0;i<CellCount;i++) sum+=Cell(i); return sum/CellCount; }
        }
        internal TelekineticItem(int id,TelekineticPart kind,Vector2Int position,int turns=0)
        { Id=id; Kind=kind; Position=position; QuarterTurns=(turns%4+4)%4; }
    }

    // The only source of placement truth. Failed transactions leave positions, levels and timers intact.
    public sealed class TelekineticBuild
    {
        public const int Radius = 4;
        public static readonly Vector2Int[] Sides = { Vector2Int.up, Vector2Int.right, Vector2Int.down, Vector2Int.left };
        private readonly List<TelekineticItem> items = new();
        public IReadOnlyList<TelekineticItem> Items => items;
        private int nextId;
        public readonly HashSet<Vector2Int> Disconnected = new();
        public string Message { get; private set; } = "Выберите деталь, затем зелёную клетку.";
        public TelekineticItem At(Vector2Int p)
        { foreach (var item in items) if (item.Contains(p)) return item; return null; }
        // Iterate logical items once. Multiple common sides never duplicate an effect source/recipient.
        public IEnumerable<TelekineticItem> Neighbours(TelekineticItem item) => items.Where(n=>n!=item && Adjacent(item,n));
        public static bool Adjacent(TelekineticItem a,TelekineticItem b)
        {
            if (a==null || b==null || a==b) return false;
            for (int i=0;i<a.CellCount;i++) for (int j=0;j<b.CellCount;j++) if (Adjacent(a.Cell(i),b.Cell(j))) return true;
            return false;
        }
        public static bool Adjacent(Vector2Int a, Vector2Int b) => Math.Abs(a.x-b.x)+Math.Abs(a.y-b.y)==1;
        public static bool InBounds(Vector2Int p) => Math.Abs(p.x)<=Radius && Math.Abs(p.y)<=Radius && p!=Vector2Int.zero;
        public bool CanPlace(TelekineticPart kind,Vector2Int p,int turns=0) => CanPlace(kind,p,turns,out _);
        private bool CanPlace(TelekineticPart kind,Vector2Int anchor,int turns,out string reason)
        {
            bool connected=false; var shape=TelekineticItem.Shape(kind);
            for (int i=0;i<shape.Count;i++)
            {
                var p=anchor+TelekineticItem.RotateOffset(shape[i],turns);
                if (!InBounds(p)) { reason="Вся форма должна быть внутри сетки и не занимать ядро."; return false; }
                if (At(p)!=null) { reason="Для целого предмета недостаточно свободного места."; return false; }
                foreach (var side in Sides) if (p+side==Vector2Int.zero || At(p+side)!=null) connected=true;
            }
            reason=connected ? "" : "Предмет должен касаться ядра или конструкции общей стороной."; return connected;
        }
        public bool Place(TelekineticPart kind,Vector2Int p,int turns=0)
        {
            Disconnected.Clear();
            if (!CanPlace(kind,p,turns,out string reason)) return Reject(reason);
            var item=new TelekineticItem(++nextId,kind,p,turns);
            items.Add(item); Message="Установлено: "+Label(kind)+$" ({item.CellCount} кл.)"; return true;
        }
        public bool Move(TelekineticItem item,Vector2Int p,int? turns=null) => item!=null && Transform(item,p,turns??item.QuarterTurns,true);
        public bool Rotate(TelekineticItem item) => item!=null && Transform(item,item.Position,item.QuarterTurns+1,true);
        public bool CanMove(TelekineticItem item,Vector2Int p,int turns) => Transform(item,p,turns,false);
        private bool Transform(TelekineticItem item,Vector2Int p,int turns,bool commit)
        {
            if (item==null || !items.Contains(item)) return false;
            TelekineticItem other=null;
            for (int i=0;i<item.CellCount;i++)
            {
                var target=At(p+TelekineticItem.RotateOffset(item.Footprint[i],turns));
                if (target==null || target==item) continue;
                // A drag can exchange one overlapping item even when grabbed away from its anchor.
                // In-place R never moves somebody else, and one drag cannot displace several items.
                if (p==item.Position || other!=null && other!=target)
                { if (commit) Reject("Для поворота нужно свободное место; обмен возможен только с одним предметом."); return false; }
                other=target;
            }
            var before=item.Position; int rotation=item.QuarterTurns;
            var otherBefore=other!=null ? other.Position : Vector2Int.zero;
            item.Position=p; item.QuarterTurns=(turns%4+4)%4;
            if (other!=null) other.Position=before;
            bool valid=ValidateLayout(out string reason,out var problem);
            if (!valid || !commit)
            { item.Position=before; item.QuarterTurns=rotation; if (other!=null) other.Position=otherBefore; }
            if (!commit) return valid;
            Disconnected.Clear(); Disconnected.UnionWith(problem);
            if (!valid) return Reject(reason+" Операция отменена целиком.");
            Message=other!=null ? "Предметы обменены целиком." : "Положение и форма обновлены."; return true;
        }
        public bool Remove(TelekineticItem item)
        {
            if (item==null || !items.Contains(item)) return false;
            int index=items.IndexOf(item); items.RemoveAt(index);
            bool valid=ValidateLayout(out string reason,out var problem);
            Disconnected.Clear(); Disconnected.UnionWith(problem);
            if (!valid) { items.Insert(index,item); return Reject(reason+" Удаление отменено."); }
            Message=$"Предмет удалён: освобождено {item.CellCount} кл."; return true;
        }
        public bool Merge(TelekineticItem keep,TelekineticItem consume)
        {
            if (keep==null || consume==null || keep==consume || !items.Contains(keep) || !items.Contains(consume) ||
                !keep.CanMerge || keep.Kind!=consume.Kind || keep.Level!=consume.Level || keep.Level>=3)
                return Reject("Merge: выберите второй модуль того же типа и уровня I/II.");
            if (!Remove(consume)) return false;
            keep.Level++; Message=Label(keep.Kind)+" → "+Roman(keep.Level)+$". Освобождено {consume.CellCount} кл."; return true;
        }
        public void Clear() { items.Clear(); Disconnected.Clear(); Message="Конструкция сброшена; ядро закреплено."; }
        private bool Reject(string message) { Message=message; return false; }
        public bool Validate()
        {
            bool valid=ValidateLayout(out _,out var problem); Disconnected.Clear(); Disconnected.UnionWith(problem); return valid;
        }
        private bool ValidateLayout(out string reason,out HashSet<Vector2Int> problem)
        {
            problem=new HashSet<Vector2Int>(); var occupied=new HashSet<Vector2Int>();
            foreach (var item in items) for (int i=0;i<item.CellCount;i++)
            { var p=item.Cell(i); if (!InBounds(p) || !occupied.Add(p)) problem.Add(p); }
            if (problem.Count>0) { reason="Формы пересекаются, занимают ядро или выходят за границы."; return false; }
            var visited=new HashSet<Vector2Int> { Vector2Int.zero }; var queue=new Queue<Vector2Int>();
            queue.Enqueue(Vector2Int.zero);
            while (queue.Count>0)
            {
                var p=queue.Dequeue();
                foreach (var side in Sides) { var n=p+side; if (occupied.Contains(n) && visited.Add(n)) queue.Enqueue(n); }
            }
            foreach (var p in occupied) if (!visited.Contains(p)) problem.Add(p);
            reason=problem.Count>0 ? "Выделенная ветвь теряет связь с ядром." : ""; return problem.Count==0;
        }
        public static string Label(TelekineticPart kind) => kind switch
        {
            TelekineticPart.Frame => "FRAME", TelekineticPart.Pistol => "PISTOL", TelekineticPart.Shotgun => "SHOTGUN",
            TelekineticPart.Saw => "SAW", TelekineticPart.Double => "2X", TelekineticPart.Energy => "ENERGY", _ => kind.ToString().ToUpperInvariant()
        };
        public static string Roman(int level) => level==1 ? "I" : level==2 ? "II" : "III";
    }

    [Serializable]
    public sealed class TelekineticModuleTuning
    {
        [Min(.1f)] public float DamageCooldown=4, RapidCooldown=5, DoubleCooldown=6, RicochetCooldown=5;
        [Min(0)] public float EnergyBonus=.25f;
        [Min(0)] public float LevelCooldownBonus=.2f;
        [Min(1)] public float DamageMultiplier=1.75f, RapidMultiplier=1.7f;
        [Min(.1f)] public float RapidDuration=2;
        [Min(0)] public float LevelEffectBonus=.5f;
        [Header("Extended modules / experimental values")]
        [Min(.1f)] public float PierceCooldown=5,SplashCooldown=6,ChainCooldown=6,CritCooldown=5,SlowCooldown=4,ShredCooldown=6,KnockCooldown=4,EchoCooldown=6;
        [Min(1)] public int PierceBodies=2;
        [Min(.1f)] public float SplashRadius=1.5f,ChainRadius=3.5f;
        [Min(0)] public float SplashDamageFraction=.35f,ChainDamageFraction=.65f;
        [Min(1)] public float CritMultiplier=2;
        [Range(.1f,1)] public float SlowMultiplier=.65f;
        [Min(.1f)] public float SlowSeconds=2;
        [Min(0)] public float KnockBonus=2.5f;
        [Min(.05f)] public float EchoDelay=.25f;
        public float Strength(TelekineticItem c) => 1+(c.Level-1)*LevelEffectBonus;
        public float Cooldown(TelekineticItem c,TelekineticBuild build)
        {
            float baseCd=c.Kind switch { TelekineticPart.DMG=>DamageCooldown, TelekineticPart.FR=>RapidCooldown,
                TelekineticPart.Double=>DoubleCooldown, TelekineticPart.RIC=>RicochetCooldown,
                TelekineticPart.Pierce=>PierceCooldown,TelekineticPart.Splash=>SplashCooldown,TelekineticPart.Chain=>ChainCooldown,
                TelekineticPart.Crit=>CritCooldown,TelekineticPart.Slow=>SlowCooldown,TelekineticPart.Shred=>ShredCooldown,
                TelekineticPart.Knock=>KnockCooldown,TelekineticPart.Echo=>EchoCooldown,_=>0 };
            float energy=build.Neighbours(c).Where(n=>n.Kind==TelekineticPart.Energy).Sum(n=>EnergyBonus*n.Level);
            return Mathf.Max(.1f,baseCd/(1+(c.Level-1)*LevelCooldownBonus)/(1+energy));
        }
    }
}
#endif
