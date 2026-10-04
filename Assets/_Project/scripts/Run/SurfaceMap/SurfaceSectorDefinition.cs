using UnityEngine;

[CreateAssetMenu(menuName = "Subject42/Surface/Sector")]
public sealed class SurfaceSectorDefinition : ScriptableObject
{
    [SerializeField] private string id;
    [SerializeField] private string displayName;
    [SerializeField, TextArea] private string description;
    [SerializeField] private Vector2 mapPosition;
    [SerializeField] private string[] neighbours = System.Array.Empty<string>();
    [SerializeField] private RunConfigParameters parameters = new();
    [SerializeField] private SurfaceSectorContent[] content = System.Array.Empty<SurfaceSectorContent>();
    public System.Collections.Generic.IReadOnlyList<SurfaceSectorContent> Content => content;
    public string Id => id;
    public string DisplayName => displayName;
    public string Description => description;
    public Vector2 MapPosition => mapPosition;
    public System.Collections.Generic.IReadOnlyList<string> Neighbours => neighbours;
    public RunConfig BuildRunConfig(string mapId) => new(mapId, id, parameters);
}
