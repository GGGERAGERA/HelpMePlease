using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Subject42/Surface/Map")]
public sealed class SurfaceMapDefinition : ScriptableObject
{
    [SerializeField] private string id = "surface";
    [SerializeField] private Vector2 bunkerPosition;
    [SerializeField] private SurfaceSectorDefinition[] sectors = Array.Empty<SurfaceSectorDefinition>();
    [SerializeField] private string[] startingSectors = Array.Empty<string>();
    [SerializeField] private SurfaceSectorContent[] contentCatalog = Array.Empty<SurfaceSectorContent>();
    public IReadOnlyList<SurfaceSectorContent> ContentCatalog => contentCatalog;
    public string Id => id;
    public Vector2 BunkerPosition => bunkerPosition;
    public IReadOnlyList<SurfaceSectorDefinition> Sectors => sectors;
    public IReadOnlyList<string> StartingSectors => startingSectors;
    public SurfaceSectorDefinition Find(string sectorId)
    {
        foreach (var sector in sectors) if (sector != null && sector.Id == sectorId) return sector;
        return null;
    }
    public bool TryValidate(out string error)
    {
        error = null;
        var ids = new HashSet<string>(StringComparer.Ordinal);
        if (string.IsNullOrWhiteSpace(id) || sectors == null || sectors.Length == 0 || startingSectors == null || startingSectors.Length == 0)
            error = "Map identity, sectors and starting sectors are required.";
        if (error != null) return false;
        foreach (var sector in sectors)
            if (sector == null || string.IsNullOrWhiteSpace(sector.Id) || !ids.Add(sector.Id))
            { error = "Sector identities must be non-empty and unique."; return false; }
        var startIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var start in startingSectors)
            if (!ids.Contains(start) || !startIds.Add(start))
            { error = "Starting sector is missing or repeated."; return false; }
        foreach (var sector in sectors)
        {
            var links = new HashSet<string>(StringComparer.Ordinal);
            foreach (var neighbour in sector.Neighbours)
            {
                var other = Find(neighbour);
                if (other == null || other == sector || !links.Add(neighbour) || !Contains(other.Neighbours, sector.Id))
                { error = "Sector links must be unique, valid and reciprocal: " + sector.Id; return false; }
            }
        }
        var reached = new HashSet<string>(startingSectors, StringComparer.Ordinal);
        var queue = new Queue<string>(startingSectors);
        while (queue.Count > 0)
            foreach (var next in Find(queue.Dequeue()).Neighbours) if (reached.Add(next)) queue.Enqueue(next);
        if (reached.Count != sectors.Length) { error = "Every sector must be reachable from a starting sector."; return false; }
        return true;
    }
    private static bool Contains(IReadOnlyList<string> ids, string id)
    { foreach (string value in ids) if (value == id) return true; return false; }
}
