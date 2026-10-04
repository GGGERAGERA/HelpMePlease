using System;
using UnityEngine;

/// <summary>Owns frontier progression and selection. Has no scene or gameplay controller dependencies.</summary>
public sealed class SurfaceMapService
{
    public event Action Changed;
    public SurfaceMapDefinition Definition { get; }
    public SurfaceContentService Content { get; }
    public string SelectedSectorId { get; private set; }
    private readonly ISurfaceMapStorage storage;
    private readonly SurfaceMapProgressionState state;

    public SurfaceMapService(SurfaceMapDefinition definition, ISurfaceMapStorage storage = null)
    {
        Definition = definition != null ? definition : throw new ArgumentNullException(nameof(definition));
        if (!definition.TryValidate(out string error)) throw new ArgumentException(error, nameof(definition));
        this.storage = storage ?? new PlayerPrefsSurfaceMapStorage();
        Content = new SurfaceContentService(definition, this.storage);
        Content.Changed += () => Changed?.Invoke();
        string json = this.storage.Load(definition.Id);
        try { state = string.IsNullOrEmpty(json) ? new() : JsonUtility.FromJson<SurfaceMapProgressionState>(json) ?? new(); }
        catch (ArgumentException) { state = new(); }
        state.unlocked ??= new(); state.completed ??= new();
        state.unlocked.RemoveAll(id => definition.Find(id) == null);
        state.completed.RemoveAll(id => definition.Find(id) == null);
        foreach (string id in definition.StartingSectors) Unlock(id);
        // Reconcile new links added to previously completed nodes after a content update.
        foreach (string id in state.completed) { Unlock(id); foreach (var next in definition.Find(id).Neighbours) Unlock(next); }
    }
    public SurfaceSectorStatus GetStatus(string sectorId) =>
        Definition.Find(sectorId) == null ? SurfaceSectorStatus.Locked :
        state.completed.Contains(sectorId) ? SurfaceSectorStatus.Completed :
        state.unlocked.Contains(sectorId) ? SurfaceSectorStatus.Available : SurfaceSectorStatus.Locked;
    public bool TrySelect(string sectorId)
    {
        if (GetStatus(sectorId) == SurfaceSectorStatus.Locked) return false;
        SelectedSectorId = sectorId; Changed?.Invoke(); return true;
    }
    public bool TryBuildRunConfig(out RunConfig config)
    {
        config = null;
        if (GetStatus(SelectedSectorId) == SurfaceSectorStatus.Locked) return false;
        config = Content.Resolve(Definition.Find(SelectedSectorId).BuildRunConfig(Definition.Id)); return true;
    }
    public bool CompleteVictory(RunConfig config, RunEndReason reason, bool confirmedProductionVictory)
    {
        if (!confirmedProductionVictory || reason != RunEndReason.Victory || config == null || config.MapId != Definition.Id ||
            GetStatus(config.SectorId) == SurfaceSectorStatus.Locked || state.completed.Contains(config.SectorId)) return false;
        state.completed.Add(config.SectorId);
        foreach (string next in Definition.Find(config.SectorId).Neighbours) Unlock(next);
        storage.Save(Definition.Id, JsonUtility.ToJson(state)); Changed?.Invoke(); return true;
    }
    private void Unlock(string id) { if (!state.unlocked.Contains(id)) state.unlocked.Add(id); }
}
