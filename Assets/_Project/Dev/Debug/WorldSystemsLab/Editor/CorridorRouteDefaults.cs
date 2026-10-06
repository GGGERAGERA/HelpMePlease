#if UNITY_EDITOR
using UnityEngine;
public static class CorridorRouteDefaults
{
    public static void Populate(CorridorRouteDefinition definition, CorridorRouteMode preset)
    {
        definition.preset = preset;
        definition.startAnchor = Vector2.zero;
        definition.referenceSegmentLength = 35f;
        definition.referenceExitSegmentLength = 24f;
        definition.checkpointInset = 2f;
        definition.checkpointEndAnchors = new Vector2[3];
        Vector2 position = Vector2.zero;
        for (int i = 0; i < 4; i++)
        {
            Vector2 direction = Vector2.right;
            if (preset == CorridorRouteMode.L && i >= 2) direction = Vector2.up;
            if (preset == CorridorRouteMode.Zigzag && i % 2 == 1) direction = i == 3 ? Vector2.down : Vector2.up;
            position += direction * (i < 3 ? 35f : 24f);
            if (i < 3) definition.checkpointEndAnchors[i] = position;
            else definition.exitAnchor = position;
        }
    }
}
#endif
