using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

// Shared 16 PPU raster for world effects (two pixels of the 32 PPU sprite art).
// Geometry is built in world space, so rotating a marker never rotates its pixels.
public sealed class PixelEffectMesh
{
    public const float Ppu = 16f;
    private readonly GameObject root;
    private readonly Mesh mesh;
    private readonly MeshRenderer renderer;
    private readonly List<Vector3> vertices = new();
    private readonly List<Color32> colors = new();
    private readonly List<int> indices = new();
    private readonly HashSet<Vector2Int> occupied = new();
    public int CellCount => vertices.Count / 4;

    public PixelEffectMesh(Transform owner)
    {
        root = new GameObject("Pixel Effect Raster");
        root.layer = owner.gameObject.layer;
        root.transform.SetParent(owner, false);
        mesh = new Mesh { name = "Pixel Effect", indexFormat = IndexFormat.UInt32 };
        mesh.MarkDynamic();
        root.AddComponent<MeshFilter>().sharedMesh = mesh;
        renderer = root.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = Resources.Load<Material>("PixelWorldEffect");
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
    }

    public void Begin(Renderer source)
    {
        vertices.Clear();
        colors.Clear();
        indices.Clear();
        occupied.Clear();
        renderer.sortingLayerID = source.sortingLayerID;
        renderer.sortingOrder = source.sortingOrder;
    }

    public void Cell(int x, int y, float z, Color color, bool unique = true)
    {
        if (unique && !occupied.Add(new Vector2Int(x, y))) return;
        int first = vertices.Count;
        for (int i = 0; i < 4; i++)
        {
            var world = new Vector3((x + (i == 1 || i == 2 ? 1 : 0)) / Ppu,
                (y + (i >= 2 ? 1 : 0)) / Ppu, z);
            vertices.Add(root.transform.InverseTransformPoint(world));
            colors.Add(color);
        }
        indices.Add(first); indices.Add(first + 1); indices.Add(first + 2);
        indices.Add(first); indices.Add(first + 2); indices.Add(first + 3);
    }

    public void Segment(Vector3 a, Vector3 b, float width, Color color)
    {
        Vector2 start = (Vector2)a * Ppu, end = (Vector2)b * Ppu;
        float radius = Mathf.Max(0.5f, width * Ppu * 0.5f);
        int samples = Mathf.Max(1, Mathf.CeilToInt(Vector2.Distance(start, end) * 2));
        int reach = Mathf.CeilToInt(radius);
        Vector2 delta = end - start;
        float lengthSquared = delta.sqrMagnitude;
        // Visit only cells near the line, not its full diagonal bounding rectangle.
        for (int i = 0; i <= samples; i++)
        {
            Vector2 center = Vector2.Lerp(start, end, i / (float)samples);
            int cx = Mathf.FloorToInt(center.x), cy = Mathf.FloorToInt(center.y);
            for (int y = cy - reach; y <= cy + reach; y++)
            for (int x = cx - reach; x <= cx + reach; x++)
            {
                Vector2 pixel = new(x + 0.5f, y + 0.5f);
                float t = lengthSquared > 0.00001f
                    ? Mathf.Clamp01(Vector2.Dot(pixel - start, delta) / lengthSquared) : 0;
                if ((pixel - (start + delta * t)).sqrMagnitude <= radius * radius)
                    Cell(x, y, Mathf.Lerp(a.z, b.z, t), color);
            }
        }
    }

    public void End()
    {
        mesh.Clear();
        mesh.SetVertices(vertices);
        mesh.SetColors(colors);
        mesh.SetTriangles(indices, 0);
        renderer.enabled = vertices.Count > 0;
    }

    public void Hide() => renderer.enabled = false;
    public void Dispose()
    {
        Object.Destroy(mesh);
        Object.Destroy(root);
    }
}
