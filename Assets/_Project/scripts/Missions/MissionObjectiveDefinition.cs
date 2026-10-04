using UnityEngine;

public readonly struct MissionObjectiveSignal
{
    public readonly RunConfig Config;
    public readonly int RunId;
    public readonly string Kind, Tag;
    public readonly object Payload;
    public MissionObjectiveSignal(RunConfig config,int runId,string kind,object payload,string tag=null)
    { Config=config; RunId=runId; Kind=kind; Payload=payload; Tag=tag; }
}

public interface IMissionObjectiveHandler
{
    bool IsCompleted { get; }
    bool Observe(MissionObjectiveSignal signal);
    string CaptureProgress();
    void RestoreProgress(string data);
}

/// <summary>Extend with another objective asset/handler; MissionService does not switch on objective types.</summary>
public abstract class MissionObjectiveDefinition : ScriptableObject
{
    [SerializeField] private string id;
    [SerializeField] private string description;
    public string Id => id;
    public string Description => description;
    public abstract SurfaceSectorContent RunContent { get; }
    public abstract IMissionObjectiveHandler CreateHandler();
    public abstract bool IsValid { get; }
}
