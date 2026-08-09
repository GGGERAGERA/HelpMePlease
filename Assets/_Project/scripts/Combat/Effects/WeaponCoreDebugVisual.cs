using UnityEngine;

public static class WeaponCoreDebugVisual
{
    private const string SortingLayerName = "Effects";
    private const int SortingOrder = 24;
    private static Material lineMaterial;
    public static Material SharedLineMaterial => GetLineMaterial();

    public static void DrawLine(
        Vector2 start,
        Vector2 end,
        Color color,
        float width,
        float duration)
    {
        LineRenderer line = CreateLine("WeaponCore_DebugLine", color, width);
        line.positionCount = 2;
        line.SetPosition(0, start);
        line.SetPosition(1, end);
        Object.Destroy(line.gameObject, duration);
    }

    public static void DrawRing(
        Vector2 center,
        float radius,
        Color color,
        float duration)
    {
        const int segments = 28;
        LineRenderer line = CreateLine(
            "WeaponCore_RuptureRing",
            color,
            0.085f
        );
        line.loop = true;
        line.positionCount = segments;

        for (int i = 0; i < segments; i++)
        {
            float angle = Mathf.PI * 2f * i / segments;
            line.SetPosition(i, center + new Vector2(
                Mathf.Cos(angle),
                Mathf.Sin(angle)
            ) * radius);
        }

        Object.Destroy(line.gameObject, duration);
    }

    private static LineRenderer CreateLine(
        string objectName,
        Color color,
        float width)
    {
        GameObject visual = new(objectName);
        LineRenderer line = visual.AddComponent<LineRenderer>();
        line.useWorldSpace = true;
        line.sharedMaterial = GetLineMaterial();
        line.startColor = color;
        line.endColor = new Color(color.r, color.g, color.b, 0.25f);
        line.startWidth = width;
        line.endWidth = width * 0.65f;
        line.numCapVertices = 3;
        line.numCornerVertices = 2;
        line.sortingLayerName = SortingLayerName;
        line.sortingOrder = SortingOrder;
        return line;
    }

    private static Material GetLineMaterial()
    {
        if (lineMaterial != null)
            return lineMaterial;

        Shader shader = Shader.Find("Sprites/Default");
        shader ??= Shader.Find("Universal Render Pipeline/Unlit");
        lineMaterial = new Material(shader)
        {
            name = "WeaponCore Debug Line Material",
            hideFlags = HideFlags.HideAndDontSave
        };
        return lineMaterial;
    }
}
