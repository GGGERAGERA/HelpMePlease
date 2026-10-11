#if UNITY_EDITOR || DEVELOPMENT_BUILD
using UnityEngine;

namespace Subject42.DebugLabs.Constructor
{
    public enum PartKind { Beam, Fork, Blade, Electrode, Shield, Hook }
    public enum SegmentRole { Frame, Contact, Shield, Hook }

    public readonly struct PartSegment
    {
        public readonly Vector2 A, B;
        public readonly float Width;
        public readonly SegmentRole Role;
        public PartSegment(Vector2 a, Vector2 b, float width, SegmentRole role = SegmentRole.Frame)
        { A = a; B = b; Width = width; Role = role; }
    }

    public readonly struct Mount
    {
        public readonly Vector2 Position;
        public readonly float Angle;
        public Mount(Vector2 position, float angle = 0) { Position = position; Angle = angle; }
    }

    // Small, local data catalog. Geometry is shared by validation, previews and combat queries.
    public sealed class PartDefinition
    {
        public readonly string Name;
        public readonly Color Color;
        public readonly int Stock;
        public readonly PartSegment[] Segments;
        public readonly Mount[] Outputs;
        public readonly Vector2 Terminal;
        public readonly Vector2 MassCenter;
        public readonly Vector2 WorkingCenter;
        public readonly float DiskRadius;
        public PartDefinition(string name, Color color, int stock, PartSegment[] segments, Mount[] outputs, Vector2 terminal = default,
            Vector2 workingCenter = default, float diskRadius = 0)
        {
            Name = name; Color = color; Stock = stock; Segments = segments; Outputs = outputs; Terminal = terminal;
            WorkingCenter=workingCenter; DiskRadius=diskRadius;
            // Approximate each part's mass at its geometric center, weighted by segment area.
            float area=0; Vector2 weighted=Vector2.zero;
            foreach (var segment in segments)
            {
                float weight=Vector2.Distance(segment.A,segment.B)*segment.Width;
                area+=weight; weighted+=(segment.A+segment.B)*.5f*weight;
            }
            MassCenter=diskRadius>0 ? workingCenter : area>0 ? weighted/area : Vector2.zero;
        }
    }

    public static class OrbitalPartCatalog
    {
        public static readonly float[] Orientations = { 0, 45, -45, 90, -90 };
        public static readonly PartDefinition[] Parts =
        {
            new("Балка", new Color(.53f,.64f,.69f), 4,
                new[] { new PartSegment(Vector2.zero, new Vector2(1.1f,0), .11f) },
                new[] { new Mount(new Vector2(1.1f,0)) }),
            new("Разветвитель", new Color(1,.65f,.26f), 1,
                new[] { new PartSegment(Vector2.zero,new Vector2(.45f,0),.13f),
                    new PartSegment(new Vector2(.45f,0),new Vector2(1,.6f),.11f),
                    new PartSegment(new Vector2(.45f,0),new Vector2(1,-.6f),.11f) },
                new[] { new Mount(new Vector2(1,.6f),45), new Mount(new Vector2(1,-.6f),-45) }),
            // Keep the existing kind for serialized/editor compatibility. Only the disk is a weapon.
            new("Дисковая пила", new Color(.91f,.94f,1), 2,
                new[] { new PartSegment(Vector2.zero,new Vector2(.95f,0),.1f) },
                System.Array.Empty<Mount>(),workingCenter:new Vector2(.95f,0),diskRadius:.6f),
            new("Электрод", new Color(.3f,1,.88f), 2,
                new[] { new PartSegment(Vector2.zero,new Vector2(.6f,0),.2f) },
                new[] { new Mount(new Vector2(.6f,0)) },new Vector2(.55f,0)),
            new("Щит", new Color(.35f,.64f,1), 1,
                new[] { new PartSegment(Vector2.zero,new Vector2(.9f,0),.14f),
                    new PartSegment(new Vector2(.65f,-.8f),new Vector2(.65f,.8f),.22f,SegmentRole.Shield) },
                new[] { new Mount(new Vector2(.9f,0)) }),
            new("Крюк", new Color(1,.42f,.58f), 1,
                new[] { new PartSegment(Vector2.zero,new Vector2(.6f,0),.16f),
                    new PartSegment(new Vector2(.6f,0),new Vector2(.6f,.28f),.18f,SegmentRole.Hook),
                    new PartSegment(new Vector2(.6f,.28f),new Vector2(.35f,.28f),.18f,SegmentRole.Hook) },
                System.Array.Empty<Mount>(),new Vector2(.48f,.18f))
        };
        public static PartDefinition Get(PartKind kind) => Parts[(int)kind];
        public static Vector2 Rotate(Vector2 v, float angle)
        {
            float a = angle * Mathf.Deg2Rad;
            return new Vector2(v.x * Mathf.Cos(a) - v.y * Mathf.Sin(a), v.x * Mathf.Sin(a) + v.y * Mathf.Cos(a));
        }
    }
}
#endif
