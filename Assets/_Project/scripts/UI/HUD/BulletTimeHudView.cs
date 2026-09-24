using Subject42.Combat.OrbitalStation;
using UnityEngine;
using UnityEngine.UI;

public sealed class BulletTimeHudView : MonoBehaviour
{
    [SerializeField] private HUDManager hud;
    [SerializeField] private CanvasGroup visibility;
    [SerializeField] private HudBar energy;
    [SerializeField] private Image icon;
    [SerializeField] private Color readyColor;
    [SerializeField] private Color activeColor;
    [SerializeField] private Color emptyColor;
    private OrbitalStationRuntime station;
    public void Bind(GameObject player) => station = player != null ? player.GetComponentInChildren<OrbitalStationRuntime>() : null;
    private void LateUpdate()
    {
        bool available = station != null && station.IsInitialized;
        visibility.alpha = available && hud.IsInformationVisible ? 1f : 0f;
        if (!available) return;
        var ability = station.BulletTime;
        energy.SetValue(ability.Energy, 1f);
        icon.color = ability.IsActive ? activeColor : ability.Energy <= 0f ? emptyColor : readyColor;
    }
}
