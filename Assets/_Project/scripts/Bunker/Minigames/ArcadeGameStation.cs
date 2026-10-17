using UnityEngine;

[DisallowMultipleComponent]
public sealed class ArcadeGameStation : MonoBehaviour, IBunkerInteractable
{
    [SerializeField] private ArcadeMinigame game;
    public bool CanInteract => isActiveAndEnabled && game != null && game.CanStart &&
        Time.timeScale > 0f && !SceneTransitionOverlay.IsTransitioning &&
        (BunkerContext.Instance == null || !BunkerContext.Instance.Panels.IsAnyPanelOpen);
    public string InteractionText => LocalizationService.Instance != null && LocalizationService.Instance.CurrentLanguage == GameLanguage.English
        ? game is AnomalyPinball ? "PLAY PINBALL" : game is EuropeanRoulette ? "PLAY ROULETTE" : "PLAY DICE POKER"
        : game is AnomalyPinball ? "ИГРАТЬ В ПИНБОЛ" : game is EuropeanRoulette ? "ИГРАТЬ В РУЛЕТКУ" : "ИГРАТЬ В ПОКЕР НА КОСТЯХ";
    public void Interact() { if (CanInteract) game.Open(); }
}
