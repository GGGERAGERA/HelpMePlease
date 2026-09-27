#if UNITY_EDITOR || DEVELOPMENT_BUILD
using UnityEngine;

// Fixed lab geometry only. Distances are along the route, never toward EXIT.
public sealed class CorridorV2Route
{
    public const float HalfWidth = 4f;
    public const float Length = 120f;
    public readonly Vector2[] Vertices;
    public int Completed { get; private set; }
    public bool ExitOpen => Completed == 3;

    public CorridorV2Route(bool bent, int quarterTurns)
    {
        Vertices = bent
            ? new[] { new Vector2(-30, -30), new Vector2(30, -30), new Vector2(30, 30) }
            : new[] { new Vector2(-60, 0), new Vector2(60, 0) };
        for (int i = 0; i < Vertices.Length; i++)
            for (int turn = 0; turn < ((quarterTurns % 4 + 4) % 4); turn++)
                Vertices[i] = new Vector2(-Vertices[i].y, Vertices[i].x);
    }

    public Vector2 Point(float distance, out Vector2 direction)
    {
        distance = Mathf.Clamp(distance, 0, Length);
        direction = Vector2.right;
        for (int i = 1; i < Vertices.Length; i++)
        {
            Vector2 delta = Vertices[i] - Vertices[i - 1];
            direction = delta.normalized;
            if (distance <= delta.magnitude || i == Vertices.Length - 1)
                return Vertices[i - 1] + direction * distance;
            distance -= delta.magnitude;
        }
        return Vertices[0];
    }

    public float Project(Vector2 point, out Vector2 nearest)
    {
        float best = float.PositiveInfinity, along = 0, walked = 0;
        nearest = Vertices[0];
        for (int i = 1; i < Vertices.Length; i++)
        {
            Vector2 delta = Vertices[i] - Vertices[i - 1];
            float t = Mathf.Clamp01(Vector2.Dot(point - Vertices[i - 1], delta) / delta.sqrMagnitude);
            Vector2 candidate = Vertices[i - 1] + delta * t;
            float squared = (point - candidate).sqrMagnitude;
            if (squared < best)
            {
                best = squared;
                nearest = candidate;
                along = walked + delta.magnitude * t;
            }
            walked += delta.magnitude;
        }
        return along;
    }

    public bool Contains(Vector2 point)
    {
        Project(point, out Vector2 nearest);
        return (point - nearest).sqrMagnitude <= HalfWidth * HalfWidth;
    }

    public bool StaysInside(Vector2 previous, Vector2 current)
    {
        if (!Contains(previous) || !Contains(current)) return false;
        Vector2 delta = current - previous;
        // Teleports/reset must not grant nodes. Sweeping catches fast movement
        // while rejecting a chord through the outside of the L-shaped route.
        if (delta.sqrMagnitude > 144f) return false;
        int steps = Mathf.Max(1, Mathf.CeilToInt(delta.magnitude / .5f));
        for (int i = 1; i < steps; i++)
            if (!Contains(Vector2.Lerp(previous, current, (float)i / steps))) return false;
        return true;
    }

    public bool TryAdvance(Vector2 previous, Vector2 current)
    {
        if (ExitOpen || !StaysInside(previous, current)) return false;
        Vector2 delta = current - previous;
        Vector2 node = Point(30f * (Completed + 1), out _);
        float t = delta.sqrMagnitude < .0001f ? 0 :
            Mathf.Clamp01(Vector2.Dot(node - previous, delta) / delta.sqrMagnitude);
        if (Vector2.Distance(previous + delta * t, node) > 3.5f) return false;
        Completed++;
        return true;
    }
}
#endif
