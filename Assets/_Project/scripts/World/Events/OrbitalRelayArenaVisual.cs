using UnityEngine;

// Authored pixel sprites only; never builds geometry or participates in placement.
public sealed class OrbitalRelayArenaVisual : MonoBehaviour
{
    [SerializeField] private OrbitalRelayEvent source;
    [SerializeField] private SpriteRenderer arenaBorder, spawnInner, spawnOuter;
    [SerializeField] private Color arenaTint = new(.08f, .82f, .86f, .75f);
    [SerializeField] private Color spawnTint = new(.239216f, .552941f, 1f, .28f);
    public bool IsValid => source != null && arenaBorder != null && spawnInner != null && spawnOuter != null;
    public void Show(bool visible)
    {
        if (visible && source != null && source.ArenaBounds != null)
        {
            var bounds = source.ArenaBounds;
            transform.position = bounds.transform.TransformPoint(bounds.offset);
            Fit(arenaBorder, source.ArenaRadius, arenaTint);
            Vector2 range = source.BaseNodeSpawnRange;
            Fit(spawnInner, range.x, spawnTint);
            Fit(spawnOuter, range.y, spawnTint);
        }
        gameObject.SetActive(visible);
    }
    private static void Fit(SpriteRenderer ring, float radius, Color tint)
    {
        Vector3 parentScale = ring.transform.parent.lossyScale;
        Vector2 size = ring.sprite.bounds.size;
        ring.transform.localScale = new Vector3(radius * 2 / size.x / Mathf.Abs(parentScale.x),
            radius * 2 / size.y / Mathf.Abs(parentScale.y), 1);
        ring.color = tint;
    }
}
