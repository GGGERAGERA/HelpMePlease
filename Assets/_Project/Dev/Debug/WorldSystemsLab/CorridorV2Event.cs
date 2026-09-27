#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections.Generic;
using UnityEngine;

// Intentionally separate from the production moving EvacuationCorridorEvent.
public sealed class CorridorV2Event : WorldEvent
{
    public const float Duration = 25f;
    public CorridorV2Route Route { get; private set; }
    public float Remaining { get; private set; } = Duration;
    public string Result { get; private set; } = "Ready";
    public string Pattern { get; private set; } = "Centre / gap / sweep";
    public bool Outside { get; private set; }
    public int BoundaryHits { get; private set; }
    public int RocketsLaunched { get; private set; }
    public System.Action<string> Finished;

    private Transform player;
    private PlayerHealth health;
    private Material material;
    private readonly List<IWorldHazardAttack> attacks = new();
    private readonly List<LineRenderer> nodes = new();
    private readonly List<TextMesh> labels = new();
    private LineRenderer exit;
    private Vector2 previous;
    private float damageCooldown, patternTimer = 1f, sweepTimer;
    private int patternIndex, sweepIndex = -1;
    private Vector2 sweepCenter, sweepSide;
    private bool cleaned;

    public void Configure(Transform target, bool bent, int turns, RocketHazardDefinition rocket)
    {
        player = target;
        health = target.GetComponent<PlayerHealth>();
        Route = new CorridorV2Route(bent, turns);
        transform.position = Route.Vertices[0];
        previous = player.position;
        for (int i = 0; i < 3; i++) attacks.Add(rocket.CreateAttack(this));
        material = new Material(Shader.Find("Sprites/Default"));
        BuildVisuals();
    }

    protected override void OnEventStarted()
    {
        Result = "Running";
        previous = player.position;
    }

    private void Update()
    {
        if (!IsStarted || IsCompleted || Time.timeScale <= 0f) return;
        if (player == null || health == null || health.IsDead)
        {
            End(false, "Player down");
            return;
        }
        Remaining = Mathf.Max(0, Remaining - Time.deltaTime);
        if (Remaining <= 0) { End(false, "Time expired"); return; }

        Vector2 position = player.position;
        Outside = !Route.Contains(position);
        damageCooldown -= Time.deltaTime;
        bool crossedOutside = Route.Contains(previous) && !Route.StaysInside(previous, position);
        if ((Outside || crossedOutside) && damageCooldown <= 0)
        {
            Route.Project(position, out Vector2 nearest);
            if (health.TakeDamage(12f, (position - nearest).normalized))
            {
                BoundaryHits++;
                damageCooldown = 1f;
            }
        }
        if (Route.TryAdvance(previous, position)) RefreshNodes();
        previous = position;
        if (Route.ExitOpen && !Outside &&
            Vector2.Distance(position, Route.Point(CorridorV2Route.Length, out _)) < 2.5f)
        {
            End(true, $"EXIT reached in {Duration - Remaining:F1}s");
            return;
        }
        foreach (var attack in attacks)
            attack.Tick(Time.deltaTime, () => isActiveAndEnabled && !IsCompleted && health != null && !health.IsDead);
        UpdatePatterns();
    }

    private void UpdatePatterns()
    {
        if (sweepIndex >= 0)
        {
            sweepTimer -= Time.deltaTime;
            if (sweepTimer > 0) return;
            Launch(sweepIndex, sweepCenter + sweepSide * ((1 - sweepIndex) * 2.5f));
            sweepTimer = .4f;
            if (++sweepIndex == 3) sweepIndex = -1;
            return;
        }
        patternTimer -= Time.deltaTime;
        if (patternTimer > 0 || attacks.Exists(attack => attack.IsBusy)) return;
        float along = Route.Project(player.position, out _);
        // Aim ahead along the polyline, including around its corner.
        Vector2 center = Route.Point(Mathf.Min(along + 12f, 117f), out Vector2 forward);
        Vector2 side = new(-forward.y, forward.x);
        switch (patternIndex++ % 3)
        {
            case 0:
                Pattern = "CENTRE - use either side";
                Launch(0, center);
                break;
            case 1:
                Pattern = "PAIR - safe middle gap";
                Launch(0, center - side * 2.8f);
                Launch(1, center + side * 2.8f);
                break;
            default:
                Pattern = "SWEEP - left to right";
                sweepCenter = center; sweepSide = side;
                sweepIndex = 0; sweepTimer = 0;
                break;
        }
        patternTimer = 3.4f;
    }

    private void Launch(int index, Vector2 position)
    {
        if (attacks[index].TryStart(position)) RocketsLaunched++;
    }

    private void End(bool success, string message)
    {
        Result = $"{message} | CP {Route.Completed}/3 | boundary hits {BoundaryHits} | rockets {RocketsLaunched}";
        Finished?.Invoke(Result);
        if (success) CompleteEvent();
        else
        {
            FailEvent();
            Destroy(gameObject);
        }
    }

    protected override void CleanupEvent()
    {
        if (cleaned) return;
        cleaned = true;
        foreach (var attack in attacks) attack.Dispose();
        attacks.Clear();
        // FailEvent leaves the event instance alive, but temporary borders must end.
        foreach (Transform child in transform) child.gameObject.SetActive(false);
        if (material != null) Destroy(material);
    }

    private void OnDisable()
    {
        foreach (var attack in attacks) attack.Cancel();
    }

    private LineRenderer Line(string name, Vector2[] points, float width, Color color, int order)
    {
        var item = new GameObject(name);
        item.transform.SetParent(transform, false);
        var line = item.AddComponent<LineRenderer>();
        line.sharedMaterial = material;
        line.useWorldSpace = true;
        line.widthMultiplier = width;
        line.startColor = line.endColor = color;
        line.positionCount = points.Length;
        line.sortingOrder = order;
        line.sortingLayerName = "Midground";
        for (int i = 0; i < points.Length; i++) line.SetPosition(i, points[i]);
        return line;
    }

    private void Label(string text, Vector2 position)
    {
        var item = new GameObject(text);
        item.transform.SetParent(transform, false);
        item.transform.position = position;
        var label = item.AddComponent<TextMesh>();
        label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        label.GetComponent<MeshRenderer>().sharedMaterial = label.font.material;
        label.text = text;
        label.characterSize = .3f;
        label.fontSize = 36;
        label.anchor = TextAnchor.MiddleCenter;
        label.GetComponent<MeshRenderer>().sortingOrder = 15;
        label.GetComponent<MeshRenderer>().sortingLayerName = "Midground";
        labels.Add(label);
    }

    private void BuildVisuals()
    {
        var fill = Line("Route floor", Route.Vertices, 8f, new Color(.08f, .24f, .3f, .6f), -5);
        fill.numCornerVertices = fill.numCapVertices = 16;
        // Draw only the exposed boundary of the union of segment capsules.
        // No colliders: enemies can enter anywhere, player can emergency-exit.
        for (int segment = 1; segment < Route.Vertices.Length; segment++)
        {
            Vector2 a = Route.Vertices[segment - 1], b = Route.Vertices[segment];
            Vector2 forward = (b - a).normalized;
            Vector2 side = new Vector2(-forward.y, forward.x) * CorridorV2Route.HalfWidth;
            for (int sign = -1; sign <= 1; sign += 2)
                for (float d = 0; d < Vector2.Distance(a, b); d += 1f)
                    Border(a + forward * d + side * sign, a + forward * (d + 1f) + side * sign);
        }
        foreach (Vector2 vertex in Route.Vertices)
            for (int i = 0; i < 64; i++)
            {
                float a = i * Mathf.PI * 2 / 64, b = (i + 1) * Mathf.PI * 2 / 64;
                Border(vertex + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 4,
                    vertex + new Vector2(Mathf.Cos(b), Mathf.Sin(b)) * 4);
            }
        for (int i = 0; i < 3; i++)
        {
            Vector2 p = Route.Point(30f * (i + 1), out Vector2 forward);
            Vector2 side = new(-forward.y, forward.x);
            nodes.Add(Line($"CP {i + 1}", new[] { p - side * 3.5f, p + side * 3.5f }, .4f, Color.gray, 12));
            Label($"CP {i + 1}", p + Vector2.up * 1.3f);
        }
        Vector2 end = Route.Point(120, out Vector2 direction);
        Vector2 normal = new(-direction.y, direction.x);
        exit = Line("EXIT", new[] { end - normal * 3.5f, end + normal * 3.5f }, .7f, Color.red, 12);
        Label("EXIT LOCKED", end + Vector2.up * 1.8f);
        RefreshNodes();
    }

    private void Border(Vector2 a, Vector2 b)
    {
        Vector2 middle = (a + b) * .5f;
        Route.Project(middle, out Vector2 nearest);
        if (Vector2.Distance(middle, nearest) < 3.97f) return;
        Line("Anomalous boundary (12 damage)", new[] { a, b }, .16f, new Color(1f, .15f, .7f), 8);
    }

    private void RefreshNodes()
    {
        for (int i = 0; i < nodes.Count; i++)
        {
            Color color = i < Route.Completed ? Color.green : i == Route.Completed ? Color.cyan : Color.gray;
            nodes[i].startColor = nodes[i].endColor = color;
            labels[i].color = color;
            labels[i].text = $"CP {i + 1}: " + (i < Route.Completed ? "DONE" : i == Route.Completed ? "NEXT" : "INACTIVE");
        }
        exit.startColor = exit.endColor = Route.ExitOpen ? Color.green : Color.red;
        labels[3].text = Route.ExitOpen ? "EXIT OPEN" : "EXIT LOCKED";
    }
}
#endif
