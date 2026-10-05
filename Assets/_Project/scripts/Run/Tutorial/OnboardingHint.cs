using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
public sealed class OnboardingHint : MonoBehaviour
{
    [SerializeField] private OnboardingStepDefinition definition;
    [SerializeField, Min(0f)] private float fadeSeconds = .45f;
    private SpriteRenderer sprite;
    private Color authoredColor;
    private bool complete;
    public OnboardingStepDefinition Definition => definition;
    private void Awake()
    {
        sprite = GetComponent<SpriteRenderer>();
        authoredColor = sprite.color;
    }

    public void Present(bool completed)
    {
        complete = completed;
        var color = authoredColor;
        if (completed) color.a = 0f;
        sprite.color = color;
        sprite.enabled = !completed;
    }
    public void Complete() => complete = true;
    private void Update()
    {
        if (!complete || sprite == null) return;
        var color = sprite.color;
        color.a = fadeSeconds <= 0f ? 0f : Mathf.MoveTowards(color.a, 0f,
            authoredColor.a * Time.unscaledDeltaTime / fadeSeconds);
        sprite.color = color;
        if (color.a <= 0f) sprite.enabled = false;
    }
    public void SetVisible(bool visible)
    {
        if (sprite != null) sprite.enabled = visible && sprite.color.a > 0f;
    }
}
