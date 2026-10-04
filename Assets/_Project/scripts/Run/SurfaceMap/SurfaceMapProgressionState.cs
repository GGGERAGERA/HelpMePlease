using System;
using System.Collections.Generic;

[Serializable]
public sealed class SurfaceMapProgressionState
{
    public List<string> unlocked = new();
    public List<string> completed = new();
}

public enum SurfaceSectorStatus { Locked, Available, Completed }

public interface ISurfaceMapStorage
{
    string Load(string mapId);
    void Save(string mapId, string json);
}

public sealed class PlayerPrefsSurfaceMapStorage : ISurfaceMapStorage
{
    public const string KeyPrefix = "META_SURFACE_MAP_";
    public string Load(string mapId) => UnityEngine.PlayerPrefs.GetString(KeyPrefix + mapId, string.Empty);
    public void Save(string mapId, string json)
    {
        UnityEngine.PlayerPrefs.SetString(KeyPrefix + mapId, json);
        UnityEngine.PlayerPrefs.Save();
    }
}
