using System.Collections.Generic;
using UnityEngine;

namespace Subject42.Combat.OrbitalStation
{
    // Shared closed polyline used by production and the drawing lab. The rendered segments and distance evaluator share one geometry.
    public sealed class CustomOrbitPath
    {
        private readonly Vector2[] points;
        private readonly float[] distances;
        public float TotalPathLength => distances[distances.Length - 1];
        public int PointCount => points.Length;
        public Vector2 GetPoint(int index) => points[index];
        public Vector2[] CapturePoints() => (Vector2[])points.Clone();

        // Restore already processed geometry exactly; never smooth it again at scene transitions.
        public static bool TryRestore(Vector2[] vertices, out CustomOrbitPath path)
        {
            path = null;
            if (!IsValidSavedPath(vertices)) return false;
            path = new CustomOrbitPath((Vector2[])vertices.Clone());
            return true;
        }

        public static bool IsValidSavedPath(Vector2[] vertices)
        {
            if (vertices == null || vertices.Length < 3 || vertices.Length > 4097) return false;
            float length = 0f;
            for (int i = 0; i < vertices.Length; i++)
            {
                if (!float.IsFinite(vertices[i].x) || !float.IsFinite(vertices[i].y) ||
                    (vertices[i] - vertices[(i + 1) % vertices.Length]).sqrMagnitude < .00000001f) return false;
                length += Vector2.Distance(vertices[i], vertices[(i + 1) % vertices.Length]);
            }
            return float.IsFinite(length) && length > .1f;
        }

        private CustomOrbitPath(Vector2[] vertices)
        {
            points = vertices;
            distances = new float[points.Length + 1];
            for (int i = 0; i < points.Length; i++)
                distances[i + 1] = distances[i] + Vector2.Distance(points[i], points[(i + 1) % points.Length]);
        }

        public Vector2 PositionAtDistance(float distance)
        {
            distance = Mathf.Repeat(distance, TotalPathLength);
            int lo = 0, hi = points.Length;
            while (lo + 1 < hi)
            {
                int mid = (lo + hi) / 2;
                if (distances[mid] <= distance) lo = mid;
                else hi = mid;
            }
            float fraction = (distance - distances[lo]) / (distances[lo + 1] - distances[lo]);
            return Vector2.LerpUnclamped(points[lo], points[(lo + 1) % points.Length], fraction);
        }

        public static bool TryBuild(IReadOnlyList<Vector2> input, float minDistance, float closeThreshold,
            float smoothing, out CustomOrbitPath path, out string reason)
        {
            path = null;
            reason = "Draw a larger closed loop.";
            if (input.Count < 4) return false;
            if (Vector2.Distance(input[0], input[input.Count - 1]) > closeThreshold)
            {
                reason = "End too far from START. Finish inside the start circle.";
                return false;
            }
            float tolerance = Mathf.Max(.002f, minDistance * .3f);
            var source = new List<Vector3>(input.Count + 1);
            for (int i = 0; i < input.Count; i++)
                if (source.Count == 0 || Vector2.Distance(source[source.Count - 1], input[i]) > tolerance)
                    source.Add(input[i]);
            if (source.Count < 3) return false;
            if (Vector2.Distance(source[source.Count - 1], source[0]) <= tolerance) source.RemoveAt(source.Count - 1);
            // Split at a distant vertex so each simplification has distinct endpoints.
            // This bounds the deviation from the original stroke instead of accumulating local errors.
            int split = 1;
            for (int i = 2; i < source.Count; i++)
                if ((source[i] - source[0]).sqrMagnitude > (source[split] - source[0]).sqrMagnitude) split = i;
            var first = new List<Vector3>();
            var second = new List<Vector3>();
            LineUtility.Simplify(source.GetRange(0, split + 1), tolerance, first);
            var remainder = source.GetRange(split, source.Count - split);
            remainder.Add(source[0]);
            LineUtility.Simplify(remainder, tolerance, second);
            first.RemoveAt(first.Count - 1);
            second.RemoveAt(second.Count - 1);
            source = first;
            source.AddRange(second);
            if (source.Count < 3) return false;
            var vertices = new Vector2[source.Count];
            for (int i = 0; i < vertices.Length; i++) vertices[i] = source[i];
            // Reject a click or an out-and-back straight line, but allow self-crossing figure eights.
            Vector2 axis = Vector2.zero;
            foreach (var vertex in vertices)
                if ((vertex - vertices[0]).sqrMagnitude > axis.sqrMagnitude) axis = vertex - vertices[0];
            float span = 0;
            foreach (var vertex in vertices)
            {
                Vector2 delta = vertex - vertices[0];
                span = Mathf.Max(span, Mathf.Abs(axis.x * delta.y - axis.y * delta.x) / Mathf.Max(.0001f, axis.magnitude));
            }
            if (span < Mathf.Max(.05f, minDistance)) return false;
            path = new CustomOrbitPath(vertices);
            if (smoothing > 0)
            {
                // Uniform sampling makes smoothing independent of hand speed / input point density.
                int count = Mathf.Clamp(Mathf.CeilToInt(path.TotalPathLength / Mathf.Max(.04f, minDistance)), 16, 2048);
                vertices = new Vector2[count];
                for (int i = 0; i < count; i++) vertices[i] = path.PositionAtDistance(path.TotalPathLength * i / count);
                for (int pass = 0; pass < 3; pass++)
                {
                    var filtered = new Vector2[count];
                    for (int i = 0; i < count; i++)
                        filtered[i] = Vector2.Lerp(vertices[i], (vertices[(i + count - 1) % count] + vertices[(i + 1) % count]) * .5f,
                            Mathf.Clamp01(smoothing) * .65f);
                    vertices = filtered;
                }
                path = new CustomOrbitPath(vertices);
            }
            reason = "Closed path ready. CONFIRM or Enter to orbit.";
            return true;
        }
    }
}
