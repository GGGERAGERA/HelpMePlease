using UnityEngine;

[CreateAssetMenu(menuName = "World Events/Corridor Config")]
public sealed class CorridorConfig : ScriptableObject
{
    public CorridorSettings settings = new();
    public CorridorKit kit;
    public GameObject rocket, warning;
    public ParticleSystem explosion;
    public bool TryValidate(out string error)
    {
        error = null;
        if (settings == null || settings.checkpointCount != 3 || settings.corridorWidth < 5 ||
            settings.segmentLength < 10 || settings.exitSegmentLength < 8)
            error = "Corridor requires three gates and valid dimensions.";
        else if (kit == null || kit.straight == null || kit.corner == null || kit.cap == null ||
            kit.gate == null || kit.exit == null || kit.collapse == null || kit.reclaimed == null || kit.hud == null ||
            rocket == null || warning == null || explosion == null)
            error = "Corridor presentation or shared rocket references are missing.";
        else
            foreach (var mode in new[] { CorridorRouteMode.Straight, CorridorRouteMode.L, CorridorRouteMode.Zigzag })
                if (!CorridorRouteBuilder.HasDefinition(settings, mode)) { error = "Missing authored route: " + mode; break; }
        return error == null;
    }
}
