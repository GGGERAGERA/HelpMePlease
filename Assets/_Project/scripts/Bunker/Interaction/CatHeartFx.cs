using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(SpriteRenderer))]
public sealed class CatHeartFx : MonoBehaviour
{
    [SerializeField, Min(.1f)] private float duration = 1.2f;
    [SerializeField, Min(0f)] private float riseDistance = .85f;
    [SerializeField, Min(0f)] private float pulseAmount = .12f;
    private SpriteRenderer graphic;
    private Vector3 origin;
    private Vector3 originalScale;
    private Color originalColor;
    private float elapsed;
    public float Duration => duration;

    private void Awake() => graphic = GetComponent<SpriteRenderer>();

    private void OnEnable()
    {
        origin = transform.position;
        originalScale = transform.localScale;
        originalColor = graphic.color;
        elapsed = 0f;
    }

    public void SetSorting(int layer, int order)
    {
        graphic.sortingLayerID = layer;
        graphic.sortingOrder = order;
    }

    private void Update()
    {
        elapsed += Time.deltaTime;
        float t = Mathf.Clamp01(elapsed / duration);
        transform.position = origin + Vector3.up * (riseDistance * t);
        transform.localScale = originalScale * (1f + .2f * t + Mathf.Sin(t * Mathf.PI * 4f) * pulseAmount);
        Color color = originalColor;
        color.a *= 1f - Mathf.SmoothStep(0f, 1f, t);
        graphic.color = color;
        if (elapsed >= duration) Destroy(gameObject);
    }
}
