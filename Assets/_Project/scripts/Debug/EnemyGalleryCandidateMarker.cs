using UnityEngine;

/// <summary>Authored provenance for a static gallery exhibit; no runtime discovery or gameplay.</summary>
public sealed class EnemyGalleryCandidateMarker : MonoBehaviour
{
    public string DesignId;
    public string DisplayName;
    public Object SourceAsset;
    public string SourcePath;
    public string CandidateType;
    [TextArea] public string Description;
    [TextArea] public string FoundIn;
    public Transform Display;
    public GameObject Label;
    public float PreviewScale = 1f;
}
