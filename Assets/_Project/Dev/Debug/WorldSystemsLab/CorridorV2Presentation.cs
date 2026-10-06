#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections.Generic;
using UnityEngine;
// All geometry here is intentionally procedural, isolated to the Lab prototype.
public sealed class CorridorV2Presentation : System.IDisposable
{
    private readonly Transform root;
    private readonly CorridorV2Route route;
    private readonly Material material;
    private readonly List<LineRenderer> gates = new();
    private readonly List<TMPro.TextMeshPro> labels = new();
    private readonly TMPro.TMP_FontAsset font;
    private readonly LineRenderer exit, front, trail;
    private float pulse;
    public IReadOnlyList<Collider2D> Walls => walls;
    private readonly List<Collider2D> walls = new();
    public CorridorV2Presentation(Transform root, CorridorV2Route route, int playerLayers, TMPro.TMP_FontAsset font)
    {
        this.root = root; this.route = route; this.font = font;
        material = new Material(Shader.Find("Sprites/Default"));
        Line("Route floor", route.Vertices, route.HalfWidth * 2, new Color(.05f, .18f, .23f, .3f), -5);
        for (int segment = 1; segment < route.Vertices.Length; segment++)
        {
            Vector2 a = route.Vertices[segment - 1], b = route.Vertices[segment];
            Vector2 forward = (b - a).normalized;
            Vector2 side = new Vector2(-forward.y, forward.x) * route.HalfWidth;
            float length = Vector2.Distance(a, b);
            for (int sign = -1; sign <= 1; sign += 2)
                for (float d = 0; d < length; d += .5f)
                    Border(a + forward * d + side * sign, a + forward * Mathf.Min(d + .5f, length) + side * sign, playerLayers);
        }
        foreach (Vector2 vertex in route.Vertices)
            for (int i = 0; i < 64; i++)
            {
                float a = i * Mathf.PI * 2 / 64, b = (i + 1) * Mathf.PI * 2 / 64;
                Border(vertex + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * route.HalfWidth,
                    vertex + new Vector2(Mathf.Cos(b), Mathf.Sin(b)) * route.HalfWidth, playerLayers);
            }
        for (int i = 0; i < route.CheckpointCount; i++)
        {
            Vector2 p = route.Point(route.CheckpointDistance(i), out Vector2 forward);
            Vector2 side = new(-forward.y, forward.x);
            float size = route.HalfWidth * .65f;
            gates.Add(Line($"Checkpoint gate {i + 1}", new[] { p - side * size - forward * .8f,
                p - side * size, p - side * (size - .7f), p - side * size,
                p + side * size, p + side * (size - .7f), p + side * size, p + side * size - forward * .8f }, .22f, Color.gray, 12));
            labels.Add(Label($"CP {i + 1}", p + forward * 1.3f));
        }
        Vector2 end = route.Point(route.Length, out Vector2 direction), normal = new(-direction.y, direction.x);
        exit = Line("EXIT", new[] { end - normal * route.HalfWidth * .7f, end + normal * route.HalfWidth * .7f }, .5f, Color.red, 13);
        labels.Add(Label("EXIT LOCKED", end + direction * 1.5f));
        front = Line("Collapse front", new[] { route.Vertices[0], route.Vertices[0] }, .7f, Color.magenta, 20);
        front.gameObject.SetActive(false);
        trail = Line("Collapsed route", new[] { route.Vertices[0], route.Vertices[0] }, route.HalfWidth * 2, new Color(.8f, .03f, .3f, .4f), -3);
        trail.gameObject.SetActive(false);
        Refresh(false);
    }
    private LineRenderer Line(string name, Vector2[] points, float width, Color color, int order)
    {
        var item = new GameObject(name); item.transform.SetParent(root, false);
        var line = item.AddComponent<LineRenderer>();
        line.sharedMaterial = material; line.useWorldSpace = true; line.widthMultiplier = width;
        line.startColor = line.endColor = color; line.positionCount = points.Length;
        line.sortingOrder = order; line.sortingLayerName = "Midground";
        for (int i = 0; i < points.Length; i++) line.SetPosition(i, points[i]);
        return line;
    }
    private TMPro.TextMeshPro Label(string text, Vector2 position)
    {
        var item = new GameObject(text); item.transform.SetParent(root, false); item.transform.position = position;
        var label = item.AddComponent<TMPro.TextMeshPro>(); if (font != null) label.font = font;
        label.text = text; label.fontSize = 24; label.alignment = TMPro.TextAlignmentOptions.Center;
        label.rectTransform.sizeDelta = new Vector2(28, 5);
        label.transform.localScale = Vector3.one * .2f;
        label.GetComponent<MeshRenderer>().sortingOrder = 15; label.GetComponent<MeshRenderer>().sortingLayerName = "Midground";
        return label;
    }
    private void Border(Vector2 a, Vector2 b, int playerLayers)
    {
        Vector2 middle = (a + b) * .5f;
        route.Project(middle, out Vector2 nearest);
        // Interior seams of the union are omitted, including at bends.
        if (Vector2.Distance(middle, nearest) < route.HalfWidth - .025f) return;
        var line = Line("Player-only anomalous wall", new[] { a, b }, .14f, new Color(.3f, .6f, .85f), 8);
        var collider = line.gameObject.AddComponent<EdgeCollider2D>();
        collider.points = new[] { a - (Vector2)root.position, b - (Vector2)root.position };
        collider.edgeRadius = .08f;
        // Per-collider filtering, no global collision-matrix edits or enemy AI branches.
        collider.includeLayers = playerLayers;
        collider.excludeLayers = ~playerLayers;
        collider.layerOverridePriority = 100;
        walls.Add(collider);
    }
    public void CheckpointPulse() { pulse = .5f; Refresh(false); }
    public void Refresh(bool exitReady)
    {
        for (int i = 0; i < gates.Count; i++)
        {
            Color color = i < route.Completed ? new Color(.3f, .8f, .35f) : i == route.Completed ? Color.cyan : new Color(.2f, .3f, .36f);
            gates[i].startColor = gates[i].endColor = color; labels[i].color = color;
            labels[i].text = $"CP {i + 1} " + (i < route.Completed ? "DONE" : i == route.Completed ? "NEXT" : "");
        }
        exit.startColor = exit.endColor = exitReady ? Color.green : route.ExitOpen ? Color.yellow : new Color(.5f, .15f, .2f);
        labels[route.CheckpointCount].text = exitReady ? "EXIT OPEN" : route.ExitOpen ? "FINAL PUSH" : "EXIT LOCKED";
    }
    public void Tick(float delta, float collapse, bool exitReady, bool finishing)
    {
        pulse = Mathf.Max(0, pulse - delta);
        for (int i = 0; i < gates.Count; i++)
            gates[i].widthMultiplier = .22f + (i == route.Completed - 1 ? pulse * .8f : 0f) +
                (i == route.Completed ? .045f * (1f + Mathf.Sin(Time.time * 5f)) : 0f);
        exit.widthMultiplier = finishing ? 1.3f : exitReady ? .55f + .15f * Mathf.Sin(Time.time * 8f) : .35f;
        if (collapse <= 0) return;
        front.gameObject.SetActive(true); trail.gameObject.SetActive(true);
        Vector2 p = route.Point(collapse, out Vector2 forward), side = new(-forward.y, forward.x);
        front.positionCount = 9;
        for (int i = 0; i < 9; i++)
            front.SetPosition(i, p + side * Mathf.Lerp(-route.HalfWidth, route.HalfWidth, i / 8f) +
                forward * (i % 2 == 0 ? .16f : -.16f) * (1f + Mathf.Sin(Time.time * 12f)));
        front.widthMultiplier = .55f + .15f * (1f + Mathf.Sin(Time.time * 9f));
        var points = new List<Vector2> { route.Vertices[0] };
        float walked = 0;
        for (int i = 1; i < route.Vertices.Length; i++)
        {
            walked += Vector2.Distance(route.Vertices[i - 1], route.Vertices[i]);
            if (walked >= collapse) break;
            points.Add(route.Vertices[i]);
        }
        points.Add(p); trail.positionCount = points.Count;
        for (int i = 0; i < points.Count; i++) trail.SetPosition(i, points[i]);
    }
    public void Dispose()
    {
        foreach (Transform child in root) child.gameObject.SetActive(false);
        if (material != null) Object.Destroy(material);
    }
}
#endif
