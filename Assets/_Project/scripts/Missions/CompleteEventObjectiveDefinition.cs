using UnityEngine;

[CreateAssetMenu(menuName="Subject42/Missions/Objectives/Complete Event")]
public sealed class CompleteEventObjectiveDefinition : MissionObjectiveDefinition
{
    [SerializeField] private SurfaceSectorContent content;
    public override SurfaceSectorContent RunContent => content;
    public override bool IsValid => content!=null && content.requiredEvent!=null && !string.IsNullOrWhiteSpace(Id);
    private bool Matches(MissionObjectiveSignal signal) => signal.Kind=="CompleteEvent" &&
        signal.Payload is WorldEvent source && content!=null && source==content.requiredEvent &&
        (string.IsNullOrEmpty(content.requiredEventTag)||signal.Tag==content.requiredEventTag);
    public override IMissionObjectiveHandler CreateHandler() => new Handler(this);
    private sealed class Handler : IMissionObjectiveHandler
    {
        private readonly CompleteEventObjectiveDefinition definition;
        public bool IsCompleted { get; private set; }
        public Handler(CompleteEventObjectiveDefinition definition) { this.definition=definition; }
        public bool Observe(MissionObjectiveSignal signal)
        {
            if(IsCompleted||!definition.Matches(signal))return false;
            IsCompleted=true; return true;
        }
        public string CaptureProgress() => IsCompleted?"complete":string.Empty;
        public void RestoreProgress(string data) { IsCompleted=data=="complete"; }
    }
}
