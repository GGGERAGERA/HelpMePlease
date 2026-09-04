using UnityEngine;

namespace Subject42.Combat.OrbitalStation
{
    public enum OrbitalInteractionMode { Idle, RewardSelection, RewardFlight, RewardSecondTarget, Relocation, WorldTelekinesis }

    // One player-local owner. Controllers retain their domain behavior and presentation.
    [DefaultExecutionOrder(-200)]
    [DisallowMultipleComponent]
    public sealed class OrbitalInteractionController : MonoBehaviour
    {
        private OrbitalStationRuntime station;
        private int consumedFrame = -1;
        private int escapeFrame = -1;
        private float previousScale;
        private float ownedScale;
        private bool ownsScale;
        public OrbitalInteractionMode Mode { get; private set; }
        public bool IsIdle => Mode == OrbitalInteractionMode.Idle;
        public bool IsGameplayInputBlocked => OrbitalDevelopmentInput.IsGameplayInputBlocked;
        private bool Alive => station != null && station.IsInitialized && station.Owner != null && !station.Owner.IsDead;
        private bool QueueIdle => UpgradeManager.Instance == null || UpgradeManager.Instance.IsRewardQueueIdle;
        public bool CanTransition => IsIdle && QueueIdle;
        public bool CanQueueDebugPlacement => IsIdle && QueueIdle;
        public bool CanUseDebugPlacement => CanQueueDebugPlacement && !IsGameplayInputBlocked && consumedFrame != Time.frameCount;
        public bool CanStartRelocation => Alive && !IsGameplayInputBlocked && QueueIdle &&
            !station.IsDebugPlacementActive && Time.timeScale > 0f && consumedFrame != Time.frameCount &&
            (IsIdle || Mode == OrbitalInteractionMode.WorldTelekinesis);
        public bool CanStartWorldTelekinesis => Alive && !IsGameplayInputBlocked && QueueIdle &&
            !station.IsDebugPlacementActive && Time.timeScale > 0f && consumedFrame != Time.frameCount && IsIdle &&
            !Input.GetMouseButton(0);
        public bool CanContinueWorldTelekinesis => Alive && !IsGameplayInputBlocked && QueueIdle &&
            Time.timeScale > 0f && (Mode == OrbitalInteractionMode.WorldTelekinesis || CanStartWorldTelekinesis);
        public bool CanConsumeRewardPointer => Alive && !IsGameplayInputBlocked && consumedFrame != Time.frameCount &&
            (Mode == OrbitalInteractionMode.RewardSelection || Mode == OrbitalInteractionMode.RewardSecondTarget);
        public void Bind(OrbitalStationRuntime runtime) { station = runtime; Mode = OrbitalInteractionMode.Idle; }
        public void PrepareForExternalPause()
        {
            station?.GetComponent<OrbitalRelocationController>()?.CancelDrag("external pause");
            station?.GetComponent<OrbitalWorldTelekinesisController>()?.CancelInteraction();
        }
        public bool BeginReward()
        {
            if (!Alive || Mode == OrbitalInteractionMode.RewardSelection || Mode == OrbitalInteractionMode.RewardFlight ||
                Mode == OrbitalInteractionMode.RewardSecondTarget) return false;
            station.GetComponent<OrbitalRelocationController>()?.CancelDrag("reward started");
            station.GetComponent<OrbitalWorldTelekinesisController>()?.CancelInteraction();
            Mode = OrbitalInteractionMode.RewardSelection;
            return true;
        }
        public void SetRewardPhase(OrbitalInteractionMode mode) => Mode = mode;
        public void EndReward() { Mode = OrbitalInteractionMode.Idle; consumedFrame = Time.frameCount; }
        public bool BeginRelocation()
        {
            if (!CanStartRelocation) return false;
            station.GetComponent<OrbitalWorldTelekinesisController>()?.CancelInteraction();
            Mode = OrbitalInteractionMode.Relocation;
            previousScale = Time.timeScale;
            ownedScale = Mathf.Min(previousScale, OrbitalPresentationConfig.Active.RelocationTimeScale);
            ownsScale = ownedScale != previousScale;
            Time.timeScale = ownedScale;
            return true;
        }
        public void EndRelocation()
        {
            if (Mode != OrbitalInteractionMode.Relocation) return;
            // Do not undo another owner's pause or reward pause.
            if (ownsScale && Time.timeScale == ownedScale && QueueIdle) Time.timeScale = previousScale;
            ownsScale = false;
            Mode = OrbitalInteractionMode.Idle;
            consumedFrame = Time.frameCount;
        }
        public bool BeginWorldTelekinesis()
        {
            if (!CanStartWorldTelekinesis) return false;
            Mode = OrbitalInteractionMode.WorldTelekinesis;
            return true;
        }
        public void EndWorldTelekinesis()
        {
            if (Mode == OrbitalInteractionMode.WorldTelekinesis) Mode = OrbitalInteractionMode.Idle;
        }
        // Pause calls this too: consumption remains true after cancellation in either Update order.
        public bool TryConsumeEscape()
        {
            if (escapeFrame == Time.frameCount) return true;
            if (IsGameplayInputBlocked) { escapeFrame = consumedFrame = Time.frameCount; return true; }
            if (IsIdle) return false;
            escapeFrame = consumedFrame = Time.frameCount;
            CancelActive();
            return true;
        }
        private void CancelActive()
        {
            station?.RewardFlow?.CancelForSceneTransition();
            station?.GetComponent<OrbitalRelocationController>()?.CancelDrag("interaction cancelled");
            station?.GetComponent<OrbitalWorldTelekinesisController>()?.CancelInteraction();
        }
        private void OnDisable() { if (!IsIdle) CancelActive(); }

        private void Update()
        {
            if (!Alive) { if (!IsIdle) CancelActive(); return; }
            if (IsGameplayInputBlocked) return;
            if (Input.GetKeyDown(KeyCode.Escape)) { TryConsumeEscape(); return; }
            if (Input.GetMouseButtonDown(1) && !IsIdle && Mode != OrbitalInteractionMode.WorldTelekinesis)
            { consumedFrame = Time.frameCount; CancelActive(); }
            if (Mode == OrbitalInteractionMode.Relocation && (IsGameplayInputBlocked || !QueueIdle || Time.timeScale <= 0f))
                station.GetComponent<OrbitalRelocationController>()?.CancelDrag("input blocked");
        }
    }
}
