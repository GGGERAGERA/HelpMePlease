using UnityEngine;

namespace Subject42.Combat.OrbitalStation
{
    public enum OrbitalPathType { Circle, FigureEight }

    // One immutable spatial evaluator per station. No progression or saved state lives here.
    public sealed class OrbitalPathGeometry
    {
        private const int Samples = 1024;
        public static readonly OrbitalPathGeometry Circle = new();
        public OrbitalPathType Type { get; }
        private readonly float width, height, spacing, depthRange;
        private readonly float[] parameters;
        private readonly Vector2[] points;

        private OrbitalPathGeometry() { Type = OrbitalPathType.Circle; }
        public OrbitalPathGeometry(OrbitalPresentationConfig config)
        {
            Type = OrbitalPathType.FigureEight;
            width = config.FigureEightWidth;
            height = config.FigureEightHeight;
            spacing = config.FigureEightSpacing;
            depthRange = config.FigureEightDepthRange;
            // Invert cumulative chord length once, then use constant-time interpolation at runtime.
            var lengths = new float[Samples + 1];
            Vector2 previous = Parametric(0f);
            for (int i = 1; i <= Samples; i++)
            {
                Vector2 next = Parametric(i * Mathf.PI * 2f / Samples);
                lengths[i] = lengths[i - 1] + Vector2.Distance(previous, next);
                previous = next;
            }
            parameters = new float[Samples + 1];
            points = new Vector2[Samples + 1];
            int segment = 1;
            for (int i = 0; i <= Samples; i++)
            {
                float distance = lengths[Samples] * i / Samples;
                while (segment < Samples && lengths[segment] < distance) segment++;
                float fraction = Mathf.InverseLerp(lengths[segment - 1], lengths[segment], distance);
                parameters[i] = (segment - 1 + fraction) * Mathf.PI * 2f / Samples;
                points[i] = Parametric(parameters[i]);
            }
            points[Samples] = points[0];
        }

        private Vector2 Parametric(float t) => new(width * Mathf.Cos(t), height * Mathf.Sin(2f * t));
        public float Scale(float radius) => Type == OrbitalPathType.Circle ? radius : radius * spacing;
        public Vector2 Extents(float radius) => Type == OrbitalPathType.Circle
            ? Vector2.one * radius : new Vector2(width, height) * Scale(radius);

        // Public phase is cyclic 0..1; persisted CurrentPhase remains degrees for existing runs.
        public Vector2 Position(float phase, float radius)
        {
            if (Type == OrbitalPathType.Circle)
            {
                float radians = phase * 360f * Mathf.Deg2Rad;
                return new Vector2(Mathf.Cos(radians) * radius, Mathf.Sin(radians) * radius);
            }
            float sample = Mathf.Repeat(phase, 1f) * Samples;
            int index = (int)sample;
            return Vector2.LerpUnclamped(points[index], points[index + 1], sample - index) * Scale(radius);
        }

        public Vector3 PositionDegrees(float degrees, float radius)
        {
            // Keep the original operation order (including floating point rounding) for Gera.
            if (Type == OrbitalPathType.Circle)
            {
                float radians = degrees * Mathf.Deg2Rad;
                return new Vector3(Mathf.Cos(radians) * radius, Mathf.Sin(radians) * radius, 0f);
            }
            return Position(degrees / 360f, radius);
        }

        public float Rotation(float degrees)
        {
            if (Type == OrbitalPathType.Circle) return degrees * Mathf.Deg2Rad;
            float sample = Mathf.Repeat(degrees / 360f, 1f) * Samples;
            int index = (int)sample;
            float t = Mathf.LerpUnclamped(parameters[index], parameters[index + 1], sample - index);
            // Tangent never vanishes at the crossing, unlike a radial direction from the player.
            return Mathf.Atan2(2f * height * Mathf.Cos(2f * t), -width * Mathf.Sin(t));
        }

        public float BackAmount(Vector2 position) => Type == OrbitalPathType.Circle ? 0f
            : Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(depthRange, depthRange * 2f, position.y));
        public bool IsBehind(Vector2 position) => position.y > depthRange * 1.5f;

        // Used for picking only. Segment projection avoids selecting an imaginary circular radius.
        public float Distance(Vector2 local, float radius)
        {
            if (Type == OrbitalPathType.Circle) return Mathf.Abs(local.magnitude - radius);
            float scale = Scale(radius);
            Vector2 point = local / scale;
            float best = float.PositiveInfinity;
            for (int i = 0; i < Samples; i += 4)
            {
                Vector2 start = points[i], delta = points[i + 4] - start;
                float along = Mathf.Clamp01(Vector2.Dot(point - start, delta) / delta.sqrMagnitude);
                best = Mathf.Min(best, (point - start - delta * along).sqrMagnitude);
            }
            return Mathf.Sqrt(best) * scale;
        }

        // Two continuous spatial halves reuse the authored front/back renderers and material.
        public Vector3 UpperPoint(float fraction) => new(-width * Mathf.Cos(fraction * Mathf.PI),
            height * Mathf.Abs(Mathf.Sin(fraction * Mathf.PI * 2f)), 0f);
    }
}
