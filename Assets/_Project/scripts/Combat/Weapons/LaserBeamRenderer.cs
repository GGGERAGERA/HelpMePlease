using UnityEngine;

public sealed class LaserBeamRenderer : MonoBehaviour
{
    [Header("Core")]
    [SerializeField] private Material coreMaterial;
    [SerializeField] private Color coreColor = Color.white;
    [SerializeField] private float coreWidth = 0.035f;

    [Header("Glow")]
    [SerializeField] private Material glowMaterial;
    [SerializeField] private Color glowColor = new Color(0.1f, 0.85f, 1f, 0.45f);
    [SerializeField] private float glowWidth = 0.16f;

    [Header("Lifetime")]
    [SerializeField] private float beamDuration = 0.1f;

    [Header("Feel")]
    [SerializeField] private float endJitter = 0.035f;
    [SerializeField] private float widthJitter = 0.01f;

    [Header("Sorting")]
    [SerializeField] private string sortingLayerName = "Effects";
    [SerializeField] private int glowSortingOrder = 20;
    [SerializeField] private int coreSortingOrder = 21;

    public void Render(Vector2 start, Vector2 end, float widthScale = 1f)
    {
        Vector2 jitteredEnd = end + Random.insideUnitCircle * endJitter;
        widthScale = Mathf.Max(0.1f, widthScale);

        CreateGlow(start, jitteredEnd, widthScale);
        CreateCore(start, jitteredEnd, widthScale);
    }

    private void CreateCore(Vector2 start, Vector2 end, float widthScale)
    {
        LineRenderer line = CreateLine("LaserBeam_Core", coreMaterial, coreSortingOrder);

        float width = Mathf.Max(
            0.01f,
            (coreWidth + Random.Range(-widthJitter, widthJitter)) * widthScale
        );

        line.startWidth = width;
        line.endWidth = width;

        line.startColor = coreColor;
        line.endColor = coreColor;

        line.SetPosition(0, start);
        line.SetPosition(1, end);

        Destroy(line.gameObject, beamDuration);
    }

    private void CreateGlow(Vector2 start, Vector2 end, float widthScale)
    {
        LineRenderer line = CreateLine("LaserBeam_Glow", glowMaterial, glowSortingOrder);

        float width = Mathf.Max(
            0.01f,
            (glowWidth + Random.Range(-widthJitter, widthJitter)) * widthScale
        );

        line.startWidth = width;
        line.endWidth = width;

        line.startColor = glowColor;
        line.endColor = new Color(
            glowColor.r,
            glowColor.g,
            glowColor.b,
            0f
        );

        line.SetPosition(0, start);
        line.SetPosition(1, end);

        Destroy(line.gameObject, beamDuration);
    }

    private LineRenderer CreateLine(string objectName, Material material, int sortingOrder)
    {
        GameObject beamObject = new GameObject(objectName);
        LineRenderer line = beamObject.AddComponent<LineRenderer>();

        line.positionCount = 2;
        line.useWorldSpace = true;

        line.material = material;
        line.sortingLayerName = sortingLayerName;
        line.sortingOrder = sortingOrder;

        line.numCapVertices = 4;
        line.numCornerVertices = 4;
        line.textureMode = LineTextureMode.Stretch;
        line.alignment = LineAlignment.View;

        return line;
    }
}
