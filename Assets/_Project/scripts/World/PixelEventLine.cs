using UnityEngine;

[DefaultExecutionOrder(100)]
public sealed class PixelEventLine : MonoBehaviour
{
    private LineRenderer source;
    private PixelEffectMesh raster;
    private bool originalForceOff;
    private int previousHash;
    private bool built;
    public int CellCount => raster?.CellCount ?? 0;

    public static void Attach(LineRenderer line)
    {
        foreach (var existing in line.GetComponents<PixelEventLine>())
            if (existing.source == line) return;
        var adapter = line.gameObject.AddComponent<PixelEventLine>();
        adapter.source = line;
        adapter.originalForceOff = line.forceRenderingOff;
        adapter.raster = new PixelEffectMesh(line.transform);
        line.forceRenderingOff = true;
    }

    private void LateUpdate() => Refresh();

    public void Refresh()
    {
        if (source == null || raster == null) return;
        source.forceRenderingOff = true;
        if (!source.enabled || source.positionCount < 2) { raster.Hide(); built = false; return; }
        int hash = source.transform.localToWorldMatrix.GetHashCode();
        unchecked
        {
            hash = hash * 31 + source.startColor.GetHashCode();
            hash = hash * 31 + source.endColor.GetHashCode();
            hash = hash * 31 + source.startWidth.GetHashCode();
            hash = hash * 31 + source.endWidth.GetHashCode();
            hash = hash * 31 + source.loop.GetHashCode();
            hash = hash * 31 + source.useWorldSpace.GetHashCode();
            hash = hash * 31 + source.sortingLayerID;
            hash = hash * 31 + source.sortingOrder;
            for (int i = 0; i < source.positionCount; i++) hash = hash * 31 + source.GetPosition(i).GetHashCode();
        }
        if (built && hash == previousHash) return;
        previousHash = hash;
        built = true;
        raster.Begin(source);
        int count = source.loop ? source.positionCount : source.positionCount - 1;
        float scale = source.useWorldSpace ? 1 : Mathf.Max(
            source.transform.lossyScale.x, source.transform.lossyScale.y);
        for (int i = 0; i < count; i++)
        {
            Vector3 a = source.GetPosition(i), b = source.GetPosition((i + 1) % source.positionCount);
            if (!source.useWorldSpace) { a = source.transform.TransformPoint(a); b = source.transform.TransformPoint(b); }
            float t = count > 1 ? i / (float)(count - 1) : 0;
            Color color = Color.Lerp(source.startColor, source.endColor, Mathf.Round(t * 4) / 4);
            raster.Segment(a, b, Mathf.Lerp(source.startWidth, source.endWidth, t) * Mathf.Abs(scale), color);
        }
        raster.End();
    }

    private void OnDisable() { raster?.Hide(); built = false; if (source != null) source.forceRenderingOff = originalForceOff; }
    private void OnDestroy() { if (source != null) source.forceRenderingOff = originalForceOff; raster?.Dispose(); }
}
