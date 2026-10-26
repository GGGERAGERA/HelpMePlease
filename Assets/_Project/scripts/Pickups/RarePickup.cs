using UnityEngine;

// Reuses XP's radius, movement, anomaly interactions and collection distance.
public sealed class RarePickup : ExperiencePickup
{
    public enum PickupKind { MedKit, Magnet }
    [SerializeField] private PickupKind kind;
    [SerializeField, Range(0f, 1f)] private float healFraction = 0.25f;

    protected override bool GrantsExperience => false;
    protected override bool CanCollect => PlayerHealth != null && !PlayerHealth.IsDead &&
        ExperienceManager.Instance != null &&
        ExperienceManager.Instance.CanUseRarePickups(gameObject.scene) &&
        (kind == PickupKind.Magnet ||
            (healFraction > 0f && PlayerHealth.CurrentHealth < PlayerHealth.MaxHealth));

    protected override void ApplyPickup()
    {
        var manager = ExperienceManager.Instance;
        if (kind == PickupKind.Magnet)
        {
            manager.ActivateGlobalMagnet(PlayerHealth);
            AudioService.Instance?.PlayAt(AudioCueId.MagnetPickup, transform.position);
            return;
        }

        float before = PlayerHealth.CurrentHealth;
        PlayerHealth.Heal(PlayerHealth.MaxHealth * healFraction);
        manager.ShowHealing(PlayerHealth, PlayerHealth.CurrentHealth - before);
        AudioService.Instance?.PlayAt(AudioCueId.MedkitPickup, transform.position);
    }
}
