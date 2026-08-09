using UnityEngine;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
public sealed class SectorVisualDebugController : MonoBehaviour
{
    public enum SectorPreset
    {
        Calibration = 1,
        CorruptedTest = 2,
        Containment = 3,
        SystemFailure = 4,
        CoreFinalTest = 5
    }

    private GameplayAreaService gameplayArea;
    private Camera targetCamera;
    private GameObject visualRoot;
    private Material lineMaterial;
    private SectorPreset currentPreset = SectorPreset.Calibration;
    private bool gridVisible;
    private bool sectorLinesVisible;
    private bool boundariesVisible;
    private Color originalCameraColor;
    private bool cameraColorCaptured;

    public SectorPreset CurrentPreset => currentPreset;
    public bool GridVisible => gridVisible;
    public bool SectorLinesVisible => sectorLinesVisible;
    public bool BoundariesVisible => boundariesVisible;
    public string CurrentPresetName => GetPresetName(currentPreset);

    public void Configure(GameplayAreaService area, Camera camera)
    {
        gameplayArea = area;
        targetCamera = camera != null ? camera : Camera.main;
        if (targetCamera != null)
        {
            originalCameraColor = targetCamera.backgroundColor;
            cameraColorCaptured = true;
        }
        EnsureMaterial();
        Rebuild();
    }

    public void ApplyPreset(SectorPreset preset)
    {
        currentPreset = preset;
        Rebuild();
    }

    public void SetGridVisible(bool visible)
    {
        gridVisible = visible;
        Rebuild();
    }

    public void SetSectorLinesVisible(bool visible)
    {
        sectorLinesVisible = visible;
        Rebuild();
    }

    public void SetBoundariesVisible(bool visible)
    {
        boundariesVisible = visible;
        Rebuild();
    }

    public static string GetPresetName(SectorPreset preset) => preset switch
    {
        SectorPreset.CorruptedTest => "Sector 2 - Corrupted Test",
        SectorPreset.Containment => "Sector 3 - Containment",
        SectorPreset.SystemFailure => "Sector 4 - System Failure",
        SectorPreset.CoreFinalTest => "Sector 5 - Core / Final Test",
        _ => "Sector 1 - Calibration"
    };

    private void Rebuild()
    {
        if (gameplayArea == null || gameplayArea.PlayableArea == null)
            return;

        if (visualRoot != null)
            Destroy(visualRoot);

        bool anyDecoration = gridVisible || sectorLinesVisible || boundariesVisible;
        if (!anyDecoration)
        {
            if (targetCamera != null && cameraColorCaptured)
                targetCamera.backgroundColor = originalCameraColor;
            visualRoot = null;
            return;
        }

        visualRoot = new GameObject("Sandbox Sector Visual Preset");
        visualRoot.transform.SetParent(transform, false);
        Bounds bounds = gameplayArea.PlayableArea.bounds;

        if (targetCamera == null)
            targetCamera = Camera.main;
        if (targetCamera != null)
            targetCamera.backgroundColor = BackgroundFor(currentPreset);

        if (gridVisible)
            BuildGrid(bounds);
        if (sectorLinesVisible)
            BuildSectorIdentity(bounds);
        if (boundariesVisible)
            BuildBoundary(bounds);
    }

    private void BuildGrid(Bounds bounds)
    {
        float spacing = currentPreset == SectorPreset.CoreFinalTest ? 10f : 5f;
        Color cyan = currentPreset switch
        {
            SectorPreset.Containment => new Color(0.12f, 0.46f, 0.52f, 0.28f),
            SectorPreset.SystemFailure => new Color(0.12f, 0.7f, 0.78f, 0.3f),
            _ => new Color(0.1f, 0.58f, 0.7f, 0.32f)
        };

        int index = 0;
        for (float x = Mathf.Ceil(bounds.min.x / spacing) * spacing;
            x <= bounds.max.x; x += spacing, index++)
        {
            if (ShouldSkipGridLine(index))
                continue;
            float offset = GridOffset(index);
            CreateLine("Grid V", new[]
            {
                new Vector3(x + offset, bounds.min.y),
                new Vector3(x + offset, bounds.max.y)
            }, GridColor(index, cyan), 0.018f, false, -40);
        }

        index = 0;
        for (float y = Mathf.Ceil(bounds.min.y / spacing) * spacing;
            y <= bounds.max.y; y += spacing, index++)
        {
            if (ShouldSkipGridLine(index + 2))
                continue;
            float offset = GridOffset(index + 3);
            CreateLine("Grid H", new[]
            {
                new Vector3(bounds.min.x, y + offset),
                new Vector3(bounds.max.x, y + offset)
            }, GridColor(index + 1, cyan), 0.018f, false, -40);
        }
    }

    private bool ShouldSkipGridLine(int index) =>
        (currentPreset == SectorPreset.CorruptedTest && index % 4 == 1) ||
        (currentPreset == SectorPreset.SystemFailure && index % 3 == 1);

    private float GridOffset(int index)
    {
        if (currentPreset == SectorPreset.CorruptedTest && index % 3 == 0)
            return index % 2 == 0 ? 0.55f : -0.45f;
        if (currentPreset == SectorPreset.SystemFailure && index % 4 == 0)
            return index % 2 == 0 ? 0.8f : -0.65f;
        return 0f;
    }

    private Color GridColor(int index, Color fallback)
    {
        if (currentPreset == SectorPreset.CorruptedTest && index % 5 == 0)
            return new Color(0.9f, 0.12f, 0.78f, 0.38f);
        if (currentPreset == SectorPreset.SystemFailure && index % 4 == 0)
            return new Color(0.95f, 0.12f, 0.3f, 0.38f);
        return fallback;
    }

    private void BuildSectorIdentity(Bounds bounds)
    {
        Vector2 center = bounds.center;
        switch (currentPreset)
        {
            case SectorPreset.Calibration:
                CreateCrossMarks(bounds, new Color(0.2f, 0.86f, 0.94f, 0.52f));
                CreateLine("Center Axes", new[]
                {
                    new Vector3(bounds.min.x, center.y),
                    new Vector3(bounds.max.x, center.y)
                }, new Color(0.16f, 0.72f, 0.82f, 0.3f), 0.035f, false, -36);
                break;

            case SectorPreset.CorruptedTest:
                CreateBrokenRect(center + new Vector2(-18f, 8f), new Vector2(11f, 7f),
                    new Color(0.9f, 0.12f, 0.75f, 0.58f), 0f);
                CreateBrokenRect(center + new Vector2(15f, -9f), new Vector2(14f, 6f),
                    new Color(0.42f, 0.22f, 0.92f, 0.5f), 0f);
                CreateBrokenRect(center + new Vector2(5f, 12f), new Vector2(8f, 5f),
                    new Color(0.15f, 0.75f, 0.85f, 0.42f), 0f);
                break;

            case SectorPreset.Containment:
                CreateRect(center + new Vector2(-17f, 7f), new Vector2(20f, 13f),
                    new Color(1f, 0.46f, 0.08f, 0.55f), 0f);
                CreateRect(center + new Vector2(16f, -8f), new Vector2(22f, 14f),
                    new Color(0.92f, 0.16f, 0.12f, 0.48f), 0f);
                CreateWarningTicks(bounds);
                break;

            case SectorPreset.SystemFailure:
                CreateBrokenRect(center + new Vector2(-17f, 8f), new Vector2(17f, 10f),
                    new Color(0.12f, 0.8f, 0.9f, 0.52f), 12f);
                CreateBrokenRect(center + new Vector2(14f, 5f), new Vector2(14f, 8f),
                    new Color(0.94f, 0.1f, 0.72f, 0.58f), -17f);
                CreateBrokenRect(center + new Vector2(7f, -12f), new Vector2(19f, 7f),
                    new Color(0.96f, 0.12f, 0.2f, 0.48f), 8f);
                break;

            case SectorPreset.CoreFinalTest:
                CreateCircle("Core Ring Outer", center, 17f,
                    new Color(0.1f, 0.66f, 0.78f, 0.34f), 0.055f);
                CreateCircle("Core Ring Middle", center, 10.5f,
                    new Color(0.85f, 0.12f, 0.18f, 0.42f), 0.045f);
                CreateCircle("Core Ring Inner", center, 5f,
                    new Color(0.12f, 0.78f, 0.88f, 0.5f), 0.07f);
                CreateLine("Core Axis", new[]
                {
                    new Vector3(bounds.min.x, center.y),
                    new Vector3(bounds.max.x, center.y)
                }, new Color(0.8f, 0.1f, 0.16f, 0.28f), 0.035f, false, -36);
                break;
        }
    }

    private void BuildBoundary(Bounds bounds)
    {
        Color color = currentPreset switch
        {
            SectorPreset.Containment => new Color(1f, 0.35f, 0.08f, 0.82f),
            SectorPreset.SystemFailure => new Color(0.9f, 0.12f, 0.35f, 0.76f),
            SectorPreset.CoreFinalTest => new Color(0.68f, 0.12f, 0.2f, 0.68f),
            _ => new Color(0.2f, 0.72f, 0.84f, 0.72f)
        };
        CreateLine("Debug Boundary", new[]
        {
            new Vector3(bounds.min.x, bounds.min.y),
            new Vector3(bounds.max.x, bounds.min.y),
            new Vector3(bounds.max.x, bounds.max.y),
            new Vector3(bounds.min.x, bounds.max.y)
        }, color, 0.07f, true, -35);
    }

    private void CreateCrossMarks(Bounds bounds, Color color)
    {
        Vector2[] marks =
        {
            new(bounds.min.x + 5f, bounds.min.y + 5f),
            new(bounds.max.x - 5f, bounds.min.y + 5f),
            new(bounds.min.x + 5f, bounds.max.y - 5f),
            new(bounds.max.x - 5f, bounds.max.y - 5f)
        };
        for (int i = 0; i < marks.Length; i++)
        {
            Vector2 p = marks[i];
            CreateLine("Calibration Mark", new[]
            {
                (Vector3)(p + Vector2.left * 1.2f),
                (Vector3)(p + Vector2.right * 1.2f)
            }, color, 0.045f, false, -34);
            CreateLine("Calibration Mark", new[]
            {
                (Vector3)(p + Vector2.down * 1.2f),
                (Vector3)(p + Vector2.up * 1.2f)
            }, color, 0.045f, false, -34);
        }
    }

    private void CreateWarningTicks(Bounds bounds)
    {
        Color orange = new(1f, 0.5f, 0.08f, 0.65f);
        for (int i = 0; i < 8; i++)
        {
            float x = Mathf.Lerp(bounds.min.x + 3f, bounds.max.x - 3f, i / 7f);
            CreateLine("Warning Tick", new[]
            {
                new Vector3(x - 0.8f, bounds.min.y + 1f),
                new Vector3(x + 0.8f, bounds.min.y + 2f)
            }, orange, 0.09f, false, -34);
        }
    }

    private void CreateRect(Vector2 center, Vector2 size, Color color, float angle)
    {
        Vector2 half = size * 0.5f;
        Vector3[] points =
        {
            center + new Vector2(-half.x, -half.y),
            center + new Vector2(half.x, -half.y),
            center + new Vector2(half.x, half.y),
            center + new Vector2(-half.x, half.y)
        };
        Rotate(points, center, angle);
        CreateLine("Containment Outline", points, color, 0.075f, true, -34);
    }

    private void CreateBrokenRect(Vector2 center, Vector2 size, Color color, float angle)
    {
        Vector2 half = size * 0.5f;
        Vector3[] a =
        {
            center + new Vector2(-half.x, half.y * 0.2f),
            center + new Vector2(-half.x, half.y),
            center + new Vector2(half.x * 0.35f, half.y)
        };
        Vector3[] b =
        {
            center + new Vector2(half.x, half.y * 0.4f),
            center + new Vector2(half.x, -half.y),
            center + new Vector2(-half.x * 0.25f, -half.y)
        };
        Rotate(a, center, angle);
        Rotate(b, center, angle);
        CreateLine("Broken Geometry A", a, color, 0.075f, false, -34);
        CreateLine("Broken Geometry B", b, color, 0.075f, false, -34);
    }

    private static void Rotate(Vector3[] points, Vector2 center, float angle)
    {
        Quaternion rotation = Quaternion.Euler(0f, 0f, angle);
        for (int i = 0; i < points.Length; i++)
            points[i] = center + (Vector2)(rotation * ((Vector2)points[i] - center));
    }

    private void CreateCircle(string name, Vector2 center, float radius, Color color, float width)
    {
        const int count = 64;
        Vector3[] points = new Vector3[count];
        for (int i = 0; i < count; i++)
        {
            float angle = i * Mathf.PI * 2f / count;
            points[i] = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
        }
        CreateLine(name, points, color, width, true, -34);
    }

    private void CreateLine(string name, Vector3[] points, Color color,
        float width, bool loop, int sortingOrder)
    {
        GameObject lineObject = new(name);
        lineObject.transform.SetParent(visualRoot.transform, false);
        LineRenderer line = lineObject.AddComponent<LineRenderer>();
        line.useWorldSpace = true;
        line.loop = loop;
        line.positionCount = points.Length;
        line.SetPositions(points);
        line.startWidth = width;
        line.endWidth = width;
        line.startColor = color;
        line.endColor = color;
        line.sortingLayerName = "Background";
        line.sortingOrder = sortingOrder;
        if (lineMaterial != null)
            line.sharedMaterial = lineMaterial;
    }

    private void EnsureMaterial()
    {
        if (lineMaterial != null)
            return;
        Shader shader = Shader.Find("Sprites/Default");
        if (shader == null)
            return;
        lineMaterial = new Material(shader)
        {
            name = "Sandbox Sector Lines (Runtime)",
            hideFlags = HideFlags.HideAndDontSave
        };
    }

    private static Color BackgroundFor(SectorPreset preset) => preset switch
    {
        SectorPreset.CorruptedTest => new Color(0.018f, 0.012f, 0.03f, 1f),
        SectorPreset.Containment => new Color(0.026f, 0.018f, 0.018f, 1f),
        SectorPreset.SystemFailure => new Color(0.016f, 0.008f, 0.02f, 1f),
        SectorPreset.CoreFinalTest => new Color(0.006f, 0.008f, 0.013f, 1f),
        _ => new Color(0.012f, 0.018f, 0.026f, 1f)
    };

    private void OnDestroy()
    {
        if (lineMaterial != null)
            Destroy(lineMaterial);
    }
}
#endif
