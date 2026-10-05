using UnityEngine;

namespace Subject42.Combat.OrbitalStation
{
    // Runtime geometry only: authored body bounds, never selection halos or mount padding.
    internal static class OrbitalBodyContactDetector
    {
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        public static bool IntersectsCircle(SpriteRenderer body, Vector2 center, float radius)
        {
            if (body == null || !body.enabled || !body.gameObject.activeInHierarchy ||
                body.sprite == null || radius < 0 || !Finite(radius) || !Finite(center.x) || !Finite(center.y)) return false;
            Bounds bounds = body.localBounds;
            Vector2 a = body.transform.TransformPoint(new Vector3(bounds.min.x, bounds.min.y, bounds.center.z));
            Vector2 b = body.transform.TransformPoint(new Vector3(bounds.max.x, bounds.min.y, bounds.center.z));
            Vector2 c = body.transform.TransformPoint(new Vector3(bounds.max.x, bounds.max.y, bounds.center.z));
            Vector2 d = body.transform.TransformPoint(new Vector3(bounds.min.x, bounds.max.y, bounds.center.z));
            float area = Cross(b - a, d - a);
            if (!Finite(area) || Mathf.Abs(area) < .0000001f) return false;
            float ab = Cross(b - a, center - a), bc = Cross(c - b, center - b);
            float cd = Cross(d - c, center - c), da = Cross(a - d, center - d);
            if ((ab >= 0 && bc >= 0 && cd >= 0 && da >= 0) ||
                (ab <= 0 && bc <= 0 && cd <= 0 && da <= 0)) return true;
            float r2 = radius * radius;
            return DistanceSquared(center, a, b) <= r2 || DistanceSquared(center, b, c) <= r2 ||
                DistanceSquared(center, c, d) <= r2 || DistanceSquared(center, d, a) <= r2;
        }
        private static float Cross(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;
        private static float DistanceSquared(Vector2 point, Vector2 a, Vector2 b)
        {
            Vector2 edge = b - a;
            float t = edge.sqrMagnitude > 0 ? Mathf.Clamp01(Vector2.Dot(point - a, edge) / edge.sqrMagnitude) : 0;
            return (point - (a + edge * t)).sqrMagnitude;
        }
    }
}
