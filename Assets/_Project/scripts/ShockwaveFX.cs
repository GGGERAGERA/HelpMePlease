using UnityEngine;

public class ShockwaveFx : MonoBehaviour
{
    [SerializeField] private float startScale = 0.2f;
    [SerializeField] private float endScale = 3f;
    [SerializeField] private float lifetime = 0.25f;

    private SpriteRenderer spriteRenderer;
    private float timer;
    private Color startColor;

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();

        if (spriteRenderer != null)
            startColor = spriteRenderer.color;

        transform.localScale = Vector3.one * startScale;
    }

    private void Update()
    {
        timer += Time.deltaTime;

        float t = Mathf.Clamp01(timer / lifetime);
        float scale = Mathf.Lerp(startScale, endScale, t);

        transform.localScale = Vector3.one * scale;

        if (spriteRenderer != null)
        {
            Color color = startColor;
            color.a = Mathf.Lerp(startColor.a, 0f, t);
            spriteRenderer.color = color;
        }

        if (timer >= lifetime)
            Destroy(gameObject);
    }
}