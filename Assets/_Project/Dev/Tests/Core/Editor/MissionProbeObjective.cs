#if UNITY_EDITOR
// Synthetic handler fixture only: demonstrates that counted progress needs no MissionService type switch.
public sealed class MissionProbeObjective : MissionObjectiveDefinition
{
    public override SurfaceSectorContent RunContent=>null;
    public override bool IsValid=>!string.IsNullOrEmpty(Id);
    public override IMissionObjectiveHandler CreateHandler()=>new Handler();
    sealed class Handler:IMissionObjectiveHandler
    {
        int count;
        public bool IsCompleted=>count>=2;
        public bool Observe(MissionObjectiveSignal signal) { if(signal.Kind!="Probe"||IsCompleted)return false; count++; return true; }
        public string CaptureProgress()=>count.ToString(System.Globalization.CultureInfo.InvariantCulture);
        public void RestoreProgress(string data) { int.TryParse(data,out count); }
    }
}
#endif
