#if UNITY_EDITOR || DEVELOPMENT_BUILD
using UnityEngine;
public enum CorridorV2Preset { Straight, L, Zigzag }
[System.Serializable]
public sealed class CorridorV2Settings
{
    public TMPro.TMP_FontAsset font;
    public CorridorV2Preset routePreset;
    [Range(1, 8)] public int checkpointCount = 5;
    [Min(5f)] public float corridorWidth = 8f;
    [Min(10f)] public float segmentLength = 21f;
    [Min(8f)] public float exitSegmentLength = 24f;
    [Min(.1f)] public float collapseSpeed = 5f;
    [Min(0f)] public float collapseDamage = 8f;
    [Min(.65f)] public float collapseDamageInterval = 1f;
    [Min(.5f)] public float strikeInterval = 3.2f;
    [Min(.6f)] public float strikeTelegraphTime = 1.4f;
    [Min(.1f)] public float strikeFallTime = .35f;
    [Min(0)] public int strikeDamage = 10;
    [Range(.5f, 2f)] public float strikeRadius = 1.5f;
    [Min(.1f)] public float chaseSpacingTime = .4f;
    [Min(0f)] public float finalPushDuration = 3.5f;
    [Min(1f)] public float finalCollapseMultiplier = 1.25f;
    [Range(.4f, 1f)] public float finalStrikeIntervalMultiplier = .75f;
    public AudioCueId checkpointSfx = AudioCueId.CorePulse;
    public AudioCueId completionSfx = AudioCueId.CoreCascade;
    public CorridorV2Settings Snapshot() => JsonUtility.FromJson<CorridorV2Settings>(JsonUtility.ToJson(this));
}
#endif
