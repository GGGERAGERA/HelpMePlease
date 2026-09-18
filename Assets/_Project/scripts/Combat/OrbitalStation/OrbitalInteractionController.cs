using UnityEngine;

namespace Subject42.Combat.OrbitalStation
{
    public enum OrbitalInteractionMode { Idle, RewardSelection, RewardFlight, RewardSecondTarget, Relocation, CustomDrawing = 6 }

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
        private bool ownsBulletTime;
        private float bulletBaseScale, bulletScale, bulletBaseFixedStep;
        private CharacterMovement2D movement;
        public float BulletTimeBlend => ownsBulletTime ? Mathf.InverseLerp(1f, station.BulletTime.WorldTimeScale, bulletScale / bulletBaseScale) : 0f;
        private OrbitalInteractionMode beforeDraw;
        private float beforeDrawScale;
        public bool IsCustomDrawing => Mode == OrbitalInteractionMode.CustomDrawing || station != null && station.HasPendingCustomRings;
        public OrbitalInteractionMode Mode { get; private set; }
        public bool IsIdle => Mode == OrbitalInteractionMode.Idle;
        public bool IsGameplayInputBlocked => IsCustomDrawing || SceneTransitionOverlay.IsTransitioning || OrbitalDevelopmentInput.IsGameplayInputBlocked
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            || DebugSuppressPlayerInput
#endif
            ;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        public bool DebugSuppressPlayerInput { get; set; }
#endif
        private bool Alive => station != null && station.IsInitialized && station.Owner != null && !station.Owner.IsDead;
        private bool QueueIdle => UpgradeManager.Instance == null || UpgradeManager.Instance.IsRewardQueueIdle;
        public bool CanTransition => IsIdle && QueueIdle && !IsCustomDrawing;
        public bool CanQueueDebugPlacement => IsIdle && QueueIdle;
        public bool CanUseDebugPlacement => CanQueueDebugPlacement && !IsGameplayInputBlocked && consumedFrame != Time.frameCount;
        public bool CanStartRelocation => Alive && !IsGameplayInputBlocked && QueueIdle &&
            !station.IsDebugPlacementActive && Time.timeScale > 0f && consumedFrame != Time.frameCount &&
            IsIdle;
        public bool CanConsumeRewardPointer => Alive && !IsGameplayInputBlocked && consumedFrame != Time.frameCount &&
            (Mode == OrbitalInteractionMode.RewardSelection || Mode == OrbitalInteractionMode.RewardSecondTarget);
        public void Bind(OrbitalStationRuntime runtime)
        {
            ReleaseBulletTime();
            station = runtime;
            movement = station != null && station.Owner != null
                ? station.Owner.Transform.GetComponent<CharacterMovement2D>() : null;
            Mode = OrbitalInteractionMode.Idle;
        }

        public void TickBulletTime(bool active, float scale, float transitionSeconds, float unscaledDeltaTime)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (active || ownsBulletTime) PhysicalCombatFeedbackRuntime.CancelHitStopForExternalTimeControl();
#endif
            // Never overwrite another time owner (pause, rewards, transition, death or reset).
            if (!Alive || !IsIdle || !QueueIdle || IsGameplayInputBlocked || Time.timeScale <= 0f ||
                ownsBulletTime && !Mathf.Approximately(Time.timeScale, bulletScale))
            { ReleaseBulletTime(); return; }
            if (!ownsBulletTime)
            {
                if (!active) return;
                bulletBaseScale = bulletScale = Time.timeScale;
                bulletBaseFixedStep = Time.fixedDeltaTime;
                ownsBulletTime = true;
            }
            float target = active ? bulletBaseScale * scale : bulletBaseScale;
            bulletScale = Mathf.MoveTowards(bulletScale, target,
                bulletBaseScale * (1f - scale) * unscaledDeltaTime / Mathf.Max(.01f, transitionSeconds));
            Time.timeScale = bulletScale;
            Time.fixedDeltaTime = bulletBaseFixedStep * bulletScale / bulletBaseScale;
            if (movement != null) movement.BulletTimeControlMultiplier = bulletBaseScale / bulletScale;
            if (!active && Mathf.Approximately(bulletScale, bulletBaseScale)) ReleaseBulletTime();
        }

        public void ReleaseBulletTime()
        {
            if (!ownsBulletTime) return;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            PhysicalCombatFeedbackRuntime.CancelHitStopForExternalTimeControl();
#endif
            if (Mathf.Approximately(Time.timeScale, bulletScale)) Time.timeScale = bulletBaseScale;
            Time.fixedDeltaTime = bulletBaseFixedStep;
            if (movement != null) movement.BulletTimeControlMultiplier = 1f;
            ownsBulletTime = false;
            station?.BulletTime.Release();
        }
        public void PrepareForExternalPause()
        {
            ReleaseBulletTime();
            station?.GetComponent<OrbitalRelocationController>()?.CancelDrag("external pause");
        }
        public bool BeginReward()
        {
            if (!Alive || Mode == OrbitalInteractionMode.RewardSelection || Mode == OrbitalInteractionMode.RewardFlight ||
                Mode == OrbitalInteractionMode.RewardSecondTarget) return false;
            station.GetComponent<OrbitalRelocationController>()?.CancelDrag("reward started");
            ReleaseBulletTime();
            Mode = OrbitalInteractionMode.RewardSelection;
            return true;
        }
        public void SetRewardPhase(OrbitalInteractionMode mode) => Mode = mode;
        public void EndReward()
        {
            if (Mode == OrbitalInteractionMode.CustomDrawing) beforeDraw = OrbitalInteractionMode.Idle;
            else Mode = OrbitalInteractionMode.Idle;
            consumedFrame = Time.frameCount;
        }
        public void BeginCustomDraw()
        {
            if (Mode == OrbitalInteractionMode.CustomDrawing) return;
            PrepareForExternalPause();
            beforeDraw = Mode;
            beforeDrawScale = Time.timeScale;
            Mode = OrbitalInteractionMode.CustomDrawing;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            PhysicalCombatFeedbackRuntime.CancelHitStopForExternalTimeControl();
#endif
            Time.timeScale = 0f;
        }
        public void EndCustomDraw()
        {
            if (Mode != OrbitalInteractionMode.CustomDrawing) return;
            Mode = beforeDraw;
            consumedFrame = Time.frameCount;
            if (!SceneTransitionOverlay.IsTransitioning && station?.Owner != null && !station.Owner.IsDead)
                Time.timeScale = UpgradeManager.Instance != null && UpgradeManager.Instance.IsChoosingUpgrade ? 0f : beforeDrawScale;
        }
        public bool BeginRelocation()
        {
            if (!CanStartRelocation) return false;
            ReleaseBulletTime();
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
        }
        private void OnDisable() { ReleaseBulletTime(); if (!IsIdle) CancelActive(); }

        private void Update()
        {
            if (!Alive) { ReleaseBulletTime(); if (!IsIdle) CancelActive(); return; }
            if (IsGameplayInputBlocked) return;
            if (Input.GetKeyDown(KeyCode.Escape)) { TryConsumeEscape(); return; }
            if (Input.GetMouseButtonDown(1) && !IsIdle)
            { consumedFrame = Time.frameCount; CancelActive(); }
            if (Mode == OrbitalInteractionMode.Relocation && (IsGameplayInputBlocked || !QueueIdle || Time.timeScale <= 0f))
                station.GetComponent<OrbitalRelocationController>()?.CancelDrag("input blocked");
        }
    }
}
