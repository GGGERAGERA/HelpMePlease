using UnityEngine;

// Local appearance / handoff flash, without delaying contact or gameplay.
public sealed class OrbitalRelaySpawnFeedback : MonoBehaviour
{
    [SerializeField] private SpriteRenderer marker;
    [SerializeField, Range(.1f, .3f)] private float duration = .24f;
    [SerializeField] private Color tint = new(.08f, .82f, .86f, .8f);
    [SerializeField, Min(.1f)] private float size = 1.2f;
    private float remaining;
    public bool IsValid => marker != null && marker.sprite != null;
    public void Play() { remaining = duration; Draw(); }
    private void Update()
    {
        if (remaining <= 0) return;
        remaining = Mathf.Max(0, remaining - Time.deltaTime); Draw();
    }
    private void Draw()
    {
        marker.enabled = remaining > 0;
        if (!marker.enabled) return;
        int step = Mathf.Min(2, Mathf.FloorToInt((1 - remaining / duration) * 3));
        float scale = size * (1 + step * .25f) / marker.sprite.bounds.size.x;
        marker.transform.localScale = Vector3.one * scale;
        Color color = tint; color.a *= 1 - step * .3f; marker.color = color;
    }
    public void Clear() { remaining = 0; if (marker != null) marker.enabled = false; }
    private void OnDisable() => Clear();
}
