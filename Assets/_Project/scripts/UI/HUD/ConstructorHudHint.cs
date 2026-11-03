using Subject42.Combat.OrbitalStation;
using UnityEngine;
using UnityEngine.UI;

// Authored beside the other HUD counters; the existing cursor Canvas draws above it.
public sealed class ConstructorHudHint : MonoBehaviour
{
    [SerializeField] private HUDManager hud;
    [SerializeField] private CanvasGroup visibility;
    [SerializeField] private Button button;
    private OrbitalStationRuntime station;

    public void Bind(GameObject player)
    {
        station=player!=null ? player.GetComponentInChildren<OrbitalStationRuntime>() : null;
        Refresh();
    }

    private void LateUpdate() => Refresh();

    private void Refresh()
    {
        bool visible=station!=null && station.IsInitialized && station.UsesTelekineticRig && hud.IsInformationVisible;
        visibility.alpha=visible?1f:0f;
        visibility.blocksRaycasts=visible;
        visibility.interactable=visible;
        button.interactable=visible && station.InputOwner.CanOpenConstructor;
    }

    public void Open()
    {
        if (station!=null) station.InputOwner.TryToggleConstructor();
    }
}
