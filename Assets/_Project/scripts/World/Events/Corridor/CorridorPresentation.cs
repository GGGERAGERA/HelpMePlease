using System.Collections.Generic;
using UnityEngine;
// Only instantiates authored modules and updates their transforms/states.
public sealed class CorridorPresentation : System.IDisposable
{
    private readonly CorridorRoute route;
    private readonly CorridorRuntimeState state;
    private bool disposed;
    private readonly List<GameObject> owned = new();
    private readonly List<Collider2D> walls = new();
    private readonly List<CorridorNodeView> gates = new();
    private readonly List<Transform> trails = new();
    private readonly CorridorNodeView exit, frontView;
    private readonly Transform front;
    private readonly CorridorHudView hud;
    public IReadOnlyList<Collider2D> Walls => walls;
    public CorridorPresentation(Transform root, CorridorRoute route, int playerLayers, CorridorKit kit, CorridorRuntimeState state)
    {
        this.route = route; this.state = state;
        float widthScale = route.HalfWidth / 4f;
        GameObject Place(GameObject prefab, Vector2 position, Vector2 forward, Vector3 scale)
        {
            var item = Object.Instantiate(prefab, root);
            item.transform.position = position;
            item.transform.rotation = Quaternion.Euler(0, 0, Mathf.Atan2(forward.y, forward.x) * Mathf.Rad2Deg);
            item.transform.localScale = Vector3.Scale(item.transform.localScale, scale);
            // Preserve the authored rail's pixel density while sizing its tiles to the module.
            foreach (var renderer in item.GetComponentsInChildren<SpriteRenderer>(true))
                if (renderer.drawMode == SpriteDrawMode.Tiled)
                {
                    Vector3 stretch = renderer.transform.lossyScale;
                    renderer.size = Vector2.Scale(renderer.size, new Vector2(Mathf.Abs(stretch.x), Mathf.Abs(stretch.y)));
                    var local = renderer.transform.localScale;
                    renderer.transform.localScale = new Vector3(local.x / Mathf.Abs(stretch.x), local.y / Mathf.Abs(stretch.y), local.z);
                }
            owned.Add(item); return item;
        }
        bool CornerAt(int index) => index > 0 && index < route.Vertices.Count - 1 &&
            Vector2.Dot((route.Vertices[index] - route.Vertices[index - 1]).normalized,
                (route.Vertices[index + 1] - route.Vertices[index]).normalized) < .5f;
        for (int i = 1; i < route.Vertices.Count; i++)
        {
            Vector2 a = route.Vertices[i - 1], b = route.Vertices[i], direction = (b - a).normalized;
            Vector2 start = a + direction * (CornerAt(i - 1) ? route.HalfWidth : i == 1 ? -route.HalfWidth : 0);
            Vector2 end = b - direction * (CornerAt(i) ? route.HalfWidth : i == route.Vertices.Count - 1 ? -route.HalfWidth : 0);
            Place(kit.straight, (start + end) * .5f, direction, new Vector3(Vector2.Distance(start, end), widthScale, 1));
            var trail = Place(kit.reclaimed, a, direction, new Vector3(1, widthScale, 1)).transform;
            trail.gameObject.SetActive(false); trails.Add(trail);
            if (CornerAt(i))
            {
                Vector2 next = (route.Vertices[i + 1] - b).normalized;
                float turn = Mathf.Sign(direction.x * next.y - direction.y * next.x);
                Place(kit.corner, b, direction, new Vector3(widthScale, widthScale * turn, 1));
            }
        }
        route.Point(0, out Vector2 startDirection); route.Point(route.Length, out Vector2 endDirection);
        Place(kit.cap, route.Vertices[0] - startDirection * route.HalfWidth, startDirection, new Vector3(1, widthScale, 1));
        Place(kit.cap, route.Vertices[route.Vertices.Count - 1] + endDirection * route.HalfWidth, endDirection, new Vector3(1, widthScale, 1));
        // Colliders belong to authored modules; per-instance overrides leave the global matrix intact.
        foreach (var item in owned)
            foreach (var collider in item.GetComponentsInChildren<Collider2D>(true))
            {
                collider.includeLayers = playerLayers; collider.excludeLayers = ~playerLayers;
                collider.layerOverridePriority = 100; walls.Add(collider);
            }
        for (int i = 0; i < route.CheckpointCount; i++)
        {
            Vector2 p = route.Point(route.CheckpointDistance(i), out Vector2 direction);
            gates.Add(Place(kit.gate, p, direction, new Vector3(1, widthScale, 1)).GetComponent<CorridorNodeView>());
        }
        exit = Place(kit.exit, route.Point(route.Length, out _), endDirection, new Vector3(1, widthScale, 1)).GetComponent<CorridorNodeView>();
        front = Place(kit.collapse, route.Vertices[0], startDirection, new Vector3(1, widthScale, 1)).transform;
        frontView = front.GetComponent<CorridorNodeView>(); frontView.SetState(1); front.gameObject.SetActive(false);
        var hudObject = Object.Instantiate(kit.hud, root); owned.Add(hudObject);
        hud = hudObject.GetComponent<CorridorHudView>();
        Refresh(false);
    }
    public void CheckpointPulse() { gates[state.Completed - 1].Pulse(); Refresh(false); }
    public void Refresh(bool exitReady)
    {
        for (int i = 0; i < gates.Count; i++) gates[i].SetState(i < state.Completed ? 2 : i == state.Completed ? 1 : 0);
        exit.SetState(exitReady ? 2 : state.Completed == route.CheckpointCount ? 1 : 0);
    }
    public void Tick(float delta, float collapse, bool exitReady, bool finishing, bool pressure = false)
    {
        Refresh(exitReady);
        foreach (var gate in gates) gate.Tick(delta);
        exit.Tick(delta, finishing);
        hud.Set(state.Completed, route.CheckpointCount, state.Completed == route.CheckpointCount, exitReady, pressure);
        if (collapse <= 0) return;
        front.gameObject.SetActive(true);
        front.position = route.Point(collapse, out Vector2 forward);
        front.rotation = Quaternion.Euler(0, 0, Mathf.Atan2(forward.y, forward.x) * Mathf.Rad2Deg);
        frontView.Tick(delta);
        float walked = 0;
        for (int i = 0; i < trails.Count; i++)
        {
            Vector2 a = route.Vertices[i], b = route.Vertices[i + 1];
            float segment = Vector2.Distance(a, b), filled = Mathf.Clamp(collapse - walked, 0, segment);
            trails[i].gameObject.SetActive(filled > .01f);
            if (filled > .01f)
            {
                Vector2 direction = (b - a).normalized;
                float extension = i == 0 ? route.HalfWidth : 0;
                trails[i].position = a + direction * ((filled - extension) * .5f);
                var scale = trails[i].localScale; scale.x = filled + extension; trails[i].localScale = scale;
            }
            walked += segment;
        }
    }
    public void Dispose()
    {
        if (disposed) return; disposed = true;
        hud.Hide();
        foreach (var wall in walls) if (wall != null) wall.enabled = false;
        foreach (var item in owned) if (item != null) { item.SetActive(false); Object.Destroy(item); }
        owned.Clear();
    }
}
