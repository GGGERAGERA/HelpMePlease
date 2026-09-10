using UnityEngine;

/// <summary>Three authored pixel sprites, recycled by the existing scene-owned prefab pool.</summary>
public sealed class ExperiencePickupEffect : MonoBehaviour
{
    public const float Duration = 0.15f;
    [SerializeField] private SpriteRenderer flash;
    [SerializeField] private SpriteRenderer left;
    [SerializeField] private SpriteRenderer right;
    private float startedAt;

    public void Play(Color color)
    {
        startedAt = Time.time;
        flash.color = Color.white;
        left.color = right.color = color;
        Animate(0f);
    }

    private void Update() => Animate((Time.time - startedAt) / Duration);

    private void Animate(float progress)
    {
        // Integer pixel steps avoid fractional sprite deformation and soft particles.
        flash.enabled = progress < 0.45f;
        int step = progress < 0.35f ? 1 : progress < 0.7f ? 2 : 3;
        left.transform.localPosition = new Vector3(-step, step, 0) / flash.sprite.pixelsPerUnit;
        right.transform.localPosition = new Vector3(step, -step, 0) / flash.sprite.pixelsPerUnit;
    }
}
