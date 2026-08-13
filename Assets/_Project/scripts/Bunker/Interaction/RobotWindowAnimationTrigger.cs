using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(BoxCollider2D))]
public sealed class RobotWindowAnimationTrigger : MonoBehaviour
{
    [Header("Robot Animation")]
    [SerializeField] private Animator robotAnimator;
    [SerializeField] private string animationStateName = "animRobot2D2";
    [SerializeField] private bool waitForPlayerBeforeAnimation = true;
    [SerializeField, Min(0f)] private float animationStartDelay = 2f;

    [Header("Camera Focus")]
    [SerializeField] private CameraFollow cameraFollow;
    [SerializeField] private Transform focusTarget;
    [SerializeField, Range(0.5f, 1f)] private float zoomMultiplier = 0.85f;
    [SerializeField, Range(0f, 1f)] private float positionStrength = 0.35f;
    [SerializeField, Min(0.01f)] private float focusInDuration = 0.4f;
    [SerializeField, Min(0.01f)] private float focusOutDuration = 0.5f;

    private readonly HashSet<Collider2D> playerColliders = new HashSet<Collider2D>();
    private bool animationStarted;
    private bool animatorHeld;
    private float animatorPlaybackSpeed = 1f;
    private bool stopAfterCurrentLoop;
    private float stopAtNormalizedTime;
    private bool animationStartPending;
    private float animationStartTimer;

    private void Awake()
    {
        if (robotAnimator == null || !waitForPlayerBeforeAnimation)
            return;

        animatorPlaybackSpeed = robotAnimator.speed;
        robotAnimator.speed = 0f;
        animatorHeld = true;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!IsPlayer(other) || !playerColliders.Add(other) || playerColliders.Count != 1)
            return;

        ScheduleRobotAnimation();

        if (cameraFollow != null && focusTarget != null)
        {
            cameraFollow.BeginTemporaryFocus(
                this,
                focusTarget,
                zoomMultiplier,
                positionStrength,
                focusInDuration);
        }
    }

    private void Update()
    {
        UpdatePendingAnimationStart();
        UpdateLoopCompletion();
    }

    private void UpdatePendingAnimationStart()
    {
        if (!animationStartPending)
            return;

        if (playerColliders.Count == 0)
        {
            animationStartPending = false;
            return;
        }

        animationStartTimer -= Time.deltaTime;
        if (animationStartTimer > 0f)
            return;

        animationStartPending = false;
        PlayRobotAnimation();
    }

    private void UpdateLoopCompletion()
    {
        if (!stopAfterCurrentLoop || robotAnimator == null || animatorHeld)
            return;

        AnimatorStateInfo state = robotAnimator.GetCurrentAnimatorStateInfo(0);
        float stateLength = Mathf.Max(0.0001f, state.length);
        float normalizedFrameStep = Time.deltaTime * Mathf.Abs(robotAnimator.speed) / stateLength;

        if (state.normalizedTime + normalizedFrameStep < stopAtNormalizedTime)
            return;

        robotAnimator.Play(animationStateName, 0, 0.9999f);
        robotAnimator.Update(0f);
        PauseRobotAnimation();
        stopAfterCurrentLoop = false;
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (!playerColliders.Remove(other) || playerColliders.Count != 0)
            return;

        animationStartPending = false;
        FinishCurrentLoopThenPause();
        EndCameraFocus();
    }

    private void OnDisable()
    {
        playerColliders.Clear();
        animationStartPending = false;
        PauseRobotAnimation();
        EndCameraFocus();
    }

    private void ScheduleRobotAnimation()
    {
        animationStartTimer = animationStartDelay;
        animationStartPending = true;

        if (animationStartDelay <= 0f)
        {
            animationStartPending = false;
            PlayRobotAnimation();
        }
    }

    private void PlayRobotAnimation()
    {
        if (robotAnimator == null || string.IsNullOrWhiteSpace(animationStateName))
            return;

        stopAfterCurrentLoop = false;
        robotAnimator.Play(animationStateName, 0, 0f);
        ResumeRobotAnimation();
        animationStarted = true;
    }

    private void FinishCurrentLoopThenPause()
    {
        if (robotAnimator == null || !animationStarted || animatorHeld)
            return;

        AnimatorStateInfo state = robotAnimator.GetCurrentAnimatorStateInfo(0);
        stopAtNormalizedTime = Mathf.Floor(state.normalizedTime) + 1f;
        stopAfterCurrentLoop = true;
    }

    private void PauseRobotAnimation()
    {
        if (robotAnimator == null || !animationStarted)
            return;

        robotAnimator.speed = 0f;
        animatorHeld = true;
    }

    private void ResumeRobotAnimation()
    {
        if (robotAnimator == null || !animatorHeld)
            return;

        robotAnimator.speed = animatorPlaybackSpeed;
        animatorHeld = false;
    }

    private void EndCameraFocus()
    {
        if (cameraFollow != null)
            cameraFollow.EndTemporaryFocus(this, focusOutDuration);
    }

    private static bool IsPlayer(Collider2D other)
    {
        if (other == null)
            return false;

        if (other.CompareTag("Player"))
            return true;

        Rigidbody2D attachedBody = other.attachedRigidbody;
        return attachedBody != null && attachedBody.CompareTag("Player");
    }

    private void OnDrawGizmosSelected()
    {
        if (focusTarget == null)
            return;

        Gizmos.color = new Color(0.2f, 0.85f, 1f, 0.9f);
        Gizmos.DrawLine(transform.position, focusTarget.position);
        Gizmos.DrawWireSphere(focusTarget.position, 0.35f);
    }
}
