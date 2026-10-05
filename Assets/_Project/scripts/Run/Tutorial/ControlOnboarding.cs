using Subject42.Combat.OrbitalStation;
using UnityEngine;

// Player-local detector binding. Other abilities can complete their own definitions through meta.
public sealed class ControlOnboarding : MonoBehaviour
{
    [SerializeField] private OnboardingStepDefinition movementStep, dashStep, orbitalShiftStep;
    [SerializeField] private OnboardingHint[] hints;
    [SerializeField] private bool followPlayer;
    private CharacterMovement2D movement;
    private OrbitalCenterShift orbital;
    private MetaProgressionManager progress;

    public void Bind(CharacterMovement2D playerMovement, OrbitalCenterShift center = null)
    {
        Unbind();
        movement = playerMovement;
        orbital = center;
        progress = MetaProgressionManager.EnsureExists();
        progress.TutorialCompleted += OnCompleted;
        foreach (var hint in hints) hint.Present(progress.IsTutorialCompleted(hint.Definition.Id));
        if (isActiveAndEnabled) Subscribe();
    }
    private void Subscribe()
    {
        movement.MovementInputUsed += OnMovement;
        movement.DashStarted += OnDash;
        if (orbital != null) orbital.Shifted += OnShift;
    }
    private void OnEnable() { if (movement != null) Subscribe(); }
    private void OnDisable()
    {
        if (movement != null)
        {
            movement.MovementInputUsed -= OnMovement;
            movement.DashStarted -= OnDash;
        }
        if (orbital != null) orbital.Shifted -= OnShift;
    }
    private void Unbind()
    {
        OnDisable();
        if (progress != null) progress.TutorialCompleted -= OnCompleted;
    }
    private void OnDestroy() => Unbind();
    private void OnMovement() => Complete(movementStep);
    private void OnDash() => Complete(dashStep);
    private void OnShift() => Complete(orbitalShiftStep);
    private void Complete(OnboardingStepDefinition step)
    {
        if (step != null) progress.CompleteTutorial(step.Id);
    }
    private void OnCompleted(string id)
    {
        foreach (var hint in hints) if (hint.Definition.Id == id) hint.Complete();
    }
    private void LateUpdate()
    {
        if (movement == null) { Destroy(gameObject); return; }
        if (followPlayer) transform.position = movement.transform.position;
        bool visible = movement.isActiveAndEnabled && !SceneTransitionOverlay.IsTransitioning && Time.timeScale > 0f &&
            (orbital == null || orbital.CanControl);
        foreach (var hint in hints) hint.SetVisible(visible);
    }
}
