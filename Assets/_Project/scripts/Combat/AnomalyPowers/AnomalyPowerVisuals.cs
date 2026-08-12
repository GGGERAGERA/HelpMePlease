using UnityEngine;

internal static class AnomalyPowerVisuals
{
    public static Material CreateMaterial(string name)
    {
        Shader shader = Shader.Find("Sprites/Default");

        if (shader == null)
            shader = Shader.Find("Universal Render Pipeline/Unlit");

        return shader != null
            ? new Material(shader)
            {
                name = name,
                hideFlags = HideFlags.HideAndDontSave
            }
            : null;
    }

    public static LineRenderer CreateLine(
        Transform parent,
        string name,
        Color color,
        float width,
        int positionCount,
        Material material)
    {
        GameObject lineObject = new(name);
        lineObject.transform.SetParent(parent, false);
        LineRenderer line = lineObject.AddComponent<LineRenderer>();
        line.useWorldSpace = true;
        line.positionCount = positionCount;
        line.startWidth = width;
        line.endWidth = width;
        line.startColor = color;
        line.endColor = color;
        line.numCapVertices = 5;
        line.sortingLayerName = "Effects";
        line.sortingOrder = 30;

        if (material != null)
            line.sharedMaterial = material;

        return line;
    }
}
