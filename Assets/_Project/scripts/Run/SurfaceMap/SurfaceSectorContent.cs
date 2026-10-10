using UnityEngine;

[CreateAssetMenu(menuName = "Subject42/Surface/Sector Content")]
public sealed class SurfaceSectorContent : ScriptableObject
{
    public const string UnavailableStatusKey = "content.temporarilyUnavailable";
    public const string UnavailableDescriptionKey = "content.progressPaused";
    public bool AvailableInProduction => requiredEvent == null || requiredEvent.AvailableInProduction;
    public string id;
    public SurfaceMarker marker;
    public string title = "UNKNOWN ACTIVITY";
    [TextArea] public string description = "Signal detected";
    public WorldEvent requiredEvent;
    public string requiredEventTag;
    public SurfaceMarkerState completionState = SurfaceMarkerState.Revealed;
    [Min(.1f)] public float experienceMultiplier = 1f;
    [Min(.1f)] public float goldMultiplier = 1f;
    [Min(0f)] public float threatGrowthMultiplier = 1f;
    [Min(.1f)] public float spawnPressureMultiplier = 1f;
}
