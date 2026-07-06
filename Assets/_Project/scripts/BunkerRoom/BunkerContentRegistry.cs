using System.Collections.Generic;
using UnityEngine;

public sealed class BunkerContentRegistry : MonoBehaviour
{
    private readonly Dictionary<string, BunkerContent> contentById = new();

    public void Register(BunkerContent content)
    {
        if (content == null || content.Data == null)
            return;

        string id = content.Data.Id;

        if (string.IsNullOrWhiteSpace(id))
            return;

        contentById[id] = content;
    }

    public void Unregister(BunkerContent content)
    {
        if (content == null || content.Data == null)
            return;

        string id = content.Data.Id;

        if (contentById.TryGetValue(id, out BunkerContent registered) && registered == content)
            contentById.Remove(id);
    }

    public void RefreshAll()
    {
        foreach (BunkerContent content in contentById.Values)
            content.Refresh();
    }

    public bool TryGet(string id, out BunkerContent content)
    {
        return contentById.TryGetValue(id, out content);
    }
}