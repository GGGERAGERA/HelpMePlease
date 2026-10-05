using UnityEngine;

public sealed class OrbitalRelayNode : MonoBehaviour
{
    [SerializeField] private CircleCollider2D contactArea;
    [SerializeField] private SpriteRenderer core;
    [SerializeField] private GameObject activeGlow;
    [SerializeField] private Transform progressFill;
    [SerializeField] private Animator activation;
    public bool TryGetContactCircle(out Vector2 center, out float radius)
    {
        center = default; radius = 0;
        if (contactArea == null) return false;
        Vector3 scale = contactArea.transform.lossyScale;
        if (Mathf.Abs(scale.x) < .0001f || Mathf.Abs(Mathf.Abs(scale.x) - Mathf.Abs(scale.y)) > .001f) return false;
        center = contactArea.transform.TransformPoint(contactArea.offset);
        radius = contactArea.radius * Mathf.Abs(scale.x);
        return radius > 0 && !float.IsNaN(radius) && !float.IsInfinity(radius);
    }
    public bool IsValid => core != null && activeGlow != null && progressFill != null &&
        activation != null && activation.runtimeAnimatorController != null && TryGetContactCircle(out _, out _);
    public void Apply(bool active, float progress)
    {
        activeGlow.SetActive(active);
        core.color = active ? new Color(.2f, 1f, .9f) : new Color(.2f, .35f, .4f);
        Vector3 scale = progressFill.localScale; scale.x = Mathf.Clamp01(progress) * .7f;
        progressFill.localScale = scale;
    }
    public void PlayActivation() => activation.Play("NodeActivation", 0, 0f);
}
