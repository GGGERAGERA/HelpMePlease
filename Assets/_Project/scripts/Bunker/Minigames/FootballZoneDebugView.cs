using UnityEngine;

[DisallowMultipleComponent]
public sealed class FootballZoneDebugView : MonoBehaviour
{
    private static readonly Color BallColor = new(0.12f, 0.95f, 0.3f, 0.16f);
    private static readonly Color AnomalyColor = new(1f, 0.18f, 0.12f, 0.14f);
    private static readonly Color TargetColor = new(0.08f, 0.58f, 1f, 0.16f);

    private readonly GameObject[] visuals = new GameObject[3];
    private readonly Material[] materials = new Material[3];

    public void Synchronize(
        Collider2D ballZone,
        Collider2D anomalyZone,
        Collider2D targetZone,
        bool visible)
    {
        if (!visible)
        {
            for (int i = 0; i < visuals.Length; i++)
            {
                if (visuals[i] != null)
                    visuals[i].SetActive(false);
            }

            return;
        }

        EnsureVisuals();
        Collider2D[] zones = { ballZone, anomalyZone, targetZone };

        for (int i = 0; i < visuals.Length; i++)
        {
            bool active = visible && zones[i] != null;
            visuals[i].SetActive(active);
            if (!active)
                continue;

            Bounds bounds = zones[i].bounds;
            visuals[i].transform.SetPositionAndRotation(
                new Vector3(bounds.center.x, bounds.center.y, 0f),
                Quaternion.identity);
            visuals[i].transform.localScale = new Vector3(
                bounds.size.x,
                bounds.size.y,
                1f);
        }
    }

    private void EnsureVisuals()
    {
        if (visuals[0] != null)
            return;

        Mesh quad = Resources.GetBuiltinResource<Mesh>("Quad.fbx");
        Shader shader = Shader.Find("Sprites/Default");
        Color[] colors = { BallColor, AnomalyColor, TargetColor };
        string[] names = { "Ball Zone Debug", "Anomaly Zone Debug", "Target Zone Debug" };

        for (int i = 0; i < visuals.Length; i++)
        {
            GameObject visual = new(names[i]);
            visual.transform.SetParent(transform, false);
            MeshFilter filter = visual.AddComponent<MeshFilter>();
            filter.sharedMesh = quad;
            MeshRenderer renderer = visual.AddComponent<MeshRenderer>();
            materials[i] = new Material(shader)
            {
                name = $"Football {names[i]} Material",
                color = colors[i]
            };
            renderer.sharedMaterial = materials[i];
            renderer.sortingLayerName = "Background";
            renderer.sortingOrder = -1000;
            visuals[i] = visual;
        }
    }

    private void OnDestroy()
    {
        for (int i = 0; i < materials.Length; i++)
        {
            if (materials[i] != null)
                Destroy(materials[i]);
        }
    }
}
