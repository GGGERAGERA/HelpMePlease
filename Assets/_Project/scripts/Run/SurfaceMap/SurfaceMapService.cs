using System;
using UnityEngine;

/// <summary>Owns frontier progression and selection. Has no scene or gameplay controller dependencies.</summary>
public sealed class SurfaceMapService
{
    public event Action Changed;
    public SurfaceMapDefinition Definition { get; }
    public SurfaceContentService Content { get; }
    public string SelectedSectorId { get; private set; }
    public ExpeditionTaskDefinition SelectedTask { get; private set; }
    private readonly ISurfaceMapStorage storage;
    private readonly SurfaceMapProgressionState state;
    private readonly Func<bool> escapeAccess;
    public void RefreshAccess() => Changed?.Invoke();

    public SurfaceMapService(SurfaceMapDefinition definition, ISurfaceMapStorage storage = null, Func<bool> escapeAccess = null)
    {
        Definition = definition != null ? definition : throw new ArgumentNullException(nameof(definition));
        this.escapeAccess = escapeAccess;
        if (!definition.TryValidate(out string error)) throw new ArgumentException(error, nameof(definition));
        this.storage = storage ?? new PlayerPrefsSurfaceMapStorage();
        Content = new SurfaceContentService(definition, this.storage);
        Content.Changed += () => Changed?.Invoke();
        string json = this.storage.Load(definition.Id);
        try { state = string.IsNullOrEmpty(json) ? new() : JsonUtility.FromJson<SurfaceMapProgressionState>(json) ?? new(); }
        catch (ArgumentException) { state = new(); }
        state.unlocked ??= new(); state.completed ??= new(); state.scanned ??= new();
        state.unlocked.RemoveAll(id => definition.Find(id) == null);
        state.completed.RemoveAll(id => definition.Find(id) == null);
        state.scanned.RemoveAll(id => definition.Find(id) == null);
        foreach (string id in definition.StartingSectors) Unlock(id);
        // Reconcile new links added to previously completed nodes after a content update.
        foreach (string id in state.completed) { Unlock(id); foreach (var next in definition.Find(id).Neighbours) Unlock(next); }
    }
    public SurfaceSectorStatus GetStatus(string sectorId) =>
        Definition.Find(sectorId) == null ? SurfaceSectorStatus.Locked :
        Definition.Find(sectorId).IsEscapeFinal && escapeAccess?.Invoke() != true ? SurfaceSectorStatus.Locked :
        state.completed.Contains(sectorId) ? SurfaceSectorStatus.Completed :
        Definition.Find(sectorId).IsEscapeFinal ? SurfaceSectorStatus.Available :
        state.unlocked.Contains(sectorId) ? SurfaceSectorStatus.Available : SurfaceSectorStatus.Locked;
    public bool TrySelect(string sectorId)
    {
        if (GetStatus(sectorId) == SurfaceSectorStatus.Locked) return false;
        if (SelectedSectorId != sectorId) SelectedTask = null;
        SelectedSectorId = sectorId; Changed?.Invoke(); return true;
    }
    public bool TrySelectTask(string taskId)
    {
        var sector = Definition.Find(SelectedSectorId);
        if (sector == null || GetStatus(SelectedSectorId) == SurfaceSectorStatus.Locked) return false;
        ExpeditionTaskDefinition selected = null;
        if (!string.IsNullOrEmpty(taskId))
        {
            var basis = sector.BuildRunConfig(Definition.Id);
            foreach (var task in sector.ExpeditionTasks)
                if (task != null && task.Id == taskId && task.IsAvailable(basis)) { selected = task; break; }
            if (selected == null) return false;
        }
        SelectedTask = selected; Changed?.Invoke(); return true;
    }
    public bool IsScanned(string sectorId) => state.scanned.Contains(sectorId) || state.completed.Contains(sectorId);
    public bool HasConcealedIntel(string sectorId) => !IsScanned(sectorId) &&
        (Definition.Find(sectorId)?.RequiresScan == true ||
         Content.TryGetMarker(sectorId, out var marker) && marker.Concealed && string.IsNullOrEmpty(marker.Status));
    public bool CanScan(string sectorId) => GetStatus(sectorId) != SurfaceSectorStatus.Locked && HasConcealedIntel(sectorId);
    public bool TryScan(string sectorId)
    {
        if (!CanScan(sectorId)) return false;
        // Reconnaissance changes knowledge only. It never unlocks a route,
        // completes an objective, consumes content or awards currency.
        state.scanned.Add(sectorId);
        storage.Save(Definition.Id, JsonUtility.ToJson(state));
        Changed?.Invoke();
        return true;
    }
    public bool TryBuildRunConfig(out RunConfig config)
    {
        config = null;
        if (GetStatus(SelectedSectorId) == SurfaceSectorStatus.Locked) return false;
        var sector = Definition.Find(SelectedSectorId);
        if (!sector.FinalChallengeReady) return false;
        if (SelectedTask != null && !SelectedTask.IsAvailable(sector.BuildRunConfig(Definition.Id))) SelectedTask = null;
        config = Content.Resolve(sector.BuildRunConfig(Definition.Id, SelectedTask)); return true;
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
