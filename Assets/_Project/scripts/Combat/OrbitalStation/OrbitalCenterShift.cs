using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Subject42.Combat.OrbitalStation
{
    // The station owns the transform; CharacterMovement owns directional input sampling.
    [DisallowMultipleComponent]
    public sealed class OrbitalCenterShift : MonoBehaviour
    {
        [SerializeField, Min(0f)] private float maximumOffset = 2f;
        [SerializeField, Min(0f)] private float moveSpeed = 6f;
        [SerializeField, Min(0f)] private float returnSpeed = 8f;
        private OrbitalStationRuntime station;
        private CharacterMovement2D movement;
        private Vector3 baseLocalPosition;
        private Func<Vector2, Vector2> filter;
        private Camera pointerCamera;
        private float steeringSpeedMultiplier = .7f;
        private bool pointerHeld, pointerNeedsRelease;
        public bool IsPointerControlConfigured => pointerCamera != null;
        public bool IsPointerSteering => pointerHeld;
        public float MaximumOffset => maximumOffset;
        public Vector3 RangeCenter => station.Owner.Transform.position;

        public void ConfigurePointerControl(Camera camera, float speedMultiplier)
        {
            pointerCamera = camera;
            steeringSpeedMultiplier = Mathf.Clamp(speedMultiplier, .1f, 1f);
        }

        private void AdvancePointer(bool down, bool held, bool overUI, Vector2 world, float deltaTime)
        {
            if (!held) { pointerHeld = false; pointerNeedsRelease = false; }
            if (!CanControl || overUI)
            {
                SuspendPointer();
                if (held) pointerNeedsRelease = true;
            }
            else if (down && !pointerNeedsRelease) pointerHeld = true;
            Vector2 previous = Offset;
            if (pointerHeld)
            {
                // Convert to the same player-relative world axes used by Shift steering.
                Vector2 origin = (Vector2)transform.position - Offset;
                Vector2 target = Vector2.ClampMagnitude(world - origin, maximumOffset);
                Offset = Vector2.MoveTowards(Offset, target, moveSpeed * deltaTime);
                movement.SetAnomalySpeedMultiplier(this, steeringSpeedMultiplier);
                if ((Offset - previous).sqrMagnitude > .000001f) Shifted?.Invoke();
                ApplyPosition();
            }
            else
            {
                movement?.RemoveAnomalySpeedMultiplier(this);
                Advance(Vector2.zero, false, deltaTime);
            }
        }

        private void SuspendPointer()
        {
            if (pointerHeld) pointerNeedsRelease = true;
            pointerHeld = false;
            movement?.RemoveAnomalySpeedMultiplier(this);
        }

        private void OnApplicationFocus(bool focused)
        {
            if (!focused && IsPointerControlConfigured) SuspendPointer();
        }
        private void OnApplicationPause(bool paused)
        {
            if (paused && IsPointerControlConfigured) SuspendPointer();
        }
        public Vector2 Offset { get; private set; }
        public event Action Shifted;
        public bool CanControl => isActiveAndEnabled && station != null && station.IsInitialized &&
            station.Owner.CanControl && !station.IsRunEnded && station.InputOwner.IsIdle && !station.InputOwner.IsGameplayInputBlocked &&
            station.InputOwner.IsRewardQueueIdle &&
            movement != null && movement.isActiveAndEnabled && movement.MovementIntent == null &&
            Application.isFocused && Time.timeScale > 0f;

        public void Bind(OrbitalStationRuntime owner)
        {
            Unbind();
            station = owner;
            baseLocalPosition = transform.localPosition;
            movement = owner.Owner.Transform.GetComponent<CharacterMovement2D>();
            ConfigurePointerControl(owner.InputOwner.PointerCamera, .7f);
            filter = FilterInput;
            AttachInput();
        }

        private void AttachInput()
        {
            if (movement != null && isActiveAndEnabled)
            {
                if (movement.DirectionalInputFilter != null && movement.DirectionalInputFilter != filter)
                    throw new InvalidOperationException("Directional input already has an owner.");
                movement.DirectionalInputFilter = filter;
            }
        }

        private Vector2 FilterInput(Vector2 direction)
        {
            if (station?.InputOwner?.IsGameplayInputBlocked == true)
            {
                SuspendPointer();
                return Vector2.zero;
            }
            // The scene camera is bound after CharacterSpawner creates the station.
            if (pointerCamera == null && station?.InputOwner?.PointerCamera != null)
                ConfigurePointerControl(station.InputOwner.PointerCamera, steeringSpeedMultiplier);
            if (IsPointerControlConfigured)
            {
                Vector3 screen = Input.mousePosition;
                screen.z = -pointerCamera.transform.position.z;
                AdvancePointer(Input.GetMouseButtonDown(1), Input.GetMouseButton(1),
                    EventSystem.current != null && EventSystem.current.IsPointerOverGameObject(),
                    pointerCamera.ScreenToWorldPoint(screen), Time.deltaTime);
                return direction;
            }
            Advance(Vector2.zero, false, Time.deltaTime);
            return direction;
        }

        private void Advance(Vector2 direction, bool held, float deltaTime)
        {
            Vector2 previous = Offset;
            Offset = AdvanceOffset(Offset, direction, held, deltaTime, maximumOffset, moveSpeed, returnSpeed);
            // Moving back, hitting the limit, a pause or holding Shift alone is not completion.
            if (held && direction.sqrMagnitude > .01f && (Offset - previous).sqrMagnitude > .000001f)
                Shifted?.Invoke();
            ApplyPosition();
        }

        public static Vector2 AdvanceOffset(Vector2 offset, Vector2 direction, bool held,
            float deltaTime, float maximum, float move, float returning)
        {
            return held && direction.sqrMagnitude > .01f
                ? Vector2.ClampMagnitude(offset + direction.normalized * move * deltaTime, maximum)
                : Vector2.MoveTowards(offset, Vector2.zero, returning * deltaTime);
        }

        // Called before station combat ticks too, including during pause/transition.
        public void Refresh()
        {
            if (station == null) return;
            if (pointerCamera == null && station.InputOwner?.PointerCamera != null)
                ConfigurePointerControl(station.InputOwner.PointerCamera, steeringSpeedMultiplier);
            if (IsPointerControlConfigured && !CanControl) SuspendPointer();
            if (movement == null || !movement.isActiveAndEnabled || SceneTransitionOverlay.IsTransitioning)
                ResetOffset();
            ApplyPosition();
        }

        private void ApplyPosition()
        {
            // Offset is in world axes, independent of player facing/scale.
            transform.localPosition = baseLocalPosition;
            transform.position += (Vector3)Offset;
        }

        public void Configure(float maximum, float moving, float returning)
        {
            maximumOffset = Mathf.Max(0f, maximum);
            moveSpeed = Mathf.Max(0f, moving);
            returnSpeed = Mathf.Max(0f, returning);
        }

        public void ResetOffset()
        {
            SuspendPointer();
            Offset = Vector2.zero; ApplyPosition();
        }
        public void Unbind()
        {
            if (movement != null && movement.DirectionalInputFilter == filter)
                movement.DirectionalInputFilter = null;
            if (station != null) ResetOffset();
            movement = null;
            station = null;
            filter = null;
            pointerCamera = null;
            pointerNeedsRelease = false;
        }
        private void OnEnable() => AttachInput();
        private void OnDisable()
        {
            if (movement != null && movement.DirectionalInputFilter == filter)
                movement.DirectionalInputFilter = null;
            if (station != null) ResetOffset();
        }
    }
}
