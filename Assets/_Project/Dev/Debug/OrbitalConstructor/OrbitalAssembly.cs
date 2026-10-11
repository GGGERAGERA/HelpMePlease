#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Subject42.DebugLabs.Constructor
{
    public sealed class AssemblyNode
    {
        public int Id, Parent, Port;
        public PartKind Kind;
        public Vector2 Position;
        public float Angle;
        public Vector2 Point(Vector2 point) => Position + OrbitalPartCatalog.Rotate(point, Angle);
    }

    public sealed class OrbitalAssembly
    {
        public const int MaxParts = 11;
        public const float MaxReach = 8f, MaxBridge = 6f;
        private readonly List<AssemblyNode> nodes = new();
        public IReadOnlyList<AssemblyNode> Nodes => nodes;
        private int nextId = 1;
        public int ElectrodeA { get; private set; } = -1;
        public int ElectrodeB { get; private set; } = -1;
        public AssemblyNode Find(int id) => nodes.Find(n => n.Id == id);
        public int Remaining(PartKind kind) => OrbitalPartCatalog.Get(kind).Stock - nodes.Count(n => n.Kind == kind);
        public float Reach => nodes.Select(NodeReach).DefaultIfEmpty(.8f).Max();
        private static float NodeReach(AssemblyNode node)
        {
            var def=OrbitalPartCatalog.Get(node.Kind);
            float reach=def.Segments.Select(s=>Mathf.Max(node.Point(s.A).magnitude,node.Point(s.B).magnitude)+s.Width*.5f).DefaultIfEmpty(0).Max();
            return Mathf.Max(reach,def.DiskRadius>0 ? node.Point(def.WorkingCenter).magnitude+def.DiskRadius : 0);
        }
        public void Clear() { nodes.Clear(); nextId = 1; ElectrodeA = ElectrodeB = -1; }
        public bool Occupied(int parent, int port) => nodes.Any(n => n.Parent == parent && n.Port == port);
        public IEnumerable<(int parent, int port, Mount mount)> FreeMounts()
        {
            if (!Occupied(0,0)) yield return (0,0,new Mount(new Vector2(.8f,0)));
            foreach (var node in nodes)
                for (int i = 0; i < OrbitalPartCatalog.Get(node.Kind).Outputs.Length; i++)
                    if (!Occupied(node.Id,i)) yield return (node.Id,i,GetMount(node.Id,i));
        }
        public Mount GetMount(int parent, int port)
        {
            if (parent == 0) return new Mount(new Vector2(.8f,0));
            var node = Find(parent);
            var mount = OrbitalPartCatalog.Get(node.Kind).Outputs[port];
            return new Mount(node.Point(mount.Position),node.Angle+mount.Angle);
        }
        public bool Preview(PartKind kind, int parent, int port, float turn, out AssemblyNode candidate, out string reason)
        {
            candidate = null;
            var owner = Find(parent);
            if ((parent == 0 && port != 0) || (parent != 0 && (owner == null || port < 0 || port >= OrbitalPartCatalog.Get(owner.Kind).Outputs.Length)))
            { reason = "Точка крепления отсутствует"; return false; }
            var mount = GetMount(parent,port);
            candidate = new AssemblyNode { Id = nextId, Kind = kind, Parent = parent, Port = port, Position = mount.Position, Angle = mount.Angle+turn };
            if (!OrbitalPartCatalog.Orientations.Any(a => Mathf.Approximately(a,turn)))
            { reason = "Недопустимый поворот"; return false; }
            if (Occupied(parent,port)) { reason = "Точка занята"; return false; }
            if (Remaining(kind) <= 0 || nodes.Count >= MaxParts) { reason = "Запас деталей исчерпан"; return false; }
            var definition=OrbitalPartCatalog.Get(kind);
            if (definition.DiskRadius>0)
            {
                Vector2 center=candidate.Point(definition.WorkingCenter);
                if (center.magnitude+definition.DiskRadius>MaxReach) { reason="Диск выходит за предел 8 м"; return false; }
                if (center.magnitude<.55f+definition.DiskRadius) { reason="Диск накладывается на игрока"; return false; }
                foreach (var existing in nodes)
                {
                    var def=OrbitalPartCatalog.Get(existing.Kind);
                    if (def.DiskRadius>0 && Vector2.Distance(center,existing.Point(def.WorkingCenter))<definition.DiskRadius+def.DiskRadius+.03f)
                    { reason="Диски накладываются"; return false; }
                    foreach (var other in def.Segments)
                    {
                        Vector2 a=existing.Point(other.A),b=existing.Point(other.B);
                        // A disk's input mount may touch its parent at the small mechanical joint.
                        if (Vector2.Distance(a,mount.Position)<.04f) a=Vector2.MoveTowards(a,b,.3f);
                        if (Vector2.Distance(b,mount.Position)<.04f) b=Vector2.MoveTowards(b,a,.3f);
                        if (SegmentDistance(center,a,b)<definition.DiskRadius+other.Width*.5f+.025f)
                        { reason="Диск накладывается на каркас"; return false; }
                    }
                }
            }
            foreach (var segment in OrbitalPartCatalog.Get(kind).Segments)
            {
                Vector2 a = candidate.Point(segment.A), b = candidate.Point(segment.B);
                float padding = segment.Width*.5f;
                if (Mathf.Max(a.magnitude,b.magnitude)+padding > MaxReach)
                { reason = "Вылет больше 8 м"; return false; }
                if (SegmentDistance(Vector2.zero,a,b) < .55f+padding)
                { reason = "Наложение на игрока"; return false; }
                foreach (var existing in nodes)
                {
                    var existingDef=OrbitalPartCatalog.Get(existing.Kind);
                    if (existingDef.DiskRadius>0 && SegmentDistance(existing.Point(existingDef.WorkingCenter),a,b)<existingDef.DiskRadius+padding+.025f)
                    { reason="Наложение на диск пилы"; return false; }
                    foreach (var other in OrbitalPartCatalog.Get(existing.Kind).Segments)
                    {
                        Vector2 c = existing.Point(other.A), d = existing.Point(other.B);
                        // Allow only the small common joint, never the remainder of an attached part.
                        Vector2 aa = TrimJoint(a,b,mount.Position), bb = TrimJoint(b,a,mount.Position);
                        Vector2 cc = TrimJoint(c,d,mount.Position), dd = TrimJoint(d,c,mount.Position);
                        if (SegmentsDistance(aa,bb,cc,dd) < (segment.Width+other.Width)*.5f+.025f)
                        { reason = "Наложение на " + OrbitalPartCatalog.Get(existing.Kind).Name + " #" + existing.Id; return false; }
                    }
                }
            }
            reason = "ЛКМ — установить";
            return true;
        }
        private static Vector2 TrimJoint(Vector2 end, Vector2 other, Vector2 joint) =>
            Vector2.Distance(end,joint) < .04f ? Vector2.MoveTowards(end,other,.26f) : end;
        public bool Add(PartKind kind, int parent, int port, float turn, out string reason)
        {
            if (!Preview(kind,parent,port,turn,out var node,out reason)) return false;
            nodes.Add(node); nextId++; return true;
        }
        public int RemoveBranch(int id)
        {
            if (Find(id) == null) return 0;
            var removed = new HashSet<int> { id };
            foreach (var node in nodes) if (removed.Contains(node.Parent)) removed.Add(node.Id);
            int count = nodes.RemoveAll(n => removed.Contains(n.Id));
            if (removed.Contains(ElectrodeA) || removed.Contains(ElectrodeB)) ElectrodeA = ElectrodeB = -1;
            return count;
        }
        public bool Pair(int first, int second, out string reason)
        {
            var a = Find(first); var b = Find(second);
            if (first == second || a?.Kind != PartKind.Electrode || b?.Kind != PartKind.Electrode)
            { reason = "Выберите два разных электрода"; return false; }
            if (Vector2.Distance(a.Point(OrbitalPartCatalog.Get(a.Kind).Terminal),b.Point(OrbitalPartCatalog.Get(b.Kind).Terminal)) > MaxBridge)
            { reason = "Мост длиннее 6 м"; return false; }
            ElectrodeA = first; ElectrodeB = second; reason = "Пара назначена"; return true;
        }
        public void Unpair() => ElectrodeA = ElectrodeB = -1;
        public bool Bridge(out Vector2 a, out Vector2 b)
        {
            a = b = default;
            var first = Find(ElectrodeA); var second = Find(ElectrodeB);
            if (first == null || second == null) return false;
            a = first.Point(OrbitalPartCatalog.Get(first.Kind).Terminal);
            b = second.Point(OrbitalPartCatalog.Get(second.Kind).Terminal);
            return Vector2.Distance(a,b) <= MaxBridge;
        }
        public void LoadPreset(int index)
        {
            Clear();
            int Put(PartKind kind,int parent,int port=0,float turn=0)
            {
                if (!Add(kind,parent,port,turn,out var error)) throw new InvalidOperationException($"Preset {index}: {kind}: {error}");
                return nodes[nodes.Count-1].Id;
            }
            if (index == 0)
            {
                int spine = Put(PartKind.Beam,0);
                spine = Put(PartKind.Beam,spine);
                spine = Put(PartKind.Beam,spine);
                spine = Put(PartKind.Beam,spine);
                int fork = Put(PartKind.Fork,spine);
                Put(PartKind.Blade,fork);
                Put(PartKind.Blade,fork,1);
            }
            else
            {
                int fork = Put(PartKind.Fork,0);
                int branch = Put(PartKind.Beam,fork,0,45);
                branch = Put(PartKind.Beam,branch,0,45);
                Put(PartKind.Blade,branch,0,90);
                if (index==1)
                {
                    // Fold the lower arm back toward the player, mirroring the upper arm.
                    branch=Put(PartKind.Beam,fork,1,-45);
                    branch=Put(PartKind.Beam,branch,0,-45);
                    Put(PartKind.Blade,branch,0,-90);
                }
                else { branch=Put(PartKind.Beam,fork,1); branch=Put(PartKind.Beam,branch); Put(PartKind.Blade,branch); }
            }
        }
        public static float SegmentDistance(Vector2 p,Vector2 a,Vector2 b)
        {
            Vector2 ab = b-a;
            return Vector2.Distance(p,a+ab*Mathf.Clamp01(Vector2.Dot(p-a,ab)/Mathf.Max(.000001f,ab.sqrMagnitude)));
        }
        public static float SegmentsDistance(Vector2 a,Vector2 b,Vector2 c,Vector2 d)
        {
            float Cross(Vector2 x,Vector2 y) => x.x*y.y-x.y*y.x;
            float divisor = Cross(b-a,d-c);
            if (Mathf.Abs(divisor) > .000001f)
            {
                float t = Cross(c-a,d-c)/divisor, u = Cross(c-a,b-a)/divisor;
                if (t >= 0 && t <= 1 && u >= 0 && u <= 1) return 0;
            }
            return Mathf.Min(SegmentDistance(a,c,d),SegmentDistance(b,c,d),SegmentDistance(c,a,b),SegmentDistance(d,a,b));
        }
    }
}
#endif
