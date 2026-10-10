using UnityEngine;

public enum FalseSignalPointState { Unchecked, Scanning, Verified, Warning, Activated, Disabled }

[RequireComponent(typeof(CircleCollider2D))]
public sealed class FalseSignalPoint : Interactable
{
    [SerializeField] private CircleCollider2D contactArea;
    [SerializeField] private FalseSignalPointView view;
    private FalseSignalEvent owner;
    private bool isReal;
    private float scanDuration, elapsed;
    private Vector2 diagnosticTarget;
    public FalseSignalPointState State { get; private set; }
    internal bool IsReal => isReal;
    public bool IsValid => contactArea != null && contactArea.radius > 0 && view != null && view.IsValid;
    public override bool CanInteract => base.CanInteract && State == FalseSignalPointState.Verified && owner != null && owner.AcceptsSignals &&
        PlayerRuntimeReference.ResolvePlayerTransform() is Transform player && Vector2.Distance(player.position, transform.position) <= 2f;
    public void Initialize(FalseSignalEvent eventOwner, bool realSignal, Vector2 target, float duration)
    {
        owner = eventOwner; isReal = realSignal; diagnosticTarget = target; scanDuration = duration;
        State = FalseSignalPointState.Unchecked; elapsed = 0;
        Render();
    }
    public void GetContactCircle(out Vector2 center, out float radius)
    {
        center = contactArea.transform.TransformPoint(contactArea.offset);
        radius = contactArea.radius * Mathf.Abs(contactArea.transform.lossyScale.x);
    }
    internal void BeginScan()
    {
        if (State != FalseSignalPointState.Unchecked || owner == null || !owner.AcceptsSignals) return;
        State = FalseSignalPointState.Scanning; elapsed = 0;
        AudioService.Instance?.PlayAt(AudioCueId.CorePulse, transform.position);
        Render();
    }
    private void Update()
    {
        if (owner == null || owner.IsCompleted || Time.deltaTime <= 0) return;
        elapsed += Time.deltaTime;
        if (State == FalseSignalPointState.Scanning && elapsed >= scanDuration)
        { State = FalseSignalPointState.Verified; elapsed = 0; }
        Render();
    }
    public override void Interact()
    {
        if (!CanInteract) return;
        State = isReal ? FalseSignalPointState.Activated : FalseSignalPointState.Warning;
        elapsed = 0; Render(); owner.ActivateSignal(this);
    }
    internal void DisableSignal()
    { State = FalseSignalPointState.Disabled; elapsed = 0; Render(); }
    private void Render() => view.Render(State, isReal, elapsed, scanDuration, diagnosticTarget);
}
