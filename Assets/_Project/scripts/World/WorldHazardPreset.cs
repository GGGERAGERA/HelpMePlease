using UnityEngine;

[CreateAssetMenu(menuName = "Game/World Hazards/Sector Preset", fileName = "WorldHazardPreset")]
public sealed class WorldHazardPreset : ScriptableObject
{
    [SerializeField] private WorldHazardDefinition[] attacks;
    [SerializeField, Min(5f)] private float initialDelay = 45f;
    [SerializeField] private Vector2 interval = new(45f, 65f);
    [SerializeField] private Vector2 targetDistance = new(3.5f, 5f);
    [SerializeField, Min(.5f)] private float playerClearance = 1f;
    [SerializeField, Min(.5f)] private float edgeClearance = .75f;

    public WorldHazardDefinition[] Attacks => attacks;
    public float InitialDelay => Mathf.Max(5f, initialDelay);
    public float PlayerClearance => Mathf.Max(.5f, playerClearance);
    public float EdgeClearance => Mathf.Max(.5f, edgeClearance);
    public float MinDistance => Mathf.Max(0f, Mathf.Min(targetDistance.x, targetDistance.y));
    public float MaxDistance => Mathf.Max(MinDistance, Mathf.Max(targetDistance.x, targetDistance.y));
    public float NextInterval() => Random.Range(Mathf.Max(5f, Mathf.Min(interval.x, interval.y)),
        Mathf.Max(5f, Mathf.Max(interval.x, interval.y)));
}
