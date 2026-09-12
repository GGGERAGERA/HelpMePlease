using System.Collections.Generic;
using UnityEngine;

// Decoration only. Called once after gameplay has finished placing its sector objects.
internal static class ProductionSectorProps
{
    public static void Place(Transform parent, GameplayAreaService area,
        Vector2[] sites, Vector2 specialSite, Vector2 exit, float exitRadius,
        IReadOnlyList<WorldBreakable> breakables)
    {
        var prefabs = Resources.LoadAll<GameObject>("SectorProps");
        if (prefabs.Length == 0) return;
        System.Array.Sort(prefabs, (a, b) => string.CompareOrdinal(a.name, b.name));
        var old = parent.Find("Sector visual props");
        if (old != null) { old.gameObject.SetActive(false); Object.Destroy(old.gameObject); }
        var root = new GameObject("Sector visual props").transform;
        root.SetParent(parent, false);
        var bounds = area.PlayableArea.bounds;
        // Do not consume Unity/WorldRandom: adding decoration must preserve gameplay seeds.
        int seed = 42073 ^ Mathf.RoundToInt(exit.x * 32) ^ (Mathf.RoundToInt(exit.y * 32) << 12);
        var random = new System.Random(seed);
        float Next(float min, float max) => Mathf.Lerp(min, max, (float)random.NextDouble());
        var player = GameObject.FindGameObjectWithTag("Player");
        Vector2 start = player != null ? player.transform.position : bounds.center;
        var anchors = new List<Vector2>();
        var positions = new List<Vector2>();
        int target = Mathf.RoundToInt(bounds.size.x * bounds.size.y / 42f);
        bool Clear(Vector2 p)
        {
            if (!area.IsInsidePlayableArea(p, 2f) || Vector2.Distance(p, start) < 3f ||
                Vector2.Distance(p, exit) < exitRadius + 3f ||
                Vector2.Distance(p, specialSite) < 3.5f) return false;
            foreach (var site in sites) if (Vector2.Distance(p, site) < 3.5f) return false;
            foreach (var b in breakables)
                if (b != null && Vector2.Distance(p, b.transform.position) < 2f) return false;
            foreach (var q in positions) if ((q - p).sqrMagnitude < 1.15f * 1.15f) return false;
            return true;
        }
        for (int attempt = 0; attempt < target * 40 && positions.Count < target; attempt++)
        {
            var anchor = new Vector2(Next(bounds.min.x, bounds.max.x), Next(bounds.min.y, bounds.max.y));
            if (!Clear(anchor) || anchors.Exists(p => (p - anchor).sqrMagnitude < 4.5f * 4.5f)) continue;
            anchors.Add(anchor);
            int count = random.Next(1, 4);
            var groupKinds = new HashSet<int>();
            float angle = Next(0, Mathf.PI * 2);
            for (int i = 0; i < count && positions.Count < target; i++)
            {
                float a = angle + i * 2.4f;
                var p = anchor + (i == 0 ? Vector2.zero : new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * Next(1.25f, 1.7f));
                p = new Vector2(Mathf.Round(p.x * 32) / 32, Mathf.Round(p.y * 32) / 32);
                if (!Clear(p)) continue;
                int kind;
                do { kind = random.Next(prefabs.Length); } while (groupKinds.Contains(kind) && groupKinds.Count < prefabs.Length);
                groupKinds.Add(kind);
                var source = prefabs[kind];
                var prop = Object.Instantiate(source, p, Quaternion.identity, root);
                prop.name = source.name;
                bool flat = source.name.Contains("Scrap") || source.name.Contains("Cable") || source.name.Contains("Pipe");
                prop.transform.rotation = Quaternion.Euler(0, 0, flat ? random.Next(4) * 90 : Next(-8, 8));
                float scale = Next(.92f, 1.06f);
                prop.transform.localScale = new Vector3(random.Next(2) == 0 ? -scale : scale, scale, 1);
                positions.Add(p);
            }
        }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log($"[SectorProps] {positions.Count} visual props in {anchors.Count} groups; no colliders, seed={seed}.");
#endif
    }
}
