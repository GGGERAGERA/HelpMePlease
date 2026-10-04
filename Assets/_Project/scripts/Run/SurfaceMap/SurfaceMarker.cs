using UnityEngine;

// Open type identifier: adding a marker asset never requires a UI switch/enum change.
[CreateAssetMenu(menuName = "Subject42/Surface/Marker")]
public sealed class SurfaceMarker : ScriptableObject
{
    public string typeId = "unknown";
    public string icon = "?";
    public string revealedIcon = "✓";
    public Color color = new(1f, .8f, .3f);
    public bool pulse = true;
    public bool concealDetails = true;
}
public enum SurfaceMarkerState { Active, Revealed, Completed }
public enum SurfaceMissionState { Active, Completed }
