using System.Collections.Generic;
using UnityEngine;

// One sector's visual-only scatter. No Update, gameplay RNG or colliders.
internal sealed class ProductionSectorProps
{
    readonly Transform parent;
    readonly GameplayAreaService area;
    readonly Vector2 start, exit;
    readonly Vector2[] sites;
    readonly float exitRadius;
    readonly List<Vector2> positions = new();
    readonly List<float> radii = new();
    Transform root;
    public PropScatterProfile Profile { get; }
    public int Seed { get; private set; }
    public int Count => positions.Count;

    public ProductionSectorProps(Transform parent, GameplayAreaService area, Vector2[] sites,
        Vector2 special, Vector2 exit, float exitRadius, PropScatterProfile profile)
    {
        this.parent = parent; this.area = area; this.exit = exit; this.exitRadius = exitRadius;
        this.sites = new Vector2[sites.Length + 1];
        sites.CopyTo(this.sites, 0); this.sites[sites.Length] = special;
        var player = GameObject.FindGameObjectWithTag("Player");
        start = player != null ? (Vector2)player.transform.position : (Vector2)area.PlayableArea.bounds.center;
        Profile = profile;
        Seed = (profile != null ? profile.seed : 42073) ^ Mathf.RoundToInt(exit.x * 32) ^
            (Mathf.RoundToInt(exit.y * 32) << 12);
    }
    public void Clear()
    {
        if (root != null)
        {
            root.gameObject.SetActive(false);
            // Destroy is deferred; name lookups must immediately resolve the replacement root.
            root.name = "Cleared props (pending destroy)";
            Object.Destroy(root.gameObject);
        }
        root = null; positions.Clear(); radii.Clear();
    }
    public void ChangeSeed(int delta) { Seed = unchecked(Seed + delta); Regenerate(); }
    public void Regenerate()
    {
        Clear();
        if (Profile == null || Profile.entries == null || area == null) return;
        var random = new System.Random(Seed);
        float Next(float a, float b) => Mathf.Lerp(Mathf.Min(a, b), Mathf.Max(a, b), (float)random.NextDouble());
        root = new GameObject("Sector visual props").transform;
        root.SetParent(parent, false);
        Physics2D.SyncTransforms();
        var protectedPoints = new List<Vector2>(sites);
        foreach (var item in Object.FindObjectsByType<Interactable>(FindObjectsSortMode.None))
            protectedPoints.Add(item.transform.position);
        foreach (var item in Object.FindObjectsByType<WorldBreakable>(FindObjectsSortMode.None))
            protectedPoints.Add(item.transform.position);
        foreach (var item in Object.FindObjectsByType<ResourceNode>(FindObjectsSortMode.None))
            protectedPoints.Add(item.transform.position);
        // Moving actors must not affect a deterministic layout.
        var walls = new List<Collider2D>();
        foreach (var c in Object.FindObjectsByType<Collider2D>(FindObjectsSortMode.None))
            if (c.enabled && !c.isTrigger && c != area.PlayableArea && c != area.SpawnArea &&
                (c.attachedRigidbody == null || c.attachedRigidbody.bodyType == RigidbodyType2D.Static) &&
                c.GetComponentInParent<WorldBreakable>() == null && c.GetComponentInParent<EnemyHealth>() == null &&
                c.GetComponentInParent<PlayerHealth>() == null)
                walls.Add(c);
        var entries = Profile.entries;
        var counts = new int[entries.Length];
        var sizes = new float[entries.Length];
        var usable = new bool[entries.Length];
        for (int i = 0; i < entries.Length; i++)
        {
            var e = entries[i];
            if (e == null || e.prefab == null || e.maxCount <= 0) continue;
            // Reject gameplay prefabs before Instantiate (Awake/OnEnable can change gameplay).
            if (e.prefab.GetComponentsInChildren<MonoBehaviour>(true).Length > 0 ||
                e.prefab.GetComponentsInChildren<Collider2D>(true).Length > 0 ||
                e.prefab.GetComponentsInChildren<Rigidbody2D>(true).Length > 0)
            { Debug.LogWarning($"[PropScatter] Skipped non-visual prefab: {e.prefab.name}"); continue; }
            foreach (var r in e.prefab.GetComponentsInChildren<SpriteRenderer>(true))
            {
                if (r.sprite == null) continue;
                var center = r.transform.TransformPoint(r.sprite.bounds.center) - e.prefab.transform.position;
                var extent = Vector3.Scale(r.sprite.bounds.extents, r.transform.lossyScale);
                sizes[i] = Mathf.Max(sizes[i], ((Vector2)center).magnitude + ((Vector2)extent).magnitude);
            }
            usable[i] = sizes[i] > 0;
        }
        var bounds = area.PlayableArea.bounds;
        bool Safe(Vector2 p, float radius)
        {
            if (!area.IsInsidePlayableArea(p, radius + Mathf.Max(0, Profile.edgeClearance)) ||
                Vector2.Distance(p, start) < radius + Mathf.Max(0, Profile.playerClearance) ||
                Vector2.Distance(p, exit) < radius + exitRadius + Mathf.Max(0, Profile.exitClearance)) return false;
            foreach (var q in protectedPoints)
                if (Vector2.Distance(p, q) < radius + Mathf.Max(0, Profile.interactableClearance)) return false;
            foreach (var wall in walls)
                if (Vector2.Distance(p, wall.ClosestPoint(p)) < radius + Mathf.Max(.05f, Profile.wallClearance)) return false;
            for (int j = 0; j < positions.Count; j++)
                if (Vector2.Distance(p, positions[j]) < Mathf.Max(Profile.minDistance, radius + radii[j] + .15f)) return false;
            return true;
        }
        bool Place(int kind)
        {
            var e = entries[kind];
            float scale = e.varyScale ? Mathf.Max(.05f, Next(e.scaleRange.x, e.scaleRange.y)) : 1;
            float radius = sizes[kind] * scale;
            Vector2 best = default;
            float bestScore = -1;
            // Best of several safe candidates fills gaps without a grid or dense clusters.
            for (int attempt = 0; attempt < 64; attempt++)
            {
                var p = new Vector2(Next(bounds.min.x, bounds.max.x), Next(bounds.min.y, bounds.max.y));
                p = new Vector2(Mathf.Round(p.x * 32) / 32, Mathf.Round(p.y * 32) / 32);
                if (!Safe(p, radius)) continue;
                float score = float.MaxValue;
                foreach (var q in positions) score = Mathf.Min(score, (q - p).sqrMagnitude);
                if (score > bestScore) { best = p; bestScore = score; }
                if (attempt >= 11) break;
            }
            if (bestScore < 0) return false;
            float rotation = e.varyRotation ? Next(e.rotationRange.x, e.rotationRange.y) : 0;
            var prop = Object.Instantiate(e.prefab, best, Quaternion.Euler(0, 0, rotation), root);
            prop.name = e.prefab.name;
            prop.transform.localScale = Vector3.Scale(e.prefab.transform.localScale,
                new Vector3(random.Next(2) == 0 ? -scale : scale, scale, 1));
            foreach (var r in prop.GetComponentsInChildren<SpriteRenderer>(true))
            { r.sortingLayerName = "Background"; r.sortingOrder = -105; }
            positions.Add(best); radii.Add(radius); counts[kind]++;
            return true;
        }
        int target = Mathf.Max(0, Profile.totalCount);
        for (int i = 0; i < entries.Length && Count < target; i++)
            if (usable[i])
                for (int j = 0; j < Mathf.Clamp(entries[i].minCount, 0, entries[i].maxCount) && Count < target; j++)
                    if (!Place(i)) { usable[i] = false; break; }
        for (int attempt = 0; Count < target && attempt < target * 4; attempt++)
        {
            float weight = 0;
            for (int i = 0; i < entries.Length; i++)
                if (usable[i] && counts[i] < entries[i].maxCount) weight += Mathf.Max(0, entries[i].weight);
            if (weight <= 0) break;
            float roll = (float)random.NextDouble() * weight;
            for (int i = 0; i < entries.Length; i++)
            {
                if (!usable[i] || counts[i] >= entries[i].maxCount || entries[i].weight <= 0) continue;
                roll -= entries[i].weight;
                if (roll > 0) continue;
                if (!Place(i)) usable[i] = false;
                break;
            }
        }
        Debug.Log($"[PropScatter] seed={Seed}, count={Count}/{target}. Safety takes priority over requested counts.");
    }
}
