using System;
using System.Collections.Generic;
using UnityEngine;

public sealed partial class MetaProgressionManager
{
    public const string EscapeProtocolKey = "META_ESCAPE_PROTOCOL_PROJECT_V1";
    [SerializeField] private EscapeProtocolDefinition escapeProtocol;
    [Serializable] private sealed class StageProgress { public string id; public int invested; public bool completed; }
    [Serializable] private sealed class ProtocolProgress
    { public List<StageProgress> stages = new(); public bool completed, completionAnnounced; }
    private ProtocolProgress protocolProgress;
    private bool investingProtocol;
    public EscapeProtocolDefinition EscapeProtocol => escapeProtocol;
    public bool HasEscapeProtocol => escapeProtocol != null && escapeProtocol.IsValid;
    public event Action<string> EscapeUnlockGranted;
    public bool EscapeProtocolCompleted => HasEscapeProtocol && State.completed;
    public bool HasEscapeProtocolCompletionUpdate => EscapeProtocolCompleted && !State.completionAnnounced;
    public int CurrentEscapeStage
    {
        get
        {
            if (!HasEscapeProtocol) return 0;
            for (int i = 0; i < escapeProtocol.Stages.Count; i++) if (!Record(escapeProtocol.Stages[i].id).completed) return i;
            return escapeProtocol.Stages.Count;
        }
    }
    public bool EscapeProtocolFunded => HasEscapeProtocol && CurrentEscapeStage == escapeProtocol.Stages.Count;
    public int GetEscapeInvestment(int index) => !HasEscapeProtocol || index < 0 || index >= escapeProtocol.Stages.Count ? 0 :
        Mathf.Clamp(Record(escapeProtocol.Stages[index].id).invested, 0, escapeProtocol.Stages[index].cost);
    public bool IsEscapeStageCompleted(int index) => HasEscapeProtocol && index >= 0 && index < escapeProtocol.Stages.Count && Record(escapeProtocol.Stages[index].id).completed;
    private ProtocolProgress State { get { if (protocolProgress == null) LoadEscapeProtocol(); return protocolProgress; } }
    private StageProgress Record(string id)
    {
        var progress = State.stages.Find(s => s.id == id);
        if (progress == null) { progress = new StageProgress { id = id }; State.stages.Add(progress); }
        return progress;
    }
    private void LoadEscapeProtocol()
    {
        protocolProgress = new ProtocolProgress();
        if (!HasEscapeProtocol) return;
        bool hadSave = PlayerPrefs.HasKey(EscapeProtocolKey);
        try { if (hadSave) protocolProgress = JsonUtility.FromJson<ProtocolProgress>(PlayerPrefs.GetString(EscapeProtocolKey)) ?? new(); }
        catch (ArgumentException) { protocolProgress = new(); }
        protocolProgress.stages ??= new();
        protocolProgress.stages.RemoveAll(s => s == null || string.IsNullOrEmpty(s.id));
        bool changed = !hadSave;
        for (int i = 0; i < escapeProtocol.Stages.Count; i++)
        {
            var definition = escapeProtocol.Stages[i]; var progress = Record(definition.id);
            // Grandfather existing story access exactly once. A new Guardian victory
            // after this migration never replaces paid investment or pays a stage.
            if (!hadSave && i < Mathf.Clamp(EscapeAccess, 0, escapeProtocol.Stages.Count))
            { progress.invested = definition.cost; progress.completed = true; }
            if (!progress.completed && progress.invested >= definition.cost) { progress.completed = true; changed = true; }
            progress.invested = Mathf.Max(0, progress.invested);
        }
        if (changed) { StageProtocolSave(); PlayerPrefs.Save(); }
    }
    private void StageProtocolSave() => PlayerPrefs.SetString(EscapeProtocolKey, JsonUtility.ToJson(State));
    public bool TryInvestEscapeCores(string expectedStageId, int requested, out int invested)
    {
        invested = 0;
        if (investingProtocol || requested <= 0 || !HasEscapeProtocol || EscapeProtocolFunded || EscapeProtocolCompleted || CurrencyManager.Instance == null) return false;
        var stage = escapeProtocol.Stages[CurrentEscapeStage];
        if (!string.Equals(stage.id, expectedStageId, StringComparison.Ordinal)) return false;
        var record = Record(stage.id);
        int amount = Mathf.Min(requested, stage.cost - record.invested, CurrencyManager.Instance.TotalCores);
        if (amount <= 0) return false;
        investingProtocol = true;
        try
        {
            if (!CurrencyManager.Instance.TrySpendCoresExact(amount, () =>
            {
                record.invested += amount; record.completed = record.invested >= stage.cost; StageProtocolSave();
            })) return false;
            invested = amount;
            SurfaceMap?.RefreshAccess();
            ProgressChanged?.Invoke();
            if (record.completed && stage.unlockIds != null)
                foreach (var unlock in stage.unlockIds) if (!string.IsNullOrWhiteSpace(unlock)) EscapeUnlockGranted?.Invoke(unlock);
            return true;
        }
        finally { investingProtocol = false; }
    }
    public bool TryCompleteEscapeProtocol(RunConfig config, RunEndReason reason, bool confirmedProductionVictory)
    {
        if (!confirmedProductionVictory || reason != RunEndReason.Victory || !EscapeProtocolFunded || EscapeProtocolCompleted ||
            config == null || !config.IsEscapeFinal || config.SectorId != escapeProtocol.FinalSector.Id ||
            SurfaceMap == null || config.MapId != SurfaceMap.Definition.Id) return false;
        State.completed = true; State.completionAnnounced = false;
        StageProtocolSave(); PlayerPrefs.Save(); ProgressChanged?.Invoke(); return true;
    }
    public void AcknowledgeEscapeProtocolCompletion()
    {
        if (!HasEscapeProtocolCompletionUpdate) return;
        State.completionAnnounced = true; StageProtocolSave(); PlayerPrefs.Save();
    }
    public string GetEscapeObjectiveText()
    {
        if (!HasEscapeProtocol) return null;
        if (EscapeProtocolCompleted) return SurfaceMapBriefing.Text("escape.objective.completed");
        if (EscapeProtocolFunded) return SurfaceMapBriefing.Text("escape.objective.final");
        var stage = escapeProtocol.Stages[CurrentEscapeStage];
        return SurfaceMapBriefing.Format(stage.objective, GetEscapeInvestment(CurrentEscapeStage), stage.cost);
    }
}
