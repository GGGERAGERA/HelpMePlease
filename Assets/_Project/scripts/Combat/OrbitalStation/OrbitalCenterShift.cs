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
        [SerializeField] private KeyCode modifier = KeyCode.LeftShift;
        private OrbitalStationRuntime station;
        private CharacterMovement2D movement;
        private Vector3 baseLocalPosition;
        private Func<Vector2, Vector2> filter;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private Camera labCamera;
        private float labSpeedMultiplier = .7f;
        private bool pointerHeld, pointerNeedsRelease;
        public bool IsLabPointerControl => labCamera != null;
        public bool IsPointerSteering => pointerHeld;

        public void ConfigureLabPointerControl(Camera camera, float speedMultiplier)
        {
            labCamera = camera;
            labSpeedMultiplier = Mathf.Clamp(speedMultiplier, .1f, 1f);
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
                movement.SetAnomalySpeedMultiplier(this, labSpeedMultiplier);
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
            if (!focused && IsLabPointerControl) SuspendPointer();
        }
        private void OnApplicationPause(bool paused)
        {
            if (paused && IsLabPointerControl) SuspendPointer();
        }
#endif
        public Vector2 Offset { get; private set; }
        public event Action Shifted;
        public bool CanControl => isActiveAndEnabled && station != null && station.IsInitialized &&
            station.Owner.CanControl && station.InputOwner.IsIdle && !station.InputOwner.IsGameplayInputBlocked &&
            station.InputOwner.IsRewardQueueIdle &&
            movement != null && movement.isActiveAndEnabled && movement.MovementIntent == null &&
            Application.isFocused && Time.timeScale > 0f;

        public void Bind(OrbitalStationRuntime owner)
        {
            Unbind();
            station = owner;
            baseLocalPosition = transform.localPosition;
            movement = owner.Owner.Transform.GetComponent<CharacterMovement2D>();
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
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (IsLabPointerControl)
            {
                Vector3 screen = Input.mousePosition;
                screen.z = -labCamera.transform.position.z;
                AdvancePointer(Input.GetMouseButtonDown(1), Input.GetMouseButton(1),
                    EventSystem.current != null && EventSystem.current.IsPointerOverGameObject(),
                    labCamera.ScreenToWorldPoint(screen), Time.deltaTime);
                return direction;
            }
#endif
            bool held = CanControl && Input.GetKey(modifier);
            Advance(direction, held, Time.deltaTime);
            return held ? Vector2.zero : direction;
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
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (IsLabPointerControl && !CanControl) SuspendPointer();
#endif
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
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            SuspendPointer();
#endif
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
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            labCamera = null;
            pointerNeedsRelease = false;
#endif
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
