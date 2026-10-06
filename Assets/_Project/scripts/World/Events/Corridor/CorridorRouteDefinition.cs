using UnityEngine;

// Data only: anchors are in authored local units. No runtime geometry is generated here.
[CreateAssetMenu(menuName = "Dev/World Systems Lab/Corridor Route")]
public sealed class CorridorRouteDefinition : ScriptableObject
{
    public CorridorRouteMode preset;
    public Vector2 startAnchor;
    public Vector2[] checkpointEndAnchors;
    public Vector2 exitAnchor;
    [Min(1f)] public float referenceSegmentLength = 35f;
    [Min(1f)] public float referenceExitSegmentLength = 24f;
    [Min(.1f)] public float checkpointInset = 2f;

}
