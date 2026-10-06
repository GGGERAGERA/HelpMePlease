using System;
using System.Collections.Generic;
using UnityEngine;
// Progress follows the authored polyline. Rotation transforms the whole route.
public sealed class CorridorRoute
{
    public float HalfWidth { get; }
    public float GateHalfWidth => HalfWidth * .65f;
    public float Length { get; }
    public int CheckpointCount { get; }
    public float SegmentLength { get; }
    public int Seed { get; }
    private readonly Vector2[] vertices;
    public System.Collections.ObjectModel.ReadOnlyCollection<Vector2> Vertices { get; }
    private readonly float[] gates;
    public CorridorRoute(CorridorSettings settings, int quarterTurns = 0, Vector2? start = null)
    {
        CheckpointCount = Mathf.Clamp(settings.checkpointCount, 1, 8);
        SegmentLength = Mathf.Max(10f, settings.segmentLength);
        HalfWidth = Mathf.Clamp(settings.corridorWidth, 5f, SegmentLength * .7f) * .5f;
        Seed = settings.routeSeed != 0 ? settings.routeSeed : Guid.NewGuid().GetHashCode();
        float exitLength = Mathf.Max(8f, settings.exitSegmentLength);
        vertices = new Vector2[CheckpointCount + 2];
        Vertices = System.Array.AsReadOnly(vertices);
        gates = new float[CheckpointCount];
        float inset = 2f;
        CorridorRouteDefinition authored = null;
        if (settings.routeDefinitions != null)
            foreach (var definition in settings.routeDefinitions)
                if (definition != null && definition.preset == settings.routePreset &&
                    definition.checkpointEndAnchors != null && definition.checkpointEndAnchors.Length == CheckpointCount)
                { authored = definition; break; }
        if (settings.routePreset == CorridorRouteMode.Random)
            BuildRandom(exitLength);
        else if (authored != null)
        {
            inset = Mathf.Max(.1f, authored.checkpointInset);
            vertices[0] = authored.startAnchor * (SegmentLength / Mathf.Max(1f, authored.referenceSegmentLength));
            Vector2 previous = authored.startAnchor;
            for (int i = 1; i < vertices.Length; i++)
            {
                Vector2 anchor = i <= CheckpointCount ? authored.checkpointEndAnchors[i - 1] : authored.exitAnchor;
                Vector2 delta = anchor - previous;
                if (delta.sqrMagnitude < .01f) throw new ArgumentException("Corridor route anchors must be distinct.");
                float scale = i <= CheckpointCount ? SegmentLength / Mathf.Max(1f, authored.referenceSegmentLength) :
                    exitLength / Mathf.Max(1f, authored.referenceExitSegmentLength);
                vertices[i] = vertices[i - 1] + delta * scale;
                previous = anchor;
            }
        }
        else throw new ArgumentException("Missing authored Corridor preset definition.");
        float length = 0;
        for (int i = 1; i < vertices.Length; i++)
        {
            float segment = Vector2.Distance(vertices[i - 1], vertices[i]);
            length += segment;
            if (i <= CheckpointCount) gates[i - 1] = length - Mathf.Min(inset, segment * .25f);
        }
        Length = length;
        Vector2 min = vertices[0], max = min;
        foreach (var vertex in vertices) { min = Vector2.Min(min, vertex); max = Vector2.Max(max, vertex); }
        Vector2 center = (min + max) * .5f;
        for (int i = 0; i < vertices.Length; i++)
        {
            vertices[i] -= center;
            for (int turn = 0; turn < ((quarterTurns % 4 + 4) % 4); turn++)
                vertices[i] = new Vector2(-vertices[i].y, vertices[i].x);
        }
        if (start.HasValue)
        { Vector2 offset = start.Value - vertices[0]; for (int i = 0; i < vertices.Length; i++) vertices[i] += offset; }
    }
    private void BuildRandom(float exitLength)
    {
        var random = new System.Random(Seed);
        Vector2[] cardinal = { Vector2.right, Vector2.up, Vector2.left, Vector2.down };
        // Bounded backtracking rejects nearby nonadjacent corridors, including endpoint patches.
        int budget = 2048;
        bool Search(int index, int previous)
        {
            if (index == vertices.Length) return true;
            if (--budget < 0) return false;
            var choices = new List<int>();
            for (int d = 0; d < 4; d++) if (previous < 0 || d != (previous + 2) % 4) choices.Add(d);
            for (int i = choices.Count - 1; i > 0; i--)
            { int j = random.Next(i + 1); int temp = choices[i]; choices[i] = choices[j]; choices[j] = temp; }
            foreach (int direction in choices)
            {
                // Always introduce a turn at the second segment; variation is visible every run.
                if (index == 2 && direction == previous) continue;
                Vector2 a = vertices[index - 1];
                Vector2 b = a + cardinal[direction] * (index <= CheckpointCount ? SegmentLength : exitLength);
                bool safe = true;
                float clearance = HalfWidth * 2f + 1f;
                for (int earlier = 1; earlier < index - 1; earlier++)
                    if (BoxesNear(a, b, vertices[earlier - 1], vertices[earlier], clearance)) { safe = false; break; }
                if (!safe) continue;
                vertices[index] = b;
                if (Search(index + 1, direction)) return true;
            }
            return false;
        }
        if (!Search(1, -1))
        {
            // A monotonic stair is safe even at maximum width and never branches.
            for (int i = 1; i < vertices.Length; i++)
                vertices[i] = vertices[i - 1] + (i % 2 == 1 ? Vector2.right : Vector2.up) *
                    (i <= CheckpointCount ? SegmentLength : exitLength);
        }
    }
    private static bool BoxesNear(Vector2 a, Vector2 b, Vector2 c, Vector2 d, float clearance)
    {
        Vector2 minA = Vector2.Min(a, b), maxA = Vector2.Max(a, b);
        Vector2 minB = Vector2.Min(c, d), maxB = Vector2.Max(c, d);
        return minA.x <= maxB.x + clearance && maxA.x + clearance >= minB.x &&
            minA.y <= maxB.y + clearance && maxA.y + clearance >= minB.y;
    }
    public float CheckpointDistance(int index) => gates[index];
    public Vector2 Point(float distance, out Vector2 direction)
    {
        distance = Mathf.Clamp(distance, 0, Length); direction = Vector2.right;
        for (int i = 1; i < vertices.Length; i++)
        {
            Vector2 delta = vertices[i] - vertices[i - 1]; direction = delta.normalized;
            if (distance <= delta.magnitude || i == vertices.Length - 1)
                return vertices[i - 1] + direction * Mathf.Min(distance, delta.magnitude);
            distance -= delta.magnitude;
        }
        return vertices[0];
    }
    public float Project(Vector2 point, out Vector2 nearest)
    {
        float best = float.PositiveInfinity, along = 0, walked = 0; nearest = vertices[0];
        for (int i = 1; i < vertices.Length; i++)
        {
            Vector2 delta = vertices[i] - vertices[i - 1];
            float t = Mathf.Clamp01(Vector2.Dot(point - vertices[i - 1], delta) / delta.sqrMagnitude);
            Vector2 candidate = vertices[i - 1] + delta * t;
            float squared = (point - candidate).sqrMagnitude;
            if (squared < best) { best = squared; nearest = candidate; along = walked + delta.magnitude * t; }
            walked += delta.magnitude;
        }
        return along;
    }
    public bool Contains(Vector2 point)
    {
        for (int i = 1; i < vertices.Length; i++)
        {
            Vector2 delta = vertices[i] - vertices[i - 1], forward = delta.normalized;
            Vector2 offset = point - vertices[i - 1];
            float along = Vector2.Dot(offset, forward);
            float lateral = Mathf.Abs(Vector2.Dot(offset, new Vector2(-forward.y, forward.x)));
            if (along >= -HalfWidth && along <= delta.magnitude + HalfWidth && lateral <= HalfWidth) return true;
        }
        return false;
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
    public bool Crosses(float distance, Vector2 previous, Vector2 current)
    {
        if (!StaysInside(previous, current)) return false;
        Vector2 node = Point(distance, out Vector2 forward);
        float before = Vector2.Dot(previous - node, forward), after = Vector2.Dot(current - node, forward);
        if (before > 0 || after <= 0 || after <= before) return false;
        Vector2 crossing = Vector2.Lerp(previous, current, -before / (after - before));
        return Vector2.Distance(crossing, node) <= GateHalfWidth;
    }
    public Vector2 Sample(float distance) => Point(distance, out _);
    public float Project(Vector2 position) => Project(position, out _);
}
