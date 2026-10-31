using UnityEngine;
using UnityEngine.SceneManagement;

// Authored local feedback; health and movement remain the gameplay owners.
public sealed class PlayerFeedbackPresentation : MonoBehaviour
{
    [SerializeField] private PlayerHealth health;
    [SerializeField] private CharacterMovement2D movement;
    [SerializeField] private ParticleSystem dashEffect;
    [SerializeField] private PlayerWhiteFlash flash;
    [SerializeField] private PlayerHitSound hitSound;
    [SerializeField] private SpriteRenderer[] renderers;
    private CameraShake cameraShake;
    private ParticleSystem activeDashEffect;
    public void BindCamera(CameraShake camera) => cameraShake = camera;
    private void OnEnable()
    {
        health.DamageTaken += OnHit;
        health.Died += OnDeath;
        movement.DashStarted += OnDash;
    }
    private void OnDisable()
    {
        health.DamageTaken -= OnHit;
        health.Died -= OnDeath;
        movement.DashStarted -= OnDash;
        if (activeDashEffect != null) Destroy(activeDashEffect.gameObject);
        activeDashEffect = null;
    }
    private void OnDash()
    {
        AudioService.Instance?.PlayAt(AudioCueId.PlayerDash, transform.position);
        Vector3 direction = movement.LastMoveDirection.normalized;
        activeDashEffect = Instantiate(dashEffect,
            transform.position - direction * .2f + Vector3.up * .25f,
            Quaternion.LookRotation(-direction, Vector3.forward));
        SceneManager.MoveGameObjectToScene(activeDashEffect.gameObject, gameObject.scene);
    }
    private void OnHit()
    {
        flash?.Flash();
        if (!health.IsDead)
        {
            if (AudioService.Instance != null) AudioService.Instance.PlayAt(AudioCueId.PlayerHurt, transform.position);
            else hitSound?.Play();
            cameraShake?.Shake(.12f, .08f);
        }
    }
    private void OnDeath()
    {
        AudioService.Instance?.PlayAt(AudioCueId.PlayerDeath, transform.position);
        cameraShake?.StopAllShakes();
        foreach (var renderer in renderers) if (renderer != null) renderer.enabled = false;
        GameOverManager.Instance?.GameOver();
    }
}
