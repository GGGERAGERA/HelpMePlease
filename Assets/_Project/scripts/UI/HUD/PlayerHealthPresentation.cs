using UnityEngine;

// Authored sibling of health. No combat state is written by this adapter.
public sealed class PlayerHealthPresentation : MonoBehaviour
{
    [SerializeField] private PlayerHealth health;
    [SerializeField] private PlayerWhiteFlash flash;
    [SerializeField] private PlayerHitSound hitSound;
    [SerializeField] private SpriteRenderer[] renderers;
    private CameraShake cameraShake;
    public void BindCamera(CameraShake camera) => cameraShake = camera;
    private void OnEnable() { health.DamageTaken += OnHit; health.Died += OnDeath; }
    private void OnDisable() { health.DamageTaken -= OnHit; health.Died -= OnDeath; }
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
        foreach (var renderer in renderers) renderer.enabled = false;
        GameOverManager.Instance?.GameOver();
    }
}
