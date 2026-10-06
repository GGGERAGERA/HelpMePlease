#if UNITY_EDITOR || DEVELOPMENT_BUILD
using UnityEngine;
// Lab presets; progress is measured along the polyline, never towards EXIT.
public sealed class CorridorV2Route
{
    public float HalfWidth { get; }
    public float Length { get; }
    public int CheckpointCount { get; }
    public float SegmentLength { get; }
    public readonly Vector2[] Vertices;
    public int Completed { get; private set; }
    public bool ExitOpen => Completed == CheckpointCount;
    public CorridorV2Route(bool bent, int quarterTurns) : this(new CorridorV2Settings
        { routePreset = bent ? CorridorV2Preset.L : CorridorV2Preset.Straight }, quarterTurns) { }
    public CorridorV2Route(CorridorV2Settings settings, int quarterTurns = 0)
    {
        CheckpointCount = Mathf.Clamp(settings.checkpointCount, 1, 8);
        HalfWidth = Mathf.Clamp(settings.corridorWidth, 5f, Mathf.Max(5f, settings.segmentLength * .7f)) * .5f;
        SegmentLength = Mathf.Max(10f, settings.segmentLength);
        Length = SegmentLength * CheckpointCount + Mathf.Max(8f, settings.exitSegmentLength);
        Vertices = new Vector2[CheckpointCount + 2];
        for (int i = 1; i < Vertices.Length; i++)
        {
            Vector2 direction = Vector2.right;
            if (settings.routePreset == CorridorV2Preset.L && i > (CheckpointCount + 1) / 2) direction = Vector2.up;
            if (settings.routePreset == CorridorV2Preset.Zigzag && i % 2 == 0)
                direction = i % 4 == 0 ? Vector2.down : Vector2.up;
            Vertices[i] = Vertices[i - 1] + direction *
                (i <= CheckpointCount ? SegmentLength : Mathf.Max(8f, settings.exitSegmentLength));
        }
        Vector2 min = Vertices[0], max = min;
        foreach (var vertex in Vertices) { min = Vector2.Min(min, vertex); max = Vector2.Max(max, vertex); }
        Vector2 center = (min + max) * .5f;
        for (int i = 0; i < Vertices.Length; i++)
        {
            Vertices[i] -= center;
            for (int turn = 0; turn < ((quarterTurns % 4 + 4) % 4); turn++)
                Vertices[i] = new Vector2(-Vertices[i].y, Vertices[i].x);
        }
    }
    // Gates precede corners, so each crossing plane has an unambiguous direction.
    public float CheckpointDistance(int index) => SegmentLength * (index + 1) - 2f;
    public Vector2 Point(float distance, out Vector2 direction)
    {
        distance = Mathf.Clamp(distance, 0, Length); direction = Vector2.right;
        for (int i = 1; i < Vertices.Length; i++)
        {
            Vector2 delta = Vertices[i] - Vertices[i - 1]; direction = delta.normalized;
            if (distance <= delta.magnitude || i == Vertices.Length - 1)
                return Vertices[i - 1] + direction * Mathf.Min(distance, delta.magnitude);
            distance -= delta.magnitude;
        }
        return Vertices[0];
    }
    public float Project(Vector2 point, out Vector2 nearest)
    {
        float best = float.PositiveInfinity, along = 0, walked = 0; nearest = Vertices[0];
        for (int i = 1; i < Vertices.Length; i++)
        {
            Vector2 delta = Vertices[i] - Vertices[i - 1];
            float t = Mathf.Clamp01(Vector2.Dot(point - Vertices[i - 1], delta) / delta.sqrMagnitude);
            Vector2 candidate = Vertices[i - 1] + delta * t;
            float squared = (point - candidate).sqrMagnitude;
            if (squared < best) { best = squared; nearest = candidate; along = walked + delta.magnitude * t; }
            walked += delta.magnitude;
        }
        return along;
    }
    public bool Contains(Vector2 point)
    {
        Project(point, out Vector2 nearest); return (point - nearest).sqrMagnitude <= HalfWidth * HalfWidth;
    }
    public bool StaysInside(Vector2 previous, Vector2 current)
    {
        if (!Contains(previous) || !Contains(current)) return false;
        Vector2 delta = current - previous;
        if (delta.sqrMagnitude > 144f) return false;
        int steps = Mathf.Max(1, Mathf.CeilToInt(delta.magnitude / .25f));
        for (int i = 1; i < steps; i++)
            if (!Contains(Vector2.Lerp(previous, current, (float)i / steps))) return false;
        return true;
    }
    public bool TryAdvance(Vector2 previous, Vector2 current)
    {
        if (ExitOpen || !StaysInside(previous, current)) return false;
        Vector2 node = Point(CheckpointDistance(Completed), out Vector2 forward);
        float before = Vector2.Dot(previous - node, forward), after = Vector2.Dot(current - node, forward);
        if (before > 0 || after <= 0 || after <= before) return false;
        Vector2 crossing = Vector2.Lerp(previous, current, -before / (after - before));
        if (Vector2.Distance(crossing, node) > HalfWidth * .65f) return false;
        Completed++; return true;
    }
}
#endif
